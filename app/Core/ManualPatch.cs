using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SmaliPatcherReborn.Core;

/// <summary>
/// Manual mode: patch a services.jar that was copied off a phone (no phone connected) and write a ready-to-flash module
/// next to it. The module carries the SHA-256 of the original jar and refuses to install on a phone with a different one.
/// </summary>
public static class ManualPatch
{
    public sealed record Outcome(bool Ok, string Message, string? ZipPath = null);

    public static readonly (string Label, int Api)[] AndroidVersions =
    {
        ("Auto-detect", 0), ("Android 17 (API 37)", 37), ("Android 16 (API 36)", 36), ("Android 15 (API 35)", 35),
        ("Android 14 (API 34)", 34), ("Android 13 (API 33)", 33), ("Android 12L (API 32)", 32), ("Android 12 (API 31)", 31),
        ("Android 11 (API 30)", 30), ("Android 10 (API 29)", 29),
    };

    /// <summary>Accepts a services.jar, or a folder such as system/framework (or a folder above it).</summary>
    public static string? FindJar(string path)
    {
        if (File.Exists(path)) return path;
        if (!Directory.Exists(path)) return null;
        foreach (var rel in new[] { "services.jar", "framework/services.jar", "system/framework/services.jar", "system/system/framework/services.jar" })
        {
            var p = System.IO.Path.Combine(path, rel);
            if (File.Exists(p)) return p;
        }
        return Directory.EnumerateFiles(path, "services.jar", SearchOption.AllDirectories).FirstOrDefault();
    }

