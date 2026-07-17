using System.IO.Pipes;
using System.Text;

namespace DesktopAppFolder;

internal static class Program
{
    private const string MutexName = "DesktopAppFolder_SingleInstance_Mutex";
    private const string PipeName = "DesktopAppFolder_Pipe";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // WindowsFormsSynchronizationContextを明示的にセットする。
        // Application.Run()を呼ぶ前はSynchronizationContext.Currentがnullのため、
        // TrayAppContextのコンストラクタでこれをキャプチャできるようにする。
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        using var mutex = new Mutex(true, MutexName, out bool isNew);

        if (!isNew)
        {
            TrySendToExistingInstance(args);
            return;
        }

        SplashForm.ShowForMilliseconds(2000);

        var context = new TrayAppContext();

        if (args.Length > 0)
        {
            context.HandleCommand(args);
        }

        StartPipeServer(context);

        Application.Run(context);
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Storage.RootDir);
            string path = Path.Combine(Storage.RootDir, "DesktopAppFolder_crash.log");
            File.AppendAllText(path, $"[{DateTime.Now}] {source}:\n{ex}\n\n");
        }
        catch { /* ignore */ }
    }

    private static void TrySendToExistingInstance(string[] args)
    {
        try
        {
            LogCrash($"TrySend: connecting... args={string.Join(",", args)}", null);
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            LogCrash("TrySend: connected", null);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(string.Join("\u0001", args));
            LogCrash("TrySend: written", null);
        }
        catch (Exception ex)
        {
            LogCrash("TrySend: FAILED", ex);
        }
    }

    private static void StartPipeServer(TrayAppContext context)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    LogCrash("PipeServer: waiting for connection...", null);
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    server.WaitForConnection();
                    LogCrash("PipeServer: client connected", null);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line = reader.ReadLine();
                    LogCrash($"PipeServer: received line='{line}'", null);
                    if (!string.IsNullOrEmpty(line))
                    {
                        var args = line.Split('\u0001');
                        context.PostCommand(args);
                    }
                }
                catch (Exception ex)
                {
                    LogCrash("PipeServer: LOOP ERROR", ex);
                }
            }
        })
        { IsBackground = true };
        thread.Start();
    }
}