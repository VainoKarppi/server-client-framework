using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerFramework {
    public class Commands {
        public static void Help() {
            Console.WriteLine("Commands: ");
            Console.WriteLine();
            Console.WriteLine("Clear        | Clears console");
            Console.WriteLine("Users        | Users connected to server");
            Console.WriteLine("Start        | Start server");
            Console.WriteLine("Stop         | Stop server");
            Console.WriteLine("Status       | Check if server is running");
            Console.WriteLine("SendData     | Sends a command to user(s)");
            Console.WriteLine("RequestData  | Requests data from user");
            Console.WriteLine("Exit         | Closes server");
        }
        public static void UserList() {
            if (Network.ClientList.Count() == 0)
                throw new Exception("No users connected!");

            Console.WriteLine("Connected clients count: " + Network.ClientList.Count());
            foreach (NetworkClient client in Network.ClientList) {
                string remoteIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                Console.WriteLine("    User: " + client.UserName + " - (" + remoteIP + ") : ID=" + client.ID.ToString());
            }
        }
        public static void SendData() {
            if (!Network.ServerRunning)
                throw new Exception("Start the server first!");

            if (Network.ClientList.Count() == 0)
                throw new Exception("No clients online!");

            Console.WriteLine();
            Console.WriteLine("method to be sent to client: ");
            string method = Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string target = Console.ReadLine();
            if (string.IsNullOrEmpty(target))
                target = "0";

            Network.NetworkMessage message = new Network.NetworkMessage {
                Parameters = "TEST MSG FROM SERVER",
                TargetId = Int32.Parse(target),
                MethodName = method
            };
            Network.SendData(message);
        }
        public static void GetClientMethods() {
            Console.WriteLine();
            foreach (var item in Network.ClientMethods) Console.WriteLine(item);
        }
        public static void GetServerMethods() {
            Console.WriteLine();
            foreach (var item in Network.ServerMethods) Console.WriteLine(item);
        }
        public static void RequestData() {
            if (!Network.ServerRunning)
                throw new Exception("Start the server first!");

            if (Network.ClientList.Count() == 0)
                throw new Exception("No clients online!");

            Console.WriteLine();
            Console.WriteLine("method to be sent to client: ");
            string method = Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string target = Console.ReadLine();
            if (string.IsNullOrEmpty(target))
                target = "0";

            Network.NetworkMessage message = new Network.NetworkMessage {
                Parameters = new object[] {"TEST MSG FROM SERVER"},
                TargetId = Int32.Parse(target),
                MethodName = method
            };
            dynamic data = Network.RequestData(message);
            Console.WriteLine(data.GetType());
            Console.WriteLine(data);
        }
        public static void RequestDataType() {
            if (!Network.ServerRunning)
                throw new Exception("Start the server first!");

            if (Network.ClientList.Count() == 0)
                throw new Exception("No clients online!");

            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string target = Console.ReadLine();
            if (string.IsNullOrEmpty(target))
                target = "0";

            Network.NetworkMessage message = new Network.NetworkMessage {
                Parameters = Network.SerializeParameters("Hello From Client"),
                MethodName = "TestType",
                TargetId = Int32.Parse(target)
            };
            TestClass a = Network.RequestData<TestClass>(message);
            Console.WriteLine($"RETURNED:{a.StringTest}");
            Console.WriteLine($"RETURNED:{a.Data[0]}");
        }
    }
}
