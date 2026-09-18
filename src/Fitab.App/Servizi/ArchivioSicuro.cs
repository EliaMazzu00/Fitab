namespace Fitab.App.Servizi;

/// <summary>
/// Conservazione locale delle credenziali del tesserato.
/// <para>
/// I web service FITAB non rilasciano un token: ogni chiamata personale richiede
/// di nuovo numero di tessera e codice fiscale, quindi vanno conservati sul
/// dispositivo. Essendo dati personali usiamo l'archivio cifrato del sistema
/// (Keystore su Android), non le semplici preferenze.
/// </para>
/// </summary>
public interface IArchivioSicuro
{
    Task<string?> LeggiAsync(string chiave);
    Task ScriviAsync(string chiave, string valore);
    void Rimuovi(string chiave);
}

public sealed class ArchivioSicuro : IArchivioSicuro
{
    public async Task<string?> LeggiAsync(string chiave)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(chiave);
        }
        catch (Exception)
        {
            // SecureStorage non e' disponibile ovunque (tipicamente su Windows
            // non pacchettizzato, che usiamo solo in sviluppo): in quel caso
            // ripieghiamo sulle preferenze, che bastano per far girare l'app.
            return Preferences.Default.Get<string?>(chiave, null);
        }
    }

    public async Task ScriviAsync(string chiave, string valore)
    {
        try
        {
            await SecureStorage.Default.SetAsync(chiave, valore);
        }
        catch (Exception)
        {
            Preferences.Default.Set(chiave, valore);
        }
    }

    public void Rimuovi(string chiave)
    {
        try
        {
            SecureStorage.Default.Remove(chiave);
        }
        catch (Exception)
        {
            // ignorata: si rimuove comunque dalle preferenze
        }

        Preferences.Default.Remove(chiave);
    }
}
