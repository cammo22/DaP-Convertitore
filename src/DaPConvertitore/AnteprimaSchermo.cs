using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// Lo schermo, dal vivo, dentro la finestra di registrazione. Pochi fotogrammi al secondo, rimpiccioliti:
/// è un'anteprima, non il video. BitBlt del desktop è SRCCOPY, mai CAPTUREBLT:
/// quel flag fa sparire e ridisegnare il cursore, e dopo un po' il mouse sfarfalla.
/// La finestra esclusa dalla cattura (WDA_EXCLUDEFROMCAPTURE) non entra nel fotogramma,
/// così non si vede l'effetto specchio. Il timer si ferma appena il pannello non si vede.
/// </summary>
public sealed class AnteprimaSchermo : IDisposable
{
    readonly Image img;
    readonly DispatcherTimer timer;
    WriteableBitmap? bmp;
    int x, y, w, h;
    int occupata;
    int serie;
    bool detto;

    public AnteprimaSchermo(Image img)
    {
        this.img = img;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
        timer.Tick += (_, _) => Scatta();
    }

    public void Guarda(int x, int y, int w, int h)
    {
        this.x = x; this.y = y; this.w = w; this.h = h;
        if (w > 1 && h > 1 && !timer.IsEnabled) timer.Start();
    }

    public void Ferma() { serie++; timer.Stop(); }

    public void Dispose() { serie++; timer.Stop(); }

    void Scatta()
    {
        if (!timer.IsEnabled || w < 2 || h < 2) return;
        if (Interlocked.Exchange(ref occupata, 1) == 1) return;
        var mia = serie;
        var (sx, sy, sw, sh) = (x, y, w, h);
        Task.Run(() => Cattura(sx, sy, sw, sh)).ContinueWith(t =>
        {
            var px = t.IsFaulted ? null : t.Result;
            img.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (mia != serie) return;
                    if (px is { } f) Dipingi(f);
                    else if (!detto && t.IsFaulted) { detto = true; Registro.Errore("anteprima schermo", t.Exception?.GetBaseException() ?? new Exception("vuota")); }
                }
                finally { Interlocked.Exchange(ref occupata, 0); }
            });
        });
    }

    void Dipingi((int L, int A, byte[] Pixel) f)
    {
        if (bmp is null || bmp.PixelWidth != f.L || bmp.PixelHeight != f.A)
        {
            bmp = new WriteableBitmap(f.L, f.A, 96, 96, PixelFormats.Bgra32, null);
            img.Source = bmp;
        }
        bmp.WritePixels(new Int32Rect(0, 0, f.L, f.A), f.Pixel, f.L * 4, 0);
    }

    static (int L, int A, byte[] Pixel)? Cattura(int x, int y, int w, int h)
    {
        var dw = Math.Min(480, w);
        var dh = Math.Max(1, (int)Math.Round(h * (dw / (double)w)));
        var hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return null;
        var mem = CreateCompatibleDC(hdc);
        var bmp = CreateCompatibleBitmap(hdc, dw, dh);
        var vecchio = SelectObject(mem, bmp);
        try
        {
            SetStretchBltMode(mem, 3);
            if (StretchBlt(mem, 0, 0, dw, dh, hdc, x, y, w, h, ComandiRegistrazione.RopAnteprima) == 0) return null;
            var info = new BITMAPINFO { biSize = 40, biWidth = dw, biHeight = -dh, biPlanes = 1, biBitCount = 32 };
            var pixel = new byte[dw * dh * 4];
            if (GetDIBits(mem, bmp, 0, (uint)dh, pixel, ref info, 0) == 0) return null;
            return (dw, dh, pixel);
        }
        finally
        {
            SelectObject(mem, vecchio);
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFO
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] static extern int StretchBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, int rop);
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFO info, uint usage);
}
