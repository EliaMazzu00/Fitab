# Fitab 2.0 — soluzione

App dimostrativa per FITAB-APS. .NET MAUI Blazor, componenti Blazor propri
(nessuna libreria UI), dati dai web service `cms.fitab.it`.

Documenti di riferimento nella cartella superiore:

| File | Contenuto |
|---|---|
| `API-FITAB.md` | mappa degli endpoint, modello dati, prestazioni misurate |
| `STRUTTURA-APP.md` | struttura funzionale dell'app attuale (reverse engineering) |
| `PROPOSTA-2.0.md` | scelte tecniche e di prodotto |

## Progetti

```
Fitab.Core    modelli di dominio, IFitabApi, utilità testo
Fitab.Api     client HTTP su cms.fitab.it (anti-corruption layer)
Fitab.Data    cache-first su file + politiche di aggiornamento
Fitab.App     MAUI Blazor: componenti, pagine, servizi di piattaforma
Fitab.Probe   sonda di contratto contro il backend reale
```

La dipendenza va in una sola direzione: `App → Data → Api → Core`.
Nessun progetto oltre `Fitab.Api` conosce la forma degli endpoint: se il backend
cambia — o viene rifatto — si riscrive quel progetto e basta.

## Comandi

Sviluppo su Windows (iterazione rapida sulla UI):

```bash
dotnet run --project src/Fitab.App -f net10.0-windows10.0.19041.0
```

Deploy su un Android collegato:

```bash
dotnet build src/Fitab.App/Fitab.App.csproj -f net10.0-android -c Release -t:Run
```

APK da presentare:

```bash
dotnet publish src/Fitab.App/Fitab.App.csproj -f net10.0-android -c Release
```

Verifica che i web service rispondano ancora come ci aspettiamo:

```bash
dotnet run --project src/Fitab.Probe
```

## Note operative

- **Package id `it.caleidoscopio.fitab.demo`**, non `com.fitab.evin`: l'app
  pubblicata resta di Evin srl e le due devono poter convivere sullo stesso
  telefono.
- **Target Android e Windows.** iOS e MacCatalyst sono stati tolti dai
  `TargetFrameworks` per accorciare i tempi di build: si riattivano aggiungendoli
  in `Fitab.App.csproj`.
- **Percorsi lunghi.** Su Windows AAPT2 può fallire con `APT2066 failed parsing
  overlays` quando il percorso supera i 260 caratteri. Se succede: `rm -rf
  src/Fitab.App/obj` e ricompilare; la soluzione definitiva è spostare il
  progetto in un percorso corto e senza spazi (es. `C:\dev\Fitab`).
- **Notifiche push**: fuori scope, richiedono il progetto Firebase di Evin.
  L'alternativa è descritta in `API-FITAB.md` §7.

## Marchi e grafica

| Asset | Origine | Uso |
|---|---|---|
| `wwwroot/img/fitab-logo.png` | marchio ufficiale FITAB-APS, a colori | fondi chiari: accesso, contatti |
| `wwwroot/img/fitab-logo-bianco.png` | stessa versione in bianco | fondi colorati: testata, tessera, avvio |
| `wwwroot/img/aics-logo.png` | marchio AICS | sezione affiliazione nei contatti |
| `Resources/AppIcon/*.svg` | ridisegnati da noi | icona adattiva Android |
| `Resources/Splash/splash.svg` | ridisegnato da noi | schermata di avvio |
| `Components/Comuni/MotivoSemi.razor` | disegno originale | filigrana della testata |

I marchi sono estratti dall'APK ufficiale e **appartengono alla federazione e ad
AICS**, non a noi e nemmeno a Evin srl. Usarli in un APK dimostrativo presentato
alla federazione stessa e' legittimo; per una eventuale pubblicazione serve
l'autorizzazione scritta di FITAB. Le due versioni del logo non vanno mai
ricolorate, deformate o usate come texture: per la decorazione c'e' il motivo
con i semi delle carte, che e' roba nostra.

Icona e splash **non** riproducono l'artwork altrui: sono una stella a cinque
punte inscritta in un cerchio, ridisegnata in SVG, che richiama il simbolo del
marchio restando una forma geometrica generica.

