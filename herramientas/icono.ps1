# Dibuja el icono: cuadrado rojo, «PDF» en blanco y debajo otra palabra más fina, del mismo ancho.
# Uso:
#   .\herramientas\icono.ps1 -Nombre PDFbyWilly -Debajo 'BY WILLY'                       → src\PDFbyWilly\iconos\PDFbyWilly.ico
#   .\herramientas\icono.ps1 -Nombre PDFSIMPLE  -Debajo 'SIMPLE' -Store store\Assets     → además, los PNG de la Microsoft Store
param(
  [Parameter(Mandatory)] [string]$Nombre,
  [Parameter(Mandatory)] [string]$Debajo,
  [string]$Store
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$raiz = Split-Path $PSScriptRoot

function Dibujar([int]$s) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
  $r = [single]([Math]::Max(1, $s * 0.10)); $w = [single]($s - 1)
  $fondo = New-Object System.Drawing.Drawing2D.GraphicsPath
  $fondo.AddArc(0, 0, $r*2, $r*2, 180, 90); $fondo.AddArc($w-$r*2, 0, $r*2, $r*2, 270, 90); $fondo.AddArc($w-$r*2, $w-$r*2, $r*2, $r*2, 0, 90); $fondo.AddArc(0, $w-$r*2, $r*2, $r*2, 90, 90); $fondo.CloseFigure()
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(214, 32, 39))), $fondo)
  $margen = $s * 0.09; $ancho = $s - 2 * $margen
  # Cada texto se convierte en trazo y se escala para ocupar el ancho entero: «PDF» gruesa, la de debajo fina.
  $trazos = @()
  foreach ($t in @(@('PDF', 'Arial Black', 1), @($Debajo, 'Segoe UI', 0))) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddString($t[0], (New-Object System.Drawing.FontFamily $t[1]), $t[2], 100, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $p.GetBounds(); $k = $ancho / $b.Width
    $trazos += ,@($p, $b, $k)
  }
  $hPdf = $trazos[0][1].Height * $trazos[0][2]; $hDebajo = $trazos[1][1].Height * $trazos[1][2]; $hueco = $s * 0.07
  $y = ($s - ($hPdf + $hueco + $hDebajo)) / 2
  foreach ($i in 0, 1) {
    $p, $b, $k = $trazos[$i]
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate($margen, $y); $m.Scale($k, $k); $m.Translate(-$b.X, -$b.Y)
    $p.Transform($m); $g.FillPath([System.Drawing.Brushes]::White, $p)
    $y += $b.Height * $k + $hueco
  }
  $g.Dispose(); return $bmp
}

# .ico con varios tamaños (PNG dentro).
$sizes = 16, 24, 32, 48, 64, 128, 256; $pngs = @()
foreach ($s in $sizes) { $bmp = Dibujar $s; $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $pngs += ,($ms.ToArray()); $bmp.Dispose() }
$out = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count); $off = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) { $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }; $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$off); $off += $pngs[$i].Length }
foreach ($d in $pngs) { $bw.Write($d) }
[IO.File]::WriteAllBytes((Join-Path $raiz "src\PDFbyWilly\iconos\$Nombre.ico"), $out.ToArray())

if ($Store) {
  $assets = if ([IO.Path]::IsPathRooted($Store)) { $Store } else { Join-Path $raiz $Store }
  New-Item -ItemType Directory -Force $assets | Out-Null
  function Png([string]$archivo, [int]$s) { $b = Dibujar $s; $b.Save((Join-Path $assets $archivo), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose() }
  foreach ($e in @(@(100, 1.0), @(150, 1.5), @(200, 2.0), @(400, 4.0))) {
    $q = $e[0]; $f = $e[1]
    Png "Square44x44Logo.scale-$q.png" ([int](44 * $f))
    Png "Square150x150Logo.scale-$q.png" ([int](150 * $f))
    Png "StoreLogo.scale-$q.png" ([int](50 * $f))
  }
  # Iconos de la barra de tareas y del menú Inicio, a su tamaño exacto (con y sin «placa»).
  foreach ($t in 16, 24, 32, 48, 256) {
    Png "Square44x44Logo.targetsize-$t.png" $t
    Png "Square44x44Logo.targetsize-${t}_altform-unplated.png" $t
  }
}
