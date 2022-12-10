
using System.Reflection.Metadata;
using System.Data;
using System.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;


#if SERVER
using static ServerFramework.Logger;
using static ServerFramework.NetworkEvents;
using static ServerFramework.Network;
namespace ServerFramework;
#else
using static ClientFramework.Logger;
using static ClientFramework.NetworkEvents;
using static ClientFramework.Network;
namespace ClientFramework;
#endif



/// <summary>Create new instance of Logger</summary>
public static class Logger {
    /// <summary>Toggle writing to external .log file. (Creates a Logs folder in executing assembly path)</summary>
    public static bool Enabled = true;
    /// <summary>Toggle writing to console</summary>
    public static bool Debug = true;
    private static Thread? writerThread;
    private static List<object?> Texts = new List<object?>();
    private static string? logFile;
    private static void WriterThread() {
        try
        {
            string LogFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Logs";
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);

            if (logFile == null)
                logFile = LogFolder + @"\Log_" + DateTime.Now.ToString("yyyy-MM-dd-H_mm_ss") + ".txt";

            StreamWriter writer = new StreamWriter(logFile, true);
            while (writerThread != null)
            {
                if (Texts.Count() > 0)
                {
                    try
                    {
                        foreach (object? text in Texts.ToList())
                        {
                            writer.WriteLine(text?.ToString());
                            Texts.Remove(text);
                        }
                        writer.Flush();
                    }
                    catch (Exception ex)
                    {
                        Texts.Add(ex.Message);
                    }
                }
                Thread.Sleep(5);
            }
            writer.Flush();
            writer.Close();
        } catch {}
    }

    private static void CloseWriter() {
        writerThread = null;
    }

    internal static void Log(object? text = null) {
        string time = DateTime.Now.ToString("HH:mm: ss:FF");
        time = time.Remove(5,1);
        while (time.Length != 12) {
            if (time.Length == 8)
                time += ".";
            else
                time += "0";
        }
        text = text == null ? "             |" : $"{time} | {text}";

        if (Debug) Console.WriteLine(text);

        if (!Enabled) return;

        if (writerThread == null) {
            writerThread = new Thread(() => WriterThread());
            writerThread.Start();
        }

        Texts.Add(text);
    }
}