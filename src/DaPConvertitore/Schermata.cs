using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using SD = System.Drawing;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace DaP.Convertitore.App;

/// <summary>
/// Lo screenshot, come ShareX e solo quello: scorciatoia globale, schermo fermo e scurito, la zona
/// (o la finestra sotto il cursore), le misure, la lente, poi il file nella cartella e un'anteprima.
/// </summary>
public static class Schermata
{
    const int WmHotkey = 0x0312;
    const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000;
    const int DwmExtended = 9, DwmCloaked = 14;

    static HwndSource? gancio;
    static int idCaldo = 1;
    static FinestraCattura? aperta;

    public static void Avvia()
    {
        if (gancio is not null) return;
        var p = new HwndSourceParameters("DaP scorciatoia")
        {
            Width = 0, Height = 0,
            WindowStyle = unchecked((int)0x80000000),
        };
        gancio = new HwndSource(p);
        gancio.AddHook(Messaggio);
        ApplicaScorciatoia();
    }

    public static void ApplicaScorciatoia()
    {
        if (gancio is null) return;
        UnregisterHotKey(gancio.Handle, idCaldo);
        var (mod, vk) = Leggi(Impostazioni.Comune.ScorciatoiaSchermo);
        if (vk == 0) return;
        if (!RegisterHotKey(gancio.Handle, idCaldo, mod | ModNoRepeat, vk))
            Registro.Scrivi("La scorciatoia dello screenshot è già presa da un altro programma.");
    }

    public static void Cattura()
    {
        if (aperta is not null) return;
        try
        {
            var dpi = DpiDi();
            var foto = Scatta(dpi.X, dpi.Y);
            aperta = new FinestraCattura(foto, dpi.X, dpi.Y);
            aperta.Closed += (_, _) => aperta = null;
            aperta.Show();
        }
        catch (Exception e)
        {
            Registro.Errore("screenshot", e);
            Vassoio.Dire("Screenshot non fatto", e.Message);
        }
    }

