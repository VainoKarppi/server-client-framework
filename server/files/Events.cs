using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ServerFramework {
    public class BaseEventClass {
        public string? EventName { get; set; }
        public BaseEventClass() {
            EventName = this.GetType().UnderlyingSystemType.Name;
        }
    }
    public class OnClientConnectEvent : BaseEventClass {
        public int? ClientID { get; set; }
        public string? UserName { get; set; }
        public bool? Success { get; set; } = true;
        public OnClientConnectEvent (int id, string username, bool success = false) {
            ClientID = id;
            UserName = username;
            Success = success;
        }
    }
    public class OnClientDisconnectEvent : BaseEventClass {
        public int? ClientID { get; set; }
        public string? UserName { get; set; }
        public bool? Success { get; set; }
        public OnClientDisconnectEvent (int id, string username, bool success = false) {
            ClientID = id;
            UserName = username;
            Success = success;
        }
    }
    public class OnServerShutdownEvent : BaseEventClass {
        public bool Success { get; set; } = false;
        public OnServerShutdownEvent (bool success) {
            Success = success;
        }
    }
    public class OnServerStartEvent : BaseEventClass {
        public bool Success { get; set; } = false;
        public string? Version { get; set; }
        public OnServerStartEvent (bool success, string? version) {
            Success = success;
            Version = version;
        }
    }
    public class OnMessageSentEvent : BaseEventClass {
        public Network.NetworkMessage Message;
        public OnMessageSentEvent (Network.NetworkMessage message) {
            Message = message;
        }
    }
    public class OnMessageReceivedEvent : BaseEventClass {
        public Network.NetworkMessage Message;
        public OnMessageReceivedEvent (Network.NetworkMessage message) {
            Message = message;
        }
    }
    public class OnHandShakeStartEvent : BaseEventClass {
        public string? ClientVersion { get; set; }
        public string? ServerVersion { get; set; }
        public string? UserName { get; set; }
        /// <summary>
        /// Client ID not available on client event!
        /// </summary>
        public int? ClientID { get; set; }
        public OnHandShakeStartEvent (string? version, string? serverVersion, string username, int? id) {
            ClientID = id;
            ClientVersion = version;
            ServerVersion = serverVersion;
            UserName = username;
        }
    }
    public class OnHandShakeEndEvent : BaseEventClass {
        public string? ClientVersion { get; set; }
        public string? ServerVersion { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; }
        public int ClientID { get; set; }
        /// <summary>
        /// 0 = not defined, 1 = server issue, not defined, 2 = version mismatch, 3 = username already in use
        /// </summary>
        public int StatusCode { get; set;  }
        public OnHandShakeEndEvent (string? version, string? serverVersion, string username, int id, bool success = false, int code = 0) {
            Success = success;
            ClientVersion = version;
            ServerVersion = serverVersion;
            UserName = username;
            StatusCode = code;
            ClientID = id;
        }
    }



    public class NetworkEvents {
        public static NetworkEvents eventsListener { get; set; } = new NetworkEvents();
        internal void ExecuteEvent(dynamic? classData, bool useBlocked = false) {
            Action action = (() => {
                try {
                    string? eventName = (classData is JsonElement) ? ((JsonElement)classData).GetProperty("EventName").GetString() : classData?.EventName;
                    if (eventName == null) throw new Exception("INVALID EVENT. Not found!");

                    switch (eventName.ToLower()) {
                        case "onclientconnectevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<OnClientConnectEvent>();
                            OnClientConnected(classData);
                            break;
                        case "onclientdisconnectevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<OnClientDisconnectEvent>();
                            OnClientDisconnect(classData);
                            break;
                        case "onservershutdownevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<OnServerShutdownEvent>();
                            OnServerShutdown(classData);
                            break;
                        case "onserverstartevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<OnServerStartEvent>();
                            OnServerStart(classData);
                            break;
                        case "onmessagesentevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<Network.NetworkMessage>();
                            OnMessageSent(classData);
                            break;
                        case "onmessagereceivedevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<Network.NetworkMessage>();
                            OnMessageReceived(classData);
                            break;
                        case "onhandshakestartevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<OnHandShakeStartEvent>();
                            OnHandShakeStart(classData);
                            break;
                        case "onhandshakeendevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<OnHandShakeEndEvent>();
                            OnHandShakeEnd(classData);
                            break;
                        default:
                            Logger.Log(JsonSerializer.Deserialize<object>(classData));
                            throw new NotImplementedException();
                    }
                } catch (Exception ex) {
                    Logger.Log(ex.Message);
                }
            });
            if (useBlocked) {
                action.Invoke();
            } else {
                new Thread(() => {
                    action.Invoke();
                }).Start();
            }
        }


        public event EventHandler<OnClientConnectEvent>? ClientConnected;
        protected virtual void OnClientConnected(OnClientConnectEvent classData) {
            ClientConnected?.Invoke(this, classData);
        }
        
        public event EventHandler<OnClientDisconnectEvent>? ClientDisconnect;
        protected virtual void OnClientDisconnect(OnClientDisconnectEvent classData) {
            ClientDisconnect?.Invoke(this, classData);
        }

        /// <summary>
        /// Uses blocking. Once events are finished server continues to start
        /// </summary>
        public event EventHandler<OnServerStartEvent>? ServerStart;
        protected virtual void OnServerStart(OnServerStartEvent classData) {
            ServerStart?.Invoke(this, classData);
        }

        public event EventHandler<OnServerShutdownEvent>? ServerShutdown;
        protected virtual void OnServerShutdown(OnServerShutdownEvent classData) {
            ServerShutdown?.Invoke(this, classData);
        }

        public event EventHandler<OnMessageSentEvent>? MessageSent;
        protected virtual void OnMessageSent(OnMessageSentEvent classData) {
            MessageSent?.Invoke(this, classData);
        }

        public event EventHandler<OnMessageReceivedEvent>? MessageReceived;
        protected virtual void OnMessageReceived(OnMessageReceivedEvent classData) {
            MessageReceived?.Invoke(this, classData);
        }

        public event EventHandler<OnHandShakeStartEvent>? HandshakeStart;
        protected virtual void OnHandShakeStart(OnHandShakeStartEvent classData) {
            HandshakeStart?.Invoke(this, classData);
        }

        public event EventHandler<OnHandShakeEndEvent>? HandshakeEnd;
        protected virtual void OnHandShakeEnd(OnHandShakeEndEvent classData) {
            HandshakeEnd?.Invoke(this, classData);
        }
    }
}