using SmaliPatcherReborn.Core;

namespace SmaliPatcherReborn;

/// <summary>Command line mode (same code as the GUI): devices | status | install | uninstall | reboot | export.</summary>
public static class Cli
{
    public static readonly string[] Commands = { "devices", "status", "install", "uninstall", "reboot", "export", "connect", "manual", "help", "--help", "-h" };

    public static int Run(string[] a)
    {
        var cmd = a[0];
        var opts = new Dictionary<string, string>();
        for (int i = 1; i < a.Length; i++)
        {
            if (a[i].StartsWith("--")) opts[a[i]] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : "1";
            else opts["_"] = a[i];
        }

        if (cmd is "help" or "--help" or "-h") { Help(); return 0; }
        if (cmd == "export")
        {
            var path = opts.GetValueOrDefault("_", "SmaliPatcherReborn-module.zip");
            DeviceSession.ExportModule(path);
            Console.WriteLine($"Module written to {System.IO.Path.GetFullPath(path)}");
            return 0;
        }

        if (cmd == "manual")
        {
            if (!opts.TryGetValue("--in", out var inPath)) { Console.Error.WriteLine("usage: manual --in <system/framework folder or services.jar> [--api N] [--patches a,b] [--java path]"); return 2; }
            var ids0 = opts.TryGetValue("--patches", out var pl0) ? pl0.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                                  : PatchInfo.All.Where(p => p.DefaultOn).Select(p => p.Id).ToArray();
            int.TryParse(opts.GetValueOrDefault("--api", "0"), out var api0);
            var res = ManualPatch.Run(inPath, api0, ids0, Console.WriteLine, opts.GetValueOrDefault("--java"));
            Console.WriteLine(res.Message);
            return res.Ok ? 0 : 1;
        }

        var adb = new Adb();
        if (!adb.Locate(opts.GetValueOrDefault("--adb"))) { Console.Error.WriteLine("adb not found."); return 2; }
        Console.WriteLine($"adb: {adb.Path} ({adb.Source})");

        if (cmd == "connect")
        {
            var r = adb.Connect(opts.GetValueOrDefault("_", ""));
            Console.WriteLine(r.Output.Trim());
            return r.Ok ? 0 : 1;
        }

        var devices = adb.Devices();
        if (cmd == "devices")
        {
            foreach (var d in devices) Console.WriteLine($"{d.Serial}\t{d.State}\t{d.Model}");
            if (devices.Count == 0) Console.WriteLine("(no devices)");
            return 0;
        }

        var ready = devices.Where(d => d.Ready).ToList();
        AdbDevice? dev = opts.TryGetValue("--serial", out var ser) ? devices.FirstOrDefault(d => d.Serial == ser) : ready.Count == 1 ? ready[0] : null;
        if (dev == null)
        {
            Console.Error.WriteLine(ready.Count > 1 ? "More than one device connected: pass --serial <id> (see 'devices')." : "No device selected or connected.");
            return 2;
        }
        if (!dev.Ready) { Console.Error.WriteLine($"Device {dev.Serial} is {dev.State}."); return 2; }

        var session = new DeviceSession(adb, dev.Serial);
        Action<string> log = Console.WriteLine;
        var st = session.ReadStatus();
        switch (cmd)
        {
            case "status":
                Console.WriteLine($"{st.Brand} {st.Model}  Android {st.Release} (API {st.Api})  patch {st.SecurityPatch}");
                Console.WriteLine($"build:   {st.Fingerprint}");
                Console.WriteLine($"root:    {st.RootMode}   manager: {(st.Manager == "" ? "none" : $"{st.Manager} {st.ManagerVersion}")}");
                Console.WriteLine($"jar:     {st.JarSize:N0} bytes{(st.JarStripped ? "  (STRIPPED)" : "")}");
                Console.WriteLine($"module:  {(st.ModuleInstalled ? $"{st.ModuleVersion}, applied [{st.ModuleApplied}]{(st.ModuleDisabled ? ", DISABLED" : "")}{(st.ModuleRemoving ? ", pending removal" : "")}{(st.FingerprintMatches ? "" : ", needs re-patch")}" : "not installed")}");
                if (st.GuardLog != "") Console.WriteLine($"guard:   {st.GuardLog}");
                return 0;
            case "install":
                var ids = opts.TryGetValue("--patches", out var pl) ? pl.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                                   : PatchInfo.All.Where(p => p.DefaultOn).Select(p => p.Id).ToArray();
                var bad = ids.Where(i => PatchInfo.All.All(p => p.Id != i)).ToList();
                if (bad.Count > 0) { Console.Error.WriteLine("unknown patch: " + string.Join(",", bad)); return 2; }
                var ok = session.Install(st, ids, log);
                if (ok && opts.ContainsKey("--reboot")) { log("Rebooting..."); session.Reboot(); }
                return ok ? 0 : 1;
            case "uninstall":
                var u = session.Uninstall(st, log);
                if (u && opts.ContainsKey("--reboot")) { log("Rebooting..."); session.Reboot(); }
                return u ? 0 : 1;
            case "reboot":
                session.Reboot();
                return 0;
        }
        Help();
        return 2;
    }

    static void Help() => Console.WriteLine("""
        Smali Patcher Reborn - CLI
          SmaliPatcherReborn                       open the window
          SmaliPatcherReborn devices               list adb devices
          SmaliPatcherReborn connect <ip:port>     adb connect (wireless debugging)
          SmaliPatcherReborn status  [--serial ID]
          SmaliPatcherReborn install [--serial ID] [--patches mock-hide,mock-permission,secure-flag] [--reboot]
          SmaliPatcherReborn uninstall [--serial ID] [--reboot]
          SmaliPatcherReborn reboot  [--serial ID]
          SmaliPatcherReborn export [file.zip]     save the Magisk/KernelSU/APatch module zip
          SmaliPatcherReborn manual --in <system/framework or services.jar> [--api N] [--patches ...]
                                                   patch a jar copied off a phone; module is written next to it (needs Java 17+)
        Common: --adb <path to adb>

        By saadnahid7 - https://droidrooter.com - https://github.com/saadnahid7/SmaliPatcherReborn
        Thanks to fOmey (original Smali Patcher) and sabpprook (SmaliPatcherEx).
        """);
}
