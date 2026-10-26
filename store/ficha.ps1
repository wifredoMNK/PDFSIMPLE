# Rellena las descripciones de la Store sobre el CSV que da Partner Center («Exportar descripciones»), con su
# formato exacto (BOM, CRLF, sin comillas salvo donde hace falta, sin salto al final: si no, no lo importa).
#   .\store\ficha.ps1 -Exportado listingData-....csv
# Deja store\ficha\descripciones.csv (con capturas y logos: «Importar descripciones → Cargar carpeta») y
# store\descripciones-solo-texto.csv (solo textos: «Cargar .csv»).
param([Parameter(Mandatory)] [string]$Exportado)
$ErrorActionPreference = 'Stop'
$url = 'https://github.com/wifredoMNK/PDFSIMPLE'

$textos = @{
  'es-es' = @{
    Description = "PDFSIMPLE es un lector de PDF para Windows sin publicidad y sin cuentas.`n`n• Rellena formularios PDF y guarda los datos en el propio documento.`n• Escribe a máquina en cualquier parte de la página.`n• Añade tu firma manuscrita: escrita, dibujada o desde una imagen.`n• Firma digitalmente con tu certificado (FNMT, representante, DNI electrónico o archivo .pfx/.p12), con sello de tiempo; las firmas anteriores siguen siendo válidas.`n• Comprueba las firmas digitales de los PDF que recibes.`n• En español, francés e inglés. Funciona en equipos Intel, AMD y ARM.`n`nCódigo abierto (GNU AGPL v3): $url"
    ShortDescription = 'Lector de PDF sin publicidad: rellena formularios, escribe y firma con tu certificado digital.'
    ReleaseNotes = 'Primera versión.'
    AdditionalLicenseTerms = "Software libre bajo la licencia GNU AGPL v3. Código fuente: $url"
    Cap = @('Formulario relleno y firmado digitalmente', 'Miniaturas de las páginas y página completa')
    Features = @('Rellena formularios PDF y guarda los datos en el documento', 'Escribe texto en cualquier parte de la página', 'Firma manuscrita: escrita, dibujada o desde una imagen', 'Firma digital con certificado (FNMT, DNIe, .pfx) y sello de tiempo', 'Comprueba las firmas digitales de los PDF que recibes', 'Sin publicidad y sin cuentas', 'En español, francés e inglés')
    Search = @('pdf', 'lector pdf', 'firmar pdf', 'firma digital', 'formulario pdf', 'certificado digital', 'dnie')
    Img = 'es'
  }
  'fr-fr' = @{
    Description = "PDFSIMPLE est un lecteur PDF pour Windows, sans publicité et sans compte.`n`n• Remplissez les formulaires PDF et enregistrez les données dans le document.`n• Écrivez n'importe où sur la page.`n• Ajoutez votre signature manuscrite : tapée, dessinée ou à partir d'une image.`n• Signez numériquement avec votre certificat (fichier .pfx/.p12 ou certificat installé dans Windows), avec horodatage ; les signatures précédentes restent valides.`n• Vérifiez les signatures numériques des PDF que vous recevez.`n• En espagnol, français et anglais. Fonctionne sur les PC Intel, AMD et ARM.`n`nCode source ouvert (GNU AGPL v3) : $url"
    ShortDescription = 'Lecteur PDF sans publicité : remplissez des formulaires, écrivez et signez avec votre certificat numérique.'
    ReleaseNotes = 'Première version.'
    AdditionalLicenseTerms = "Logiciel libre sous licence GNU AGPL v3. Code source : $url"
    Cap = @('Formulaire rempli et signé numériquement', 'Miniatures des pages et page entière')
    Features = @('Remplissez les formulaires PDF et enregistrez les données dans le document', "Écrivez du texte n'importe où sur la page", "Signature manuscrite : tapée, dessinée ou à partir d'une image", 'Signature numérique avec certificat (.pfx ou Windows) et horodatage', 'Vérifiez les signatures numériques des PDF reçus', 'Sans publicité et sans compte', 'En espagnol, français et anglais')
    Search = @('pdf', 'lecteur pdf', 'signer pdf', 'signature numérique', 'formulaire pdf', 'certificat', 'remplir pdf')
    Img = 'fr'
  }
  'en-us' = @{
    Description = "PDFSIMPLE is an ad-free PDF reader for Windows, with no accounts.`n`n• Fill in PDF forms and save the data in the document itself.`n• Type anywhere on the page.`n• Add your handwritten signature: typed, drawn or from an image.`n• Sign digitally with your certificate (.pfx/.p12 file or a certificate installed in Windows), with a timestamp; earlier signatures stay valid.`n• Check the digital signatures of the PDFs you receive.`n• In Spanish, French and English. Works on Intel, AMD and ARM PCs.`n`nOpen source (GNU AGPL v3): $url"
    ShortDescription = 'Ad-free PDF reader: fill in forms, type and sign with your digital certificate.'
    ReleaseNotes = 'First release.'
    AdditionalLicenseTerms = "Free software under the GNU AGPL v3 license. Source code: $url"
    Cap = @('Form filled in and digitally signed', 'Page thumbnails and full-page view')
    Features = @('Fill in PDF forms and save the data in the document', 'Type text anywhere on the page', 'Handwritten signature: typed, drawn or from an image', 'Digital signature with certificate (.pfx or Windows) and timestamp', 'Check the digital signatures of the PDFs you receive', 'No ads, no accounts', 'In Spanish, French and English')
    Search = @('pdf', 'pdf reader', 'sign pdf', 'digital signature', 'pdf form', 'certificate', 'fill pdf')
    Img = 'en'
  }
}

