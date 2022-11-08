using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ClientFramework {
    public class TestClass {
        public bool Test { get; set; }
        public string? StringTest { get; set; }
        public dynamic? Data { get; set; }
    }
    class ClientMethods {
        public static string Test(TcpClient server, dynamic testMessage) {
            if (testMessage is Array) {
                foreach (var item in testMessage) Console.WriteLine($"MSG:{item} ({item.GetType()})");
            } else {
                Console.WriteLine($"MSG:{testMessage} ({testMessage.GetType()})");
            }
            return ($"Hello MSG RESPONSE From Client: {Network.Client.UserName} ({Network.Client.Id})");
        }
        public static int TestInt(TcpClient server, dynamic testMessage) {
            Console.WriteLine($"MSG:{testMessage} IP:{server.Client.RemoteEndPoint}");
            return 1221;
        }
        public static dynamic TestType(TcpClient server, dynamic testMessage) {
            Console.WriteLine($"MSG:{testMessage} IP:{server.Client.RemoteEndPoint}");
            TestClass test = new TestClass();
            test.StringTest = "TESTI";
            test.Test = true;
            test.Data = new string[] {"asd"};
            return test;
        }
        public static object[] TestArray(TcpClient server, dynamic testMessage) {
            return new object[] {"test",true,1213};
        }
        public static void Disconnect(TcpClient server) {
            throw new NotImplementedException();
        }
    }
}
