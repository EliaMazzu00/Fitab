using System.Globalization;
using System.Text.Json;
using Fitab.Api.Dto;
using Fitab.Api.Json;
using Fitab.Core;
using Fitab.Core.Abstractions;
using Fitab.Core.Models;

namespace Fitab.Api;

/// <summary>
/// Client dei web service FITAB.
/// <para>
/// Particolarita' del backend, tutte gestite qui e non oltre:
/// i parametri viaggiano negli <b>header HTTP</b> e non in query string (la query
/// string contiene solo un parametro fittizio); tutte le chiamate sono GET; la
/// risposta e' un oggetto con un unico nodo array il cui nome varia per endpoint
/// (<c>NewsClass</c>, <c>CalendarioClass</c>, ...), quindi lo individuiamo per
/// forma anziche' per nome, cosi' una rinomina lato server non ci rompe.
/// </para>
/// </summary>
public sealed class FitabApiClient : IFitabApi
{
    private const string Comandi = "ComandiClass?Codice=''";

    private readonly HttpClient _http;
    private readonly FitabApiOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new FitabBooleanConverter(),
            new FitabDateOnlyConverter(),
            new FitabIntConverter(),
            new FitabStringConverter()
        }
    };

    public FitabApiClient(HttpClient http, FitabApiOptions options)
    {
        _http = http;
        _options = options;
        _http.BaseAddress ??= options.BaseUrl;
    }

    // --- News ---------------------------------------------------------------

    public async Task<IReadOnlyList<News>> GetNewsAsync(int anno = 0, CancellationToken ct = default)
    {
        var dto = await GetListAsync<NewsDto>(Comandi, ct, new()
        {
            ["Comando"] = "ELENEW",
            ["Anno"] = anno.ToString(CultureInfo.InvariantCulture)
        });

        return dto.Where(d => d.Visibile).Select(Map).ToList();
    }

    public async Task<News?> GetNewsAsync(ContenutoId id, CancellationToken ct = default)
    {
        var dto = await GetListAsync<NewsDto>(
            $"NewsClass?Anno={id.Anno}&Progressivo={id.Progressivo}", ct);
        return dto.Count > 0 ? Map(dto[0]) : null;
    }

    private static News Map(NewsDto d) => new()
    {
        Id = new ContenutoId(d.Anno, d.Progressivo),
        Data = d.Data,
        Titolo = d.Titolo.Trim(),
        Messaggio = d.Messaggio?.Trim() ?? "",
        Allegato = Vuoto(d.Link) ? null : d.Link!.Trim(),
        InPrimoPiano = d.Prima,
        IdCategoria = d.IdCategoria
    };

    // --- Tornei -------------------------------------------------------------

    public async Task<IReadOnlyList<Torneo>> GetCalendarioAsync(
        DateOnly? daData = null, Rilevanza? rilevanza = null, CancellationToken ct = default)
    {
        var dto = await GetListAsync<CalendarioDto>(Comandi, ct, new()
        {
            ["Comando"] = "ELETOR",
            ["DaData"] = daData?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
            ["Rilevanza"] = rilevanza?.ToCodice() ?? ""
        });

        return dto.Select(Map).ToList();
    }

    public async Task<Torneo?> GetTorneoAsync(ContenutoId id, CancellationToken ct = default)
    {
        var dto = await GetListAsync<CalendarioDto>(Comandi, ct, new()
        {
            ["Comando"] = "TORDET",
            ["Anno"] = id.Anno.ToString(CultureInfo.InvariantCulture),
            ["Progressivo"] = id.Progressivo.ToString(CultureInfo.InvariantCulture)
        });

        return dto.Count > 0 ? Map(dto[0]) : null;
    }

    private static Torneo Map(CalendarioDto d) => new()
    {
        Id = new ContenutoId(d.Anno, d.Progressivo),
        Titolo = d.Titolo.Trim(),
        DaData = d.DaData,
        AData = d.Adata,
        DataEvento = d.DataEvento?.Trim() ?? "",
        Luogo = d.Luogo?.Trim() ?? "",
        Messaggio = d.Messaggio?.Trim() ?? "",
        Rilevanza = RilevanzaExtensions.FromCodice(d.Rilevanza),
        IdCircolo = d.Idcircolo,
        Locandina = Vuoto(d.Link) ? null : d.Link!.Trim(),
        LinkRisultati = Vuoto(d.LinkRisultati) ? null : d.LinkRisultati!.Trim(),
        Tipo = d.Tipo
    };

    // --- Circoli e arbitri --------------------------------------------------

    public async Task<IReadOnlyList<Circolo>> GetCircoliAsync(CancellationToken ct = default)
    {
        var dto = await GetListAsync<CircoloDto>("CircoloClass?Attivo=1", ct);
        return dto.Select(Map).OrderBy(c => c.Descrizione, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Circolo?> GetCircoloAsync(int codice, CancellationToken ct = default)
    {
        var dto = await GetListAsync<CircoloDto>($"CircoloClass?Codice={codice}", ct);
        return dto.Count > 0 ? Map(dto[0]) : null;
    }

    private static Circolo Map(CircoloDto d) => new()
    {
        Codice = d.Codice,
        Descrizione = d.Descrizione.Trim(),
        Indirizzo = d.Indirizzo?.Trim() ?? "",
        Cap = d.Cap?.Trim() ?? "",
        Citta = d.Citta?.Trim() ?? "",
        IdRegione = d.Idregione?.Trim() ?? "",
        IdProvincia = d.Idprovincia?.Trim() ?? "",
        Regione = d.Regione?.Trim() ?? "",
        Email = d.Email?.Trim() ?? "",
        Telefono = Vuoto(d.Telefono1) ? d.Telefono2?.Trim() ?? "" : d.Telefono1!.Trim(),
        Presidente = d.Presidente?.Trim() ?? "",
        Responsabile = d.Responsabile?.Trim() ?? "",
        Cellulare = d.Cellulare?.Trim() ?? "",
        // Due terzi dei circoli hanno questi campi incollati da Word attraverso
        // il CMS: arrivano pieni di <p class="MsoNormal">, <o:p> e <div>.
        // Li ripuliamo qui, cosi' nessuna schermata deve preoccuparsene.
        DoveGioca = Testo.SenzaHtml(d.DoveGioca),
        QuandoGioca = Testo.SenzaHtml(d.QuandoGioca),
        SitoWeb = d.SitoWebEsterno?.Trim() ?? "",
        Attivo = d.Attivo
    };

    public async Task<IReadOnlyList<Arbitro>> GetArbitriAsync(int anno = 0, CancellationToken ct = default)
    {
        var annoRichiesto = anno > 0 ? anno : DateTime.Today.Year;
        var dto = await GetListAsync<ArbitroDto>(Comandi, ct, new()
        {
            ["Comando"] = "ELEARB",
            ["Anno"] = annoRichiesto.ToString(CultureInfo.InvariantCulture)
        });

        return dto.Select(d => new Arbitro
        {
            CognomeNome = d.CognomeNome.Trim(),
            Nota = d.Nota?.Trim() ?? ""
        }).ToList();
    }

    // --- Classifiche --------------------------------------------------------

    public async Task<IReadOnlyList<VoceClassifica>> GetClassificaAsync(
        int tipoClassifica,
        int daPosizione = 1,
        int? codiceCircolo = null,
        bool escludiFederaliECircolo = false,
        CancellationToken ct = default)
    {
        var headers = new Dictionary<string, string?>
        {
            ["TipoClassifica"] = tipoClassifica.ToString(CultureInfo.InvariantCulture),
            ["DaPosizione"] = daPosizione.ToString(CultureInfo.InvariantCulture),
            ["EscludiFedCir"] = escludiFederaliECircolo ? "-1" : "0"
        };

        // Attenzione: l'header Circolo va omesso se non filtriamo. Inviarlo vuoto
        // fa rispondere al backend con un errore non-JSON.
        if (codiceCircolo is { } circolo)
            headers["Circolo"] = circolo.ToString(CultureInfo.InvariantCulture);

        var dto = await GetListAsync<ClassificaDto>(
            "ClassificaClass?Posizione=0", ct, headers, _options.TimeoutClassifica);

        return dto.Select(d => new VoceClassifica
        {
            Posizione = d.Posizione,
            CognomeNome = d.CognomeNome.Trim(),
            Tessera = d.TesseraTesserati?.Trim() ?? "",
            CodiceTesserato = d.CodiceTesserati?.Trim() ?? "",
            Punti = d.Punti,
            IdCircolo = d.IdCircolo,
            Circolo = d.DescrizioneCircolo?.Trim() ?? "",
            Regione = d.DescrizioneRegione?.Trim() ?? "",
            Provincia = d.DescrizioneProvince?.Trim() ?? "",
            Categoria = CategoriaExtensions.FromIcona(d.Icona)
        }).ToList();
    }

    public async Task<MiaPosizione?> GetMiaPosizioneAsync(
        string tessera, string codiceFiscale, CancellationToken ct = default)
    {
        var dto = await GetListAsync<PosizioneDto>("PosizioneClass?Posizione=0", ct, new()
        {
            ["Tessera"] = tessera,
            ["CodiceFiscale"] = codiceFiscale
        });

        if (dto.Count == 0) return null;
        var d = dto[0];

        return new MiaPosizione
        {
            Tessera = d.Tessera?.Trim() ?? tessera,
            Nominativo = d.Nominativo?.Trim() ?? "",
            Circolo = d.Circolo?.Trim() ?? "",
            Anno = d.Anno,
            Posizione = d.Posizione,
            Punti = d.Punti,
            AnnoPrecedente = d.AnnoPrec,
            PosizionePrecedente = d.PosizionePrec,
            PuntiPrecedenti = d.PuntiPrec
        };
    }

    public async Task<IReadOnlyList<Punteggio>> GetPunteggiAsync(
        string tessera, string codiceFiscale, int anno, CancellationToken ct = default)
    {
        var dto = await GetListAsync<PunteggiDto>("PunteggiClass?Punti=0", ct, new()
        {
            ["Tessera"] = tessera,
            ["CodiceFiscale"] = codiceFiscale,
            ["Anno"] = anno.ToString(CultureInfo.InvariantCulture)
        });

        return dto.Select(d => new Punteggio
        {
            Data = d.Data,
            Descrizione = d.Descrizione?.Trim() ?? "",
            Rilevanza = RilevanzaExtensions.FromCodice(d.Rilevanza),
            Modalita = d.Modalita?.Trim() ?? "",
            Punti = d.Punti,
            Posizione = d.Posizione,
            Arbitro = d.Arbitro?.Trim() ?? "",
            Circolo = d.Circolo?.Trim() ?? ""
        }).ToList();
    }

    // --- Live ---------------------------------------------------------------

    public async Task<IReadOnlyList<LiveTorneo>> GetLiveTorneiAsync(
        string? tessera = null, CancellationToken ct = default)
    {
        var dto = await GetListAsync<LiveTorneoDto>(Comandi, ct, new()
        {
            ["Comando"] = "LIVTOR",
            ["Tessera"] = tessera ?? ""
        });

        return dto.Select(d => new LiveTorneo
        {
            Codice = d.Codice,
            Descrizione = d.Descrizione.Trim(),
            Data = d.Data,
            CodiceTorneo = d.CodiceTorneo,
            Rilevanza = RilevanzaExtensions.FromCodice(d.Rilevanza),
            Modalita = d.Modalita?.Trim() ?? "",
            Stato = d.Stato?.Trim() ?? "",
            Nota = d.Nota?.Trim() ?? "",
            Circolo = d.Circolo?.Trim() ?? "",
            Arbitro = d.Arbitro?.Trim() ?? "",
            IdCircolo = d.IdCircolo,
            IdRegione = d.IdRegione?.Trim() ?? "",
            IdProvincia = d.IdProvince?.Trim() ?? "",
            NumeroTavoli = d.NumeroTavoli,
            TurniTotali = d.TurniTot,
            TurniMitchell = d.TurniMit,
            TurniDanesi = d.TurniDan,
            SoloDanesi = d.SoloDanesi,
            TipoGioco = d.TipoGioco
        }).ToList();
    }

    public async Task<IReadOnlyList<LiveTurno>> GetLiveTurniAsync(
        string idTorneo, string? tessera = null, CancellationToken ct = default)
    {
        var dto = await GetListAsync<LiveTurnoDto>(Comandi, ct, new()
        {
            ["Comando"] = "LIVTUR",
            ["IdTorneo"] = idTorneo,
            ["Tessera"] = tessera ?? ""
        });

        return dto.Select(d => new LiveTurno
        {
            IdTurno = d.IdTurno.Trim(),
            Codice = d.Codice.Trim(),
            Descrizione = d.Turno.Trim(),
            Girone = d.Girone?.Trim() ?? "",
            Tipo = TipoTurnoExtensions.FromCodice(d.Tipo),
            SonoQui = d.SonoQui,
            Posizione = d.Posizione?.Trim() ?? ""
        }).ToList();
    }

    public async Task<IReadOnlyList<LiveRiga>> GetLiveClassificaAsync(
        string idTorneo, string turno, string girone,
        string? tessera = null, CancellationToken ct = default)
    {
        var dto = await GetListAsync<LivePuntoDto>(Comandi, ct, new()
        {
            ["Comando"] = "LIVCLA",
            ["IdTorneo"] = idTorneo,
            ["Turno"] = turno,
            ["Girone"] = girone,
            ["Tessera"] = tessera ?? ""
        });

        return dto.Select(d => new LiveRiga
        {
            Codice = d.Codice,
            Posizione = d.Posizione,
            DescrizioneCoppia = d.DescrizioneCoppia.Trim(),
            Giocatore1 = d.Giocatore1?.Trim() ?? "",
            Giocatore2 = d.Giocatore2?.Trim() ?? "",
            IdTesseratoG1 = d.IdTesseratoG1?.Trim() ?? "",
            IdTesseratoG2 = d.IdTesseratoG2?.Trim() ?? "",
            VP = d.VP,
            MP = d.MP,
            NumTavolo = d.NumTavolo,
            Girone = d.Girone?.Trim() ?? "",
            IdentTurno = d.IdentTurno?.Trim() ?? "",
            SonoIo = d.SonoIo
        }).OrderBy(r => r.Posizione).ToList();
    }

    // --- Tesserato ----------------------------------------------------------

    public async Task<EsitoLogin> LoginAsync(
        string tessera, string codiceFiscale, CancellationToken ct = default)
    {
        var dto = await GetListAsync<EsitoDto>(Comandi, ct, new()
        {
            ["Comando"] = "LOGIN",
            ["Tessera"] = tessera,
            ["CodiceFiscale"] = codiceFiscale
        });

        if (dto.Count == 0)
            return EsitoLogin.Fallito("Nessuna risposta dal server.");

        var e = dto[0];
        if (!e.Ok)
            return EsitoLogin.Fallito(Vuoto(e.Messaggio) ? "Tessera o codice fiscale non validi." : e.Messaggio!.Trim());

        // Sul comando LOGIN il backend usa Messaggio per restituire il nominativo.
        var tesserato = new Tesserato
        {
            Tessera = tessera,
            CodiceFiscale = codiceFiscale,
            Nominativo = e.Messaggio?.Trim() ?? "",
            Tipo = e.Tipo?.Trim() ?? "",
            IdCircolo = e.IdCircolo
        };

        var validita = await GetValiditaAsync(tessera, codiceFiscale, ct);
        if (validita is { Valida: true })
        {
            tesserato = tesserato with
            {
                Associazione = validita.Associazione,
                DataValidita = validita.DataValidita
            };
        }

        return new EsitoLogin
        {
            Riuscito = true,
            Messaggio = tesserato.Nominativo,
            Tesserato = tesserato
        };
    }

    public async Task<ValiditaTessera?> GetValiditaAsync(
        string tessera, string codiceFiscale, CancellationToken ct = default)
    {
        var dto = await GetListAsync<EsitoDto>(Comandi, ct, new()
        {
            ["Comando"] = "VALIDITA",
            ["Tessera"] = tessera,
            ["CodiceFiscale"] = codiceFiscale
        });

        if (dto.Count == 0) return null;
        var e = dto[0];

        return new ValiditaTessera
        {
            Valida = e.Ok,
            Messaggio = e.Messaggio?.Trim() ?? "",
            Associazione = e.Associazione?.Trim() ?? "",
            DataValidita = e.DataValidita?.Trim() ?? ""
        };
    }

    // --- Allegati -----------------------------------------------------------

    public string UrlAllegatoNews(string nomeFile) =>
        new Uri(_options.UrlAllegatiNews, Uri.EscapeDataString(nomeFile)).ToString();

    public string UrlLocandinaTorneo(string nomeFile) =>
        new Uri(_options.UrlAllegatiCalendario, Uri.EscapeDataString(nomeFile)).ToString();

    // Locandina e classifica finale stanno nella stessa cartella: il backend
    // distingue i due file col suffisso "Ris" ("202400011.pdf" e
    // "202400011Ris.pdf"), ma il nome ce lo da' lui e non lo deduciamo noi.
    public string UrlRisultatiTorneo(string nomeFile) =>
        new Uri(_options.UrlAllegatiCalendario, Uri.EscapeDataString(nomeFile)).ToString();

    // --- Infrastruttura -----------------------------------------------------

    private static bool Vuoto(string? s) => string.IsNullOrWhiteSpace(s);

    private async Task<List<T>> GetListAsync<T>(
        string percorso,
        CancellationToken ct,
        Dictionary<string, string?>? headers = null,
        TimeSpan? timeout = null)
    {
        using var richiesta = new HttpRequestMessage(HttpMethod.Get, percorso);

        if (headers is not null)
        {
            foreach (var (nome, valore) in headers)
                richiesta.Headers.TryAddWithoutValidation(nome, valore ?? "");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? _options.Timeout);

        using var risposta = await _http.SendAsync(
            richiesta, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        risposta.EnsureSuccessStatusCode();

        await using var stream = await risposta.Content.ReadAsStreamAsync(cts.Token);
        using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

        // Il nodo dati e' l'unica proprieta' di tipo array: lo troviamo per forma,
        // non per nome, cosi' non dipendiamo dai nomi delle classi del backend.
        foreach (var proprieta in documento.RootElement.EnumerateObject())
        {
            if (proprieta.Value.ValueKind != JsonValueKind.Array) continue;
            return proprieta.Value.Deserialize<List<T>>(JsonOptions) ?? [];
        }

        return [];
    }
}
