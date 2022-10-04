using System;
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
        public object[]? Parameters { get; set; }
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
            Parameters = new object[] {10,10000f,"test",false,new object[] {"asd"}}
        };

        uint num;
        string nam = "-1";
        bool succ = uint.TryParse(nam,out num);
        Console.WriteLine(num);
        Console.WriteLine(succ);
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
        object []? paramArray = networkMessage1?.Parameters;
        
        if (paramArray != null) {
            object[] asd = ParseParameters(paramArray);
        }

        

    }

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
            char[] nums = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', '-' };
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