using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using static ServerFramework.Network;


//namespace ServerFramework;
public class ServerMethods
{
    public static string TestServer(string testMessage) {
        Console.WriteLine($"MSG:{testMessage}");
        return "Hello MSG RESPONSE From SERVER!";
    }
    // BOTH NetworkClient AND NetworkMessage are OPTIONAL!
    // NetworkClient is available only on server!
    public static int TestIntServer(NetworkClient client, NetworkMessage message, string testMessage) {
        Console.WriteLine($"MSG:{testMessage} ID:{client.ID} MSG:{message.Hash}");
        return 165;
    }
    // Get data using class
    public static TestClass GetClassData(NetworkClient client, string testMessage) {
        return new TestClass(true, "testClass", 12.5);
    }
}

// Has to be exactly the same as on CLIENT!
public class TestClass
{
    public bool Test { get; set; }
    public string? Text { get; set; }
    public dynamic? Data { get; set; }
    public TestClass()
    {

    }
    public TestClass(bool test, string? text, dynamic? anything = null)
    {
        this.Test = true;
        this.Text = text;
        this.Data = anything;
    }
}