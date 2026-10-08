using System.Windows.Media.Imaging;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.App.Miniature;

/// <summary>
/// Le miniature di Esplora file. La DLL DaPConvertitore.Miniature.dll (menu\DaPMiniature.cpp) lancia questo exe con
///   --miniatura «file» «lato» «uscita»
/// e rende a Esplora file i pixel che scriviamo in «uscita»: due interi (larghezza, altezza) e poi i pixel BGRA
/// premoltiplicati, riga per riga dall'alto. Codice d'uscita 0 se il disegno c'è, 1 se no (Esplora file si tiene l'icona).
/// Per guardarle a occhio: --miniatura-png «file» «lato» «uscita.png», e --icone «cartella» scrive le icone dei tipi.
/// </summary>
public static class Miniatura
{
    public static bool Richiesta(string[] args) => args.Length > 0 && args[0] is "--miniatura" or "--miniatura-png" or "--icone";

    [STAThread]
    public static int Esegui(string[] args)
    {
        // un guardiano: se qualcosa si pianta, dopo un po' si esce comunque (la DLL ha il suo tempo massimo, ma meglio non lasciare processi)
        var guardiano = new Thread(() => { Thread.Sleep(45_000); Environment.Exit(3); }) { IsBackground = true };
        guardiano.Start();
        try
        {
            if (args[0] == "--icone")
            {
                Icone.ScriviTutte(args.Length > 1 ? args[1] : Environment.CurrentDirectory, "prova");
                return 0;
            }
            if (args.Length < 4) return 2;
            var lato = int.TryParse(args[2], out var l) ? Math.Clamp(l, 16, 512) : 256;
            var quadro = Disegna(args[1], lato);
            if (quadro is null) return 1;
            if (args[0] == "--miniatura-png")
            {
                File.WriteAllBytes(args[3], Tela.Png(quadro));
                return 0;
            }
            using var f = File.Create(args[3]);
            using var w = new BinaryWriter(f);
            w.Write(quadro.Larghezza);
            w.Write(quadro.Altezza);
            w.Write(quadro.Pixel);
            return 0;
        }
        catch (Exception e)
        {
            Registro.Errore($"miniatura di {(args.Length > 1 ? Path.GetFileName(args[1]) : "?")}", e);
            return 1;
        }
    }

    /// <summary>Il disegno per un file, o null se non c'è niente da mostrare.</summary>
    public static Quadro? Disegna(string percorso, int lato)
    {
        if (!File.Exists(percorso)) return null;
        var est = Path.GetExtension(percorso);
        var tipo = Vista.Tipo(percorso);
        var gruppo = Vista.Gruppo(est);
        var strumenti = new Lazy<Strumenti>(Strumenti.Trova);
        return tipo switch
        {
            "video" => strumenti.Value.HaFfmpeg ? Multimedia.Video(percorso, lato, strumenti.Value) : null,
            "audio" => strumenti.Value.HaFfmpeg ? Multimedia.Audio(percorso, lato, strumenti.Value) : null,
            "modello" => Multimedia.Modello(percorso, lato),
            "immagine" => Multimedia.Foto(percorso, lato, strumenti.Value),
            "pdf" => Documenti.Pdf(percorso, lato),
            "documento" or "presentazione" => Documenti.Ufficio(percorso, lato, gruppo),
            "tabella" when est.Equals(".csv", StringComparison.OrdinalIgnoreCase) || est.Equals(".tsv", StringComparison.OrdinalIgnoreCase)
                => Documenti.Testo(percorso, lato, gruppo, tipo),
            "tabella" => Documenti.Ufficio(percorso, lato, "Tabella"),
            "font" => Documenti.Carattere(percorso, lato) ?? Documenti.Generica("Font", est, lato),
            "archivio" => Documenti.Archivio(percorso, lato) ?? Documenti.Generica("Archivio", est, lato),
            "codice" or "markdown" or "web" or "json" or "testo" or "sottotitoli" => Documenti.Testo(percorso, lato, gruppo, tipo),
            _ => Documenti.Generica(gruppo, est, lato),
        };
    }
}
