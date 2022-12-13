using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using static ClientFramework.Logger;
using static ClientFramework.NetworkEvents;

namespace ClientFramework;


/// <summary></summary>
public partial class Network {
    /// <summary>Client object that is connected to server</summary>
    public static NetworkClient Client = new NetworkClient();
    public static string ClientVersion { get; private set; } = "1.0.0.0";


    /// <summary>Instance of other client info</summary>
    public class OtherClient {
        /// <summary>ID of the other client</summary>
        public int? ID { get; internal set; }
        /// <summary>Username of that client</summary>
        public string? UserName { get; internal set; } = "error (NoName)";
        /// <summary>BOOL If the client is connected to server</summary>
        public bool Connected { get; internal set; } = true;
        internal OtherClient(int? id, string? name, bool connected = true) {
            ID = id;
            UserName = name;
            Connected = connected;
        }
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

        //--- Create new main client object class
        Client = new NetworkClient();

        //--- Connect to server
        Log("Trying to connect at: (" + ip + ":" + port.ToString() + "), with name: " + userName);
        Client.Connect(IPAddress.Parse(ip), port);

        //--- Update version
        string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        if (version != null) ClientVersion = version; 

        //--- Get Network Objects
        Client.Stream = Client.GetStream();
        Client.Reader = new StreamReader(Client.Stream);
        Client.Writer = new StreamWriter(Client.Stream);

        //--- Request client ID and do handshake
        int _id = Network.Handshake(userName);
        if (_id < 2) return;
        Log($"*DEBUG* HANDSHAKE DONE: ID={_id}");

        //--- Continue in new thread
        Thread thread = new Thread(ReceiveDataThread);
        thread.Start();
    }



    /// <summary>
    /// Disconnects from the server
    /// </summary>
    /// <exception cref="Exception">Not connected to server!</exception>
    public static void Disconnect() {
        //--- Clear client list
        ClientList.Clear();
        if (!IsConnected())
            throw new Exception("Not connected to server!");

        //--- Remove Network Objects
        Log("Disconnected From the server!");
        Client.HandshakeDone = false;
        Client.Stream?.Close();
        Client.Writer?.Close();
        Client.Client.Close();

        //--- Invoke OnClientDisconnectEvent event
        NetworkEvents? listener = NetworkEvents.Listener;
        listener?.ExecuteEvent(new OnClientDisconnectEvent(Client.ID, Client.UserName, ClientVersion, true));
    }


