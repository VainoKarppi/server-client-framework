using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientFramework {
    public class Commands {
        public static void Help() {
            Console.WriteLine("Commands: ");
            Console.WriteLine();
            Console.WriteLine("Clear        | Clears console");
            Console.WriteLine("Users        | Users connected to server");
            Console.WriteLine("Connect      | Connect to server");
            Console.WriteLine("Disconnect   | Disconnect from server");
            Console.WriteLine("Status       | Check if connected to server");
            Console.WriteLine("SendData     | Sends a command to another client/server");
            Console.WriteLine("Exit         | Closes application");
        }
        public static void UserList() {


            Console.WriteLine("Connected clients: ");
            throw new NotImplementedException();
        }
        public static void SendCommand() {
            if (!Network.IsConnected)
                throw new Exception("Connect to server first!");

            Console.WriteLine();
            Console.WriteLine("method to be sent to client/server: ");
            string method = Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string target = Console.ReadLine();
            if (string.IsNullOrEmpty(target))
                target = "0";

            string output;
            //Program.TestData(method,new string []{},out output);

            
            //Network.SendData(method, null, short.Parse(target));
        }

    }
}
