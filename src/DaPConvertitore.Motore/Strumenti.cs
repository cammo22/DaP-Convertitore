using Microsoft.Win32;

namespace DaP.Convertitore;

/// <summary>
/// Dove stanno gli attrezzi: FFmpeg (viaggia con l'app), LibreOffice e Office (se ci sono),
/// tar di Windows per gli archivi. E le cartelle dove l'app scrive le sue cose.
/// </summary>
public sealed class Strumenti
{
    public required string Ffmpeg { get; init; }
    public required string Ffprobe { get; init; }
    public string? LibreOffice { get; init; }
    public bool Word { get; init; }
    public bool Excel { get; init; }
    public bool PowerPoint { get; init; }
    /// <summary>7z.exe (con 7z.dll accanto): il motore di tutti gli archivi. Viaggia con l'app.</summary>
    public string? SetteZip { get; init; }
    public string Tar { get; init; } = Path.Combine(Environment.SystemDirectory, "tar.exe");

    /// <summary>%LocalAppData%\DaProd\Convertitore: impostazioni, cache dell'hardware, registro, profilo di LibreOffice.</summary>
    public static string CartellaDati
    {
        get
        {
            // DAP_DATI sposta tutto altrove: le prove e le foto per il README non toccano i dati veri
            var c = Environment.GetEnvironmentVariable("DAP_DATI") is { Length: > 0 } altrove
                ? altrove
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaProd", "Convertitore");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    public static string CartellaTemporanea
    {
        get
        {
            var c = Path.Combine(Path.GetTempPath(), "DaP Convertitore");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    public static Strumenti Trova()
    {
        var ffmpeg = CercaFfmpeg();
        return new Strumenti
        {
            Ffmpeg = ffmpeg,
            Ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", "ffprobe.exe"),
            SetteZip = CercaSetteZip(),
            LibreOffice = CercaLibreOffice(),
            Word = ComRegistrato("Word.Application"),
            Excel = ComRegistrato("Excel.Application"),
            PowerPoint = ComRegistrato("PowerPoint.Application"),
        };
    }

    static string CercaFfmpeg()
    {
        // accanto all'exe (installato), poi su per le cartelle fino al repo (sviluppo e prove)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var d = dir; d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "motori", "ffmpeg", "ffmpeg.exe");
            if (File.Exists(p)) return p;
        }
        foreach (var cartella in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(cartella.Trim(), "ffmpeg.exe");
            if (File.Exists(p)) return p;
        }
        return Path.Combine(AppContext.BaseDirectory, "motori", "ffmpeg", "ffmpeg.exe");
    }

    static string? CercaSetteZip()
    {
        // accanto all'exe (installato), poi su per le cartelle fino al repo (sviluppo e prove)
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "motori", "7zip", "7z.exe");
            if (File.Exists(p) && File.Exists(Path.Combine(d.FullName, "motori", "7zip", "7z.dll"))) return p;
        }
        // in sviluppo, se il 7-Zip del PC c'è, va bene lo stesso
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
        return File.Exists(pf) ? pf : null;
    }

    static string? CercaLibreOffice()
    {
        foreach (var vista in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var base_ = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, vista);
                using var k = base_.OpenSubKey(@"SOFTWARE\LibreOffice\UNO\InstallPath");
                if (k?.GetValue(null) is string dir)
                {
                    var p = Path.Combine(dir, "soffice.exe");
                    if (File.Exists(p)) return p;
                }
            }
            catch { /* chiave non leggibile: si prova la prossima */ }
        }
        foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var p = Path.Combine(pf, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static bool ComRegistrato(string progId)
    {
        try
        {
            using var k = Registry.ClassesRoot.OpenSubKey(progId + @"\CLSID");
            return k is not null;
        }
        catch { return false; }
    }

    public bool HaSetteZip => SetteZip is not null && File.Exists(SetteZip);
    public bool HaFfmpeg => File.Exists(Ffmpeg) && File.Exists(Ffprobe);
    public bool HaOffice => LibreOffice is not null || Word || Excel || PowerPoint;
}
