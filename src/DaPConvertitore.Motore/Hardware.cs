using System.Text.Json;
using Microsoft.Win32;

namespace DaP.Convertitore;

public sealed record Gpu(string Nome, string Marca, long Vram, string Driver);

public sealed record InfoHardware(string Cpu, int Thread, IReadOnlyList<Gpu> Gpu, IReadOnlyList<string> Encoder)
{
    public bool Ha(string encoder) => Encoder.Contains(encoder);

    /// <summary>La scheda che lavora davvero, per l'etichetta in alto: "RTX 4060 · NVENC".</summary>
    public string? Acceleratore =>
        Ha("hevc_nvenc") || Ha("h264_nvenc") ? $"{Corto(Gpu.FirstOrDefault(g => g.Marca == "nvidia")?.Nome)} · NVENC"
        : Ha("hevc_amf") || Ha("h264_amf") ? $"{Corto(Gpu.FirstOrDefault(g => g.Marca == "amd")?.Nome)} · AMF"
        : Ha("hevc_qsv") || Ha("h264_qsv") ? $"{Corto(Gpu.FirstOrDefault(g => g.Marca == "intel")?.Nome)} · Quick Sync"
        : null;

    static string Corto(string? nome) => (nome ?? "GPU")
        .Replace("NVIDIA ", "").Replace("GeForce ", "").Replace("AMD ", "").Replace("(TM)", "").Replace("Intel(R) ", "").Trim();
}

/// <summary>
/// Cosa c'è nel PC. Gli encoder si provano davvero (tre fotogrammi neri ciascuno, in parallelo):
/// un driver vecchio o una scheda spenta non si scoprono a metà di un video da due ore.
/// Il risultato si tiene in hardware.json finché non cambiano FFmpeg o i driver.
/// </summary>
public static class Hardware
{
    static readonly string[] daProvare =
    [
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_amf", "hevc_amf", "av1_amf",
        "h264_qsv", "hevc_qsv", "av1_qsv",
    ];

    /// <summary>Quelli della CPU ci sono sempre nella nostra build di FFmpeg.</summary>
    static readonly string[] software = ["libx264", "libx265", "libsvtav1", "libvpx-vp9", "prores_ks"];

    public static async Task<InfoHardware> Rileva(Strumenti s, bool rifai = false)
    {
        var gpu = SchedeVideo();
        var cpu = NomeCpu();
        var firma = $"{FirmaFfmpeg(s)}|{string.Join(";", gpu.Select(g => g.Nome + g.Driver))}";
        var cache = Path.Combine(Strumenti.CartellaDati, "hardware.json");

        if (!rifai && File.Exists(cache))
        {
            try
            {
                var salvato = JsonSerializer.Deserialize<Salvato>(File.ReadAllText(cache));
                if (salvato?.Firma == firma) return new InfoHardware(cpu, Environment.ProcessorCount, gpu, salvato.Encoder);
            }
            catch { /* cache rovinata: si rifà */ }
        }

        var prove = daProvare
            .Where(e => gpu.Any(g => e.EndsWith("nvenc") ? g.Marca == "nvidia" : e.EndsWith("amf") ? g.Marca == "amd" : g.Marca == "intel"))
            .Select(async e => (e, ok: await Prova(s, e)));
        var esiti = await Task.WhenAll(prove);
        var encoder = esiti.Where(x => x.ok).Select(x => x.e).Concat(software).ToList();
        try { File.WriteAllText(cache, JsonSerializer.Serialize(new Salvato(firma, encoder))); } catch { }
        Registro.Scrivi($"Hardware: {cpu} · {string.Join(", ", gpu.Select(g => g.Nome))} · encoder {string.Join(" ", encoder)}");
        return new InfoHardware(cpu, Environment.ProcessorCount, gpu, encoder);
    }

    sealed record Salvato(string Firma, List<string> Encoder);

    static async Task<bool> Prova(Strumenti s, string encoder)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var r = await Processi.Esegui(s.Ffmpeg,
                ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=black:s=256x144:r=10",
                 "-frames:v", "3", "-c:v", encoder, "-f", "null", "-"], cts.Token, tieniUscita: false);
            return r.Codice == 0;
        }
        catch { return false; }
    }

    static string FirmaFfmpeg(Strumenti s)
    {
        try { var fi = new FileInfo(Path.Combine(Path.GetDirectoryName(s.Ffmpeg)!, "avcodec-63.dll")); return fi.Exists ? $"{fi.Length}-{fi.LastWriteTimeUtc.Ticks}" : s.Ffmpeg; }
        catch { return s.Ffmpeg; }
    }

    /// <summary>Le schede video vere (le virtuali di Virtual Desktop, spacedesk & co. si saltano).</summary>
    public static List<Gpu> SchedeVideo()
    {
        var elenco = new List<Gpu>();
        try
        {
            using var classe = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classe is null) return elenco;
            foreach (var sotto in classe.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
            {
                using var k = classe.OpenSubKey(sotto);
                if (k?.GetValue("DriverDesc") is not string nome) continue;
                var marca = nome.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "nvidia"
                    : nome.Contains("AMD", StringComparison.OrdinalIgnoreCase) || nome.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ? "amd"
                    : nome.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "intel" : null;
                if (marca is null) continue;
                long vram = k.GetValue("HardwareInformation.qwMemorySize") switch
                {
                    long l => l,
                    byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                    byte[] b when b.Length >= 4 => BitConverter.ToUInt32(b, 0),
                    int i => (uint)i,
                    _ => 0,
                };
                var driver = k.GetValue("DriverVersion") as string ?? "";
                if (!elenco.Any(g => g.Nome == nome)) elenco.Add(new Gpu(nome, marca, vram, driver));
            }
        }
        catch (Exception e) { Registro.Errore("schede video", e); }
        // la NVIDIA davanti: è quella che di solito si vuole far lavorare
        return elenco.OrderBy(g => g.Marca switch { "nvidia" => 0, "amd" => 1, _ => 2 }).ToList();
    }

    public static string NomeCpu()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (k?.GetValue("ProcessorNameString") is string n) return n.Trim();
        }
        catch { }
        return "CPU";
    }
}
