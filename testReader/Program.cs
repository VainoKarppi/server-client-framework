using System;
using System.ComponentModel;
using System.IO;
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
        public List<object>? Parameters { get; set; }
    }
    static void Main()
    {
        //WriteDefaultValues();
        //DisplayValues();


        var networkMessage = new NetworkMessage
        {
            Date = DateTime.Parse("2019-08-01"),
            TemperatureCelsius = 25,
            Test = 100f,
            Summary = "Hot",
            Parameters = new List<object> {10,true}
        };

        

        List<object> newParams = new List<object>();
        foreach (object parameter in networkMessage.Parameters) {
            newParams.Add(parameter.GetType().ToString());
            newParams.Add(parameter);
        }

        networkMessage.Parameters = newParams;

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
        List<object>? paramArray = networkMessage1?.Parameters;
        
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
    
    public static object Parse(Type t, string s)
      => TypeDescriptor.GetConverter(t).ConvertFromInvariantString(s);
      
    public static object[] ParseParameters(object[] parameters) {
        object[] newParameters = new object[]{};

        foreach (object? parameter in parameters) {
            if (parameter == null) continue;
            Console.WriteLine(parameter);
            int intValue;
            double doubleValue;
            long longValue;
            string? stringValue;
            bool boolValue;
            
            // Check if number
            char[] nums = new char[] {'0','1','2','3','4','5','6','7','8','9','0','.','-'};
            if (nums.Contains(parameter.ToString().ElementAt(0))) {
                if (parameter.ToString().Contains('.')) {
                    if(double.TryParse(parameter.ToString(),out doubleValue)) {
                        newParameters.Append(doubleValue);
                        Console.WriteLine($"DOUBLE: {doubleValue}");
                    } else {
                        newParameters.Append(parameter); // Should not happen?
                    }
                } else {
                    if(int.TryParse(parameter.ToString(),out intValue)) {
                        newParameters.Append(intValue);
                        Console.WriteLine($"INT: {intValue}");
                    } else {
                        // Unable to parse as 32-bit so use 64-bit instead
                        if(long.TryParse(parameter.ToString(),out longValue)) {
                            newParameters.Append(longValue);
                            Console.WriteLine($"LONG: {longValue}");
                        } else {
                            newParameters.Append(parameter);
                        }
                        
                    }
                }
                continue;
            }

            
            
           

            if (Boolean.TryParse(parameter.ToString(),out boolValue)) {
                newParameters.Append(parameter.ToString());
                Console.WriteLine($"BOOL: {parameter}");
                continue;
            }

            stringValue = parameter.ToString();
            if(stringValue != null && stringValue.Length > 0 && stringValue.ElementAt(0) == '[') {
                // TODO inner arrays
                newParameters.Append(parameter.ToString());
                Console.WriteLine($"ARRAY: {parameter}");
                continue;
            }

            if(stringValue != null && stringValue.Length > 0) {
                newParameters.Append(parameter.ToString());
                Console.WriteLine($"STRING: {parameter}");
                continue;
            }

        }

        return newParameters;
    }

}