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
        public static string Test(NetworkClient client, string testMessage) {
            Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.ID}");
            return "Hello MSG RESPONSE From SERVER!";
        }
        public static dynamic TestType(NetworkClient client, string testMessage) {
            Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.ID}");
            TestClass test = new TestClass();
            test.StringTest = "TESTI";
            test.Test = true;
            test.Data = new string[] {"asd"};
            return test;
        }
        public static void Disconnect(NetworkClient client, string testMessage) {
            Console.WriteLine("CLIENT DISCONNECTED (TEST)");
        }

        public static object[] TestArray(NetworkClient client, object[] parameters) {
            foreach (var x in parameters) Console.WriteLine(x);
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
