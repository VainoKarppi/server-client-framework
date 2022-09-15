using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ClientFrameworkServer {
    class ClientFunctions {
        public static string Test(TcpClient server, object[] parameters, byte RequestType) {
            Console.WriteLine($"RequestType:{RequestType} IP:{server.Client.RemoteEndPoint}");
            Console.WriteLine($"PARAMS COUNT:{parameters.Count()}");
            foreach (var x in parameters) {
                Console.WriteLine("\t" + $">> {x}");
            }
            return "testAAAA";
        }
        public static void Disconnect(TcpClient server, object[] parameters, byte RequestType) {
            throw new NotImplementedException();
        }

        public static object[] TestArray(TcpClient server, object[] parameters, byte RequestType) {
            return new object[] {"asd",121};
        }
    }
}
