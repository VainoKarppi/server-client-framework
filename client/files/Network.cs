using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using static ClientFramework.Logger;
using static ClientFramework.NetworkEvents;

namespace ClientFramework;


/// <summary></summary>
public partial class Network
{
    /// <summary>Client object that is connected to server</summary>
    public static NetworkClient Client = new NetworkClient();

    
    /// <summary>Instance of other client info</summary>
    public class OtherClient
    {
        /// <summary>ID of the other client</summary>
        public int? ID { get; internal set; }
        /// <summary>Username of that client</summary>
        public string? UserName { get; internal set; } = "error (NoName)";
        /// <summary>BOOL If the client is connected to server</summary>
        public bool Connected { get; internal set; } = true;
        internal OtherClient(int? id, string? name, bool connected = true)
        {
            ID = id;
            UserName = name;
            Connected = connected;
        }
    }






    /// <summary>
    /// Checks if connected to server (once handshake is done)
    /// </summary>
    /// <returns>BOOL : TRUE if connected, FALSE if not</returns>
    public static bool IsConnected()
    {
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
    public static void Connect(string ip = "127.0.0.1", int port = 5001, string userName = "unknown")
    {
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
        Log($"*DEBUG* HANDSHAKE DONE: ID={_id}");

        // Continue in new thread
        Thread thread = new Thread(ReceiveDataThread);
        thread.Start();
    }



    /// <summary>
    /// Disconnects from the server
    /// </summary>
    /// <exception cref="Exception">Not connected to server!</exception>
    public static void Disconnect()
    {
        ClientList.Clear();
        if (!IsConnected())
            throw new Exception("Not connected to server!");

        Log("Disconnected From the server!");
        Client.HandshakeDone = false;
        Client.Stream?.Close();
        Client.Writer?.Close();
        Client.Client.Close();

        NetworkEvents? listener = NetworkEvents.eventsListener;
        listener?.ExecuteEvent(new OnClientDisconnectEvent(Client.ID, Client.UserName, true));
    }


    private static void ReceiveDataThread()
    {
        try
        {
            while (Client.Connected)
            {
                byte[] bytes = ReadMessageBytes(Client.GetStream());
                if (bytes.Count() == 0)
                {
                    if (Client.Available != 0) throw new EndOfStreamException("Server closed connection!");
                    Client.Stream?.Close();
                    Client.Close();
                    break;
                }
                
                // Check if MSG is ACK
                if (bytes.Count() == 1 && bytes[0] == 0x06) continue;      

                var utf8Reader = new Utf8JsonReader(bytes);
                dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
                string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

                int type = -1;
                if (!Int32.TryParse(property, out type)) continue;
                if (type < 0) continue;

                // HANDLE EVENT
                NetworkEvents? listener = NetworkEvents.eventsListener;
                if (type == 10)
                {
                    dynamic? eventClass = ((JsonElement)messageTemp).GetProperty("EventClass");
                    string? eventName = (eventClass is JsonElement) ? ((JsonElement)eventClass).GetProperty("EventName").GetString() : eventClass?.EventName;
                    if (eventName?.ToLower() == "onclientconnectevent")
                    {
                        int? id = ((JsonElement)eventClass).GetProperty("ClientID").GetInt32();
                        string? name = ((JsonElement)eventClass).GetProperty("UserName").GetString();
                        bool? success = ((JsonElement)eventClass).GetProperty("Success").GetBoolean();
                        if (id == null || name == null) continue;

                        eventClass = new OnClientConnectEvent(id, name, success);
                        if (id != Client.ID) ClientList.Add(new OtherClient(id, name));
                    }
                    if (eventName?.ToLower() == "onclientdisconnectevent")
                    {
                        int? id = ((JsonElement)eventClass).GetProperty("ClientID").GetInt32();
                        string? name = ((JsonElement)eventClass).GetProperty("UserName").GetString();
                        bool? success = ((JsonElement)eventClass).GetProperty("Success").GetBoolean();
                        ClientList.RemoveAll(x => x.ID == id);
                        if (id == null || name == null) continue;
                        eventClass = new OnClientDisconnectEvent(id, name, success);
                    }
                    listener?.ExecuteEvent(eventClass);
                    continue;
                }

                // HANDLE NETWORK MESSAGE
                var msgBytes = new Utf8JsonReader(bytes);
                NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;

                DebugMessage(message, 2);

                message.Parameters = DeserializeParameters(message.Parameters, message.UseClass);

                listener?.ExecuteEvent(new OnMessageReceivedEvent(message));

                // Dump result to array and continue
                if (message.MessageType == 11)
                {
                    Results.Add(message.Key, message.Parameters);
                    continue;
                }

                // GET METHOD INFO
                int methodId;
                MethodInfo? method;
                bool isInt = int.TryParse(message.MethodName, out methodId);
                if (isInt && (methodId < 0))
                {
                    string methodName = PrivateMethods[Math.Abs(methodId) - 1];
                    method = typeof(Network).GetMethod(methodName);
                }
                else
                {
                    method = ClientMethods.FirstOrDefault(x => x.Name.ToLower() == message.MethodName?.ToLower());
                    if (method == default) throw new Exception($"Method {message.MethodName} was not found from Registered Methods!");
                }

                // GET PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                object[]? parameters = null;
                ParameterInfo[]? parameterInfo = method?.GetParameters();
                if (parameterInfo?.Count() > 0)
                {
                    List<object> paramList = new List<object>();
                    ParameterInfo first = parameterInfo[0];
                    if (first.ParameterType == typeof(NetworkMessage)) paramList.Add(message);

                    if (message.Parameters != null)
                    {
                        if (message.Parameters is Array)
                        {
                            foreach (var item in message.Parameters)
                            {
                                if (method?.GetParameters().Count() == paramList.Count()) break; // Not all parameters can fill in
                                paramList.Add(item);
                            }
                        }
                        else
                        {
                            paramList.Add(message.Parameters);
                        }
                    }
                    parameters = paramList.ToArray();
                }

                switch (message.MessageType)
                {
                    // SEND A REQUEST FOR CLIENT/SERVER
                    case (int)MessageTypes.RequestData:
                        NetworkMessage responseMessage = new NetworkMessage
                        {
                            MessageType = 11,
                            MethodName = message.MethodName,
                            TargetId = message.Sender,
                            Key = message.Key
                        };
                        object? data = method?.Invoke(null, parameters);
                        if (data != null) responseMessage.Parameters = data;
                        Network.SendData(responseMessage);
                        break;

                    // FIRE AND FORGET (Dont return method return data)
                    case (int)MessageTypes.SendData:
                        method?.Invoke(null, parameters);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
        catch (Exception ex)
        {
            Client.Close();
            if (!Client.HandshakeDone)
            {
                return;
            }
            Log(ex.Message);
            NetworkEvents? listener = NetworkEvents.eventsListener;
            if (ex.InnerException is SocketException || ex is EndOfStreamException)
            {
                OnServerShutdownEvent eventShutdown = new OnServerShutdownEvent(false);
                listener.ExecuteEvent(eventShutdown, true);
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
    public static void SendData(NetworkMessage message)
    {
        if (!IsConnected()) throw new Exception("Not connected to server");
        if (message.TargetId == Client.ID) throw new Exception("Cannot send data to self! (client)");
        if (message.MessageType == null) message.MessageType = (int?)MessageTypes.SendData;

        if (message.TargetId != 1)
        {
            var found = ClientMethods.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
        }
        else
        {
            var found = ServerMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
        }
        if (message.TargetId == 0) Log($"DATA SENT TO: ({ClientList.Count()}) CLIENT(s)!");
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
    public static dynamic RequestData(NetworkMessage message)
    {
        if (!IsConnected()) throw new Exception("Not connected to server");
        if (message.TargetId == Client.ID) throw new Exception("Cannot request data from self!");
        if (message.TargetId == 0) throw new Exception("Invalid target! Cannot request data from all clients at the same time!");
        if (message.TargetId != 1)
        {
            if ((ClientList.SingleOrDefault(x => x.ID == message.TargetId)) == default) throw new Exception("Invalid target ID. ID not listed in clients list!");

            var found = ClientMethods.FirstOrDefault(x => x.Name.ToString().ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
            if (found.ReturnType == typeof(void)) throw new ArgumentException($"Method {message.MethodName} doesn't have a return value! (Uses void) Set message.Parameters to null before requesting data!");
        }
        else
        {
            var found = ServerMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in SERVER'S methods list");
            if (found.ReturnType == typeof(void)) throw new ArgumentException($"Method {message.MethodName} doesn't have a return value! (Uses void) Set message.Parameters to null before requesting data!");
        }
        message.MessageType = (int?)MessageTypes.RequestData;
        SendMessage(message, Client.GetStream());
        DebugMessage(message, 1);
        return RequestDataResult(message);
    }




    private static int Handshake(string userName)
    {
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

        NetworkMessage handshakeMessage = new NetworkMessage
        {
            MessageType = (int?)MessageTypes.RequestData,
            TargetId = 1,
            isHandshake = true,
            Parameters = new object[] { clientVersion, userName, methodsToSend }
        };

        SendMessage(handshakeMessage, Client.GetStream(),false);
        DebugMessage(handshakeMessage);

        // TODO Add timeout
        byte[] bytes = ReadMessageBytes(Client.GetStream());
        if (bytes.Count() == 0)
        {
            Client.Writer?.Close();
            Client.Reader?.Close();
            Client.Stream?.Close();
            Client.Client.Close();
            throw new Exception("ERROR HANDSHAKE! UNKNOWN REASON (SERVER)");
        }

        var utf8Reader = new Utf8JsonReader(bytes);
        NetworkMessage? returnMessage = JsonSerializer.Deserialize<NetworkMessage>(ref utf8Reader)!;
        DebugMessage(returnMessage);
        object[] returnedParams = DeserializeParameters(returnMessage.Parameters);

        int _clientID = (int)returnedParams[0];
        ServerVersion = (string)returnedParams[1];

        NetworkEvents? listener = NetworkEvents.eventsListener;
        listener?.ExecuteEvent(new OnHandShakeStartEvent(clientVersion, userName, 0), true);

        if (_clientID < 0)
        {
            listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion, userName, -1, false, _clientID * 1));
            if (_clientID == -2) throw new Exception($"Version mismatch! You have: {clientVersion}, server has: {ServerVersion}");
            if (_clientID == -3) throw new Exception($"Username:{userName} already in use!");
            throw new Exception($"Handshake failed. Code:{_clientID}");
        }


        Client.ID = _clientID;
        Client.UserName = userName;

        object[] methods = (object[])returnedParams[2];
        foreach (object[] method in methods)
        {
            try
            {
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

        object[] clients = (object[])returnedParams[3];
        foreach (object[] clientData in clients)
        {
            ClientList.Add(new OtherClient((int)clientData[0], (string)clientData[1]));
        }
        Log($"*DEBUG* Added ({ClientList.Count()}) other clients to list!");

        NetworkMessage handshakeMessageSuccess = new NetworkMessage
        {
            MessageType = (int?)MessageTypes.SendData,
            TargetId = 1,
            isHandshake = true,
            Sender = Client.ID
        };
        SendMessage(handshakeMessageSuccess, Client.GetStream(),false);

        Client.HandshakeDone = true;

        listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion, userName, _clientID, true), true);
        
        ClientID = _clientID;

        return _clientID;
    }



    /// <summary>Client instance that holds the client info.</summary>
    public class NetworkClient : TcpClient
    {
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