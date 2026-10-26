# Genera PDFSIMPLE para repartir, en la carpeta dist:
#   dist\PDFSIMPLE-Setup.exe   EL QUE SE REPARTE fuera de la Store: instala PDFSIMPLE en Archivos de programa,
#                              lo registra y propone dejarlo para los PDF
#   dist\PDFSIMPLE.exe         el programa suelto: un solo .exe para Intel/AMD y ARM
# Es .NET Framework 4.8, el que ya trae Windows 10 y 11: no lleva .NET dentro (con .NET 10 pesaba 55 MB).
# El paquete de la Microsoft Store lo hace store\empaquetar.ps1 con este mismo .exe.
$ErrorActionPreference = 'Stop'
$dist = "$PSScriptRoot\dist"
New-Item -ItemType Directory -Force $dist | Out-Null
Remove-Item "$dist\PDFSIMPLE-ARM64.exe" -ErrorAction SilentlyContinue # de cuando había uno por procesador

# Siempre desde cero: si el reloj del equipo retrocede, la compilación incremental cree que todo está al día
# y mete los archivos del visor antiguos (pasó el 26/10/2026).
Remove-Item "$PSScriptRoot\src\PDFSIMPLE\obj\Release", "$PSScriptRoot\src\PDFSIMPLE\bin\Release", "$PSScriptRoot\src\PDFSIMPLE\obj\web.zip",
            "$PSScriptRoot\instalador\obj\Release", "$PSScriptRoot\instalador\bin\Release" -Recurse -Force -ErrorAction SilentlyContinue

dotnet build "$PSScriptRoot\src\PDFSIMPLE\PDFSIMPLE.csproj" -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'No ha compilado el programa.' }
Copy-Item "$PSScriptRoot\src\PDFSIMPLE\bin\Release\net48\PDFSIMPLE.exe" "$dist\PDFSIMPLE.exe" -Force

# El instalador, con el programa dentro; se deja también en la carpeta del proyecto para repartirlo.
dotnet build "$PSScriptRoot\instalador\Setup.csproj" -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'No ha compilado el instalador.' }
Copy-Item "$PSScriptRoot\instalador\bin\Release\net48\PDFSIMPLE-Setup.exe" $dist -Force
Copy-Item "$dist\PDFSIMPLE-Setup.exe" $PSScriptRoot -Force

Get-ChildItem $dist -Filter *.exe | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
