# Compila el Lector PDF, lo deja en %LOCALAPPDATA%\Programs\LectorPDF, pone el acceso directo en el
# menú Inicio y lo da de alta como lector de .pdf (para el usuario actual; no pide administrador).
# Se puede volver a lanzar para actualizar: cierra antes las ventanas del lector.
$ErrorActionPreference = 'Stop'
$destino = Join-Path $env:LOCALAPPDATA 'Programs\LectorPDF'
$rid = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }

Get-Process LectorPDF -ErrorAction SilentlyContinue | Stop-Process
dotnet publish "$PSScriptRoot\src\LectorPDF\LectorPDF.csproj" -c Release -r $rid --self-contained false -o $destino
if ($LASTEXITCODE -ne 0) { throw 'No ha compilado.' }

$exe = Join-Path $destino 'LectorPDF.exe'
$sh = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Lector PDF.lnk'))
$sh.TargetPath = $exe
$sh.WorkingDirectory = $destino
$sh.IconLocation = "$exe,0"
$sh.Description = 'Lector de PDF: formularios, texto y firma digital'
$sh.Save()

& $exe --registrar | Out-Null
Write-Host "Instalado en $destino"
Write-Host 'Para que los PDF se abran siempre con él: Configuración > Aplicaciones > Aplicaciones predeterminadas > Lector PDF > .pdf'
