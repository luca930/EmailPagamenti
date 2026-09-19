using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace EmailPagamenti.Application.Parsing;

/// <summary>
/// Compila una volta sola le espressioni che arrivano da configurazione e le tiene pronte.
/// Il timeout e' obbligatorio: una formula scritta male non deve poter bloccare l'acquisizione.
/// </summary>
internal static class RegexCache
{
    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    public static Regex Get(string pattern) =>
        Cache.GetOrAdd(
            pattern,
            static p => new Regex(
                p,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Multiline,
                MatchTimeout));
}
