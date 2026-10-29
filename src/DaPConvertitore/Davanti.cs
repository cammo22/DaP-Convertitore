using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DaP.Convertitore.App;

/// <summary>
/// Portare una finestra davanti. Windows non lascia che un programma passi in primo piano da solo: lo permette solo a chi ha
/// appena ricevuto un'azione dell'utente (come il processo lanciato da Esplora file). Il processo che passa i file al primo
/// (<see cref="Istanza"/>) autorizza chiunque a farlo prima di chiudersi; la finestra poi usa il permesso, e se non basta si
/// aggancia per un attimo al thread di chi ha il primo piano.
/// </summary>
public static class Davanti
{
    const int SW_SHOW = 5, SW_RESTORE = 9;
    const uint ASFW_ANY = 0xFFFFFFFF;

    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processo);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int comando);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint processo);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint da, uint a, bool attacca);
    [DllImport("user32.dll")] static extern void keybd_event(byte tasto, byte scansione, uint flag, UIntPtr extra);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    /// <summary>Chiamato dal processo che sta per chiudersi: l'app già aperta può passare in primo piano.</summary>
    public static void Permetti()
    {
        try { AllowSetForegroundWindow(ASFW_ANY); } catch { /* senza il permesso la finestra lampeggia nella barra */ }
    }

    /// <summary>Riporta la finestra (anche ridotta a icona) davanti a tutte e le dà il fuoco.</summary>
    public static void Porta(Window w)
    {
        try
        {
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
            var h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero) return;
            if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
            // il trucco di Topmost mette la finestra sopra le altre senza lasciarcela
            w.Topmost = true;
            w.Topmost = false;
            if (SetForegroundWindow(h)) return;
            // il permesso non c'era: ci si aggancia al thread di chi ha il primo piano, il tempo di passare avanti
            var primoPiano = GetForegroundWindow();
            var thread = GetWindowThreadProcessId(primoPiano, out _);
            var mio = GetCurrentThreadId();
            if (thread != 0 && thread != mio && AttachThreadInput(mio, thread, true))
            {
                try { BringWindowToTop(h); ShowWindow(h, SW_SHOW); SetForegroundWindow(h); }
                finally { AttachThreadInput(mio, thread, false); }
            }
            // ultimo rimedio: un tocco del tasto Alt dice a Windows che c'è un'azione dell'utente, e allora lascia passare
            if (GetForegroundWindow() != h)
            {
                keybd_event(0xA4, 0, 0, UIntPtr.Zero);
                keybd_event(0xA4, 0, 2, UIntPtr.Zero);
                SetForegroundWindow(h);
            }
            w.Focus();
        }
        catch (Exception e) { Registro.Errore("davanti", e); }
    }
}
