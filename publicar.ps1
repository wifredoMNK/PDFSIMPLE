# Genera PDFbyWilly en un solo .exe para repartir, en la carpeta dist:
#   dist\PDFbyWilly.exe        para casi todos los PC (Intel / AMD; en ARM también funciona, emulado)
#   dist\PDFbyWilly-ARM64.exe  para equipos ARM (Surface Pro X / Copilot+ con Snapdragon), más rápido en ellos
# Lleva .NET dentro: en el otro equipo no hace falta instalar nada más que el WebView2 que trae Windows.
# Al abrirlo la primera vez se ofrece a instalarse (sin administrador) y a quedarse para los PDF.
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

Get-ChildItem $dist -Filter *.exe | ForEach-Object { '{0}  {1:N0} MB' -f $_.Name, ($_.Length / 1MB) }
