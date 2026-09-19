namespace Fitab.Core.Models;

public enum TipoTurno
{
    Sconosciuto = 0,
    Mitchell,
    Danese,

    /// <summary>
    /// Pseudo-turno "0-P" che il backend aggiunge in coda all'elenco: contiene gli
    /// abbinamenti e non i risultati (VP e MP sono a zero su tutte le righe, verificato
    /// su 13 tornei). Non e' un turno giocato e va escluso dalla progressione.
    /// </summary>
    Abbinamenti
}

public static class TipoTurnoExtensions
{
    public static TipoTurno FromCodice(string? codice) => codice?.Trim().ToUpperInvariant() switch
    {
        "M" => TipoTurno.Mitchell,
        "D" => TipoTurno.Danese,
        "P" => TipoTurno.Abbinamenti,
        _ => TipoTurno.Sconosciuto
    };

    public static string Descrizione(this TipoTurno t) => t switch
    {
        TipoTurno.Mitchell => "Mitchell",
        TipoTurno.Danese => "Danese",
        TipoTurno.Abbinamenti => "Abbinamenti",
        _ => ""
    };
}

/// <summary>Torneo con risultati in diretta.</summary>
public sealed record LiveTorneo
{
    /// <summary>Identificativo del torneo (GUID), da passare come header <c>IdTorneo</c>.</summary>
    public required string Codice { get; init; }

    public required string Descrizione { get; init; }
    public DateOnly? Data { get; init; }
    public int CodiceTorneo { get; init; }
    public Rilevanza Rilevanza { get; init; }
    public string Modalita { get; init; } = "";

    /// <summary>
    /// Stato del torneo cosi' come lo espone il backend (es. "P").
    /// La semantica esatta dei codici non e' documentata: va ricavata osservando
    /// una serata reale prima di usarla per logica di business.
    /// </summary>
    public string Stato { get; init; } = "";

    public string Nota { get; init; } = "";
    public string Circolo { get; init; } = "";
    public string Arbitro { get; init; } = "";
    public int IdCircolo { get; init; }
    public string IdRegione { get; init; } = "";
    public string IdProvincia { get; init; } = "";
    public int NumeroTavoli { get; init; }
    public int TurniTotali { get; init; }
    public int TurniMitchell { get; init; }
    public int TurniDanesi { get; init; }
    public bool SoloDanesi { get; init; }
    public int TipoGioco { get; init; }

    public bool EDiOggi => Data == DateOnly.FromDateTime(DateTime.Today);
}

/// <summary>Turno di un torneo live. Diventa un chip nella barra dei turni.</summary>
public sealed record LiveTurno
{
    public required string IdTurno { get; init; }

    /// <summary>Codice del turno, es. "01-M". E' il valore da passare come header <c>Turno</c>.</summary>
    public required string Codice { get; init; }

    /// <summary>Etichetta estesa, es. "01° Mitchell".</summary>
    public required string Descrizione { get; init; }

    public string Girone { get; init; } = "";
    public TipoTurno Tipo { get; init; }

    /// <summary>Vero se il tesserato loggato sta giocando in questo turno.</summary>
    public bool SonoQui { get; init; }

    /// <summary>Posizione del tesserato in questo turno, gia' formattata dal backend (es. "3°").</summary>
    public string Posizione { get; init; } = "";

    /// <summary>
    /// Numero del turno estratto dal codice ("04-D" -> 4). Zero per lo pseudo-turno
    /// degli abbinamenti ("0-P").
    /// </summary>
    public int Numero =>
        int.TryParse(Codice.Split('-').FirstOrDefault(), out var n) ? n : 0;

    /// <summary>Vero se il turno e' stato davvero giocato, cioe' porta dei risultati.</summary>
    public bool Giocato => Tipo is TipoTurno.Mitchell or TipoTurno.Danese;

    /// <summary>
    /// Chiave univoca del turno. Attenzione: <see cref="Codice"/> da solo non basta,
    /// nei tornei a piu' gironi esistono due "04-D", uno per girone.
    /// </summary>
    public string Chiave => $"{Codice}|{Girone}";

