using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaP.Convertitore;

/// <summary>
/// Le scelte di Cammo per un lavoro. Arrivano dall'interfaccia come JSON; quello che manca prende il valore di partenza.
/// Ogni motore legge solo il suo pezzo.
/// </summary>
public sealed class Opzioni
{
    public OpzioniVideo Video { get; set; } = new();
    public OpzioniAudio Audio { get; set; } = new();
    public OpzioniImmagine Immagine { get; set; } = new();
    public OpzioniPdf Pdf { get; set; } = new();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static Opzioni Da(JsonElement? e)
    {
        if (e is null || e.Value.ValueKind != JsonValueKind.Object) return new();
        try { return e.Value.Deserialize<Opzioni>(Json) ?? new(); }
        catch { return new(); }
    }

    public Opzioni Copia() => JsonSerializer.Deserialize<Opzioni>(JsonSerializer.Serialize(this, Json), Json)!;

    /// <summary>Le voci del menu rapido che non sono solo "il formato": metà peso, foto più piccola.</summary>
    public static Opzioni DaPreset(string? preset, long pesoSorgente)
    {
        var o = new Opzioni();
        switch (preset)
        {
            case "meta":
                o.Video.Modo = "peso";
                o.Video.PesoByte = Math.Max(1, pesoSorgente / 2);
                o.Video.Codec = "hevc";
                break;
            case "piccola":
                o.Immagine.Lato = 1920;
                o.Immagine.Qualita = 85;
                break;
        }
        return o;
    }
}

public sealed class OpzioniVideo
{
    /// <summary>"auto" (copia se si può, se no qualità), "peso", "qualita", "copia".</summary>
    public string Modo { get; set; } = "auto";
    /// <summary>Il peso finale voluto, in byte, per il modo "peso".</summary>
    public long PesoByte { get; set; }
    /// <summary>0..100 per il modo "qualita".</summary>
    public int Qualita { get; set; } = 70;
    /// <summary>h264, hevc, av1, vp9, prores. Se il contenitore non lo regge si prende il suo di partenza.</summary>
    public string Codec { get; set; } = "h264";
    /// <summary>Il lato corto voluto (1080, 720…). 0 = deciso da solo (originale, o più piccolo se serve per stare nel peso).</summary>
    public int Lato { get; set; }
    /// <summary>auto, nvidia, amd, intel, cpu.</summary>
    public string Motore { get; set; } = "auto";
    public bool SenzaAudio { get; set; }
    /// <summary>0 = deciso da solo.</summary>
    public int AudioKbps { get; set; }
    /// <summary>0 = come l'originale.</summary>
    public int Fps { get; set; }
    public int GifLarghezza { get; set; } = 480;
    public int GifFps { get; set; } = 15;
}

public sealed class OpzioniAudio
{
    /// <summary>0 = quello di partenza del formato.</summary>
    public int Kbps { get; set; }
    /// <summary>Volume uniforme (EBU R128, -14 LUFS come le piattaforme di musica).</summary>
    public bool Normalizza { get; set; }
    public bool Mono { get; set; }
    /// <summary>Per WAV, AIFF e FLAC: 16 o 24.</summary>
    public int Bit { get; set; } = 16;
}

public sealed class OpzioniImmagine
{
    public int Qualita { get; set; } = 88;
    /// <summary>Il lato lungo massimo in pixel. 0 = come l'originale.</summary>
    public int Lato { get; set; }
    /// <summary>Se &gt; 0, la qualità scende finché il file non ci sta (JPG, WEBP, AVIF, HEIC, JXL).</summary>
    public long PesoMaxByte { get; set; }
}

public sealed class OpzioniPdf
{
    /// <summary>Punti per pollice delle pagine trasformate in immagini.</summary>
    public int Dpi { get; set; } = 200;
}
