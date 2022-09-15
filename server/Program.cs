using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace EdenOnlineExtensionServer {
    public class Log {
        public static void Write(object text = null) {
            string time = DateTime.Now.ToString("ss:FF");
            if (time.Length < 11) {
                string last = time.Substring(time.Length - 1,1);
                time = time.Remove(time.Length - 1);
                time = time + ("0" + last);
            }
            Console.WriteLine($"{time} | {text}");
        }
    }
    public class Program {
        public const int Version = 1000;

        static void Main(string[] args) {
            Log.Write();
            Console.Clear();
            Console.Title = "EDEN Online Extension Server";
            Log.Write("Type 'help' for commands!");
            //Network.StartServer();
            while (true) {
                Log.Write();
                Console.Write(">> ");
                string command = Console.ReadLine();
                command = command.ToLower();


                try {
                    if (command == "help")
                        Commands.Help();

                    else if (command == "clear")
                        Console.Clear();

                    else if (command == "exit")
                        break;

                    else if (command == "start")
                        Network.StartServer();

                    else if (command == "stop")
                        Network.StopServer();

                    else if (command == "users")
                        Commands.UserList();

                    else if (command == "senddata")
                        Commands.SendCommand();

                    else if (command == "status")
                        Log.Write(Network.ServerRunning ? "Server is running!" : "Server is not running!");
                    else
                        Log.Write("Unknown command!" + "\n" + "Type 'help' for commands!");

                } catch (Exception e) {
                    Log.Write(e.Message);
                }
            }
        }


    }
}