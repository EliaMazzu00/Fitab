using System.Text.Json.Serialization;

namespace Fitab.Api.Dto;

// I DTO rispecchiano esattamente il JSON di cms.fitab.it, nomi compresi.
// Non escono mai da Fitab.Api: vengono mappati sui modelli di dominio in FitabApiClient.
// I campi di servizio del runtime Instant Developer (do_loaded, do_updated,
// do_inserted, do_deleted) sono volutamente ignorati.

internal sealed class NewsDto
{
    public int Anno { get; set; }
    public int Progressivo { get; set; }
    public DateOnly? Data { get; set; }
    public string Titolo { get; set; } = "";
    public string? Messaggio { get; set; }
    public string? Link { get; set; }
    public bool Visibile { get; set; }
    public int IdCategoria { get; set; }
}

internal sealed class CalendarioDto
{
    public int Anno { get; set; }
    public int Progressivo { get; set; }
    public int Idcircolo { get; set; }
    public string? DataEvento { get; set; }
    public string Titolo { get; set; } = "";
    public string? Messaggio { get; set; }
    public string? Luogo { get; set; }
    public string? Rilevanza { get; set; }
    public string? Link { get; set; }
    public string? LinkRisultati { get; set; }
    public DateOnly? DaData { get; set; }
    public DateOnly? Adata { get; set; }
    public int Tipo { get; set; }
}

internal sealed class CircoloDto
{
    public int Codice { get; set; }
    public string Descrizione { get; set; } = "";
    public string? Idregione { get; set; }
    public string? Idprovincia { get; set; }
    public string? Regione { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Citta { get; set; }
    public string? Email { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Presidente { get; set; }
    public string? Responsabile { get; set; }
    public string? Cellulare { get; set; }
    public string? DoveGioca { get; set; }
    public string? QuandoGioca { get; set; }
    public string? SitoWebEsterno { get; set; }
    public bool Attivo { get; set; }
}

internal sealed class ArbitroDto
{
    public string CognomeNome { get; set; } = "";
    public string? Nota { get; set; }
}

internal sealed class ClassificaDto
{
    public int Posizione { get; set; }
    public string CognomeNome { get; set; } = "";
    public string? TesseraTesserati { get; set; }
    public int IdCircolo { get; set; }
    public string? DescrizioneRegione { get; set; }
    public string? DescrizioneCircolo { get; set; }
    public string? DescrizioneProvince { get; set; }
    public int Punti { get; set; }
    public int Icona { get; set; }
}

internal sealed class PosizioneDto
{
    public string? Tessera { get; set; }
    public string? Nominativo { get; set; }
    public int Anno { get; set; }
    public int Posizione { get; set; }
    public int Punti { get; set; }
    public int AnnoPrec { get; set; }
    public int PosizionePrec { get; set; }
    public int PuntiPrec { get; set; }
    public string? Circolo { get; set; }
}

internal sealed class PunteggiDto
{
    public DateOnly? Data { get; set; }
    public string? Descrizione { get; set; }
    public string? Rilevanza { get; set; }
    public string? Modalita { get; set; }
    public int Punti { get; set; }
    public int Posizione { get; set; }
    public string? Arbitro { get; set; }
    public string? Circolo { get; set; }
}

internal sealed class LiveTorneoDto
{
    public string Codice { get; set; } = "";
    public int IdCircolo { get; set; }
    public string? IdRegione { get; set; }
    public string? IdProvince { get; set; }
    public string Descrizione { get; set; } = "";
    public int CodiceTorneo { get; set; }
    public DateOnly? Data { get; set; }
    public string? Rilevanza { get; set; }
    public string? Modalita { get; set; }
    public int NumeroTavoli { get; set; }
    public string? Stato { get; set; }
    public string? Nota { get; set; }
    public int TipoGioco { get; set; }
    public int TurniTot { get; set; }
    public int TurniMit { get; set; }

    [JsonPropertyName("SOLODANESI")]
    public bool SoloDanesi { get; set; }

    public string? Arbitro { get; set; }
    public string? Circolo { get; set; }
}

internal sealed class LiveTurnoDto
{
    public string IdTurno { get; set; } = "";
    public string Turno { get; set; } = "";
    public string? Girone { get; set; }
    public string? Tipo { get; set; }
    public string Codice { get; set; } = "";
    public bool SonoQui { get; set; }
    public string? Posizione { get; set; }
}

internal sealed class LivePuntoDto
{
    public string Codice { get; set; } = "";
    public string DescrizioneCoppia { get; set; } = "";
    public string? Giocatore1 { get; set; }
    public string? Giocatore2 { get; set; }
    public string? IdTesseratoG1 { get; set; }
    public string? IdTesseratoG2 { get; set; }
    public string? Girone { get; set; }
    public int Posizione { get; set; }
    public int VP { get; set; }
    public int MP { get; set; }
    public string? IdentTurno { get; set; }
    public int NumTavolo { get; set; }
    public bool SonoIo { get; set; }
}

/// <summary>Risposta dei comandi LOGIN e VALIDITA (nodo "Esito").</summary>
internal sealed class EsitoDto
{
    public string? Codice { get; set; }
    public string? Messaggio { get; set; }
    public string? Tipo { get; set; }
    public int IdCircolo { get; set; }
    public string? Tessera { get; set; }

    [JsonPropertyName("DATAVALIDITA")]
    public string? DataValidita { get; set; }

    [JsonPropertyName("ASSOCIAZIONE")]
    public string? Associazione { get; set; }

    public bool Ok => string.Equals(Codice?.Trim(), "OK", StringComparison.OrdinalIgnoreCase);
}
