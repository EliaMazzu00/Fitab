using Microsoft.JSInterop;

namespace Fitab.App.Servizi;

public enum Tema
{
    /// <summary>Segue l'impostazione del telefono.</summary>
    Automatico,
    Chiaro,
    Scuro
}

/// <summary>
/// Tema dell'interfaccia scelto dall'utente.
/// <para>
/// Il CSS sa gia' fare entrambi i temi e per impostazione predefinita segue il
/// sistema. Qui aggiungiamo la scelta manuale, che scrive l'attributo
/// <c>data-tema</c> sulla radice del documento: i token lo leggono e l'intera
/// app cambia. La preferenza sopravvive alla chiusura.
/// </para>
/// </summary>
public sealed class ServizioTema
{
    private const string Chiave = "fitab.tema";

    private IJSRuntime? _js;

    public Tema Attuale { get; private set; } = Tema.Automatico;

    public event Action? Cambiato;

    /// <summary>
    /// Da chiamare quando il runtime JavaScript e' disponibile: legge la
    /// preferenza salvata e la applica.
    /// </summary>
    public async Task InizializzaAsync(IJSRuntime js)
    {
        _js = js;

        Attuale = Preferences.Default.Get(Chiave, nameof(Tema.Automatico)) switch
        {
            nameof(Tema.Chiaro) => Tema.Chiaro,
            nameof(Tema.Scuro) => Tema.Scuro,
            _ => Tema.Automatico
        };

        await ApplicaAsync();
    }

    public async Task ImpostaAsync(Tema tema)
    {
        if (tema == Attuale) return;

        Attuale = tema;
        Preferences.Default.Set(Chiave, tema.ToString());

        await ApplicaAsync();
        Cambiato?.Invoke();
    }

    private async Task ApplicaAsync()
    {
        if (_js is null) return;

        var valore = Attuale switch
        {
            Tema.Chiaro => "chiaro",
            Tema.Scuro => "scuro",
            _ => "auto"
        };

        try
        {
            await _js.InvokeVoidAsync("fitab.impostaTema", valore);
        }
        catch (JSDisconnectedException)
        {
            // La WebView non c'e' piu': niente da applicare.
        }
    }

    public static string Descrizione(Tema tema) => tema switch
    {
        Tema.Chiaro => "Chiaro",
        Tema.Scuro => "Scuro",
        _ => "Automatico"
    };
}
