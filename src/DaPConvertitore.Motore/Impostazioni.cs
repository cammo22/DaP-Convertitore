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
    /// <summary>
    /// A conversione riuscita l'originale va nel Cestino da solo. Di partenza è spento: l'originale si butta col tasto
    /// accanto al risultato. Il nome è nuovo apposta: la 1.0.2 salvava "cestino" acceso, qui si riparte da spento.
    /// </summary>
    public bool CestinoDaSolo { get; set; }
    /// <summary>"Apri con" di Windows: il lettore fra le app che aprono foto, video, musica, PDF…</summary>
    public bool ApriCon { get; set; } = true;
    /// <summary>L'invito a mettere il convertitore nel menu di Windows 11 è già stato mostrato (e chiuso).</summary>
    public bool Menu11Chiesto { get; set; }
    /// <summary>L'ultimo formato scelto per ogni categoria ("video" → "video.mp4").</summary>
    public Dictionary<string, string> Formati { get; set; } = [];
    /// <summary>Le ultime scelte dell'interfaccia (codec, qualità, motore…), tali e quali.</summary>
    public JsonObject? Scelte { get; set; }

    /// <summary>Dove stava il lettore l'ultima volta: sinistra, alto, larghezza, altezza.</summary>
    public double[]? Lettore { get; set; }
    public bool LettoreGrande { get; set; }

    static string Percorso => Path.Combine(Strumenti.CartellaDati, "impostazioni.json");

    static Impostazioni? comune;
    /// <summary>Le stesse per tutte le finestre dell'app: chi salva non cancella quello che ha cambiato un'altra.</summary>
    public static Impostazioni Comune => comune ??= Carica();

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
