using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Wpf;

namespace DaP.Convertitore.App;

/// <summary>
/// La finestra del lettore: nera, senza cornice, si ricorda dove l'avevi messa e quanto grande. Lo schermo intero
/// lo chiede la pagina (F, F11, doppio clic sul video) e qui la finestra copre tutto il monitor.
/// </summary>
public sealed class FinestraLettore : Window
{
    readonly WebView2 vista = new();
    public PonteLettore Ponte { get; }
    Rect? primaDelloSchermo;
    WindowState statoPrima;

    public FinestraLettore(IReadOnlyList<string> file)
    {
        Title = Path.GetFileName(file[0]) + " · DaP Convertitore";
        Background = new SolidColorBrush(Color.FromRgb(0x07, 0x06, 0x0A));
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
        vista.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 7, 6, 10);
        vista.Margin = new Thickness(4);
        Content = vista;
        MinWidth = 520;
        MinHeight = 380;
        Posiziona();

        Ponte = new PonteLettore(this, vista, file);
        SourceInitialized += (_, _) => Ambiente.Windows11(Maniglia);
        Loaded += async (_, _) => await Avvia();
        StateChanged += (_, _) => vista.Margin = new Thickness(WindowState == WindowState.Maximized || primaDelloSchermo is not null ? 0 : 4);
        Closing += (_, _) => { Ricorda(); Ponte.Chiudi(); };
    }

    async Task Avvia()
    {
        try
        {
            var env = await Ambiente.Prendi();
            await vista.EnsureCoreWebView2Async(env);
            Ambiente.Prepara(vista.CoreWebView2);
            Ponte.Collega(env);
            vista.CoreWebView2.ContainsFullScreenElementChanged += (_, _) => SchermoIntero(vista.CoreWebView2.ContainsFullScreenElement);
            vista.CoreWebView2.Navigate(Ambiente.Pagina("lettore.html"));
        }
        catch (Exception e)
        {
            Registro.Errore("WebView2 lettore", e);
            MessageBox.Show("Per il lettore serve Microsoft Edge WebView2 Runtime (su Windows 11 c'è già).\n\n" + e.Message, "DaP Convertitore", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    public IntPtr Maniglia => new WindowInteropHelper(this).Handle;

    public void Massimizza() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>Tutto il monitor, barra delle applicazioni compresa; all'uscita torna com'era.</summary>
    public void SchermoIntero(bool si)
    {
        if (si == (primaDelloSchermo is not null)) return;
        if (si)
        {
            statoPrima = WindowState;
            primaDelloSchermo = new Rect(Left, Top, Width, Height);
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            vista.Margin = new Thickness(0);
            Topmost = true;
            Ambiente.SuTuttoLoSchermo(Maniglia);
        }
        else
        {
            var r = primaDelloSchermo!.Value;
            primaDelloSchermo = null;
            Topmost = false;
            ResizeMode = ResizeMode.CanResize;
            Left = r.Left; Top = r.Top; Width = r.Width; Height = r.Height;
            WindowState = statoPrima;
            vista.Margin = new Thickness(WindowState == WindowState.Maximized ? 0 : 4);
        }
    }

    /// <summary>La misura dell'ultima volta (se sta ancora su uno schermo), se no grande al centro.</summary>
    void Posiziona()
    {
        var lavoro = SystemParameters.WorkArea;
        var imp = Impostazioni.Comune;
        WindowStartupLocation = WindowStartupLocation.Manual;
        if (imp.Lettore is { Length: 4 } r && r[2] >= MinWidth && r[3] >= MinHeight
            && r[0] + 80 < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth && r[0] + r[2] > SystemParameters.VirtualScreenLeft + 80
            && r[1] >= SystemParameters.VirtualScreenTop - 10 && r[1] + 60 < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            Left = r[0]; Top = r[1]; Width = r[2]; Height = r[3];
        }
        else
        {
            Width = Math.Min(1280, lavoro.Width * 0.86);
            Height = Math.Min(820, lavoro.Height * 0.86);
            Left = lavoro.Left + (lavoro.Width - Width) / 2;
            Top = lavoro.Top + (lavoro.Height - Height) / 2;
        }
        if (imp.LettoreGrande) Loaded += (_, _) => WindowState = WindowState.Maximized;
    }

    void Ricorda()
    {
        try
        {
            var imp = Impostazioni.Comune;
            var r = primaDelloSchermo ?? (WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds);
            imp.Lettore = [Math.Round(r.Left), Math.Round(r.Top), Math.Round(r.Width), Math.Round(r.Height)];
            imp.LettoreGrande = (primaDelloSchermo is not null ? statoPrima : WindowState) == WindowState.Maximized;
            imp.Salva();
        }
        catch { }
    }
}
