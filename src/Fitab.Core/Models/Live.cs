namespace Fitab.Core.Models;

public enum TipoTurno
{
    Sconosciuto = 0,
    Mitchell,
    Danese
}

public static class TipoTurnoExtensions
{
    public static TipoTurno FromCodice(string? codice) => codice?.Trim().ToUpperInvariant() switch
    {
        "M" => TipoTurno.Mitchell,
        "D" => TipoTurno.Danese,
        _ => TipoTurno.Sconosciuto
    };

    public static string Descrizione(this TipoTurno t) => t switch
    {
        TipoTurno.Mitchell => "Mitchell",
        TipoTurno.Danese => "Danese",
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
}
