using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SmaliPatcherReborn.Core;

public sealed record AdbDevice(string Serial, string State, string Model, string Product)
{
    public bool Ready => State == "device";
    public bool IsEmulator => Serial.StartsWith("emulator-");
    public override string ToString() =>
        (string.IsNullOrEmpty(Model) ? Serial : $"{Model}  ({Serial})") + (Ready ? "" : $"  [{State}]");
}

/// <summary>Thin adb wrapper. Uses the adb already on this PC if there is one, otherwise the copy embedded in the app.</summary>
public sealed class Adb
{
    public string Path { get; private set; } = "";
    public string Source { get; private set; } = "";
    public Action<string>? Log { get; set; }

    static string PlatformKey =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" : "linux";

    static string Exe => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "adb.exe" : "adb";

    public static string AppDataDir
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root)) root = System.IO.Path.GetTempPath();
            return System.IO.Path.Combine(root, "SmaliPatcherReborn");
        }
    }

    /// <summary>Finds adb: explicit path, PATH, ANDROID_HOME/SDK_ROOT, then the embedded copy.</summary>
    public bool Locate(string? explicitPath = null)
    {
        var candidates = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(explicitPath)) candidates.Add((explicitPath!, "custom path"));

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add((System.IO.Path.Combine(dir.Trim('"'), Exe), "PATH"));
        foreach (var v in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var sdk = Environment.GetEnvironmentVariable(v);
            if (!string.IsNullOrEmpty(sdk)) candidates.Add((System.IO.Path.Combine(sdk, "platform-tools", Exe), v));
        }

        foreach (var (p, src) in candidates)
        {
            if (File.Exists(p) && Works(p)) { Path = p; Source = src; return true; }
        }

        var embedded = ExtractEmbedded();
        if (embedded != null && Works(embedded)) { Path = embedded; Source = "built in"; return true; }
        return false;
    }

    static bool Works(string exe)
    {
        try
        {
            var r = RunRaw(exe, new[] { "version" }, 8000);
            return r.Code == 0 && r.Output.Contains("Android Debug Bridge");
        }
        catch { return false; }
    }

    static string? ExtractEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = $"platform-tools-{PlatformKey}.zip";
        using var s = asm.GetManifestResourceStream(name);
        if (s == null) return null;
        var dir = System.IO.Path.Combine(AppDataDir, "platform-tools-" + asm.GetName().Version);
        var exe = System.IO.Path.Combine(dir, "platform-tools", Exe);
        if (!File.Exists(exe))
        {
            Directory.CreateDirectory(dir);
            using var z = new ZipArchive(s);
            z.ExtractToDirectory(dir, true);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        return exe;
    }

    public sealed record Result(int Code, string Output)
    {
        public bool Ok => Code == 0;
    }

    static Result RunRaw(string exe, IEnumerable<string> args, int timeoutMs, Action<string>? onLine = null)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        var gate = new object();
        void Handler(object _, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (gate) sb.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        }
        p.OutputDataReceived += Handler;
        p.ErrorDataReceived += Handler;
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(true); } catch { }
            return new Result(-1, sb + "\n[timeout]");
        }
        p.WaitForExit();
        return new Result(p.ExitCode, sb.ToString());
    }

    public Result Run(string? serial, int timeoutMs, params string[] args)
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(serial)) { list.Add("-s"); list.Add(serial!); }
        list.AddRange(args);
        return RunRaw(Path, list, timeoutMs);
    }

    public Result RunStreaming(string serial, int timeoutMs, Action<string> onLine, params string[] args)
    {
        var list = new List<string> { "-s", serial };
        list.AddRange(args);
        return RunRaw(Path, list, timeoutMs, onLine);
    }

    public List<AdbDevice> Devices()
    {
        Run(null, 15000, "start-server");
        var r = Run(null, 15000, "devices", "-l");
        var list = new List<AdbDevice>();
        foreach (var raw in r.Output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("List of") || line.StartsWith("*")) continue;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            string Field(string k) => parts.Skip(2).FirstOrDefault(x => x.StartsWith(k + ":"))?.Substring(k.Length + 1) ?? "";
            list.Add(new AdbDevice(parts[0], parts[1], Field("model").Replace('_', ' '), Field("product")));
        }
        return list;
    }

    public Result Connect(string hostPort) => Run(null, 20000, "connect", hostPort);
    public Result Pair(string hostPort, string code) => Run(null, 30000, "pair", hostPort, code);
    public Result Shell(string serial, string command, int timeoutMs = 60000) => Run(serial, timeoutMs, "shell", command);
    public Result Push(string serial, string local, string remote) => Run(serial, 300000, "push", local, remote);
    public Result Reboot(string serial) => Run(serial, 20000, "reboot");
}
