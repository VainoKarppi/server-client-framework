﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

using static ClientFramework.Network;

namespace ClientFramework;
public class TestClass
{
    public bool Test { get; set; }
    public string? StringTest { get; set; }
    public dynamic? Data { get; set; }
}
public class ClientMethods
{
    public static string Test(NetworkMessage message, dynamic testMessage)
    {
        if (testMessage is Array)
        {
            foreach (var item in testMessage) Console.WriteLine($"MSG:{item} ({item.GetType()})");
        }
        else
        {
            Console.WriteLine($"MSG:{testMessage} ({testMessage.GetType()})");
        }
        return ($"Hello MSG RESPONSE From Client: {Network.Client.UserName} ({Network.Client.Id})");
    }
    public static int TestInt(NetworkMessage message, dynamic testMessage)
    {
        Console.WriteLine($"MSG:{testMessage} sender:{message.Sender}");
        return 1221;
    }
    public static dynamic TestType(NetworkMessage message, dynamic testMessage)
    {
        Console.WriteLine($"MSG:{testMessage} sender:{message.Sender}");
        TestClass test = new TestClass();
        test.StringTest = "TESTI";
        test.Test = true;
        test.Data = new string[] { "asd" };
        return test;
    }
    public static object[] TestArray(NetworkMessage message, dynamic testMessage)
    {
        return new object[] { "test", true, 1213 };
    }
    public static void Disconnect(NetworkMessage message)
    {
        throw new NotImplementedException();
    }
}