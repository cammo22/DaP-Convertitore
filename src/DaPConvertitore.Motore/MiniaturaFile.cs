using System.Diagnostics;

namespace DaP.Convertitore;

/// <summary>Un fotogramma piccolo di un video (o la copertina di un audio) per i recenti della home.</summary>
public static class MiniaturaFile
{
    public static string? PngBase64(string percorso)
    {
        try
        {
            if (!File.Exists(percorso)) return null;
            var cat = Catalogo.CategoriaDi(percorso);
            if (cat is not (Categoria.Video or Categoria.Audio)) return null;
            var ff = Strumenti.Trova().Ffmpeg;
            if (!File.Exists(ff)) return null;
            var psi = new ProcessStartInfo(ff)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in ComandiRegistrazione.ArgomentiFotogramma(percorso)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            if (!p.WaitForExit(8000)) { try { p.Kill(entireProcessTree: true); } catch { /* è già fermo */ } }
            if (ms.Length < 32) return null;
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception e)
        {
            Registro.Scrivi($"miniatura di {Path.GetFileName(percorso)}: {e.Message}");
            return null;
        }
    }
}
