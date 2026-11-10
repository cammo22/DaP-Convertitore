using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// Mette una finestra accanto all'avatar, nel monitor dove sta lui, in pixel veri.
/// <see cref="SystemParameters.WorkArea"/> è solo il principale e in unità diverse dai pixel:
/// con un avatar su un altro monitor, o con un DPI diverso, la finestra finiva fuori dallo schermo.
/// </summary>
public static class PosizioneFinestre
{
    const uint MonitorVicino = 2;
    static readonly Dictionary<Window, int> giri = new();

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFO mi);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr dopo, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool PhysicalToLogicalPointForPerMonitorDPI(IntPtr hwnd, ref POINT pt);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmonitor, int tipo, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Accanto ad <paramref name="ancora"/>, o al centro del monitor principale se l'avatar è spento.
    /// <paramref name="dipLarghezza"/> e <paramref name="dipAltezza"/> sono la misura voluta, in DIP:
    /// sul monitor dell'avatar diventano pixel con il DPI di quel monitor, e se non ci stanno si riducono.
    /// </summary>
    public static void Sistema(Window finestra, Window? ancora, double dipLarghezza, double dipAltezza, bool scrivi)
    {
        var h = new WindowInteropHelper(finestra).Handle;
        if (h == IntPtr.Zero) return;
        try
        {
            var (monitor, area) = AreaDi(ancora, h);
            var dpi = DpiDi(monitor, h);
            var pxL = Math.Max(80, (int)Math.Round(dipLarghezza * dpi / 96.0));
            var pxA = Math.Max(60, (int)Math.Round(dipAltezza * dpi / 96.0));
            Ancoraggio.Riquadro dove;
            if (ancora is not null && ancora.IsVisible && GetWindowRect(new WindowInteropHelper(ancora).Handle, out var ar))
            {
                var box = new Ancoraggio.Riquadro(ar.Left, ar.Top, Math.Max(1, ar.Right - ar.Left), Math.Max(1, ar.Bottom - ar.Top));
                dove = Ancoraggio.Accanto(box, pxL, pxA, area);
            }
            else dove = Ancoraggio.Centro(pxL, pxA, area);
            // prima i pixel veri, così la finestra è già sul monitor giusto quando WPF legge i DPI
            Metti(h, dove);
            var logica = Logica(h, dove.X, dove.Y, dpi);
            finestra.Left = logica.X;
            finestra.Top = logica.Y;
            var dipW = dove.L * 96.0 / dpi;
            var dipH = dove.A * 96.0 / dpi;
            if (Math.Abs(finestra.Width - dipW) > 1) finestra.Width = dipW;
            if (Math.Abs(finestra.Height - dipH) > 1) finestra.Height = dipH;
            // WPF, scrivendo Left e Width, può rimandare la finestra. L'ultima parola sono i pixel veri.
            var x = dove.X; var y = dove.Y; var l = dove.L; var a = dove.A;
            var mio = giri[finestra] = giri.GetValueOrDefault(finestra) + 1;
            finestra.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (giri.GetValueOrDefault(finestra) != mio) return;
                if (new WindowInteropHelper(finestra).Handle == h) Metti(h, new Ancoraggio.Riquadro(x, y, l, a));
            });
            if (scrivi)
                Registro.Scrivi($"registrazione: finestra a {dove.X},{dove.Y} ({dove.L}×{dove.A} px, dpi {dpi}) dentro {area.X},{area.Y} {area.L}×{area.A}");
        }
        catch (Exception e) { Registro.Errore("registrazione: posizione", e); }
    }

    static (IntPtr Monitor, Ancoraggio.Riquadro Area) AreaDi(Window? ancora, IntPtr finestra)
    {
        IntPtr mon;
        if (ancora is not null && ancora.IsVisible)
        {
            var ha = new WindowInteropHelper(ancora).Handle;
            mon = ha != IntPtr.Zero ? MonitorFromWindow(ha, MonitorVicino) : MonitorPrincipale();
        }
        else mon = MonitorPrincipale();
        if (mon == IntPtr.Zero) mon = MonitorFromWindow(finestra, MonitorVicino);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref info))
        {
            var w = info.rcWork;
            return (mon, new Ancoraggio.Riquadro(w.Left, w.Top, Math.Max(1, w.Right - w.Left), Math.Max(1, w.Bottom - w.Top)));
        }
        var s = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        return (mon, new Ancoraggio.Riquadro(s.X, s.Y, s.Width, s.Height));
    }

    static IntPtr MonitorPrincipale()
    {
        var s = System.Windows.Forms.Screen.PrimaryScreen;
        if (s is null) return MonitorFromPoint(new POINT { X = 1, Y = 1 }, 1);
        var b = s.Bounds;
        return MonitorFromPoint(new POINT { X = b.X + Math.Max(1, b.Width / 2), Y = b.Y + Math.Max(1, b.Height / 2) }, MonitorVicino);
    }

    static uint DpiDi(IntPtr monitor, IntPtr hwnd)
    {
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0, out var x, out _) == 0 && x >= 96) return x;
        var d = GetDpiForWindow(hwnd);
        return d >= 96 ? d : 96;
    }

    static void Metti(IntPtr h, Ancoraggio.Riquadro dove) =>
        SetWindowPos(h, new IntPtr(-1), dove.X, dove.Y, dove.L, dove.A, 0x0010);

    /// <summary>WPF, appena può, riscrive Left e Width. L'ultima parola sono i pixel veri, ma non nel passaggio di render: lì il cursore sfarfalla.</summary>
    static void MettiEConferma(Window finestra, IntPtr h, Ancoraggio.Riquadro dove)
    {
        Metti(h, dove);
        var mio = giri[finestra] = giri.GetValueOrDefault(finestra) + 1;
        var x = dove.X; var y = dove.Y; var l = dove.L; var a = dove.A;
        finestra.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (giri.GetValueOrDefault(finestra) != mio) return;
            if (new WindowInteropHelper(finestra).Handle == h) Metti(h, new Ancoraggio.Riquadro(x, y, l, a));
        });
    }

    static Point Logica(IntPtr hwnd, int x, int y, uint dpi)
    {
        var pt = new POINT { X = x, Y = y };
        if (PhysicalToLogicalPointForPerMonitorDPI(hwnd, ref pt)) return new Point(pt.X, pt.Y);
        return new Point(x * 96.0 / dpi, y * 96.0 / dpi);
    }

    /// <summary>Le aree di lavoro, in pixel veri, un rettangolo per monitor.</summary>
    public static IReadOnlyList<Ancoraggio.Riquadro> Schermi()
    {
        var lista = new List<Ancoraggio.Riquadro>();
        try
        {
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                var w = s.WorkingArea;
                if (w.Width > 2 && w.Height > 2)
                    lista.Add(new Ancoraggio.Riquadro(w.X, w.Y, w.Width, w.Height));
            }
        }
        catch { /* senza l'elenco si lascia la finestra dov'è */ }
        return lista;
    }

    public static Ancoraggio.Riquadro? Leggi(Window finestra)
    {
        var h = new WindowInteropHelper(finestra).Handle;
        if (h == IntPtr.Zero || !GetWindowRect(h, out var r)) return null;
        var l = r.Right - r.Left;
        var a = r.Bottom - r.Top;
        if (l < 2 || a < 2) return null;
        return new Ancoraggio.Riquadro(r.Left, r.Top, l, a);
    }

    /// <summary>Mette la finestra in quei pixel e, se sborda, la riporta dentro uno schermo.</summary>
    public static void Applica(Window finestra, Ancoraggio.Riquadro dove)
    {
        var h = new WindowInteropHelper(finestra).Handle;
        if (h == IntPtr.Zero) return;
        var schermi = Schermi();
        if (schermi.Count > 0) dove = Ancoraggio.DentroSchermi(dove, schermi);
        MettiEConferma(finestra, h, dove);
    }

    /// <summary>
    /// Accanto all'avatar, tenendo la misura che la finestra ha già (così un ridimensionamento non si perde).
    /// Se non ha ancora una misura, si usano i pixel di partenza.
    /// </summary>
    public static void Accanto(Window finestra, Window? ancora, int pixelLarghezza, int pixelAltezza)
    {
        var h = new WindowInteropHelper(finestra).Handle;
        if (h == IntPtr.Zero) return;
        try
        {
            var letto = Leggi(finestra);
            var l = letto is { L: > 80 } r ? r.L : pixelLarghezza;
            var a = letto is { A: > 40 } r2 ? r2.A : pixelAltezza;
            var (monitor, area) = AreaDi(ancora, h);
            _ = monitor;
            Ancoraggio.Riquadro dove;
            if (ancora is not null && ancora.IsVisible && GetWindowRect(new WindowInteropHelper(ancora).Handle, out var ar))
            {
                var box = new Ancoraggio.Riquadro(ar.Left, ar.Top, Math.Max(1, ar.Right - ar.Left), Math.Max(1, ar.Bottom - ar.Top));
                dove = Ancoraggio.Accanto(box, l, a, area);
            }
            else dove = Ancoraggio.Centro(l, a, area);
            var schermi = Schermi();
            if (schermi.Count > 0) dove = Ancoraggio.DentroSchermi(dove, schermi);
            else dove = Ancoraggio.Blocca(dove, area);
            MettiEConferma(finestra, h, dove);
        }
        catch (Exception e) { Registro.Errore("registrazione: posizione", e); }
    }

    public static void Dimentica(Window finestra) => giri.Remove(finestra);
}
