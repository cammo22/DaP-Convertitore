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
        if (!OperatingSystem.IsWindows()) return SpostaLinux(percorso);
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

    /// <summary>Il Cestino di Linux (la specifica FreeDesktop): il file si sposta, non si cancella.</summary>
    static string? SpostaLinux(string percorso)
    {
        try
        {
            if (!File.Exists(percorso) && !Directory.Exists(percorso)) return "non c'è più";
            var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var files = Path.Combine(casa, ".local", "share", "Trash", "files");
            var info = Path.Combine(casa, ".local", "share", "Trash", "info");
            Directory.CreateDirectory(files);
            Directory.CreateDirectory(info);
            var nome = Path.GetFileName(percorso.TrimEnd('/', '\\'));
            var dest = Path.Combine(files, nome);
            for (var n = 2; File.Exists(dest) || Directory.Exists(dest); n++)
                dest = Path.Combine(files, Path.GetFileNameWithoutExtension(nome) + " " + n + Path.GetExtension(nome));
            var pieno = Path.GetFullPath(percorso);
            if (Directory.Exists(pieno)) Directory.Move(pieno, dest);
            else File.Move(pieno, dest);
            var path = pieno.Replace("%", "%25").Replace(" ", "%20");
            File.WriteAllText(Path.Combine(info, Path.GetFileName(dest) + ".trashinfo"),
                "[Trash Info]\nPath=" + path + "\nDeletionDate=" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "\n");
            return null;
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
