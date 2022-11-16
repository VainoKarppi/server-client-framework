using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using static ServerFramework.Network;

//namespace ServerFramework;
public class TestClass {
    public bool Test { get; set; }
    public string? StringTest { get; set; }
    public dynamic? Data { get; set; }
}
public class ServerMethods {
    public static string Test(NetworkClient client, dynamic testMessage)
    {
        Console.WriteLine(
            $"MSG:{testMessage} ({testMessage.GetType()}) " +
            $"CLIENT: {client.Client.RemoteEndPoint} ID:{client.Id}"
        );
        return "Hello MSG RESPONSE From SERVER!";
    }
    public static int TestInt(NetworkClient client, dynamic testMessage)
    {
        Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.Id}");
        return 123;
    }
    public static void TestVoid(NetworkClient client, dynamic testMessage)
    {
        Console.WriteLine($"This is a VOID method: {testMessage}");
    }
    public static dynamic TestType(NetworkClient client, dynamic testMessage)
    {
        Console.WriteLine($"MSG:{testMessage} CLIENT: {client.Client.RemoteEndPoint} ID:{client.Id}");
        TestClass test = new TestClass();
        test.StringTest = "TESTI";
        test.Test = true;
        test.Data = new string[] { "asd" };
        return test;
    }

    public static object[] TestArray(NetworkClient client, dynamic parameters)
    {
        return new object[] { "test", true, 1213 };
    }



    public static object[] ConnectedClients(NetworkClient client, dynamic parameters)
    {
        List<object[]> list = new List<object[]>();
        foreach (NetworkClient toAdd in ClientList)
        {
            if (!toAdd.Connected) continue;
            list.Add(new object[] { toAdd.Id, toAdd.UserName });
        }
        return list.ToArray();
    }
}