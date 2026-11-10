using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DaP.Convertitore.App;

/// <summary>
/// F9 mette in pausa (o fa partire, se il pannello è aperto), F10 salva.
/// È RegisterHotKey, non un hook WH_KEYBOARD_LL: un hook globale passa di qui a ogni tasto
/// e, se la funzione è lenta, il mouse e la tastiera di tutto il PC si mettono a scattare.
/// I tasti si registrano solo mentre il pannello è aperto o si sta registrando, e poi si tolgono.
/// </summary>
public static class TastiRegistrazione
{
    const int WmHotkey = 0x0312;
    const uint NoRepeat = 0x4000;
    const int IdPausa = 41;
    const int IdStop = 42;
    const uint VkF9 = 0x78;
    const uint VkF10 = 0x79;

    static HwndSource? gancio;
    static bool accesi;
    static bool detto;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    public static void Accendi()
    {
        if (accesi) return;
        try
        {
            if (gancio is null)
            {
                gancio = new HwndSource(new HwndSourceParameters("DaP registra tasti")
                {
                    Width = 0, Height = 0,
                    WindowStyle = unchecked((int)0x80000000),
                });
                gancio.AddHook(Messaggio);
            }
            var a = RegisterHotKey(gancio.Handle, IdPausa, NoRepeat, VkF9);
            var b = RegisterHotKey(gancio.Handle, IdStop, NoRepeat, VkF10);
            accesi = a || b;
            if ((!a || !b) && !detto)
            {
                detto = true;
                Registro.Scrivi("registrazione: F9 o F10 è già preso da un altro programma. I tasti sul pannello funzionano lo stesso.");
            }
        }
        catch (Exception e) { Registro.Errore("registrazione: tasti", e); }
    }

    public static void Spegni()
    {
        if (gancio is null) { accesi = false; return; }
        try
        {
            UnregisterHotKey(gancio.Handle, IdPausa);
            UnregisterHotKey(gancio.Handle, IdStop);
        }
        catch { /* già tolti */ }
        accesi = false;
    }

    /// <summary>Accesi se c'è il pannello, la barra o una registrazione. Spenti se non serve più niente.</summary>
    public static void Sistema()
    {
        if (Registrazione.Attiva || MenuRegistrazione.Visibile || BarraRegistrazione.Visibile) Accendi();
        else Spegni();
    }

    static IntPtr Messaggio(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        var id = wParam.ToInt32();
        if (id != IdPausa && id != IdStop) return IntPtr.Zero;
        handled = true;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (id == IdStop) { if (Registrazione.Attiva) Registrazione.Salva(); }
                else MenuRegistrazione.TastoPausa();
            }
            catch (Exception e) { Registro.Errore("registrazione: tasto", e); }
        });
        return IntPtr.Zero;
    }
}
