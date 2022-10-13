using System.Reflection.Metadata;
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
using System.Text.Json.Nodes;

namespace ClientFramework {
	// 0 SendData = Fire And forget
	// 1 RequestData = Send using a ID and read from ResponseData
	// 2 ResponseData = Used to receive the data for ID that was sent using RequestData
	
	
	
    public class Network {
		public static event EventHandler<Events.ServerEventMessage> ?ClientConnected;
		public class EventMessage {
            public int? MessageType { get; set; } = (int)MessageTypes.ServerEvent;
            public int[]? Targets { get; set; }
            // Who to sent the event to
            public string? EventName { get; set; }
            // OnPlayerConnected etc
            public List<object>? Parameters { get; set; } = new List<object>() {};

        }
		
		public enum MessageTypes : int {SendData, RequestData, ResponseData, ServerEvent, ClientEvent}
		public class NetworkMessage
		{
			public int? MessageType { get; set; }
			// One of the tpes in "MessageTypes"
			public int? TargetId { get; set; } = 1;
			// 0 = everyone, 1 = server, 2 = client 1...
			public int MethodId { get; set; }
			// Minus numbers are for internal use!
			public List<object>? Parameters { get; set; } = new List<object>() {};
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; } = new Random().Next(100,int.MaxValue);
			// Key for getting the response for specific request
			public int? Sender { get; set; } = ClientID;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string> ClientMethods = new List<string>() {};
		private static List<string> PrivateMethods = new List<string>() {"GetMethods","ConnectedClients","HandleEvent"};
		// To be read from handshake (register on server)
		public static Dictionary<int,object[]> Results = new Dictionary<int,object[]>();



		public static int ClientID;
		public static bool IsConnected { get; set; }
		public static bool HandshakeDone { get; set; }
		public static TcpClient Client = default!;
		public static List<OtherClient> OtherClients = new List<OtherClient>() {};
		// {ID,USERNAME,CONNECTED}
		public class OtherClient {
			public int? Id { get; set;}
			public string UserName { get; set; } = "error (NoName)";
			public bool Connected { get; set; } = true;
			public OtherClient(int id, string name, bool connected = true) {
				this.Id = id;
				this.UserName = name;
				this.Connected = connected;
			}
		}

		
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
					byte[] bytes = ReadMessage(Client.GetStream());
					Console.WriteLine("MSG RECIEVED!");
					Console.WriteLine(bytes.Count());

					var utf8Reader = new Utf8JsonReader(bytes);
                    dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
					Console.WriteLine((JsonElement)messageTemp);
					string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

					int type = -1;
					if (!Int32.TryParse(property, out type)) continue;
					if (type < 0) continue;

					Console.WriteLine(type);

					//TODO start in new thread?
					

					if (type == (int)MessageTypes.ServerEvent) {
						EventMessage? eventMessage = (EventMessage)messageTemp;
						HandleEvent(eventMessage);
						continue;
					}

					NetworkMessage? message = (NetworkMessage)messageTemp;
					DebugMessage(messageTemp);
					object[] deserialisedParams = DeserializeParameters(message.Parameters);

					// Dump result to array and continue
					if (message.MessageType == (int)MessageTypes.ResponseData) {
						Results.Add(message.Key,deserialisedParams);
						continue;
					}
	
					List<object> paramList = new List<object>();
                    paramList.Add(Client);
                    foreach (object p in deserialisedParams) {
                        paramList.Add(p);
                    }
                    object[] parameters = paramList.ToArray();

					

					// GET METHOD INFO
                    string method;
                    MethodInfo methodInfo;
                    if (message.MethodId < 0) {
                        method = PrivateMethods[Math.Abs(message.MethodId) - 1];
                        methodInfo = typeof(Network).GetMethod(method);
                    } else {
                        method = ClientMethods[message.MethodId];
                        methodInfo = typeof(ClientMethods).GetMethod(method);
                        if (methodInfo == null) throw new Exception($"Method {message.MethodId} was not found ({method})");
                    }

