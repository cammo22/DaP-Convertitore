using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// La barra compatta: tempo, pausa, salva, annulla, e il tasto per riaprire il pannello.
/// Si trascina, si stacca dall'avatar e resta dentro gli schermi. È opaca ed esclusa dalla cattura.
/// </summary>
public sealed class BarraRegistrazione : Window
{
    static readonly SolidColorBrush Fondo = Fermo(0x12, 0x10, 0x18);
    static readonly SolidColorBrush Oro = Fermo(0xFF, 0xD5, 0x4A);
    static readonly Brush PuntoRosso = Fermo(0xFF, 0x3D, 0x6E);
    static readonly Brush PuntoPausa = Fermo(0xA1, 0x9D, 0xB0);

    static BarraRegistrazione? aperta;
    readonly TextBlock tempo;
    readonly Button pausa;
    readonly Ellipse punto;
    readonly DispatcherTimer orologio;
    Window? ancora;
    bool chiusuraVoluta;
    bool sistemo;

    public static bool Visibile => aperta is { IsVisible: true };

    public static void Mostra()
    {
        if (aperta is not null) { PortaDavanti(); return; }
        try
        {
            aperta = new BarraRegistrazione();
            Registrazione.Escludi(aperta);
            aperta.Closed += (_, _) =>
            {
                if (aperta is not null) PosizioneFinestre.Dimentica(aperta);
                aperta = null;
                TastiRegistrazione.Sistema();
            };
            aperta.Show();
            TastiRegistrazione.Accendi();
        }
        catch (Exception e)
        {
            aperta = null;
            Registro.Errore("registrazione: barra", e);
            Vassoio.Dire("La barra della registrazione non si è aperta", e.Message);
        }
    }

    public static void Chiudi()
    {
        var f = aperta;
        if (f is null) return;
        f.chiusuraVoluta = true;
        f.Close();
    }

    public static void PortaDavanti()
    {
        if (aperta is null) Mostra();
        else
        {
            aperta.Show();
            aperta.Topmost = true;
            try { aperta.Activate(); } catch { /* già davanti */ }
        }
    }

    public static void Aggiorna() => aperta?.Rinfresca();

    BarraRegistrazione()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Fondo;
        Title = "Registrazione";
        MinWidth = 280;

        punto = new Ellipse { Width = 10, Height = 10, Fill = PuntoRosso, VerticalAlignment = VerticalAlignment.Center };
        tempo = new TextBlock
        {
            Text = "00:00", Foreground = Oro, FontFamily = new FontFamily("Consolas, Segoe UI"),
            FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 64,
            Margin = new Thickness(8, 0, 12, 0),
        };
        pausa = Tasto("Pausa", Fermo(0x3A, 0x35, 0x48), () => MenuRegistrazione.TastoPausa());
        var riga = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 10, 8) };
        riga.Children.Add(punto);
        riga.Children.Add(tempo);
        riga.Children.Add(pausa);
        riga.Children.Add(Tasto("Salva", Fermo(0x1C, 0x9A, 0x62), Registrazione.Salva));
        riga.Children.Add(Tasto("Annulla", Fermo(0x8A, 0x2A, 0x3C), Registrazione.Annulla));
        riga.Children.Add(Tasto("Apri", Fermo(0x3A, 0x35, 0x48), () => MenuRegistrazione.PortaDavanti()));
        Content = new Border { Background = Fondo, BorderBrush = Oro, BorderThickness = new Thickness(1), Child = riga };

        MouseLeftButtonDown += (_, e) =>
        {
            if (DentroTasto(e.OriginalSource)) return;
            var imp = Impostazioni.Comune;
            imp.RegistrazioneAgganciata = false;
            imp.Salva();
            try { DragMove(); } catch { /* il tasto è già partito */ }
            if (PosizioneFinestre.Leggi(this) is { } r) PosizioneFinestre.Applica(this, r);
            Ricorda();
        };

        orologio = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        orologio.Tick += (_, _) => { if (IsVisible) Rinfresca(); };
        SourceInitialized += (_, _) => Sistema(false);
        Loaded += (_, _) =>
        {
            Aggancia(Registrazione.Ancora);
            Sistema(true);
            if (IsVisible) orologio.Start();
            Rinfresca();
        };
        IsVisibleChanged += (_, _) => { if (IsVisible) orologio.Start(); else orologio.Stop(); };
        Closed += (_, _) =>
        {
            orologio.Stop();
            if (ancora is not null) ancora.LocationChanged -= Segui;
        };
    }

    void Aggancia(Window? a)
    {
        if (ancora is not null) ancora.LocationChanged -= Segui;
        ancora = a;
        if (ancora is not null) ancora.LocationChanged += Segui;
    }

    void Segui(object? sender, EventArgs e)
    {
        if (Impostazioni.Comune.RegistrazioneAgganciata) Sistema(false);
    }

    void Sistema(bool scrivi)
    {
        if (sistemo) return;
        sistemo = true;
        try
        {
            UpdateLayout();
            var imp = Impostazioni.Comune;
            if (!imp.RegistrazioneAgganciata && Ancoraggio.ProvaChiave(imp.RegistrazioneBarra, out var salvata))
                PosizioneFinestre.Applica(this, salvata);
            else
            {
                var w = ActualWidth > 8 ? (int)ActualWidth : 420;
                var h = ActualHeight > 8 ? (int)ActualHeight : 48;
                PosizioneFinestre.Accanto(this, ancora ?? Registrazione.Ancora, w, h);
            }
            if (scrivi && PosizioneFinestre.Leggi(this) is { } r)
                Registro.Scrivi($"registrazione: barra a {r.X},{r.Y} ({r.L}×{r.A} px)");
        }
        finally { sistemo = false; }
    }

    void Ricorda()
    {
        if (PosizioneFinestre.Leggi(this) is not { } r) return;
        var imp = Impostazioni.Comune;
        imp.RegistrazioneBarra = Ancoraggio.Chiave(r);
        imp.Salva();
    }

    void Rinfresca()
    {
        var testo = Registrazione.Attiva ? Registrazione.TestoTempo() : "00:00";
        if (tempo.Text != testo) tempo.Text = testo;
        var ferma = !Registrazione.Attiva || Registrazione.InPausa;
        var etichetta = !Registrazione.Attiva ? "Avvia" : Registrazione.InPausa ? "Riprendi" : "Pausa";
        if (!Equals(pausa.Content, etichetta)) pausa.Content = etichetta;
        punto.Fill = ferma ? PuntoPausa : PuntoRosso;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!chiusuraVoluta && Registrazione.Attiva)
        {
            e.Cancel = true;
            MenuRegistrazione.PortaDavanti();
            return;
        }
        base.OnClosing(e);
    }

    static bool DentroTasto(object? o)
    {
        var d = o as DependencyObject;
        while (d is not null)
        {
            if (d is Button) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    static SolidColorBrush Fermo(byte r, byte g, byte b)
    {
        var p = new SolidColorBrush(Color.FromRgb(r, g, b));
        p.Freeze();
        return p;
    }

    static Button Tasto(string testo, Brush sfondo, Action fai)
    {
        var b = new Button
        {
            Content = testo, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(12, 6, 12, 6),
            Background = sfondo, Foreground = Brushes.White, BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 13, Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => fai();
        return b;
    }
}
