using System.Diagnostics.Tracing;
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
		public const int Version = 1000;
		public class EventMessage {
            public int? MessageType { get; set; } = (int)MessageTypes.ServerEvent;
            public int[]? Targets { get; set; } = new int[] {1};
			public dynamic? EventClass { get; set; }
        }
		
		public enum MessageTypes : int {SendData, RequestData, ResponseData, ServerEvent, ClientEvent}
		public class NetworkMessage
		{
			public int? MessageType { get; set; }
			// One of the tpes in "MessageTypes"
			public int? TargetId { get; set; } = 1;
			// 0 = everyone, 1 = server, 2 = client 1...
			// Minus numbers are for internal use!
			public string? MethodName { get; set; }
			public dynamic? Parameters { get; set; }
			public bool UseClass { get; set; } = false;
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; } = new Random().Next(100,int.MaxValue);
			// Key for getting the response for specific request
			public int? Sender { get; set; } = Client.Id;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string>? ClientMethods;
		public static List<string>? ServerMethods;
		private static List<string> PrivateMethods = new List<string>() {"GetMethods","ConnectedClients","HandleEvent"};
		// To be read from handshake (register on server)
		public static Dictionary<int,dynamic> Results = new Dictionary<int,dynamic>();
		public static ClientBase? Client = new ClientBase();



		public class ClientBase : TcpClient {
			public NetworkStream? Stream { get; set; }
			public StreamReader? Reader { get; set; }
        	public StreamWriter? Writer { get; set; }
			public int? Id { get; set;}
			public bool HandshakeDone { get; set; } = false;
			public string UserName { get; set; } = "error (NoName)";
		}
		public static List<NetworkClient> OtherClients = new List<NetworkClient>() {};
		// {ID,USERNAME,CONNECTED}
		public class NetworkClient {
			public int? Id { get; set;}
			public string UserName { get; set; } = "error (NoName)";
			public bool Connected { get; set; } = true;
			public NetworkClient(int id, string name, bool connected = true) {
				this.Id = id;
				this.UserName = name;
				this.Connected = connected;
			}
		}

		
		//!! METHODS !!//
		public static bool IsConnected() {
			if (Client == default || (!Client.Connected || !Client.HandshakeDone)) return false;
			return true;
		}
		public static void Connect(string ip = "127.0.0.1", int port = 2302, string userName = "unknown") {
			if (IsConnected())
				throw new Exception("Already connected to server!");

			if (ClientMethods == null) {
                List<string> methods = typeof(ClientMethods).GetMethods().Select(x => x.Name).ToList<string>();
                methods.RemoveRange((methods.Count() - 4),4);
                ClientMethods = methods;
            }

			Client = new ClientBase();

			Console.WriteLine("Trying to connect at: (" + ip + ":" + port.ToString() + "), with name: " + userName);
			Client.Connect(IPAddress.Parse(ip), port);


			Client.Stream = Client.GetStream();
			Client.Reader = new StreamReader(Client.Stream);
			Client.Writer = new StreamWriter(Client.Stream);
			
			// Request client ID and do handshake
			int _id = Network.Handshake(userName);
			if (_id < 0) return;
			Console.WriteLine($"HANDSHAKE DONE: ID={_id}");

			// Continue in new thread
			Thread thread = new Thread(ReceiveDataThread);
			thread.Start();
		}

		public static void Disconnect() {
			OtherClients = new List<NetworkClient>() {};
			if (!IsConnected())
				throw new Exception("Not connected to server!");
			
			Console.WriteLine("Disconnected From the server!");
			Client.HandshakeDone = false;
			Client.Client.Close();
		}

		public static void SendEvent(EventMessage message) {
            if (!Client.HandshakeDone) throw new Exception("Server not running!");

			SendMessage(message,Client.GetStream());
        }
		public static void ReceiveDataThread() {
			try {
				while (Client.Connected) {
					byte[] bytes = ReadMessageBytes(Client.GetStream());
					if (bytes.Count() == 0) {
						Client.Close();
						throw new Exception("ERROR BYTES!");
					}
					
					var utf8Reader = new Utf8JsonReader(bytes);
                    dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
					string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

					int type = -1;
					if (!Int32.TryParse(property, out type)) continue;
					if (type < 0) continue;

					
					NetworkEvents listener = NetworkEvents.eventsListener;
					if (type == (int)MessageTypes.ServerEvent) {
						var eventBytes = new Utf8JsonReader(bytes);
						
						EventMessage? eventMessage = JsonSerializer.Deserialize<EventMessage>(ref eventBytes)!;
						listener.ExecuteEvent(eventMessage?.EventClass);

						continue;
					}

					// HANDLE EVENT
					var msgBytes = new Utf8JsonReader(bytes);
					NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;
					

					DebugMessage(message,2);
					
					message.Parameters = DeserializeParameters(message.Parameters,message.UseClass);

					listener.ExecuteEvent(new OnMessageReceivedEvent(message));

					// Dump result to array and continue
					if (message.MessageType == (int)MessageTypes.ResponseData) {
						Results.Add(message.Key,message.Parameters);
						continue;
					}
	
					List<object> paramList = new List<object>();
                    paramList.Add(Client);
					if (!(message.Parameters is null)) {
                        paramList.Add(message.Parameters);
                    }
                    object[] parameters = paramList.ToArray();

					// GET METHOD INFO
                    string methodName;
					int methodId;
                    MethodInfo methodInfo;
					bool isInt = int.TryParse(message.MethodName, out methodId);
					if (isInt) {
						if (methodId < 0) {
							methodName = PrivateMethods[Math.Abs(methodId) - 1];
							methodInfo = typeof(Network).GetMethod(methodName);
						} else {
							methodName = ClientMethods[methodId];
							methodInfo = typeof(ClientMethods).GetMethod(methodName);
							if (methodInfo == null) throw new Exception($"Method {methodId} was not found ({methodName})");
						}
					} else {
						methodName = ClientMethods.FirstOrDefault(x => x.ToLower() == message.MethodName.ToLower());
						methodInfo = typeof(ClientMethods).GetMethod(methodName);
						if (methodInfo == null) throw new Exception($"Method {methodName} was not found");
					}
                    

					switch (message.MessageType)
					{
						// SEND A REQUEST FOR CLIENT/SERVER
						case (int)MessageTypes.RequestData:
							NetworkMessage responseMessage = new NetworkMessage {
								MessageType = (int)MessageTypes.ResponseData,
								MethodName = message.MethodName,
								TargetId = message.Sender,
								Key = message.Key
							};
							object? data = methodInfo?.Invoke(methodName,parameters);
							if (data != null) responseMessage.Parameters = data;
							Network.SendData(responseMessage);
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (int)MessageTypes.SendData:
							methodInfo?.Invoke(methodName,parameters);
							break;
						default:
							throw new NotImplementedException();
					}
				}
			} catch (Exception ex) {
				if (!Client.HandshakeDone) {
					return;
				}
				Console.WriteLine(ex.Message);

				if (ex.InnerException is SocketException) {
					if (Client.Id != null) Console.WriteLine("Server has crashed!");
				}
				Console.WriteLine("Disconnected from the server!");
				Client.Client.Close();
			}
		}
		// Fire and forget
		public static void SendData(NetworkMessage message) {
            if (!IsConnected()) throw new Exception("Not connected to server");
			if (message.TargetId == Client.Id) throw new Exception("Cannot send data to self! (client)");	
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

			if (message.TargetId != 1) {
				if (!ClientMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in CLIENT's methods list");
			} else {
				if (!ServerMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
			}
			if (message.TargetId == 0) Console.WriteLine($"DATA SENT TO: ({OtherClients.Count()}) CLIENT(s)!");
			SendMessage(message,Client.GetStream());
			DebugMessage(message,1);
        }

		public static void SendMessage(dynamic message, NetworkStream Stream) {
			NetworkEvents listener = NetworkEvents.eventsListener;
			listener.ExecuteEvent(new OnMessageSentEvent(message));
			if (message is NetworkMessage && !(message.Parameters is null)) {
                bool useClass = false;
                message.Parameters = SerializeParameters(message.Parameters,ref useClass);
                message.UseClass = useClass;
            }
			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
			byte[] lenght = BitConverter.GetBytes((ushort)msg.Length);
			byte[] bytes = lenght.Concat(msg).ToArray();
			Stream.WriteAsync(bytes,0,bytes.Length);
		}
		public static byte[] ReadMessageBytes(NetworkStream Stream) {
			byte[] lenghtBytes = new byte[2];
			Stream.Read(lenghtBytes,0,2);
			ushort msgLenght = BitConverter.ToUInt16(lenghtBytes,0);
			byte[] bytes = new byte[msgLenght];
			Stream.Read(bytes,0,msgLenght);
			return bytes;
		}
		public static dynamic RequestDataResult(NetworkMessage message) {
			dynamic returnMessage;
			short timer = 0;
			while (true) {
				Thread.Sleep(1);
				if (Results.ContainsKey(message.Key)) {
					returnMessage = (Results.First(n => n.Key == message.Key)).Value; // Just in case if the index changes in between
					Results.Remove(message.Key);
					break;
				}
				if (timer > 100) throw new Exception($"Request {message.Key} ({message.MethodName}) timed out!");
				timer++;
			}
			return returnMessage;
		}
		public static dynamic RequestData(NetworkMessage message) {
			if (!IsConnected()) throw new Exception("Not connected to server");
			if (message.TargetId == Client.Id) throw new Exception("Cannot request data from self!");
			if (message.TargetId != 1) {
				if ((OtherClients.SingleOrDefault(x => x.Id == message.TargetId)) == default) throw new Exception("Invalid target ID. ID not listed in clients list!");
				if (!ClientMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in CLIENT's methods list");
			} else {
				if (!ServerMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
			}
			message.MessageType = (int?)MessageTypes.RequestData;
			SendMessage(message,Client.GetStream());
			DebugMessage(message,1);
			return RequestDataResult(message);
		}
		public static dynamic RequestData<T>(NetworkMessage message) {
			if (!IsConnected()) throw new Exception("Not connected to server");
			if (message.TargetId == Client.Id) throw new Exception("Cannot request data from self!");
			if (message.TargetId != 1) {
				if (!ClientMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in CLIENT's methods list");
			} else {
				if (!ServerMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
			}
			message.MessageType = (int?)MessageTypes.RequestData;
			SendMessage(message,Client.GetStream());
			DebugMessage(message,1);
			dynamic returnMessage = RequestDataResult(message);
			if (returnMessage is JsonElement) {
				return ((JsonElement)returnMessage[1]).Deserialize<T>();
			}
			return (T)returnMessage;
		}
		
		public static int Handshake(string userName) {
			int version = Network.Version;
			Client.UserName = userName;
			Console.WriteLine($"Starting HANDSHAKE with server, with version: {version}, with name: {userName}");

			NetworkMessage handshakeMessage = new NetworkMessage {
				MessageType = (int?)MessageTypes.RequestData,
				TargetId = 1,
				isHandshake = true,
				Parameters = new object[] {version,userName,ClientMethods.ToArray()},
				Sender = -1
			};

			SendMessage(handshakeMessage,Client.GetStream());
			byte[] bytes = ReadMessageBytes(Client.GetStream());
			if (bytes.Count() == 0) {
				Console.WriteLine("ERROR HANDSHAKE");
				Client.Client.Close();
				return -1;
			}

			var utf8Reader = new Utf8JsonReader(bytes);
			NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;
			object[] returnedParams = DeserializeParameters(returnMessage.Parameters);

			int _clientID = (int)returnedParams[0];
			if (_clientID < 0) {
				if (_clientID == -2) throw new Exception("Version mismatch!");
				if (_clientID == -3) throw new Exception("Username already in use!");
				throw new Exception($"Handshake failed. Code:{_clientID}");
			}

			Client.Id = _clientID;
			Client.UserName = userName;
			
			object[] methods = (object[])returnedParams[1];
			foreach (string method in methods) {
				if (ServerMethods == null) ServerMethods = new List<string>();
				ServerMethods.Add(method);
			}
			Console.WriteLine($"DEBUG: Added ({ServerMethods.Count()}) SERVER methods to list!");
			
			object[] clients = (object[])returnedParams[2];
			foreach (object[] clientData in clients) {
				OtherClients.Add(new NetworkClient((int)clientData[0], (string)clientData[1]));
			}
			Console.WriteLine($"DEBUG: Added ({OtherClients.Count()}) other clients to list!");

			NetworkMessage handshakeMessageSuccess = new NetworkMessage {
				MessageType = (int?)MessageTypes.SendData,
				TargetId = 1,
				isHandshake = true,
				Sender = Client.Id
			};
			SendMessage(handshakeMessageSuccess,Client.GetStream());

			Client.HandshakeDone = true;

			return _clientID;	
		}

		// new object[]{data,"test"} --> ["System.Object[]",[type,data],"System.String","text"]
		// "data" --> ["System.String","text"]
		public static object[] SerializeParameters(dynamic parameters,ref bool useClass) {
            try {
                Type paramType = Type.GetType(parameters.ToString());
                useClass = paramType.GetConstructor(new Type[0])!=null;
                if (useClass) return parameters;
            } catch {}
            
			List<object> newParams = new List<object>();
			if (!(parameters is Array)) {
				newParams.Add(parameters.GetType().ToString());
				newParams.Add(parameters);
			} else {
				newParams.Add(parameters.GetType().ToString());
				foreach (object parameter in parameters) {
					newParams.Add(parameter.GetType().ToString());
					newParams.Add(parameter);
				}
			}
			return newParams.ToArray();
		}
		public static dynamic DeserializeParameters(dynamic parameterData,bool isClass = false) {
			if(parameterData is null) return null;
			if (isClass) return parameterData;
            List<object> parameters = JsonSerializer.Deserialize<List<object>>(parameterData);
            bool odd = parameters.Count()%2 != 0;
            if (odd && parameters.Count() > 2) {
                parameters.RemoveAt(0);
            }
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
					object dataTemp = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value.ToString());
					final.Add(dataTemp);
				}
			}
            if (!odd) {
                return final[0];
            }
			return final.ToArray();
		}



        public static void DebugMessage(NetworkMessage message,int mode = 0) {
			Console.WriteLine();
            Console.WriteLine("===============DEBUG MESSAGE===============");
            string type = "UNKNOWN";
            if (mode == 1) {
                type = "OUTBOUND";
            } else if (mode == 2) {
                type = "INBOUND";
            }
            Console.WriteLine($"TYPE: {type}");
            Console.WriteLine($"TIME: {DateTime.Now.Millisecond}");
            Console.WriteLine($"MessageType:{message.MessageType}");
            Console.WriteLine($"TargetId:{message.TargetId}");
            Console.WriteLine($"MethodName:{message.MethodName}");
			Console.WriteLine($"IsClass:{message.UseClass}");
            Console.WriteLine();
            Console.WriteLine(JsonSerializer.Serialize<object>(message.Parameters));
            Console.WriteLine();
            /*
            if (!(message.Parameters is null)) {
                object[] parameters = message.Parameters.ToArray();
				int i = 0;
				int ii = 1;
				Console.WriteLine($"Parameters ({parameters.Count()}):");
                Type LastType = default;
                foreach (dynamic pr in parameters) {
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
                    if (data.GetType().IsArray) {
                        Console.WriteLine($"  ({ii}): ({data.GetType()}): {JsonSerializer.Serialize<object>(data)}");
                    } else {
                        Console.WriteLine($"  ({ii}): ({data.GetType()}): {data.ToString()}");
                    } 
                    ii++;
                }
            }*/
            Console.WriteLine($"Key:{message.Key}");
            Console.WriteLine($"Sender:{message.Sender}");
            Console.WriteLine("===============DEBUG MESSAGE===============");
			Console.WriteLine();
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
