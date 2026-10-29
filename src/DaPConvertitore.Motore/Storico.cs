using System.Text.Json;

namespace DaP.Convertitore;

/// <summary>Una conversione finita, come la ricorda la home.</summary>
public sealed record VoceStorico(DateTime Quando, string Nome, string Formato, string Uscita, long PesoPrima, long PesoDopo, string Categoria);

/// <summary>I totali di sempre: quante conversioni e quanti byte sono stati risparmiati (solo dove il file è diventato più leggero).</summary>
public sealed record TotaliStorico(int Conversioni, long ByteRisparmiati);

/// <summary>
/// Le ultime conversioni riuscite e i totali, in storico.json nella cartella dati. Serve alla home («Ultimi convertiti»).
/// Solo l'app lo scrive: le prove non lo toccano (la coda lo chiama solo se l'app glielo chiede).
/// </summary>
public static class Storico
{
    const int Massimo = 40;
    static readonly object chiave = new();
    static string Percorso => Path.Combine(Strumenti.CartellaDati, "storico.json");

    sealed class Dati
    {
        public int Conversioni { get; set; }
        public long ByteRisparmiati { get; set; }
        public List<VoceStorico> Voci { get; set; } = [];
    }

    static Dati Leggi()
    {
        try
        {
            if (File.Exists(Percorso)) return JsonSerializer.Deserialize<Dati>(File.ReadAllText(Percorso), Opzioni.Json) ?? new();
        }
        catch (Exception e) { Registro.Errore("storico", e); }
        return new();
    }

    /// <summary>Registra un lavoro riuscito. Non fa mai fallire la conversione.</summary>
    public static void Aggiungi(Lavoro l)
    {
        try
        {
            if (l.Uscita is null || l.Stato != StatoLavoro.Fatto) return;
            lock (chiave)
            {
                var d = Leggi();
                var nome = l.Sorgenti.Count == 1 ? Path.GetFileName(l.Sorgente.TrimEnd('\\')) : $"{l.Sorgenti.Count} file";
                var dopo = l.PesoDopo ?? 0;
                d.Voci.Insert(0, new VoceStorico(DateTime.Now, nome, l.Formato.Etichetta, l.Uscita, l.PesoPrima, dopo, Catalogo.Chiave(l.Formato.Categoria)));
                if (d.Voci.Count > Massimo) d.Voci.RemoveRange(Massimo, d.Voci.Count - Massimo);
                d.Conversioni++;
                if (l.Formato.Sostituisce && l.PesoPrima > dopo && dopo > 0) d.ByteRisparmiati += l.PesoPrima - dopo;
                var tmp = Percorso + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(d, new JsonSerializerOptions(Opzioni.Json) { WriteIndented = true }));
                File.Move(tmp, Percorso, true);
            }
        }
        catch (Exception e) { Registro.Errore("storico: salva", e); }
    }

    /// <summary>Le ultime <paramref name="quante"/> conversioni il cui risultato c'è ancora, e i totali.</summary>
    public static (List<VoceStorico> Recenti, TotaliStorico Totali) Recenti(int quante)
    {
        lock (chiave)
        {
            var d = Leggi();
            var vive = d.Voci.Where(v => File.Exists(v.Uscita) || Directory.Exists(v.Uscita)).Take(quante).ToList();
            return (vive, new TotaliStorico(d.Conversioni, d.ByteRisparmiati));
        }
    }
}
