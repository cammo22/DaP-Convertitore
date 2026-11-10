using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace DaP.Convertitore.App;

/// <summary>
/// Si trascina un rettangolo sul monitor scelto. Non è esclusa dalla cattura e non è usata mentre si registra:
/// una finestra trasparente esclusa, su Windows 11, sparirebbe anche per chi la guarda.
/// </summary>
public static class SceltaZona
{
    public static Task<Ancoraggio.Riquadro?> Chiedi(Ancoraggio.Riquadro monitor)
    {
        var tcs = new TaskCompletionSource<Ancoraggio.Riquadro?>();
        var w = new Finestra(monitor, tcs);
        w.Show();
        return tcs.Task;
    }

    sealed class Finestra : Window
    {
        readonly Ancoraggio.Riquadro monitor;
        readonly TaskCompletionSource<Ancoraggio.Riquadro?> esito;
        readonly Rectangle segno = new()
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 255, 213, 74)),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        bool chiuso;
        int? x0, y0;

        public Finestra(Ancoraggio.Riquadro monitor, TaskCompletionSource<Ancoraggio.Riquadro?> esito)
        {
            this.monitor = monitor;
            this.esito = esito;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(90, 8, 6, 14));
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = true;
            Cursor = Cursors.Cross;
            Title = "Scegli la zona";
            var tela = new System.Windows.Controls.Canvas();
            tela.Children.Add(segno);
            var nota = new System.Windows.Controls.TextBlock
            {
                Text = "Trascina la zona. Esc annulla.",
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(16),
            };
            var griglia = new System.Windows.Controls.Grid();
            griglia.Children.Add(tela);
            griglia.Children.Add(nota);
            Content = griglia;
            SourceInitialized += (_, _) => MettiSulMonitor();
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Fine(null); };
            MouseLeftButtonDown += (_, _) =>
            {
                var p = Cursore();
                x0 = p.X; y0 = p.Y;
                CaptureMouse();
            };
            MouseMove += (_, _) =>
            {
                if (x0 is not int a) return;
                var p = Cursore();
                var r = Rettangolo(a, y0!.Value, p.X, p.Y);
                var s = Scala();
                segno.Margin = new Thickness((r.X - monitor.X) / s, (r.Y - monitor.Y) / s, 0, 0);
                segno.Width = Math.Max(1, r.L / s);
                segno.Height = Math.Max(1, r.A / s);
                segno.Visibility = Visibility.Visible;
            };
            MouseLeftButtonUp += (_, _) =>
            {
                if (x0 is not int a) return;
                ReleaseMouseCapture();
                var p = Cursore();
                var r = Rettangolo(a, y0!.Value, p.X, p.Y);
                x0 = y0 = null;
                if (r.L >= 16 && r.A >= 16) Fine(r);
            };
            Closed += (_, _) => { if (!chiuso) esito.TrySetResult(null); };
        }

        void MettiSulMonitor()
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowPos(h, new IntPtr(-1), monitor.X, monitor.Y, monitor.L, monitor.A, 0x0040);
        }

        double Scala()
        {
            var d = VisualTreeHelper.GetDpi(this).DpiScaleX;
            return d <= 0 ? 1 : d;
        }

        static Ancoraggio.Riquadro Rettangolo(int x1, int y1, int x2, int y2)
        {
            var x = Math.Min(x1, x2);
            var y = Math.Min(y1, y2);
            return new Ancoraggio.Riquadro(x, y, Math.Max(1, Math.Abs(x2 - x1)), Math.Max(1, Math.Abs(y2 - y1)));
        }

        void Fine(Ancoraggio.Riquadro? r)
        {
            if (chiuso) return;
            chiuso = true;
            esito.TrySetResult(r);
            Close();
        }

        static (int X, int Y) Cursore()
        {
            GetCursorPos(out var p);
            return (p.X, p.Y);
        }

        [DllImport("user32.dll")] static extern bool GetCursorPos(out PUNTO p);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr dopo, int x, int y, int cx, int cy, uint flags);
        [StructLayout(LayoutKind.Sequential)] struct PUNTO { public int X, Y; }
    }
}
