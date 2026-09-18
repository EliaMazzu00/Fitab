namespace Fitab.Core.Models;

/// <summary>
/// Tesserato autenticato. L'autenticazione Fitab e' di sola lettura: sblocca
/// la posizione personale in classifica, lo storico punteggi e l'evidenziazione
/// della propria coppia nelle classifiche live. Non esiste area riservata.
/// </summary>
public sealed record Tesserato
{
    public required string Tessera { get; init; }
    public required string CodiceFiscale { get; init; }
    public string Nominativo { get; init; } = "";
    public string Tipo { get; init; } = "";
    public int IdCircolo { get; init; }
    public string Associazione { get; init; } = "";
    public string DataValidita { get; init; } = "";
}

public sealed record EsitoLogin
{
    public required bool Riuscito { get; init; }

    /// <summary>Messaggio del backend: il nominativo se l'esito e' positivo, l'errore altrimenti.</summary>
    public string Messaggio { get; init; } = "";

    public Tesserato? Tesserato { get; init; }

    public static EsitoLogin Fallito(string messaggio) =>
        new() { Riuscito = false, Messaggio = messaggio };
}

public sealed record ValiditaTessera
{
    public required bool Valida { get; init; }
    public string Messaggio { get; init; } = "";
    public string Associazione { get; init; } = "";
    public string DataValidita { get; init; } = "";
}
