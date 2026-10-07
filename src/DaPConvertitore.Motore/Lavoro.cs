namespace DaP.Convertitore;

public enum StatoLavoro { Attesa, Corre, Fatto, Errore, Annullato }

/// <summary>A che punto è un lavoro. Velocità = quante volte il tempo reale (solo audio e video).</summary>
public sealed record Avanzamento(double Frazione, string? Fase = null, double? Velocita = null, double? Fps = null);

/// <summary>Chi sa trasformare una pagina HTML in PDF (nell'app è WebView2). Le prove ne fanno a meno.</summary>
public interface IStampante
{
    /// <summary>Apre la pagina (un file .html) e la stampa in PDF.</summary>
    Task Stampa(string fileHtml, string pdf, CancellationToken ct);
    /// <summary>Il testo della pagina come lo vedi (innerText).</summary>
    Task<string> Testo(string fileHtml, CancellationToken ct);
}

/// <summary>Una conversione: uno o più file d'ingresso, un formato, le scelte, e il posto dove esce.</summary>
public sealed class Lavoro
{
    public required string Id { get; init; }
    public required IReadOnlyList<string> Sorgenti { get; init; }
    public required Formato Formato { get; init; }
    public required Opzioni Opzioni { get; init; }
    /// <summary>L'id del file nell'interfaccia (il primo, se il lavoro ne unisce tanti).</summary>
    public string? FileId { get; init; }

    public StatoLavoro Stato { get; internal set; } = StatoLavoro.Attesa;
    public double Frazione { get; internal set; }
    public string? Fase { get; internal set; }
    public double? Velocita { get; internal set; }
    public double? Fps { get; internal set; }
    public double? Eta { get; internal set; }
    public string? Uscita { get; internal set; }
    public long PesoPrima { get; internal set; }
    public long? PesoDopo { get; internal set; }
    public string? Errore { get; internal set; }
    public string? Dettaglio { get; internal set; }
    public DateTime Inizio { get; internal set; }
    public double Secondi { get; internal set; }

    /// <summary>Dove il motore scrive (accanto all'uscita, col ".~" davanti). Alla fine diventa <see cref="Uscita"/>.</summary>
    public string Bozza { get; internal set; } = "";

    internal CancellationTokenSource Annullo { get; } = new();

    public string Sorgente => Sorgenti[0];
}

/// <summary>Tutto quello che serve a un motore per lavorare.</summary>
public sealed class Contesto(Strumenti strumenti, InfoHardware hardware, CancellationToken ct, Action<Avanzamento> riporta, IStampante? stampante = null)
{
    public Strumenti Strumenti { get; } = strumenti;
    public InfoHardware Hardware { get; } = hardware;
    public CancellationToken Ct { get; } = ct;
    public IStampante? Stampante { get; } = stampante;
    readonly List<string> temporanee = [];

    public void Riporta(Avanzamento a) => riporta(a);
    public void Fase(string testo, double frazione = 0) => riporta(new Avanzamento(frazione, testo));

    /// <summary>Una cartella di lavoro che si butta da sola alla fine del lavoro.</summary>
    public string Temporanea()
    {
        var d = Path.Combine(Strumenti.CartellaTemporanea, Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(d);
        lock (temporanee) temporanee.Add(d);
        return d;
    }

    public void Pulisci()
    {
        lock (temporanee)
        {
            foreach (var d in temporanee)
                try { Directory.Delete(d, true); } catch { }
            temporanee.Clear();
        }
    }
}