    /// <summary>Etichetta breve per il chip, es. "1° M".</summary>
    public string EtichettaBreve
    {
        get
        {
            var numero = Codice.Split('-').FirstOrDefault()?.TrimStart('0');
            if (string.IsNullOrEmpty(numero)) return Codice;
            var sigla = Tipo switch
            {
                TipoTurno.Mitchell => "M",
                TipoTurno.Danese => "D",
                _ => ""
            };
            return string.IsNullOrEmpty(Girone) || Tipo == TipoTurno.Mitchell
                ? $"{numero}° {sigla}".Trim()
                : $"{numero}° {sigla}{Girone}".Trim();
        }
    }
}

public static class LiveTurnoExtensions
{
    /// <summary>
    /// Mette i turni nell'ordine in cui sono stati giocati: prima i Mitchell, poi i
    /// Danesi, ciascuno per numero.
    /// <para>
    /// Serve perche' il backend li restituisce alla rinfusa — su un torneo reale
    /// l'elenco era <c>01-D, 01-M, 02-M</c> — e perche' la sequenza puo' avere buchi:
    /// capita di vedere <c>01-D, 04-D</c> senza il 2° e il 3°, quando il gestionale
    /// dell'arbitro non ha spedito qualche turno. I buchi non sono un problema,
    /// VP e MP sono cumulativi e l'ultimo turno pubblicato porta comunque il totale.
    /// </para>
    /// </summary>
    public static IEnumerable<LiveTurno> InOrdineDiGioco(this IEnumerable<LiveTurno> turni) =>
        turni.Where(t => t.Giocato)
             .OrderBy(t => t.Tipo == TipoTurno.Mitchell ? 0 : 1)
             .ThenBy(t => t.Numero);

    /// <summary>Gironi presenti, in ordine alfabetico.</summary>
    public static IReadOnlyList<string> Gironi(this IEnumerable<LiveTurno> turni) =>
        turni.Select(t => t.Girone).Distinct().OrderBy(g => g, StringComparer.Ordinal).ToList();
}

/// <summary>Riga della classifica live di un turno: una coppia.</summary>
public sealed record LiveRiga
{
    /// <summary>Identificativo stabile della coppia: chiave per il diff tra due polling.</summary>
    public required string Codice { get; init; }

    public required int Posizione { get; init; }
    public required string DescrizioneCoppia { get; init; }
    public string Giocatore1 { get; init; } = "";
    public string Giocatore2 { get; init; } = "";
    public string IdTesseratoG1 { get; init; } = "";
    public string IdTesseratoG2 { get; init; } = "";
    public int VP { get; init; }
    public int MP { get; init; }
    public int NumTavolo { get; init; }
    public string Girone { get; init; } = "";
    public string IdentTurno { get; init; } = "";

    /// <summary>Vero se e' la coppia del tesserato loggato.</summary>
    public bool SonoIo { get; init; }

    /// <summary>
    /// Vero per la coppia segnaposto che il gestionale inserisce quando il numero di
    /// coppie e' dispari: compare come "FITTIZIO R - FITTIZIO R" con tessere 999991 e
    /// 999992, sempre a zero VP e zero MP. Chi la incontra e' di fatto a riposo.
    /// <para>
    /// Va riconosciuta: occupa un posto in classifica e un posto al tavolo, e rende
    /// non nulla la somma degli MP, che altrimenti e' zero per costruzione.
    /// </para>
    /// </summary>
    public bool EFittizia =>
        DescrizioneCoppia.Contains("FITTIZIO", StringComparison.OrdinalIgnoreCase)
        // Le due tessere insieme: una sola potrebbe in teoria appartenere a un
        // tesserato vero, tutte e due nella stessa coppia no.
        || (IdTesseratoG1.TrimStart('0') == "999991" && IdTesseratoG2.TrimStart('0') == "999992");

    /// <summary>
    /// Chiave stabile della coppia, utilizzabile per confrontare turni diversi.
    /// <para>
    /// <see cref="Codice"/> non va bene: e' l'identificativo del record di turno e
    /// cambia da un turno all'altro (verificato: la stessa coppia ha codici diversi
    /// nel 04-D e nello 0-P dello stesso torneo). Dentro un singolo turno invece e'
    /// stabile tra un polling e l'altro.
    /// </para>
    /// </summary>
    public string ChiaveCoppia =>
        string.IsNullOrEmpty(IdTesseratoG1) && string.IsNullOrEmpty(IdTesseratoG2)
            ? DescrizioneCoppia
            : $"{IdTesseratoG1}+{IdTesseratoG2}";
}
