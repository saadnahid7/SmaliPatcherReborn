using System.Reflection;
using System.Text;

namespace SmaliPatcherReborn.Core;

public sealed class PatchInfo
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool DefaultOn { get; init; }

    public static readonly PatchInfo[] All =
    {
        new() { Id = "mock-hide", Title = "Hide mock-location flag", Description = "Apps that check Location.isMock() see a normal location.", DefaultOn = true },
        new() { Id = "mock-permission", Title = "Mock apps without developer setting", Description = "Test providers work without picking a \"mock location app\".", DefaultOn = true },
        new() { Id = "secure-flag", Title = "Allow screenshots in secure windows", Description = "Ignores FLAG_SECURE for screenshots and screen recording.", DefaultOn = false },
    };
}

public sealed class DeviceStatus
{
    public string Serial = "";
    public string Model = "", Brand = "", Release = "", Fingerprint = "", SecurityPatch = "", Abi = "";
    public int Api;
    public bool Debuggable;
    public long JarSize;
    public bool RootOk;
    public string RootMode = "none";           // none | adbd | su
    public string Manager = "";                // magisk | ksu | apatch
    public string ManagerBin = "";
    public string ManagerVersion = "";
    public bool ModuleInstalled;
    public bool ModuleDisabled;
    public bool ModuleRemoving;
    public string ModuleVersion = "";
    public string ModuleApplied = "";
    public string ModuleFingerprint = "";
    public string BootCount = "";
    public string GuardLog = "";
    public string ConfPatches = "";

    public bool JarStripped => JarSize > 0 && JarSize < 1_000_000;
    public bool FingerprintMatches => ModuleFingerprint != "" && ModuleFingerprint == Fingerprint;
    public bool SupportedApi => Api >= 29;
}

public sealed class DeviceSession
{
    public const string ModuleId = "smalipatcher_reborn";
    const string Tmp = "/data/local/tmp";

    readonly Adb _adb;
    public string Serial { get; }
    public Action<string> Log { get; set; } = _ => { };

    public DeviceSession(Adb adb, string serial) { _adb = adb; Serial = serial; }

    // ---- root plumbing ----------------------------------------------------------------------------

    string RootMode = "";

    /// <summary>Runs a multi-line script as root. Uses adbd root when available (emulators), otherwise su.</summary>
    public Adb.Result RootScript(string script, int timeoutMs = 300000, Action<string>? onLine = null)
    {
        var tmp = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, script.Replace("\r\n", "\n"), new UTF8Encoding(false));
            var name = $"spr_{Environment.ProcessId}.sh";
            var push = _adb.Push(Serial, tmp, $"{Tmp}/{name}");
            if (!push.Ok) return push;
            var cmd = RootMode == "adbd" ? $"sh {Tmp}/{name}" : $"su -c \"sh {Tmp}/{name}\"";
            var r = onLine == null
                ? _adb.Shell(Serial, cmd, timeoutMs)
                : _adb.RunStreaming(Serial, timeoutMs, onLine, "shell", cmd);
            _adb.Shell(Serial, $"rm -f {Tmp}/{name}", 10000);
            return r;
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    /// <summary>Detects how we can become root: adbd already root, adb root, or su.</summary>
    public string DetectRoot()
    {
        bool IsRoot(string s) => s.Contains("uid=0");
        if (IsRoot(_adb.Shell(Serial, "id", 8000).Output)) return RootMode = "adbd";
        // debug builds / emulators: adb root
        var dbg = _adb.Shell(Serial, "getprop ro.debuggable", 8000).Output.Trim();
        if (dbg == "1" && Serial.StartsWith("emulator-"))  // never restart adbd on a real phone
        {
            _adb.Run(Serial, 15000, "root");
            Thread.Sleep(2500);
            _adb.Run(Serial, 30000, "wait-for-device");
            if (IsRoot(_adb.Shell(Serial, "id", 8000).Output)) return RootMode = "adbd";
        }
        // su (Magisk/KernelSU/APatch). The manager app may show a grant prompt on the phone.
        if (IsRoot(_adb.Shell(Serial, "su -c id", 30000).Output)) return RootMode = "su";
        return RootMode = "none";
    }

    // ---- status ------------------------------------------------------------------------------------

