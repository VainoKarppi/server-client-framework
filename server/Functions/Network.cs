using System.Reflection.Metadata;
using System.Data;
using System.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ServerFramework {
    public class Log {
		public static void Write(object data) {
			if (!Network.Debug) return;
			Console.WriteLine(data);
		}
	}
    public class Network {
        public static bool Debug = true;
        public const int Version = 1000;
        public static TcpListener? ServerListener;
        public static readonly object _lock = new object();
        public static readonly List<NetworkClient> ClientList = new List<NetworkClient>();
        public static bool ServerRunning { get; set; }
        public enum MessageTypes : int {SendData, RequestData, ResponseData, ServerEvent, ClientEvent}
        private class NetworkEvent {
            public int? MessageType { get; set; } = (int)MessageTypes.ServerEvent;
            public int[]? Targets { get; set; } = new int[] {1};
			public dynamic? EventClass { get; set; }
			public NetworkEvent (dynamic data = null) {
				EventClass = data;
			}
        }
        
		public class NetworkMessage {
			public int? MessageType { get; set; } = 0;
			// One of the tpes in "MessageTypes"
			public int? TargetId { get; set; } = 0;
			// 0 = everyone, 1 = server, 2 = client 1...
            public string? MethodName { get; set; }
			// Minus numbers are for internal use!
			public dynamic? Parameters { get; set; }
            public bool UseClass { get; set; } = false;
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; } = new Random().Next(100,int.MaxValue);
			// Key for getting the response for specific request (0-100) = event id
			public int? Sender { get; set; } = 1;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
		}
        public static List<object[]>? ServerMethodsNEW; 
        public static List<string>? ServerMethods;
        public static List<string>? ServerVoidMethods;
        public static List<string>? ClientMethods;
        private static List<string> PrivateMethods = new List<string>() {};
		public static Dictionary<int,dynamic> Results = new Dictionary<int,dynamic>();

        public static void StartServer(int serverPort) {
            if (ServerRunning)
                throw new Exception("Server already running!");
            
            if (ServerMethods == null) {
                MethodInfo[] methodInfos = typeof(ServerMethods).GetMethods();
                List<string> ServerVoidMethods = methodInfos.Where(x => (x.ReturnType == typeof(void))).Select(x => x.Name).ToList<string>();
                List<string> methods = methodInfos.Select(x => x.Name).ToList<string>();
                methods.RemoveRange((methods.Count() - 4),4);
                ServerMethods = methods;

                ServerMethodsNEW = new List<object[]> ();
                foreach (MethodInfo method in methodInfos) {
                    ServerMethodsNEW.Add(new object[]{method.Name,method.ReturnType.ToString()});
                }
                ServerMethodsNEW.RemoveRange(ServerMethodsNEW.Count() - 4,4);
            }

            new Thread(() => {
                Thread.CurrentThread.IsBackground = true;
                
                ServerListener = new TcpListener(IPAddress.Any, serverPort);
                ServerListener.Start();
                ServerRunning = true;

                Log.Write("Running server at port: " + ServerListener.LocalEndpoint.ToString().Split(':')[1]);

                int _clientID = 2; // (0 = All clients, 1 = server, 2 and above for specific clients)
                while (ServerRunning) {
                    try {
                        // Start accepting clients
                        NetworkClient _client = new NetworkClient(ServerListener);
                        _client.Id = _clientID;
                        // Make sure the connection is already not created
                        if(ClientList.Contains(_client)) {
                            _client.Close();
                            throw new Exception("Client already connected!");
                        }
                        lock (_lock) ClientList.Add(_client);
                        
                        Log.Write("*NEW* Client (" + _client.Client.RemoteEndPoint  + ") trying to connect...");

                        if (_clientID >= 32000) throw new Exception("Max user count reached! (32000)");

                        // Start new thread for each client
                        new Thread(() => HandleClient(_client)).Start();
                        _clientID++;;
                    } catch (Exception ex) {
                        if (!(ex is SocketException)) {
                            Log.Write(ex.Message);
                        }
                    }
                }
            }).Start();
        }
        public static void StopServer() {
            if (!ServerRunning)
                throw new Exception("Server not running!");
            
            Log.Write("Stopping server...");

            NetworkEvent message = new NetworkEvent {
                EventClass = new OnServerShutdownEvent(true)
            };
            SendEvent(message);
            
            ServerRunning = false;
            ServerListener.Server.Dispose();
            ServerListener.Server.Close();
            ServerListener = null;
            foreach (NetworkClient client in ClientList) {
                client.Close();
            }
            ClientList.Clear();

            Log.Write("Server stopped!");
        }


        // Request ID is used to answer to specific message

        private static void SendEvent(NetworkEvent message) {
            if (!ServerRunning) throw new Exception("Server not running!");

            // Add ALL clients to list if left as blank
			if (message.Targets == default) {
                List<int> targets = new List<int>(){};
                foreach (NetworkClient client in ClientList) {
                    targets.Add(client.Id);
                }
                message.Targets = targets.ToArray();
            }

            // Send to single or multiple users
            foreach (int id in message.Targets) {
                NetworkClient client = ClientList.FirstOrDefault(c => c.Id == id);
                if (client == default) continue;
                
                SendMessage(message,client.Stream);
            }
        }

        private static void SendMessage(dynamic message, NetworkStream Stream) {
            if (!message.isHandshake) {
				NetworkEvents listener = NetworkEvents.eventsListener;
				listener.ExecuteEvent(new OnMessageSentEvent(message));
			}
			if (message is NetworkMessage && !(message.Parameters is null) && message.Sender == 1) {
                bool useClass = false;
                message.Parameters = SerializeParameters(message.Parameters,ref useClass);
                message.UseClass = useClass;
            }

			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
			byte[] lenght = BitConverter.GetBytes((ushort)msg.Length);
			byte[] bytes = lenght.Concat(msg).ToArray();
			Stream.WriteAsync(bytes,0,bytes.Length);
		}
        private static byte[] ReadMessageBytes(NetworkStream Stream) {
			byte[] lenghtBytes = new byte[2];
			Stream.Read(lenghtBytes,0,2);
			ushort msgLenght = BitConverter.ToUInt16(lenghtBytes,0);
			byte[] bytes = new byte[msgLenght];
			Stream.Read(bytes,0,msgLenght);
			return bytes;
		}
        
        public static void SendData(NetworkMessage message) {
            if (!ServerRunning) throw new Exception("Server not running!");
			if (message.TargetId == 1) throw new Exception("Cannot send data to self (server)!");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

            if (message.MessageType != (int)MessageTypes.ResponseData) {
				if (!ClientMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in CLIENT's methods list");
            }
            
            // Send to single ro multiple users
            if (message.TargetId > 0) {
                NetworkClient client = ClientList.FirstOrDefault(c => c.Id == message.TargetId);
                if (client == default) throw new Exception("Invalid target!");
                SendMessage(message,client.Stream);
                int mode = message.Sender == 1?1:3;
                DebugMessage(message,mode);
            } else {
                int i = 0;
                foreach (NetworkClient client in ClientList) {
                    if (message.Sender == client.Id) continue;
                    SendMessage(message,client.Stream);
                    i++;
                }
                if (message.Sender == 1) Log.Write($"DATA SENT TO {i} USERS(s)!");
                else Log.Write($"DATA FORWARDED TO {i} USERS(s)!");
            }
        }
        
        private static dynamic RequestDataResult(NetworkMessage message) {
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
			if (!ServerRunning) throw new Exception("Server Not running!");
            if (message.TargetId == 1) throw new Exception("Cannot request data from self!");
            if (message.MessageType != (int)MessageTypes.ResponseData) {
				if (!ClientMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in CLIENT's methods list");
            }
			message.MessageType = (int?)MessageTypes.RequestData;

            NetworkClient client = ClientList.FirstOrDefault(client => client.Id == message.TargetId);
            if (client == default) throw new Exception("Invalid target!");

			
			SendMessage(message,client.Stream);
            DebugMessage(message,1);

			return RequestDataResult(message);
		}
		public static dynamic RequestData<T>(NetworkMessage message) {
			if (!ServerRunning) throw new Exception("Server Not running!");
            if (message.TargetId == 1) throw new Exception("Cannot request data from self!");
            if (message.MessageType != (int)MessageTypes.ResponseData) {
				if (!ClientMethods.Contains(message.MethodName,StringComparer.OrdinalIgnoreCase)) throw new Exception($"Method {message.MethodName} not listed in CLIENT's methods list");
            }
			message.MessageType = (int?)MessageTypes.RequestData;

            NetworkClient client = ClientList.FirstOrDefault(client => client.Id == message.TargetId);
            if (client == default) throw new Exception("Invalid target!");
            
			
			SendMessage(message,client.Stream);
            DebugMessage(message,1);

			dynamic returnMessage = RequestDataResult(message);
			if (returnMessage is JsonElement) {
				return ((JsonElement)returnMessage[1]).Deserialize<T>();
			}
			return (T)returnMessage;
		}

        // One thread for one user
        // Start listening data coming from this client
        private static void HandleClient(NetworkClient _client) {
            while (true) {
                try {
                    byte[] bytes = ReadMessageBytes(_client.Stream);
					
                    if (bytes.Count() == 0) {
                        throw new Exception($"ERROR BYTES IN CLIENT: {_client.Id} RECEIVE DATA THREAD!");
                    };
                    
                    var utf8Reader = new Utf8JsonReader(bytes);
                    dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
					string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

					int type = -1;
					if (!Int32.TryParse(property, out type)) continue;
					if (type < 0) continue;

                    if (type == (int)MessageTypes.ServerEvent) {
						var eventBytes = new Utf8JsonReader(bytes);
						NetworkEvent? eventMessage = JsonSerializer.Deserialize<NetworkEvent>(ref eventBytes)!;
						SendEvent(eventMessage);
						continue;
					}
					var msgBytes = new Utf8JsonReader(bytes);
					NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;

                    // FORWARD DATA IF NOT MENT FOR SERVER (+forget)
                    if (message.TargetId != 1) {
                        Network.SendData(message);
                        continue;
                    }

					DebugMessage(message,2);

					message.Parameters = DeserializeParameters(message.Parameters,message.UseClass);

                    NetworkEvents listener = NetworkEvents.eventsListener;
                    
                    // HANDLE HANDSHAKE
                    if (message.isHandshake) {
                        // Return of successfull handshake
                        if (message.MessageType == (int)MessageTypes.SendData) {
                            if (message.Sender > 1) {
                                NetworkEvent eventMessage = new NetworkEvent {
                                    Targets = ClientList.Select(x => x.Id).Where(x => x != _client.Id).ToArray(),
                                    EventClass = new OnClientConnectEvent(_client.Id,_client.UserName,true)
                                };
                                SendEvent(eventMessage);
                                
                                listener.ExecuteEvent(eventMessage?.EventClass);

                                _client.HandshakeDone = true;
                                Log.Write($"*SUCCESS* Handshake done! ({_client.Id})");
                            } else {
                                _client.Close();
                                Log.Write($"*FAILED* Handshake failed! ({_client.Id})");
                            }
                        } else {
                            HandshakeClient(_client,message.Parameters);
                        }
                        continue;
                    }

                    listener.ExecuteEvent(new OnMessageReceivedEvent(message));

                    // Dump result to array and continue
					if (message.MessageType == (int)MessageTypes.ResponseData) {
                        Results.Add(message.Key,message.Parameters);
						continue;
					}

                    
                    // DESERIALISE PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                    List<object> paramList = new List<object>();
                    paramList.Add(_client);
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
							methodName = ServerMethods[methodId];
							methodInfo = typeof(ServerMethods).GetMethod(methodName);
							if (methodInfo == null) throw new Exception($"Method {methodId} was not found ({methodName})");
						}
					} else {
                        methodName = ServerMethods.FirstOrDefault(x => x.ToLower() == message.MethodName.ToLower());
						methodInfo = typeof(ServerMethods).GetMethod(methodName);
						if (methodInfo == null) throw new Exception($"Method {methodName} was not found");
					}

					switch (message.MessageType)
					{
						// SEND A RESPONSE FOR CLIENT/SERVER
						case (int)MessageTypes.RequestData:
                            if (message.TargetId > 1) {
                                Network.SendData(message); // FORWARD TO CLIENT
                            } else {
                                NetworkMessage responseMessage = new NetworkMessage {
                                    MessageType = (int)MessageTypes.ResponseData,
                                    MethodName = message.MethodName,
                                    TargetId = message.Sender,
                                    Key = message.Key
                                };
                                // HANDLE ON SERVER
                                object? data = methodInfo?.Invoke(methodName,parameters);
                                if (data != null) responseMessage.Parameters = data;

                                Network.SendData(responseMessage);
                            }
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (int)MessageTypes.SendData:
							methodInfo?.Invoke(methodName,parameters);
							break;
						default:
							throw new NotImplementedException();
					}
                } catch (Exception ex) {
                    ClientList.Remove(_client);
                    bool success = (ex is IOException || ex is SocketException);
                    if (success)
                        Log.Write("Client " + _client.Id.ToString() + " disconnected!");
                    else
                        Log.Write(ex);
                    
                    NetworkEvent message = new NetworkEvent {
                        EventClass = new OnClientDisconnectEvent(_client.Id,_client.UserName,success)
                    };
                    SendEvent(message);
                    break;
                }
            }
            ClientList.Remove(_client);
            _client.Client.Shutdown(SocketShutdown.Both);
            _client.Close();
        }

        private static void HandshakeClient(NetworkClient client, object[] parameters) {
            
            int version = (int)parameters[0];
            string userName = (string)parameters[1];
            // RETURNS client id if success (minus number if error (each value is one type of error))
            Log.Write($"*HANDSHAKE START* Version:{version} Name:{userName}");
            client.UserName = userName;

            if (ClientMethods == null) {
                ClientMethods = new List<string>() {};
                foreach (string method in (object[])parameters[2]) {
                    ClientMethods.Add(method);
                }
                Log.Write($"Added ({ClientMethods.Count()}) client methods!");
            }
            
            

            NetworkMessage handshakeMessage = new NetworkMessage {
                MessageType = (int)MessageTypes.ResponseData,
                isHandshake = true,
                TargetId = client.Id,
                Parameters = new object[] {client.Id}
            };

            //TODO add major and minor checking
            if (version != Network.Version) {
                Log.Write($"User {userName} has wrong version! Should be: {Network.Version} has: {version}");
                Network.SendData(handshakeMessage);
                return;
            }

            List<object[]> clientlist = new List<object[]>(){};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.Id == client.Id) continue;
                clientlist.Add(new object[] {toAdd.Id,toAdd.UserName});
            }

            List<int> targetList = new List<int>() {};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.Id == client.Id) continue;
                targetList.Add(toAdd.Id);
            }
            handshakeMessage.Parameters = new object[] {client.Id,ServerMethodsNEW.ToArray(),clientlist.ToArray()};

            Network.SendData(handshakeMessage);
        }



        private static object[] SerializeParameters(dynamic parameters,ref bool useClass) {
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
		private static dynamic DeserializeParameters(dynamic parameterData,bool isClass = false) {
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

        private static void DebugMessage(NetworkMessage message,int mode = 0) {
            if (!Debug) return;
            Console.WriteLine();
            Console.WriteLine("===============DEBUG MESSAGE===============");
            string type = "UNKNOWN";
            if (mode == 1) {
                type = "OUTBOUND";
            } else if (mode == 2) {
                type = "INBOUND";
            } else if (mode == 3) {
                type = "FORWARD";
            }
            Console.WriteLine($"MODE: {type}");
            Console.WriteLine($"TIME: {DateTime.Now.Millisecond}");
            Console.WriteLine($"MessageType:{message.MessageType}");
            Console.WriteLine($"TargetId:{message.TargetId}");
            Console.WriteLine($"MethodName:{message.MethodName}");
            Console.WriteLine($"IsClass:{message.UseClass}");
            Console.WriteLine();
            Console.WriteLine(JsonSerializer.Serialize<object>(message.Parameters));
            Console.WriteLine();
            Console.WriteLine($"Key:{message.Key}");
            Console.WriteLine($"Sender:{message.Sender}");
            Console.WriteLine("===============DEBUG MESSAGE===============");
            Console.WriteLine();
        }
        private static object[] ParseParamArray(string args) {
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
				} else if (Substring(args, i, 4).ToLower() == "true") {
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

    public class NetworkClient : TcpClient {
        public NetworkStream? Stream { get; set; }
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }
        public int Id { get; set; }
        public string UserName { get; set; } = "error (NoName)";
        public bool HandshakeDone { get; set; } = false;
        public NetworkClient(TcpListener listener)
        {   
            TcpClient _client = listener.AcceptTcpClient();
            Stream = _client.GetStream();

            this.Client = _client.Client;
            
            this.Active = true;
            //TcpClient _client = listener.AcceptTcpClient();
            Reader = new StreamReader(Stream);
            Writer = new StreamWriter(Stream);
        }
    }
}