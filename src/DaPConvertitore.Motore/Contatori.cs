namespace DaP.Convertitore;

/// <summary>
/// Una fotografia delle risorse del processo. L'app la scrive nel registro ogni cinque minuti:
/// se GDI, USER o gli handle crescono, si vede, invece di accorgersene quando il cursore sfarfalla.
/// </summary>
public readonly record struct Contatori(int Gdi, int User, int Handle, long MemoriaByte, int Thread)
{
    public string Riga() =>
        $"risorse: gdi {Gdi}, user {User}, handle {Handle}, ram {MemoriaByte / (1024 * 1024)} MB, thread {Thread}";
}

public static class PerditaRisorse
{
    /// <summary>Tanta roba in più rispetto a cinque minuti fa: non è il respiro normale del programma.</summary>
    public static bool Sospetta(Contatori prima, Contatori dopo) =>
        dopo.Gdi - prima.Gdi >= 400 || dopo.User - prima.User >= 200 || dopo.Handle - prima.Handle >= 800;

    public static string Riga(Contatori prima, Contatori adesso)
    {
        var testo = adesso.Riga();
        return Sospetta(prima, adesso) ? testo + " · in crescita" : testo;
    }
}

/// <summary>I processi esterni che abbiamo aperto (FFmpeg della registrazione). Si segnano e si tolgono.</summary>
public static class Figli
{
    static readonly HashSet<int> pids = [];

    public static void Segna(int pid)
    {
        if (pid <= 0) return;
        lock (pids) pids.Add(pid);
    }

    public static void Tolto(int pid)
    {
        lock (pids) pids.Remove(pid);
    }

    public static int Quanti()
    {
        lock (pids) return pids.Count;
    }

    public static void Azzera()
    {
        lock (pids) pids.Clear();
    }
}