    public DeviceStatus ReadStatus()
    {
        var s = new DeviceStatus { Serial = Serial };
        var props = _adb.Shell(Serial,
            "echo model=$(getprop ro.product.model); echo brand=$(getprop ro.product.brand);" +
            "echo release=$(getprop ro.build.version.release); echo api=$(getprop ro.build.version.sdk);" +
            "echo fp=$(getprop ro.build.fingerprint); echo patch=$(getprop ro.build.version.security_patch);" +
            "echo abi=$(getprop ro.product.cpu.abi); echo dbg=$(getprop ro.debuggable);" +
            "echo jar=$(stat -c %s /system/framework/services.jar 2>/dev/null)", 20000).Output;
        var kv = ParseKv(props);
        s.Model = kv.GetValueOrDefault("model", "");
        s.Brand = kv.GetValueOrDefault("brand", "");
        s.Release = kv.GetValueOrDefault("release", "");
        int.TryParse(kv.GetValueOrDefault("api", "0"), out s.Api);
        s.Fingerprint = kv.GetValueOrDefault("fp", "");
        s.SecurityPatch = kv.GetValueOrDefault("patch", "");
        s.Abi = kv.GetValueOrDefault("abi", "");
        s.Debuggable = kv.GetValueOrDefault("dbg", "") == "1";
        long.TryParse(kv.GetValueOrDefault("jar", "0"), out s.JarSize);

        s.RootMode = DetectRoot();
        s.RootOk = s.RootMode != "none";
        if (!s.RootOk) return s;

        const string script = @"
M=/data/adb/modules/smalipatcher_reborn
C=/data/adb/smalipatcher
for b in magisk /debug_ramdisk/magisk /sbin/magisk; do
  if v=$($b -v 2>/dev/null) && [ -n ""$v"" ]; then echo mgr=magisk; echo mgrbin=$b; echo mgrver=$v; break; fi
done
if [ -z ""$v"" ]; then
  for b in ksud /data/adb/ksud; do
    if v=$($b -V 2>/dev/null) && [ -n ""$v"" ]; then echo mgr=ksu; echo mgrbin=$b; echo mgrver=$v; break; fi
  done
fi
if [ -z ""$v"" ]; then
  for b in apd /data/adb/apd /data/adb/ap/bin/apd; do
    if v=$($b -V 2>/dev/null || $b --version 2>/dev/null) && [ -n ""$v"" ]; then echo mgr=apatch; echo mgrbin=$b; echo mgrver=$v; break; fi
  done
fi
if [ -d $M ]; then
  echo installed=1
  echo modver=$(sed -n 's/^version=//p' $M/module.prop)
  [ -f $M/disable ] && echo disabled=1
  [ -f $M/remove ] && echo removing=1
  echo applied=$(cat $M/patches.applied 2>/dev/null)
  echo modfp=$(cat $M/fingerprint 2>/dev/null)
fi
echo bootcount=$(cat $C/boot_count 2>/dev/null)
echo guard=$(tail -n 3 $C/guard.log 2>/dev/null | tr '\n' ';')
echo conf=$(sed -n 's/=1$//p' $C/patches.conf 2>/dev/null | tr '\n' ',')
";
        var k = ParseKv(RootScript(script, 60000).Output);
        s.Manager = k.GetValueOrDefault("mgr", "");
        s.ManagerBin = k.GetValueOrDefault("mgrbin", "");
        s.ManagerVersion = k.GetValueOrDefault("mgrver", "");
        s.ModuleInstalled = k.ContainsKey("installed");
        s.ModuleDisabled = k.ContainsKey("disabled");
        s.ModuleRemoving = k.ContainsKey("removing");
        s.ModuleVersion = k.GetValueOrDefault("modver", "");
        s.ModuleApplied = k.GetValueOrDefault("applied", "");
        s.ModuleFingerprint = k.GetValueOrDefault("modfp", "");
        s.BootCount = k.GetValueOrDefault("bootcount", "");
        s.GuardLog = k.GetValueOrDefault("guard", "");
        s.ConfPatches = k.GetValueOrDefault("conf", "").Trim(',');
        return s;
    }

