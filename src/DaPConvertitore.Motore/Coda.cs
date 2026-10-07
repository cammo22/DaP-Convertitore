using System.Diagnostics;

namespace DaP.Convertitore;

/// <summary>
/// La coda dei lavori. I video vanno uno alla volta (la scheda video lavora meglio su uno, e FFmpeg usa già tutti i
/// core); il resto va in parallelo. Ogni lavoro scrive in una bozza accanto all'originale e solo alla fine prende
/// il suo nome: se qualcosa va storto non resta un file a metà col nome giusto.
/// </summary>
public sealed class Coda(Strumenti strumenti, Func<InfoHardware> hardware, Func<IStampante?> stampante, Func<string> scritta)
{
    readonly SemaphoreSlim pesanti = new(1, 1);
    readonly SemaphoreSlim leggeri = new(Math.Clamp(Environment.ProcessorCount / 4, 2, 4));
    readonly List<Lavoro> lavori = [];
    int contatore;

    /// <summary>Ogni cambiamento di un lavoro (avanzamento compreso, al massimo ~8 volte al secondo).</summary>
    public event Action<Lavoro>? Cambiato;

    public IReadOnlyList<Lavoro> Tutti { get { lock (lavori) return lavori.ToList(); } }

    public bool Occupata { get { lock (lavori) return lavori.Any(l => l.Stato is StatoLavoro.Attesa or StatoLavoro.Corre); } }

    public Lavoro Aggiungi(IReadOnlyList<string> sorgenti, Formato formato, Opzioni opzioni, string? fileId = null)
    {
        var l = new Lavoro
        {
            Id = $"L{Interlocked.Increment(ref contatore)}",
            Sorgenti = sorgenti,
            Formato = formato,
            Opzioni = opzioni,
            FileId = fileId,
        };
        l.PesoPrima = sorgenti.Sum(Peso);
        lock (lavori) lavori.Add(l);
        Cambiato?.Invoke(l);
        _ = Task.Run(() => Esegui(l));
        return l;
    }

    public void Annulla(string id)
    {
        Lavoro? l;
        lock (lavori) l = lavori.FirstOrDefault(x => x.Id == id);
        l?.Annullo.Cancel();
    }

    public void AnnullaTutto()
    {
        foreach (var l in Tutti) if (l.Stato is StatoLavoro.Attesa or StatoLavoro.Corre) l.Annullo.Cancel();
    }

    public void TogliFiniti()
    {
        lock (lavori) lavori.RemoveAll(l => l.Stato is StatoLavoro.Fatto or StatoLavoro.Errore or StatoLavoro.Annullato);
    }

