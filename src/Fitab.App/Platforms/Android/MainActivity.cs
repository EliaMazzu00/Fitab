using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace Fitab.App;

// Orientamento bloccato in verticale, come faceva l'app originale
// (app.device.lockOrientation("portrait")): l'interfaccia e' pensata per il
// telefono in piedi e in orizzontale tabelle e testate non hanno senso.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
        | ConfigChanges.UiMode | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Blu scuro del marchio, lo stesso di colorPrimaryDark.
    private static readonly Android.Graphics.Color BluBarre =
        Android.Graphics.Color.ParseColor("#003F7A");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Le barre di sistema si colorano da qui e non da un tema.
        //
        // Sovrascrivere "Maui.MainTheme" in styles.xml sembrava la strada
        // naturale, ma in Android uno stile chiamato "A.B" eredita
        // implicitamente da "A": dando a Maui.MainTheme il genitore
        // Maui.MainTheme.NoActionBar si crea un anello. Android lo spezza,
        // ripiega sul tema di piattaforma (che ha una action bar) e l'app
        // muore all'avvio inflazionando ActionBarContextView.
        // Da codice non c'e' ereditarieta' da rompere.
        if (Window is { } finestra)
        {
            finestra.SetStatusBarColor(BluBarre);
            finestra.SetNavigationBarColor(BluBarre);

            var controlli = WindowCompat.GetInsetsController(finestra, finestra.DecorView);
            if (controlli is not null)
            {
                // Icone chiare: sotto c'e' il blu scuro.
                controlli.AppearanceLightStatusBars = false;
                controlli.AppearanceLightNavigationBars = false;
            }
        }

        ApplicaInsetDiSistema();
    }

    /// <summary>
    /// Sposta il contenuto sotto le barre di sistema.
    /// <para>
    /// Da Android 15 la finestra e' sempre "edge to edge" e da targetSdk 36 non
    /// esiste piu' modo di rinunciarvi: senza questo la WebView parte da y=0 e
    /// il marchio finisce dietro orologio e batteria.
    /// </para>
    /// </summary>
    private void ApplicaInsetDiSistema()
    {
        var contenuto = FindViewById(Android.Resource.Id.Content);
        if (contenuto is null) return;

        ViewCompat.SetOnApplyWindowInsetsListener(contenuto, new GestoreInset());
        ViewCompat.RequestApplyInsets(contenuto);
    }

    private sealed class GestoreInset : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        // "View" e' ambiguo fra Android.Views e Microsoft.Maui.Controls:
        // qui e' sempre quella di Android.
        public WindowInsetsCompat OnApplyWindowInsets(
            Android.Views.View? vista, WindowInsetsCompat? inset)
        {
            if (vista is null || inset is null) return inset ?? WindowInsetsCompat.Consumed;

            var barre = inset.GetInsets(
                WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());

            vista.SetPadding(barre.Left, barre.Top, barre.Right, barre.Bottom);
            return WindowInsetsCompat.Consumed;
        }
    }
}
