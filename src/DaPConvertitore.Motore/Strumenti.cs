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
    public string Tar { get; init; } = Path.Combine(Environment.SystemDirectory, "tar.exe");

    /// <summary>%LocalAppData%\DaProd\Convertitore: impostazioni, cache dell'hardware, registro, profilo di LibreOffice.</summary>
    public static string CartellaDati
    {
        get
        {
            var c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaProd", "Convertitore");
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

    public bool HaFfmpeg => File.Exists(Ffmpeg) && File.Exists(Ffprobe);
    public bool HaOffice => LibreOffice is not null || Word || Excel || PowerPoint;
}
