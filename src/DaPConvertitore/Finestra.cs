using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Wpf;

namespace DaP.Convertitore.App;

/// <summary>
/// La finestra: senza cornice di Windows (la barra la disegna l'interfaccia), angoli tondi e bordo scuro di
/// Windows 11, e dentro WebView2 con l'interfaccia. Due misure: grande (la piastra) e rapida (la finestrella in
/// basso a destra che compare dal menu del tasto destro).
/// </summary>
public sealed class Finestra : Window
{
    readonly WebView2 vista = new();
    public Ponte Ponte { get; }
    public string Modo { get; private set; }
    bool chiusuraPronta;

    public Finestra(Richiesta prima)
    {
        Title = "DaP Convertitore";
        Background = new SolidColorBrush(Color.FromRgb(0x09, 0x08, 0x0D));
        WindowStyle = WindowStyle.None;
        try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icona.ico")); } catch { }
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });
        TaskbarItemInfo = new TaskbarItemInfo();
        vista.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 9, 8, 13);
        Content = vista;

        Ponte = new Ponte(this, vista);
        Modo = prima.Azione is not null && !prima.Finestra ? "rapido" : "finestra";
        Misura(Modo);
        // i file di chi ci ha lanciato entrano subito, prima di quelli che arrivano dagli altri processi
        Ponte.Gestisci(prima);

        SourceInitialized += (_, _) => Ambiente.Windows11(Maniglia);
        Loaded += async (_, _) => await Avvia();
        Closing += async (_, e) =>
        {
            if (chiusuraPronta || !Ponte.Coda.Occupata) { Ponte.Chiudi(); return; }
            // si annulla tutto e si aspetta che le bozze siano buttate: niente file a metà
            e.Cancel = true;
            Ponte.Coda.AnnullaTutto();
            for (var i = 0; i < 50 && Ponte.Coda.Occupata; i++) await Task.Delay(100);
            chiusuraPronta = true;
            Close();
        };
    }

    async Task Avvia()
    {
        try
        {
            var env = await Ambiente.Prendi();
            await vista.EnsureCoreWebView2Async(env);
            Ambiente.Prepara(vista.CoreWebView2);
            Ponte.Collega(env);
            vista.CoreWebView2.Navigate(Ambiente.Pagina("index.html"));
        }
        catch (Exception e)
        {
            Registro.Errore("WebView2", e);
            MessageBox.Show("Per l'interfaccia serve Microsoft Edge WebView2 Runtime (su Windows 11 c'è già).\n\n" + e.Message, "DaP Convertitore", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    /// <summary>Grande al centro, o rapida in basso a destra sopra la barra delle applicazioni.</summary>
    public void Misura(string modo)
    {
        Modo = modo;
        var lavoro = SystemParameters.WorkArea;
        if (modo == "rapido")
        {
            ResizeMode = ResizeMode.NoResize;
            MinWidth = 0; MinHeight = 0;
            Width = 480; Height = 190;
            Left = lavoro.Right - Width - 16;
            Top = lavoro.Bottom - Height - 16;
            vista.Margin = new Thickness(0);
        }
        else
        {
            var eraRapida = ResizeMode == ResizeMode.NoResize;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 880; MinHeight = 600;
            Width = Math.Min(1120, lavoro.Width - 40);
            Height = Math.Min(740, lavoro.Height - 40);
            if (eraRapida || !IsLoaded || WindowStartupLocation == WindowStartupLocation.Manual)
            {
                Left = lavoro.Left + (lavoro.Width - Width) / 2;
                Top = lavoro.Top + (lavoro.Height - Height) / 2;
            }
            // i bordi per ridimensionare stanno fuori da WebView2: se no se li prende lei
            vista.Margin = new Thickness(4);
        }
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    public void Massimizza() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>La barra delle applicazioni fa da barra d'avanzamento, come quando copi i file.</summary>
    public void Avanzamento(double? frazione, bool errore = false)
    {
        var t = TaskbarItemInfo;
        if (frazione is null) { t.ProgressState = TaskbarItemProgressState.None; return; }
        t.ProgressState = errore ? TaskbarItemProgressState.Error : frazione < 0 ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal;
        t.ProgressValue = Math.Clamp(frazione.Value, 0, 1);
    }

    public IntPtr Maniglia => new WindowInteropHelper(this).Handle;
}
