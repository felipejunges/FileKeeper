using System;
using System.Text;

namespace FileKeeper.Core.Utils;

/// <summary>
/// Small helpers for Base64 encoding/decoding of UTF-8 strings.
/// </summary>
public static class EncodingUtils
{
    /// <summary>
    /// Encodes the provided text as UTF-8 and returns its Base64 representation.
    /// </summary>
    /// <exception cref="ArgumentNullException" />
    public static string ToBase64(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Decodes a Base64 string to the original UTF-8 text.
    /// Throws ArgumentException if the input is not valid Base64.
    /// </summary>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="ArgumentException" />
    public static string FromBase64(string base64)
    {
        if (base64 is null) throw new ArgumentNullException(nameof(base64));

        try
        {
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Input is not a valid Base64 string.", nameof(base64), ex);
        }
    }
}