### Palette

Il colore principale e' il **blu del marchio, `#0055A3`**, campionato dal logo
ufficiale FITAB-APS. Il verde dell'app attuale non era un colore istituzionale:
era una scelta dello sviluppatore precedente.

| Token | Valore | Ruolo |
|---|---|---|
| `--primario` | `#0055A3` | interfaccia: barre, azioni, evidenziazioni |
| `--tessera-da` / `--tessera-a` | `#00305E` → `#0055A3` | tessera socio, piu' profonda per staccare dalle schede |
| `--positivo` | `#1B9A3D` | guadagno: posizioni risalite, punti in crescita |
| `--negativo` | `#C62828` | perdita |

Il verde non e' stato buttato: e' diventato il colore semantico del guadagno,
dove nessun altro colore funzionerebbe. Le frecce di variazione nelle classifiche
live restano verdi e rosse indipendentemente dal marchio.

Nel tema scuro i token del primario si schiariscono (`#5AA2E0`) e il testo che ci
sta sopra diventa scuro, per non perdere contrasto. Fanno eccezione **testata
della home e schermata di avvio**, che restano al blu istituzionale in entrambi i
temi: portano il marchio, che esiste solo in bianco e a colori, e su azzurro
chiaro il bianco non reggerebbe. La schermata di avvio Blazor usa esattamente il
colore dello splash nativo dichiarato nel csproj, altrimenti si vedrebbe un lampo
nel passaggio fra i due.

Nessuna regola usa piu' i colori grezzi: cambiare tinta significa toccare
`--blu*` in `tokens.css` e nient'altro.

## Comportamenti dell'app

### Accesso persistente

Tessera e codice fiscale restano nell'archivio cifrato del sistema (Keystore su
Android, Portachiavi su iOS) e vengono ripristinati all'avvio, prima che il
router disegni qualsiasi schermata. I web service FITAB non rilasciano un token:
ogni chiamata personale richiede di nuovo le credenziali, quindi conservarle non
e' una comodita' ma un requisito.

Attenzione: **disinstallare l'app cancella l'archivio**. Aggiornare con
`adb install -r` lo preserva, disinstallare e reinstallare no. Se durante i test
"l'accesso non viene ricordato", la prima cosa da verificare e' se in mezzo c'e'
stata una disinstallazione.

### Tema chiaro/scuro

Tre scelte in *Profilo → Aspetto*: Automatico (segue il telefono), Chiaro, Scuro.
La preferenza sta nelle `Preferences` e scrive l'attributo `data-tema` sulla
radice del documento; i token in `tokens.css` fanno il resto.

Il marchio non si ricolora: esiste in due versioni ufficiali e il componente
`LogoFitab` mostra quella giusta per il tema. Sul tema scuro la versione a
colori, che ha il wordmark nero, sarebbe illeggibile.

### Precaricamento delle classifiche

All'avvio parte `Precaricamento`, che scarica in background, **una richiesta alla
volta e con due secondi di pausa**:

