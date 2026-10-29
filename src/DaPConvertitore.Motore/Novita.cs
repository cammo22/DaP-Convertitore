namespace DaP.Convertitore;

/// <summary>
/// Le novità di una versione: la sua voce del CHANGELOG, che viaggia dentro l'app (accanto all'exe; in sviluppo, nel repo).
/// Si mostrano dal tasto in alto e, una volta, alla prima apertura dopo un aggiornamento.
/// </summary>
public static class Novita
{
    public static string? Di(string versione)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "CHANGELOG.md");
            if (!File.Exists(p)) continue;
            var righe = File.ReadAllLines(p);
            var i = Array.FindIndex(righe, r => r.StartsWith("## ") && r.Contains(versione, StringComparison.Ordinal));
            if (i < 0) return null;
            var j = Array.FindIndex(righe, i + 1, r => r.StartsWith("## "));
            if (j < 0) j = righe.Length;
            return string.Join('\n', righe[(i + 1)..j]).Trim();
        }
        return null;
    }
}
