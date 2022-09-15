using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;


public class Request {
    public static string Serialize(object[] parameters) {
        DataArray arr = new DataArray();

        foreach (object x in parameters) {
            arr.Add(x);
        }

        string returnaaa = DataArray.Serialize(arr);
        return returnaaa;
    }
    public static object[] Deserialize(string message) {
        DataArray arr = DataArray.Unserialize(message);

        return arr.ToArray();
    }

    public enum MessageTypes : byte {
        SendData,
        RequestData,
        ResponseData
    }
    public static List<Type> DataTypes = new List<Type>() {
        typeof(System.Boolean),
        typeof(System.Byte),
        typeof(System.Char),
        typeof(System.Int16),
        typeof(System.Int32),
        typeof(System.Int64),
        typeof(System.Single),
        typeof(System.Double),
        typeof(System.String),
        typeof(System.Array),
        typeof(System.Object),
        typeof(System.Object[])
    };
    public static List<string> Functions = new List<string>() {"Handshake","Disconnect","ConnectedClients","Test","TestArray"};
}
