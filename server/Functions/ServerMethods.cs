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
        public static void Disconnect(NetworkClient client, string testMessage) {
            Console.WriteLine("CLIENT DISCONNECTED (TEST)");
        }

        public static object[] TestArray(NetworkClient client, object[] parameters) {
            foreach (var x in parameters) Console.WriteLine(x);
            return new object[] {"TestArray",123};
        }
        


        public static object[] ConnectedClients(NetworkClient client) {
            List<object[]> list = new List<object[]>();
            foreach (NetworkClient toAdd in Network.ClientList) {
                if (!toAdd.Connected) continue;
                list.Add(new object[] {toAdd.ID,toAdd.UserName});
            }
            return list.ToArray();
        }
    }
}
