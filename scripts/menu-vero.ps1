# Legge il menu del tasto destro di un file come lo costruisce Esplora file (IContextMenu, anche i sottomenu).
# Serve a provare che la voce DaP Convertitore c'è davvero. Uso: scripts\menu-vero.ps1 -File "C:\...\video.mp4"
param([Parameter(Mandatory)][string]$File, [switch]$Esteso)
Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class MenuVero {
  [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IContextMenu {
    [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint index, uint first, uint last, uint flags);
    void InvokeCommand(IntPtr pici);
    void GetCommandString(UIntPtr id, uint type, IntPtr res, StringBuilder name, uint cch);
  }
  [ComImport, Guid("000214f4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IContextMenu2 {
    [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint index, uint first, uint last, uint flags);
    void InvokeCommand(IntPtr pici);
    void GetCommandString(UIntPtr id, uint type, IntPtr res, StringBuilder name, uint cch);
    [PreserveSig] int HandleMenuMsg(uint msg, IntPtr w, IntPtr l);
  }
  [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IShellItem {
    void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
  }
  [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
  static extern void SHCreateItemFromParsingName(string p, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);
  [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
  [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr m);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetMenuString(IntPtr m, uint id, StringBuilder s, int max, uint flags);
  [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr m, int pos);
  [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr m);
  public static List<string> Leggi(string file, bool esteso) {
    IShellItem item; SHCreateItemFromParsingName(file, IntPtr.Zero, typeof(IShellItem).GUID, out item);
    object o; item.BindToHandler(IntPtr.Zero, new Guid("3981e225-f559-11d3-8e3a-00c04f6837d5"), typeof(IContextMenu).GUID, out o);
    var cm = (IContextMenu)o;
    var menu = CreatePopupMenu();
    cm.QueryContextMenu(menu, 0, 1, 0x7FFF, esteso ? 0x100u : 0u);
    var righe = new List<string>();
    Giro(o as IContextMenu2, menu, "", righe);
    DestroyMenu(menu);
    return righe;
  }
  static void Giro(IContextMenu2 cm2, IntPtr menu, string rientro, List<string> righe) {
    int n = GetMenuItemCount(menu);
    for (int i = 0; i < n; i++) {
      var sb = new StringBuilder(256);
      GetMenuString(menu, (uint)i, sb, 256, 0x400);
      var sotto = GetSubMenu(menu, i);
      if (sb.Length > 0) righe.Add(rientro + sb.ToString().Replace("&", ""));
      if (sotto != IntPtr.Zero && sb.ToString().Contains("DaP")) {
        if (cm2 != null) cm2.HandleMenuMsg(0x0117, sotto, (IntPtr)i);
        Giro(cm2, sotto, rientro + "    ", righe);
      }
    }
  }
}
"@
[MenuVero]::Leggi((Resolve-Path $File).Path, $Esteso.IsPresent)
