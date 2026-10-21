using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DaP.Convertitore.App;

/// <summary>
/// Il ponte fra l'interfaccia (JavaScript in WebView2) e il motore. Le domande arrivano come {id, cmd, args} e la
/// risposta torna con lo stesso id; le notizie (file nuovi, avanzamento, carico) partono da qui come {evento, dati}.
/// </summary>
public sealed class Ponte
{
    readonly Finestra finestra;
    readonly WebView2 vista;
    readonly Strumenti strumenti = Strumenti.Trova();
    readonly Analisi analisi;
    readonly Impostazioni imp = Impostazioni.Comune;
    readonly ConcurrentDictionary<string, VoceFile> file = new();
    readonly List<string> ordine = [];
    readonly SemaphoreSlim anteprime = new(3);
    // i file per lo scrub dell'anteprima (video e brani suonati dalla pagina): su https://dap.file/, a gettone
    readonly Risorse risorse;
    readonly Dictionary<string, List<string>> rapide = [];
    readonly DispatcherTimer timerRapide;
    readonly DispatcherTimer timerCarico;
    InfoHardware? hardware;
    Task<InfoHardware>? rilevazione;
    Monitor? monitor;
    // la stampante arriva quando WebView2 è pronto; un lavoro che parte prima la aspetta
    readonly TaskCompletionSource<IStampante> stampante = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool pronta;
    readonly HashSet<string> giro = [];

    public Coda Coda { get; }

    static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    sealed class VoceFile(InfoFile info)
    {
        public InfoFile Info { get; } = info;
        public Dictionary<string, object?>? Dettagli { get; set; }
        public string? Anteprima { get; set; }
    }

