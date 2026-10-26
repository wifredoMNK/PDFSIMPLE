# PDFSIMPLE

Lector de PDF para Windows, sin publicidad ni cuentas, en español, francés e inglés:

- **Leer** (buscar, miniaturas, imprimir, girar, pantalla completa…).
- **Rellenar formularios** y guardar los valores en el propio PDF.
- **Escribir a máquina** en cualquier sitio de la página (herramienta «Texto»).
- **Firma manuscrita**: escrita, dibujada o desde una imagen («Añadir firma»); se recuerdan para la próxima vez.
- **Firma digital con certificado** (botón «Firmar»): los certificados personales de Windows (FNMT, representante,
  DNIe con lector…) o un archivo `.pfx` / `.p12`. Firma PAdES con sello de tiempo, visible en el recuadro que se
  arrastre en la página o invisible. Se añade al final del PDF, así que las firmas anteriores siguen valiendo.
- **Comprobar firmas** de los PDF que llegan: el icono ✓ junto a «Firmar» dice si son válidas y si el certificado es
  de una autoridad en la que confía Windows.
- **Idioma**: el icono 文A de la barra (español, francés, inglés); la primera vez, el de Windows.

## La barra

Abrir, Guardar y Guardar como a la izquierda; luego panel lateral, buscar y páginas; zoom; Firmar y ✓ de firmas;
herramientas de edición; imprimir; y en secciones todo lo que pdf.js tenía en su menú (presentación, primera/última
página, girar, cursor, desplazamiento, páginas a la par, propiedades). La flecha de la derecha recoge esas secciones
en el menú `»` (se recuerda) y lo que no cabe al estrechar la ventana va también ahí.

## Dos versiones

```powershell
.\publicar.ps1           # dist\PDFSIMPLE-Setup.exe (también en la carpeta del proyecto) y dist\PDFSIMPLE.exe suelto
.\store\empaquetar.ps1   # dist\store\PDFSIMPLE_<versión>.msixbundle para la Microsoft Store
```

**Instalador `PDFSIMPLE-Setup.exe`**, para repartir fuera de la Store:

- Un solo `PDFSIMPLE.exe` (unos 9,5 MB) vale para Intel/AMD y ARM.
- Pide permiso de administrador una vez: se instala en `C:\Program Files\PDFSIMPLE` y se registra para todo el
  equipo, que es como Windows lo enseña en su lista de programas para los PDF.
- Opciones: acceso directo en el escritorio y anclar a la barra de tareas (Windows no deja anclar por programa:
  al terminar dice dónde hacer clic). Al acabar pregunta si se quiere para abrir los PDF.
- Volver a ejecutarlo actualiza. Se desinstala desde Configuración → Aplicaciones → Aplicaciones instaladas.
- No va firmado con certificado de código: Windows puede avisar la primera vez («Más información» → «Ejecutar de
  todas formas»).

**Microsoft Store**: la firma Microsoft y no sale ningún aviso. Ver [store/LEEME.md](store/LEEME.md).

Programa e instalador son **.NET Framework 4.8**, que ya viene con Windows 10 y 11: no llevan .NET dentro (con .NET 10
pesaba 55 MB). WebView2 también viene con Windows; si faltara, el programa ofrece descargarlo.

Atajos: Ctrl+O abrir, Ctrl+S guardar, Ctrl+Mayús+S guardar como, Ctrl+F buscar, Ctrl+P imprimir. Arrastrar un PDF
a la ventana lo abre (en otra ventana si ya hay uno).

## Cómo está hecho

- `src/PDFSIMPLE`: programa WinForms (.NET Framework 4.8, C#) con un WebView2 dentro. Todo va en un solo .exe:
  las librerías con Costura, el visor en un zip y el cargador nativo de WebView2 de cada procesador (`Recursos.cs`).
  - `web/pdfjs`: el visor de [pdf.js](https://github.com/mozilla/pdf.js) 6.4.299 (Apache 2.0) **tal cual sale**
    (para actualizarlo, se sustituye la carpeta por la del nuevo `pdfjs-*-dist.zip`).
  - `web/lector`: lo nuestro: barra, guardar en disco, colocar la firma, verificador de firmas, idiomas, traducciones
    que le faltan a pdf.js. `Ventana.cs` lo engancha al servir `viewer.html`.
  - `Idioma.cs`: textos del programa en los tres idiomas (los del visor, en `lector.js`).
  - `Firmador.cs`: firma con iText 9. `Verificador.cs`: comprueba firmas con lo que trae .NET y la cadena de
    confianza de Windows. `Instalacion.cs`: alta y baja en Windows (`--registrar`, `--desinstalar`).
- `instalador`: `PDFSIMPLE-Setup.exe`, en el idioma de Windows (español, francés o inglés).
- `store`: manifiesto, iconos y `empaquetar.ps1` de la Microsoft Store.
- `herramientas/icono.ps1`: dibuja el icono (cuadrado rojo, «PDF» y debajo «SIMPLE») y los de la Store.
- `pruebas`: `dotnet run` genera un formulario, lo firma dos veces con un certificado de prueba y comprueba que se
  verifican y que un documento manipulado se detecta. `dotnet run -- comprobar archivo.pdf` enseña firmas, campos y
  anotaciones de cualquier PDF.

## Licencia

PDFSIMPLE se publica bajo la **GNU AGPL v3** (archivo `LICENSE`), la misma que iText, la librería de firma que
lleva dentro. pdf.js es de Mozilla, con licencia Apache 2.0 (`src/PDFSIMPLE/web/pdfjs/LICENSE`).
