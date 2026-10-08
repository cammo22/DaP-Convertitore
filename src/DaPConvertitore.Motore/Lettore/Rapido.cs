using System.Diagnostics;

namespace DaP.Convertitore.Lettore;

/// <summary>
/// Un programma lanciato al volo di cui serve solo quello che scrive sull'uscita (un fotogramma JPEG da FFmpeg…).
/// Senza finestre, senza registro (le miniature partono a migliaia), con un tempo massimo: se scade si ferma tutto.
/// </summary>
public static class Rapido
{
    public static async Task<byte[]?> Uscita(string programma, IEnumerable<string> argomenti, TimeSpan massimo, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(programma)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = Path.GetDirectoryName(programma) ?? Environment.CurrentDirectory,
        };
        foreach (var a in argomenti) psi.ArgumentList.Add(a);
        using var p = new Process { StartInfo = psi };
        try { p.Start(); }
        catch { return null; }
        try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        p.StandardInput.Close();
        _ = p.StandardError.ReadToEndAsync();

        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(massimo);
        try
        {
            using var memoria = new MemoryStream();
            await p.StandardOutput.BaseStream.CopyToAsync(memoria, tempo.Token);
            await p.WaitForExitAsync(tempo.Token);
            return p.ExitCode == 0 && memoria.Length > 0 ? memoria.ToArray() : null;
        }
        catch (OperationCanceledException) { return null; }
        catch (IOException) { return null; }
        finally
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
        }
    }
}