    public Ponte(Finestra finestra, WebView2 vista)
    {
        this.finestra = finestra;
        this.vista = vista;
        analisi = new Analisi(strumenti);
        risorse = new Risorse(finestra.Dispatcher);
        Processi.Priorita = imp.AlMassimo ? ProcessPriorityClass.Normal : ProcessPriorityClass.BelowNormal;
        rilevazione = Task.Run(() => Hardware.Rileva(strumenti));
        Coda = new Coda(strumenti, () => hardware ?? rilevazione.GetAwaiter().GetResult(), () => new StampanteInAttesa(stampante.Task), imp.ScrittaPulita, () => imp.CestinoDaSolo);
        Coda.Cambiato += l => finestra.Dispatcher.BeginInvoke(() => LavoroCambiato(l));
        timerRapide = new DispatcherTimer(TimeSpan.FromMilliseconds(450), DispatcherPriority.Normal, (_, _) => PartonoRapide(), finestra.Dispatcher) { IsEnabled = false };
        timerCarico = new DispatcherTimer(TimeSpan.FromMilliseconds(700), DispatcherPriority.Background, (_, _) => Carico(), finestra.Dispatcher) { IsEnabled = false };
        _ = rilevazione.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) hardware = t.Result;
            finestra.Dispatcher.BeginInvoke(() => Manda("hardware", HardwareDto()));
        });
    }

    public void Collega(CoreWebView2Environment env)
    {
        stampante.TrySetResult(new Stampante(env, finestra));
        risorse.Collega(vista.CoreWebView2, env);
        vista.CoreWebView2.WebMessageReceived += Ricevi;
    }

    public void Chiudi()
    {
        timerCarico.Stop();
        risorse.Ferma();
        if (avvisoAgg is not null) Aggiornamenti.Trovata -= avvisoAgg;
        monitor?.Dispose();
    }

    Action<string>? avvisoAgg;
    CancellationTokenSource? stima;

    // ————————————————————————— richieste da fuori (riga di comando, altre istanze) —————————————————————————

    public void Gestisci(Richiesta r)
    {
        var ids = r.Percorsi.Select(Aggiungi).ToList();
        if (r.Azione is { } azione && Catalogo.TrovaRapida(azione) is not null)
        {
            if (!rapide.TryGetValue(azione, out var l)) rapide[azione] = l = [];
            l.AddRange(ids);
            // Esplora file manda i file uno per processo: si aspetta un attimo che arrivino tutti
            timerRapide.Stop();
            timerRapide.Start();
        }
        if (r.Finestra && finestra.Modo == "rapido") CambiaModo("finestra");
        if (finestra.WindowState == WindowState.Minimized) finestra.WindowState = WindowState.Normal;
        finestra.Activate();
    }

    void CambiaModo(string modo)
    {
        if (finestra.Modo == modo) return;
        finestra.Misura(modo);
        Manda("modo", new { modo });
    }

    void PartonoRapide()
    {
        timerRapide.Stop();
        foreach (var (azione, ids) in rapide)
        {
            var r = Catalogo.TrovaRapida(azione)!;
            var f = Catalogo.Trova(r.Formato)!;
            var adatti = ids.Select(i => file.TryGetValue(i, out var v) ? v : null).Where(v => v is not null && v.Info.Formati.Contains(f.Id)).ToList();
            if (adatti.Count == 0) continue;
            // Esplora file non dice in che ordine erano: si mettono in ordine di nome, come li mostra lui (1, 2, 10)
            adatti.Sort((a, b) => StrCmpLogicalW(a!.Info.Nome, b!.Info.Nome));
            if (f.Unisce) Avvia(adatti.Select(v => v!.Info.Percorso).ToList(), f, Opzioni.DaPreset(r.Preset, adatti[0]!.Info.Peso), adatti[0]!.Info.Id);
            else foreach (var v in adatti) Avvia([v!.Info.Percorso], f, Opzioni.DaPreset(r.Preset, v.Info.Peso), v.Info.Id);
        }
        rapide.Clear();
    }

    Lavoro Avvia(IReadOnlyList<string> sorgenti, Formato f, Opzioni o, string? fileId)
    {
        // le scelte salvate valgono anche per il menu rapido (codec, motore…), il preset vince
        var l = Coda.Aggiungi(sorgenti, f, o, fileId);
        giro.Add(l.Id);
        return l;
    }

    string Aggiungi(string percorso)
    {
        var gia = file.Values.FirstOrDefault(v => string.Equals(v.Info.Percorso, percorso, StringComparison.OrdinalIgnoreCase));
        if (gia is not null) return gia.Info.Id;
        var info = analisi.Primo(percorso);
        var voce = new VoceFile(info);
        file[info.Id] = voce;
        lock (ordine) ordine.Add(info.Id);
        Manda("file", FileDto(voce));
        _ = Task.Run(async () =>
        {
            var d = await analisi.Dettagli(info);
            voce.Dettagli = d;
            Manda("dettagli", new { id = info.Id, dettagli = d });
            await anteprime.WaitAsync();
            try
            {
                var durata = d.TryGetValue("durata", out var x) && x is double dd ? dd : 0;
                var a = await Anteprime.Fai(percorso, Catalogo.CategoriaDi(percorso), strumenti, durata);
                if (a is null) return;
                voce.Anteprima = a.DataUrl;
                Manda("anteprima", new { id = info.Id, url = voce.Anteprima });
            }
            finally { anteprime.Release(); }
        });
        return info.Id;
    }

    /// <summary>La durata dai dettagli del file; se l'analisi non è ancora finita (la colonna dell'anteprima la chiede subito) la aspetta un po'.</summary>
    static async Task<double> DurataDi(VoceFile v)
    {
        for (var i = 0; i < 100 && v.Dettagli is null; i++) await Task.Delay(50);
        return v.Dettagli is not null && v.Dettagli.TryGetValue("durata", out var x) && x is double d ? d : 0;
    }

    // ————————————————————————— messaggi dall'interfaccia —————————————————————————

    async void Ricevi(object? _, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonNode? m;
        try { m = JsonNode.Parse(e.WebMessageAsJson); }
        catch { return; }
        if (m is null) return;
        var cmd = (string?)m["cmd"] ?? "";
        var id = m["id"]?.GetValue<int>();
        var args = m["args"] as JsonObject ?? [];

        if (cmd == "lasciati")
        {
            // i file trascinati dentro: WebView2 ci dà i percorsi veri
            var percorsi = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(f => f.Path).ToList() ?? [];
            Gestisci(new Richiesta(null, percorsi, true));
            return;
        }
        try
        {
            var dati = await Esegui(cmd, args);
            if (id is not null) Rispondi(id.Value, true, dati, null);
        }
        catch (Exception ex)
        {
            Registro.Errore($"ponte {cmd}", ex);
            if (id is not null) Rispondi(id.Value, false, null, ex.Message);
        }
    }

    async Task<object?> Esegui(string cmd, JsonObject a)
    {
        switch (cmd)
        {
            case "stato":
                if (!pronta)
                {
                    avvisoAgg = v => Manda("aggiornamento", new { versione = v });
                    Aggiornamenti.Trovata += avvisoAgg;
                }
                pronta = true;
                timerCarico.Start();
                return new
                {
                    versione = Versione,
                    hardware = HardwareDto(),
                    formati = Catalogo.Formati.Select(f => new { f.Id, categoria = Catalogo.Chiave(f.Categoria), f.Etichetta, f.Descrizione, f.Estensione, f.Unisce, f.Sostituisce, manca = Conversioni.Manca(f, strumenti) }),
                    categorie = Enum.GetValues<Categoria>().Select(c => new { id = Catalogo.Chiave(c), nome = Catalogo.NomeCategoria(c) }),
                    impostazioni = imp,
                    apriConGruppi = ApriCon.Elenco().Select(g => new { g.Id, g.Nome, g.Esempi }),
                    regolaPredefiniti = new { supportata = Predefiniti.Supportata(), attiva = Predefiniti.Attiva(), diAltri = Predefiniti.DiAltri() },
                    aggiornamento = Aggiornamenti.Disponibile,
                    modo = finestra.Modo,
                    file = Ordinati().Select(FileDto),
                    lavori = Coda.Tutti.Where(l => giro.Contains(l.Id)).Select(LavoroDto),
                    office = new { libreOffice = strumenti.LibreOffice is not null, word = strumenti.Word },
                    menu11 = StatoMenu11(),
                };
            case "pronto":
                return true;
            case "scegli":
                Scegli();
                return true;
            case "incolla":
            {
                // i file copiati in Esplora file (Ctrl+C) entrano nella coda, come se fossero trascinati
                var elenco = Clipboard.ContainsFileDropList() ? Clipboard.GetFileDropList().Cast<string>().Where(p => File.Exists(p) || Directory.Exists(p)).ToList() : [];
                if (elenco.Count > 0) Gestisci(new Richiesta(null, elenco, true));
                return elenco.Count;
            }
            case "svuota":
                // la coda si svuota tutta: i file restano dove sono, si tolgono solo dalla lista
                file.Clear();
                lock (ordine) ordine.Clear();
                return true;
            case "togli":
            {
                var id = (string?)a["id"];
                if (id is not null && file.TryRemove(id, out _)) lock (ordine) ordine.Remove(id);
                return true;
            }
            case "piano":
            {
                var v = file[(string)a["id"]!];
                var f = Catalogo.Trova((string)a["formato"]!)!;
                var o = Opzioni.Da(JsonSerializer.SerializeToElement(a["opzioni"]));
                var info = await analisi.LeggiMedia(v.Info.Percorso) ?? throw new ErroreConversione("Il video non si legge.");
                var hw = hardware ?? await rilevazione!;
                var p = PianoVideo.Calcola(info, v.Info.Peso, v.Info.Estensione, f.Estensione, o.Video, hw);
                return new
                {
                    p.Modo, p.Codec, p.Encoder, p.Hardware, p.Larghezza, p.Altezza, p.Fps, p.BitrateVideo, p.AudioKbps,
                    p.Punteggio, p.Giudizio, p.PesoStimato, p.PesoMin, p.PesoMax, p.PuoCopiare, p.Note,
                    codecAmmessi = PianoVideo.CodecPer(f.Estensione, hw),
                };
            }
            case "stimaImmagine":
            {
                // quanto peserà la foto (e a che qualità ci si arriva): una stima alla volta, la nuova ferma la vecchia
                var v = file[(string)a["id"]!];
                var f = Catalogo.Trova((string)a["formato"]!)!;
                var mia = new CancellationTokenSource();
                Interlocked.Exchange(ref stima, mia)?.Cancel();
                var s = await StimaImmagini.Fai(strumenti, v.Info.Percorso, f.Estensione, a["lato"]?.GetValue<int>() ?? 0, a["qualita"]?.GetValue<int>() ?? 88,
                    a["pesoMax"]?.GetValue<long>() ?? 0, a["intervallo"]?.GetValue<bool>() == true, mia.Token);
                return new { s.Peso, s.Qualita, s.Scala, s.Min, s.Max, s.Larghezza, s.Altezza, s.Esatta };
            }
            case "converti":
            {
                var ids = new List<string>();
                foreach (var el in (a["elementi"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var f = Catalogo.Trova((string)el["formato"]!);
                    if (f is null) continue;
                    var o = Opzioni.Da(JsonSerializer.SerializeToElement(el["opzioni"]));
                    var voci = (el["ids"] as JsonArray ?? []).Select(x => (string?)x).Where(x => x is not null && file.ContainsKey(x)).Select(x => file[x!]).ToList();
                    if (voci.Count == 0) continue;
                    if (f.Unisce) ids.Add(Avvia(voci.Select(v => v.Info.Percorso).ToList(), f, o, voci[0].Info.Id).Id);
                    else foreach (var v in voci) ids.Add(Avvia([v.Info.Percorso], f, o, v.Info.Id).Id);
                }
                return ids;
            }
            case "annulla":
                if ((string?)a["id"] is { } lid) Coda.Annulla(lid); else Coda.AnnullaTutto();
                return true;
            case "pulisci":
                Coda.TogliFiniti();
                giro.Clear();
                finestra.Avanzamento(null);
                return true;
            case "apri":
            {
                var p = (string?)a["percorso"];
                if (p is not null && (File.Exists(p) || Directory.Exists(p))) Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                return true;
            }
            case "mostra":
            {
                var p = (string?)a["percorso"];
                if (p is not null && (File.Exists(p) || Directory.Exists(p))) Programma.Mostra(p);
                return true;
            }
            case "confronta":
            {
                var l = Coda.Tutti.FirstOrDefault(x => x.Id == (string?)a["lavoro"]) ?? throw new ErroreConversione("Lavoro sparito.");
                var pos = a["posizione"]?.GetValue<double>() ?? 0.35;
                return await Confronto.Fai(l, pos, strumenti);
            }
            case "finestra":
                switch ((string?)a["azione"])
                {
                    case "chiudi": finestra.Close(); break;
                    case "riduci": finestra.WindowState = WindowState.Minimized; break;
                    case "massimizza": finestra.Massimizza(); break;
                    case "espandi": CambiaModo("finestra"); break;
                }
                return true;
            case "impostazioni":
            {
                if (a["scritta"] is { } s) imp.Scritta = (string?)s ?? "convertito";
                if (a["menu"] is { } m) imp.Menu = m.GetValue<bool>();
                if (a["alMassimo"] is { } am) { imp.AlMassimo = am.GetValue<bool>(); Processi.Priorita = imp.AlMassimo ? ProcessPriorityClass.Normal : ProcessPriorityClass.BelowNormal; }
                if (a["suoni"] is { } su) imp.Suoni = su.GetValue<bool>();
                if (a["apriCartella"] is { } ac) imp.ApriCartella = ac.GetValue<bool>();
                if (a["cestinoDaSolo"] is { } ce) imp.CestinoDaSolo = ce.GetValue<bool>();
                if (a["apriCon"] is { } ap)
                {
                    imp.ApriCon = ap.GetValue<bool>();
                    if (imp.ApriCon) ApriCon.Registra(Environment.ProcessPath!); else ApriCon.Rimuovi();
                }
                if (a["formati"] is JsonObject fo) imp.Formati = fo.ToDictionary(kv => kv.Key, kv => (string?)kv.Value ?? "");
                if (a["scelte"] is JsonObject sc) imp.Scelte = (JsonObject)sc.DeepClone();
                imp.Salva();
                return true;
            }
            case "cestino":
            {
                // il tasto accanto al risultato: l'originale nel Cestino, solo quando lo chiedi
                var perche = Coda.OriginaleNelCestino((string?)a["lavoro"] ?? "");
                return new { ok = perche is null, perche };
            }
            case "guarda":
            {
                var p = (string?)a["percorso"];
                if (p is not null && File.Exists(p)) Regia.ApriNelLettore(p);
                return true;
            }
            case "anteprimaTesto":
            {
                // il pannello a destra: il testo del file scelto nella coda, con la codifica in cui è scritto
                var v = file.TryGetValue((string?)a["id"] ?? "", out var x) ? x : throw new ErroreConversione("Il file non è più nella coda.");
                var t = await Task.Run(() => Codifiche.Leggi(v.Info.Percorso, Lettore.Contenuti.MaxTesto));
                return new { testo = t.Testo, codifica = t.Codifica, bom = t.Bom, troncato = t.Troncato, peso = v.Info.Peso };
            }
            case "indiceAnteprima":
            {
                // la colonna a destra per i file che non sono testo: com'è fatto l'indice (pagine, fotogrammi, onde, una figura sola)
                var v = file.TryGetValue((string?)a["id"] ?? "", out var x) ? x : throw new ErroreConversione("Il file non è più nella coda.");
                var i = await IndiceAnteprima.Descrivi(v.Info.Percorso, Catalogo.CategoriaDi(v.Info.Percorso), await DurataDi(v));
                return new { tipo = i.Tipo, voci = i.Voci, totale = i.Totale, punti = Enumerable.Range(0, i.Voci).Select(n => IndiceAnteprima.Punto(i, n)).ToArray() };
            }
            case "fonteAnteprima":
            {
                // lo scrub: il file vero per il tag <video>/<audio> della pagina (gettone, mai il percorso)
                var v = file.TryGetValue((string?)a["id"] ?? "", out var x) ? x : throw new ErroreConversione("Il file non è più nella coda.");
                return new { url = File.Exists(v.Info.Percorso) ? risorse.Url(v.Info.Percorso) : null };
            }
            case "scrubAnteprima":
            {
                // il fotogramma del video a quel secondo, o l'onda intera del brano (quando la pagina non sa suonare il file)
                var v = file.TryGetValue((string?)a["id"] ?? "", out var x) ? x : throw new ErroreConversione("Il file non è più nella coda.");
                var c = Catalogo.CategoriaDi(v.Info.Percorso);
                var durata = await DurataDi(v);
                var lato = (uint)Math.Clamp((int?)a["lato"] ?? 480, 48, 1024);
                await anteprime.WaitAsync();
                try
                {
                    var r = c == Categoria.Video ? await IndiceAnteprima.Fotogramma(v.Info.Percorso, (double?)a["t"] ?? 0, durata, strumenti, lato)
                        : c == Categoria.Audio ? await IndiceAnteprima.OndaIntera(v.Info.Percorso, durata, strumenti, lato) : null;
                    return new { url = r?.DataUrl };
                }
                finally { anteprime.Release(); }
            }
            case "voceAnteprima":
            {
                var v = file.TryGetValue((string?)a["id"] ?? "", out var x) ? x : throw new ErroreConversione("Il file non è più nella coda.");
                var c = Catalogo.CategoriaDi(v.Info.Percorso);
                var i = await IndiceAnteprima.Descrivi(v.Info.Percorso, c, await DurataDi(v));
                var lato = (uint)Math.Clamp((int?)a["lato"] ?? 160, 48, 1024);
                await anteprime.WaitAsync();
                try
                {
                    var r = await IndiceAnteprima.Voce(v.Info.Percorso, c, i, (int?)a["n"] ?? 0, strumenti, lato);
                    return new { url = r?.DataUrl };
                }
                finally { anteprime.Release(); }
            }
            case "predefinite":
            {
                // la lista scelta nella pagina: si registrano solo quei tipi, e Windows (che le app predefinite le fa scegliere
                // a te) apre la pagina del convertitore già pronta, col clic finale
                if (a["gruppi"] is JsonArray scelti)
                {
                    var gruppi = scelti.Select(x => (string?)x).Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToList();
                    imp.GruppiPredefiniti = gruppi;
                    imp.Salva();
                    imp.ApriCon = true;
                    ApriCon.Registra(Environment.ProcessPath!, gruppi);
                }
                var pagina = () => Process.Start(new ProcessStartInfo($"ms-settings:defaultapps?registeredAppUser={Uri.EscapeDataString(ApriCon.NomeApp)}") { UseShellExecute = true });
                if (a["gruppi"] is not JsonArray || !Predefiniti.Supportata() || Predefiniti.DiAltri()) { pagina(); return new { esito = "pagina" }; }
                // il modo di Windows per gli amministratori: un file di associazioni, applicato all'accesso (serve il permesso)
                var xml = Path.Combine(Path.GetTempPath(), $"dap-associazioni-{Guid.NewGuid():N}.xml");
                await File.WriteAllTextAsync(xml, Predefiniti.Xml(imp.GruppiPredefiniti ?? []), new System.Text.UTF8Encoding(false));
                try
                {
                    var codice = await Predefiniti.ChiediAdmin(Environment.ProcessPath!, $"--predefiniti-applica \"{xml}\"");
                    return new { esito = codice switch { null => "negato", Predefiniti.Fatto => "ok", Predefiniti.GiaUsata => "gia", _ => "errore" } };
                }
                finally { try { File.Delete(xml); } catch { } }
            }
            case "predefiniteTogli":
            {
                var codice = await Predefiniti.ChiediAdmin(Environment.ProcessPath!, "--predefiniti-togli");
                return new { esito = codice switch { null => "negato", Predefiniti.Fatto => "ok", _ => "errore" } };
            }
            case "esplora":
                await Menu11.RiavviaEsplora();
                return true;
            case "classico":
                Menu11.ClassicoOvunque = a["attivo"]?.GetValue<bool>() == true;
                return StatoMenu11();
            case "menu11":
            {
                var cartella = AppContext.BaseDirectory;
                switch ((string?)a["azione"])
                {
                    case "attiva":
                        imp.Menu11Chiesto = true;
                        imp.Salva();
                        // l'unico permesso da amministratore: Windows mostra la sua finestra, una volta sola
                        if (!Menu11.Fidato(cartella) && !await Menu11.RendiFidato(cartella))
                            return new { stato = StatoMenu11(), errore = "Senza il permesso Windows non accetta il menu nuovo. Puoi riprovare quando vuoi." };
                        var e = await Menu11.Registra(cartella);
                        return new { stato = StatoMenu11(), errore = e };
                    case "togli":
                        await Menu11.Rimuovi();
                        return new { stato = StatoMenu11(), errore = (string?)null };
                    default: // "non ora"
                        imp.Menu11Chiesto = true;
                        imp.Salva();
                        return new { stato = StatoMenu11(), errore = (string?)null };
                }
            }
            case "menu":
            {
                if (a["attivo"]?.GetValue<bool>() == true)
                {
                    var exe = Environment.ProcessPath!;
                    MenuContestuale.Registra(exe, Path.Combine(AppContext.BaseDirectory, "icona.png"));
                }
                else MenuContestuale.Rimuovi();
                return true;
            }
            case "aggiorna":
                if (a["installa"]?.GetValue<bool>() == true) { await Aggiornamenti.Installa(); return true; }
                return new { versione = await Aggiornamenti.Controlla() };
            case "link":
            {
                // solo i posti che conosciamo: l'interfaccia non apre indirizzi a caso
                var url = (string?)a["url"] ?? "";
                if (url.StartsWith("https://it.libreoffice.org/") || url.StartsWith("https://github.com/cammo22/DaP-Convertitore"))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            case "registro":
                if (File.Exists(Registro.Percorso)) Process.Start(new ProcessStartInfo(Registro.Percorso) { UseShellExecute = true });
                return true;
        }
        throw new InvalidOperationException($"Comando sconosciuto: {cmd}");
    }

    void Scegli()
    {
        var d = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Scegli i file da convertire" };
        if (d.ShowDialog(finestra) == true) Gestisci(new Richiesta(null, d.FileNames, true));
    }

    // ————————————————————————— notizie verso l'interfaccia —————————————————————————

    void LavoroCambiato(Lavoro l)
    {
        Manda("lavoro", LavoroDto(l));
        var miei = Coda.Tutti.Where(x => giro.Contains(x.Id)).ToList();
        if (miei.Count == 0) return;
        var finiti = miei.All(x => x.Stato is StatoLavoro.Fatto or StatoLavoro.Errore or StatoLavoro.Annullato);
        if (!finiti)
        {
            var corrente = miei.FirstOrDefault(x => x.Stato == StatoLavoro.Corre);
            var totale = miei.Sum(x => x.Stato is StatoLavoro.Fatto or StatoLavoro.Errore or StatoLavoro.Annullato ? 1 : Math.Max(0, x.Frazione)) / miei.Count;
            finestra.Avanzamento(miei.Count == 1 && corrente is { Frazione: < 0 } ? -1 : totale);
        }
        else
        {
            finestra.Avanzamento(miei.Any(x => x.Stato == StatoLavoro.Errore) ? 1 : null, miei.Any(x => x.Stato == StatoLavoro.Errore));
        }
        if (l.Stato == StatoLavoro.Fatto && finiti)
        {
            var ok = miei.Where(x => x.Stato == StatoLavoro.Fatto).ToList();
            if (!finestra.IsActive || finestra.Modo == "rapido") Notifiche.Fatto(ok);
            if (imp.ApriCartella && ok.LastOrDefault()?.Uscita is { } u) Programma.Mostra(u);
        }
        else if (l.Stato == StatoLavoro.Errore && finiti && !finestra.IsActive)
            Notifiche.Errore(l);
    }

    void Carico()
    {
        if (!pronta || finestra.WindowState == WindowState.Minimized) return;
        if (!Coda.Occupata && finestra.Modo == "rapido") return;
        monitor ??= new Monitor();
        var m = monitor;
        _ = Task.Run(() =>
        {
            var c = m.Leggi();
            Manda("carico", new { c.Cpu, c.Encoder, c.Decoder, c.Grafica });
        });
    }

    void Manda(string evento, object? dati)
    {
        if (!pronta) return;
        var testo = JsonSerializer.Serialize(new { evento, dati }, json);
        finestra.Dispatcher.BeginInvoke(() =>
        {
            try { vista.CoreWebView2?.PostWebMessageAsJson(testo); } catch { }
        });
    }

    void Rispondi(int id, bool ok, object? dati, string? errore)
    {
        var testo = JsonSerializer.Serialize(new { id, ok, dati, errore }, json);
        try { vista.CoreWebView2?.PostWebMessageAsJson(testo); } catch { }
    }

    IEnumerable<VoceFile> Ordinati()
    {
        lock (ordine) return ordine.Where(file.ContainsKey).Select(i => file[i]).ToList();
    }

    static object FileDto(VoceFile v) => new
    {
        v.Info.Id, v.Info.Percorso, v.Info.Nome, v.Info.Cartella, v.Info.Estensione, v.Info.Categoria, v.Info.NomeCategoria,
        v.Info.Peso, v.Info.EDirectory, v.Info.Formati, dettagli = v.Dettagli, anteprima = v.Anteprima,
    };

    static object LavoroDto(Lavoro l) => new
    {
        l.Id, l.FileId, l.Sorgenti, formato = l.Formato.Id, etichetta = l.Formato.Etichetta,
        stato = l.Stato.ToString().ToLowerInvariant(),
        frazione = double.IsFinite(l.Frazione) ? l.Frazione : 0,
        l.Fase, l.Velocita, l.Fps, eta = l.Eta is { } e && double.IsFinite(e) ? e : (double?)null,
        l.Uscita, l.PesoPrima, l.PesoDopo, l.Errore, l.Dettaglio, l.Secondi, l.NelCestino, l.NotaCestino,
    };

    object? HardwareDto() => hardware is null ? null : new
    {
        cpu = hardware.Cpu, thread = hardware.Thread,
        gpu = hardware.Gpu.Select(g => new { g.Nome, g.Marca, g.Vram, g.Driver }),
        encoder = hardware.Encoder, acceleratore = hardware.Acceleratore,
    };

    /// <summary>Il menu nuovo di Windows 11: si può avere qui? c'è già? manca solo il permesso?</summary>
    object StatoMenu11()
    {
        var cartella = AppContext.BaseDirectory;
        var supportato = Menu11.Windows11 && Menu11.Presente(cartella);
        return new
        {
            supportato,
            registrato = supportato && Menu11.Registrato(),
            fidato = supportato && Menu11.Fidato(cartella),
            chiesto = imp.Menu11Chiesto,
            windows11 = Menu11.Windows11,
            classico = Menu11.ClassicoOvunque,
        };
    }

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);

    sealed class StampanteInAttesa(Task<IStampante> pronta) : IStampante
    {
        public async Task Stampa(string fileHtml, string pdf, CancellationToken ct) => await (await pronta.WaitAsync(ct)).Stampa(fileHtml, pdf, ct);
        public async Task<string> Testo(string fileHtml, CancellationToken ct) => await (await pronta.WaitAsync(ct)).Testo(fileHtml, ct);
    }

    public static string Versione =>
        typeof(Ponte).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "1.0.0";
}
