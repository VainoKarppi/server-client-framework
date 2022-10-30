using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ClientFramework {
    public class ClientConnectEvent  {
        public int Id { get; set; }
        public string? UserName { get; set; }
    }
    public class ClientDisconnectEvent : ClientConnectEvent {
        public bool Success { get; set; }
    }
    public class ServerEventMessage : EventArgs {
        public int ClientID { get; set; }
        public int Code { get; set; }
        public DateTime CompletionTime { get; set; }
        public List<object>? Parameters { get; set; }

    }
    public class ServerEvents
    {
        public static ServerEvents? eventsListener { get; set; }
        public event EventHandler<ClientConnectEvent>? ClientConnected;
        public event EventHandler<ClientDisconnectEvent>? ClientDisconnect;
        public void ExecuteEvent(string eventName, dynamic classData)
        {
            switch (eventName.ToLower()) {
				case "onclientconnect":
                    OnClientConnected( ((JsonElement)classData).Deserialize<ClientConnectEvent>() );
					break;
				case "onclientdisconnect":
                    OnClientDisconnect( ((JsonElement)classData).Deserialize<ClientDisconnectEvent>() );
					break;
				case "onservershutdown":
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


        protected virtual void OnClientConnected(dynamic classData){
            ClientConnected?.Invoke(this, classData);
        }
        protected virtual void OnClientDisconnect(dynamic classData){
            ClientDisconnect?.Invoke(this, classData);
        }
    }
}
