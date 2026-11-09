using System.Diagnostics;
using System.Text;

namespace DaP.Convertitore.App;

/// <summary>
/// Registra lo schermo principale e, se c'è, il microfono. FFmpeg con gdigrab: si ferma con «q», il file resta intero.
/// Lo schermo intero di tutti i monitor insieme no: è il desktop principale.
/// </summary>
public static class Registrazione
{
    static Process? proc;
    static string? file;

    public static bool InCorso => proc is { HasExited: false };

    public static void Alterna()
    {
        if (InCorso) Ferma();
        else Avvia();
    }

    public static void Avvia()
    {
        if (InCorso) return;
        var s = Strumenti.Trova();
        var cartella = CartellaLavori.Assicura();
        file = Path.Combine(cartella, $"registrazione {DateTime.Now:yyyy-MM-dd HH-mm-ss}.mp4");
        var mic = Microfono(s.Ffmpeg);
        var psi = new ProcessStartInfo(s.Ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("gdigrab");
        psi.ArgumentList.Add("-framerate"); psi.ArgumentList.Add("30");
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add("desktop");
        if (mic is not null)
        {
            psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("dshow");
            psi.ArgumentList.Add("-i"); psi.ArgumentList.Add("audio=" + mic);
        }
        psi.ArgumentList.Add("-c:v"); psi.ArgumentList.Add("libx264");
        psi.ArgumentList.Add("-preset"); psi.ArgumentList.Add("veryfast");
        psi.ArgumentList.Add("-crf"); psi.ArgumentList.Add("23");
        psi.ArgumentList.Add("-pix_fmt"); psi.ArgumentList.Add("yuv420p");
        if (mic is not null) { psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("aac"); psi.ArgumentList.Add("-b:a"); psi.ArgumentList.Add("160k"); }
        psi.ArgumentList.Add(file);
        try
        {
            proc = Process.Start(psi);
            if (proc is null) throw new InvalidOperationException("FFmpeg non è partito.");
            proc.ErrorDataReceived += (_, _) => { };
            proc.BeginErrorReadLine();
            Vassoio.Dire("Registro lo schermo", mic is null ? "Senza microfono. Riclicca per fermare." : "Schermo e microfono. Riclicca per fermare.");
        }
        catch (Exception e)
        {
            Registro.Errore("registrazione", e);
            Vassoio.Dire("Non registro", e.Message);
            proc = null;
        }
    }

    public static void Ferma()
    {
        var p = proc;
        var dove = file;
        proc = null;
        if (p is null) return;
        try { if (!p.HasExited) { p.StandardInput.WriteLine("q"); p.StandardInput.Flush(); } } catch { /* già chiuso */ }
        _ = Task.Run(async () =>
        {
            try { if (!p.WaitForExit(8000)) p.Kill(entireProcessTree: true); } catch { /* se ne è già andato */ }
            try { p.Dispose(); } catch { }
            if (dove is not null && File.Exists(dove))
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Vassoio.Dire("Registrazione salvata", Path.GetFileName(dove)));
        });
    }

    /// <summary>Il primo microfono che DirectShow elenca, o null.</summary>
    static string? Microfono(string ffmpeg)
    {
        try
        {
            var psi = new ProcessStartInfo(ffmpeg, "-hide_banner -list_devices true -f dshow -i dummy")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, StandardErrorEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var testo = p.StandardError.ReadToEnd();
            p.WaitForExit(8000);
            var audio = false;
            foreach (var riga in testo.Split('\n'))
            {
                if (riga.Contains("DirectShow audio", StringComparison.OrdinalIgnoreCase)) audio = true;
                else if (riga.Contains("DirectShow video", StringComparison.OrdinalIgnoreCase)) audio = false;
                if (!audio) continue;
                var a = riga.IndexOf('"');
                var b = a >= 0 ? riga.IndexOf('"', a + 1) : -1;
                if (a >= 0 && b > a) return riga[(a + 1)..b];
            }
        }
        catch (Exception e) { Registro.Scrivi($"microfono: {e.Message}"); }
        return null;
    }
}
