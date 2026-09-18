namespace Fitab.Core.Models;

/// <summary>
/// Rilevanza di un torneo. I codici corrispondono all'enum <c>vlRilevanzaTorneo</c>
/// del backend (app.js:5392 dell'app originale).
/// </summary>
public enum Rilevanza
{
    Sconosciuta = 0,
    Nazionale,
    Federale,
    Provinciale,
    Libero,
    Regionale,
    Circolo,
    Campionato,
    Altro
}

public static class RilevanzaExtensions
{
    public static Rilevanza FromCodice(string? codice) => codice?.Trim().ToUpperInvariant() switch
    {
        "N" => Rilevanza.Nazionale,
        "F" => Rilevanza.Federale,
        "P" => Rilevanza.Provinciale,
        "L" => Rilevanza.Libero,
        "R" => Rilevanza.Regionale,
        "C" => Rilevanza.Circolo,
        "M" => Rilevanza.Campionato,
        "A" => Rilevanza.Altro,
        _ => Rilevanza.Sconosciuta
    };

    public static string ToCodice(this Rilevanza r) => r switch
    {
        Rilevanza.Nazionale => "N",
        Rilevanza.Federale => "F",
        Rilevanza.Provinciale => "P",
        Rilevanza.Libero => "L",
        Rilevanza.Regionale => "R",
        Rilevanza.Circolo => "C",
        Rilevanza.Campionato => "M",
        Rilevanza.Altro => "A",
        _ => ""
    };

    public static string Descrizione(this Rilevanza r) => r switch
    {
        Rilevanza.Nazionale => "Nazionale",
        Rilevanza.Federale => "Federale",
        Rilevanza.Provinciale => "Provinciale",
        Rilevanza.Libero => "Libero",
        Rilevanza.Regionale => "Regionale",
        Rilevanza.Circolo => "Circolo",
        Rilevanza.Campionato => "Campionato",
        Rilevanza.Altro => "Altro",
        _ => ""
    };
}
