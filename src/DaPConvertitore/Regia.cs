using System.Windows;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// Chi apre cosa. Un processo solo per tutto: la piastra del convertitore (una) e il lettore. Il doppio clic su
/// una foto apre il lettore; se è già aperto ci passa dentro, come fa Foto. Più file scelti insieme finiscono
/// nello stesso lettore, uno dopo l'altro. Quando si chiude l'ultima finestra, si chiude l'app.
/// </summary>
public static class Regia
{
    static Application app = null!;
    static Finestra? convertitore;
    static readonly List<FinestraLettore> lettori = [];
    static readonly List<string> daGuardare = [];
    static DispatcherTimer? raccolta;

    public static void Avvia(Application a) => app = a;

    public static Finestra? Convertitore => convertitore;

    public static void Gestisci(Richiesta r)
    {
        if (r.Guarda)
        {
            // Esplora file lancia un processo per file: si raccolgono per un attimo, poi un lettore solo
            daGuardare.AddRange(r.Percorsi.Where(File.Exists));
            raccolta ??= new DispatcherTimer(TimeSpan.FromMilliseconds(220), DispatcherPriority.Normal, (_, _) => Guarda(), app.Dispatcher);
            raccolta.Stop();
            raccolta.Start();
            return;
        }
        if (convertitore is null)
        {
            convertitore = new Finestra(r);
            convertitore.Closed += (_, _) => { convertitore = null; ForseFine(); };
            convertitore.Show();
            convertitore.Activate();
        }
        else convertitore.Ponte.Gestisci(r);
    }

    static void Guarda()
    {
        raccolta?.Stop();
        var file = daGuardare.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        daGuardare.Clear();
        if (file.Count == 0) { ForseFine(); return; }
        var l = lettori.LastOrDefault();
        if (l is null)
        {
            l = new FinestraLettore(file);
            lettori.Add(l);
            var questo = l;
            l.Closed += (_, _) => { lettori.Remove(questo); ForseFine(); };
            l.Show();
        }
        else l.Ponte.Apri(file);
        if (l.WindowState == WindowState.Minimized) l.WindowState = WindowState.Normal;
        l.Activate();
    }

    /// <summary>Dal lettore al convertitore: il file entra nella piastra, con tutte le scelte.</summary>
    public static void Converti(string percorso) => Gestisci(new Richiesta(null, [percorso], true));

    /// <summary>Dal convertitore al lettore.</summary>
    public static void ApriNelLettore(string percorso) => Gestisci(new Richiesta(null, [percorso], false, true));

    static void ForseFine()
    {
        if (convertitore is null && lettori.Count == 0 && daGuardare.Count == 0) app.Shutdown();
    }
}
