using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ClientFramework {
    class ClientMethods {
        public static string Test(TcpClient server, string testMessage) {

            Console.WriteLine($"RECEIVED:{testMessage} IP:{server.Client.RemoteEndPoint}");
            return "TestFromClient";
        }
        public static void Disconnect(TcpClient server) {
            throw new NotImplementedException();
        }

        public static object[] TestArray(TcpClient server) {
            return new object[] {"asd",121};
        }
    }
}