					switch (message.MessageType)
					{
						// SEND A REQUEST FOR CLIENT/SERVER
						case (int)MessageTypes.RequestData:
							object data = methodInfo?.Invoke(method,parameters);

							NetworkMessage responseMessage = new NetworkMessage
							{
								MessageType = ((int)MessageTypes.ResponseData),
								MethodId = message.MethodId,
								TargetId = (int?)message.Sender
							};
							if (data != null) responseMessage.Parameters = SerializeParameters(data);
							Network.SendData(responseMessage);
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (int)MessageTypes.SendData:
							methodInfo?.Invoke(method,parameters);
							break;
						case (int)MessageTypes.ClientEvent:
							throw new NotImplementedException();
							
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
				Console.WriteLine(ex);
			}
		}
		public static void HandleEvent(EventMessage message) {
			Console.WriteLine(message.ToString());
			Console.WriteLine("HEEELP");
			string eventName = message?.EventName;
			Console.WriteLine(eventName);
			Console.WriteLine(message?.Parameters);
			object[] parameters = DeserializeParameters(message?.Parameters);
			Console.WriteLine("HEEELP2");

			var da = new Events.ServerEventMessage();
			da.ClientID = 2;
			da.Code = 5;
			da.IsSuccessful = true;
			da.CompletionTime = DateTime.Now;
			switch (eventName.ToLower()) {
				case "onclientconnected":
					ClientConnected?.Invoke(Network.ClientConnected,da);
					break;
				case "onclientdisconnected":
					break;
				case "onservershutdown":
					break;
				case "onmessagesent":
					break;
				case "onmessagereceived":
					break;
				case "onhandshakestart":
					break;
				case "onhandshakeend":
					break;
				default:
					throw new NotImplementedException();
			}
			//OnUserConnected(int ID, string name);
			//OnUserDisconnected(int ID, int reason);
			//OnServerShutdown();
			//OnMessageSent();
			//OnMessageReceived();


		}
		// Fire and forget
		public static void SendData(NetworkMessage message) {
            if (!IsConnected) throw new Exception("Not connected to server");
			if (message.TargetId == ClientID) throw new Exception("Cannot send data to self! (client)");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

			DebugMessage(message);

			SendMessage(message,Client.GetStream());
			
        }

		public static void SendMessage(dynamic message, NetworkStream Stream) {
			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
			byte[] lenght = BitConverter.GetBytes((ushort)msg.Length);
			byte[] bytes = lenght.Concat(msg).ToArray();
			Console.WriteLine(bytes.Length);
			Stream.WriteAsync(bytes, 0, bytes.Length);
		}
		public static byte[] ReadMessage(NetworkStream Stream) {
			byte[] lenghtBytes = new byte[2];
			Stream.Read(lenghtBytes,0,2);
			ushort msgLenght = BitConverter.ToUInt16(lenghtBytes,0);
			Console.WriteLine(msgLenght);
			byte[] bytes = new byte[msgLenght];
			Stream.Read(bytes,0,msgLenght);
			return bytes;
		}

		public static object[] RequestData(NetworkMessage message) {
			if (!IsConnected) throw new Exception("Not connected to server");

			message.MessageType = (int?)MessageTypes.RequestData;

			DebugMessage(message);

			// Send request
			SendMessage(message,Client.GetStream());

			
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
				
				if (timer > 100) throw new Exception($"Request {message.Key} ({ClientMethods[message.MethodId]}) timed out!");
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

			SendMessage(handshakeMessage,Client.GetStream());

			NetworkStream Stream = Client.GetStream();
			byte[] bytes = ReadMessage(Client.GetStream());
			Console.WriteLine("MSG RECIEVED!");
			Console.WriteLine(bytes.Count());

			var utf8Reader = new Utf8JsonReader(bytes);
			NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;

			object[] returnedParams = DeserializeParameters(returnMessage.Parameters);

			int _clientID = (int)returnedParams[0];
			if (_clientID < 0) {
				if (_clientID == -1) throw new Exception("Version mismatch!");
				if (_clientID == -2) throw new Exception("Username already in use!");
				throw new Exception($"Handshake failed. Code:{_clientID}");
			}

			ClientID = _clientID;
			object[] methods = (object[])returnedParams[1];
			foreach (string method in methods) {
				ClientMethods.Add(method);
			}
			Console.WriteLine($"DEBUG: Added ({ClientMethods.Count()}) methods to list!");

