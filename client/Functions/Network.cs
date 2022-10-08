using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.ComponentModel;

namespace ClientFramework {
	// 0 SendData = Fire And forget
	// 1 RequestData = Send using a ID and read from ResponseData
	// 2 ResponseData = Used to receive the data for ID that was sent using RequestData
	
	
	
    public class Network {
		
		public enum MessageTypes : int {SendData, RequestData, ResponseData}
		public class NetworkMessage
		{
			public int? MessageType { get; set; }
			// One of the tpes in "MessageTypes"
			public int TargetId { get; set; }
			// 0 = everyone, 1 = server, 2 = client 1...
			public int MethodId { get; set; }
			// Minus numbers are for internal use!
			public List<object>? Parameters { get; set; }
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; }
			// Key for getting the response for specific request
			public int Sender { get; set; }
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set;}
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string> Methods = new List<string>() {"Disconnect","ConnectedClients","Test","TestArray"};
		public static Dictionary<int,object[]> Results = new Dictionary<int,object[]>();



		public static int ClientID;
		public static bool IsConnected { get; set; }
		public static bool HandshakeDone { get; set; }
		public static TcpClient Client = default!;
	

		
		//!! METHODS !!//

		public static void Connect(string ip = "127.0.0.1", int port = 2302, string userName = "unknown") {
			if (IsConnected)
				throw new Exception("Already connected to server!");

			// Reset variables (Just to be sure)
			Client = new TcpClient();


			Console.WriteLine("Trying to connect at: (" + ip + ":" + port.ToString() + "), with name: " + userName);
			Client.Connect(IPAddress.Parse(ip), port);
			
			// Request client ID and do handshake
			int _id = Network.Handshake(userName);
			if (_id < 0) return;
			Console.WriteLine($"HANDSHAKE DONE: ID={_id}");

			// Continue in new thread
			Thread thread = new Thread(ReceiveDataThread);
			thread.Start();

		}

		public static void Disconnect() {
			if (!IsConnected)
				throw new Exception("Not connected to server!");

			
			Client.Client.Close();
			IsConnected = false;
		}


		public static void ReceiveDataThread() {
			Console.WriteLine("STARTED ReceiveDataThread");
			try {
				IsConnected = true;
				
				while (IsConnected) {
					NetworkStream Stream = Client.GetStream();
					byte[] bytes = new byte[1024];
					Stream.Read(bytes, 0, 1024);

					var utf8Reader = new Utf8JsonReader(bytes);
                    NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;

					DebugMessage(message);
	
					List<object> paramList = new List<object>();
                    paramList.Add(Client);
                    foreach (object p in DeserializeParameters(message.Parameters)) {
                        paramList.Add(p);
                    }
                    object[] parameters = paramList.ToArray();

					// Dump result to array and continue
					if (message.MessageType == (byte)MessageTypes.ResponseData) {
						Results.Add(message.Key,parameters);
						continue;
					}

					string method = Methods[message.MethodId];
					MethodInfo methodInfo = typeof(ClientMethods).GetMethod(method);
					if (methodInfo == null) throw new Exception($"Method {message.MethodId} was not found ({method})");

					switch (message.MessageType)
					{
						// SEND A REQUEST FOR CLIENT/SERVER
						case (byte)MessageTypes.RequestData:
							object data = methodInfo.Invoke(method,message.Parameters);

							NetworkMessage responseMessage = new NetworkMessage
							{
								MessageType = ((byte)MessageTypes.ResponseData),
								MethodId = message.MethodId,
								TargetId = message.Sender
							};
							if (data != null) responseMessage.Parameters = SerializeParameters(data);
							Network.SendData(responseMessage);
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (byte)MessageTypes.SendData:
							methodInfo.Invoke(method,message.Parameters);
							break;
						default:
							throw new NotImplementedException();
					}
				}
			} catch (Exception ex) {
				if (!IsConnected)
					return;

				if (ex.InnerException is SocketException) {
					Console.WriteLine("Server was shutdown!");
					//Extension.Callmethod("disconnect", "0");
				}
				Client.Client.Close();
				IsConnected = false;
				Console.WriteLine(ex.Message);
			}
		}

