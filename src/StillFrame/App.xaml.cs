using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace StillFrame;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--render-ui")
        {
            string output = Path.GetFullPath(e.Args[1]);
            Environment.SetEnvironmentVariable("STILLFRAME_DATA_DIR", Path.Combine(output, "isolated-data"));
            Directory.CreateDirectory(output);
            var design = new MainWindow(renderOnly: true);
            design.ExportDesignScreens(output);
            design.Close(); Shutdown(); return;
        }
        instance = new Mutex(true, "Local\\StillFrame.Desktop", out bool created);
        if (!created) { MessageBox.Show("静界已经在运行，请从任务栏通知区域打开。", "静界"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            try { Directory.CreateDirectory(Store.DirectoryPath); File.AppendAllText(Path.Combine(Store.DirectoryPath, "error.log"), DateTimeOffset.Now + " " + args.Exception + Environment.NewLine); } catch { }
            MessageBox.Show("发生错误，辅助画面将关闭。诊断信息位于本机数据目录。\n" + args.Exception.Message, "静界");
            Shutdown(1);
        };
        var window = new MainWindow(); MainWindow = window; window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
