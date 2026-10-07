using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DaP.Convertitore.App;

/// <summary>
/// La finestra: senza cornice di Windows (la barra la disegna l'interfaccia), angoli tondi e bordo scuro di
/// Windows 11, e dentro WebView2 con l'interfaccia. Due misure: grande (la piastra) e rapida (la finestrella in
/// basso a destra che compare dal menu del tasto destro).
/// </summary>
public sealed class Finestra : Window
{
    public const string Host = "dap.locale";
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

        SourceInitialized += (_, _) => Windows11();
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
            var dati = Path.Combine(Strumenti.CartellaDati, "webview");
            var env = await CoreWebView2Environment.CreateAsync(null, dati, new CoreWebView2EnvironmentOptions { Language = "it-IT" });
            await vista.EnsureCoreWebView2Async(env);
            var w = vista.CoreWebView2;
            w.Settings.IsNonClientRegionSupportEnabled = true;
            w.Settings.AreDefaultContextMenusEnabled = false;
            w.Settings.IsStatusBarEnabled = false;
            w.Settings.IsZoomControlEnabled = false;
            w.Settings.AreBrowserAcceleratorKeysEnabled = false;
#if !DEBUG
            w.Settings.AreDevToolsEnabled = false;
#endif
            w.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
            // niente navigazione fuori dall'interfaccia: i link esterni si aprono nel browser, e solo dal ponte
            w.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith($"https://{Host}/")) e.Cancel = true; };
            w.NewWindowRequested += (_, e) => e.Handled = true;
            Ponte.Collega(env);
            var sviluppo = Environment.GetEnvironmentVariable("DAP_UI");
            w.Navigate(string.IsNullOrEmpty(sviluppo) ? $"https://{Host}/index.html" : sviluppo);
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

    // ——— Windows 11: tema scuro, angoli tondi, bordo ———
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    void Windows11()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int si = 1, tondo = 2, bordo = 0x00322A2A; // COLORREF è BGR: #2A2A32
        DwmSetWindowAttribute(hwnd, 20, ref si, sizeof(int));     // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(hwnd, 33, ref tondo, sizeof(int));  // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
        DwmSetWindowAttribute(hwnd, 34, ref bordo, sizeof(int));  // DWMWA_BORDER_COLOR
    }

    public IntPtr Maniglia => new WindowInteropHelper(this).Handle;
}
