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
        public enum MessageTypes : int {SendData, RequestData, ResponseData, EventData}
		public class NetworkMessage
		{
			public int? MessageType { get; set; } = 0;
			// One of the tpes in "MessageTypes"
			public int? TargetId { get; set; } = 1;
			// 0 = everyone, 1 = server, 2 = client 1...
			public int MethodId { get; set; }
			// Minus numbers are for internal use!
			public List<object>? Parameters { get; set; } = new List<object>() {};
			// Array of parameters passed to method that is going to be executed
			public int Key { get; set; } = new Random().Next(100,int.MaxValue);
			// Key for getting the response for specific request (0-100) = event id
			public int? Sender { get; set; } = ClientID;
			// Id of the sender. Can be null in case handshake is not completed
			public bool isHandshake { get; set; } = false;
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string> ClientMethods = new List<string>() {"Disconnect","ConnectedClients","Test","TestArray"};
        private static List<string> PrivateMethods = new List<string>() {"GetMethods","ConnectedClients","HandleEvent"};
		public static Dictionary<int,object[]> Results = new Dictionary<int,object[]>();

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
                        Console.WriteLine(ex.Message);
                    }
                }
                Console.WriteLine("EXITING WHILE LOOP FATAL ERROR");
            }).Start();
        }
        public static void StopServer() {
            if (!ServerRunning)
                throw new Exception("Server not running!");
                
            Console.WriteLine("Stopping server...");
            ServerRunning = false;
            ServerListener.Stop();
            ServerListener = default!;
            ClientList.Clear();

            // TODO Send msg to all clients for succesfull server shutdown?
            Console.WriteLine("Server stopped!");
        }

        // TODO Add Async method aswell
        // TODO Add data type as header + allow any data type using encode/decode
        // Request ID is used to answer to specific message

        
        public static void SendData(NetworkMessage message) {
            if (!ServerRunning) throw new Exception("Server not running!");
			if (message.TargetId == 1) throw new Exception("Cannot send data to self (server)!");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;
            DebugMessage(message);

            byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
            
            // Send to single ro multiple users
            if (message.TargetId > 0) {
                NetworkClient client = ClientList.FirstOrDefault(c => c.ID == message.TargetId);
                if (client == default) throw new Exception("Invalid target!");
                
                client.GetStream().WriteAsync(msg, 0, msg.Length);
            } else {
                var clients = from s in ClientList where s.ID == message.TargetId select s;
                foreach (NetworkClient clientTemp in clients) {
                    NetworkStream stream = clientTemp.GetStream();
                    stream.WriteAsync(msg, 0, msg.Length);
                }
            }
        }
        

        public static object[] RequestData(NetworkMessage message) {
			if (!ServerRunning) throw new Exception("Not connected to server");
			
			message.MessageType = (int?)MessageTypes.RequestData;
			message.Key = new Random().Next(1,int.MaxValue);
			message.Sender = 1;

			// Send request
			byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
            NetworkClient client = (NetworkClient)ClientList.Select(client => client.ID == message.TargetId);
            if (client == null) throw new Exception("Invalid target!");
			client.GetStream().WriteAsync(msg, 0, msg.Length);

			
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


        // One thread for one user
        // Start listening data coming from this client
        public static void HandleClient(NetworkClient _client) {
            while (true) {
                try {
                    NetworkStream stream = _client.GetStream();
                    byte[] bytes = new byte[1024];
					stream.Read(bytes, 0, 1024);

                    var utf8Reader = new Utf8JsonReader(bytes);
                    NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;
                    DebugMessage(message);
                    object[] parsedParameters = DeserializeParameters(message.Parameters);

                    // HANDLE HANDSHAKE
                    if (message.isHandshake && message.MethodId == default) {
                        object[] data = Handshake(_client, (int)parsedParameters[0], (string)parsedParameters[1]);
                        //object data = typeof(Network).GetMethod("Handshake").Invoke("Handshake",parameters);
                        NetworkMessage handshakeMessage = new NetworkMessage {
                            MessageType = ((byte)MessageTypes.ResponseData),
                            MethodId = message.MethodId
                        };
                        if (data != null) handshakeMessage.Parameters = SerializeParameters(data);
                        handshakeMessage.TargetId = Int32.Parse(data[0].ToString());
                        Network.SendData(handshakeMessage);
                        Console.WriteLine($"*SUCCESS* Handshake done! ({_client.ID})");
                        continue;
                    }



                    // DESERIALISE PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                    List<object> paramList = new List<object>();
                    paramList.Add(_client);
                    if (message.Parameters != null) {
                        foreach (object p in parsedParameters) {
                            paramList.Add(p);
                        }
                    }
                    object[] parameters = paramList.ToArray();

                
                    // FORWARD DATA IF NOT MENT FOR SERVER (+forget)
                    if (message.TargetId != 1) {
                        Network.SendData(message);
                        continue;
                    }
                    
                    
                    // Dump result to array and continue
					if (message.MessageType == (byte)MessageTypes.ResponseData) {
                        Results.Add(message.Key,parameters);
                        continue;
                    }

                    
                    

                    // GET METHOD INFO
                    string method;
                    MethodInfo methodInfo;
                    if (message.MethodId < 0) {
                        method = PrivateMethods[Math.Abs(message.MethodId) - 1];
                        methodInfo = typeof(Network).GetMethod(method);
                    } else {
                        method = ClientMethods[message.MethodId];
                        methodInfo = typeof(ServerMethods).GetMethod(method);
                        if (methodInfo == null) throw new Exception($"Method {message.MethodId} was not found ({method})");
                    }
                    

					switch (message.MessageType)
					{
						// SEND A RESPONSE FOR CLIENT/SERVER
						case (byte)MessageTypes.RequestData:
                            if (message.TargetId > 1) {
                                Network.SendData(message); // FORWARD TO CLIENT
                            } else {
                                NetworkMessage responseMessage = new NetworkMessage {
                                    MessageType = ((byte)MessageTypes.ResponseData),
                                    MethodId = message.MethodId,
                                    TargetId = message.Sender,
                                    Sender = 1,
                                    Key = message.Key
                                };
                                // HANDLE ON SERVER
                                object? data = default;
                                data = methodInfo?.Invoke(method,parameters);
                                if (data != null) responseMessage.Parameters = SerializeParameters(data);
                                Network.SendData(responseMessage);
                            }
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (byte)MessageTypes.SendData:
							methodInfo?.Invoke(method,parameters);
							break;
						default:
							throw new NotImplementedException();
					}
                    Console.WriteLine("\n");
                } catch (Exception ex) {
                    if (ex is IOException || ex is SocketException)
                        Console.WriteLine("Client " + _client.ID.ToString() + " disconnected!");
                    else
                        Console.WriteLine(ex);
                    break;
                }
            }

            ClientList.Remove(_client);
            _client.Client.Shutdown(SocketShutdown.Both);
            _client.Close();
        }

        public static object[] Handshake(NetworkClient client, int version, string userName) {
            
            // RETURNS client id if success (minus number if error (each value is one type of error))
            Console.WriteLine($"*HANDSHAKE START* Version:{version} Name:{userName}");
            client.UserName = userName;

            object[] returnData = new object[] {client.ID};

            //TODO add major and minor checking
            if (version != Program.Version) {
                Console.WriteLine($"User {userName} has wrong version! Should be: {Program.Version} has: {version}");
                return returnData;
            }

            List<object[]> clientlist = new List<object[]>(){};
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected || toAdd.ID == client.ID) continue;
                clientlist.Add(new object[] {toAdd.ID,toAdd.UserName});
            }

            returnData = new object[] {client.ID,ClientMethods.ToArray(),clientlist.ToArray()};
            return returnData;
        }




        // Used to read 
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
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }
        public int ID { get; set; }
        public string UserName { get; set; } = "error (NoName)";


        public NetworkClient(TcpListener listener)
        {   
            TcpClient _client = listener.AcceptTcpClient();
            //this.Client.Dispose();
            this.Client = _client.Client;
            
            this.Active = true;
            //TcpClient _client = listener.AcceptTcpClient();
            Reader = new StreamReader(this.GetStream());
            Writer = new StreamWriter(this.GetStream());
        }
    }
}
