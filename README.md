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

Deja en `dist`:

- `PDFbyWilly.exe`: para casi todos los PC (Intel / AMD; en ARM también funciona, emulado).
- `PDFbyWilly-ARM64.exe`: para equipos ARM (Snapdragon), más rápido en ellos.

Llevan .NET dentro: en el otro equipo no hace falta instalar nada (WebView2 ya viene con Windows 10/11; si
faltara, el programa ofrece descargarlo). No están firmados con certificado de código, así que la primera vez
Windows puede avisar («Windows protegió su PC» → Más información → Ejecutar de todas formas).

## Al abrirlo la primera vez

Se ofrece a instalarse (para el usuario, sin administrador) en `%LOCALAPPDATA%\Programs\PDFbyWilly`, con:

- acceso en el menú Inicio, y si se marca, en el **escritorio**;
- **abrir siempre los PDF con él**: Windows no deja que un programa se ponga solo, así que se abre
  Configuración → Aplicaciones predeterminadas: escribir `.pdf` en el buscador de arriba, pulsar la casilla que
  sale debajo, elegir PDFbyWilly y «Establecer predeterminado». También desde el menú `>>` del programa;
- **barra de tareas**: tampoco se puede anclar por programa; al abrirse dice dónde hacer clic.

Un .exe más nuevo, al abrirlo, se ofrece a cambiar el instalado. Se desinstala desde Configuración →
Aplicaciones → Aplicaciones instaladas.

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
    confianza de Windows. `Instalacion.cs` / `DialogoInstalar.cs`: la autoinstalación.
- `herramientas/icono.ps1`: dibuja el icono (cuadrado rojo, «PDF» y debajo «BY WILLY»).
- `pruebas`: `dotnet run` genera un formulario, lo firma dos veces con un certificado de prueba y comprueba que se
  verifican y que un documento manipulado se detecta. `dotnet run -- comprobar archivo.pdf` enseña firmas, campos y
  anotaciones de cualquier PDF.

**Licencia de iText:** es AGPL. Para repartir el programa hay que ofrecer también su código fuente a quien lo
reciba (por ejemplo, publicando este repositorio); si no se quiere, hay que cambiar iText por otra librería de firma.
