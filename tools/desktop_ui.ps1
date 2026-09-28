# Автоматизация настольного приложения через Windows UI Automation (для проверки и скриншотов руководства)
# Использование: . .\tools\desktop_ui.ps1 ; $app = Start-Desk ; Login-Desk '+79000000001' 'Priem2026' ; Shot 'orders.png'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W32 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[W32]::SetProcessDPIAware() | Out-Null   # координаты окна — в физических пикселях
$UIA = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$Exe = Join-Path $PSScriptRoot '..\src\ServiceDesk.Desktop\bin\Debug\net8.0-windows\ServiceDesk.Desktop.exe'

function Cond($prop, $val) { New-Object System.Windows.Automation.PropertyCondition($prop, $val) }

function Get-Win([string]$titleLike, [int]$timeout = 20, [string]$notLike = '') {
  $end = (Get-Date).AddSeconds($timeout)
  while ((Get-Date) -lt $end) {
    $w = $UIA::RootElement.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition) |
      Where-Object { $_.Current.Name -like $titleLike -and ($notLike -eq '' -or $_.Current.Name -notlike $notLike) -and $_.Current.ProcessId -eq $script:Proc.Id } | Select-Object -First 1
    if ($w) { return $w }
    Start-Sleep -Milliseconds 300
  }
  throw "Окно '$titleLike' не найдено"
}

function Find([System.Windows.Automation.AutomationElement]$root, [string]$name, [int]$timeout = 10) {
  $end = (Get-Date).AddSeconds($timeout)
  while ((Get-Date) -lt $end) {
    $e = $root.FindFirst($TS::Descendants, (Cond $UIA::NameProperty $name))
    if ($e) { return $e }
    Start-Sleep -Milliseconds 250
  }
  throw "Элемент '$name' не найден"
}

function FindId($root, [string]$id) { $root.FindFirst($TS::Descendants, (Cond $UIA::AutomationIdProperty $id)) }

function Click($el) {
  $p = $null
  if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { $p.Invoke(); return }
  if ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select(); return }
  if ($el.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$p)) { $p.Toggle(); return }
  throw "Нельзя нажать '$($el.Current.Name)'"
}

function SetText($el, [string]$text) {
  $p = $null
  if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$p)) { $p.SetValue($text); return }
  $el.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait($text)
}

function Start-Desk {
  Get-Process ServiceDesk.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
  $script:Proc = Start-Process $Exe -PassThru
  return $script:Proc
}

function Login-Desk([string]$login, [string]$password) {
  $w = Get-Win 'СервисДеск — вход'
  SetText (FindId $w 'LoginBox') $login
  $pb = FindId $w 'PasswordBox'; $pb.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait($password)
  Click (FindId $w 'LoginButton')
  $script:Main = Get-Win 'СервисДеск —*' 20 '*вход'
  Start-Sleep -Seconds 2
  return $script:Main
}

function Main-Window { if (-not $script:Main) { $script:Main = Get-Win 'СервисДеск —*' 20 '*вход' }; return $script:Main }

# Снимок окна (PrintWindow — не зависит от перекрытия другими окнами)
$Scale = [double](Get-ItemProperty 'HKCU:\Control Panel\Desktop\WindowMetrics' -ErrorAction SilentlyContinue).AppliedDPI / 96
if (-not $Scale) { $Scale = 1 }
function Shot([string]$file, $win = $null, [int]$w = [int](1366 * $Scale), [int]$h = [int](768 * $Scale)) {
  if (-not $win) { $win = Main-Window }
  $hwnd = [IntPtr]$win.Current.NativeWindowHandle
  if ($w -gt 0) { [W32]::ShowWindow($hwnd, 1) | Out-Null; [W32]::MoveWindow($hwnd, 20, 20, $w, $h, $true) | Out-Null; Start-Sleep -Milliseconds 700 }
  $r = New-Object W32+RECT; [W32]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc(); [W32]::PrintWindow($hwnd, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
  $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  Write-Output "saved $file"
}

function Menu([string]$name) { Click (Find (Main-Window) $name); Start-Sleep -Milliseconds 1500 }

# Открыть заявку в списке: выделение строки, фокус на таблице и Enter
function Open-Order([string]$id) {
  $m = Main-Window
  $el = Find $m $id
  $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
  $DI = [System.Windows.Automation.ControlType]::DataItem
  $DG = [System.Windows.Automation.ControlType]::DataGrid
  while ($el -and $el.Current.ControlType -ne $DI) { $el = $walker.GetParent($el) }
  $p = $null
  if ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select() }
  $grid = $el; while ($grid -and $grid.Current.ControlType -ne $DG) { $grid = $walker.GetParent($grid) }
  [W32]::SetForegroundWindow([IntPtr]$m.Current.NativeWindowHandle) | Out-Null
  $grid.SetFocus(); Start-Sleep -Milliseconds 300
  [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
  Start-Sleep -Milliseconds 1800
}