		// Fire and forget
		public static void SendData(NetworkMessage message) {
            if (!IsConnected) throw new Exception("Not connected to server");
			if (message.TargetId == ClientID) throw new Exception("Cannot send data to self! (client)");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
			Client.GetStream().Write(msg, 0, msg.Length);
        }


		public static object[] RequestData(NetworkMessage message) {
			if (!IsConnected) throw new Exception("Not connected to server");

			message.MessageType = (int?)MessageTypes.RequestData;
			message.Key = new Random().Next(1,int.MaxValue);
			message.Sender = ClientID;

			DebugMessage(message);

			// Send request
			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
			Client.GetStream().Write(msg, 0, msg.Length);

			
			// Wait for response
			object[] returnData;
			short timer = 0;
			while (true) {
				Thread.Sleep(1);
				if (Results.ContainsKey(message.Key)) {
					returnData = (Results.First(n => n.Key == message.Key)).Value; // Just in case if the index changes in between
					Results.Remove(message.Key);
					break;
				}
				
				if (timer > 100) throw new Exception($"Request {message.Key} ({Methods[message.MethodId]}) timed out!");
				timer++;
			}
			return returnData;
		}
		
		public static int Handshake(string userName) {
			int version = Program.Version;
			Console.WriteLine($"Starting HANDSHAKE with server, with version {version}");

			NetworkMessage handshakeMessage = new NetworkMessage
			{
				TargetId = 1,
				isHandshake = true,
				Parameters = SerializeParameters(version,userName),
				MessageType = (int?)MessageTypes.RequestData,
				Sender = -1
			};

			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(handshakeMessage);
			Client.GetStream().Write(msg, 0, msg.Length);

			NetworkStream Stream = Client.GetStream();
			byte[] bytes = new byte[1024];
			Stream.Read(bytes, 0, 1024);

			var utf8Reader = new Utf8JsonReader(bytes);
			NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;

			DebugMessage(returnMessage);
			object[] returnedParams = DeserializeParameters(returnMessage.Parameters);

			int _clientID = (int)returnedParams[0];
			if (_clientID < 0) {
				if (_clientID == -1) throw new Exception("Version mismatch!");
				if (_clientID == -2) throw new Exception("Username already in use!");
				throw new Exception($"Handshake failed. Code:{_clientID}");
			}

			ClientID = _clientID;
			return _clientID;
		}

		public static int GetMethodIndex(string method) {
		public static List<object> SerializeParameters(params object[] parameters) {
			List<object> newParams = new List<object>();
			foreach (object parameter in parameters) {
				newParams.Add(parameter.GetType().ToString());
				newParams.Add(parameter);
			}
			return newParams;
		}
		public static object[] DeserializeParameters(List<object> parameters) {
			Type type = default;
			List<object> final = new List<object>();
			for (int i = 0; i < parameters.Count(); i++) {
				object value = parameters[i];
				if (i%2 == 0) {
					type = Type.GetType(value.ToString());
					continue;
				}
				object data = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value.ToString());
				final.Add(data);
			}
			return final.ToArray();
		}
		}







		public static void DebugMessage(NetworkMessage message) {
            Console.WriteLine("--------------DEBUG MESSGAE--------------");
            Console.WriteLine($"MessageType:{message.MessageType}");
            Console.WriteLine($"TargetId:{message.TargetId}");
            Console.WriteLine($"MethodId:{message.MethodId}");
            if (message.Parameters != null) {
				int i = 0;
				int ii = 1;
				Console.WriteLine($"Parameters:");
                Type LastType = default;
                foreach (object pr in message.Parameters) {
                    if (i%2 == 0) {
                        i++;
                        LastType = Type.GetType(pr.ToString());
                        continue;
                    }
                    i++;
                    object data = TypeDescriptor.GetConverter(LastType).ConvertFromInvariantString(pr.ToString());
                    Console.WriteLine($"  ({ii}) PARAM: ({data.GetType()}): {data.ToString()}");
                    ii++;
                }
            }
            Console.WriteLine($"Key:{message.Key}");
            Console.WriteLine($"Sender:{message.Sender}");
            Console.WriteLine($"isHandshake:{message.isHandshake}");
            Console.WriteLine("--------------DEBUG MESSGAE--------------");
        }
	}
	
}
