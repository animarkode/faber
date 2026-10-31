using System.Net;
using System.Text.RegularExpressions;

namespace Faber.Modules.Documents.Application.Razor.Extensions;

public static partial class HtmlSanitizer
{
    private const int RegexTimeoutMilliseconds = 1000;

    public static string Sanitize(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(input);

        decoded = decoded
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Replace("\u202F", " ", StringComparison.Ordinal)
            .Replace("\u2007", " ", StringComparison.Ordinal)
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace("\u200B", "", StringComparison.Ordinal)
            .Replace("\u200C", "", StringComparison.Ordinal)
            .Replace("\u200D", "", StringComparison.Ordinal)
            .Replace("\uFEFF", "", StringComparison.Ordinal)
            .Replace("\u2060", "", StringComparison.Ordinal)
            .Replace("\u00AD", "", StringComparison.Ordinal)
            .Replace("&shy;", "", StringComparison.Ordinal);

        decoded = BlindOrControlSymbolsRegex().Replace(decoded, "");
        decoded = TwoAndMoreSpaces().Replace(decoded, " ");

        return decoded.Trim();
    }

    [GeneratedRegex(@"[\p{Cc}&&[^\r\n\t]]", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex BlindOrControlSymbolsRegex();

    [GeneratedRegex(@"[ ]{2,}", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex TwoAndMoreSpaces();
}