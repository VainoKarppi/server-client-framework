#define SERVER

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

using static ServerFramework.Logger;
using static ServerFramework.NetworkEvents;

namespace ServerFramework {
    /// <summary>Settings for the framework to be used</summary>
    public class Settings {
        /// <summary>Checks if username is already in use on one of the clients. If in use and set to false, error will occur</summary>
        public static bool AllowSameUsername = true;

        /**
        * <summary>
        * NOT IMPLEMENTED YET!!!
        * If true all clients can have their own methods.
        * Otherwise the first client that joins is used as a "base" for methods and if other clients have more or less methods, this will occur error</summary>*/
        public static bool AllowDifferentMethods = false;//TODO Download and upload to all clients their methods and return types and parameter types 
    }
    /// <summary></summary>
    public class Network {
        /// <summary>Verion of the server. example: "1.0.0.0"</summary>
        public static readonly string? ServerVersion;
        private static TcpListener? ServerListener;
        private static readonly object _lock = new object();
        /// <summary>List of the connected clients on server. WARNING Handshake might not be completed yet!</summary>
        public static readonly List<NetworkClient> ClientList = new List<NetworkClient>();
        /// <summary>Check if the server is running.</summary>
        public static bool ServerRunning { get; internal set; } = false;
        private static int? ClientID = 1;
        /// <summary>Check if the current Network namespace is using server framework</summary>
        public const bool IsServer = false;
        /// <summary>Message types to be used in NetworkMessage and NetworkEvent</summary>
        public enum MessageTypes : int {
            /// <summary>Used for fire and forget</summary>
            SendData,
            /// <summary>Used for requesting data from target</summary>
            RequestData
        }

        /// <summary>Create new instance of a Network Event to be executed on client(s)</summary>
        public class NetworkEvent {
            public int MessageType { get; set; } = 10;
            /// <summary>Array of targets. Use negative int to remove from list. {0 = everyone} {-2 = everyone else expect client 2} {-5,-6,...}</summary>
            public int[]? Targets { get; set; } = new int[] { 0 };
            ///<summary>Class instance of event to be sent. T:ServerFramework.NetworkEvents</summary>
            public dynamic? EventClass { get; set; }
            ///<summary>NetworkEvent to be invoked on client</summary>
            public NetworkEvent(dynamic eventClass) {
                this.EventClass = eventClass;
            }
            ///<summary>Empty NetworkEvent to be invoked on client. Requires at least EventClass</summary>
            public NetworkEvent() {}
        }
        
        ///<summary>Network message that contains the data that was sent or received</summary>
		public class NetworkMessage {
            internal int? Hash;
            ///<summary>Type of the message (use MessageTypes)</summary>
            public int? MessageType { get; set; } = 0;
            ///<summary>Target ID who to send the message to. (0 = everyone, 1 = server, 2 = clientID, -2 = Everyone excpect client 2)</summary>
			public int? TargetId { get; set; } = 0;
			///<summary>Name of the registered method on receivers end</summary>
            public string? MethodName { get; set; }
			///<summary>Parameters to be send into the method</summary>
			public dynamic? Parameters { get; set; }
            public bool UseClass { get; set; } = false;
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; } = new Random().Next(100,int.MaxValue);
			///<summary>ID of the client who sent the message</summary>
			public int? Sender { get; set; } = ClientID;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
            public dynamic? OriginalParams { get; set; }
            /// <summary>Builds a new NetworkMessage that can be sent to wanted target using SendData or RequestData</summary>
            public NetworkMessage() {
                this.Hash = this.GetHashCode(); // TODO Check if same as on client
            }
            /// <summary>Builds a new NetworkMessage that can be sent to wanted target using SendData or RequestData</summary>
            public NetworkMessage(string methodName, int targetID, dynamic? parameters = null) {
                this.MethodName = methodName;
                this.TargetId = targetID;
                this.Parameters = parameters;
                this.Hash = this.GetHashCode(); // TODO Check if same as on client
            }
		}

