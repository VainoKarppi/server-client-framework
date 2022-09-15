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
            Console.WriteLine("Exit         | Closes server");
        }
        public static void UserList() {
            if (Network.ClientList.Count() == 0)
                throw new Exception("No users connected!");

            Console.WriteLine("Connected clients count: " + Network.ClientList.Count());
            foreach (KeyValuePair<short,TcpClient> client in Network.ClientList) {
                string remoteIP = ((IPEndPoint)client.Value.Client.RemoteEndPoint).Address.ToString();
                Console.WriteLine("    User: " + client.Key + " - (" + remoteIP + ")");
            }
        }
        public static void SendCommand() {
            if (!Network.ServerRunning)
                throw new Exception("Start the server first!");

            if (Network.ClientList.Count() == 0)
                throw new Exception("No clients online!");

            Console.WriteLine();
            Console.WriteLine("Function to be sent to client: ");
            string function = Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string target = Console.ReadLine();
            if (string.IsNullOrEmpty(target))
                target = "0";
 
            Network.SendData(function, null, short.Parse(target));
        }

    }
}
