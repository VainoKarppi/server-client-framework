using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ClientFramework {
    public class ServerEventMessage : EventArgs {
        public int ClientID { get; set; }
        public int Code { get; set; }
        public DateTime CompletionTime { get; set; }
        public List<object> Parameters { get; set; }

    }
    public class ServerEvents
    {
        public static ServerEvents? eventsListener { get; set; }
        public event EventHandler<object[]> ClientConnected;
        public event EventHandler<object[]> ClientDisconnect; // event
        public void ExecuteEvent(string eventName, object[] parameters)
        {
            var data = new ServerEventMessage();
            switch (eventName.ToLower()) {
				case "onclientconnect":
                    OnClientConnected(parameters);
					break;
				case "onclientdisconnect":
                    OnClientDisconnect(parameters);
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


        protected virtual void OnClientConnected(object[] parameters){
            ClientConnected?.Invoke(this, parameters);
        }
        protected virtual void OnClientDisconnect(object[] parameters){
            ClientDisconnect?.Invoke(this, parameters);
        }
    }
}
