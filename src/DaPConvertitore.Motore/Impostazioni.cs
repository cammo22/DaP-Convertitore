using System.Text.Json;
using System.Text.Json.Nodes;

namespace DaP.Convertitore;

/// <summary>Le preferenze, in impostazioni.json nella cartella dati. Si ricordano anche le ultime scelte fatte.</summary>
public sealed class Impostazioni
{
    /// <summary>La parola fra parentesi nel nome: "foto (convertito).jpg".</summary>
    public string Scritta { get; set; } = "convertito";
    /// <summary>La voce "DaP Convertitore" nel tasto destro.</summary>
    public bool Menu { get; set; } = true;
    /// <summary>FFmpeg a priorità normale invece che bassa: più veloce, ma il PC si sente.</summary>
    public bool AlMassimo { get; set; }
    public bool Suoni { get; set; } = true;
    /// <summary>Alla fine apre la cartella con il file selezionato.</summary>
    public bool ApriCartella { get; set; }
    /// <summary>L'ultimo formato scelto per ogni categoria ("video" → "video.mp4").</summary>
    public Dictionary<string, string> Formati { get; set; } = [];
    /// <summary>Le ultime scelte dell'interfaccia (codec, qualità, motore…), tali e quali.</summary>
    public JsonObject? Scelte { get; set; }

    static string Percorso => Path.Combine(Strumenti.CartellaDati, "impostazioni.json");

    public static Impostazioni Carica()
    {
        try
        {
            if (File.Exists(Percorso))
                return JsonSerializer.Deserialize<Impostazioni>(File.ReadAllText(Percorso), Opzioni.Json) ?? new();
        }
        catch (Exception e) { Registro.Errore("impostazioni", e); }
        return new();
    }

    public void Salva()
    {
        try
        {
            var tmp = Percorso + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions(Opzioni.Json) { WriteIndented = true }));
            File.Move(tmp, Percorso, true);
        }
        catch (Exception e) { Registro.Errore("salva impostazioni", e); }
    }

    /// <summary>La scritta pulita: niente caratteri che Windows non vuole nei nomi.</summary>
    public string ScrittaPulita()
    {
        var s = new string((Scritta ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c is not '(' and not ')').ToArray()).Trim();
        return s.Length > 40 ? s[..40] : s;
    }
}
