using System.Text.Json;
using System.Text.Json.Nodes;
using Photino.NET;

namespace DaP.Convertitore.Linux;

/// <summary>
/// Le stesse domande dell'interfaccia di Windows ({id, cmd, args}), risposte nello stesso formato.
/// Quello che su Windows è il menu, l'avatar o l'OCR qui risponde in chiaro che non c'è.
/// </summary>
sealed class PonteLinux
{
    readonly string versione;
    readonly Strumenti strumenti = Strumenti.Trova();
    readonly Impostazioni imp = Impostazioni.Comune;
    readonly Analisi analisi;
    readonly Coda coda;
    readonly Dictionary<string, Voce> file = new(StringComparer.Ordinal);
    readonly List<string> ordine = [];
    readonly object lucchetto = new();
    PhotinoWindow? finestra;
    InfoHardware? hardware;
    readonly Task<InfoHardware> rilevazione;
    string? aggiornamento;

    static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    sealed class Voce(InfoFile info) { public InfoFile Info { get; } = info; public Dictionary<string, object?>? Dettagli { get; set; } }

    public PonteLinux(string versione, IReadOnlyList<string> gia)
    {
        this.versione = versione;
        analisi = new Analisi(strumenti);
        rilevazione = Task.Run(() => Hardware.Rileva(strumenti));
        coda = new Coda(strumenti, () => hardware ?? rilevazione.GetAwaiter().GetResult(), () => new SenzaStampante(), () => imp.ScrittaPulita(), () => imp.CestinoDaSolo)
        { QuandoFatto = Storico.Aggiungi };
        coda.Cambiato += l => Manda("lavoro", LavoroDto(l));
        _ = rilevazione.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully) return;
            hardware = t.Result;
            Manda("hardware", HardwareDto());
        });
        foreach (var p in gia) Aggiungi(p);
        _ = CercaAggiornamento();
    }

    public void Collega(PhotinoWindow w) => finestra = w;

    public async void Ricevi(string messaggio)
    {
        JsonNode? m;
        try { m = JsonNode.Parse(messaggio); }
        catch { return; }
        if (m is null) return;
        var cmd = (string?)m["cmd"] ?? "";
        var id = m["id"]?.GetValue<int>();
        var args = m["args"] as JsonObject ?? [];
        try
        {
            var dati = await Esegui(cmd, args);
            if (id is not null) Rispondi(id.Value, true, dati, null);
        }
        catch (Exception ex) when (!EccezioniPonte.DaRegistro(ex))
        {
            if (id is not null) Rispondi(id.Value, false, null, null);
        }
        catch (Exception ex)
        {
            Registro.Errore($"ponte {cmd}", ex);
            if (id is not null) Rispondi(id.Value, false, null, ex is ErroreConversione ? ex.Message : "Qualcosa è andato storto.");
        }
    }

    async Task<object?> Esegui(string cmd, JsonObject a)
    {
        switch (cmd)
        {
            case "stato":
                return new
                {
                    versione,
                    hardware = HardwareDto(),
                    formati = Catalogo.Formati.Select(f => new { f.Id, categoria = Catalogo.Chiave(f.Categoria), f.Etichetta, f.Descrizione, f.Estensione, f.Unisce, f.Sostituisce, manca = Conversioni.Manca(f, strumenti) }),
                    categorie = Enum.GetValues<Categoria>().Select(c => new { id = Catalogo.Chiave(c), nome = Catalogo.NomeCategoria(c) }),
                    impostazioni = imp,
                    apriConGruppi = ApriCon.Elenco().Select(g => new { g.Id, g.Nome, g.Esempi }),
                    regolaPredefiniti = new { supportata = false, attiva = false, diAltri = false, impostati = 0, totale = 0, inAttesa = false },
                    aggiornamento,
                    modo = "finestra",
                    file = Ordinati().Select(FileDto),
                    lavori = coda.Tutti.Select(LavoroDto),
                    office = new { libreOffice = strumenti.LibreOffice is not null, word = false, setteZip = strumenti.HaSetteZip },
                    menu11 = new { supportato = false, registrato = false, fidato = false, chiesto = true, windows11 = false, classico = false },
                    compatto = false,
                    scelta = (object?)null,
                };
            case "pronto":
                return true;
            case "scegli":
                foreach (var p in await ScegliFile()) Aggiungi(p);
                return true;
            case "storico":
            {
                var (recenti, totali) = Storico.Recenti(a["quanti"]?.GetValue<int>() ?? 6);
                return new { recenti = recenti.Select(v => new { v.Quando, v.Nome, v.Formato, v.Uscita, v.PesoPrima, v.PesoDopo, v.Categoria }), totali };
            }
            case "svuota":
                lock (lucchetto) { file.Clear(); ordine.Clear(); }
                return true;
            case "togli":
            {
                var id = (string?)a["id"];
                if (id is not null) lock (lucchetto) { file.Remove(id); ordine.Remove(id); }
                return true;
            }
            case "piano":
            {
                var v = Prendi((string)a["id"]!);
                var f = Catalogo.Trova((string)a["formato"]!) ?? throw new ErroreConversione("Formato sconosciuto.");
                var o = Opzioni.Da(JsonSerializer.SerializeToElement(a["opzioni"]));
                var info = await analisi.LeggiMedia(v.Info.Percorso) ?? throw new ErroreConversione("Il video non si legge.");
                var hw = hardware ?? await rilevazione;
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
                var v = Prendi((string)a["id"]!);
                var f = Catalogo.Trova((string)a["formato"]!) ?? throw new ErroreConversione("Formato sconosciuto.");
                var lato = a["lato"]?.GetValue<int>() ?? 0;
                var qualita = a["qualita"]?.GetValue<int>() ?? 88;
                var pesoMax = a["pesoMax"]?.GetValue<long>() ?? 0;
                var s = await Immagini.Stima(v.Info.Percorso, f.Estensione, lato, qualita, pesoMax, CancellationToken.None);
                return new { peso = s.Peso, qualita = s.Qualita, scala = 1.0, min = Math.Max(1, s.Peso / 4), max = Math.Max(s.Peso, s.Peso * 2), larghezza = s.Larghezza, altezza = s.Altezza, esatta = false };
            }
            case "converti":
            {
                var ids = new List<string>();
                foreach (var el in (a["elementi"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var f = Catalogo.Trova((string?)el["formato"] ?? "");
                    if (f is null) continue;
                    var o = Opzioni.Da(JsonSerializer.SerializeToElement(el["opzioni"]));
                    var voci = (el["ids"] as JsonArray ?? []).Select(x => (string?)x).Where(x => x is not null && Contiene(x)).Select(x => Prendi(x!)).ToList();
                    if (voci.Count == 0) continue;
                    if (f.Unisce) ids.Add(coda.Aggiungi(voci.Select(v => v.Info.Percorso).ToList(), f, o, voci[0].Info.Id).Id);
                    else foreach (var v in voci) ids.Add(coda.Aggiungi([v.Info.Percorso], f, o, v.Info.Id).Id);
                }
                return ids;
            }
            case "annulla":
                if ((string?)a["id"] is { } lid) coda.Annulla(lid); else coda.AnnullaTutto();
                return true;
            case "pulisci":
                coda.TogliFiniti();
                return true;
            case "apri":
            case "guarda":
                Apri((string?)a["percorso"]);
                return true;
            case "mostra":
                Mostra((string?)a["percorso"]);
                return true;
            case "finestra":
                switch ((string?)a["azione"])
                {
                    case "chiudi": finestra?.Close(); break;
                    case "riduci": if (finestra is not null) finestra.Minimized = true; break;
                    case "massimizza": if (finestra is not null) finestra.Maximized = !finestra.Maximized; break;
                }
                return true;
            case "impostazioni":
                if (a["scritta"] is { } scritta) imp.Scritta = (string?)scritta ?? "convertito";
                if (a["alMassimo"] is { } am) { imp.AlMassimo = am.GetValue<bool>(); Processi.Priorita = imp.AlMassimo ? System.Diagnostics.ProcessPriorityClass.Normal : System.Diagnostics.ProcessPriorityClass.BelowNormal; }
                if (a["suoniAccesi"] is { } su) imp.SuoniAccesi = su.GetValue<bool>();
                if (a["apriCartella"] is { } ac) imp.ApriCartella = ac.GetValue<bool>();
                if (a["cestinoDaSolo"] is { } ce) imp.CestinoDaSolo = ce.GetValue<bool>();
                if (a["formati"] is JsonObject fo) imp.Formati = fo.ToDictionary(kv => kv.Key, kv => (string?)kv.Value ?? "");
                if (a["scelte"] is JsonObject sc) imp.Scelte = (JsonObject)sc.DeepClone();
                imp.Salva();
                return true;
            case "cestino":
            {
                var perche = coda.OriginaleNelCestino((string?)a["lavoro"] ?? "");
                return new { ok = perche is null, perche };
            }
            case "anteprimaTesto":
            {
                var v = Prendi((string?)a["id"] ?? "");
                var t = Codifiche.Leggi(v.Info.Percorso, 6 * 1024 * 1024);
                return new { testo = t.Testo, codifica = t.Codifica, bom = t.Bom, troncato = t.Troncato, peso = v.Info.Peso };
            }
            case "novita":
            {
                var nuova = !string.IsNullOrEmpty(imp.NovitaVista) && imp.NovitaVista != versione;
                if (imp.NovitaVista != versione) { imp.NovitaVista = versione; imp.Salva(); }
                return new { versione, testo = Novita.Di(versione), nuova };
            }
            case "aggiorna":
                if (a["installa"]?.GetValue<bool>() == true) { await ScaricaAggiornamento(); return true; }
                await CercaAggiornamento();
                return new { versione = aggiornamento };
            case "aggiustaTutto":
            {
                var exe = Ambiente.Eseguibile();
                var fatti = string.IsNullOrEmpty(exe) ? [] : MenuLinux.Installa(exe, Ambiente.Casa());
                return new
                {
                    passi = new[] { new { nome = "Tasto destro (Nautilus, Nemo, Dolphin)", esito = fatti.Count > 0 ? "ok" : "errore", nota = fatti.Count > 0 ? "Riavvia il gestore dei file se la voce non c'è subito." : "Non trovo il programma." } },
                    daRiavviare = false,
                    menu11 = new { supportato = false, registrato = false, fidato = false, chiesto = true, windows11 = false, classico = false },
                    regola = new { supportata = false, attiva = false, diAltri = false, impostati = 0, totale = 0, inAttesa = false },
                    impostazioni = imp,
                };
            }
            case "registro":
                if (File.Exists(Registro.Percorso)) Apri(Registro.Percorso);
                return true;
            case "link":
            {
                var url = (string?)a["url"] ?? "";
                if (url.StartsWith("https://it.libreoffice.org/") || url.StartsWith("https://github.com/cammo22/DaP-Convertitore"))
                    Apri(url);
                return true;
            }
            case "menu11" or "predefinite" or "predefiniteTogli" or "menu" or "classico" or "esplora" or "disconnetti" or "avatar":
                throw new ErroreConversione("Questa cosa c'è solo su Windows.");
            case "incolla" or "indietro":
                return cmd == "incolla" ? 0 : new { ids = Array.Empty<string>(), percorsi = Array.Empty<string>() };
            default:
                throw new ErroreConversione("Questo comando su Linux non c'è ancora.");
        }
    }

    void Aggiungi(string percorso)
    {
        lock (lucchetto)
        {
            if (file.Values.Any(v => string.Equals(v.Info.Percorso, percorso, StringComparison.Ordinal))) return;
        }
        var info = analisi.Primo(percorso);
        var voce = new Voce(info);
        lock (lucchetto) { file[info.Id] = voce; ordine.Add(info.Id); }
        Manda("file", FileDto(voce));
        _ = Task.Run(async () =>
        {
            try
            {
                voce.Dettagli = await analisi.Dettagli(info);
                Manda("dettagli", new { id = info.Id, dettagli = voce.Dettagli });
            }
            catch (Exception e) { Registro.Scrivi($"Dettagli di {info.Nome}: {e.Message}"); }
        });
    }

    Voce Prendi(string id)
    {
        lock (lucchetto) return file.TryGetValue(id, out var v) ? v : throw new ErroreConversione("Il file non è più nella lista.");
    }

    bool Contiene(string id) { lock (lucchetto) return file.ContainsKey(id); }

    IEnumerable<Voce> Ordinati() { lock (lucchetto) return ordine.Where(file.ContainsKey).Select(i => file[i]).ToList(); }

    static async Task<List<string>> ScegliFile()
    {
        foreach (var (programma, argomenti) in new[] { ("/usr/bin/zenity", new[] { "--file-selection", "--multiple", "--separator=|", "--title=Scegli i file da convertire" }), ("/usr/bin/kdialog", new[] { "--getopenfilename", "--multiple", "--title", "Scegli i file da convertire" }) })
        {
            if (!File.Exists(programma)) continue;
            var r = await Processi.Esegui(programma, argomenti, CancellationToken.None);
            if (r.Codice != 0 || string.IsNullOrWhiteSpace(r.Uscita)) return [];
            var sep = programma.EndsWith("zenity") ? '|' : ' ';
            return r.Uscita.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(File.Exists).ToList();
        }
        throw new ErroreConversione("Per scegliere i file serve zenity (GNOME) o kdialog (KDE). Puoi anche usare il tasto destro sul file.");
    }

    static void Apri(string? percorso)
    {
        if (string.IsNullOrWhiteSpace(percorso)) return;
        Processi.Esegui("xdg-open", [percorso], CancellationToken.None).GetAwaiter().GetResult();
    }

    static void Mostra(string? percorso)
    {
        if (string.IsNullOrWhiteSpace(percorso)) return;
        var cartella = Directory.Exists(percorso) ? percorso : Path.GetDirectoryName(percorso);
        if (cartella is not null) Apri(cartella);
    }

    async Task CercaAggiornamento()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var risposta = await http.GetAsync(FonteAggiornamenti.Repo + "/releases/latest", HttpCompletionOption.ResponseHeadersRead);
            var tag = Versioni.DalLink(risposta.RequestMessage?.RequestUri?.AbsoluteUri);
            if (tag is null || !Versioni.PiùNuova(versione, tag)) return;
            aggiornamento = tag;
            Manda("aggiornamento", new { versione = tag });
        }
        catch (Exception e) { Registro.Scrivi($"Aggiornamenti: lo riprovo fra un po' ({e.Message})"); }
    }

    async Task ScaricaAggiornamento()
    {
        var nome = "DaPConvertitore-linux.AppImage";
        var dest = Path.Combine(Ambiente.Casa(), "Scaricati", nome);
        if (!Directory.Exists(Path.GetDirectoryName(dest)!)) dest = Path.Combine(Ambiente.Casa(), "Downloads", nome);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var risposta = await http.GetAsync(FonteAggiornamenti.Repo + "/releases/latest/download/" + nome);
        if (!risposta.IsSuccessStatusCode)
            throw new ErroreConversione("L'aggiornamento non è ancora scaricabile. Riprova fra un po'.");
        await using (var ingresso = await risposta.Content.ReadAsStreamAsync())
        await using (var uscita = File.Create(dest))
            await ingresso.CopyToAsync(uscita);
        try { File.SetUnixFileMode(dest, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); } catch { }
        Registro.Scrivi($"Aggiornamento scaricato in {dest}");
        var avvio = new System.Diagnostics.ProcessStartInfo(dest) { UseShellExecute = false };
        System.Diagnostics.Process.Start(avvio);
        finestra?.Close();
    }

    void Manda(string evento, object? dati)
    {
        var testo = JsonSerializer.Serialize(new { evento, dati }, json);
        try { finestra?.SendWebMessage(testo); } catch { /* la finestra è già chiusa */ }
    }

    void Rispondi(int id, bool ok, object? dati, string? errore)
    {
        var testo = JsonSerializer.Serialize(new { id, ok, dati, errore }, json);
        try { finestra?.SendWebMessage(testo); } catch { }
    }

    static object FileDto(Voce v) => new
    {
        v.Info.Id, v.Info.Percorso, v.Info.Nome, v.Info.Cartella, v.Info.Estensione, v.Info.Categoria, v.Info.NomeCategoria,
        v.Info.Peso, v.Info.EDirectory, v.Info.Formati, dettagli = v.Dettagli, anteprima = (string?)null,
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

    sealed class SenzaStampante : IStampante
    {
        public Task Stampa(string fileHtml, string pdf, CancellationToken ct) =>
            throw new ErroreConversione("Stampare una pagina web in PDF per ora c'è solo su Windows.");
        public Task<string> Testo(string fileHtml, CancellationToken ct) =>
            throw new ErroreConversione("Leggere una pagina web per ora c'è solo su Windows.");
    }
}
