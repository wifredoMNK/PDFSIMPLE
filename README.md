# Lector PDF

Lector de PDF para Windows, sin publicidad ni cuentas. Hace lo justo:

- **Leer** (buscar, miniaturas, imprimir, girar, pantalla completa…).
- **Rellenar formularios** y guardar los valores en el propio PDF.
- **Escribir a máquina** en cualquier sitio de la página (herramienta «Texto»).
- **Firma manuscrita**: escrita, dibujada o desde una imagen («Añadir firma»); se recuerdan para la próxima vez.
- **Firma digital con certificado** (botón «Firmar»): los certificados personales de Windows (FNMT, representante,
  DNIe con lector…) o un archivo `.pfx` / `.p12`. Firma PAdES con sello de tiempo, visible en el recuadro que se
  arrastre en la página o invisible. Se añade al final del PDF, así que las firmas anteriores siguen valiendo.
- **Comprobar firmas** de los PDF que llegan: el icono de firmas de la barra dice si son válidas y si el
  certificado es de una autoridad en la que confía Windows.

## Instalar / actualizar

```powershell
.\instalar.ps1
```

Deja el programa en `%LOCALAPPDATA%\Programs\LectorPDF`, crea «Lector PDF» en el menú Inicio y lo da de alta para
abrir `.pdf`. Para que sea el predeterminado: Configuración → Aplicaciones → Aplicaciones predeterminadas →
Lector PDF → `.pdf` (Windows no deja que un programa se ponga solo).

Atajos: Ctrl+O abrir, Ctrl+S guardar, Ctrl+Mayús+S guardar como, Ctrl+F buscar, Ctrl+P imprimir. Arrastrar un PDF
a la ventana lo abre (en otra ventana si ya hay uno).

## Cómo está hecho

- `src/LectorPDF`: programa WinForms (.NET 10) con un WebView2 dentro.
  - `web/pdfjs`: el visor de [pdf.js](https://github.com/mozilla/pdf.js) 6.4.299 **tal cual sale** (para actualizarlo,
    se sustituye la carpeta por la del nuevo `pdfjs-*-dist.zip`).
  - `web/lector`: lo nuestro: botones, guardar en disco, colocar la firma, verificador de firmas, traducciones que
    faltan. `Ventana.cs` lo engancha al servir `viewer.html`.
  - `Firmador.cs`: firma con iText 9 (licencia AGPL; uso personal). `Verificador.cs`: comprueba firmas con lo que
    trae .NET y la cadena de confianza de Windows.
- `pruebas`: `dotnet run` genera un formulario, lo firma dos veces con un certificado de prueba y comprueba que se
  verifican y que un documento manipulado se detecta. `dotnet run -- comprobar archivo.pdf` enseña firmas, campos y
  anotaciones de cualquier PDF.
