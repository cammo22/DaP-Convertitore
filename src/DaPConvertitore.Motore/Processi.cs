using System.Diagnostics;
using System.Text;

namespace DaP.Convertitore;

/// <summary>Una conversione andata storta, con un messaggio che si può far leggere a Cammo.</summary>
public sealed class ErroreConversione(string messaggio, string? dettaglio = null) : Exception(messaggio)
{
    public string? Dettaglio { get; } = dettaglio;
}

public sealed record EsitoProcesso(int Codice, string Uscita, string Errori);

/// <summary>
/// Lancia i programmi esterni (FFmpeg, LibreOffice, tar) senza finestre nere, a priorità bassa
/// così il PC resta usabile, e li ferma per davvero quando si annulla.
/// </summary>
public static class Processi
{
    /// <summary>Priorità dei lavori pesanti. "Spingi al massimo" nelle impostazioni la alza.</summary>
    public static ProcessPriorityClass Priorita { get; set; } = ProcessPriorityClass.BelowNormal;

    public static async Task<EsitoProcesso> Esegui(
        string programma,
        IEnumerable<string> argomenti,
        CancellationToken ct,
        Action<string>? riga = null,
        string? cartella = null,
        bool tieniUscita = true,
        Func<Process, Task>? quandoAnnulli = null,
        IReadOnlyDictionary<string, string>? ambiente = null,
        bool silenzioso = false)
    {
        var psi = new ProcessStartInfo(programma)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = cartella ?? Path.GetDirectoryName(programma) ?? Environment.CurrentDirectory,
        };
        foreach (var a in argomenti) psi.ArgumentList.Add(a);
        if (ambiente is not null) foreach (var (k, v) in ambiente) psi.Environment[k] = v;
        if (!silenzioso) Registro.Scrivi($"> {Path.GetFileName(programma)} {string.Join(' ', psi.ArgumentList.Select(Virgolette))}");

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var uscita = new StringBuilder();
        var errori = new Coda(60);
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (tieniUscita) lock (uscita) uscita.AppendLine(e.Data);
            riga?.Invoke(e.Data);
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) errori.Metti(e.Data); };

        try { p.Start(); }
        catch (Exception e) { throw new ErroreConversione($"Non riesco ad avviare {Path.GetFileName(programma)}.", e.Message); }
        try { p.PriorityClass = Priorita; } catch { /* già finito o non permesso */ }
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using (ct.Register(() => _ = Ferma(p, quandoAnnulli)))
        {
            await p.WaitForExitAsync(CancellationToken.None);
        }
        p.WaitForExit(); // svuota gli ultimi eventi di output
        ct.ThrowIfCancellationRequested();
        string testoUscita;
        lock (uscita) testoUscita = uscita.ToString();
        var r = new EsitoProcesso(p.ExitCode, testoUscita, errori.Testo());
        if (r.Codice != 0 && !silenzioso) Registro.Scrivi($"  codice {r.Codice}: {r.Errori}");
        return r;
    }

    static async Task Ferma(Process p, Func<Process, Task>? garbato)
    {
        try
        {
            if (p.HasExited) return;
            if (garbato is not null)
            {
                await garbato(p);
                await Task.WhenAny(p.WaitForExitAsync(), Task.Delay(1500));
                if (p.HasExited) return;
            }
            p.Kill(entireProcessTree: true);
        }
        catch { /* già chiuso */ }
    }

    static string Virgolette(string a) => a.Contains(' ') ? $"\"{a}\"" : a;

    /// <summary>Tiene solo le ultime righe: degli errori di FFmpeg contano quelle in fondo.</summary>
    sealed class Coda(int max)
    {
        readonly Queue<string> righe = new();
        public void Metti(string r) { lock (righe) { righe.Enqueue(r); while (righe.Count > max) righe.Dequeue(); } }
        public string Testo() { lock (righe) return string.Join('\n', righe); }
    }
}
