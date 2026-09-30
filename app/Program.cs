using Avalonia;

namespace SmaliPatcherReborn;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && Cli.Commands.Contains(args[0]))
        {
            if (OperatingSystem.IsWindows()) AttachConsole(-1);
            return Cli.Run(args);
        }
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (DllNotFoundException ex) when (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The window could not start because a system library is missing: " + ex.Message.Split('\n')[0]);
            Console.Error.WriteLine("On Ubuntu/Debian run: sudo apt install libice6 libsm6 libfontconfig1 libx11-6");
            Console.Error.WriteLine("You can also use the command line: SmaliPatcherReborn help");
            return 3;
        }
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool AttachConsole(int pid);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
