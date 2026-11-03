namespace DaP.Convertitore;

/// <summary>
/// Cosa il ponte fra la pagina e il motore deve scrivere nel registro.
/// Una stima annullata perché ne è partita un'altra (lo slider della foto) non è un errore.
/// </summary>
public static class EccezioniPonte
{
    public static bool DaRegistro(Exception e) => e is not OperationCanceledException;
}
