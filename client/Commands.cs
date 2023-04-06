﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ClientFramework {
    public class Commands {
        public static void Help() {
            Console.WriteLine("Commands: ");
            Console.WriteLine();
            Console.WriteLine("Clear            | Clears console");
            Console.WriteLine("ToggleDebug      | Sets Debug ON or OFF for console");
            Console.WriteLine("Users            | Users connected to server");
            Console.WriteLine("Connect          | Connect to server");
            Console.WriteLine("Disconnect       | Disconnect from server");
            Console.WriteLine("Status           | Check if connected to server");
            Console.WriteLine("Ping             | Ping server to check response time");
            Console.WriteLine("SendData         | Sends a command to another client/server");
            Console.WriteLine("RequestData      | Gets a value from another client/server");
            Console.WriteLine("ClientMethods    | Methods available on client");
            Console.WriteLine("ServerMethods    | Methods available on server");
            Console.WriteLine("Exit             | Closes application");
        }
        public static void UserList() {
            if (Network.ClientList.Count() == 0) {
                Console.WriteLine("No other clients connected");
                return;
            }
            Console.WriteLine("Connected clients: ");

            int i = 1;
            foreach (Network.OtherClient _client in Network.ClientList) {
                Console.WriteLine($"    ({i}) ID={_client.ID} Name={_client.UserName}");
                i++;
            }
        }
        public static void SendData() {
            if (!Network.Client.HandshakeDone)
                throw new Exception("Connect to server first!");

            Console.WriteLine();
            Console.WriteLine("method to be sent to client/server: ");
            string? method = Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("DATA string to be sent to client/server: ");
            string? data = Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string? target;
            while (true) {
                target = Console.ReadLine();
                if (!string.IsNullOrEmpty(target)) break;
                Console.WriteLine("Invalid target, try again!");
            }

            Network.NetworkMessage message = new Network.NetworkMessage {
                Parameters = data == null ? null : new object[] {data},
                MethodName = method,
                TargetId = Int32.Parse(target)
            };

            DateTime then = DateTime.Now;
            for (int i = 0; i < 1000; i++) {
                Network.SendData(message,true);
            }
            Console.WriteLine(DateTime.Now - then);
        }
        public static void GetClientMethods() {
            Console.WriteLine();
            foreach (var item in Network.ClientMethods) Console.WriteLine($"{item.Name} ReturnType:({item.ReturnType})  ParamCount:({item.GetParameters().Count()})");
        }
        public static void GetServerMethods() {
            if (Network.ServerMethods == null) throw new Exception("Server Methods not Initialized yet! (Gets populated when when connected to server)");
            Console.WriteLine();
            foreach (var item in Network.ServerMethods) Console.WriteLine($"{item.Name} ReturnType:({item.ReturnType})  ParamCount:({(item.Parameters)?.Count()})");
        }
        public static void RequestData() {
            if (!Network.IsConnected())
                throw new Exception("Connect to server first!");

            Console.WriteLine();
            Console.WriteLine("Enter Method Name:");
            string? method = Console.ReadLine();

            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string? target;
            while (true) {
                target = Console.ReadLine();
                if (!string.IsNullOrEmpty(target)) break;
                Console.WriteLine("Invalid target, try again!");
            }

            Network.NetworkMessage message = new Network.NetworkMessage {
                Parameters = "123",
                MethodName = method,
                TargetId = Int32.Parse(target)
            };

            dynamic a = Network.RequestData(message);
            
            if (a is Array)
                foreach (var b in a) {
                    if (b is Array) {
                        foreach (var c in b) Console.WriteLine($"{c} ({c.GetType()})");
                        continue;
                    }
                    Console.WriteLine($"{b} ({b.GetType()})");
                }
            else 
                Console.WriteLine($"{a} ({a.GetType()})");
        }
        public static void Ping() {
            var span = Network.CheckPing();
            Console.WriteLine($"Ping is: {span.Milliseconds}.{span.Microseconds}ms");
        }
        public static void RequestDataType() {
            if (!Network.IsConnected())
                throw new Exception("Connect to server first!");

            Console.WriteLine();
            Console.WriteLine("Target ID: (Blank or 0 for all clients)");
            string? target;
            while (true) {
                target = Console.ReadLine();
                if (!string.IsNullOrEmpty(target)) break;
                Console.WriteLine("Invalid target, try again!");
            }

            Network.NetworkMessage message = new Network.NetworkMessage {
                Parameters = "Hello REQUEST From Client",
                MethodName = "getclassdata",
                TargetId = Int32.Parse(target)
            };
            TestClass? a = Network.RequestData<TestClass>(message);
            Console.WriteLine($"RETURNED:{a.Text}");
            Console.WriteLine($"RETURNED:{a.Data}");
        }
    }
}
