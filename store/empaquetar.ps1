# Genera el paquete para subir a la Microsoft Store, donde el programa se llama PDFSIMPLE:
# dist\store\PDFSIMPLE_<versión>.msixbundle, con las versiones Intel/AMD y ARM dentro. La Store lo firma al publicarlo; aquí no hace falta certificado.
#
# Antes:
#   1. store\identidad.json con los datos de Partner Center (el producto → Identidad del producto):
#      Nombre = Package/Identity/Name, Editor = Package/Identity/Publisher,
#      EditorVisible = Package/Properties/PublisherDisplayName.
#   2. Cada envío nuevo a la Store, subir «Version» (1.0.1.0, 1.0.2.0…; el último número siempre 0).
# Uso: .\store\empaquetar.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot
$dist = Join-Path $raiz 'dist'
$salida = Join-Path $dist 'store'
$trabajo = Join-Path $raiz 'store\obj'
$id = Get-Content (Join-Path $PSScriptRoot 'identidad.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($id.Nombre -like 'PENDIENTE*') { Write-Warning 'store\identidad.json aún tiene los datos de prueba: el paquete sirve para comprobar, no para subir.' }

# makeappx y makepri, del paquete oficial de herramientas del SDK de Windows (sin instalar el SDK).
$sdk = Join-Path $raiz 'herramientas\sdk'
$herr = Get-ChildItem $sdk -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1
if (-not $herr) {
    $ver = '10.0.28000.2705'
    $zip = Join-Path $env:TEMP "sdk-buildtools-$ver.zip"
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$ver/microsoft.windows.sdk.buildtools.$ver.nupkg" -OutFile $zip
    Expand-Archive $zip -DestinationPath $sdk -Force
    $herr = Get-ChildItem $sdk -Recurse -Filter makeappx.exe | Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1
}
$makeappx = $herr.FullName
$makepri = Join-Path $herr.DirectoryName 'makepri.exe'

Remove-Item $trabajo, $salida -Recurse -Force -ErrorAction SilentlyContinue

# Los mismos .exe que el instalador (publicar.ps1 los deja en dist).
if (-not (Test-Path "$dist\PDFSIMPLE.exe")) { & (Join-Path $raiz 'publicar.ps1') }

New-Item -ItemType Directory -Force $salida, "$trabajo\msix" | Out-Null
$plantilla = Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw -Encoding UTF8

# El mismo .exe (AnyCPU, .NET Framework) en el paquete de cada procesador.
foreach ($a in @(@{ Arq = 'x64'; Exe = 'PDFSIMPLE.exe' }, @{ Arq = 'arm64'; Exe = 'PDFSIMPLE.exe' })) {
    $dir = Join-Path $trabajo $a.Arq
    New-Item -ItemType Directory -Force $dir | Out-Null
    Copy-Item "$dist\$($a.Exe)" "$dir\PDFSIMPLE.exe"
    Copy-Item (Join-Path $PSScriptRoot 'Assets') "$dir\Assets" -Recurse
    $manifiesto = $plantilla.Replace('{{Nombre}}', $id.Nombre).Replace('{{Editor}}', $id.Editor).
        Replace('{{EditorVisible}}', $id.EditorVisible).Replace('{{Version}}', $id.Version).Replace('{{Arquitectura}}', $a.Arq)
    [IO.File]::WriteAllText("$dir\AppxManifest.xml", $manifiesto, (New-Object Text.UTF8Encoding $false))

    # resources.pri: para que Windows elija el icono de cada tamaño y escala.
    & $makepri createconfig /cf "$trabajo\priconfig.xml" /dq es-ES_en-US_fr-FR /pv 10.0.0 /o | Out-Null
    & $makepri new /pr $dir /cf "$trabajo\priconfig.xml" /mn "$dir\AppxManifest.xml" /of "$dir\resources.pri" /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri ha fallado ($($a.Arq))." }

    & $makeappx pack /d $dir /p "$trabajo\msix\PDFSIMPLE_$($id.Version)_$($a.Arq).msix" /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack ha fallado ($($a.Arq))." }
}

$bundle = Join-Path $salida "PDFSIMPLE_$($id.Version).msixbundle"
& $makeappx bundle /d "$trabajo\msix" /p $bundle /bv $id.Version /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle ha fallado.' }
'{0}  {1:N0} MB' -f $bundle, ((Get-Item $bundle).Length / 1MB)
