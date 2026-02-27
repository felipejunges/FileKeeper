using FileKeeper.Core.Extensions;
using System.Reflection;

namespace FileKeeper.Core.Application;

public class ApplicationInfo
{
    public static string GetAppVersion()
    {
        var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = entry.GetName().Version?.ToFormatedString();
        return version ?? string.Empty;
    }
    
    public static bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}