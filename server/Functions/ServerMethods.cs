using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ServerFramework {
    class ServerMethods {
        public static string Test(NetworkClient client, string testMessage) {
            Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.ID}");
            return "moi sinulle :)";
        }
        public static void Disconnect(NetworkClient client) {
            throw new NotImplementedException();
        }

        public static object[] TestArray(NetworkClient client, object[] parameters) {
            foreach (var x in parameters) Console.WriteLine(x);
            return new object[] {"TestArray",123};
        }
        


        public static object[] ConnectedClients(NetworkClient client) {

            
            // TODO send as list or array (ADD IN FRAMEWORK)
            return Network.ClientList.ToArray();
        }
    }
}
