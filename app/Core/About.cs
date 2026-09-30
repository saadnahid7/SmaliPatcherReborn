using System.Diagnostics;

namespace SmaliPatcherReborn.Core;

public static class About
{
    public const string Author = "saadnahid7";
    public const string AuthorUrl = "https://github.com/saadnahid7";
    public const string Website = "https://droidrooter.com";
    public const string Repo = "https://github.com/saadnahid7/SmaliPatcherReborn";
    public const string Original = "fOmey";
    public const string OriginalUrl = "https://xdaforums.com/t/module-smali-patcher-7-4.3680053/";
    public const string Update = "sabpprook";
    public const string UpdateUrl = "https://xdaforums.com/t/module-smalipatcherex-1-2-2.4627905/";

    public static string Credits =>
        $"Smali Patcher Reborn is maintained by {Author}.\n" +
        $"With thanks to {Original}, who created the original Smali Patcher, and to {Update}, who carried it forward as SmaliPatcherEx. " +
        "This is a new implementation built on their idea.";

    public static void Open(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS()) Process.Start("open", url);
            else Process.Start("xdg-open", url);
        }
        catch { }
    }
}
