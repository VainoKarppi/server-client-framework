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
using static ClientFramework.Logger;
using static ClientFramework.NetworkEvents;

namespace ClientFramework {
	/// <summary></summary>
    public class Network {
		/// <summary>Verion of the server. example: "1.0.0.0". Gets its value after successfull handshake</summary>
		public static readonly string? ServerVersion;
		/// <summary>Create new instance of a Network Event to be executed on client(s)</summary>
		public class NetworkEvent {
            public int MessageType { get; set; } = 10;
            /// <summary>Array of targets. Use negative int to remove from list. {0 = everyone} {-2 = everyone else expect client 2} {-5,-6,...}</summary>
            public int[]? Targets { get; set; } = new int[] { 0 };
            ///<summary>Class instance of event to be sent. T:ServerFramework.NetworkEvents</summary>
            public dynamic? EventClass { get; set; }
            ///<summary>NetworkEvent to be invoked on client</summary>
            public NetworkEvent(dynamic eventClass) {
                EventClass = eventClass;
            }
            ///<summary>Empty NetworkEvent to be invoked on client. Requires at least EventClass</summary>
            public NetworkEvent() {}
        }
		
		/// <summary>Message types to be used in NetworkMessage and NetworkEvent</summary>
        public enum MessageTypes : int {
            /// <summary>Used for fire and forget</summary>
            SendData,
            /// <summary>Used for requesting data from target</summary>
            RequestData
        }

