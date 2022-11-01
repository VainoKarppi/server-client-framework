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

    public class Network {
        public static int ClientID = 1;
        public static TcpListener ServerListener = default!;
        public static readonly object _lock = new object();
        public static readonly List<NetworkClient> ClientList = new List<NetworkClient>();
        public static int ServerPort { get; set; } = 2302;
        public static bool ServerRunning { get; set; }
        public enum MessageTypes : int {SendData, RequestData, ResponseData, ServerEvent, ClientEvent}
        public class EventMessage {
            public int? MessageType { get; set; } = (int)MessageTypes.ServerEvent;
            public int[]? Targets { get; set; }
            public dynamic? EventClass { get; set; }
        }
        
		public class NetworkMessage {
			public int? MessageType { get; set; } = 0;
			// One of the tpes in "MessageTypes"
			public int? TargetId { get; set; } = 0;
			// 0 = everyone, 1 = server, 2 = client 1...
            public string? MethodName { get; set; }
			// Minus numbers are for internal use!
			public List<object>? Parameters { get; set; } = new List<object>() {};
            public string? ReturnDataType { get; set; }
            public dynamic? ReturnData { get; set; }
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; } = new Random().Next(100,int.MaxValue);
			// Key for getting the response for specific request (0-100) = event id
			public int? Sender { get; set; } = ClientID;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string> ClientMethods = new List<string>() {"Disconnect","ConnectedClients","Test","TestArray","TestType"};
        private static List<string> PrivateMethods = new List<string>() {"GetMethods","ConnectedClients","HandleEvent"};
		public static Dictionary<int,dynamic> Results = new Dictionary<int,dynamic>();

        public static void StartServer() {
            if (ServerRunning)
                throw new Exception("Server already running!");

            new Thread(() => {
                Thread.CurrentThread.IsBackground = true; 
                
                ServerListener = new TcpListener(IPAddress.Any, ServerPort);
                ServerListener.Start();
                ServerRunning = true;

                Console.WriteLine("Running server at: (" + ServerListener.LocalEndpoint + ")");
                Console.WriteLine();

                int _clientID = 2; // (0 = All clients, 1 = server, 2 and above for specific clients)
                while (ServerRunning) {
                    try {
                        // Start accepting clients
                        NetworkClient _client = new NetworkClient(ServerListener);
                        _client.ID = _clientID;
                        _client.Client.SendTimeout = 5;
                        // Make sure the connection is already not created
                        if(ClientList.Contains(_client)) {
                            _client.Close();
                            throw new Exception("Client already connected!");
                        }
                        lock (_lock) ClientList.Add(_client);
                        
                        Console.WriteLine("*NEW* Client (" + _client.Client.RemoteEndPoint  + ") trying to connect...");

                        if (_clientID >= 32000) throw new Exception("Max user count reached! (32000)");

                        // Start new thread for each client
                        new Thread(() => HandleClient(_client)).Start();
                        _clientID++;;
                    } catch (Exception ex) {
                        if (!(ex is SocketException)) {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }).Start();
        }
        public static void StopServer() {
            if (!ServerRunning)
                throw new Exception("Server not running!");
            
            Console.WriteLine("Stopping server...");

            EventMessage message = new EventMessage {
                EventClass = new OnServerShutdown(true)
            };
            SendEvent(message);
            
            ServerRunning = false;
            ServerListener.Stop();
            ServerListener = default!;
            ClientList.Clear();

            // TODO Send msg to all clients for succesfull server shutdown?
            Console.WriteLine("Server stopped!");
        }


        // Request ID is used to answer to specific message

        public static void SendEvent(EventMessage message) {
            if (!ServerRunning) throw new Exception("Server not running!");

            // Add ALL clients to list if left as blank
			if (message.Targets == default) {
                List<int> targets = new List<int>(){};
                foreach (NetworkClient client in ClientList) {
                    targets.Add(client.ID);
                }
                message.Targets = targets.ToArray();
            }

            // Send to single or multiple users
            foreach (int id in message.Targets) {
                NetworkClient client = ClientList.FirstOrDefault(c => c.ID == id);
                if (client == default) continue;
                message.Targets = null; // Dont send targets over net
                SendMessage(message,client.Stream);
            }
        }

        public static void SendMessage(dynamic message, NetworkStream Stream) {
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
        
        public static void SendData(NetworkMessage message) {
            if (!ServerRunning) throw new Exception("Server not running!");
			if (message.TargetId == 1) throw new Exception("Cannot send data to self (server)!");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;
            if (message.ReturnData != null && (message.ReturnDataType == default)) message.ReturnDataType = message.ReturnData.GetType().ToString();
            
            // Send to single ro multiple users
            if (message.TargetId > 0) {
                NetworkClient client = ClientList.FirstOrDefault(c => c.ID == message.TargetId);
                if (client == default) throw new Exception("Invalid target!");
                int mode = message.Sender > 1 ? 3 : 1;
                DebugMessage(message,mode);
                SendMessage(message,client.Stream);
            } else {
                foreach (NetworkClient client in ClientList) {
                    SendMessage(message,client.Stream);
                }
                Console.WriteLine($"DATA SENT {ClientList.Count()} to USERS(s)!");
            }
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
			if (!ServerRunning) throw new Exception("Server Not running!");
			message.MessageType = (int?)MessageTypes.RequestData;

            NetworkClient client = ClientList.FirstOrDefault(client => client.ID == message.TargetId);
            if (client == default) throw new Exception("Invalid target!");

			DebugMessage(message,1);
			SendMessage(message,client.Stream);

			dynamic returnMessage = RequestDataResult(message);
			return returnMessage;
		}
		public static dynamic RequestData<T>(NetworkMessage message) {
			if (!ServerRunning) throw new Exception("Server Not running!");
			message.MessageType = (int?)MessageTypes.RequestData;

            NetworkClient client = ClientList.FirstOrDefault(client => client.ID == message.TargetId);
            if (client == default) throw new Exception("Invalid target!");
            
			DebugMessage(message,1);
			SendMessage(message,client.Stream);

			dynamic returnMessage = RequestDataResult(message);
			if (returnMessage is JsonElement) {
				return ((JsonElement)returnMessage).Deserialize<T>();
			}
			return (T)returnMessage;
		}

        // One thread for one user
        // Start listening data coming from this client
        public static void HandleClient(NetworkClient _client) {
            while (true) {
                try {
                    byte[] bytes = ReadMessageBytes(_client.Stream);
					
                    if (bytes.Count() == 0) {
                        throw new Exception($"ERROR BYTES IN CLIENT: {_client.ID} RECEIVE DATA THREAD!");
                    };
                    Console.WriteLine("MSG RECIEVED!");
                    
                    var utf8Reader = new Utf8JsonReader(bytes);
                    dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
					Console.WriteLine((JsonElement)messageTemp);
					string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

					int type = -1;
					if (!Int32.TryParse(property, out type)) continue;
					if (type < 0) continue;

                    if (type == (int)MessageTypes.ServerEvent) {
						var eventBytes = new Utf8JsonReader(bytes);
						EventMessage? eventMessage = JsonSerializer.Deserialize<EventMessage>(ref eventBytes)!;
						SendEvent(eventMessage);
						continue;
					}
					var msgBytes = new Utf8JsonReader(bytes);
					NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;
					DebugMessage(message,2);
					object[] deserialisedParams = DeserializeParameters(message.Parameters);

                    // HANDLE HANDSHAKE
                    if (message.isHandshake) {
                        HandshakeClient(_client, (int)deserialisedParams[0], (string)deserialisedParams[1]);
                        continue;
                    }

                    // FORWARD DATA IF NOT MENT FOR SERVER (+forget)
                    if (message.TargetId != 1) {
                        Network.SendData(message);
                        continue;
                    }

                    // DESERIALISE PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                    List<object> paramList = new List<object>();
                    paramList.Add(_client);
                    if (message.Parameters != null) {
                        foreach (object p in deserialisedParams) {
                            paramList.Add(p);
                        }
                    }
                    object[] parameters = paramList.ToArray();  
                    
                    
                    // Dump result to array and continue
					if (message.MessageType == (int)MessageTypes.ResponseData) {
                        Results.Add(message.Key,parameters);
                        continue;
                    }

                    
                    
                    
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
							methodInfo = typeof(ServerMethods).GetMethod(methodName);
							if (methodInfo == null) throw new Exception($"Method {methodId} was not found ({methodName})");
						}
					} else {
                        methodName = ClientMethods.FirstOrDefault(x => x.ToLower() == message.MethodName.ToLower());
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

                                responseMessage.ReturnData = data;

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
                    Console.WriteLine("\n");
                } catch (Exception ex) {
                    
                    bool success = (ex is IOException || ex is SocketException);
                    if (success)
                        Console.WriteLine("Client " + _client.ID.ToString() + " disconnected!");
                    else
                        Console.WriteLine(ex);
                    
                    EventMessage message = new EventMessage {
                        EventClass = new OnClientDisconnect(_client.ID,_client.UserName,success)
                    };
                    SendEvent(message);
                    break;
                }
            }
            ClientList.Remove(_client);
            _client.Client.Shutdown(SocketShutdown.Both);
            _client.Close();
        }

        public static void HandshakeClient(NetworkClient client, int version, string userName) {
            
            // RETURNS client id if success (minus number if error (each value is one type of error))
            Console.WriteLine($"*HANDSHAKE START* Version:{version} Name:{userName}");
            client.UserName = userName;

            object[] returnData = new object[] {client.ID};

            NetworkMessage handshakeMessage = new NetworkMessage {
                MessageType = (int)MessageTypes.ResponseData,
                isHandshake = true,
                TargetId = client.ID
            };


            //TODO add major and minor checking
            if (version != Program.Version) {
                Console.WriteLine($"User {userName} has wrong version! Should be: {Program.Version} has: {version}");
                handshakeMessage.Parameters = SerializeParameters(returnData);
                Network.SendData(handshakeMessage);
                
                return;
            }

            List<object[]> clientlist = new List<object[]>(){};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.ID == client.ID) continue;
                clientlist.Add(new object[] {toAdd.ID,toAdd.UserName});
            }

            // TODO move elsewhere
            List<int> targetList = new List<int>() {};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.ID == client.ID) continue;
                targetList.Add(toAdd.ID);
            }
            EventMessage message = new EventMessage {
                Targets = targetList.ToArray(),
                EventClass = new OnClientConnect(client.ID,client.UserName)
            };
            SendEvent(message);
            
            Console.WriteLine($"*SUCCESS* Handshake done! ({client.ID})");

            returnData = new object[] {client.ID,ClientMethods.ToArray(),clientlist.ToArray()};
            handshakeMessage.Parameters = SerializeParameters(returnData);

            handshakeMessage.ReturnData = returnData;

            Network.SendData(handshakeMessage);
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

        public static void DebugMessage(NetworkMessage message,int mode = 0) {
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
            Console.WriteLine($"ReturnDataType: {message.ReturnDataType}");
            Console.WriteLine($"ReturnData: {message.ReturnData}");
            Console.WriteLine($"TargetId:{message.TargetId}");
            Console.WriteLine($"MethodName:{message.MethodName}");
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
                    if (data.GetType().IsArray) {
                        Console.WriteLine($"  ({ii}): ({data.GetType()}): {JsonSerializer.Serialize<object>(data)}");
                    } else {
                        Console.WriteLine($"  ({ii}): ({data.GetType()}): {data.ToString()}");
                    } 
                    ii++;
                }
            }
            Console.WriteLine($"Key:{message.Key}");
            Console.WriteLine($"Sender:{message.Sender}");
            Console.WriteLine("===============DEBUG MESSAGE===============");
            Console.WriteLine();
        }
        public static object[] ParseParamArray(string input) {
			List<object> returned = new List<object>(){};
			if (input.ElementAt(0) == '[') {
				if (input == "[]") return returned.ToArray();
				input = input.Remove(0,1);
				input = input.Remove(input.Length - 1,1);
				string[] separated = input.Split(',');
				foreach (string value in separated) {
					int i;
					if (int.TryParse(value, out i)) returned.Add(i);

					double d;
					if (double.TryParse(value, out d)) returned.Add(d);

					bool b;
					if (bool.TryParse(value, out b)) returned.Add(b);

					if (value.ElementAt(0) == '[') returned.Add(ParseParamArray(value));

					if (value.StartsWith(@"""")) returned.Add(value);
				}
			}
			return returned.ToArray();
		}
    }

    public class NetworkClient : TcpClient {
        public NetworkStream? Stream { get; set; }
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }
        public int ID { get; set; }
        public string UserName { get; set; } = "error (NoName)";


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