    private static void ReceiveDataThread() {
        try {
            while (Client.Connected) {
                //--- Read message
                byte[] bytes = ReadMessageBytes(Client.GetStream());

                //--- Make sure data is valid, and no socket error
                if (bytes.Count() == 0) {
                    if (Client.Available != 0) throw new EndOfStreamException("Server closed connection!");
                    Client.Stream?.Close();
                    Client.Close();
                    break;
                }
                
                //--- Check if MSG is ACK
                if (bytes.Count() == 1 && bytes[0] == 0x06) continue;      

                //--- Deserialize to dynamic (network event or network message)
                var utf8Reader = new Utf8JsonReader(bytes);
                dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
                string messageType = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

                //--- Make sure message type exists (if not reset loop)
                int type = -1;
                if (!Int32.TryParse(messageType, out type)) continue;
                if (type < 0) continue;

                //-- HANDLE EVENT
                if (type == 10) {
                    HandleEventMessage(messageTemp);
                    continue;
                }

                //--- HANDLE NETWORK MESSAGE
                var msgBytes = new Utf8JsonReader(bytes);
                NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;
                DebugMessage(message, 2);

                //--- Deserialize
                message.Parameters = DeserializeParameters(message.Parameters, message.UseClass);

                //--- Execute OnMessageReceivedEvent event once deserialized
                NetworkEvents? listener = NetworkEvents.Listener;
                listener?.ExecuteEvent(new OnMessageReceivedEvent(message));

                //--- Dump result to array and continue (RequestData's RESPONSE)
                if (message.MessageType == 11) {
                    Results.Add(message.Key, message.Parameters);
                    continue;
                }

                //--- (Is either SendData or RequestData)
                //--- GET METHOD INFO
                MethodInfo? method = GetMessageMethodInfo(message.MethodName); 
                

                //---- GET PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                object[]? parameters = GetMessageParameters(method, message);
                

                switch (message.MessageType) {

                    //--- Invoke Method and send response back to requester
                    case (int)MessageTypes.RequestData: {
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


                    //--- FIRE AND FORGET (Dont return method return data)
                    case (int)MessageTypes.SendData: {
                        method?.Invoke(null, parameters);
                        break;
                    }
                }
            }
        } catch (Exception ex) {
            Client.Close();
            if (!Client.HandshakeDone)
            {
                return;
            }
            Log(ex.Message);
            NetworkEvents? listener = NetworkEvents.Listener;
            if (ex.InnerException is SocketException || ex is EndOfStreamException)
            {
                OnServerShutdownEvent eventShutdown = new OnServerShutdownEvent(false);
                listener.ExecuteEvent(eventShutdown, true);
            }
            Log("Disconnected from the server!");
            Client.Dispose();
        }
    }

    private static void HandleEventMessage(dynamic message) {
        dynamic? eventClass = ((JsonElement)message).GetProperty("EventClass");
        string? eventName = (eventClass is JsonElement) ? ((JsonElement)eventClass).GetProperty("EventName").GetString() : eventClass?.EventName;
        
        //--- Check if message is "onclientconnectevent"
        if (eventName?.ToLower() == "onclientconnectevent") {
            int? id = ((JsonElement)eventClass).GetProperty("ClientID").GetInt32();
            string? name = ((JsonElement)eventClass).GetProperty("UserName").GetString();
            bool? success = ((JsonElement)eventClass).GetProperty("Success").GetBoolean();
            if (id == null || name == null) return;

            eventClass = new OnClientConnectEvent(id, name, ClientVersion, success);
            if (id != Client.ID) ClientList.Add(new OtherClient(id, name)); // ADD CLIENT TO CLIENTLIST
        }

        //--- Check if message is "onclientdisconnectevent"
        if (eventName?.ToLower() == "onclientdisconnectevent") {
            int? id = ((JsonElement)eventClass).GetProperty("ClientID").GetInt32();
            string? name = ((JsonElement)eventClass).GetProperty("UserName").GetString();
            bool? success = ((JsonElement)eventClass).GetProperty("Success").GetBoolean();
            ClientList.RemoveAll(x => x.ID == id); // REMOVE CLIENT FROM CLIENT LIST
            if (id == null || name == null) return;
            eventClass = new OnClientDisconnectEvent(id, name, ClientVersion, success);
        }

        //--- Run event
        NetworkEvents? listener = NetworkEvents.Listener;
        listener?.ExecuteEvent(eventClass);
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

        //--- If sendData or RequestData
        if (message.MessageType != 11) {
            if (message.TargetId != 1) {
                var found = ClientMethods.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
                if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
            } else {
                var found = ServerMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
                if (found == default) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
            }
            if (message.TargetId == 0) Log($"DATA SENT TO: ({ClientList.Count()}) CLIENT(s)!");
        }

        SendMessage(message, Client.GetStream());
        DebugMessage(message, 1);
    }







    /// <summary>
    /// Request data from target by invoking its method.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="Exception"></exception>
    public static dynamic RequestData(NetworkMessage message) {
        if (!IsConnected()) throw new Exception("Not connected to server");
        if (message.TargetId == Client.ID) throw new Exception("Cannot request data from self!");
        if (message.TargetId == 0) throw new Exception("Invalid target! Cannot request data from all clients at the same time!");
        if (message.TargetId != 1) {
            if ((ClientList.SingleOrDefault(x => x.ID == message.TargetId)) == default) throw new Exception("Invalid target ID. ID not listed in clients list!");

            var found = ClientMethods.FirstOrDefault(x => x.Name.ToString().ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
            if (found.ReturnType == typeof(void)) throw new ArgumentException($"Method {message.MethodName} doesn't have a return value! (Uses void) Set message.Parameters to null before requesting data!");
        } else {
            var found = ServerMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
            if (found.ReturnType == typeof(void)) throw new ArgumentException($"Method {message.MethodName} doesn't have a return value! (Uses void) Set message.Parameters to null before requesting data!");
        }
        message.MessageType = (int?)MessageTypes.RequestData;
        SendMessage(message, Client.GetStream());
        DebugMessage(message, 1);
        return RequestDataResult(message);
    }




    private static int Handshake(string userName) {
        Client.UserName = userName;

        Log($"Starting HANDSHAKE with server, with version: {ClientVersion}, with name: {userName}");

        //--- Build array of client methods to be sent to 
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
            Parameters = new object[] { ClientVersion, userName, methodsToSend }
        };

        SendMessage(handshakeMessage, Client.GetStream(),false);
        DebugMessage(handshakeMessage);

        
        //--- Read and wait for response
        byte[] bytes = ReadMessageBytes(Client.GetStream());

        // TODO Add timeout
        //--- Check if Unhandled socket error
        if (bytes.Count() == 0) {
            Client.Writer?.Close();
            Client.Reader?.Close();
            Client.Stream?.Close();
            Client.Client.Close();
            throw new Exception("ERROR HANDSHAKE! UNKNOWN REASON (SERVER)");
        }

        //--- Deserialize returned data
        var utf8Reader = new Utf8JsonReader(bytes);
        NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;
        object[] returnedParams = DeserializeParameters(returnMessage.Parameters);
        DebugMessage(returnMessage);

        //--- Get temporary client ID and store servers version
        int _clientID = (int)returnedParams[0];
        ServerVersion = (string)returnedParams[1];

        //--- Start handshake
        NetworkEvents? listener = NetworkEvents.Listener;
        listener?.ExecuteEvent(new OnHandShakeStartEvent(ClientVersion, userName, 0), true);

        //--- Handle if error code was returned
        if (_clientID < 0) {
            listener?.ExecuteEvent(new OnHandShakeEndEvent(ClientVersion, userName, -1, false, _clientID * 1));
            if (_clientID == -2) throw new Exception($"Version mismatch! You have: {ClientVersion}, server has: {ServerVersion}");
            if (_clientID == -3) throw new Exception($"Username:{userName} already in use!");
            throw new Exception($"Handshake failed. Code:{_clientID}");
        }

        //--- Store username and ID's
        Client.ID = _clientID;
        ClientID = _clientID;
        Client.UserName = userName;

        //--- Build server methods
        object[] methods = (object[])returnedParams[2];
        foreach (object[] method in methods) {
            try {
                string returnType = (string)method[1];
                Type? type = Type.GetType(returnType);
                if (type == null) throw new Exception($"INVALID return value type ({returnType}), found for: {(string)method[0]}");

                List<Type> typeList = new List<Type> { };
                foreach (string paramType in (object[])method[2])
                {
                    Type? typeThis = Type.GetType(paramType);
                    if (typeThis != null) typeList.Add(typeThis);
                }
                ServerMethods.Add(new NetworkMethodInfo(
                    (string)method[0],
                    type,
                    typeList.ToArray<Type>()
                ));
            }
            catch { }
        }
        Log($"*DEBUG* Added ({ServerMethods.Count()}) SERVER methods to list!");

        //--- Build client list of other clients
        object[] clients = (object[])returnedParams[3];
        foreach (object[] clientData in clients) {
            ClientList.Add(new OtherClient((int)clientData[0], (string)clientData[1]));
        }
        Log($"*DEBUG* Added ({ClientList.Count()}) other clients to list!");

        //--- Send message of successfull deserialization and initalization
        NetworkMessage handshakeMessageSuccess = new NetworkMessage
        {
            MessageType = (int?)MessageTypes.SendData,
            TargetId = 1,
            isHandshake = true
        };
        SendMessage(handshakeMessageSuccess, Client.GetStream(),false);

        //--- End handshake
        Client.HandshakeDone = true;
        listener?.ExecuteEvent(new OnHandShakeEndEvent(ClientVersion, userName, _clientID, true), true);
        listener?.ExecuteEvent(new OnClientConnectEvent(_clientID, userName, ClientVersion, true), true);
        
        return _clientID;
    }



    /// <summary>Client instance that holds the client info.</summary>
    public class NetworkClient : TcpClient {
        internal NetworkStream? Stream { get; set; }
        internal StreamReader? Reader { get; set; }
        internal StreamWriter? Writer { get; set; }
        /// <summary>ID of the Client</summary>
        public int? ID { get; internal set; }
        /// <summary>Username of the client</summary>
        public string UserName { get; internal set; } = "error (NoName)";
        /// <summary>BOOL if the handshake has been completed.</summary>
        public bool HandshakeDone { get; internal set; } = false;
    }
}