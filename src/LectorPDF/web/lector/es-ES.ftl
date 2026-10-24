
## Lo que a la traducción de pdf.js aún le falta (Ventana.cs lo añade al servir es-ES/viewer.ftl)

pdfjs-digital-signature-properties-banner-unknown =
    { $count ->
        [one] Documento firmado, pero { $count } firma no se ha podido comprobar
       *[other] Documento firmado, pero { $count } firmas no se han podido comprobar
    }
pdfjs-digital-signature-properties-banner-untrusted =
    { $count ->
        [one] Documento firmado con { $count } certificado que no es de confianza
       *[other] Documento firmado con { $count } certificados que no son de confianza
    }
pdfjs-digital-signature-properties-banner-expired =
    { $count ->
        [one] Documento firmado con { $count } certificado caducado
       *[other] Documento firmado con { $count } certificados caducados
    }
pdfjs-digital-signature-properties-banner-invalid =
    { $count ->
        [one] El documento tiene { $count } firma no válida
       *[other] El documento tiene { $count } firmas no válidas
    }
pdfjs-digital-signature-properties-banner-revoked =
    { $count ->
        [one] Documento firmado con { $count } certificado revocado
       *[other] Documento firmado con { $count } certificados revocados
    }