        /// <summary>Method info that is available on client.</summary>
        public class NetworkMethodInfo {
            /// <summary>Method name</summary>
            public string? Name { get; internal set; }
            /// <summary>Return type of the method</summary>
            public Type? ReturnType { get; internal set; }
            /// <summary>Parameter types in the method</summary>
            public Type[]? Parameters { get; internal set; }
            internal NetworkMethodInfo(string? name, Type? type, Type[]? paramTypes) {
                this.Name = name;
                this.ReturnType = type;
                this.Parameters = paramTypes;
            }
        }
        ///<summary>List of the methods available on client ( Registered using "M:ServerFramework.Network.RegisterMethod")</summary>
        public static readonly List<NetworkMethodInfo> ClientMethods = new List<NetworkMethodInfo>();
        ///<summary>List of the methods available on server ( Registered using "M:ServerFramework.Network.RegisterMethod")</summary>
        public static readonly List<MethodInfo> ServerMethods = new List<MethodInfo>();
        private static bool ClientMethodsInitialized = false;
        private static List<string> PrivateMethods = new List<string>();
		private static Dictionary<int,dynamic> Results = new Dictionary<int,dynamic>();
        

        /// <summary>
        /// Register method to available methods to be invoked. When client uses SendData or RequestData
        /// </summary>
        /// <param name="className"></param>
        /// <param name="methodName"></param>
        /// <returns>INT : Amount of the registered methods</returns>
        public static int RegisterMethod(Type className, string? methodName = null) {
            try {
                if (ServerRunning) throw new InvalidOperationException("Cannot register new methods while server is running!");
                if (methodName == null) {
                    MethodInfo[] methods = className.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    int i = 0;
                    foreach (var item in methods) {
                        if (ServerMethods.Contains(item)) continue;
                        // TODO Add check for return and param types (Allow only if supported type)
                        ServerMethods.Add(item);
                        i++;
                    }
                    return i;
                } else {
                    MethodInfo? item = className.GetMethod(methodName,BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (item == null) return 0;
                    if (ServerMethods.Contains(item)) return 0;
                    ServerMethods.Add(item);
                    return 1;
                }
            } catch { return -1; }
        }



        /// <summary>
        /// Starts the server thread
        /// </summary>
        /// <param name="serverPort"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void StartServer(int serverPort = 5001) {
            if (ServerRunning)
                throw new InvalidOperationException("Server already running!");

            new Thread(() => {
                Thread.CurrentThread.IsBackground = true;
                
                ServerListener = new TcpListener(IPAddress.Any, serverPort);

                string? ServerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                if (ServerVersion == null) ServerVersion = "1.0.0.0";

                NetworkEvents? listener = NetworkEvents.eventsListener;
                listener.ExecuteEvent(new OnServerStartEvent(true), true);

                Log("Running server at port: " + ServerListener.LocalEndpoint?.ToString()?.Split(':')[1] + ". ServerVersion: " + ServerVersion);
                ServerRunning = true;

                ServerListener.Start();

                int _clientID = 2; // (0 = All clients, 1 = server, 2 and above for specific clients)
                while (ServerRunning) {
                    try {
                        // Start accepting clients
                        NetworkClient _client = new NetworkClient(ServerListener);
                        _client.ID = _clientID;
                        // Make sure the connection is already not created
                        if(ClientList.Contains(_client)) {
                            _client.Close();
                            throw new Exception("Client already connected!");
                        }
                        lock (_lock) ClientList.Add(_client);
                        
                        Log("*NEW* Client (" + _client.Client.RemoteEndPoint  + ") trying to connect...");

                        if (_clientID >= 32000) throw new Exception("Max user count reached! (32000)");

                        // Start new thread for each client
                        new Thread(() => HandleClientMessages(_client)).Start();
                        _clientID++;
                    } catch (Exception ex) {
                        if (!(ex is SocketException)) {
                            Log(ex.Message);
                        }
                    }
                }
            }).Start();
        }
        /// <summary>
        /// Stop server and send event invoke to clients about successfull server stop
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public static void StopServer() {
            if (!ServerRunning)
                throw new InvalidOperationException("Server not running!");
            
            Log("Stopping server...");

            OnServerShutdownEvent shutdownEvent = new OnServerShutdownEvent(true);
            SendEvent(new NetworkEvent(shutdownEvent));
            
            NetworkEvents? listener = NetworkEvents.eventsListener;
            listener.ExecuteEvent(shutdownEvent,true);
            ServerRunning = false;

            foreach (NetworkClient client in ClientList.ToArray()) {
                CloseClient(client);
            }
            ClientList.Clear();
            ServerListener?.Stop();
            ServerListener = null;

            Log("Server stopped!");
        }


        
        /// <summary>
        /// Invoke a event on receivers end.
        /// </summary>
        /// <param name="message"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void SendEvent(NetworkEvent message) {
            if (!ServerRunning) throw new InvalidOperationException("Server not running!");

