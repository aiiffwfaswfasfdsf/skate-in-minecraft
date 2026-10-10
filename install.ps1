# Skate in GTA - one-command installer for GTA V (Enhanced or Legacy) on Windows.
#
# Run in PowerShell:
#   powershell -ExecutionPolicy Bypass -c "irm https://raw.githubusercontent.com/aiiffwfaswfasfdsf/skate-in-minecraft/main/install.ps1 | iex"
#
# What it does:
#   1. Finds your GTA V folder (Steam, Rockstar, Epic, or asks you).
#   2. Downloads Script Hook V from its official site (dev-c.com) and copies bin\ into the game folder.
#      Its args.txt turns BattlEye off for Story Mode.
#   3. Downloads the latest ScriptHookVDotNet Enhanced from GitHub and copies its files into the game folder.
#   4. Downloads the latest Skate in GTA release and copies its scripts\ folder into the game folder.
#   5. Installs the Visual C++ runtime if it's missing.
# It only writes into the GTA folder (and a temp folder it deletes afterwards). Never use mods in GTA Online.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue' # Invoke-WebRequest is very slow with the progress bar
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Self = 'https://raw.githubusercontent.com/aiiffwfaswfasfdsf/skate-in-minecraft/main/install.ps1'
$Ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) SkateInGTA-Installer'

function Say($text, $color) { if (-not $color) { $color = 'White' }; Write-Host $text -ForegroundColor $color }

function Stop-Install($text) {
    Say ""
    Say "FEHLER / ERROR: $text" Red
    Read-Host "Enter druecken zum Schliessen / press Enter to close" | Out-Null
    exit 1
}

# Game folders under Program Files need admin rights, so restart elevated if needed.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Say "Starte als Administrator neu (Windows fragt gleich nach) ..." Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -c `"irm $Self | iex`""
    exit
}

Say ""
Say "  Skate in GTA - Installer" Green
Say ""

if (Get-Process -Name 'GTA5*', 'PlayGTAV' -ErrorAction SilentlyContinue) {
    Stop-Install "GTA laeuft noch. Bitte GTA komplett schliessen und den Installer neu starten. / Close GTA first."
}

# ---------------------------------------------------------------- 1. find the game folder

function Test-GameDir($dir) {
    return $dir -and (Test-Path $dir) -and (Get-ChildItem -Path $dir -Filter 'GTA5*.exe' -File -ErrorAction SilentlyContinue)
}

$candidates = New-Object System.Collections.Generic.List[string]
$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
if ($steam) {
    $libs = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) { $libs += $m.Groups[1].Value -replace '\\\\', '\' }
    }
    foreach ($lib in $libs) {
        $candidates.Add((Join-Path $lib 'steamapps\common\Grand Theft Auto V Enhanced'))
        $candidates.Add((Join-Path $lib 'steamapps\common\Grand Theft Auto V'))
    }
}
foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
    if (-not $root) { continue }
    $candidates.Add((Join-Path $root 'Rockstar Games\Grand Theft Auto V Enhanced'))
    $candidates.Add((Join-Path $root 'Rockstar Games\Grand Theft Auto V'))
    $candidates.Add((Join-Path $root 'Epic Games\GTAV'))
}

$game = $null
foreach ($c in $candidates) { if (Test-GameDir $c) { $game = $c; break } }
while (-not $game) {
    Say "GTA-Ordner nicht automatisch gefunden. / Could not find the GTA folder." Yellow
    Say "Steam: Rechtsklick auf GTA > Verwalten > Lokale Dateien durchsuchen, dann den Pfad oben aus dem Explorer kopieren."
    $typed = (Read-Host "GTA-Ordner hier einfuegen / paste the GTA folder").Trim('"', ' ')
    if (Test-GameDir $typed) { $game = $typed } else { Say "Dort ist keine GTA5*.exe. / No GTA5*.exe there." Red }
}
$exe = (Get-ChildItem -Path $game -Filter 'GTA5*.exe' -File | Select-Object -First 1).Name
Say "GTA gefunden: $game ($exe)" Green