    static (double X, double Y) DpiDi()
    {
        var w = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsVisible);
        if (w is null) return (1, 1);
        var d = VisualTreeHelper.GetDpi(w);
        return (d.DpiScaleX <= 0 ? 1 : d.DpiScaleX, d.DpiScaleY <= 0 ? 1 : d.DpiScaleY);
    }

    static BitmapSource Scatta(double dpiX, double dpiY)
    {
        int x = (int)Math.Round(SystemParameters.VirtualScreenLeft * dpiX);
        int y = (int)Math.Round(SystemParameters.VirtualScreenTop * dpiY);
        int w = Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenWidth * dpiX));
        int h = Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenHeight * dpiY));
        using var bmp = new SD.Bitmap(w, h, SD.Imaging.PixelFormat.Format32bppArgb);
        using (var g = SD.Graphics.FromImage(bmp))
            g.CopyFromScreen(x, y, 0, 0, bmp.Size, SD.CopyPixelOperation.SourceCopy);
        var hb = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(hb, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally { DeleteObject(hb); }
    }

    static (uint Mod, uint Vk) Leggi(string? testo)
    {
        var t = string.IsNullOrWhiteSpace(testo) ? "Ctrl+Maiusc+S" : testo!;
        uint mod = 0, vk = 0;
        foreach (var parte in t.Split('+'))
        {
            var p = parte.Trim();
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase)) mod |= ModControl;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) mod |= ModAlt;
            else if (p.Equals("Maiusc", StringComparison.OrdinalIgnoreCase) || p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) mod |= ModShift;
            else vk = Virtuale(p);
        }
        return (mod, vk);
    }

    static uint Virtuale(string tasto)
    {
        if (tasto.Length == 1)
        {
            var c = char.ToUpperInvariant(tasto[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c;
        }
        if (tasto.Length is >= 2 and <= 3 && tasto[0] is 'F' or 'f' && int.TryParse(tasto[1..], out var n) && n is >= 1 and <= 24)
            return (uint)(0x70 + n - 1);
        return tasto.ToLowerInvariant() switch
        {
            "spazio" or "space" => 0x20,
            "invio" or "enter" => 0x0D,
            "tab" => 0x09,
            _ => 0,
        };
    }

    static IntPtr Messaggio(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == idCaldo)
        {
            handled = true;
            Application.Current?.Dispatcher.BeginInvoke(Cattura);
        }
        return IntPtr.Zero;
    }

    sealed class FinestraCattura : Window
    {
        readonly BitmapSource foto;
        readonly double dpiX, dpiY;
        readonly Grid piano = new();
        readonly System.Windows.Controls.Image chiara;
        readonly Rectangle zona = new() { Stroke = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)), StrokeThickness = 2, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        readonly Rectangle finestra = new() { Stroke = new SolidColorBrush(Color.FromRgb(0xff, 0x3d, 0xf2)), StrokeThickness = 2, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        readonly Border lente;
        readonly System.Windows.Controls.Image zoom = new() { Stretch = Stretch.Fill };
        readonly TextBlock misure = new() { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13, IsHitTestVisible = false };
        readonly List<Rect> finestre;
        bool tira;
        Point origine;
        Rect scelta;

        public FinestraCattura(BitmapSource foto, double dpiX, double dpiY)
        {
            this.foto = foto;
            this.dpiX = dpiX;
            this.dpiY = dpiY;
            finestre = Elenca(dpiX, dpiY);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Background = Brushes.Black;
            Cursor = Cursors.Cross;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // scurimento leggero, come ShareX: la foto resta chiara, la zona scelta è a piena luce
            piano.Children.Add(new System.Windows.Controls.Image { Source = foto, Stretch = Stretch.Fill, Opacity = 0.72, IsHitTestVisible = false });
            chiara = new System.Windows.Controls.Image { Source = foto, Stretch = Stretch.Fill, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            piano.Children.Add(chiara);
            piano.Children.Add(finestra);
            piano.Children.Add(zona);
            lente = new Border
            {
                Width = 150, Height = 150, CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)), BorderThickness = new Thickness(2),
                Background = Brushes.Black, IsHitTestVisible = false, Child = zoom,
                Visibility = Visibility.Collapsed,
            };
            piano.Children.Add(lente);
            misure.Background = new SolidColorBrush(Color.FromArgb(200, 12, 10, 18));
            misure.Padding = new Thickness(6, 2, 6, 2);
            piano.Children.Add(misure);
            Content = piano;

            MouseMove += Muovi;
            MouseLeftButtonDown += Premi;
            MouseLeftButtonUp += Lascia;
            MouseRightButtonDown += (_, _) => Close();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            Opacity = 0;
            Loaded += (_, _) =>
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
                Focus();
            };
        }

        void Muovi(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(piano);
            Lente(p);
            if (tira)
            {
                finestra.Visibility = Visibility.Collapsed;
                scelta = Normale(origine, p);
                MostraZona(scelta);
                return;
            }
            var sotto = finestre.Where(r => r.Contains(p)).OrderBy(r => r.Width * r.Height).FirstOrDefault();
            if (sotto.Width > 1)
            {
                finestra.Visibility = Visibility.Visible;
                CanvasNonServe(finestra, sotto);
                Misura((int)Math.Round(sotto.Width * dpiX), (int)Math.Round(sotto.Height * dpiY), sotto);
            }
            else finestra.Visibility = Visibility.Collapsed;
        }

        void Premi(object sender, MouseButtonEventArgs e)
        {
            tira = true;
            origine = e.GetPosition(piano);
            CaptureMouse();
            finestra.Visibility = Visibility.Collapsed;
        }

        void Lascia(object sender, MouseButtonEventArgs e)
        {
            if (!tira) return;
            tira = false;
            ReleaseMouseCapture();
            var p = e.GetPosition(piano);
            Rect r;
            if ((p - origine).Length < 6)
            {
                var sotto = finestre.Where(x => x.Contains(p)).OrderBy(x => x.Width * x.Height).FirstOrDefault();
                if (sotto.Width < 2) { Close(); return; }
                r = sotto;
            }
            else r = Normale(origine, p);
            if (r.Width < 2 || r.Height < 2) { Close(); return; }
            Salva(r);
            Close();
        }

        void MostraZona(Rect r)
        {
            zona.Visibility = Visibility.Visible;
            chiara.Visibility = Visibility.Visible;
            chiara.Clip = new RectangleGeometry(r);
            CanvasNonServe(zona, r);
            Misura((int)Math.Round(r.Width * dpiX), (int)Math.Round(r.Height * dpiY), r);
        }

        void Misura(int w, int h, Rect r)
        {
            misure.Text = $"{w} × {h}";
            misure.Visibility = Visibility.Visible;
            var x = Math.Min(r.Left, Width - 120);
            var y = r.Bottom + 8;
            if (y + 28 > Height) y = Math.Max(0, r.Top - 28);
            misure.Margin = new Thickness(Math.Max(0, x), Math.Max(0, y), 0, 0);
            misure.HorizontalAlignment = HorizontalAlignment.Left;
            misure.VerticalAlignment = VerticalAlignment.Top;
        }

        void Lente(Point p)
        {
            int cx = (int)Math.Round(p.X * dpiX);
            int cy = (int)Math.Round(p.Y * dpiY);
            const int lato = 48;
            int x = Math.Clamp(cx - lato / 2, 0, Math.Max(0, foto.PixelWidth - lato));
            int y = Math.Clamp(cy - lato / 2, 0, Math.Max(0, foto.PixelHeight - lato));
            int w = Math.Min(lato, foto.PixelWidth - x);
            int h = Math.Min(lato, foto.PixelHeight - y);
            if (w < 2 || h < 2) return;
            zoom.Source = new CroppedBitmap(foto, new Int32Rect(x, y, w, h));
            lente.Visibility = Visibility.Visible;
            var lx = p.X + 28;
            var ly = p.Y + 28;
            if (lx + 160 > Width) lx = p.X - 178;
            if (ly + 160 > Height) ly = p.Y - 178;
            lente.Margin = new Thickness(Math.Max(0, lx), Math.Max(0, ly), 0, 0);
            lente.HorizontalAlignment = HorizontalAlignment.Left;
            lente.VerticalAlignment = VerticalAlignment.Top;
        }

        void Salva(Rect r)
        {
            int x = Math.Clamp((int)Math.Round(r.X * dpiX), 0, foto.PixelWidth - 1);
            int y = Math.Clamp((int)Math.Round(r.Y * dpiY), 0, foto.PixelHeight - 1);
            int w = Math.Clamp((int)Math.Round(r.Width * dpiX), 1, foto.PixelWidth - x);
            int h = Math.Clamp((int)Math.Round(r.Height * dpiY), 1, foto.PixelHeight - y);
            var taglio = new CroppedBitmap(foto, new Int32Rect(x, y, w, h));
            taglio.Freeze();
            var cartella = CartellaLavori.Assicura();
            var percorso = Path.Combine(cartella, $"schermo {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png");
            for (var n = 2; File.Exists(percorso); n++)
                percorso = Path.Combine(cartella, $"schermo {DateTime.Now:yyyy-MM-dd HH-mm-ss} {n}.png");
            using (var fs = File.Create(percorso))
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(taglio));
                enc.Save(fs);
            }
            try { Clipboard.SetImage(taglio); } catch { /* gli appunti sono occupati */ }
            Anteprima(percorso, taglio);
            Vassoio.Dire("Screenshot", Path.GetFileName(percorso));
        }

        static void CanvasNonServe(Rectangle rett, Rect r)
        {
            rett.HorizontalAlignment = HorizontalAlignment.Left;
            rett.VerticalAlignment = VerticalAlignment.Top;
            rett.Margin = new Thickness(r.X, r.Y, 0, 0);
            rett.Width = r.Width;
            rett.Height = r.Height;
        }

        static Rect Normale(Point a, Point b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            return new Rect(x, y, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }

        static List<Rect> Elenca(double dpiX, double dpiY)
        {
            var lista = new List<Rect>();
            EnumWindows((h, _) =>
            {
                if (!IsWindowVisible(h)) return true;
                if (GetWindowTextLength(h) == 0) return true;
                DwmGetWindowAttribute(h, DwmCloaked, out int cloaked, 4);
                if (cloaked != 0) return true;
                if (DwmGetWindowAttribute(h, DwmExtended, out RECT r, Marshal.SizeOf<RECT>()) != 0 && !GetWindowRect(h, out r))
                    return true;
                var dip = new Rect(r.Left / dpiX, r.Top / dpiY, Math.Max(0, r.Right - r.Left) / dpiX, Math.Max(0, r.Bottom - r.Top) / dpiY);
                dip.Offset(-SystemParameters.VirtualScreenLeft, -SystemParameters.VirtualScreenTop);
                if (dip.Width >= 48 && dip.Height >= 48) lista.Add(dip);
                return true;
            }, IntPtr.Zero);
            return lista;
        }
    }

    static void Anteprima(string percorso, BitmapSource foto)
    {
        var w = new Window
        {
            Width = 280, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, ShowActivated = false, Opacity = 0,
        };
        var img = new System.Windows.Controls.Image { Source = foto, Height = 72, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        var nome = new TextBlock { Text = Path.GetFileName(percorso), Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis };
        var sotto = new TextBlock { Text = "Nella cartella · clic per aprirla", Foreground = new SolidColorBrush(Color.FromRgb(0xa1, 0x9d, 0xb0)), FontSize = 12 };
        var pila = new StackPanel { Margin = new Thickness(12) };
        pila.Children.Add(img);
        pila.Children.Add(nome);
        pila.Children.Add(sotto);
        w.Content = new Border
        {
            CornerRadius = new CornerRadius(12), Child = pila, Background = new SolidColorBrush(Color.FromArgb(230, 18, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 213, 74)), BorderThickness = new Thickness(1),
        };
        var area = SystemParameters.WorkArea;
        w.Left = area.Right - 300;
        w.Top = area.Bottom - 150;
        w.MouseLeftButtonUp += (_, _) => { Programma.Mostra(percorso); w.Close(); };
        w.Loaded += (_, _) => w.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        w.Show();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        t.Tick += (_, _) => { t.Stop(); if (w.IsVisible) w.Close(); };
        t.Start();
    }

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
}