			object[] clients = (object[])returnedParams[2];
			foreach (object[] clientData in clients) {
				OtherClients.Add(new OtherClient((int)clientData[0], (string)clientData[1]));
			}
			Console.WriteLine($"DEBUG: Added ({OtherClients.Count()}) other clients to list!");

			return _clientID;	
		}

		public static int GetMethodIndex(string method) {
			return ClientMethods.FindIndex(m => m.ToLower() == method.ToLower());
		}
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
				if (type.IsArray) {
					object[] data = ParseParamArray(value.ToString());
					final.Add(data);
				} else {
					object data = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value.ToString());
					final.Add(data);
				}
			}
			return final.ToArray();
		}



		public static void DebugMessage(NetworkMessage message) {
            Console.WriteLine("--------------DEBUG MESSGAE--------------");
			Console.WriteLine($"TIME: {DateTime.Now.Millisecond}");
			string jsonString = JsonSerializer.Serialize(message);
            Console.WriteLine(message);
            Console.WriteLine($"MessageType:{message.MessageType}");
            Console.WriteLine($"TargetId:{message.TargetId}");
            Console.WriteLine($"MethodId:{message.MethodId}");
            if (message.Parameters != null) {
				int i = 0;
				int ii = 1;
				Console.WriteLine($"Parameters ({message.Parameters.Count()}):");
                Type LastType = default;
                foreach (object pr in message.Parameters) {
                    if (i%2 == 0) {
                        i++;
                        LastType = Type.GetType(pr.ToString());
                        continue;
                    }
                    i++;
                    object data = default;
                    if (LastType.IsArray) {
                        data = pr;
                    } else {
                        data = TypeDescriptor.GetConverter(LastType).ConvertFromInvariantString(pr.ToString());
                    }
                    
                    Console.WriteLine($"  ({ii}) PARAM: ({data.GetType()}): {data.ToString()}");
                    ii++;
                }
            }
            Console.WriteLine($"Key:{message.Key}");
            Console.WriteLine($"Sender:{message.Sender}");
            Console.WriteLine($"isHandshake:{message.isHandshake}");
            Console.WriteLine("--------------DEBUG MESSGAE--------------");
        }
	
		public static object[] ParseParamArray(string args) {
			if (args.ElementAt(0) != '[') 
				args = "[" + args + "]";
				
			args = args.Substring(1);

			List<object> array = new List<object>();
			char[] nums = new char[] {'0','1','2','3','4','5','6','7','8','9','0','.','-'};
			for (int i = 0; i < args.Length; i++) {
				if (args[i] == '[') {
					StringBuilder str = new StringBuilder();
					int inArr = 0;
					while (true) {
						if (args[i] == '[') inArr++;
						if (args[i] == ']') inArr--;
						str.Append(args[i]);
						i++;
						if (inArr == 0) break;
					}
					object[] innerArray = ParseParamArray(str.ToString());
					array.Add(innerArray);
				} else if (args[i] == '"') {
					StringBuilder str = new StringBuilder();
					bool isEnd = false;
					i++;
					while (true) {
						try {
							if (args[i] == '"') {
								isEnd = !isEnd;
							}
						} catch {
							break;
						}
						if (isEnd && (args[i] == ',' || args[i] == ']')) {
							break;
						}
						str.Append(args[i]);
						i++;
					}
					array.Add(str.ToString().TrimEnd('"'));
				} else if (nums.Contains(args[i])) {
					StringBuilder str = new StringBuilder();
					bool isFloat = false;
					while (nums.Contains(args[i])) {
						if (args[i] == '.')
							isFloat = true;
						str.Append(args[i]);
						i++;
					}
					if (isFloat) {
						double num = Convert.ToDouble(str.ToString());
						array.Add(num);
					} else {
						int num = Convert.ToInt32(str.ToString());
						array.Add(num);
					}
				} else if (
					Substring(args, i, 4).ToLower() == "true") {
					array.Add(true);
					i = i + 4;
				} else if (Substring(args, i, 5).ToLower() == "false") {
					array.Add(false);
					i = i + 5;
				}
			}
			return array.ToArray();
		}
		private static string Substring(string input, int start, int length) {
			int inputLength = input.Length;
			if (start + length >= inputLength) {
				return input.Substring(start);
			}
			return input.Substring(start, length);
		}
	}
	
}
