using System.Runtime.InteropServices;

namespace DaP.Convertitore.Prove;

/// <summary>
/// La DLL del menu di Windows 11 provata come la usa Esplora file: si caricano i due comandi, si chiede titolo,
/// icona e stato con una selezione vera, e le voci del sottomenu devono essere quelle del Catalogo.
/// Non serve registrare niente: la DLL si carica qui dentro. Se menu\out non c'è (non compilata), la prova salta.
/// </summary>
public class ProveMenu11
{
    static readonly Guid Converti = new("5E2A8C47-1F93-4B6D-8E0A-72C4D9B135F2");

    [ComImport, Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IExplorerCommand
    {
        [PreserveSig] int GetTitle(IShellItemArray? sel, [MarshalAs(UnmanagedType.LPWStr)] out string titolo);
        [PreserveSig] int GetIcon(IShellItemArray? sel, [MarshalAs(UnmanagedType.LPWStr)] out string icona);
        [PreserveSig] int GetToolTip(IShellItemArray? sel, out IntPtr t);
        [PreserveSig] int GetCanonicalName(out Guid g);
        [PreserveSig] int GetState(IShellItemArray? sel, [MarshalAs(UnmanagedType.Bool)] bool lento, out uint stato);
        [PreserveSig] int Invoke(IShellItemArray? sel, IntPtr bind);
        [PreserveSig] int GetFlags(out uint flags);
        [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand elenco);
    }

    [ComImport, Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IEnumExplorerCommand
    {
        [PreserveSig] int Next(uint n, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IExplorerCommand[] voci, out uint dati);
    }

    [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IClassFactory
    {
        [PreserveSig] int CreateInstance(IntPtr esterno, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemArray { }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem { }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string p, IntPtr bc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);
    [DllImport("shell32.dll", PreserveSig = false)]
    static extern void SHCreateShellItemArrayFromShellItem(IShellItem item, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemArray arr);

    delegate int DllGetClassObject(ref Guid clsid, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);

    static string? Radice()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "DaP-Convertitore.slnx"))) d = d.Parent;
        return d?.FullName;
    }

    static IShellItemArray Selezione(string percorso)
    {
        SHCreateItemFromParsingName(percorso, IntPtr.Zero, typeof(IShellItem).GUID, out var item);
        SHCreateShellItemArrayFromShellItem(item, typeof(IShellItemArray).GUID, out var arr);
        return arr;
    }

