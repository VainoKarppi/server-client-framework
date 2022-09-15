using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace ClientFramework
{
    public class ClientFunctions
    {
        public static string Test(object[] parameters)
        {
            object[] data = Network.RequestData("Test",parameters);
            return (string)data[0];
        }
        public static string DelayTest(object[] parameters)
        {
            object[] data = Network.RequestData("Test",parameters);
            Thread.Sleep(300);
            return (string)data[0];
        }

        public static object[] TestArray(object[] parameters) {
            object[] data = Network.RequestData("TestArray",parameters);
            Console.WriteLine("<<<FINAL>>>");
            foreach (var x in data) Console.WriteLine($"{x.GetType()} | {x}");
            return data;
        }

        
        public static int Connect(object[] parameters) {

            string ip = (string)parameters[0];
            int port = (int)parameters[1];


            Network.Connect(ip, port);
            if (Network.ClientID == -1)
                throw new Exception("Invalid Client ID");

            Console.WriteLine("*SUCCESS* Connected to server with ClientID: " + Network.ClientID.ToString());
            return Network.ClientID;
        }

        public static void Disconnect(object[] parameters) {
            try {
                Console.WriteLine("Disconnecting from the server...");
                Network.Disconnect();
                Console.WriteLine("Disconnected succesfully!");
            } catch (Exception ex) {
                throw new Exception(ex.Message);
            }
        }

        public static string SendCommand() {
            return "";
        }
    }
}
