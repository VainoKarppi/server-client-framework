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

namespace ServerFramework;
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
public partial class Network {

    private static TcpListener? ServerListener;
    private static readonly object _lock = new object();
    /// <summary>Check if the server is running.</summary>
    public static bool ServerRunning { get; internal set; } = false;



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

            string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            if (version != null) ServerVersion = version; // Update version

            NetworkEvents? listener = NetworkEvents.Listener;
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
                    if (ClientList.Contains(_client)) {
                        _client.Close();
                        throw new Exception("Client already connected!");
                    }
                    lock (_lock) ClientList.Add(_client);

                    Log("*NEW* Client (" + _client.Client.RemoteEndPoint + ") trying to connect...");

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

        //--- send event about server Shutdown
        OnServerShutdownEvent shutdownEvent = new OnServerShutdownEvent(true);
        SendEvent(new NetworkEvent(shutdownEvent));

        //--- Run ServerShutdown event (blocked)
        NetworkEvents? listener = NetworkEvents.Listener;
        listener.ExecuteEvent(shutdownEvent, true);
        ServerRunning = false;

        //--- close all sockets before final stop
        foreach (NetworkClient client in ClientList.ToArray()) {
            CloseClient(client);
        }
        ClientList.Clear();
        ServerListener?.Stop();
        ServerListener = null;

        Log("Server stopped!");
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

        //--- Use ResponseData
        if (message.MessageType != 11) {
            var found = ClientMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
        }

        //--- Send to single or multiple users
        if (message.TargetId > 0) {
            NetworkClient? client = ClientList.FirstOrDefault(c => c.ID == message.TargetId);
            if (client == null) throw new Exception("Invalid target!");
            SendMessage(message, client.Stream);
            int mode = message.Sender == 1 ? 1 : 3;
            DebugMessage(message, mode);
        } else {
            int i = 0;
            foreach (NetworkClient client in ClientList) {
                if (message.Sender == client.ID) continue;
                SendMessage(message, client.Stream);
                i++;
            }
            if (message.Sender == 1) Log($"DATA SENT TO {i} USERS(s)!");
            else Log($"DATA FORWARDED TO {i} USERS(s)!");
        }
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
        if (!ServerRunning) throw new InvalidOperationException("Server not running!");
        if (message.TargetId == 0) throw new Exception("Invalid target! Cannot request data from all clients at the same time!");
        if (message.TargetId == ClientID) throw new Exception("Cannot request data from self!");
        
        if (message.MessageType != 11) {
            var found = ClientMethods?.FirstOrDefault(x => x.Name?.ToLower() == message.MethodName?.ToLower());
            if (found == default) throw new Exception($"Method {message.MethodName} not listed in CLIENT'S methods list");
            if (found.ReturnType == typeof(void)) throw new Exception($"Method {message.MethodName} doesn't have a return value! (Uses void)");
        }
        message.MessageType = (int?)MessageTypes.RequestData;

        NetworkClient? client = ClientList.FirstOrDefault(client => client.ID == message.TargetId);
        if (client == null) throw new Exception("Invalid target!");

        SendMessage(message, client.Stream);
        DebugMessage(message, 1);

