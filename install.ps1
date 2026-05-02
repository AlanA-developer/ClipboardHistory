# ============================================================
#  ClipboardHistory — Instalador para Windows
#  Ejecutar en PowerShell como Administrador (recomendado)
#  o como usuario normal (instala en AppData del usuario)
# ============================================================

param(
    [switch]$Uninstall
)

$AppName        = "ClipboardHistory"
$InstallDir     = "$env:LOCALAPPDATA\$AppName"
$ExePath        = "$InstallDir\$AppName.exe"
$StartMenuDir   = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$ShortcutPath   = "$StartMenuDir\Clipboard History.lnk"
$RegistryPath   = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$ProjectDir     = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Colores para la consola ──
function Write-Title($text)  { Write-Host "`n=== $text ===" -ForegroundColor Cyan }
function Write-Step($text)   { Write-Host "  ▸ $text" -ForegroundColor White }
function Write-Ok($text)     { Write-Host "  ✓ $text" -ForegroundColor Green }
function Write-Warn($text)   { Write-Host "  ⚠ $text" -ForegroundColor Yellow }
function Write-Err($text)    { Write-Host "  ✗ $text" -ForegroundColor Red }

# ── DESINSTALAR ──
if ($Uninstall) {
    Write-Title "DESINSTALANDO $AppName"

    # Cerrar proceso
    $proc = Get-Process -Name $AppName -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Step "Cerrando proceso..."
        $proc | Stop-Process -Force
        Start-Sleep -Seconds 1
        Write-Ok "Proceso terminado"
    }

    # Quitar del inicio de Windows
    if (Get-ItemProperty -Path $RegistryPath -Name $AppName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RegistryPath -Name $AppName -ErrorAction SilentlyContinue
        Write-Ok "Removido del inicio de Windows"
    }

    # Eliminar acceso directo
    if (Test-Path $ShortcutPath) {
        Remove-Item $ShortcutPath -Force
        Write-Ok "Acceso directo eliminado"
    }

    # Eliminar directorio de instalación
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Ok "Directorio de instalación eliminado: $InstallDir"
    }

    Write-Host "`n  ✓ $AppName desinstalado correctamente.`n" -ForegroundColor Green
    exit 0
}

# ── INSTALAR ──
Write-Title "INSTALADOR DE $AppName"
Write-Host "  Directorio de instalación: $InstallDir" -ForegroundColor DarkGray

# 1. Verificar .NET SDK
Write-Title "PASO 1/5 - Verificando .NET SDK"
$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Err ".NET SDK no encontrado. Descárgalo de: https://dotnet.microsoft.com/download"
    exit 1
}
Write-Ok ".NET SDK $dotnetVersion detectado"

# 2. Compilar como self-contained single-file
Write-Title "PASO 2/5 - Compilando aplicacion"
Write-Step "Publicando como ejecutable independiente..."

$publishDir = "$ProjectDir\bin\publish"
$publishArgs = @(
    "publish"
    "-c", "Release"
    "-r", "win-x64"
    "--self-contained", "true"
    "-p:PublishSingleFile=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "-p:EnableCompressionInSingleFile=true"
    "-o", $publishDir
)

Push-Location $ProjectDir
& dotnet @publishArgs
$buildResult = $LASTEXITCODE
Pop-Location

if ($buildResult -ne 0) {
    Write-Err "Error en la compilación. Revisa los errores arriba."
    exit 1
}
Write-Ok "Compilación exitosa"

# 3. Copiar a directorio de instalación
Write-Title "PASO 3/5 - Instalando archivos"

# Cerrar proceso existente si hay uno
$proc = Get-Process -Name $AppName -ErrorAction SilentlyContinue
if ($proc) {
    Write-Step "Cerrando instancia anterior..."
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 2
}

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Write-Step "Copiando archivos a $InstallDir ..."
Copy-Item "$publishDir\*" -Destination $InstallDir -Recurse -Force
Write-Ok "Archivos instalados"

# Copiar base de datos existente si la hay (preservar historial)
$existingDb = "$ProjectDir\clipboard.db"
$targetDb   = "$InstallDir\clipboard.db"
if ((Test-Path $existingDb) -and -not (Test-Path $targetDb)) {
    Copy-Item $existingDb $targetDb -Force
    Write-Ok "Base de datos existente copiada (historial preservado)"
}

# 4. Registrar en el inicio de Windows
Write-Title "PASO 4/5 - Configurando inicio automatico"
try {
    Set-ItemProperty -Path $RegistryPath -Name $AppName -Value "`"$ExePath`""
    Write-Ok "Registrado en el inicio de Windows (HKCU\Run)"
} catch {
    Write-Warn "No se pudo registrar en el inicio. Puedes hacerlo manualmente."
}

# 5. Crear acceso directo en Menu Inicio
Write-Title "PASO 5/5 - Creando acceso directo"
try {
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $ExePath
    $Shortcut.WorkingDirectory = $InstallDir
    $Shortcut.Description = "Clipboard History - Historial de portapapeles para Windows 11"
    $Shortcut.Save()
    Write-Ok "Acceso directo creado en Menu Inicio"
} catch {
    Write-Warn "No se pudo crear el acceso directo."
}

# -- Resumen final --
Write-Host ""
Write-Host "  ========================================" -ForegroundColor Green
Write-Host "    INSTALACION COMPLETADA CON EXITO" -ForegroundColor Green
Write-Host "  ========================================" -ForegroundColor Green
Write-Host "  Ejecutable : $ExePath" -ForegroundColor White
Write-Host "  Inicio auto: Activado (HKCU\Run)" -ForegroundColor White
Write-Host "  Atajo      : Alt + V (personalizable)" -ForegroundColor White
Write-Host "  Bandeja    : Click derecho para salir" -ForegroundColor White
Write-Host "  ========================================" -ForegroundColor Green
Write-Host ""

# Iniciar la aplicacion usando explorer para desenlazar el proceso de esta terminal
Invoke-WmiMethod -Class Win32_Process -Name Create -ArgumentList $ExePath | Out-Null
Write-Ok "Aplicacion iniciada. Busca el icono en la bandeja del sistema."

Write-Host ""
Write-Host "  Para desinstalar ejecuta: .\install.ps1 -Uninstall" -ForegroundColor DarkGray
Write-Host ""

