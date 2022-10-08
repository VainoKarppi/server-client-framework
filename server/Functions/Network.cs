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
        public static TcpListener ServerListener = default!;
        public static readonly object _lock = new object();
        public static readonly List<NetworkClient> ClientList = new List<NetworkClient>();
        public static int ServerPort { get; set; } = 2302;
        public static bool ServerRunning { get; set; }
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
			public int Key { get; set; }
			public int Sender { get; set; }
			public bool isHandshake { get; set;}
			// Used to detect for handshake. Else send error for not connected to server!
		}
		public static List<string> Methods = new List<string>() {"Disconnect","ConnectedClients","Test","TestArray"};
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
            if (!ServerRunning) throw new Exception("Server not running");
			if (message.TargetId == 1) throw new Exception("Cannot send data to self (server)!");
			
			if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;
            DebugMessage(message);
            byte[] msg = JsonSerializer.SerializeToUtf8Bytes(message);
            
            // Send to single ro multiple users
            if (message.TargetId > 0) {
                NetworkClient client = (NetworkClient)ClientList.Where(client => client.ID == message.TargetId);
                if (client == null) throw new Exception("Invalid target!");
                
                client.GetStream().WriteAsync(msg, 0, msg.Length);
            } else {
                List<NetworkClient> clients = (List<NetworkClient>)ClientList.Where(client => client.ID == message.TargetId);
                foreach (NetworkClient clientTemp in clients) {
                    NetworkStream stream = clientTemp.GetStream();
                    stream.Write(msg, 0, msg.Length);
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
			client.GetStream().Write(msg, 0, msg.Length);

			
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

                    Console.WriteLine("1");

                    List<object> paramList = new List<object>();
                    paramList.Add(_client);
                    object[] AAA = DeserializeParameters(message.Parameters);
                    foreach (object p in AAA) {
                        Console.WriteLine("5555");
                        paramList.Add(p);
                    }
                    object[] parameters = paramList.ToArray();
                    Console.WriteLine("2");

                    DebugMessage(message);
                
                    // FORWARD DATA IF NOT MENT FOR SERVER (forget)
                    if (message.TargetId != 1) {
                        Network.SendData(message);
                        continue;
                    }
                    
                    
                    // Dump result to array and continue
					if (message.MessageType == (byte)MessageTypes.ResponseData) {
                        Results.Add(message.Key,parameters);
                        continue;
                    }

                    string method = Methods[message.MethodId];
					MethodInfo methodInfo = typeof(ServerMethods).GetMethod(method);
					if (methodInfo == null) throw new Exception($"Method {message.MethodId} was not found ({method})");

					switch (message.MessageType)
					{
						// SEND A RESPONSE FOR CLIENT/SERVER
						case (byte)MessageTypes.RequestData:
                            if (message.TargetId > 1) {
                                // FORWARD TO CLIENT
                                Network.SendData(message);
                            } else {

                                NetworkMessage responseMessage = new NetworkMessage
                                {
                                    MessageType = ((byte)MessageTypes.ResponseData),
                                    MethodId = message.MethodId,
                                    TargetId = message.Sender,
                                    Sender = 1,
                                    Key = message.Key
                                };

                                // HANDLE ON SERVER
                                object? data = default;
                                if (message.isHandshake) { // HANDSHAKE!
                                    data = typeof(Network).GetMethod("Handshake").Invoke("Handshake",parameters);
                                    if (data != null) responseMessage.Parameters = SerializeParameters(data);
                                    responseMessage.TargetId = Int32.Parse(data.ToString());
                                    byte[] msg = JsonSerializer.SerializeToUtf8Bytes(responseMessage);
                                    _client.GetStream().WriteAsync(msg, 0, msg.Length);
                                } else {
                                    data = methodInfo.Invoke(method,parameters);
                                    if (data != null) responseMessage.Parameters = SerializeParameters(data);
                                    Network.SendData(responseMessage);
                                }
                            }
							break;
						
						// FIRE AND FORGET (Dont return method return data)
						case (byte)MessageTypes.SendData:
							methodInfo.Invoke(method,parameters);
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

        public static int Handshake(NetworkClient client, int version, string userName) {
            
            // RETURNS client id if success (minus number if error (each value is one type of error))
            Console.WriteLine($"version:{version} name:{userName}");
            client.UserName = userName;

            //TODO add major and minor checking
            if (version != Program.Version) {
                Console.WriteLine($"User {userName} has wrong version! Should be: {Program.Version} has: {version}");
                return -1;
            }


            Console.WriteLine($"*SUCCESS* {client.ID} Handshake completed!");

            return client.ID;
        }




        // Used to read 
		public static int GetMethodIndex(string method) {
			return Methods.FindIndex(m => m.ToLower() == method.ToLower());
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
            Console.WriteLine("DASDDS");
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



    public class NetworkClient : TcpClient {
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }
        public int ID { get; set; }
        public string? UserName { get; set; }


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