            // Add ALL clients to list if left as blank
            List<int> targets = new List<int>();
            if (message.Targets == null) message.Targets = new int[] {0};

            // Exclusive targeting [-2] = everyone else expect client 2

            if (message.Targets.Count() == 1) {
                int target = message.Targets[0];
                if (target < 0) {
                    foreach (NetworkClient client in ClientList) {
                        if (target * -1 != client.ID) targets.Add(client.ID);
                    }
                } else {
                    foreach (NetworkClient client in ClientList) targets.Add(client.ID);
                }
            } else {
                targets = message.Targets.ToList();
                foreach (int target in message.Targets) { // [-4,-5,...]
                    if (target < 0 && targets.Contains(target * -1)) targets.Remove(target);
                }
            }

            // Send to single or multiple users
            message.Targets = null; // Dont send targets over net
            foreach (int id in targets) {
                NetworkClient? client = ClientList.FirstOrDefault(c => c.ID == id);
                if (client == null || client == default) continue;
                SendMessage(message,client.Stream);
            }
        }
        private static void SendMessage(dynamic message, NetworkStream Stream) {
            if (message is NetworkMessage && (!message.isHandshake) && message.Sender != 1) {
				NetworkEvents? listener = NetworkEvents.eventsListener;
				listener?.ExecuteEvent(new OnMessageSentEvent(message));
			}
			if (message is NetworkMessage && !(message.Parameters is null) && message.Sender == 1) {
                bool useClass = false;
                if (message.OriginalParams == null) message.OriginalParams = message.Parameters; // TODO find better way
                message.Parameters = SerializeParameters(message.OriginalParams,ref useClass);
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
        
        
        
        /// <summary>
        /// Invoke a method on receivers end. This uses fire and forget mode. (No data to be returned)
        /// </summary>
        /// <param name="message"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public static void SendData(NetworkMessage message) {
            if (!ServerRunning) throw new InvalidOperationException("Server not running!");
			if (message.TargetId == 1) throw new InvalidOperationException("Cannot send data to self (server)!");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

            if (message.MessageType != 11) {
                var found = ClientMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
				if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
            }
            
            // Send to single ro multiple users
            if (message.TargetId > 0) {
                NetworkClient? client = ClientList.FirstOrDefault(c => c.ID == message.TargetId);
                if (client == null) throw new Exception("Invalid target!");
                SendMessage(message,client.Stream);
                int mode = message.Sender == 1?1:3;
                DebugMessage(message,mode);
            } else {
                int i = 0;
                foreach (NetworkClient client in ClientList) {
                    if (message.Sender == client.ID) continue;
                    SendMessage(message,client.Stream);
                    i++;
                }
                if (message.Sender == 1) Log($"DATA SENT TO {i} USERS(s)!");
                else Log($"DATA FORWARDED TO {i} USERS(s)!");
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
        
        
        
        /// <summary>
        /// Request data from target by invoking its method.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
		public static dynamic RequestData(NetworkMessage message) {
			if (!ServerRunning) throw new InvalidOperationException("Server not running!");
            if (message.TargetId == 1) throw new Exception("Cannot request data from self!");
            if (message.MessageType != 11) {
                var found = ClientMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
				if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
				if (found.ReturnType == typeof(void)) throw new Exception($"Method {message.MethodName} doesn't have a return value! (Uses void)");	
            }
			message.MessageType = (int?)MessageTypes.RequestData;

            NetworkClient? client = ClientList.FirstOrDefault(client => client.ID == message.TargetId);
            if (client == null) throw new Exception("Invalid target!");
		
			SendMessage(message,client.Stream);
            DebugMessage(message,1);

			return RequestDataResult(message);
		}
        
        
        
        /// <summary>
        /// Request data from target by invoking its method and parse to wanted type. (Can be class instance)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
		public static dynamic RequestData<T>(NetworkMessage message) {
			dynamic returnMessage = RequestData(message);
			if (returnMessage is JsonElement) {
                var returned = ((JsonElement)returnMessage[1]).Deserialize<T>();
                if (returned == null) throw new NullReferenceException();
                return returned;
			}
			return (T)returnMessage;
		}
       
       
       
        /// <summary>
        /// Request data from target by invoking its method and parse to wanted type. (Can be class instance) using ASYNC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public static async Task<dynamic> RequestDataAsync<T>(NetworkMessage message) {
			dynamic returnMessage = await RequestData(message);
			if (returnMessage is JsonElement) {
                var returned = ((JsonElement)returnMessage[1]).Deserialize<T>();
				if (returned == null) throw new NullReferenceException();
                return returned;
			}
			return (T)returnMessage;
		}
		
        
        
        /// <summary>
        /// Request data from target by invoking its method using ASYNC
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public static async Task<dynamic> RequestDataAsync(NetworkMessage message) {
			return await RequestData(message);
		}

        // One thread for one user
        // Start listening data coming from this client
        private static void HandleClientMessages(NetworkClient _client) {
            while (true) {
                try {
                    byte[] bytes = ReadMessageBytes(_client.Stream);
					
                    if (bytes.Count() == 0) {
                        throw new Exception($"ERROR BYTES IN CLIENT: {_client.ID} RECEIVE DATA THREAD!");
                    };
                    
                    var utf8Reader = new Utf8JsonReader(bytes);
                    dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
					string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

					int type = -1;
					if (!Int32.TryParse(property, out type)) continue;
					if (type < 0) continue;

                    NetworkEvents? listener = NetworkEvents.eventsListener;

                    if (type == 10) {
						var eventBytes = new Utf8JsonReader(bytes);
						NetworkEvent? eventMessage = JsonSerializer.Deserialize<NetworkEvent>(ref eventBytes)!;
                        if (eventMessage.Targets != null && eventMessage.Targets.Any((new int[] {0,1}).Contains)) {
                            listener?.ExecuteEvent(eventMessage.EventClass);
                            if (eventMessage.Targets == new int[] {1}) continue; // No clients to send the method to
                        }
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
                    
                    // HANDLE HANDSHAKE
                    if (message.isHandshake) {
                        // Return of successfull handshake
                        if (message.MessageType == (int)MessageTypes.SendData) {
                            if (message.Sender > 1) {
                                _client.HandshakeDone = true;
                                Thread.Sleep(5);

                                NetworkEvent eventMessage = new NetworkEvent {
                                    Targets = new int[] {0},
                                    EventClass = new OnClientConnectEvent(_client.ID,_client.UserName,true)
                                };
                                SendEvent(eventMessage);
                                
                                listener?.ExecuteEvent(eventMessage?.EventClass);

                                Log($"*DEBUG* Handshake done! ({_client.ID})");
                            } else {
                                _client.Close();
                                Log($"*DEBUG* Handshake failed! ({_client.ID})");
                            }
                        } else {
                            HandshakeClient(_client,message.Parameters);
                        }
                        continue;
                    }

                    listener?.ExecuteEvent(new OnMessageReceivedEvent(message));

                    // Dump result to array and continue
					if (message.MessageType == 11) {
                        Results.Add(message.Key,message.Parameters);
						continue;
					}

                    // GET METHOD INFO
					int methodId;
                    MethodInfo? method;
					bool isInt = int.TryParse(message.MethodName, out methodId);
					if (isInt && (methodId < 0)) {
                        string methodName = PrivateMethods[Math.Abs(methodId) - 1];
                        method = typeof(Network).GetMethod(methodName);
					} else {
                        method = ServerMethods.FirstOrDefault(x => x.Name.ToLower() == message.MethodName?.ToLower());
                        if (method == default) throw new Exception($"Method {message.MethodName} was not found from Registered Methods!");
                    }

                    // GET PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                    object[]? parameters = null;
                    ParameterInfo[]? parameterInfo = method?.GetParameters();
                    if (parameterInfo?.Count() > 0) {
                        List<object> paramList = new List<object>();
                        ParameterInfo first = parameterInfo[0];
                        if (first.ParameterType == typeof(NetworkClient)) paramList.Add(_client);
                        if (parameterInfo.Count() > 1 && (parameterInfo[1].ParameterType == typeof(NetworkMessage))) paramList.Add(message);

                        if (message.Parameters != null) {
                            if (message.Parameters is Array) {
                                foreach (var item in message.Parameters) {
                                    if (method?.GetParameters().Count() == paramList.Count()) break; // Not all parameters can fill in
                                    paramList.Add(item);
                                }
                            } else {
                                paramList.Add(message.Parameters);
                            }
                        }
                        parameters = paramList.ToArray();  
                    }

					switch (message.MessageType) {
                        case (int)MessageTypes.RequestData: { // Invoke Method and send response back to client
                            NetworkMessage responseMessage = new NetworkMessage {
                                MessageType = 11,
                                MethodName = message.MethodName,
                                TargetId = message.Sender,
                                Key = message.Key
                            };
                            object? data = method?.Invoke(null, parameters);
                            if (data != null) responseMessage.Parameters = data;

                            Network.SendData(responseMessage);
                            break;
                        }

                        case (int)MessageTypes.SendData: { // FIRE AND FORGET was used (Dont return method return data)
                            method?.Invoke(null, parameters);
							break;
                        }

						default: {
                            throw new NotImplementedException();
                        }
					}
                } catch (Exception ex) {
                    if (!ServerRunning) break;

                    CloseClient(_client);
                    bool success = ((ex is IOException || ex is SocketException) && _client.HandshakeDone);
                    if (!success) Log(ex.Message);

                    if (!_client.HandshakeDone) break;
                    Log($"Client {_client.ID} disconnected! (SUCCESS: {success})");

                    OnClientDisconnectEvent disconnectEvent = new OnClientDisconnectEvent(_client.ID,_client.UserName,success);
                    NetworkEvent eventTemp = new NetworkEvent(disconnectEvent);
                    eventTemp.EventClass = disconnectEvent;
                    SendEvent(eventTemp); // TODO FIX

                    NetworkEvents? listener = NetworkEvents.eventsListener;
                    listener?.ExecuteEvent(disconnectEvent,true);

                    break;
                }
            }
            if (ServerRunning) CloseClient(_client);
        }

        /// <summary>
        /// Close connection of client. Invokes OnClientDisconnectedEvent
        /// </summary>
        /// <param name="client"></param>
        public static void CloseClient(NetworkClient client) {
            ClientList.Remove(client);
            client.Writer.Close();
            client.Reader.Close();
            client.Stream.Close();
            client.Close();
            client.Dispose();
        }

        private static void HandshakeClient(NetworkClient client, object[] parameters) {
            
            string? clientVersion = parameters[0].ToString();
            string? userName = (string)parameters[1];
            if (clientVersion == null || userName == null) throw new Exception($"Invalid client data! version:{clientVersion}, userName:{userName}");

            client.UserName = userName;

            string? serverVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            if (serverVersion == null) serverVersion = "1.0.0.0";

            NetworkEvents? listener = NetworkEvents.eventsListener;
            listener?.ExecuteEvent(new OnHandShakeStartEvent(clientVersion,userName,client.ID),true);
            
            // RETURNS client id if success (minus number if error (each value is one type of error))
            Log($"*HANDSHAKE START* ClientVersion:{clientVersion} Name:{userName}");

            NetworkMessage handshakeMessage = new NetworkMessage {
                MessageType = 11,
                isHandshake = true,
                TargetId = client.ID
            };

            //TODO add major and minor checking
            if (clientVersion != serverVersion) {
                Log($"User {userName} has wrong version! Should be: {serverVersion} has: {clientVersion}");
                handshakeMessage.Parameters = new object[] {-2,serverVersion};
                Network.SendData(handshakeMessage);
                listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion,userName,client.ID,false,2),true);
                throw new Exception($"User {userName} has wrong version! Should be: {serverVersion} has: {clientVersion}");
            }

            //TODO Settings.AllowDifferentMethods
            if (!ClientMethodsInitialized) {
                ClientMethodsInitialized = true;
                foreach (object[] method in (object[])parameters[2]) {
                    Type? type = Type.GetType((string)method[1]);
                    if (type == null) continue;
                    object[]? paramTypes = (object[])method[2];
                    List<Type> typeList = new List<Type> {};
                    foreach (string paramType in paramTypes) {
                        Type? typeThis = Type.GetType(paramType);
                        if (typeThis != null) typeList.Add(typeThis);
                    }
                    ClientMethods.Add(new NetworkMethodInfo(
                        (string)method[0],
                        type,
                        typeList.ToArray<Type>()
                    ));
                }
                Log($"Added ({ClientMethods.Count()}) client methods!");
            }

            // Check if username already in use
            if (!Settings.AllowSameUsername) {
                NetworkClient? usedClient = ClientList.FirstOrDefault(x => x.HandshakeDone && x.UserName.ToLower() == userName.ToLower());
                if (usedClient != null) {
                    Log($"*ERROR* Handshake, Username:{userName} already in use for Client:{usedClient.ID}!");
                    handshakeMessage.Parameters = new object[] {-3,serverVersion};
                    Network.SendData(handshakeMessage);
                    listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion,userName,client.ID,false,3),true);
                    throw new Exception($"*ERROR* Handshake, Username:{userName} already in use for Client:{usedClient.ID}!");
                }
            }

            List<object[]> clientlist = new List<object[]>(){};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.ID == client.ID) continue;
                clientlist.Add(new object[] {toAdd.ID,toAdd.UserName});
            }

            List<int> targetList = new List<int>() {};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.ID == client.ID) continue;
                targetList.Add(toAdd.ID);
            }

            // Params = [type,type.type]
            object[] methodsToSend = ServerMethods.Select(x => new object[] { 
                x.Name,
                x.ReturnType.ToString(),
                x.GetParameters().
                    Where(y => y.ParameterType != typeof(NetworkMessage) && y.ParameterType != typeof(NetworkClient)).
                    Select(z => z.ParameterType.ToString()).ToArray()
            }).ToArray();

            handshakeMessage.Parameters = new object[] {client.ID,serverVersion,methodsToSend,clientlist.ToArray()};

            Network.SendData(handshakeMessage);

            new Thread(() => {
                int i = 0;
                while (i < 200) { // 2 second timer
                    Thread.Sleep(2);
                    if (client.HandshakeDone) {
                        listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion,userName,client.ID,true,null),false);
                        return;
                    }
                    ++i;
                }
                listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion,userName,client.ID,false,0),true);
                Log($"Handshake time out for Client:{client.ID}");
                CloseClient(client);
            }).Start();
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
		private static dynamic? DeserializeParameters(dynamic parameterData,bool isClass = false) {
            if(parameterData is null) return null;
			if (isClass) return parameterData;
            List<object> parameters = JsonSerializer.Deserialize<List<object>>(parameterData);
            bool odd = parameters.Count()%2 != 0;
            if (odd && parameters.Count() > 2) {
                parameters.RemoveAt(0);
            }
			Type? type = default;
			List<object?> final = new List<object?>();
			for (int i = 0; i < parameters.Count(); i++) {
				object? value = parameters[i];
                string? valueString = value.ToString();
                if (valueString == null) continue;
                if (i%2 == 0) {
					type = Type.GetType(valueString);
					continue;
				}
                if (type == null) continue;
                if (type.IsArray) {
					object[] data = ParseParamArray(valueString);
					final.Add(data);
				} else {
					object? dataTemp = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(valueString);
                    final.Add(dataTemp);
				}
			}
            if (!odd) {
                return final[0];
            }
			return final.ToArray();
		}

        private static void DebugMessage(NetworkMessage message,int mode = 0) {
            Log("===============DEBUG MESSAGE===============");
            string type = "UNKNOWN";
            if (mode == 1) type = "OUTBOUND";
            else if (mode == 2) type = "INBOUND";
            Log($"MODE: {type}");
            Log($"TIME: {DateTime.Now.Millisecond}");
            Log($"MessageType:{message.MessageType}");
            Log($"TargetId:{message.TargetId}");
            Log($"MethodName:{message.MethodName}");
            Log($"IsClass:{message.UseClass}");
            Log($"Handshake:{message.isHandshake}");
            Log();
            Log(JsonSerializer.Serialize<object>(message.Parameters));
            Log();
            Log($"Key:{message.Key}");
            Log($"Sender:{message.Sender}");
            Log("===============DEBUG MESSAGE===============");
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

        /// <summary>Client instance that holds the client info.</summary>
        public class NetworkClient : TcpClient {
            internal NetworkStream Stream { get; set; }
            internal StreamReader Reader { get; set; }
            internal StreamWriter Writer { get; set; }
            /// <summary>ID of the Client</summary>
            public int ID { get; internal set; }
            /// <summary>Username of the client</summary>
            public string UserName { get; internal set; } = "error (NoName)";
            /// <summary>BOOL if the handshake has been completed.</summary>
            public bool HandshakeDone { get; internal set; } = false;
            internal NetworkClient(TcpListener listener)
            {   
                TcpClient _client = listener.AcceptTcpClient();
                Stream = _client.GetStream();

                Client = _client.Client;
                
                Reader = new StreamReader(Stream);
                Writer = new StreamWriter(Stream);
            }
        }
    }
}