# Sin saltos de línea dentro de los campos: el importador de Partner Center no los acepta bien.
function UnaLinea([string]$v) { return (($v -replace "`n+• ", " ") -replace "`n+", " ").Trim() }

function Valor([string]$campo, $x, [bool]$imagenes) {
  switch -regex ($campo) {
    '^Description$' { return UnaLinea $x.Description }
    '^ReleaseNotes$' { return $x.ReleaseNotes }
    '^DevStudio$' { return 'WILLY MNK' }
    '^CopyrightTrademarkInformation$' { return '© 2026 WILLY MNK. pdf.js © Mozilla (Apache 2.0).' }
    '^AdditionalLicenseTerms$' { return $x.AdditionalLicenseTerms }
    '^DesktopScreenshot([12])$' { if ($imagenes) { return "$($x.Img)-$($matches[1]).png" } }
    '^DesktopScreenshotCaption([12])$' { if ($imagenes) { return $x.Cap[[int]$matches[1] - 1] } }
    '^StoreLogo(300x300|1080x1080)$' { if ($imagenes) { return "StoreLogo$($matches[1]).png" } }
    '^Feature(\d+)$' { $n = [int]$matches[1]; if ($n -le $x.Features.Count) { return $x.Features[$n - 1] } }
    '^SearchTerm(\d+)$' { $n = [int]$matches[1]; if ($n -le $x.Search.Count) { return $x.Search[$n - 1] } }
  }
  return $null
}

# Como lo hace Excel: comillas solo si hace falta.
function Celda([string]$v) {
  if ($v -match '[",\r\n]') { return '"' + $v.Replace('"', '""') + '"' }
  return $v
}

function Escribir([string]$destino, [bool]$imagenes) {
  $lineas = [IO.File]::ReadAllText((Resolve-Path $Exportado), [Text.Encoding]::UTF8) -split "`r`n"
  $cab = $lineas[0].Split(',')
  $salida = foreach ($l in $lineas) {
    $c = $l.Split(',')
    if ($l -eq $lineas[0] -or -not $c[0]) { $l; continue }
    for ($i = 4; $i -lt $cab.Count; $i++) {
      $x = $textos[$cab[$i]]
      if ($x) { $v = Valor $c[0] $x $imagenes; if ($v) { $c[$i] = Celda $v } }
    }
    $c -join ','
  }
  $utf8bom = New-Object Text.UTF8Encoding $true
  [IO.File]::WriteAllText($destino, ($salida -join "`r`n"), $utf8bom)
  $destino
}

Escribir (Join-Path $PSScriptRoot 'ficha\descripciones.csv') $true
Escribir (Join-Path $PSScriptRoot 'descripciones-solo-texto.csv') $false
