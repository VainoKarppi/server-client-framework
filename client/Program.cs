﻿
using System.Net.Mime;
using System.Reflection;





namespace ClientFramework {
    public class Program {
        public const int Version = 1000;

        static void Main(string[] args) {
            Console.WriteLine();
            Console.Clear();
            Console.Title = "EDEN Online Extension CLIENT";
            Console.WriteLine("Type 'help' for commands!");

            object[] asd = new object[] {"test"};
            Type LastType = Type.GetType(asd.ToString());

            Console.WriteLine(LastType);

            while (true) {
                Console.WriteLine();
                string? command = Console.ReadLine();
                command = command.ToLower();


                try {
                    switch (command)
                    {
                        case "help":
                            Commands.Help();
                            break;
                        case "clear":
                            Console.Clear();
                            break;
                        case "exit":
                            Environment.Exit(0);
                            break;
                        case "connect":
                        /*
                            Console.WriteLine("Ip Adress");
                            string ip = Console.ReadLine();
                            Console.WriteLine("Username");
                            string userName = Console.ReadLine();
                            Console.WriteLine("Port");
                            int port = Int32.Parse (Console.ReadLine());*/
                            Network.Connect("127.0.0.1",2302,"vaino");
                            break;
                        case "disconnect":
                            Network.Disconnect();
                            break;
                        case "users":
                            Commands.UserList();
                            break;
                        case "senddata":
                            Commands.SendData();
                            break;
                        case "requestdata":
                            Commands.RequestData();
                            break;
                        case "status":
                            Console.WriteLine(Network.IsConnected ? "Connected to server! ID:" + Network.ClientID.ToString() : "NOT connected to server!");
                            break;
                        default:
                            Console.WriteLine("Unknown command!" + "\n" + "Type 'help' for commands!");
                            break;
                    }  
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}