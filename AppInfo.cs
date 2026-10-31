using System.Reflection;

namespace MohaaBspEditor;

public static class AppInfo
{
    private const string Fallback = "1.0.1";

    /// <summary>Версия приложения (Major.Minor.Build), берётся из &lt;Version&gt; в .csproj.</summary>
    public static string Version
    {
        get
        {
            var v = typeof(AppInfo).Assembly.GetName().Version;
            return v == null ? Fallback : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
