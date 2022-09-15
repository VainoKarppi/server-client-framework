using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ServerFramework {
    class ClientFunctions {
        public static string Test(NetworkClient client, object[] parameters, byte RequestType) {
            return "te22st from server!";
        }
        public static void Disconnect(NetworkClient client, object[] parameters, byte RequestType) {
            throw new NotImplementedException();
        }

        public static object[] TestArray(NetworkClient client, object[] parameters, byte RequestType) {
            foreach (var x in parameters) Console.WriteLine(x);
            return new object[] {"TestArray",123};
        }
        


        public static Dictionary<short,TcpClient> ConnectedClients(NetworkClient client, object[] parameters, byte RequestType) {
            // TODO send as list or array (ADD IN FRAMEWORK)
            return Network.ClientList;
        }
    }
}
