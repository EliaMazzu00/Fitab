namespace Fitab.Core.Models;

/// <summary>Chiave naturale dei contenuti Fitab: anno + progressivo.</summary>
public readonly record struct ContenutoId(int Anno, int Progressivo)
{
    public override string ToString() => $"{Anno}_{Progressivo}";

    public static ContenutoId Parse(string value)
    {
        var parti = value.Split('_');
        return parti.Length == 2
               && int.TryParse(parti[0], out var anno)
               && int.TryParse(parti[1], out var prog)
            ? new ContenutoId(anno, prog)
            : throw new FormatException($"ContenutoId non valido: '{value}'");
    }
}

public sealed record News
{
    public required ContenutoId Id { get; init; }
    public DateOnly? Data { get; init; }
    public required string Titolo { get; init; }

    /// <summary>Corpo della news. Puo' contenere markup HTML.</summary>
    public string Messaggio { get; init; } = "";

    /// <summary>Nome del PDF allegato, da comporre con <c>UrlAllegatiNews</c>.</summary>
    public string? Allegato { get; init; }

    /// <summary>News in evidenza.</summary>
    public bool InPrimoPiano { get; init; }

    public int IdCategoria { get; init; }

    public bool HaAllegato => !string.IsNullOrWhiteSpace(Allegato);
}

public sealed record Torneo
{
    public required ContenutoId Id { get; init; }
    public required string Titolo { get; init; }

    /// <summary>Data di inizio in formato utilizzabile per ordinamento e filtri.</summary>
    public DateOnly? DaData { get; init; }

    public DateOnly? AData { get; init; }

    /// <summary>Data gia' formattata dal backend, es. "04 Sett. 2026". Solo per fallback.</summary>
    public string DataEvento { get; init; } = "";

    public string Luogo { get; init; } = "";
    public string Messaggio { get; init; } = "";
    public Rilevanza Rilevanza { get; init; }
    public int IdCircolo { get; init; }

    /// <summary>Locandina PDF, da comporre con <c>UrlAllegatiCalendario</c>.</summary>
    public string? Locandina { get; init; }

    /// <summary>
    /// Classifica finale in PDF, nella stessa cartella della locandina. La
    /// federazione la pubblica di rado: su ~760 tornei degli ultimi due anni e
    /// mezzo ce l'hanno in ventiquattro.
    /// </summary>
    public string? LinkRisultati { get; init; }

    public int Tipo { get; init; }

    public bool HaLocandina => !string.IsNullOrWhiteSpace(Locandina);
    public bool HaRisultati => !string.IsNullOrWhiteSpace(LinkRisultati);

    /// <summary>
    /// Vero quando e' passato anche l'ultimo giorno: i tornei di piu' giornate
    /// restano in programma mentre si stanno giocando.
    /// </summary>
    public bool EPassato =>
        (AData ?? DaData) is { } fine && fine < DateOnly.FromDateTime(DateTime.Today);
}

public sealed record Circolo
{
    public required int Codice { get; init; }
    public required string Descrizione { get; init; }
    public string Indirizzo { get; init; } = "";
    public string Cap { get; init; } = "";
    public string Citta { get; init; } = "";
    public string IdRegione { get; init; } = "";
    public string IdProvincia { get; init; } = "";
    public string Regione { get; init; } = "";
    public string Email { get; init; } = "";
    public string Telefono { get; init; } = "";
    public string Presidente { get; init; } = "";
    public string Responsabile { get; init; } = "";
    public string Cellulare { get; init; } = "";
    public string DoveGioca { get; init; } = "";
    public string QuandoGioca { get; init; } = "";
    public string SitoWeb { get; init; } = "";
    public bool Attivo { get; init; }

    public string IndirizzoCompleto =>
        string.Join(", ", new[] { Indirizzo, Cap, Citta }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed record Arbitro
{
    public required string CognomeNome { get; init; }
    public string Nota { get; init; } = "";
}
