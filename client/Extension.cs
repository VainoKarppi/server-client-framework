
/*
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace ClientFramework {

    public class Extension {
        public static bool Debug = true;
        public const int Version = 1000;

        public static int TestData(string method, string[] args, out string output) {

            
            DataRequest request = new DataRequest();
            
            // Parse arguments data
            DataArray parameters = DataArray.UnserializeArray(args);
            
            Console.WriteLine("\n===========[START OF REQUEST]===========");
            Console.WriteLine(@"CLIENT ==> [""" + method + @"""," + parameters + @"]""");

            // Get ASYNC Info
            //string[] splitted = method.Split(new char[] {'\u003f'});
            string[] splitted = method.Split('|');
            if (splitted.Count() > 1) {
                request.method = splitted[0];
                request.Id = Int32.Parse(splitted[1]);
                request.Async = (request.Id >= 0);

                Console.WriteLine("ASYNC: method: " + request.method + "\t" + "AsyncID: " + request.Id.ToString() + "\t" + "Async: " + request.Async.ToString());
            } else {
                request.method = method;
            }


            // returnCode -1 if error
            int returnCode = 0;
            output = "error";
            try {
                Console.WriteLine("---------------|method|---------------");
                if (request.Async) {
                    Thread t = new Thread(() => CallMethodAsync(request, parameters)) { IsBackground = true };
                    t.Start();
                    output = (@"[""ASYNC""]");
                } else {
                    DataArray methodReturn = new DataArray { CallMethod(request, parameters) };
                    if (methodReturn.Count() > 0) {
                        output = DataArray.Serialize(methodReturn);
                    }
                }
            } catch (Exception ex) {
                output = ParseError(ex);
                returnCode = -1;
            }

            Console.WriteLine("----------------|RETURN|----------------");
            Console.WriteLine("RETURN TO CLIENT ==> " + output + " CODE: " + returnCode.ToString());
            Console.WriteLine("============[END OF REQUEST]============\n");

            return returnCode;
        }





        //!======================================================
        //! METHODS
        //!======================================================
        public static object CallMethod(DataRequest request, DataArray parameters) {
            MethodInfo methodInfo = typeof(ClientMethods).GetMethod(request.method);
            if (methodInfo == null)
                throw new Exception("method " + request.method + " was not found");

            return methodInfo.Invoke(request.method,new object[] {parameters.ToArray()}); 
        }


        public static void CallMethodAsync(DataRequest request, DataArray parameters) {

            if (request.Id == -1) {
                CallMethod(request, parameters);
                return;
            }

            bool success = false;
            string output = "";
            try {
                DataArray methodReturn = new DataArray { CallMethod(request, parameters) };
                if (methodReturn.Count() > 0) {
                    output = DataArray.Serialize(methodReturn);    
                }
                success = true;
            } catch (Exception ex) {
                output = ParseError(ex);
            }
            Console.WriteLine(@"EXTENSION CALLBACK ==> [""ClientFramework""," + ReturnTypes.callback + "," + request.Id + "," + success + "," + output + @"""]");
            //callback.Invoke("ClientFramework", ReturnTypes.callback + "," + request.Id + "," + success.ToString(), output);
        }


        public static void Callmethod(string method, string output = "") {
            if (!output.StartsWith("[") && output != "")
                output = "[" + output + "]";

            Console.WriteLine(@"EXTENSION CALLmethod ==> [""ClientFramework"","+ ReturnTypes.callmethod + @",""" + method + @""",""" + output + @"""]");
            //callback.Invoke("ClientFramework", ReturnTypes.callmethod + @",""" + method, output);
        }


        // Parse error so that human can read it better from 3rd party source
        public static string ParseError(Exception ex) {
            Console.WriteLine("----------------|ERROR|-----------------" + "\n" + ex);
            string errorReturn;
            if (ex is TargetInvocationException) { // TODO Im sure there is a better way to parse this...
                string[] parts = ex.InnerException.ToString().Split(new string[] { ": ", "\t", "\n", "\r\n", "\r" }, StringSplitOptions.None);
                if (parts.Count() > 1) {
                    errorReturn = @"[""" + parts[1] + @"""]";
                    return errorReturn;
                }
            }
            errorReturn = @"[""" + ex.Message + @"""]";
            return errorReturn;
        }
    }
}
*/