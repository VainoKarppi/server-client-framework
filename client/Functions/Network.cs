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

namespace ClientFramework {
	// SendData = Fire And forget
	// RequestData = Send using a ID and read from ResponseData
	// ResponseData = Used to receive the data for ID that was sent using RequestData
	
	
	
    public class Network {
		
		public enum MessageTypes : int {SendData, RequestData, ResponseData}
		public class NetworkMessage
		{
			public int? MessageType { get; set; }
			// One of the tpes in "MessageTypes"
			public int? TargetId { get; set; }
			// 0 = everyone, 1 = server, 2 = client 1...
			public int MethodId { get; set; }
			// Minus numbers are for internal use!
			public object[]? Parameters { get; set; }
			public int Key { get; set; }
			public int? Sender { get; set; }
			public bool isHandshake { get; set;}
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string> Methods = new List<string>() {"Disconnect","ConnectedClients","Test","TestArray"};
		public static Dictionary<int,object[]> Results = new Dictionary<int,object[]>();



		public static int? ClientID;
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

					Console.WriteLine("ASD");
					var utf8Reader = new Utf8JsonReader(bytes);
                    NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;

					Console.WriteLine($"*RECEIVED* type:{message.MessageType} method:{message.MethodId} key:{message.Key} target:{message.TargetId} params:{message.Parameters}");
					
					message.Parameters = message.Parameters.Concat(new object[] {Client}).ToArray();

					// Dump result to array and continue
					if (message.MessageType == (byte)MessageTypes.ResponseData) {
						Results.Add(message.Key,message.Parameters);
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
							if (data != null) responseMessage.Parameters = new object[] {data};
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
				Parameters = new object[] {version,userName},
				MessageType = (int?)MessageTypes.RequestData,
				Key = new Random().Next(1,int.MaxValue),
				Sender = -1
			};

			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(handshakeMessage);
			Client.GetStream().Write(msg, 0, msg.Length);
			
			Console.WriteLine("2");

			NetworkStream Stream = Client.GetStream();
			byte[] bytes = new byte[1024];
			Stream.Read(bytes, 0, 1024);

			var utf8Reader = new Utf8JsonReader(bytes);
			NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;

			DebugMessage(returnMessage);


			int _clientID = Int32.Parse(returnMessage.Parameters[0].ToString());
			if (_clientID < 0) {
				if (_clientID == -1) throw new Exception("Version mismatch!");
				if (_clientID == -2) throw new Exception("Username already in use!");
				throw new Exception($"Handshake failed. Code:{_clientID}");
			}

			ClientID = _clientID;
			return _clientID;
		}

		public static int GetMethodIndex(string method) {
			return Array.FindIndex(Methods.ToArray(), t => t.Equals(method, StringComparison.InvariantCultureIgnoreCase));
		}


		public static void DebugMessage(NetworkMessage message) {
            Console.WriteLine();
            Console.WriteLine($"MessageType:{message.MessageType}");
            Console.WriteLine($"TargetId:{message.TargetId}");
            Console.WriteLine($"MethodId:{message.MethodId}");
            if (message.Parameters != null) {
				int i = 1;
                foreach (object pr in message.Parameters) {
                    Console.WriteLine($"   ({i}) PARAM: {pr.GetType()} > {pr.ToString()}");
					i++;
                }
            }
            Console.WriteLine($"Key:{message.Key}");
            Console.WriteLine($"Sender:{message.Sender}");
            Console.WriteLine($"isHandshake:{message.isHandshake}");
            Console.WriteLine();
        }
	}
	
}
