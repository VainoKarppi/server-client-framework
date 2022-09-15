using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EdenOnlineExtensionServer {
    public class Commands {
        public static void Help() {
            Log.Write("Commands: ");
            Log.Write();
            Log.Write("Clear        | Clears console");
            Log.Write("Users        | Users connected to server");
            Log.Write("Start        | Start server");
            Log.Write("Stop         | Stop server");
            Log.Write("Status       | Check if server is running");
            Log.Write("SendData     | Sends a command to user(s)");
            Log.Write("Exit         | Closes server");
        }
        public static void UserList() {
            if (Network.ClientList.Count() == 0)
                throw new Exception("No users connected!");

            Log.Write("Connected clients count: " + Network.ClientList.Count());
            foreach (KeyValuePair<short,TcpClient> client in Network.ClientList) {
                string remoteIP = ((IPEndPoint)client.Value.Client.RemoteEndPoint).Address.ToString();
                Log.Write("    User: " + client.Key + " - (" + remoteIP + ")");
            }
        }
        public static void SendCommand() {
            if (!Network.ServerRunning)
                throw new Exception("Start the server first!");

            if (Network.ClientList.Count() == 0)
                throw new Exception("No clients online!");

            Log.Write();
            Log.Write("Function to be sent to client: ");
            string function = Console.ReadLine();
            Log.Write();
            Log.Write("Target ID: (Blank or 0 for all clients)");
            string target = Console.ReadLine();
            if (string.IsNullOrEmpty(target))
                target = "0";
 
            Network.SendData(function, null, short.Parse(target));
        }

    }
}
