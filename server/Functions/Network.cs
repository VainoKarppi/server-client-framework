using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace EdenOnlineExtensionServer {

    public class Network {
        public static TcpListener ServerListener;
        public static readonly object _lock = new object();
        public static readonly Dictionary<short,TcpClient> ClientList = new Dictionary<short,TcpClient>();
        public static int ServerPort { get; set; } = 2302;
        public static bool ServerRunning { get; set; } = false;
        public static string WorldName { get; set; } = "vr";
        public static string[] Mods { get; set; } = new string[] {};
        public static Dictionary<ushort,object[]> Results = new Dictionary<ushort,object[]>();

        public static void StartServer() {
            if (ServerRunning)
                throw new Exception("Server already running!");

            new Thread(() => {
                Thread.CurrentThread.IsBackground = true; 
                
                ServerListener = new TcpListener(IPAddress.Any, ServerPort);
                ServerListener.Start();
                ServerRunning = true;

                Log.Write("Running server at: (" + ServerListener.LocalEndpoint + ")");
                Log.Write();

                short _clientID = 2; // (0 = All clients, 1 = server, 2 and above for specific clients)
                while (ServerRunning) {
                    try {
                        // Start accepting clients
                        NetworkClient _client = new NetworkClient(ServerListener);
                        
                        _client.Client.SendTimeout = 5;
                        // Make sure the connection is already not created
                        // TODO not working....
                        if(ClientList.FirstOrDefault(key => key.Value.Client.RemoteEndPoint == _client.Client.RemoteEndPoint).Value != null){
                            _client.Close();
                            throw new Exception("Client already connected!");
                        }
                        _client.ID = _clientID;
                        lock (_lock) ClientList.Add(_clientID,_client);
                        
                        Log.Write("*NEW* Client (" + _client.Client.RemoteEndPoint  + ") trying to connect...");

                        if (_clientID >= 32000) throw new Exception("Max user count reached! (32000)");

                        // Start new thread for each client
                        new Thread(() => HandleClient(_client)).Start();
                        _clientID++;;
                    } catch (Exception ex) {
                        Log.Write(ex.Message);
                    }
                }
                
            }).Start();
        }
        public static void StopServer() {
            if (!ServerRunning)
                throw new Exception("Server not running!");
                
            Log.Write("Stopping server...");
            ServerRunning = false;
            ServerListener.Stop();
            ServerListener = null;
            ClientList.Clear();

            // TODO Send msg to all clients for succesfull server shutdown?
            // TODO Force mission save for all clients
            Log.Write("Server stopped!");
        }

        // TODO Add Async method aswell
        // TODO Add data type as header + allow any data type using encode/decode
        // Request ID is used to answer to specific message

        public static byte[] PrepareMessage(byte messageType, short target , object function, object[] parameters, ushort key = 0) {
            
            short functionNum = (function.GetType() == typeof(string)) ? GetFunctionIndex(function) : (short)function;

            byte[] msg = {messageType,(byte)(functionNum & 255),(byte)(functionNum >> 8),(byte)(target & 255),(byte)(target >> 8),(byte)(key & 255),(byte)(key >> 8)};
            if (parameters != null || parameters.Count() >= 0) {
                string paramsString = Request.Serialize(parameters);
                msg = msg.Concat(Encoding.ASCII.GetBytes(paramsString)).ToArray();
            }
            return msg;
        }
        
        public static void SendData(object function, object[] parameters, short target = 0, short excludedTarget = 0, byte messageType = (byte)Request.MessageTypes.SendData, ushort key = 0) {
            if (target == 1)
                throw new Exception("Cannot send data to from server to server!");
            
            byte[] msg = PrepareMessage(messageType,target,function,parameters,key);

            if (target <= 0) {
                lock (_lock) {
                    foreach (KeyValuePair<short,TcpClient> connection in ClientList) {
                        if (connection.Key == excludedTarget) // Everyone else expect this user
                            continue;
                        NetworkStream stream = connection.Value.GetStream();
                        stream.Write(msg, 0, msg.Length);
                    }
                }
            } else {
                // Dont use lock
                if (!ClientList.ContainsKey(target))
                    throw new Exception("Selected target not in client list!");
                
                ClientList[target].GetStream().Write(msg, 0, msg.Length);
            }
        }
        
        public static object[] RequestData(object function, object[] parameters, short target = 0) {
			try {
                if (target <= 1)
                    throw new Exception("Invalid target!");

                short functionNum = GetFunctionIndex(function);

				// send request (use semi random key to indentify messages)
                Random r = new Random();
                ushort key = (ushort)r.Next(1,65500);

                byte[] msg = PrepareMessage(((byte)Request.MessageTypes.RequestData),target,function,parameters,key);
				(ClientList[target].GetStream()).Write(msg, 0, msg.Length);


                // Wait for response
                object[] returns;
                short timer = 0;
                while (true) {
                    Thread.Sleep(1);
                    if (Results.ContainsKey(key)) {
                        returns = Results[key];
                        Results.Remove(key);
                        break;
                    }
                    if (timer > 100) throw new Exception($"Request {key} ({function}) timed out!");
                    timer++;
                }
                return returns;

			} catch (Exception e) {
				throw new Exception(e.Message);
			}
		}

        public static string ReadDataBytes(TcpClient client, out byte requestType, out string function, out short target, out ushort key) {
            NetworkStream Stream = client.GetStream();
			byte[] bytes = new byte[client.ReceiveBufferSize];
			int byte_count = Stream.Read(bytes, 0, (int)client.ReceiveBufferSize);

			requestType = bytes[0];
			function = Request.Functions[(bytes[2] << 8) + bytes[1]];
			target = (short)((bytes[4] << 8) + bytes[3]);
			key = (ushort)((bytes[6] << 8) + bytes[5]);

			byte[] newBytes = new byte[byte_count - 7];
			Array.Copy(bytes, 7, newBytes, 0, newBytes.Length);
			return Encoding.ASCII.GetString(newBytes);
		}

        // Start listening data coming from this client
        public static void HandleClient(NetworkClient client) {

            while (true) {
                try {
                    byte requestType;
					string function;
					short target;
					ushort key;
                    string rawData = ReadDataBytes(client as TcpClient,out requestType,out function, out target, out key);
                    
                    if (target > 1) { // TODO
                        // FORWARD DATA TO CLIENT X 
                        if (requestType.Equals(Request.MessageTypes.RequestData)) {
                            Network.SendData(function, new object[] {}, target);
                            // TODO
                        }
                        return;
                    }

                    // Parse rawdata to parameters and to function
                    object[] parameters = Request.Deserialize(rawData);

                    // Make sure client function exists
                    // TODO Handle better than object[] if returns array (use ArmaArray instead)
                    MethodInfo methodInfo = typeof(ClientFunctions).GetMethod(function);
                    if (methodInfo == null)
                        throw new Exception("Function: " + function + " was not found");

                    Log.Write($"Invoking function: ({function}) for client: {client.ID}");
                    
                    object returnData = methodInfo.Invoke(function,new object[]{client,parameters,requestType});
                    if (returnData != null && requestType == 1) {
                        Log.Write($"Returning Data: ({returnData.GetType()}): [{returnData.ToString()}] to client: {client.ID}, from function: {function}");
                        Network.SendData(function, new object[] {returnData}, client.ID, 0, (byte)Request.MessageTypes.ResponseData, key);
                    }
                    Log.Write("\n");
                } catch (Exception ex) {
                    if (ex is IOException || ex is SocketException)
                        Log.Write("Client " + client.ID.ToString() + " disconnected!");
                    else
                        Log.Write(ex.Message);
                    break;
                }
            }

            lock (_lock) ClientList.Remove(client.ID);
            client.Client.Shutdown(SocketShutdown.Both);
            client.Close();
        }



        public static object CallMethod(string function, ArmaArray parameters) {
            MethodInfo methodInfo = typeof(ClientFunctions).GetMethod(function);
            if (methodInfo == null)
                throw new Exception("Function " + function + " was not found");

            return methodInfo.Invoke(function,parameters.ToArray()); 
        }


        // TODO implement
        public static void ForwardData(short function, object[] parameters, short target = 0, byte sender = 0) {
            if (target <= 1)
                throw new Exception("Invalid target to forward data to!");
            
            if (!Network.ClientList.ContainsKey(target))
                    throw new Exception("Selected target not in client list!");

            Log.Write(($"Forwarding data... FUNCTION: {0} PARAMS: {1} TARGET: {2} FROM: {3}",Request.Functions[function],String.Join(", ",parameters),target,sender));
            Network.SendData(function, parameters, target);
        }

        public static short GetFunctionIndex(object function) {

			short functionNum = -1;
			if (function.GetType() == typeof(string))
				functionNum = (short)Array.FindIndex(Request.Functions.ToArray(), t => t.Equals(function.ToString(), StringComparison.InvariantCultureIgnoreCase));
			else if(short.TryParse(function.ToString(),out functionNum))
				functionNum = short.Parse(function.ToString());
			else 
				throw new Exception($"Invalid function type: {function} ({function.GetType()})");

			return functionNum;
		}
    }



    public class NetworkClient : TcpClient {
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }
        public short ID { get; set; }
        public string UserName { get; set; }


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
