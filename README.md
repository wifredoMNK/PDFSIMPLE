# PDFbyWilly

Lector de PDF para Windows, sin publicidad ni cuentas, en **un solo .exe** para repartir:

- **Leer** (buscar, miniaturas, imprimir, girar, pantalla completa…).
- **Rellenar formularios** y guardar los valores en el propio PDF.
- **Escribir a máquina** en cualquier sitio de la página (herramienta «Texto»).
- **Firma manuscrita**: escrita, dibujada o desde una imagen («Añadir firma»); se recuerdan para la próxima vez.
- **Firma digital con certificado** (botón «Firmar»): los certificados personales de Windows (FNMT, representante,
  DNIe con lector…) o un archivo `.pfx` / `.p12`. Firma PAdES con sello de tiempo, visible en el recuadro que se
  arrastre en la página o invisible. Se añade al final del PDF, así que las firmas anteriores siguen valiendo.
- **Comprobar firmas** de los PDF que llegan: el icono de firmas de la barra dice si son válidas y si el
  certificado es de una autoridad en la que confía Windows.

## Repartirlo

```powershell
.\publicar.ps1
```

Genera **`PDFbyWilly-Setup.exe`** (en `dist` y en la carpeta del proyecto), que es lo que se reparte:

- Detecta si el equipo es Intel/AMD o ARM (Snapdragon) e instala la versión que toca (las dos van dentro).
- Pide permiso de administrador una vez: se instala en `C:\Program Files\PDFbyWilly` y se registra para todo el
  equipo, que es como Windows lo enseña en su lista de programas para los PDF.
- Opciones: acceso directo en el escritorio y anclar a la barra de tareas (Windows no deja anclar por programa:
  al terminar dice dónde hacer clic).
- Al acabar pregunta si se quiere para abrir los PDF y abre la página de Configuración donde se elige
  (Windows no deja que un programa se ponga solo como predeterminado).
- Volver a ejecutarlo actualiza. Se desinstala desde Configuración → Aplicaciones → Aplicaciones instaladas.

El instalador es .NET Framework 4.8 (ya viene con Windows 10 y 11) y el programa lleva .NET dentro: no hace falta
nada más (WebView2 viene con Windows; si faltara, el programa ofrece descargarlo). No están firmados con
certificado de código, así que Windows puede avisar la primera vez («Más información» → «Ejecutar de todas formas»).

En `dist` quedan también las versiones sueltas, `PDFbyWilly.exe` (Intel/AMD) y `PDFbyWilly-ARM64.exe`, que se
pueden usar sin instalar.
Atajos: Ctrl+O abrir, Ctrl+S guardar, Ctrl+Mayús+S guardar como, Ctrl+F buscar, Ctrl+P imprimir. Arrastrar un PDF
a la ventana lo abre (en otra ventana si ya hay uno).

## Cómo está hecho

- `src/PDFbyWilly`: programa WinForms (.NET 10) con un WebView2 dentro. Todo va incrustado en el .exe
  (`Recursos.cs`).
  - `web/pdfjs`: el visor de [pdf.js](https://github.com/mozilla/pdf.js) 6.4.299 (Apache 2.0) **tal cual sale**
    (para actualizarlo, se sustituye la carpeta por la del nuevo `pdfjs-*-dist.zip`).
  - `web/lector`: lo nuestro: botones, guardar en disco, colocar la firma, verificador de firmas, traducciones que
    faltan. `Ventana.cs` lo engancha al servir `viewer.html`.
  - `Firmador.cs`: firma con iText 9. `Verificador.cs`: comprueba firmas con lo que trae .NET y la cadena de
    confianza de Windows. `Instalacion.cs`: alta y baja en Windows (`--registrar`, `--desinstalar`).
- `instalador`: `PDFbyWilly-Setup.exe`.
- `herramientas/icono.ps1`: dibuja el icono (cuadrado rojo, «PDF» y debajo «BY WILLY»).
- `pruebas`: `dotnet run` genera un formulario, lo firma dos veces con un certificado de prueba y comprueba que se
  verifican y que un documento manipulado se detecta. `dotnet run -- comprobar archivo.pdf` enseña firmas, campos y
  anotaciones de cualquier PDF.

**Licencia de iText:** es AGPL. Para repartir el programa hay que ofrecer también su código fuente a quien lo
reciba (por ejemplo, publicando este repositorio); si no se quiere, hay que cambiar iText por otra librería de firma.
