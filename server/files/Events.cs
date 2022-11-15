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
        public int? Id { get; set; }
        public string? UserName { get; set; }
        public bool? Success { get; set; } = true;
        public OnClientConnectEvent (int id, string username, bool success = false) {
            Id = id;
            UserName = username;
            Success = success;
        }
    }
    public class OnClientDisconnectEvent : BaseEventClass {
        public int? Id { get; set; }
        public string? UserName { get; set; }
        public bool? Success { get; set; }
        public OnClientDisconnectEvent (int id, string username, bool success = false) {
            Id = id;
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



    public class NetworkEvents {
        public static NetworkEvents? eventsListener { get; set; }
        public async void ExecuteEvent(dynamic classData, bool useBlocked = false) {
            Thread eventThread = new Thread(() => {
                try {
                    string eventName;
                    if (classData is JsonElement) {
                        eventName = ((JsonElement)classData).GetProperty("EventName").GetString();
                    } else {
                        eventName = classData.EventName;
                    }

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
                        case "onmessagesentevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<Network.NetworkMessage>();
                            OnMessageSent(classData);
                            break;
                        case "onmessagereceivedevent":
                            if (classData is JsonElement) classData = ((JsonElement)classData).Deserialize<Network.NetworkMessage>();
                            OnMessageReceived(classData);
                            break;
                        default:
                            Console.WriteLine(JsonSerializer.Deserialize<object>(classData));
                            throw new NotImplementedException();
                    }
                } catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                }
            });
            if (!useBlocked) eventThread.Start();
            else await Task.Factory.StartNew(() => {eventThread.Start();});
        }


        public event EventHandler<OnClientConnectEvent>? ClientConnected;
        protected virtual void OnClientConnected(OnClientConnectEvent classData) {
            ClientConnected?.Invoke(this, classData);
        }
        
        public event EventHandler<OnClientDisconnectEvent>? ClientDisconnect;
        protected virtual void OnClientDisconnect(OnClientDisconnectEvent classData) {
            ClientDisconnect?.Invoke(this, classData);
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
    }
}