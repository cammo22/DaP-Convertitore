using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>Microfoni (DirectShow) e uscite del PC (WASAPI, quelle che si possono registrare in loopback).</summary>
public sealed record DispositiviAudio(IReadOnlyList<string> Microfoni, IReadOnlyList<string> Uscite);

/// <summary>
/// Registra lo schermo principale con l'audio, oppure solo l'audio. La pausa chiude un pezzo e la ripresa ne apre
/// un altro: alla fine si uniscono, come fa la registrazione di DaProd Editor Video. La barra e i menu stanno
/// fuori dalla cattura (WDA_EXCLUDEFROMCAPTURE). Lo schermo è quello principale, intero: una zona no.
/// </summary>
public static class Registrazione
{
    const uint WdaEscludi = 0x11;

    [DllImport("user32.dll")]
    static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valore, int size);

    static readonly List<string> pezzi = [];
    static Process? proc;
    static Task? chiusura;
    static string? dir, finale;
    static DateTime inizioPezzo;
    static TimeSpan accumulato;
    static bool attiva, inPausa;
    static string modo = "schermo", ultimoErrore = "";
    static string? mic, audioPc;
    static int generazione;

    public static bool Attiva => attiva;
    public static bool InPausa => inPausa;

    public static TimeSpan Trascorso
    {
        get
        {
            var t = accumulato;
            if (proc is { HasExited: false }) t += DateTime.UtcNow - inizioPezzo;
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
    /// si apre il menu, oppure si porta davanti la barra se si sta già registrando. Il lavoro vero parte dopo,
    /// a menu chiuso: aprirlo dentro il clic lo lasciava a misura zero, o lo chiudeva insieme al menu.
    /// </summary>
    public static void Chiedi(string da, Window? accanto = null)
    {
        var cosa = ComandiRegistrazione.CosaFa(da, attiva);
        Registro.Scrivi($"registrazione: «Registra» da {da} → {cosa}");
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null)
        {
            Registro.Scrivi("registrazione: non c'è l'interfaccia, il menu non si apre");
            return;
        }
        d.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            try
            {
                if (attiva)
                {
                    Registro.Scrivi("registrazione: è già in corso, porto la barra");
                    BarraRegistrazione.PortaDavanti();
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

    public static void Inizia(string modoScelto, string? microfono, string? uscitaPc)
    {
        if (attiva) { BarraRegistrazione.PortaDavanti(); return; }
        if (modoScelto == "audio" && microfono is null && uscitaPc is null)
        {
            Vassoio.Dire("Scegli un audio", "Per registrare solo l'audio serve il microfono o l'audio del PC.");
            return;
        }
        modo = modoScelto == "audio" ? "audio" : "schermo";
        mic = microfono;
        audioPc = uscitaPc;
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
            Registro.Scrivi($"registrazione: non è partita ({(string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg non è partito" : ultimoErrore)})");
            Vassoio.Dire("La registrazione non è partita", string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg non è partito." : ultimoErrore);
            return;
        }
        BarraRegistrazione.Mostra();
    }

    public static void Pausa()
    {
        if (!attiva || inPausa || proc is null) return;
        Registro.Scrivi("registrazione: pausa");
        var p = proc;
        proc = null;
        accumulato += DateTime.UtcNow - inizioPezzo;
        inPausa = true;
        chiusura = Task.Run(() => ChiudiMorbido(p));
        BarraRegistrazione.Aggiorna();
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
            });
        });
    }

    public static void Salva()
    {
        if (!attiva) return;
        Registro.Scrivi("registrazione: salvo");
        attiva = false;
        inPausa = false;
        var p = proc;
        var attesa = chiusura;
        proc = null;
        var lista = pezzi.ToList();
        var dest = finale;
        var cartella = dir;
        dir = finale = null;
        BarraRegistrazione.Chiudi();
        _ = Task.Run(() =>
        {
            if (p is not null) ChiudiMorbido(p);
            else try { attesa?.Wait(10000); } catch { /* già fermo */ }
            var buoni = lista.Where(x => File.Exists(x) && new FileInfo(x).Length > 800).ToList();
            try
            {
                if (dest is null || buoni.Count == 0) throw new InvalidOperationException("Non c'è niente da salvare.");
                try { Unisci(buoni, dest); }
                catch (Exception e)
                {
                    File.Copy(buoni[0], dest, true);
                    Registro.Scrivi($"registrazione, unisco: {e.Message}");
                    SulFilo(() => Vassoio.Dire("Registrazione salvata a pezzi", "Il resto non si è unito: ho tenuto il primo pezzo, " + Path.GetFileName(dest)));
                    return;
                }
                Registro.Scrivi($"registrazione: salvata {dest}");
                SulFilo(() => Vassoio.Dire("Registrazione salvata", Path.GetFileName(dest)));
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
        if (!attiva && proc is null) { BarraRegistrazione.Chiudi(); return; }
        Registro.Scrivi("registrazione: annullo");
        attiva = false;
        inPausa = false;
        var p = proc;
        proc = null;
        var cartella = dir;
        dir = finale = null;
        pezzi.Clear();
        BarraRegistrazione.Chiudi();
        _ = Task.Run(() =>
        {
            try { if (p is { HasExited: false }) p.Kill(entireProcessTree: true); } catch { /* già fermo */ }
            try { p?.Dispose(); } catch { }
            try { if (cartella is not null) Directory.Delete(cartella, true); } catch { /* ci riprova il temp di Windows */ }
        });
    }

    /// <summary>Elenco dispositivi. Su un ffmpeg senza wasapi né dshow la lista resta vuota, e lo dice il registro.</summary>
    public static DispositiviAudio Elenca()
    {
        var ff = Strumenti.Trova().Ffmpeg;
        var microfoni = ComandiRegistrazione.MicrofoniDaElenco(Leggi(ff, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"));
        var uscite = ComandiRegistrazione.UsciteDaElenco(Leggi(ff, "-hide_banner", "-list_devices", "true", "-f", "wasapi", "-i", "dummy"));
        Registro.Scrivi(microfoni.Count == 0 && uscite.Count == 0
            ? $"registrazione: nessun dispositivo (ffmpeg {ff}). DirectShow e WASAPI servono su Windows, con l'FFmpeg dell'app."
            : $"registrazione: {microfoni.Count} microfoni, {uscite.Count} uscite del PC");
        return new DispositiviAudio(microfoni, uscite);
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
        var ext = modo == "audio" ? ".m4a" : ".mp4";
        var path = Path.Combine(dir, $"p{pezzi.Count:00}{ext}");
        try
        {
            var s = Strumenti.Trova();
            var psi = new ProcessStartInfo(s.Ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
            };
            var argomenti = ComandiRegistrazione.Argomenti(modo, mic, audioPc, path, modo == "audio" ? null : SchermoPrincipale());
            Registro.Scrivi($"> ffmpeg {ComandiRegistrazione.Riga(argomenti)}");
            foreach (var a in argomenti) psi.ArgumentList.Add(a);
            var p = Process.Start(psi);
            if (p is null) return false;
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) ultimoErrore = e.Data!; };
            p.BeginErrorReadLine();
            proc = p;
            pezzi.Add(path);
            inizioPezzo = DateTime.UtcNow;
            var gen = ++generazione;
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                if (p.HasExited && p.ExitCode != 0)
                    SulFilo(() =>
                    {
                        if (!attiva || gen != generazione) return;
                        attiva = false;
                        proc = null;
                        BarraRegistrazione.Chiudi();
                        Registro.Scrivi($"registrazione: FFmpeg si è chiuso subito ({p.ExitCode}) {ultimoErrore}");
                        Vassoio.Dire("La registrazione non è partita", string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg si è chiuso subito." : ultimoErrore);
                        PulisciCartella();
                    });
            });
            return true;
        }
        catch (Exception e)
        {
            ultimoErrore = e.Message;
            Registro.Errore("registrazione", e);
            return false;
        }
    }

    static void ChiudiMorbido(Process p)
    {
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

    static string Leggi(string ffmpeg, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return "";
            var testo = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(8000)) { try { p.Kill(entireProcessTree: true); } catch { } }
            return testo;
        }
        catch (Exception e) { Registro.Scrivi($"dispositivi: {e.Message}"); return ""; }
    }

    static string Taglia(string s) => s.Length > 240 ? s[^240..] : s;

    static void SulFilo(Action fai)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) fai();
        else d.BeginInvoke(fai);
    }
}
