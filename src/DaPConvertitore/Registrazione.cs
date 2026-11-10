using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>Microfoni e uscite del PC, con i nomi di Windows (WASAPI).</summary>
public sealed record DispositiviAudio(IReadOnlyList<string> Microfoni, IReadOnlyList<string> Uscite);

/// <summary>
/// Registra lo schermo con l'audio, oppure solo l'audio. Il video lo fa FFmpeg con gdigrab.
/// L'audio lo prende WASAPI nel programma (microfono e loopback del PC), lo scrive in un WAV
/// e alla fine lo unisce. La pausa chiude un pezzo e la ripresa ne apre un altro.
/// La finestra e la barra stanno fuori dalla cattura (WDA_EXCLUDEFROMCAPTURE) ma restano opache:
/// una finestra trasparente, su Windows 11, con quell'esclusione sparisce anche per chi la guarda.
/// </summary>
public static class Registrazione
{
    const uint WdaEscludi = 0x11;

    [DllImport("user32.dll")]
    static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valore, int size);

    sealed record Pezzo(string? Video, string? Audio);

    static readonly List<Pezzo> pezzi = [];
    static Process? proc;
    static CatturaAudio? cattura;
    static Task? chiusura;
    static string? dir, finale;
    static DateTime inizioPezzo;
    static TimeSpan accumulato;
    static bool attiva, inPausa;
    static string modo = "schermo", ultimoErrore = "";
    static string? mic, audioPc, titoloScelto;
    static int generazione, fpsScelto = 30, crfScelto = 23;
    static bool cursoreScelto = true;
    static ComandiRegistrazione.Schermo? schermoScelto;

    /// <summary>L'avatar, se è acceso: la finestra e la barra gli stanno accanto finché non le stacchi.</summary>
    public static Window? Ancora { get; set; }

    public static bool Attiva => attiva;
    public static bool InPausa => inPausa;

    public static TimeSpan Trascorso
    {
        get
        {
            var t = accumulato;
            if (attiva && !inPausa) t += DateTime.UtcNow - inizioPezzo;
            return t < TimeSpan.Zero ? TimeSpan.Zero : t;
        }
    }

    public static string TestoTempo()
    {
        var t = Trascorso;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }

    /// <summary>La finestra non entra nello screenshot né nella registrazione dello schermo (Windows 10 2004 in poi).</summary>
    public static void Escludi(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) { Registro.Scrivi($"registrazione: {w.Title} senza maniglia, non la escludo"); return; }
                // angoli tondi di Windows 11 (DWMWCP_ROUND). La finestra non è trasparente: una layered
                // con WDA_EXCLUDEFROMCAPTURE su Windows 11 diventa invisibile anche a chi la guarda.
                int tondi = 2;
                try { DwmSetWindowAttribute(h, 33, ref tondi, 4); } catch { /* Windows 10 non ha gli angoli */ }
                var ok = SetWindowDisplayAffinity(h, WdaEscludi);
                Registro.Scrivi(ok
                    ? $"registrazione: «{w.Title}» non entra nella cattura"
                    : $"registrazione: «{w.Title}» resta anche nella cattura (Windows prima del 2004, o la chiamata è fallita)");
            }
            catch (Exception e) { Registro.Errore("registrazione: esclusione", e); }
        };
    }

    /// <summary>Anche il menu a comparsa, che è una finestra a sé.</summary>
    public static void EscludiMenu(ContextMenu menu)
    {
        menu.Opened += (_, _) => menu.Dispatcher.BeginInvoke(() =>
        {
            if (PresentationSource.FromVisual(menu) is HwndSource hs && hs.Handle != IntPtr.Zero)
                SetWindowDisplayAffinity(hs.Handle, WdaEscludi);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// «Registra» dal vassoio, dall'avatar o dal ponte. Stessa decisione (<see cref="ComandiRegistrazione.CosaFa"/>):
    /// si apre il pannello, oppure si porta davanti se si sta già registrando. Il lavoro vero parte dopo,
    /// a menu chiuso: aprirlo dentro il clic lo lasciava a misura zero, o lo chiudeva insieme al menu.
    /// </summary>
    public static void Chiedi(string da, Window? accanto = null)
    {
        accanto ??= AvatarVisibile();
        Ancora = accanto;
        var cosa = ComandiRegistrazione.CosaFa(da, attiva);
        Registro.Scrivi($"registrazione: «Registra» da {da} → {cosa}");
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null)
        {
            Registro.Scrivi("registrazione: non c'è l'interfaccia, il pannello non si apre");
            return;
        }
        d.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            try
            {
                if (attiva)
                {
                    Registro.Scrivi("registrazione: è già in corso, porto la finestra");
                    if (BarraRegistrazione.Visibile && !MenuRegistrazione.Visibile) BarraRegistrazione.PortaDavanti();
                    else MenuRegistrazione.PortaDavanti();
                    return;
                }
                MenuRegistrazione.Mostra(accanto, da);
            }
            catch (Exception e)
            {
                Registro.Errore("registrazione: apertura", e);
                try { Vassoio.Dire("Registra non si è aperto", e.Message); } catch { /* il vassoio può non esserci */ }
            }
        });
    }

    public static bool Inizia(string modoScelto, string? microfono, string? uscitaPc, ComandiRegistrazione.Schermo? schermo, int fps, int crf, bool cursore, string? titolo)
    {
        if (attiva) { MenuRegistrazione.PortaDavanti(); return true; }
        if (modoScelto == "audio" && microfono is null && uscitaPc is null)
        {
            Vassoio.Dire("Scegli un audio", "Per registrare solo l'audio serve il microfono o l'audio del PC.");
            return false;
        }
        modo = modoScelto == "audio" ? "audio" : modoScelto == "finestra" ? "finestra" : modoScelto == "zona" ? "zona" : "schermo";
        mic = microfono;
        audioPc = uscitaPc;
        schermoScelto = schermo;
        fpsScelto = ComandiRegistrazione.FpsDi(fps);
        crfScelto = Math.Clamp(crf, 16, 32);
        cursoreScelto = cursore;
        titoloScelto = titolo;
        pezzi.Clear();
        accumulato = TimeSpan.Zero;
        inPausa = false;
        ultimoErrore = "";
        dir = Path.Combine(Path.GetTempPath(), "DaP Convertitore", "reg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ext = modo == "audio" ? ".m4a" : ".mp4";
        finale = Path.Combine(CartellaLavori.Assicura(), $"registrazione {DateTime.Now:yyyy-MM-dd HH-mm-ss}{ext}");
        Registro.Scrivi($"registrazione: parto ({modo}, mic «{mic ?? "no"}», pc «{audioPc ?? "no"}») → {finale}");
        attiva = true;
        if (!ApriPezzo())
        {
            attiva = false;
            PulisciCartella();
            Registro.Scrivi($"registrazione: non è partita ({(string.IsNullOrWhiteSpace(ultimoErrore) ? "non è partito il pezzo" : ultimoErrore)})");
            Vassoio.Dire("La registrazione non è partita", string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg non è partito." : ultimoErrore);
            return false;
        }
        TastiRegistrazione.Accendi();
        if (!MenuRegistrazione.Visibile) BarraRegistrazione.Mostra();
        MenuRegistrazione.Aggiorna();
        return true;
    }

    static Window? AvatarVisibile()
    {
        var app = System.Windows.Application.Current;
        if (app is null) return null;
        foreach (Window w in app.Windows)
            if (w is FinestraAvatar && w.IsVisible) return w;
        return null;
    }

    public static void Pausa()
    {
        if (!attiva || inPausa) return;
        Registro.Scrivi("registrazione: pausa");
        var p = proc;
        var c = cattura;
        proc = null;
        cattura = null;
        accumulato += DateTime.UtcNow - inizioPezzo;
        inPausa = true;
        chiusura = Task.Run(() =>
        {
            ChiudiMorbido(p);
            try { c?.Ferma(); } catch { /* il WAV del pezzo resta com'è */ }
        });
        BarraRegistrazione.Aggiorna();
        MenuRegistrazione.Aggiorna();
    }

    public static void Riprendi()
    {
        if (!attiva || !inPausa) return;
        Registro.Scrivi("registrazione: riprendo");
        var attesa = chiusura;
        _ = Task.Run(() =>
        {
            try { attesa?.Wait(10000); } catch { /* il pezzo è già chiuso */ }
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!attiva || !inPausa) return;
                if (!ApriPezzo())
                {
                    Vassoio.Dire("Non riprendo", string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg non è ripartito." : ultimoErrore);
                    return;
                }
                inPausa = false;
                BarraRegistrazione.Aggiorna();
                MenuRegistrazione.Aggiorna();
            });
        });
    }

    public static void Salva()
    {
        if (!attiva) return;
        Registro.Scrivi("registrazione: salvo");
        var durata = Trascorso;
        attiva = false;
        inPausa = false;
        var p = proc;
        var c = cattura;
        var attesa = chiusura;
        proc = null;
        cattura = null;
        var lista = pezzi.ToList();
        var dest = finale;
        var cartella = dir;
        dir = finale = null;
        BarraRegistrazione.Chiudi();
        MenuRegistrazione.Aggiorna();
        TastiRegistrazione.Sistema();
        _ = Task.Run(() =>
        {
            if (p is not null) ChiudiMorbido(p);
            else try { attesa?.Wait(10000); } catch { /* già fermo */ }
            try { c?.Ferma(); } catch { /* il pezzo audio è già chiuso */ }
            var buoni = new List<string>();
            foreach (var pezzo in lista)
            {
                var uno = Prepara(pezzo);
                if (uno is not null) buoni.Add(uno);
            }
            try
            {
                if (dest is null || buoni.Count == 0) throw new InvalidOperationException("Non c'è niente da salvare.");
                try { Unisci(buoni, dest); }
                catch (Exception e)
                {
                    File.Copy(buoni[0], dest, true);
                    Registro.Scrivi($"registrazione, unisco: {e.Message}");
                    Ricorda(dest);
                    SulFilo(() =>
                    {
                        Vassoio.Dire("Registrazione salvata a pezzi", "Il resto non si è unito: ho tenuto il primo pezzo, " + Path.GetFileName(dest));
                        RiepilogoRegistrazione.Mostra(dest, durata);
                    });
                    return;
                }
                Registro.Scrivi($"registrazione: salvata {dest}");
                Ricorda(dest);
                SulFilo(() => RiepilogoRegistrazione.Mostra(dest, durata));
            }
            catch (Exception e)
            {
                Registro.Errore("registrazione", e);
                SulFilo(() => Vassoio.Dire("Non ho salvato la registrazione", e.Message));
            }
            finally { try { if (cartella is not null) Directory.Delete(cartella, true); } catch { /* i pezzi restano nel temp */ } }
        });
    }

    public static void Annulla()
    {
        if (!attiva && proc is null && cattura is null) { BarraRegistrazione.Chiudi(); TastiRegistrazione.Sistema(); return; }
        Registro.Scrivi("registrazione: annullo");
        attiva = false;
        inPausa = false;
        var p = proc;
        var c = cattura;
        proc = null;
        cattura = null;
        var cartella = dir;
        dir = finale = null;
        pezzi.Clear();
        BarraRegistrazione.Chiudi();
        MenuRegistrazione.Aggiorna();
        TastiRegistrazione.Sistema();
        _ = Task.Run(() =>
        {
            try { if (p is { HasExited: false }) { Figli.Tolto(p.Id); p.Kill(entireProcessTree: true); } } catch { /* già fermo */ }
            try { p?.Dispose(); } catch { }
            try { c?.Ferma(); } catch { }
            try { if (cartella is not null) Directory.Delete(cartella, true); } catch { /* ci riprova il temp di Windows */ }
        });
    }

    /// <summary>L'app si chiude: niente FFmpeg lasciato acceso e niente cattura WASAPI appesa.</summary>
    public static void Spegni()
    {
        attiva = false;
        var p = proc;
        var c = cattura;
        proc = null;
        cattura = null;
        try
        {
            if (p is { HasExited: false }) { Figli.Tolto(p.Id); p.Kill(entireProcessTree: true); }
        }
        catch { /* già fermo */ }
        try { p?.Dispose(); } catch { }
        try { c?.Ferma(); } catch { }
        TastiRegistrazione.Spegni();
    }

    /// <summary>Elenco dispositivi da WASAPI, con i nomi di Windows. FFmpeg non c'entra: non ha wasapi.</summary>
    public static DispositiviAudio Elenca()
    {
        var d = AudioWasapi.Elenca();
        Registro.Scrivi(d.Microfoni.Count == 0 && d.Uscite.Count == 0
            ? "registrazione: nessun dispositivo WASAPI. Il microfono e l'audio del PC li elenca Windows, non FFmpeg."
            : $"registrazione: {d.Microfoni.Count} microfoni, {d.Uscite.Count} uscite del PC (WASAPI)");
        return d;
    }

    /// <summary>Il monitor principale, in pixel veri. Se non si legge, gdigrab prende tutto il desktop.</summary>
    public static ComandiRegistrazione.Schermo? SchermoPrincipale()
    {
        try
        {
            var b = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
            if (b is null || b.Value.Width < 2 || b.Value.Height < 2) return null;
            return new ComandiRegistrazione.Schermo(b.Value.X, b.Value.Y, b.Value.Width, b.Value.Height);
        }
        catch (Exception e)
        {
            Registro.Scrivi($"registrazione: non leggo il monitor principale ({e.Message}), prendo tutto il desktop");
            return null;
        }
    }

    static bool ApriPezzo()
    {
        if (dir is null) return false;
        var n = pezzi.Count;
        string? video = null;
        string? audio = null;
        Process? avviato = null;
        CatturaAudio? presa = null;
        try
        {
            if (modo != "audio")
            {
                video = Path.Combine(dir, $"v{n:00}.mp4");
                var s = Strumenti.Trova();
                var psi = new ProcessStartInfo(s.Ffmpeg)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                var argomenti = ComandiRegistrazione.ArgomentiVideo(modo, video, schermoScelto ?? SchermoPrincipale(), fpsScelto, crfScelto, cursoreScelto, titoloScelto);
                Registro.Scrivi($"> ffmpeg {ComandiRegistrazione.Riga(argomenti)}");
                foreach (var a in argomenti) psi.ArgumentList.Add(a);
                avviato = Process.Start(psi);
                if (avviato is null) { ultimoErrore = "FFmpeg non è partito."; return false; }
                Figli.Segna(avviato.Id);
                avviato.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) ultimoErrore = e.Data!; };
                avviato.BeginErrorReadLine();
            }
            if (!string.IsNullOrWhiteSpace(mic) || !string.IsNullOrWhiteSpace(audioPc))
            {
                audio = Path.Combine(dir, $"a{n:00}.wav");
                presa = new CatturaAudio(audio, mic, audioPc);
                if (!presa.Partita && modo == "audio")
                {
                    ultimoErrore = "Il microfono e l'audio del PC non si sono aperti.";
                    presa.Ferma();
                    ChiudiMorbido(avviato);
                    return false;
                }
                if (!presa.Partita) { presa.Ferma(); presa = null; audio = null; }
            }
            if (avviato is null && presa is null)
            {
                ultimoErrore = modo == "audio" ? "Scegli un audio." : "FFmpeg non è partito.";
                return false;
            }
            proc = avviato;
            cattura = presa;
            pezzi.Add(new Pezzo(video, audio));
            inizioPezzo = DateTime.UtcNow;
            if (avviato is { } p)
            {
                var gen = ++generazione;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(800);
                    int codice;
                    try
                    {
                        if (!p.HasExited || p.ExitCode == 0) return;
                        codice = p.ExitCode;
                    }
                    catch { return; }
                    SulFilo(() =>
                    {
                        // la pausa e il salvataggio hanno già preso il processo: un «q» che esce con errore non è un fallimento
                        if (!attiva || gen != generazione || !ReferenceEquals(proc, p)) return;
                        attiva = false;
                        var c = cattura;
                        proc = null;
                        cattura = null;
                        try { Figli.Tolto(p.Id); } catch { }
                        try { c?.Ferma(); } catch { }
                        try { p.Dispose(); } catch { }
                        BarraRegistrazione.Chiudi();
                        TastiRegistrazione.Sistema();
                        Registro.Scrivi($"registrazione: FFmpeg si è chiuso subito ({codice}) {ultimoErrore}");
                        Vassoio.Dire("La registrazione non è partita", string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg si è chiuso subito." : ultimoErrore);
                        PulisciCartella();
                    });
                });
            }
            return true;
        }
        catch (Exception e)
        {
            ultimoErrore = e.Message;
            Registro.Errore("registrazione", e);
            try { presa?.Ferma(); } catch { }
            ChiudiMorbido(avviato);
            return false;
        }
    }

    /// <summary>Un pezzo diventa un file solo: video e WAV insieme, oppure l'uno o l'altro.</summary>
    static string? Prepara(Pezzo pezzo)
    {
        var videoOk = pezzo.Video is not null && File.Exists(pezzo.Video) && new FileInfo(pezzo.Video).Length > 800;
        var audioOk = pezzo.Audio is not null && File.Exists(pezzo.Audio) && new FileInfo(pezzo.Audio).Length > 64;
        if (videoOk && audioOk)
        {
            var unito = Path.Combine(Path.GetDirectoryName(pezzo.Video)!, Path.GetFileNameWithoutExtension(pezzo.Video) + "-unito.mp4");
            var esito = Processi.Esegui(Strumenti.Trova().Ffmpeg, ComandiRegistrazione.ArgomentiMux(pezzo.Video!, pezzo.Audio!, unito), CancellationToken.None).GetAwaiter().GetResult();
            if (esito.Codice == 0 && File.Exists(unito) && new FileInfo(unito).Length > 800) return unito;
            Registro.Scrivi($"registrazione: il WAV non si è unito ({Taglia(esito.Errori)}), tengo il video");
            return pezzo.Video;
        }
        if (videoOk) return pezzo.Video;
        if (!audioOk || pezzo.Audio is null) return null;
        var m4a = Path.ChangeExtension(pezzo.Audio, ".m4a");
        var audio = Processi.Esegui(Strumenti.Trova().Ffmpeg, ComandiRegistrazione.ArgomentiDaWav(pezzo.Audio, m4a), CancellationToken.None).GetAwaiter().GetResult();
        if (audio.Codice == 0 && File.Exists(m4a)) return m4a;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(audio.Errori) ? "L'audio non si è codificato." : Taglia(audio.Errori));
    }

    static void ChiudiMorbido(Process? p)
    {
        if (p is null) return;
        try { Figli.Tolto(p.Id); } catch { }
        try { if (!p.HasExited) { p.StandardInput.Write("q"); p.StandardInput.Flush(); } } catch { /* già chiuso */ }
        try { if (!p.WaitForExit(8000)) p.Kill(entireProcessTree: true); } catch { /* se ne è già andato */ }
        try { p.Dispose(); } catch { }
    }

    static void Unisci(IReadOnlyList<string> buoni, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (buoni.Count == 1) { File.Copy(buoni[0], dest, true); return; }
        var lista = Path.Combine(Path.GetDirectoryName(buoni[0])!, "lista.txt");
        File.WriteAllLines(lista, buoni.Select(p => "file '" + p.Replace('\\', '/').Replace("'", "'\\''") + "'"));
        var esito = Processi.Esegui(Strumenti.Trova().Ffmpeg,
            ["-hide_banner", "-y", "-f", "concat", "-safe", "0", "-i", lista, "-c", "copy", dest],
            CancellationToken.None).GetAwaiter().GetResult();
        if (esito.Codice != 0 || !File.Exists(dest))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(esito.Errori) ? "I pezzi non si sono uniti." : Taglia(esito.Errori));
    }

    static void PulisciCartella()
    {
        var cartella = dir;
        dir = finale = null;
        pezzi.Clear();
        try { if (cartella is not null) Directory.Delete(cartella, true); } catch { /* il temp la toglie dopo */ }
    }

    static void Ricorda(string? dest)
    {
        if (dest is not null && File.Exists(dest)) Storico.AggiungiFile(dest, "Registrazione");
    }

    static string Taglia(string s) => s.Length > 240 ? s[^240..] : s;

    static void SulFilo(Action fai)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) fai();
        else d.BeginInvoke(fai);
    }
}