    static long Peso(string p)
    {
        try
        {
            if (File.Exists(p)) return new FileInfo(p).Length;
            if (Directory.Exists(p)) return new DirectoryInfo(p).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch { }
        return 0;
    }

    static bool Pesante(Formato f) => f.Motore == Motore.Video || f.Id is "video.mp3" or "video.wav";

    async Task Esegui(Lavoro l)
    {
        var semaforo = Pesante(l.Formato) ? pesanti : leggeri;
        var ct = l.Annullo.Token;
        var ultimo = Stopwatch.StartNew();
        var orologio = new Stopwatch();
        Contesto? ctx = null;
        try
        {
            await semaforo.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Chiudi(l, StatoLavoro.Annullato);
            return;
        }

        try
        {
            l.Stato = StatoLavoro.Corre;
            l.Inizio = DateTime.Now;
            orologio.Start();
            l.Uscita = l.Formato.Id == "arch.estrai"
                ? Nomi.PrenotaCartella(l.Sorgente)
                : Nomi.Prenota(l.Sorgente, l.Formato.Estensione, scritta());
            l.Bozza = Nomi.Bozza(l.Uscita);
            Cambiato?.Invoke(l);

            ctx = new Contesto(strumenti, hardware(), ct, a =>
            {
                l.Frazione = a.Frazione;
                if (a.Fase is not null) l.Fase = a.Fase;
                l.Velocita = a.Velocita;
                l.Fps = a.Fps;
                var sec = orologio.Elapsed.TotalSeconds;
                l.Eta = a.Frazione > 0.02 && a.Frazione < 1 && sec > 2 ? sec * (1 - a.Frazione) / a.Frazione : null;
                if (ultimo.ElapsedMilliseconds >= 120 || a.Frazione >= 1) { ultimo.Restart(); Cambiato?.Invoke(l); }
            }, stampante());

            Registro.Scrivi($"[{l.Id}] {l.Formato.Id} ← {string.Join(" | ", l.Sorgenti)}");
            await Conversioni.Esegui(l, ctx);
            ct.ThrowIfCancellationRequested();
            Consegna(l);
            l.Frazione = 1;
            l.Secondi = orologio.Elapsed.TotalSeconds;
            Chiudi(l, StatoLavoro.Fatto);
            Registro.Scrivi($"[{l.Id}] fatto in {l.Secondi:0.0}s → {l.Uscita} ({l.PesoDopo} byte)");
        }
        catch (OperationCanceledException)
        {
            Butta(l);
            Chiudi(l, StatoLavoro.Annullato);
        }
        catch (Exception e)
        {
            Butta(l);
            l.Errore = e is ErroreConversione ec ? ec.Message : $"Qualcosa è andato storto: {e.Message}";
            l.Dettaglio = e is ErroreConversione ed ? ed.Dettaglio : e.ToString();
            Registro.Errore($"[{l.Id}] {l.Formato.Id}", e);
            Chiudi(l, StatoLavoro.Errore);
        }
        finally
        {
            ctx?.Pulisci();
            foreach (var f in Directory.EnumerateFiles(Strumenti.CartellaTemporanea, $"passata-{l.Id}*"))
                try { File.Delete(f); } catch { }
            if (l.Uscita is not null) Nomi.Libera(l.Uscita);
            semaforo.Release();
        }
    }

    /// <summary>La bozza prende il nome vero; le date restano quelle dell'originale (le foto si riordinano giuste).</summary>
    void Consegna(Lavoro l)
    {
        var uscita = l.Uscita!;
        if (Directory.Exists(l.Bozza))
        {
            var dentro = Directory.GetFileSystemEntries(l.Bozza);
            if (dentro.Length == 0) throw new ErroreConversione("Non è uscito niente.");
            // il PDF di una pagina sola: una foto, non una cartella con una foto dentro
            if (l.Formato.Categoria == Categoria.Pdf && dentro.Length == 1 && File.Exists(dentro[0]))
            {
                var file = Nomi.Prenota(l.Sorgente, Path.GetExtension(dentro[0]), scritta());
                Nomi.Libera(uscita);
                uscita = file;
                File.Move(dentro[0], uscita);
                Directory.Delete(l.Bozza);
            }
            else Directory.Move(l.Bozza, uscita);
            l.PesoDopo = Peso(uscita);
        }
        else if (File.Exists(l.Bozza))
        {
            File.Move(l.Bozza, uscita);
            l.PesoDopo = new FileInfo(uscita).Length;
            try
            {
                var orig = new FileInfo(l.Sorgente);
                if (orig.Exists && l.Formato.Categoria is Categoria.Immagine or Categoria.Video or Categoria.Audio)
                {
                    File.SetCreationTime(uscita, orig.CreationTime);
                    File.SetLastWriteTime(uscita, orig.LastWriteTime);
                }
            }
            catch { }
        }
        else throw new ErroreConversione("Non è uscito niente.");
        l.Uscita = uscita;
    }

    static void Butta(Lavoro l)
    {
        try
        {
            if (File.Exists(l.Bozza)) File.Delete(l.Bozza);
            else if (Directory.Exists(l.Bozza)) Directory.Delete(l.Bozza, true);
        }
        catch { }
    }

    void Chiudi(Lavoro l, StatoLavoro s)
    {
        l.Stato = s;
        l.Eta = null;
        Cambiato?.Invoke(l);
    }
}

/// <summary>Chi fa cosa: dal formato al motore.</summary>
public static class Conversioni
{
    public static Task Esegui(Lavoro l, Contesto ctx) => l.Formato.Motore switch
    {
        Motore.Video when l.Formato.Categoria == Categoria.Immagine => Video.DaAnimazione(l, ctx),
        Motore.Video => Video.Converti(l, ctx),
        Motore.Audio => Audio.Converti(l, ctx),
        Motore.Immagini => Immagini.Converti(l, ctx),
        Motore.Pdf => Pdf.Converti(l, ctx),
        Motore.Office => Office.Converti(l, ctx),
        Motore.Testo => Testo.Converti(l, ctx),
        Motore.Dati => Dati.Converti(l, ctx),
        Motore.Sottotitoli => Sottotitoli.Converti(l, ctx),
        Motore.Archivi => Archivi.Converti(l, ctx),
        _ => throw new ErroreConversione("Formato sconosciuto."),
    };

    /// <summary>Se il formato non si può fare su questo PC, il perché (per spegnerlo nell'interfaccia).</summary>
    public static string? Manca(Formato f, Strumenti s) => f.Motore switch
    {
        Motore.Video or Motore.Audio or Motore.Sottotitoli when !s.HaFfmpeg => "Manca FFmpeg: reinstalla il convertitore.",
        Motore.Office when !s.HaOffice => "Serve LibreOffice (gratis) o Microsoft Office.",
        Motore.Testo when f.Estensione == ".docx" && s.LibreOffice is null => "Serve LibreOffice (gratis).",
        Motore.Dati when f.Categoria == Categoria.Foglio && s.LibreOffice is null => null,
        _ => null,
    };
}
