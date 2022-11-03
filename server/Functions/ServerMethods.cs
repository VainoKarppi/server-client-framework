using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace ServerFramework {
    public class TestClass {
        public bool Test { get; set; }
        public string? StringTest { get; set; }
        public dynamic? Data { get; set; }
    }
    class ServerMethods {
        public static string Test(NetworkClient client, dynamic testMessage) {
            Console.WriteLine($"MSG:{testMessage} ({testMessage.GetType()}) CLIENT: {client.Client.RemoteEndPoint} ID:{client.ID}");
            return "Hello MSG RESPONSE From SERVER!";
        }
        public static int TestInt(NetworkClient client, dynamic testMessage) {
            Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.ID}");
            return 123;
        }
        public static dynamic TestType(NetworkClient client, dynamic testMessage) {
            Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.ID}");
            TestClass test = new TestClass();
            test.StringTest = "TESTI";
            test.Test = true;
            test.Data = new string[] {"asd"};
            return test;
        }

        public static object[] TestArray(NetworkClient client, dynamic parameters) {
            return new object[] {"test",true,1213};
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
