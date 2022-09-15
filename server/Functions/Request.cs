using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;


public class Request {
    public static string Serialize(object[] parameters) {
        ArmaArray arr = new ArmaArray();

        foreach (object x in parameters) {
            arr.Add(x);
        }

        return ArmaArray.Serialize(arr);
    }
    public static object[] Deserialize(string message) {
        ArmaArray arr = ArmaArray.Unserialize(message);

        return arr.ToArray();
    }
    /*
    public static string Serialize(object[] parameters) {
        StringBuilder message = new StringBuilder(parameters.Count());
        foreach (var param in parameters) {
            int index = Request.DataTypes.IndexOf(param.GetType());
            if (index == -1)
                throw new Exception($"ERROR: INVALID DATA TYPE FOR PARAM: {param} ({param.GetType()})");

            string indexString = (index < 10) ? ("0" + index.ToString()) : index.ToString();


            message.Append(String.Format("{0}{1}",(char)0x01,indexString));
            if (index == 11) {
                ArmaArray newArr = new ArmaArray();
                foreach (object x in new object[]{param}) {
                    Log.Write(param);
                    newArr.Add(x);
                } 
                string toArma = ArmaArray.Serialize(newArr);
                message.Append(toArma);
                continue;
            }
            message.Append(param);
        }
        Log.Write(message);
        return message.ToString();
    }
    public static object[] Deserialize(string message) {

        if (String.IsNullOrEmpty(message)) return new object[] {};

        List<string> splitted = message.Split((char)0x01).ToList();
        if (splitted.Count() > 1) splitted.RemoveAt(0);


        object[] returnArray = new object[(splitted.Count())];
        int index = 0;

        foreach (string data in splitted) {  
            if (String.IsNullOrEmpty(data)) continue;

            int dataIndex = Int32.Parse(data.Substring(0,2));
            Type dataType = DataTypes[dataIndex];
            
            string dataNew = data.Remove(0,2);
            
            Log.Write("\t<REQUEST DATA>: " + dataType.ToString() + " | " + dataNew.ToString());
            
            dynamic value;
            switch (Type.GetTypeCode(dataType)) {
                case TypeCode.Boolean:
                    bool a; Boolean.TryParse(dataNew,out a); value = a; break;
                case TypeCode.Byte:
                    byte b; Byte.TryParse(dataNew,out b); value = b; break;
                case TypeCode.Char:
                    char c; Char.TryParse(dataNew,out c); value = c; break;
                case TypeCode.Int16:
                    short d; Int16.TryParse(dataNew,out d); value = d; break;
                case TypeCode.Int32:
                    int e; Int32.TryParse(dataNew,out e); value = e; break;
                case TypeCode.Int64:
                    long f; Int64.TryParse(dataNew,out f); value = f; break;
                case TypeCode.Single:
                    float g; float.TryParse(dataNew,out g); value = g; break;
                case TypeCode.Double:
                    double h; Double.TryParse(dataNew,out h); value = h; break;
                case TypeCode.String:
                    value = dataNew; break;
                default:
                    if (dataType.ToString() == "System.Object[]") {
                        value = ArmaArray.UnserializeArray(new string[] {dataNew});
                        break;
                    }
                    throw new Exception("Data type not supported!");
            }
            returnArray[index] = value;
            index++;
        }
        return returnArray;
    }
    */
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
