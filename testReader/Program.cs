using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
class ConsoleApplication
{
    const string fileName = "AppSettings.dat";
    public class NetworkMessage
    {
        public DateTimeOffset Date { get; set; }
        public int TemperatureCelsius { get; set; }
        public float Test { get; set; }
        public string? Summary { get; set; }
        public object? Message {get; set; }
        public object[] Parameters { get; set; }
    }
    static int Main()
    {
        //WriteDefaultValues();
        //DisplayValues();


        NetworkMessage networkMessage = new NetworkMessage
        {
            Date = DateTime.Parse("2019-08-01"),
            TemperatureCelsius = 25,
            Test = 100f,
            Summary = "Hot",
            Parameters = new object[] {10,true,new object[]{"asd"}}
        };

        List<dynamic> newParams = new List<dynamic>();
        foreach (object parameter in networkMessage.Parameters) {
            newParams.Add(parameter.GetType().ToString());
            newParams.Add(parameter);
        }

        networkMessage.Parameters = newParams.ToArray();


        string test = @"[[29,""vaino""],10]";
        object[] p = Unserialize(test);
        Console.WriteLine(p.Count());
        foreach (var aa in p) {
            Console.WriteLine($"{aa.GetType()} | {aa.ToString()}");
        }

        return 0;
        Console.WriteLine(newParams.Count());

        string jsonString = JsonSerializer.Serialize(networkMessage);

        Console.WriteLine(jsonString);

        NetworkMessage? networkMessage1 = 
                JsonSerializer.Deserialize<NetworkMessage>(jsonString);

        Console.WriteLine(networkMessage1?.TemperatureCelsius.GetType());
        Console.WriteLine($"Date: {networkMessage1?.Date}");
        Console.WriteLine($"TemperatureCelsius: {networkMessage1?.TemperatureCelsius} {networkMessage1?.TemperatureCelsius.GetType()}");
        Console.WriteLine($"Test: {networkMessage1?.Test} {networkMessage1?.Test.GetType()}");
        Console.WriteLine($"Summary: {networkMessage1?.Summary}");
        Console.WriteLine();
        object[]? paramArray = networkMessage1?.Parameters;
        
        int ab;
        Type? type = default;
        if (paramArray != null) {
            int i = 0;
            List<object> final = new List<object>();
            for (i = 0; (paramArray.Count() / 2 + 1) >= i; i++) {
                object value = paramArray[i];
                if (i%2 == 0) {
                    type = Type.GetType(value.ToString());
                    Console.WriteLine(type);
                    continue;
                } else {
                    Console.WriteLine($"{value.ToString()}");
                    object AA = Parse(type,value.ToString());
                    final.Add(AA);
                }
            }
            //object[] finale = final.ToArray();
            foreach(object a in final){
                Console.WriteLine($"{a.GetType()} | {a.ToString()}");
            }
        }

        

    }

    public static object[] Unserialize(string args) {
        if (args.ElementAt(0) != '[') 
            args = "[" + args + "]";
            
        args = args.Substring(1);

        DataArray array = new DataArray();
        char[] nums = new char[] {'0','1','2','3','4','5','6','7','8','9','0','.','-'};
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == '[') {
                StringBuilder str = new StringBuilder();
                int inArr = 0;
                while (true) {
                    if (args[i] == '[') inArr++;
                    if (args[i] == ']') inArr--;
                    str.Append(args[i]);
                    i++;
                    if (inArr == 0) break;
                }
                object[] innerArray = Unserialize(str.ToString());
                array.Add(innerArray);
            } else if (args[i] == '"') {
                StringBuilder str = new StringBuilder();
                bool isEnd = false;
                i++;
                while (true) {
                    try {
                        if (args[i] == '"') {
                            isEnd = !isEnd;
                        }
                    } catch {
                        break;
                    }
                    if (isEnd && (args[i] == ',' || args[i] == ']')) {
                        break;
                    }
                    str.Append(args[i]);
                    i++;
                }
                array.Add(str.ToString().TrimEnd('"'));
            } else if (nums.Contains(args[i])) {
                StringBuilder str = new StringBuilder();
                bool isFloat = false;
                while (nums.Contains(args[i])) {
                    if (args[i] == '.')
                        isFloat = true;
                    str.Append(args[i]);
                    i++;
                }
                if (isFloat) {
                    double num = Convert.ToDouble(str.ToString());
                    array.Add(num);
                } else {
                    int num = Convert.ToInt32(str.ToString());
                    array.Add(num);
                }
            } else if (Substring(args, i, 4).ToLower() == "true") {
                array.Add(true);
                i = i + 4;
            } else if (Substring(args, i, 5).ToLower() == "false") {
                array.Add(false);
                i = i + 5;
            }
        }
        return array.ToArray();
    }
    private static string Substring(string input, int start, int length) {
        int inputLength = input.Length;
        if (start + length >= inputLength) {
            return input.Substring(start);
        }
        return input.Substring(start, length);
    }
    
    public static object Parse(Type t, string s)
      => TypeDescriptor.GetConverter(t).ConvertFromInvariantString(s);
      

}