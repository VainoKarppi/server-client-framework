using System.Reflection;

namespace ServerFramework;

public static class Logger {
    public static bool Enabled = true;
    public static bool Debug = true;
    private static Thread? writerThread;
    private static List<object?> Texts = new List<object?>();
    private static string? logFile;
    private static void WriterThread() {
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
                        writer.WriteLine(text?.ToString());
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

    private static void CloseWriter() {
        writerThread = null;
    }

    public static void Log(object? text = null) {
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