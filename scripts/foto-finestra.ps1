# Fotografa la finestra di DaP Convertitore (per le prove e per il README).
# Uso: scripts\foto-finestra.ps1 -Uscita test\.out\finestra.png
param([string]$Uscita = (Join-Path $PSScriptRoot "..\test\.out\finestra.png"))
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Fin {
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
}
"@
[Fin]::SetProcessDPIAware() | Out-Null
$p = Get-Process DaPConvertitore -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
[Fin]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 400
$r = New-Object Fin+R
[Fin]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
$w = $r.Ri - $r.L; $h = $r.B - $r.T
$bmp = New-Object Drawing.Bitmap $w, $h
$g = [Drawing.Graphics]::FromImage($bmp)
# PrintWindow col contenuto completo: la finestra viene presa anche se qualcosa ci sta sopra
$dc = $g.GetHdc(); [Fin]::PrintWindow($p.MainWindowHandle, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
New-Item -ItemType Directory -Force (Split-Path $Uscita) | Out-Null
$bmp.Save($Uscita, [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "$Uscita ($w x $h)"
