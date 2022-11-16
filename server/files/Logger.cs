using System.Reflection;

namespace ServerFramework;

public static class Logger {
    public static bool Enabled = true;
    public static bool Debug = true;
    public static Thread? writerThread;
    public static List<object?> Texts = new List<object?>();
    public static string? logFile;
    public static void WriterThread() {
        string LogFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Logs";
        if (!Directory.Exists(LogFolder))
            Directory.CreateDirectory(LogFolder);

        if (logFile == null)
            logFile = LogFolder + @"\Log_" + DateTime.Now.ToString("yyyy-MM-dd-H_mm_ss") + ".txt";

        StreamWriter writer = new StreamWriter(logFile, true);
        while (writerThread != null) {
            if (Texts.Count() > 0) {
                try {
                    foreach (object? text in Texts.ToList()) {
                        writer.WriteLine(DateTime.Now.ToString("HH:mm:ss:FF\t") + text?.ToString());
                        Texts.Remove(text);
                    }
                    writer.Flush();
                } catch (Exception ex) {
                    Texts.Add(ex.Message);
                }
            }
            //Thread.Sleep(5);
        }
        writer.Flush();
        writer.Close();
    }

    public static void CloseWriter() {
        writerThread = null;
    }

    public static void Log(object? text = null) {
        string time = DateTime.Now.ToString("ss:FF");
        if (time.Length < 11) {
            string last = time.Substring(time.Length - 1,1);
            time = time.Remove(time.Length - 1);
            time = time + ("0" + last);
        }
        text = text == null ? null : $"{time} | {text}";

        if (Debug) Console.WriteLine(text);

        if (!Enabled) return;

        if (writerThread == null) {
            writerThread = new Thread(() => WriterThread());
            writerThread.Start();
        }

        Texts.Add(text);
    }
}