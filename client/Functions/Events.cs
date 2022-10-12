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
        public class ServerEventMessage : EventArgs
        {
            public int ClientID { get; set; }
            public int Code { get; set; }
            public bool IsSuccessful { get; set; }
            public DateTime CompletionTime { get; set; }

        }
    }
    
}
