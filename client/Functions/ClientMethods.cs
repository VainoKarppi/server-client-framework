using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ClientFramework {
    class ClientMethods {
        public static string Test(TcpClient server, string testMessage) {
            Console.WriteLine($"RECEIVED:{testMessage} IP:{server.Client.RemoteEndPoint}");
            return ($"Hello MSG RESPONSE From Client: {Network.Client.UserName} ({Network.Client.Id})");
        }
        public static int TestInt(TcpClient server, string testMessage) {
            Console.WriteLine($"RECEIVED:{testMessage} IP:{server.Client.RemoteEndPoint}");
            return 1221;
        }
        public static dynamic TestType(TcpClient server, string testMessage) {

            Console.WriteLine($"RECEIVED:{testMessage} IP:{server.Client.RemoteEndPoint}");
            TestClass test = new TestClass();
            test.StringTest = "TEST";
            test.Test = true;
            return test;
        }
        public static object[] TestArray(TcpClient server, string testMessage) {
            return new object[] {"test",true,1213};
        }
        public static void Disconnect(TcpClient server) {
            throw new NotImplementedException();
        }

        public static object[] TestArray(TcpClient server) {
            return new object[] {"asd",121};
        }
    }
}
