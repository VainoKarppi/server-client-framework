using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;


/*
code = REASON

OnClientConnected(int ID, string name)
OnClientDisconnected(int, ID, string Name, int code)

OnMessageReceived()
OnMessageSent()

OnServerShutdown()

OnConnected()
OnDisconnected()

OnHandshakeStart()
OnHandshakeEnd()




*/
namespace ClientFramework {
    public class Events {
        public class ServerEventMessage : EventArgs {
            public int ClientID { get; set; }
            public int Code { get; set; }
            public bool IsSuccessful { get; set; }
            public DateTime CompletionTime { get; set; }

        }
        public class ServerEvents {
            public event EventHandler<ServerEventMessage> ClientConnected; // event

            public void StartProcess() {
                var data = new ServerEventMessage();

                    Console.WriteLine("Process Started!");
                    
                    //uncomment following to see the result
                    //throw new NullReferenceException();
                    
                    // some process code here..
                    
                    data.ClientID = 2;
                    data.Code = 5;
                    data.IsSuccessful = true;
                    data.CompletionTime = DateTime.Now;
                    OnClientConnected(data);
            }


            protected virtual void OnClientConnected(ServerEventMessage e)
            {
                ClientConnected?.Invoke(this, e);
            }
        }
    }
    
}
