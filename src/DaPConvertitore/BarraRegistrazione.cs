using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// La barretta mentre si registra: tempo, pausa, salva, annulla. Sta sopra a tutto e non entra nella cattura.
/// </summary>
public sealed class BarraRegistrazione : Window
{
    static BarraRegistrazione? aperta;
    readonly TextBlock tempo;
    readonly Button pausa;
    readonly Ellipse punto;
    readonly DispatcherTimer orologio;
    Window? ancora;
    bool chiusuraVoluta;
    bool sistemo;

    public static void Mostra()
    {
        if (aperta is not null) { PortaDavanti(); return; }
        try
        {
            aperta = new BarraRegistrazione();
            Registrazione.Escludi(aperta);
            aperta.Closed += (_, _) => { if (aperta is not null) aperta = null; };
            aperta.Show();
            if (!aperta.IsVisible) Registro.Scrivi("registrazione: la barra non è visibile dopo Show()");
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
        aperta = null;
        f.chiusuraVoluta = true;
        f.Close();
    }

    public static void PortaDavanti()
    {
        if (aperta is null) Mostra();
        else
        {
            aperta.Topmost = true;
            try { aperta.Activate(); } catch { /* già davanti */ }
        }
    }

    public static void Aggiorna() => aperta?.Rinfresca();

    BarraRegistrazione()
    {
        // opaca, come il menu: una finestra trasparente esclusa dalla cattura su Windows 11 non si vede
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x18));
        Title = "Registrazione";
        MinWidth = 280;

        punto = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(0xff, 0x3d, 0x6e)), VerticalAlignment = VerticalAlignment.Center };
        tempo = new TextBlock
        {
            Text = "00:00",
            Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)),
            FontFamily = new FontFamily("Consolas, Segoe UI"),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 64,
        };
        pausa = Tasto("Pausa", "#3a3548", () => { if (Registrazione.InPausa) Registrazione.Riprendi(); else Registrazione.Pausa(); Rinfresca(); });
        var riga = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 10, 8) };
        riga.Children.Add(punto);
        riga.Children.Add(tempo);
        tempo.Margin = new Thickness(8, 0, 12, 0);
        riga.Children.Add(pausa);
        riga.Children.Add(Tasto("Salva", "#1c9a62", Registrazione.Salva));
        riga.Children.Add(Tasto("Annulla", "#8a2a3c", Registrazione.Annulla));
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x18)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)),
            BorderThickness = new Thickness(1),
            Child = riga,
        };
        MouseLeftButtonDown += (_, e) =>
        {
            if (DentroTasto(e.OriginalSource)) return;
            try { DragMove(); } catch { /* il tasto è già partito */ }
        };

        orologio = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        orologio.Tick += (_, _) => Rinfresca();
        SourceInitialized += (_, _) => Sistema(false);
        Loaded += (_, _) =>
        {
            UpdateLayout();
            Aggancia(Registrazione.Ancora);
            Sistema(true);
            orologio.Start();
            Rinfresca();
        };
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

    void Segui(object? sender, EventArgs e) => Sistema(false);

    void Sistema(bool scrivi)
    {
        if (sistemo) return;
        sistemo = true;
        try
        {
            UpdateLayout();
            var w = ActualWidth > 8 ? ActualWidth : 420;
            var h = ActualHeight > 8 ? ActualHeight : 48;
            PosizioneFinestre.Sistema(this, ancora ?? Registrazione.Ancora, w, h, scrivi);
        }
        finally { sistemo = false; }
    }

    void Rinfresca()
    {
        tempo.Text = Registrazione.TestoTempo();
        var ferma = Registrazione.InPausa;
        pausa.Content = ferma ? "Riprendi" : "Pausa";
        punto.Fill = new SolidColorBrush(ferma ? Color.FromRgb(0xa1, 0x9d, 0xb0) : Color.FromRgb(0xff, 0x3d, 0x6e));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!chiusuraVoluta && Registrazione.Attiva)
        {
            e.Cancel = true;
            Registrazione.Annulla();
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

    static Button Tasto(string testo, string sfondo, Action fai)
    {
        var b = new Button
        {
            Content = testo,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(12, 6, 12, 6),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sfondo)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => fai();
        return b;
    }
}
