using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ClientFramework {
    public class BaseEventClass {
        public string? EventName { get; set; }
        public BaseEventClass() {
            EventName = this.GetType().UnderlyingSystemType.Name;
        }
    }
    public class OnClientConnectEvent : BaseEventClass {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; } = true;
        public OnClientConnectEvent (int id, string username) {
            Id = id;
            UserName = username;
        }
    }
    public class OnClientDisconnectEvent : BaseEventClass {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; }
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


    public class ServerEventMessage : EventArgs {
        public int ClientID { get; set; }
        public int Code { get; set; }
        public DateTime CompletionTime { get; set; }
        public List<object>? Parameters { get; set; }

    }
    public class ServerEvents {
        public static ServerEvents? eventsListener { get; set; }
        public event EventHandler<OnClientConnectEvent>? ClientConnected;
        public event EventHandler<OnClientDisconnectEvent>? ClientDisconnect;
        public event EventHandler<bool>? ServerShutdown;
        public void ExecuteEvent(dynamic classData) {
            string eventName = ((JsonElement)classData).GetProperty("EventName").GetString();
            switch (eventName.ToLower()) {
				case "onclientconnect":
                    OnClientConnected( ((JsonElement)classData).Deserialize<OnClientConnectEvent>() );
					break;
				case "onclientdisconnect":
                    OnClientDisconnect( ((JsonElement)classData).Deserialize<OnClientDisconnectEvent>() );
					break;
				case "onservershutdown":
                    OnServerShutdown( ((JsonElement)classData).GetProperty("Success").GetBoolean() );
					break;
				case "onmessagesent":
					break;
				case "onmessagereceived":
					break;
				case "onhandshakestart":
					break;
				case "onhandshakeend":
					break;
				default:
					throw new NotImplementedException();
			}
        }


        protected virtual void OnClientConnected(OnClientConnectEvent classData) {
            Network.OtherClients.Add(new Network.OtherClient(classData.Id,classData.UserName));
            ClientConnected?.Invoke(this, classData);
        }
        protected virtual void OnClientDisconnect(OnClientDisconnectEvent classData) {
            Network.OtherClients.RemoveAll(x => x.Id == classData.Id);
            ClientDisconnect?.Invoke(this, classData);
        }
        protected virtual void OnServerShutdown(bool success) {
            ServerShutdown?.Invoke(this, success);
        }
    }
}
