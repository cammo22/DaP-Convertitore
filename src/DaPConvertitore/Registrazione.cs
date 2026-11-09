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
            var h = new WindowInteropHelper(w).Handle;
            if (h != IntPtr.Zero) SetWindowDisplayAffinity(h, WdaEscludi);
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

    /// <summary>Dal vecchio comando: se non si sta già registrando si apre il menu, se no la barra.</summary>
    public static void Alterna()
    {
        if (attiva) BarraRegistrazione.PortaDavanti();
        else MenuRegistrazione.Mostra(null);
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
        attiva = true;
        if (!ApriPezzo())
        {
            attiva = false;
            PulisciCartella();
            Vassoio.Dire("La registrazione non è partita", string.IsNullOrWhiteSpace(ultimoErrore) ? "FFmpeg non è partito." : ultimoErrore);
            return;
        }
        BarraRegistrazione.Mostra();
    }

    public static void Pausa()
    {
        if (!attiva || inPausa || proc is null) return;
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

    /// <summary>Elenco dispositivi. Su questo PC di prova ffmpeg non ha wasapi né dshow: lì la lista resta vuota.</summary>
    public static DispositiviAudio Elenca()
    {
        var ff = Strumenti.Trova().Ffmpeg;
        return new DispositiviAudio(NomiDshow(ff), NomiWasapi(ff));
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
            foreach (var a in Argomenti(path)) psi.ArgumentList.Add(a);
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

    static IEnumerable<string> Argomenti(string uscita)
    {
        yield return "-hide_banner";
        yield return "-y";
        var ingressiAudio = 0;
        if (modo == "schermo")
        {
            yield return "-thread_queue_size"; yield return "1024";
            yield return "-f"; yield return "gdigrab";
            yield return "-framerate"; yield return "30";
            yield return "-draw_mouse"; yield return "1";
            yield return "-i"; yield return "desktop";
        }
        if (!string.IsNullOrWhiteSpace(mic))
        {
            yield return "-thread_queue_size"; yield return "1024";
            yield return "-f"; yield return "dshow";
            yield return "-i"; yield return "audio=" + mic;
            ingressiAudio++;
        }
        if (!string.IsNullOrWhiteSpace(audioPc))
        {
            yield return "-thread_queue_size"; yield return "1024";
            yield return "-f"; yield return "wasapi";
            yield return "-loopback"; yield return "1";
            yield return "-i"; yield return audioPc!;
            ingressiAudio++;
        }
        if (modo == "schermo")
        {
            if (ingressiAudio == 2)
            {
                yield return "-filter_complex"; yield return "[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[a]";
                yield return "-map"; yield return "0:v";
                yield return "-map"; yield return "[a]";
            }
            else if (ingressiAudio == 1)
            {
                yield return "-map"; yield return "0:v";
                yield return "-map"; yield return "1:a";
            }
            yield return "-c:v"; yield return "libx264";
            yield return "-preset"; yield return "veryfast";
            yield return "-crf"; yield return "23";
            yield return "-pix_fmt"; yield return "yuv420p";
            if (ingressiAudio > 0) { yield return "-c:a"; yield return "aac"; yield return "-b:a"; yield return "160k"; }
        }
        else
        {
            if (ingressiAudio == 2)
            {
                yield return "-filter_complex"; yield return "[0:a][1:a]amix=inputs=2:duration=longest:dropout_transition=0[a]";
                yield return "-map"; yield return "[a]";
            }
            yield return "-c:a"; yield return "aac";
            yield return "-b:a"; yield return "192k";
        }
        yield return uscita;
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

    static List<string> NomiDshow(string ffmpeg)
    {
        var testo = Leggi(ffmpeg, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy");
        var audio = false;
        var nomi = new List<string>();
        foreach (var riga in testo.Split('\n'))
        {
            if (riga.Contains("DirectShow audio", StringComparison.OrdinalIgnoreCase)) { audio = true; continue; }
            if (riga.Contains("DirectShow video", StringComparison.OrdinalIgnoreCase)) { audio = false; continue; }
            if (!audio || riga.Contains("Alternative name", StringComparison.OrdinalIgnoreCase)) continue;
            Aggiungi(nomi, riga);
        }
        return nomi;
    }

    static List<string> NomiWasapi(string ffmpeg)
    {
        var testo = Leggi(ffmpeg, "-hide_banner", "-list_devices", "true", "-f", "wasapi", "-i", "dummy");
        var tutti = new List<string>();
        var playback = new List<string>();
        foreach (var riga in testo.Split('\n'))
        {
            if (riga.Contains("(capture)", StringComparison.OrdinalIgnoreCase)) continue;
            if (NomeFraVirgolette(riga) is not { } n) continue;
            if (!tutti.Contains(n)) tutti.Add(n);
            if (riga.Contains("playback", StringComparison.OrdinalIgnoreCase) && !playback.Contains(n)) playback.Add(n);
        }
        return playback.Count > 0 ? playback : tutti;
    }

    static void Aggiungi(List<string> nomi, string riga)
    {
        if (NomeFraVirgolette(riga) is { } n && !nomi.Contains(n)) nomi.Add(n);
    }

    static string? NomeFraVirgolette(string riga)
    {
        var a = riga.IndexOf('"');
        var b = a >= 0 ? riga.IndexOf('"', a + 1) : -1;
        if (a < 0 || b <= a) return null;
        var n = riga[(a + 1)..b].Trim();
        return n.Length == 0 ? null : n;
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