1. la classifica del circolo del tesserato, se autenticato;
2. le prime 4 pagine della nazionale (i primi 200);
3. le classifiche di tutti i circoli affiliati (~77, meno di un secondo l'una).

Salta tutto cio' che e' gia' in cache e ancora valido, quindi dal secondo avvio
della giornata non fa traffico. Occupa circa 600 KB.

Perche' non l'intera nazionale: sono oltre 6.000 tesserati, cioe' ~120 pagine da
24 secondi — quasi un'ora di richieste. Le pagine oltre le prime restano su
richiesta, e per la domanda che l'utente si pone davvero ("a che punto sono?")
c'e' l'endpoint dedicato, immediato.

## Cose da sapere sul backend

Sono tutte gestite dentro `Fitab.Api`, ma è utile conoscerle:

- i parametri viaggiano negli **header HTTP**, non in query string;
- tutte le chiamate sono **GET**;
- la risposta ha un unico nodo array con nome variabile per endpoint: lo
  individuiamo per forma, non per nome;
- i booleani arrivano in tre formati diversi (`"S"`/`"N"`, `1`/`0`, `-1`/`0`);
- la **classifica nazionale costa ~24 secondi a pagina**, ma filtrata per
  circolo risponde in meno di un secondo: da qui la cache a 24 ore e il
  precaricamento in background delle pagine successive.

## Compilare per iOS

Il progetto nasce Android + Windows. Per iOS serve riattivare il target e, come
requisito non negoziabile, **una macchina macOS**: la catena di Apple (Xcode,
`codesign`, `actool`) non esiste su Windows.

### 1. Riattivare il target

In `Fitab.App.csproj`:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
```

### 2. Procurarsi un Mac (tre strade)

| Strada | Cosa serve | Quando ha senso |
|---|---|---|
| **Mac in locale** | un Mac con Xcode + workload `ios` | la piu' semplice se ce l'hai |
| **Pair to Mac** | Visual Studio su Windows + un Mac in rete con Xcode e `Remote Login` attivo | si continua a sviluppare su Windows, la build gira sul Mac via SSH |
| **macOS a noleggio** | MacStadium, MacinCloud, Scaleway Mac mini, oppure i runner macOS di GitHub Actions / Azure Pipelines | build occasionali e CI, senza comprare hardware |

**Hot Restart** merita una nota: permette di installare una build **di debug** su un
iPhone fisico direttamente da Windows, senza Mac. Basta per far girare la demo su
un iPhone, ma **non produce un `.ipa`** e non serve per distribuire.

### 3. Account Apple

- **Apple Developer Program**, 99 $/anno, intestato alla federazione se l'app sara'
  sua. Serve per installare su dispositivi e per TestFlight.
- Con un Apple ID gratuito si installa su un proprio dispositivo, ma la firma
  **scade dopo 7 giorni** e va rifatta: inutilizzabile per una demo lasciata in mano
  a qualcuno.

### 4. Comandi

Simulatore:

```bash
dotnet build src/Fitab.App/Fitab.App.csproj -f net10.0-ios -t:Run -p:_DeviceName=:v2:udid=<UDID-SIMULATORE>
```

Archivio firmato per la distribuzione (sul Mac):

```bash
dotnet publish src/Fitab.App/Fitab.App.csproj -f net10.0-ios -c Release -p:ArchiveOnBuild=true -p:CodesignKey="Apple Distribution: Nome (TEAMID)" -p:CodesignProvision="Nome del provisioning profile"
```

Il `.ipa` esce in `bin/Release/net10.0-ios/ios-arm64/publish/`.

### 5. Distribuire la demo su iOS

Su iOS non esiste l'equivalente dell'APK da passare via chat: o **TestFlight**
(tester interni subito, esterni dopo una revisione Apple) o **ad hoc**, con gli
UDID dei dispositivi registrati uno per uno nel provisioning profile. Per una
presentazione a poche persone, ad hoc e' piu' rapido.

### 6. Cosa cambia nel nostro codice

Poco, ma non zero:

- **Mappe**: `geo:` e' uno schema solo Android. `AperturaEsterna.CercaSuMappaAsync`
  sceglie gia' lo schema per piattaforma (`maps.apple.com` su iOS, `bingmaps:` su
  Windows). Era un bug latente, sistemato.
- **ATS (App Transport Security)**: iOS blocca il traffico in chiaro. Noi parliamo
  con `https://cms.fitab.it`, quindi siamo a posto — se fossimo rimasti su `http://`
  come l'app attuale, servirebbe un'eccezione in `Info.plist`, che Apple contesta
  in revisione.
- **SecureStorage** usa il Portachiavi: funziona senza configurazione, ma se un
  domani servisse condividerlo con un'estensione va aggiunto un `Entitlements.plist`.
- **Icona**: iOS non ammette trasparenza. MAUI compone il primo piano sul colore di
  sfondo dichiarato in `MauiIcon`, quindi il nostro simbolo funziona anche li'.
- **Aree sicure**: il CSS usa gia' `env(safe-area-inset-*)`, che e' proprio quello
  che serve per il notch e la barra gestuale.
