# Genera PDFSIMPLE para repartir, en la carpeta dist:
#   dist\PDFSIMPLE-Setup.exe   EL QUE SE REPARTE fuera de la Store: detecta si el equipo es Intel/AMD o ARM, instala
#                              la versión que toca en Archivos de programa, lo registra y propone dejarlo para los PDF
#   dist\PDFSIMPLE.exe         la versión Intel / AMD suelta
#   dist\PDFSIMPLE-ARM64.exe   la versión ARM (Snapdragon) suelta
# Llevan .NET dentro; el instalador es .NET Framework 4.8, que ya trae Windows 10 y 11.
# El paquete de la Microsoft Store lo hace store\empaquetar.ps1 con estos mismos .exe.
$ErrorActionPreference = 'Stop'
$proyecto = "$PSScriptRoot\src\PDFSIMPLE\PDFSIMPLE.csproj"
$dist = "$PSScriptRoot\dist"
New-Item -ItemType Directory -Force $dist | Out-Null

# Siempre desde cero: si el reloj del equipo retrocede, la compilación incremental cree que todo está al día
# y mete los archivos del visor antiguos (pasó el 26/10/2026).
Remove-Item "$PSScriptRoot\src\PDFSIMPLE\obj\Release", "$PSScriptRoot\src\PDFSIMPLE\bin\Release", "$PSScriptRoot\src\PDFSIMPLE\bin\publicar",
            "$PSScriptRoot\instalador\obj\Release", "$PSScriptRoot\instalador\bin\Release" -Recurse -Force -ErrorAction SilentlyContinue

foreach ($rid in 'win-x64', 'win-arm64') {
    $tmp = "$PSScriptRoot\src\PDFSIMPLE\bin\publicar\$rid"
    dotnet publish $proyecto -c Release -r $rid --self-contained true -o $tmp --disable-build-servers -m:1 `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded
    if ($LASTEXITCODE -ne 0) { throw "No ha compilado para $rid." }
    $nombre = if ($rid -eq 'win-x64') { 'PDFSIMPLE.exe' } else { 'PDFSIMPLE-ARM64.exe' }
    Copy-Item "$tmp\PDFSIMPLE.exe" "$dist\$nombre" -Force
}

# El instalador, con las dos versiones dentro; se deja también en la carpeta del proyecto para repartirlo.
dotnet build "$PSScriptRoot\instalador\Setup.csproj" -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'No ha compilado el instalador.' }
Copy-Item "$PSScriptRoot\instalador\bin\Release\net48\PDFSIMPLE-Setup.exe" $dist -Force
Copy-Item "$dist\PDFSIMPLE-Setup.exe" $PSScriptRoot -Force

Get-ChildItem $dist -Filter *.exe | ForEach-Object { '{0}  {1:N0} MB' -f $_.Name, ($_.Length / 1MB) }
