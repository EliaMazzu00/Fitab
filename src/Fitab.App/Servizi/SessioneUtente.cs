using System.Text.Json;
using Fitab.Core.Models;

namespace Fitab.App.Servizi;

/// <summary>
/// Tesserato attualmente autenticato, se c'e'.
/// <para>
/// L'autenticazione FITAB e' di sola lettura: serve a sapere quale coppia
/// evidenziare nelle classifiche live, quale turno preselezionare e quale
/// posizione personale mostrare. Nessuna funzione dell'app e' preclusa a chi
/// non accede: la consultazione resta completa.
/// </para>
/// <para>
/// Le credenziali sopravvivono alla chiusura dell'app perche' i web service le
/// richiedono a ogni chiamata personale: chiederle a ogni avvio, a un pubblico
/// che deve digitare tessera e codice fiscale, sarebbe inaccettabile.
/// </para>
/// </summary>
public sealed class SessioneUtente(IArchivioSicuro archivio)
{
    private const string Chiave = "fitab.tesserato";

    private Tesserato? _attuale;

    public Tesserato? Attuale
    {
        get => _attuale;
        private set
        {
            _attuale = value;
            Cambiata?.Invoke();
        }
    }

    public bool Autenticato => _attuale is not null;

    /// <summary>Tessera da passare ai web service, o null se non autenticati.</summary>
    public string? Tessera => _attuale?.Tessera;

    public string? CodiceFiscale => _attuale?.CodiceFiscale;

    public event Action? Cambiata;

    /// <summary>Da chiamare all'avvio, prima di mostrare l'interfaccia.</summary>
    public async Task RipristinaAsync()
    {
        try
        {
            var json = await archivio.LeggiAsync(Chiave);
            if (string.IsNullOrWhiteSpace(json)) return;

            Attuale = JsonSerializer.Deserialize<Tesserato>(json);
        }
        catch (Exception)
        {
            // Dato illeggibile o scritto da una versione precedente: si riparte
            // da non autenticati, senza disturbare l'utente con un errore.
            archivio.Rimuovi(Chiave);
        }
    }

    public async Task AccediAsync(Tesserato tesserato)
    {
        Attuale = tesserato;
        await archivio.ScriviAsync(Chiave, JsonSerializer.Serialize(tesserato));
    }

    public void Esci()
    {
        archivio.Rimuovi(Chiave);
        Attuale = null;
    }
}
