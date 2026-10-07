using System.Runtime.InteropServices;

namespace DaP.Convertitore;

/// <summary>
/// L'originale nel Cestino di Windows, mai cancellato per sempre: se il convertito non ti piace, lo ripeschi.
/// Solo sui dischi fissi: su chiavette, schede e dischi di rete Windows non ha il Cestino e cancellerebbe davvero,
/// quindi lì l'originale resta dov'è.
/// </summary>
public static class Cestino
{
    /// <summary>null se è andato nel Cestino, se no il perché è rimasto.</summary>
    public static string? Sposta(string percorso)
    {
        try
        {
            var radice = Path.GetPathRoot(Path.GetFullPath(percorso));
            if (string.IsNullOrEmpty(radice) || radice.StartsWith(@"\\")) return "è su un disco di rete, senza Cestino";
            if (new DriveInfo(radice).DriveType != DriveType.Fixed) return "è su un'unità senza Cestino";
            if (!File.Exists(percorso)) return "non c'è più";

            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = percorso + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
            };
            var r = SHFileOperation(ref op);
            if (r != 0 || op.fAnyOperationsAborted) return $"Windows non l'ha spostato (codice {r})";
            return File.Exists(percorso) ? "Windows non l'ha spostato" : null;
        }
        catch (Exception e) { return e.Message; }
    }

    const uint FO_DELETE = 3;
    const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
}
