using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

using static ClientFramework.Network;

//namespace ClientFramework;

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
        return ($"Hello MSG RESPONSE From Client: {Client.UserName} ({Client.ID})");
    }
    public static int TestInt(NetworkMessage message, dynamic testMessage)
    {
        Console.WriteLine($"MSG:{testMessage} sender:{message.Sender}");
        return 1221;
    }
    public static dynamic TestType(NetworkMessage message, dynamic testMessage)
    {
        Console.WriteLine($"MSG:{testMessage} sender:{message.Sender}");
        TestClass test = new TestClass(true,"TEXT",123);
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

public class TestClass {
    public bool Test { get; set; }
    public string? Text { get; set; }
    public dynamic? Data { get; set; }
    public TestClass() {

    }
    public TestClass(bool test, string? text, dynamic? anything = null) {
        this.Test = true;
        this.Text = text;
        this.Data = anything;
    }
}


class Foo
{
    public static void Bar()
    {
        Console.WriteLine("Bar");
    }
}