        return RequestDataResult(message);
    }





    // One thread for one user
    // Start listening data coming from this client
    private static void HandleClientMessages(NetworkClient _client) {
        while (true) {
            try {
                //--- Read message
                byte[] bytes = ReadMessageBytes(_client.GetStream());

                //--- Make sure data is valid, and no socket error
                if (bytes.Count() == 0) {
                    throw new Exception($"ERROR BYTES IN CLIENT: {_client.ID} RECEIVE DATA THREAD!");
                };

                // Check if MSG is ACK
                if (bytes.Count() == 1 && bytes[0] == 0x06) continue;

                //--- Deserialize to dynamic (network event or network message)
                var utf8Reader = new Utf8JsonReader(bytes);
                dynamic messageTemp = JsonSerializer.Deserialize<dynamic>(ref utf8Reader)!;
                string property = ((JsonElement)messageTemp).GetProperty("MessageType").ToString();

                //--- Make sure message type exists (if not reset loop)
                int type = -1;
                if (!Int32.TryParse(property, out type)) continue;
                if (type < 0) continue;

                NetworkEvents? listener = NetworkEvents.Listener;

                //-- HANDLE EVENT
                if (type == 10) {
                    var eventBytes = new Utf8JsonReader(bytes);
                    NetworkEvent? eventMessage = JsonSerializer.Deserialize<NetworkEvent>(ref eventBytes)!;
                    if (eventMessage.Targets != null && eventMessage.Targets.Any((new int[] { 0, 1 }).Contains)) {
                        listener?.ExecuteEvent(eventMessage.EventClass);
                        if (eventMessage.Targets == new int[] { 1 }) continue; // No clients to send the method to
                    }
                    SendEvent(eventMessage);
                    continue;
                }
                var msgBytes = new Utf8JsonReader(bytes);
                NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(ref msgBytes)!;

                //--- FORWARD DATA IF NOT MENT FOR SERVER (+forget)
                if (message.TargetId != 1) {
                    Network.SendData(message);
                    continue;
                }

                DebugMessage(message, 2);

                message.Parameters = DeserializeParameters(message.Parameters, message.UseClass);

                //--- HANDLE HANDSHAKE
                // todo move elsewhere
                if (message.isHandshake != null) {
                    // Return of successfull handshake
                    if (message.MessageType == (int)MessageTypes.SendData) {
                        if (message.Sender > 1) {
                            _client.HandshakeDone = true;

                            NetworkEvent eventMessage = new NetworkEvent {
                                Targets = new int[] { _client.ID * -1 },
                                EventClass = new OnClientConnectEvent(_client.ID, _client.UserName, _client.Version, true)
                            };
                            SendEvent(eventMessage);

                            listener?.ExecuteEvent(eventMessage?.EventClass);

                            Log($"*DEBUG* Handshake done! ({_client.ID})");
                        } else {
                            _client.Close();
                            Log($"*DEBUG* Handshake failed! ({_client.ID})");
                        }
                    } else {
                        HandshakeClient(_client, message.Parameters);
                    }
                    continue;
                }
                listener?.ExecuteEvent(new OnMessageReceivedEvent(message));

                //--- Dump result to array and continue (RETURN DATA)
                if (message.MessageType == 11) {
                    Results.Add(message.Key, message.Parameters);
                    continue;
                }

                //--- GET METHOD INFO
                MethodInfo? method = GetMessageMethodInfo(message.MethodName);


                //--- GET PARAMETERS AND ADD CLIENT AS FIRST PARAMETER
                object[]? parameters = GetMessageParameters(method,message,_client);

                switch (message.MessageType) {

                    //--- Invoke Method and send response back to requester
                    case (int)MessageTypes.RequestData: { 
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
                    }

                    //--- FIRE AND FORGET was used (Dont return method return data)
                    case (int)MessageTypes.SendData: { 
                        method?.Invoke(null, parameters);
                        break;
                    }
                }
            } catch (Exception ex) {
                if (!ServerRunning) break;

                CloseClient(_client);
                bool success = ((ex is IOException || ex is SocketException) && _client.HandshakeDone);
                if (!success) Log(ex.Message);

                if (!_client.HandshakeDone) break;
                Log($"Client {_client.ID} disconnected! (SUCCESS: {success})");

                OnClientDisconnectEvent disconnectEvent = new OnClientDisconnectEvent(_client.ID, _client.UserName, _client.Version, success);
                NetworkEvent eventTemp = new NetworkEvent(disconnectEvent);
                eventTemp.EventClass = disconnectEvent;
                SendEvent(eventTemp);

                NetworkEvents? listener = NetworkEvents.Listener;
                listener?.ExecuteEvent(disconnectEvent, true);

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
        client.Version = clientVersion;

        NetworkEvents? listener = NetworkEvents.Listener;
        listener?.ExecuteEvent(new OnHandShakeStartEvent(clientVersion, userName, client.ID), true);

        // RETURNS client id if success (minus number if error (each value is one type of error))
        Log($"*HANDSHAKE START* ClientVersion:{clientVersion} Name:{userName}");

        NetworkMessage handshakeMessage = new NetworkMessage {
            MessageType = 11,
            isHandshake = true,
            TargetId = client.ID
        };

        //TODO add major and minor checking
        if (clientVersion != ServerVersion) {
            Log($"User {userName} has wrong version! Should be: {ServerVersion} has: {clientVersion}");
            handshakeMessage.Parameters = new object[] { -2, ServerVersion };
            SendMessage(handshakeMessage,client.Stream,false);
            listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion, userName, client.ID, false, 2), true);
            throw new Exception($"User {userName} has wrong version! Should be: {ServerVersion} has: {clientVersion}");
        }

        //TODO Settings.AllowDifferentMethods
        if (!MethodsInitialized) {
            MethodsInitialized = true;
            foreach (object[] method in (object[])parameters[2]) {
                Type? type = Type.GetType((string)method[1]);
                if (type == null) continue;
                object[]? paramTypes = (object[])method[2];
                List<Type> typeList = new List<Type> { };
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
                handshakeMessage.Parameters = new object[] { -3, ServerVersion };
                SendMessage(handshakeMessage,client.Stream,false);
                listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion, userName, client.ID, false, 3), true);
                throw new Exception($"*ERROR* Handshake, Username:{userName} already in use for Client:{usedClient.ID}!");
            }
        }

        List<object[]> clientlist = new List<object[]>() { };
        foreach (NetworkClient toAdd in Network.ClientList) {
            if (!toAdd.Connected || toAdd.ID == client.ID) continue;
            clientlist.Add(new object[] { toAdd.ID, toAdd.UserName });
        }

        List<int> targetList = new List<int>() { };
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

        handshakeMessage.Parameters = new object[] { client.ID, ServerVersion, methodsToSend, clientlist.ToArray() };


        SendMessage(handshakeMessage,client.Stream,false);
        
        new Thread(() => {
            int i = 0;
            while (i < 200) { // 2 second timer
                Thread.Sleep(2);
                if (client.HandshakeDone) {
                    listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion, userName, client.ID, true, null), false);
                    return;
                }
                ++i;
            }
            listener?.ExecuteEvent(new OnHandShakeEndEvent(clientVersion, userName, client.ID, false, 0), true);
            Log($"Handshake time out for Client:{client.ID}");
            CloseClient(client);
        }).Start();
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
        public string Version { get; set; } = "1.0.0.0";
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