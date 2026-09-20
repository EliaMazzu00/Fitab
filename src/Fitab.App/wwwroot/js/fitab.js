// Interop minimo. Tutto il resto dell'app e' C#: qui stanno solo le due cose
// che dal lato .NET non si possono fare.

window.fitab = {

    // Avvisa .NET quando l'app passa in background, cosi' il polling della
    // schermata live si sospende invece di continuare a interrogare il server
    // di un torneo che nessuno sta guardando.
    osservaVisibilita: function (riferimentoDotNet) {
        const gestore = () => {
            riferimentoDotNet.invokeMethodAsync(
                'CambioVisibilita', document.visibilityState === 'visible');
        };

        document.addEventListener('visibilitychange', gestore);

        // Restituisce la funzione di pulizia tramite un id, cosi' la pagina
        // puo' sganciare il listener quando viene chiusa.
        window.fitab._gestori = window.fitab._gestori || {};
        const id = 'v' + Date.now() + Math.random().toString(36).slice(2, 7);
        window.fitab._gestori[id] = gestore;
        return id;
    },

    dimenticaVisibilita: function (id) {
        const gestori = window.fitab._gestori || {};
        if (gestori[id]) {
            document.removeEventListener('visibilitychange', gestori[id]);
            delete gestori[id];
        }
    }
};

// Tema scelto dall'utente. Il CSS reagisce all'attributo data-tema su <html>:
// assente = si segue il sistema.
window.fitab.impostaTema = function (tema) {
    const radice = document.documentElement;
    if (tema === 'chiaro' || tema === 'scuro') {
        radice.setAttribute('data-tema', tema);
    } else {
        radice.removeAttribute('data-tema');
    }
};
