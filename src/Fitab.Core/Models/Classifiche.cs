namespace Fitab.Core.Models;

/// <summary>
/// Categoria del tesserato, derivata dai punti. Soglie prese dall'enum
/// <c>vlPunteggi</c> del backend; il campo <c>Icona</c> della classifica
/// contiene direttamente uno di questi valori.
/// </summary>
public enum Categoria
{
    NonClassificato = 200,
    Categoria3 = 450,
    Categoria2 = 750,
    Categoria1 = 1300,
    Master3 = 2300,
    Master2 = 4000,
    Master1 = 10000,
    GrandMaster = 10001
}

public static class CategoriaExtensions
{
    public static Categoria FromIcona(int icona) =>
        Enum.IsDefined(typeof(Categoria), icona) ? (Categoria)icona : Categoria.NonClassificato;

    public static string Descrizione(this Categoria c) => c switch
    {
        Categoria.NonClassificato => "Non classificato",
        Categoria.Categoria3 => "3ª categoria",
        Categoria.Categoria2 => "2ª categoria",
        Categoria.Categoria1 => "1ª categoria",
        Categoria.Master3 => "Master 3",
        Categoria.Master2 => "Master 2",
        Categoria.Master1 => "Master 1",
        Categoria.GrandMaster => "Grand Master",
        _ => ""
    };
}

public sealed record VoceClassifica
{
    public required int Posizione { get; init; }
    public required string CognomeNome { get; init; }
    public string Tessera { get; init; } = "";
    public string CodiceTesserato { get; init; } = "";
    public int Punti { get; init; }
    public int IdCircolo { get; init; }
    public string Circolo { get; init; } = "";
    public string Regione { get; init; } = "";
    public string Provincia { get; init; } = "";
    public Categoria Categoria { get; init; }
}

/// <summary>Posizione personale del tesserato, con confronto sull'anno precedente.</summary>
public sealed record MiaPosizione
{
    public string Tessera { get; init; } = "";
    public string Nominativo { get; init; } = "";
    public string Circolo { get; init; } = "";
    public int Anno { get; init; }
    public int Posizione { get; init; }
    public int Punti { get; init; }
    public int AnnoPrecedente { get; init; }
    public int PosizionePrecedente { get; init; }
    public int PuntiPrecedenti { get; init; }

    /// <summary>Posizioni guadagnate rispetto all'anno precedente (positivo = miglioramento).</summary>
    public int DeltaPosizione => PosizionePrecedente > 0 ? PosizionePrecedente - Posizione : 0;

    public int DeltaPunti => Punti - PuntiPrecedenti;
}

/// <summary>Singolo risultato di torneo nello storico punteggi del tesserato.</summary>
public sealed record Punteggio
{
    public DateOnly? Data { get; init; }
    public string Descrizione { get; init; } = "";
    public Rilevanza Rilevanza { get; init; }
    public string Modalita { get; init; } = "";
    public int Punti { get; init; }
    public int Posizione { get; init; }
    public string Arbitro { get; init; } = "";
    public string Circolo { get; init; } = "";
}
