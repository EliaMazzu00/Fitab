using System.Net;
using System.Text.RegularExpressions;

namespace Fitab.Core;

/// <summary>
/// I campi testuali del backend (messaggi di news e tornei) contengono markup HTML
/// inserito dal CMS. L'app originale aveva un <c>getCleanMessage()</c> per la stessa
/// ragione: serve una versione in solo testo per anteprime e troncamenti.
/// </summary>
public static partial class Testo
{
    // Sia i tag di apertura sia quelli di chiusura degli elementi di blocco:
    // il CMS produce spesso "testo<div>altro testo</div>", e considerando solo
    // le chiusure le due frasi si attaccherebbero ("CavezzoVenerdi").
    [GeneratedRegex(@"<br\s*/?>|</?(?:p|div|li|tr|h[1-6])(?:\s[^>]*)?>", RegexOptions.IgnoreCase)]
    private static partial Regex AcapoRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    // Include lo spazio unificatore (U+00A0): il CMS ne inserisce a valanga
    // incollando da Word, e se resta com'e' il testo non va a capo dove dovrebbe.
    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex SpaziRegex();

    [GeneratedRegex(@"(\r?\n){3,}")]
    private static partial Regex AcapiMultipliRegex();

    /// <summary>Rimuove il markup preservando gli a capo significativi.</summary>
    public static string SenzaHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var testo = AcapoRegex().Replace(html, "\n");
        testo = TagRegex().Replace(testo, "");
        testo = WebUtility.HtmlDecode(testo);
        testo = SpaziRegex().Replace(testo, " ");
        testo = AcapiMultipliRegex().Replace(testo, "\n\n");

        return testo.Trim();
    }

    /// <summary>Versione su riga singola, per le anteprime nelle liste.</summary>
    public static string Anteprima(string? html, int lunghezzaMassima = 120)
    {
        var testo = SenzaHtml(html).ReplaceLineEndings(" ");
        testo = SpaziRegex().Replace(testo, " ").Trim();

        return testo.Length <= lunghezzaMassima
            ? testo
            : string.Concat(testo.AsSpan(0, lunghezzaMassima - 1).TrimEnd(), "…");
    }
}
