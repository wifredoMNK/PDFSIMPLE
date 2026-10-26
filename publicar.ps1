# Genera PDFbyWilly para repartir, en la carpeta dist:
#   dist\PDFbyWilly-Setup.exe  EL QUE SE REPARTE: detecta si el equipo es Intel/AMD o ARM, instala la versión
#                              que toca en Archivos de programa, lo registra y propone dejarlo para los PDF
#   dist\PDFbyWilly.exe        la versión Intel / AMD suelta
#   dist\PDFbyWilly-ARM64.exe  la versión ARM (Snapdragon) suelta
# Llevan .NET dentro; el instalador es .NET Framework 4.8, que ya trae Windows 10 y 11.
$ErrorActionPreference = 'Stop'
$proyecto = "$PSScriptRoot\src\PDFbyWilly\PDFbyWilly.csproj"
$dist = "$PSScriptRoot\dist"
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($rid in 'win-x64', 'win-arm64') {
    $tmp = "$PSScriptRoot\src\PDFbyWilly\bin\publicar\$rid"
    dotnet publish $proyecto -c Release -r $rid --self-contained true -o $tmp --disable-build-servers -m:1 `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded
    if ($LASTEXITCODE -ne 0) { throw "No ha compilado para $rid." }
    $nombre = if ($rid -eq 'win-x64') { 'PDFbyWilly.exe' } else { 'PDFbyWilly-ARM64.exe' }
    Copy-Item "$tmp\PDFbyWilly.exe" "$dist\$nombre" -Force
}

# El instalador, con las dos versiones dentro; se deja también en la carpeta del proyecto para repartirlo.
dotnet build "$PSScriptRoot\instalador\Setup.csproj" -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'No ha compilado el instalador.' }
Copy-Item "$PSScriptRoot\instalador\bin\Release\net48\PDFbyWilly-Setup.exe" $dist -Force
Copy-Item "$dist\PDFbyWilly-Setup.exe" $PSScriptRoot -Force

Get-ChildItem $dist -Filter *.exe | ForEach-Object { '{0}  {1:N0} MB' -f $_.Name, ($_.Length / 1MB) }