$tmp = Join-Path $env:TEMP ("SkateInGTA-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null

try {
    # ------------------------------------------------------------ 2. Script Hook V (official site only, redistribution is not allowed)
    Say "Lade Script Hook V ..." Cyan
    $page = Invoke-WebRequest -Uri 'https://www.dev-c.com/gtav/scripthookv/' -UserAgent $Ua -UseBasicParsing
    $zipName = [regex]::Match($page.Content, 'files/(ScriptHookV_[0-9._]+\.zip)').Groups[1].Value
    if (-not $zipName) { Stop-Install "Script Hook V Download-Link nicht gefunden. Lade es von https://www.dev-c.com/gtav/scripthookv/ und kopiere bin\ in den GTA-Ordner." }
    $shvZip = Join-Path $tmp 'shv.zip'
    # dev-c.com only hands out the zip when the request comes from its own page
    Invoke-WebRequest -Uri "https://www.dev-c.com/files/$zipName" -OutFile $shvZip -UserAgent $Ua -Headers @{ Referer = 'https://www.dev-c.com/gtav/scripthookv/' } -UseBasicParsing
    Expand-Archive -Path $shvZip -DestinationPath (Join-Path $tmp 'shv') -Force
    Copy-Item -Path (Join-Path $tmp 'shv\bin\*') -Destination $game -Force
    Say "  $zipName installiert (inkl. args.txt = BattlEye aus fuer Story Mode)" Green

    # ------------------------------------------------------------ 3. ScriptHookVDotNet Enhanced
    Say "Lade ScriptHookVDotNet Enhanced ..." Cyan
    $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases/latest' -UserAgent $Ua
    $asset = $rel.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
    if (-not $asset) { Stop-Install "Kein Download bei ScriptHookVDotNet Enhanced gefunden: https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases" }
    $dotnetZip = Join-Path $tmp 'shvdn.zip'
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $dotnetZip -UserAgent $Ua -UseBasicParsing
    $dotnetDir = Join-Path $tmp 'shvdn'
    Expand-Archive -Path $dotnetZip -DestinationPath $dotnetDir -Force
    # Their guide: copy the files in the zip's root, not README.txt and not the two folders.
    $root = $dotnetDir
    $inner = @(Get-ChildItem -Path $dotnetDir)
    if ($inner.Count -eq 1 -and $inner[0].PSIsContainer) { $root = $inner[0].FullName } # zip with one top folder
    Get-ChildItem -Path $root -File | Where-Object { $_.Name -notlike 'README*' } | Copy-Item -Destination $game -Force
    Say "  ScriptHookVDotNet $($rel.tag_name) installiert" Green

    # ------------------------------------------------------------ 4. Skate in GTA
    Say "Lade Skate in GTA ..." Cyan
    $modZip = Join-Path $tmp 'skate.zip'
    Invoke-WebRequest -Uri 'https://github.com/aiiffwfaswfasfdsf/skate-in-minecraft/releases/latest/download/SkateInGTA.zip' -OutFile $modZip -UserAgent $Ua -UseBasicParsing
    Expand-Archive -Path $modZip -DestinationPath (Join-Path $tmp 'skate') -Force
    $scripts = Join-Path $game 'scripts'
    $ini = Join-Path $scripts 'SkateInGTA.ini'
    $keepIni = Test-Path $ini
    if ($keepIni) { Copy-Item $ini (Join-Path $tmp 'keep.ini') } # an update must not reset your settings
    New-Item -ItemType Directory -Path $scripts -Force | Out-Null
    Copy-Item -Path (Join-Path $tmp 'skate\scripts\*') -Destination $scripts -Recurse -Force
    if ($keepIni) { Copy-Item (Join-Path $tmp 'keep.ini') $ini -Force }
    Say "  Skate in GTA installiert$(if ($keepIni) { ' (deine SkateInGTA.ini blieb erhalten)' })" Green

    Get-ChildItem -Path $game -File | Unblock-File -ErrorAction SilentlyContinue
    Get-ChildItem -Path $scripts -File -Recurse | Unblock-File -ErrorAction SilentlyContinue

    # ------------------------------------------------------------ 5. Visual C++ runtime (ScriptHookVDotNet needs it)
    $vc = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue
    if (-not $vc -or $vc.Installed -ne 1) {
        Say "Installiere Visual C++ Runtime ..." Cyan
        $vcExe = Join-Path $tmp 'vc_redist.x64.exe'
        Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $vcExe -UseBasicParsing
        Start-Process -FilePath $vcExe -ArgumentList '/install', '/quiet', '/norestart' -Wait
    }
    $net = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
    if (-not $net -or $net -lt 528040) {
        Say ".NET Framework 4.8 fehlt. Bitte installieren: https://dotnet.microsoft.com/download/dotnet-framework/net48" Yellow
    }
}
catch {
    Stop-Install $_.Exception.Message
}
finally {
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Say ""
Say "FERTIG! / DONE!" Green
Say "1. GTA ueber Steam starten (Startoption -nobattleye ist gut, args.txt erledigt das auch)."
Say "2. Story Mode waehlen. Unten rechts kommt 'Skate in GTA loaded'."
Say "3. K = aufs Board, H = alle Tasten, F4 = Menue (Geld: Player > Add Cash)."
Say "NIE mit Mods in GTA Online gehen!" Yellow
Read-Host "Enter druecken zum Schliessen / press Enter to close" | Out-Null
