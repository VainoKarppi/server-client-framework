using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ClientFramework {
    public class OnClientConnect {
        public string? EventName { get; set; }
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; } = true;
    }
    public class OnClientDisconnect {
        public string? EventName { get; set; }
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; }
    }
    public class ServerEventMessage : EventArgs {
        public int ClientID { get; set; }
        public int Code { get; set; }
        public DateTime CompletionTime { get; set; }
        public List<object>? Parameters { get; set; }

    }
    public class ServerEvents {
        public static ServerEvents? eventsListener { get; set; }
        public event EventHandler<OnClientConnect>? ClientConnected;
        public event EventHandler<OnClientDisconnect>? ClientDisconnect;
        public void ExecuteEvent(dynamic classData) {
            string eventName = ((JsonElement)classData).GetProperty("EventName").GetString();
            switch (eventName.ToLower()) {
				case "onclientconnect":
                    OnClientConnected( ((JsonElement)classData).Deserialize<OnClientConnect>() );
					break;
				case "onclientdisconnect":
                    OnClientDisconnect( ((JsonElement)classData).Deserialize<OnClientDisconnect>() );
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


        protected virtual void OnClientConnected(OnClientConnect classData) {
            Network.OtherClients.Add(new Network.OtherClient(classData.Id,classData.UserName));
            ClientConnected?.Invoke(this, classData);
        }
        protected virtual void OnClientDisconnect(OnClientDisconnect classData) {
            Network.OtherClients.RemoveAll(x => x.Id == classData.Id);
            ClientDisconnect?.Invoke(this, classData);
        }
    }
}