    /// <summary>Looks for ro.build.version.sdk in a build.prop near the jar.</summary>
    public static int DetectApi(string jarPath)
    {
        var dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(jarPath)!);
        for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
        {
            foreach (var name in new[] { "build.prop", "system/build.prop" })
            {
                var p = System.IO.Path.Combine(dir.FullName, name);
                if (!File.Exists(p)) continue;
                var m = Regex.Match(File.ReadAllText(p), @"^ro\.(?:system\.)?build\.version\.sdk=(\d+)", RegexOptions.Multiline);
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }
        }
        return 0;
    }

    public static string? FindJava(string? explicitPath = null)
    {
        var exe = OperatingSystem.IsWindows() ? "java.exe" : "java";
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath)) candidates.Add(explicitPath!);
        var jh = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(jh)) candidates.Add(System.IO.Path.Combine(jh, "bin", exe));
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(System.IO.Path.Combine(d.Trim('"'), exe));
        if (OperatingSystem.IsWindows())
        {
            foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
            {
                if (string.IsNullOrEmpty(root)) continue;
                foreach (var vendor in new[] { "Java", "Eclipse Adoptium", "Amazon Corretto", "Microsoft", "Zulu", "BellSoft", "Android\\Android Studio\\jbr" })
                {
                    var v = System.IO.Path.Combine(root, vendor);
                    if (!Directory.Exists(v)) continue;
                    foreach (var j in Directory.EnumerateFiles(v, exe, SearchOption.AllDirectories).Where(f => f.Contains("bin")).Take(6)) candidates.Add(j);
                }
            }
        }
        else
        {
            foreach (var root in new[] { "/usr/lib/jvm", "/Library/Java/JavaVirtualMachines" })
                if (Directory.Exists(root))
                    foreach (var j in Directory.EnumerateFiles(root, exe, SearchOption.AllDirectories).Where(f => f.Contains("bin")).Take(8)) candidates.Add(j);
        }

        foreach (var c in candidates.Distinct())
        {
            if (!File.Exists(c)) continue;
            var major = JavaMajor(c);
            if (major >= 17) return c;
        }
        return null;
    }

    static int JavaMajor(string java)
    {
        try
        {
            var psi = new ProcessStartInfo(java, "-version") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            var text = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
            p.WaitForExit(8000);
            var m = Regex.Match(text, @"version ""(\d+)(?:\.(\d+))?");
            if (!m.Success) return 0;
            var a = int.Parse(m.Groups[1].Value);
            return a == 1 ? int.Parse(m.Groups[2].Value) : a;
        }
        catch { return 0; }
    }

    static string ExtractEnginePc()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream("engine-pc.jar") ?? throw new InvalidOperationException("engine-pc.jar is not embedded");
        var dir = System.IO.Path.Combine(Adb.AppDataDir, "engine-" + asm.GetName().Version);
        Directory.CreateDirectory(dir);
        var jar = System.IO.Path.Combine(dir, "engine-pc.jar");
        if (!File.Exists(jar) || new FileInfo(jar).Length == 0)
        {
            using var f = File.Create(jar);
            s.CopyTo(f);
        }
        return jar;
    }

    static string Sha256(string path)
    {
        using var f = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
    }

    public static Outcome Run(string input, int api, IReadOnlyList<string> patches, Action<string> log, string? javaPath = null)
    {
        if (patches.Count == 0) return new Outcome(false, "Select at least one patch.");
        var jar = FindJar(input.Trim().Trim('"'));
        if (jar == null) return new Outcome(false, "No services.jar found there. Pick the system/framework folder you copied from the phone, or the services.jar itself.");
        log($"Using {jar}");
        var size = new FileInfo(jar).Length;
        log($"services.jar: {size:N0} bytes");
        if (size < 1_000_000) return new Outcome(false, "This services.jar is only a stub (the code is stored in oat/services.vdex). Stripped ROMs are not supported yet.");

        if (api == 0) api = DetectApi(jar);
        if (api == 0) return new Outcome(false, "Could not tell which Android version this is. Choose it in the list (a build.prop next to the jar would be detected automatically).");
        if (api < 29) return new Outcome(false, $"API {api} is not supported (Android 10 / API 29 or newer).");
        log($"Android API {api}");

        var java = FindJava(javaPath);
        if (java == null) return new Outcome(false, "Java 17 or newer is needed for manual patching. Install one (for example Eclipse Temurin or Amazon Corretto 21), then try again. Patching straight on a rooted phone does not need Java.");
        log($"Java: {java}");

        var outDir = System.IO.Path.GetDirectoryName(jar)!;
        var origHash = Sha256(jar);
        var tmpJar = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"spr-patched-{Environment.ProcessId}.jar");
        try
        {
            log($"Patching ({string.Join(", ", patches)})... this takes about a minute");
            var psi = new ProcessStartInfo(java)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in new[] { "-Xmx1536m", "-jar", ExtractEnginePc(), "patch", "--in", jar, "--out", tmpJar, "--api", api.ToString(), "--patches", string.Join(",", patches) })
                psi.ArgumentList.Add(a);
            var sb = new StringBuilder();
            using var p = new Process { StartInfo = psi };
            void H(object _, DataReceivedEventArgs e) { if (e.Data == null) return; lock (sb) sb.AppendLine(e.Data); if (e.Data.StartsWith("hits:") || e.Data.StartsWith("ERROR") || e.Data.StartsWith("OK") || e.Data.StartsWith("modified")) log("  " + e.Data); }
            p.OutputDataReceived += H; p.ErrorDataReceived += H;
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            p.WaitForExit();
            if (p.ExitCode != 0 || !File.Exists(tmpJar))
            {
                var errs = sb.ToString().Split('\n').Where(l => l.StartsWith("ERROR") || l.Contains("Exception")).Take(4);
                return new Outcome(false, "The patch engine reported a problem:\n" + string.Join("\n", errs));
            }

            // module = the app's embedded module + the pre-patched jar + proof of which jar it was made from
            var version = Assembly.GetExecutingAssembly().GetName().Version!;
            var zipPath = System.IO.Path.Combine(outDir, $"SmaliPatcherReborn-module-{version.Major}.{version.Minor}.{version.Build}-manual-{origHash[..8]}.zip");
            using (var srcZip = new ZipArchive(new MemoryStream(DeviceSession.ModuleZipBytes())))
            using (var fs = File.Create(zipPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var replaced = new HashSet<string> { "system/framework/services.jar", "orig.sha256", "patches.applied" };
                foreach (var e in srcZip.Entries)
                {
                    if (replaced.Contains(e.FullName) || e.FullName.EndsWith("/")) continue;
                    var ne = zip.CreateEntry(e.FullName, CompressionLevel.Optimal);
                    using var i = e.Open(); using var o = ne.Open(); i.CopyTo(o);
                }
                void Put(string name, byte[] data, CompressionLevel lvl)
                {
                    var ne = zip.CreateEntry(name, lvl);
                    using var o = ne.Open(); o.Write(data);
                }
                Put("system/framework/services.jar", File.ReadAllBytes(tmpJar), CompressionLevel.NoCompression);
                Put("orig.sha256", Encoding.ASCII.GetBytes(origHash + "\n"), CompressionLevel.Optimal);
                Put("patches.applied", Encoding.ASCII.GetBytes(string.Join(",", patches) + "\n"), CompressionLevel.Optimal);
            }
            log($"Original jar SHA-256: {origHash}");
            return new Outcome(true, $"Module written next to the jar:\n{zipPath}\nFlash it in Magisk, KernelSU or APatch on a phone with exactly this services.jar.", zipPath);
        }
        finally { try { File.Delete(tmpJar); } catch { } }
    }
}