    static Dictionary<string, string> ParseKv(string text)
    {
        var d = new Dictionary<string, string>();
        foreach (var line in text.Split('\n'))
        {
            var i = line.IndexOf('=');
            if (i <= 0) continue;
            var key = line[..i].Trim();
            if (key.Length == 0 || key.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_'))) continue;
            d[key] = line[(i + 1)..].Trim();
        }
        return d;
    }

    // ---- actions -----------------------------------------------------------------------------------

    public static byte[] ModuleZipBytes()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("module.zip")
            ?? throw new InvalidOperationException("module.zip is not embedded");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static void ExportModule(string path) => File.WriteAllBytes(path, ModuleZipBytes());

    /// <summary>Pushes the module and lets the root manager install it; the module patches services.jar on the phone.</summary>
    public bool Install(DeviceStatus st, IEnumerable<string> patches, Action<string> log)
    {
        var list = patches.ToList();
        if (list.Count == 0) { log("Select at least one patch."); return false; }
        if (!st.RootOk) { log("No root access. Grant the shell superuser permission and try again."); return false; }
        if (st.Manager == "") { log("No Magisk, KernelSU or APatch found."); return false; }
        if (!st.SupportedApi) { log($"Android API {st.Api} is not supported (needs Android 10 / API 29 or newer)."); return false; }
        if (st.JarStripped) { log("This ROM ships a stripped services.jar (code lives in odex/vdex). The on-device patcher can't read it yet."); return false; }

        log($"Writing patch selection: {string.Join(", ", list)}");
        var conf = string.Join("\n", PatchInfo.All.Select(p => $"{p.Id}={(list.Contains(p.Id) ? 1 : 0)}"));
        var prep = RootScript($"mkdir -p /data/adb/smalipatcher\ncat > /data/adb/smalipatcher/patches.conf <<'EOF'\n{conf}\nEOF\nrm -f {Tmp}/spr.zip\n");
        if (!prep.Ok) { log(prep.Output); return false; }

        var zip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmaliPatcherReborn-module.zip");
        File.WriteAllBytes(zip, ModuleZipBytes());
        try
        {
            log("Copying module to the phone...");
            var push = _adb.Push(Serial, zip, $"{Tmp}/spr.zip");
            if (!push.Ok) { log(push.Output); return false; }
        }
        finally { try { File.Delete(zip); } catch { } }

        string install = st.Manager switch
        {
            "magisk" => $"{st.ManagerBin} --install-module {Tmp}/spr.zip",
            _ => $"{st.ManagerBin} module install {Tmp}/spr.zip",
        };
        log($"Installing with {st.Manager} {st.ManagerVersion}... (patching takes about a minute)");
        var sb = new StringBuilder();
        var r = RootScript(install + "\nrc=$?\nrm -f " + Tmp + "/spr.zip\nexit $rc", 600000, l => { sb.AppendLine(l); log(l); });
        var text = sb.Length > 0 ? sb.ToString() : r.Output;
        bool ok = r.Ok && text.Contains("Reboot", StringComparison.OrdinalIgnoreCase) && !text.Contains("Nothing was changed");
        log(ok ? "Module installed. Reboot the phone to apply." : "Install did not complete. See the messages above.");
        return ok;
    }

    public bool Uninstall(DeviceStatus st, Action<string> log)
    {
        if (!st.RootOk) { log("No root access."); return false; }
        var r = RootScript($"[ -d /data/adb/modules/{ModuleId} ] && touch /data/adb/modules/{ModuleId}/remove && echo marked || echo 'module not installed'");
        log(r.Output.Trim());
        var ok = r.Output.Contains("marked");
        if (ok) log("The module will be removed at the next reboot.");
        return ok;
    }

    public bool SetDisabled(DeviceStatus st, bool disabled, Action<string> log)
    {
        var r = RootScript(disabled ? $"touch /data/adb/modules/{ModuleId}/disable && echo ok" : $"rm -f /data/adb/modules/{ModuleId}/disable && echo ok");
        log(r.Output.Contains("ok") ? (disabled ? "Module disabled. Reboot to apply." : "Module enabled. Reboot to apply.") : r.Output.Trim());
        return r.Output.Contains("ok");
    }

    public string ReadPatchLog() => RootScript("tail -n 60 /data/adb/smalipatcher/last-patch.log 2>&1").Output;

    public void Reboot() => _adb.Reboot(Serial);
}
