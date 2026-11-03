namespace DaP.Convertitore.Lettore;

/// <summary>Una voce di un archivio, per l'albero del lettore.</summary>
public sealed record VoceArchivio(string Percorso, long Peso, bool Cartella, DateTime? Data);