        /// <summary>Create new instance of a Network Event to be executed on client(s)</summary>
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
			public int? Sender { get; set; } = 1;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
            public dynamic? OriginalParams { get; set; }
            /// <summary>Builds a new NetworkMessage that can be sent to wanted target using SendData or RequestData</summary>
            public NetworkMessage() {
                Hash = this.GetHashCode(); // TODO Check if same as on client
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
		///<summary>List of the methods available on server ( Registered using "M:ServerFramework.Network.RegisterMethod")</summary>
        public static readonly List<NetworkMethodInfo> ServerMethods = new List<NetworkMethodInfo>();
		///<summary>List of the methods available on client ( Registered using "M:ServerFramework.Network.RegisterMethod")</summary>
        public static readonly List<MethodInfo> ClientMethods = new List<MethodInfo>();
 		private static List<string> PrivateMethods = new List<string>() {};
		// To be read from handshake (register on server)
		private static Dictionary<int,dynamic> Results = new Dictionary<int,dynamic>();
		/// <summary>Client object that is connected to server</summary>
		public static NetworkClient Client = new NetworkClient();



		/// <summary>List of the other clients connected to server</summary>
		public static List<OtherClient> OtherClients = new List<OtherClient>() {};
		/// <summary>Instance of other client info</summary>
		public class OtherClient {
			/// <summary>ID of the other client</summary>
			public int? ID { get; internal set;}
			/// <summary>Username of that client</summary>
			public string? UserName { get; internal set; } = "error (NoName)";
			/// <summary>BOOL If the client is connected to server</summary>
			public bool Connected { get; set; } = true;
			internal OtherClient(int? id, string? name, bool connected = true) {
				ID = id;
				UserName = name;
				Connected = connected;
			}
		}

		
		/// <summary>
        /// Register method to available methods to be invoked. When client uses SendData or RequestData
        /// </summary>
        /// <param name="className"></param>
        /// <param name="methodName"></param>
        /// <returns>INT : Amount of the registered methods</returns>
        public static int RegisterMethod(Type className, string? methodName = null) {
            try {
                if (IsConnected()) throw new Exception("Cannot register new methods while connected to server!");
                if (methodName == null) {
                    MethodInfo[] methods = className.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    int i = 0;
                    foreach (var item in methods) {
                        if (ClientMethods.Contains(item)) continue;
                        ClientMethods.Add(item);
                        i++;
                    }
                    return i;
                } else {
                    MethodInfo? item = className.GetMethod(methodName,BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
					if (item == null) return 0;
                    if (ClientMethods.Contains(item)) return 0;
                    ClientMethods.Add(item);
                    return 1;
                }
            } catch { return -1; }
        }



		/// <summary>
        /// Checks if connected to server (once handshake is done)
        /// </summary>
        /// <returns>BOOL : TRUE if connected, FALSE if not</returns>
		public static bool IsConnected() {
			if (Client == default || (!Client.Connected || !Client.HandshakeDone)) return false;
			return true;
		}



		/// <summary>
        /// Connects to server
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="userName"></param>
        /// <exception cref="Exception"></exception>
		public static void Connect(string ip = "127.0.0.1", int port = 5001, string userName = "unknown") {
			if (IsConnected())
				throw new Exception("Already connected to server!");

			Client = new NetworkClient();

			Log("Trying to connect at: (" + ip + ":" + port.ToString() + "), with name: " + userName);
			Client.Connect(IPAddress.Parse(ip), port);


			Client.Stream = Client.GetStream();
			Client.Reader = new StreamReader(Client.Stream);
			Client.Writer = new StreamWriter(Client.Stream);
			
			// Request client ID and do handshake
			int _id = Network.Handshake(userName);
			if (_id < 2) return;
			Log($"HANDSHAKE DONE: ID={_id}");

			// Continue in new thread
			Thread thread = new Thread(ReceiveDataThread);
			thread.Start();
		}
		
		

		/// <summary>
        /// Disconnects from the server
        /// </summary>
        /// <exception cref="Exception">Not connected to server!</exception>
		public static void Disconnect() {
			OtherClients = new List<OtherClient>() {};
			if (!IsConnected())
				throw new Exception("Not connected to server!");
			
			Log("Disconnected From the server!");
			Client.HandshakeDone = false;
			Client.Client.Close();

			NetworkEvents? listener = NetworkEvents.eventsListener;
			listener?.ExecuteEvent(new OnClientDisconnectEvent(Client.ID,Client.UserName,true));
		}
		
		
		/// <summary>
        /// Invoke a event on receivers end.
        /// </summary>
        /// <param name="message"></param>
        /// <exception cref="Exception"></exception>
		public static void SendEvent(NetworkEvent message) {
            if (!Client.HandshakeDone) throw new Exception("Server not running!");

			// Add ALL clients to list if left as blank
            List<int> targets = new List<int>();
            if (message.Targets == null) message.Targets = new int[] {0};
			if (Client.ID != null) message.Targets = message.Targets.Concat(new int[] {(int)Client.ID * -1}).ToArray(); // Dont send back to client
			SendMessage(message,Client.GetStream());
        }
		private static void ReceiveDataThread() {
			try {
				while (Client.Connected) {
					byte[] bytes = ReadMessageBytes(Client.GetStream());
					if (bytes.Count() == 0) {
						if (Client.Available != 0) throw new EndOfStreamException("Server closed connection!");
                        Client.Stream?.Close();
                        Client.Close();
                        break;
                    }
					var utf8Reader = new Utf8JsonReader(bytes);
                    dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
					string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

					int type = -1;
					if (!Int32.TryParse(property, out type)) continue;
					if (type < 0) continue;
					
					// HANDLE EVENT
					NetworkEvents? listener = NetworkEvents.eventsListener;
					if (type == 10) {
                        dynamic? eventClass = ((JsonElement)messageTemp).GetProperty("EventClass");
                        string? eventName = (eventClass is JsonElement) ? ((JsonElement)eventClass).GetProperty("EventName").GetString() : eventClass?.EventName;
						if (eventName?.ToLower() == "onclientconnectevent") {
                            int? id = ((JsonElement)eventClass).GetProperty("ClientID").GetInt32();
							string? name = ((JsonElement)eventClass).GetProperty("UserName").GetString();
							bool? success = ((JsonElement)eventClass).GetProperty("Success").GetBoolean();
							if (id == null || name == null) continue;

							eventClass = new OnClientConnectEvent(id,name,success);
                            if (id != Client.ID) OtherClients.Add(new OtherClient(id, name));
                        }
						if (eventName?.ToLower() == "onclientdisconnectevent") {
							int? id = ((JsonElement)eventClass).GetProperty("ClientID").GetInt32();
							string? name = ((JsonElement)eventClass).GetProperty("UserName").GetString();
							bool? success = ((JsonElement)eventClass).GetProperty("Success").GetBoolean();
						    OtherClients.RemoveAll(x => x.ID == id);
							if (id == null || name == null) continue;
							eventClass = new OnClientDisconnectEvent(id,name,success);
                        }
						listener?.ExecuteEvent(eventClass);
						continue;
					}

					// HANDLE NETWORK MESSAGE
					var msgBytes = new Utf8JsonReader(bytes);
					NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;
					
					DebugMessage(message,2);
					
					message.Parameters = DeserializeParameters(message.Parameters,message.UseClass);

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
                        method = ClientMethods.FirstOrDefault(x => x.Name.ToLower() == message.MethodName?.ToLower());
                        if (method == default) throw new Exception($"Method {message.MethodName} was not found from Registered Methods!");
                    }
                    
                    // GET PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                    object[]? parameters = null;
                    ParameterInfo[]? parameterInfo = method?.GetParameters();
                    if (parameterInfo?.Count() > 0) {
                        List<object> paramList = new List<object>();
                        ParameterInfo first = parameterInfo[0];
                        if (first.ParameterType == typeof(NetworkMessage)) paramList.Add(message);

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

					switch (message.MessageType)
					{
						// SEND A REQUEST FOR CLIENT/SERVER
						case (int)MessageTypes.RequestData:
							NetworkMessage responseMessage = new NetworkMessage {
								MessageType = 11,
								MethodName = message.MethodName,
								TargetId = message.Sender,
								Key = message.Key
							};
							object? data = method?.Invoke(null,parameters);
							if (data != null) responseMessage.Parameters = data;
							Network.SendData(responseMessage);
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (int)MessageTypes.SendData:
							method?.Invoke(null,parameters);
							break;
						default:
							throw new NotImplementedException();
					}
				}
			} catch (Exception ex) {
				Client.Close();
				if (!Client.HandshakeDone) {
					return;
				}
				Log(ex.Message);
                NetworkEvents? listener = NetworkEvents.eventsListener;
                if (ex.InnerException is SocketException || ex is EndOfStreamException) {
                    OnServerShutdownEvent eventShutdown = new OnServerShutdownEvent(false);
                    listener.ExecuteEvent(eventShutdown,true);
                }
				Log("Disconnected from the server!");
				Client.Dispose();
			}
		}
		
		
		
		/// <summary>
        /// Invoke a method on receivers end. This uses fire and forget mode. (No data to be returned)
        /// </summary>
        /// <param name="message"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
		public static void SendData(NetworkMessage message) {
            if (!IsConnected()) throw new Exception("Not connected to server");
			if (message.TargetId == Client.ID) throw new Exception("Cannot send data to self! (client)");	
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

			if (message.TargetId != 1) {
				var found = ClientMethods.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
				if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
			} else {
				var found = ServerMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
				if (found == default) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
			}
			if (message.TargetId == 0) Log($"DATA SENT TO: ({OtherClients.Count()}) CLIENT(s)!");
			SendMessage(message,Client.GetStream());
			DebugMessage(message,1);
        }
		


		/// <summary>
        /// Request data from target by invoking its method using ASYNC
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
		public static async void SendDataAsync(NetworkMessage message) {
            await Task.Run(() => { SendData(message); });
		}



		/// <summary>
        /// Request data from target by invoking its method.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
		public static dynamic RequestData(NetworkMessage message) {;
            if (!IsConnected()) throw new Exception("Not connected to server");
			if (message.TargetId == Client.ID) throw new Exception("Cannot request data from self!");
			if (message.TargetId != 1) {
				if ((OtherClients.SingleOrDefault(x => x.ID == message.TargetId)) == default) throw new Exception("Invalid target ID. ID not listed in clients list!");
		
				var found = ClientMethods.FirstOrDefault(x => x.Name.ToString().ToLower() == message.MethodName?.ToLower());
				if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
				if (found.ReturnType == typeof(void)) throw new ArgumentException($"Method {message.MethodName} doesn't have a return value! (Uses void) Set message.Parameters to null before requesting data!");	
			} else {
				var found = ServerMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
				if (found == default) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
				if (found.ReturnType == typeof(void)) throw new ArgumentException($"Method {message.MethodName} doesn't have a return value! (Uses void) Set message.Parameters to null before requesting data!");			
			}
			message.MessageType = (int?)MessageTypes.RequestData;
			SendMessage(message,Client.GetStream());
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

		private static void SendMessage(dynamic message, NetworkStream Stream) {
			if (message is NetworkMessage && (!message.isHandshake)) {
				NetworkEvents? listener = NetworkEvents.eventsListener;
				listener?.ExecuteEvent(new OnMessageSentEvent(message));
			}
			
			if (message is NetworkMessage && !(message.Parameters is null)) {
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
		private static int Handshake(string userName) {
			Client.UserName = userName;

			string? clientVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
			if (clientVersion == null) clientVersion = "1.0.0.0";

			Log($"Starting HANDSHAKE with server, with version: {clientVersion}, with name: {userName}");

			object[] methodsToSend = ClientMethods.Select(x => new object[] {
				x.Name,
				x.ReturnType.ToString(),
				x.GetParameters().
					Where(y => y.ParameterType != typeof(NetworkMessage) && y.ParameterType != typeof(NetworkClient)).
					Select(z => z.ParameterType.ToString()).ToArray()
			}).ToArray();
			
			NetworkMessage handshakeMessage = new NetworkMessage {
				MessageType = (int?)MessageTypes.RequestData,
				TargetId = 1,
				isHandshake = true,
				Parameters = new object[] {clientVersion,userName,methodsToSend},
				Sender = -1
			};

			SendMessage(handshakeMessage,Client.GetStream());
            DebugMessage(handshakeMessage);

            // TODO Add timeout
            byte[] bytes = ReadMessageBytes(Client.GetStream());
			if (bytes.Count() == 0) {
                Client.Writer?.Close();
                Client.Reader?.Close();
                Client.Stream?.Close();
                Client.Client.Close();
				throw new Exception("ERROR HANDSHAKE! UNKNOWN REASON (SERVER)");
			}
			
			var utf8Reader = new Utf8JsonReader(bytes);
			NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;
			object[] returnedParams = DeserializeParameters(returnMessage.Parameters);

			int _clientID = (int)returnedParams[0];
			string? ServerVersion = (string)returnedParams[1];

			NetworkEvents? listener = NetworkEvents.eventsListener;
			listener?.ExecuteEvent(new OnHandShakeStartEvent(clientVersion,userName,0),true);

			if (_clientID < 0) {
				listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion,userName,-1,false,_clientID * 1));
                if (_clientID == -2) throw new Exception($"Version mismatch! You have: {clientVersion}, server has: {ServerVersion}");
				if (_clientID == -3) throw new Exception($"Username:{userName} already in use!");
				throw new Exception($"Handshake failed. Code:{_clientID}");
			}

			Client.ID = _clientID;
			Client.UserName = userName;
			
			object[] methods = (object[])returnedParams[2];
			foreach (object[] method in methods) {
                Type? type = Type.GetType((string)method[1]);
				if (type == null) continue;
				object[] paramTypes = (object[])method[2];
				List<Type> typeList = new List<Type> {};
				foreach (string paramType in paramTypes) {
					Type? typeThis = Type.GetType(paramType);
					if (typeThis != null) typeList.Add(typeThis);
				}
				ServerMethods.Add(new NetworkMethodInfo(
					(string)method[0],
					type,
					typeList.ToArray<Type>()
				));
			}
			Log($"DEBUG: Added ({ServerMethods.Count()}) SERVER methods to list!");
			
			object[] clients = (object[])returnedParams[3];
			foreach (object[] clientData in clients) {
				OtherClients.Add(new OtherClient((int)clientData[0], (string)clientData[1]));
			}
			Log($"DEBUG: Added ({OtherClients.Count()}) other clients to list!");

			NetworkMessage handshakeMessageSuccess = new NetworkMessage {
				MessageType = (int?)MessageTypes.SendData,
				TargetId = 1,
				isHandshake = true,
				Sender = Client.ID
			};
			SendMessage(handshakeMessageSuccess,Client.GetStream());

			Client.HandshakeDone = true;

			listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion,userName,_clientID,true),true);

			return _clientID;	
		}


		private static object[] SerializeParameters(dynamic parameters,ref bool useClass) {
            try {
                Type paramType = Type.GetType(parameters.ToString());
                useClass = paramType.GetConstructor(new Type[0])!=null;
                if (useClass) return parameters;
            } catch {}
            
			List<object> newParams = new List<object>(){};
			if (!(parameters is Array)) {
				newParams.Add(parameters.GetType().ToString());
				newParams.Add(parameters);
			} else {
				newParams.Add(parameters.GetType().ToString());
				foreach (var parameter in parameters) {
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
            Log($"TYPE: {type}");
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

		/// <summary>Client instance that holds the client info.</summary>
		public class NetworkClient : TcpClient {
			internal NetworkStream? Stream { get; set; }
			internal StreamReader? Reader { get; set; }
        	internal StreamWriter? Writer { get; set; }
			/// <summary>ID of the Client</summary>
			public int? ID { get; internal set;}
			/// <summary>Username of the client</summary>
			public string UserName { get; internal set; } = "error (NoName)";
			/// <summary>BOOL if the handshake has been completed.</summary>
			public bool HandshakeDone { get; internal set; } = false;
			
		}
	}
}