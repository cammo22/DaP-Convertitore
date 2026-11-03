namespace DaP.Convertitore;

/// <summary>
/// Quante righe di testo sta in una miniatura. <c>FormattedText.MaxLineCount</c> di WPF
/// lancia ArgumentOutOfRangeException se gli si dà 0 o meno (è successo con i .docx):
/// 0 qui vuol dire «quante ce ne stanno nell'altezza», che per WPF è <see cref="int.MaxValue"/>.
/// </summary>
public static class RigheTesto
{
    public static int Massimo(int richiesto) => richiesto > 0 ? richiesto : int.MaxValue;
}
