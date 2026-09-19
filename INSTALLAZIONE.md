# Installare la demo di Fitab 2.0

L'app non è sugli store: è una dimostrazione, e va installata a mano. Android è
semplice, iOS molto meno — Apple non permette di installare app fuori dall'App
Store senza firmarle con un account sviluppatore.

L'app usa l'identificativo `it.mazzuchelli.fitab.demo`, diverso da quello
dell'app ufficiale (`com.fitab.evin`): **convivono sullo stesso telefono**, questa
non sostituisce quella e non ne tocca i dati.

---

## Android

### Come si fa

1. Porta il file `.apk` sul telefono: scaricalo da un link, mandatelo via
   Drive/WhatsApp, o passalo via cavo.
2. Aprilo dal gestore file o dalle notifiche di download.
3. Android chiede il permesso di installare app da quella sorgente: è una
   richiesta una tantum per l'app da cui stai aprendo il file (il browser, o
   "File"). Concedilo e torna indietro.
4. Tocca **Installa**.

Se compare un avviso di Play Protect, è normale: segnala qualunque app non
distribuita dal Play Store. Si prosegue con "Installa comunque".

### Da sapere

Il pacchetto è **firmato con la chiave di debug** della macchina che l'ha
compilato. Per un'installazione manuale va benissimo, ma ha una conseguenza
pratica: Android aggiorna un'app solo se il nuovo pacchetto è firmato con la
**stessa** chiave. Un APK compilato su un altro PC verrà rifiutato con "App non
installata", e per metterlo bisogna prima disinstallare quello vecchio, perdendo
i dati locali.

Finché le build escono sempre dalla stessa macchina non è un problema. Se la
demo dovesse girare a più persone per più versioni, conviene generare una
keystore dedicata e conservarla.

---

## iOS

Qui non esiste una strada comoda quanto Android. Un `.ipa` non firmato va
**rifirmato sul proprio computer** con un Apple ID, e con un account gratuito la
firma **scade dopo 7 giorni**: passata quella settimana l'app non si apre più
finché non la si rinnova. Un account gratuito permette inoltre al massimo 3 app
sideloadate contemporaneamente.

Tre strade, dalla più comoda alla più spartana.

### 1. TestFlight — la migliore se la demo deve circolare

Richiede un account Apple Developer a pagamento (99 $/anno). In cambio: niente
cavo, niente scadenza a 7 giorni, fino a 10.000 tester che installano da un
semplice link, e aggiornamenti che arrivano da soli. Se Fitab 2.0 deve essere
provata da persone della federazione, è l'unica strada che non richiede a loro
alcuna competenza tecnica.

Le build restano valide 90 giorni.

### 2. AltStore o SideStore — la migliore con un Apple ID gratuito

Si installa AltStore sull'iPhone tramite AltServer sul PC, poi si apre il `.ipa`
da lì. Il vantaggio è il rinnovo: finché il telefono e il PC sono sulla stessa
rete Wi-Fi, AltServer **rifirma l'app da solo** prima che scadano i 7 giorni, e
l'app continua a funzionare senza interventi.

SideStore fa la stessa cosa senza bisogno che il PC resti acceso, appoggiandosi
a un server anisette: più scomodo da configurare la prima volta, più autonomo
dopo.

### 3. Sideloadly — la più rapida per una prova al volo

Si collega l'iPhone al PC via cavo, si trascina il `.ipa` in Sideloadly, si
inserisce l'Apple ID e si installa. È la via più diretta, ma il rinnovo è
manuale: ogni 7 giorni va ricollegato il telefono e reinstallata l'app.

Va bene per far vedere l'app a qualcuno; non per usarla durante una stagione.

### Dopo l'installazione

Alla prima apertura iOS dice che lo sviluppatore non è attendibile. Va
autorizzato una volta da **Impostazioni → Generali → VPN e gestione dispositivo**,
toccando il proprio Apple ID e poi "Autorizza".

---

## Dove si prendono i pacchetti

**Android.** Si compila in locale:

```bash
dotnet publish src/Fitab.App/Fitab.App.csproj -f net10.0-android -c Release
```

L'APK esce in `src/Fitab.App/bin/Release/net10.0-android/publish/` come
`it.mazzuchelli.fitab.demo-Signed.apk`.

**iOS.** Serve un Mac, quindi la build gira su Codemagic: si avvia a mano dalla
dashboard il workflow descritto in [codemagic.yaml](codemagic.yaml), che produce
una `.ipa` non firmata come artefatto. Sul piano gratuito gli artefatti **scadono
dopo 30 giorni**: se la build serve ancora, va scaricata e conservata altrove.

I pacchetti non stanno nel repository — sono artefatti, non sorgenti, e `.gitignore`
li esclude. Per distribuirli si usano le Release di GitHub, che non finiscono nel
clone di chi scarica il codice.