    [Fact]
    public void Le_voci_del_menu_di_Windows_11_sono_quelle_del_catalogo()
    {
        var dll = Path.Combine(Radice()!, "menu", "out", Menu11.Dll);
        if (!File.Exists(dll)) return;
        var cartella = Directory.CreateTempSubdirectory("dap-menu11").FullName;
        File.Copy(dll, Path.Combine(cartella, Menu11.Dll));
        Menu11.ScriviMenu(cartella);

        var h = NativeLibrary.Load(Path.Combine(cartella, Menu11.Dll));
        var fabbrica = Marshal.GetDelegateForFunctionPointer<DllGetClassObject>(NativeLibrary.GetExport(h, "DllGetClassObject"));
        IExplorerCommand Crea(Guid clsid)
        {
            var iid = typeof(IClassFactory).GUID;
            Assert.Equal(0, fabbrica(ref clsid, ref iid, out var f));
            var iidc = typeof(IExplorerCommand).GUID;
            Assert.Equal(0, ((IClassFactory)f).CreateInstance(IntPtr.Zero, ref iidc, out var o));
            return (IExplorerCommand)o;
        }

        // un comando solo: «DaP Convertitore ›», con l'icona dell'app e il sottomenu
        var radice = Crea(Converti);
        Assert.Equal(0, radice.GetTitle(null, out var titolo));
        Assert.Equal("DaP Convertitore", titolo);
        Assert.Equal(0, radice.GetIcon(null, out var icona));
        Assert.EndsWith("DaPConvertitore.exe,0", icona);
        Assert.Equal(0, radice.GetFlags(out var flags));
        Assert.Equal(1u, flags & 1u); // ECF_HASSUBCOMMANDS
        Assert.Equal(0, radice.GetState(null, false, out var senza));
        Assert.Equal(0u, senza); // visibile anche quando Esplora file chiede senza selezione

        // un file per categoria, compresi un .tar.gz, una cartella e un'estensione che non conosce nessuno
        var prove = Path.Combine(cartella, "prove");
        Directory.CreateDirectory(Path.Combine(prove, "Vacanze"));
        var casi = new (string nome, Categoria c)[]
        {
            ("film.MOV", Categoria.Video), ("foto.heic", Categoria.Immagine), ("conti.xlsx", Categoria.Foglio),
            ("backup.tar.gz", Categoria.Archivio), ("Vacanze", Categoria.Cartella), ("strano.xyz", Categoria.Altro),
        };
        foreach (var (nome, c) in casi)
        {
            var p = Path.Combine(prove, nome);
            if (c != Categoria.Cartella) File.WriteAllText(p, "x");
            var sel = Selezione(p);
            Assert.Equal(0, radice.GetState(sel, false, out var stato));
            Assert.Equal(0u, stato); // ECS_ENABLED
            // il sottomenu lo può chiedere un'altra istanza: la categoria deve arrivarci lo stesso
            Assert.Equal(0, Crea(Converti).EnumSubCommands(out var elenco));
            var titoli = new List<string>();
            var una = new IExplorerCommand[1];
            while (elenco.Next(1, una, out var n) == 0 && n == 1)
            {
                una[0].GetFlags(out var f);
                titoli.Add((f & 8u) != 0 ? "———" : una[0].GetTitle(sel, out var t) == 0 ? t : "?"); // 8 = ECF_ISSEPARATOR
            }
            Assert.Equal(["Apri nel convertitore…", "———", .. Catalogo.Rapide.Where(r => r.Categoria == c).Select(r => r.Etichetta)], titoli);
        }

        // il clic scrive la lista dei file per l'app (qui l'exe non c'è, quindi il lancio fallisce: la lista resta da guardare)
        var video = Path.Combine(prove, "film.MOV");
        radice.GetState(Selezione(video), false, out _);
        var cartellaListe = Path.Combine(Path.GetTempPath(), "DaP Convertitore");
        var prima = Directory.Exists(cartellaListe) ? Directory.GetFiles(cartellaListe, "lista-*.txt").ToHashSet() : [];
        radice.EnumSubCommands(out var e2);
        var voce = new IExplorerCommand[1];
        e2.Next(1, voce, out _); // «Apri nel convertitore…»
        Assert.NotEqual(0, voce[0].Invoke(Selezione(video), IntPtr.Zero));
        var nuova = Directory.GetFiles(cartellaListe, "lista-*.txt").Single(f => !prima.Contains(f));
        Assert.Equal([video], File.ReadAllLines(nuova));
        File.Delete(nuova);
    }

    [Fact]
    public void Nel_Cestino_va_solo_quello_che_il_convertito_sostituisce()
    {
        Assert.True(Catalogo.Trova("video.mp4")!.Sostituisce);
        Assert.True(Catalogo.Trova("img.webp")!.Sostituisce);
        Assert.True(Catalogo.Trova("doc.pdf")!.Sostituisce);
        Assert.False(Catalogo.Trova("video.mp3")!.Sostituisce);   // l'audio di un video: il video serve ancora
        Assert.False(Catalogo.Trova("pdf.jpg")!.Sostituisce);
        Assert.False(Catalogo.Trova("img.pdf")!.Sostituisce);     // tante foto in un PDF
        Assert.False(Catalogo.Trova("arch.estrai")!.Sostituisce);
        Assert.False(Catalogo.Trova("cartella.zip")!.Sostituisce);
    }
}
