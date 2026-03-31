using System.Text;

namespace FileKeeper.Core.Extensions;

public static class VersionExtensions
{
    public static string ToFormatedString(this Version version)
    {
        var retorno = new StringBuilder();

        retorno.Append($"{version.Major}.{version.Minor}");

        if (version.Build > 0)
            retorno.Append($".{version.Build}");

        if (version.Revision > 0)
            retorno.Append($".{version.Revision}");

        return retorno.ToString();
    }
}