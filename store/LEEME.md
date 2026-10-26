# Publicar PDFbyWilly en la Microsoft Store

La Store firma el programa: quien lo instale no ve el aviso de «editor desconocido», y Windows lo ofrece
directamente para abrir los PDF. La cuenta de desarrollador es gratis para particulares.

## 1. Cuenta de desarrollador (una vez)

1. Entra en <https://storedeveloper.microsoft.com> con tu cuenta Microsoft y elige **Particular** (gratis).
   Para una cuenta de **Empresa** (XOP Shops) hay que verificar la sociedad y tarda más.
2. Te piden datos de contacto y verificar la identidad (DNI y selfi). La aprobación tarda de horas a pocos días.

## 2. Reservar el nombre

En [Partner Center](https://partner.microsoft.com/dashboard/apps-and-games/overview): **Nueva aplicación** →
nombre **PDFbyWilly** → Reservar.

Luego, en el producto: **Administración del producto → Identidad del producto**. Copia en `store\identidad.json`:

| Partner Center                       | identidad.json  |
|--------------------------------------|-----------------|
| Package/Identity/Name                | `Nombre`        |
| Package/Identity/Publisher           | `Editor`        |
| Package/Properties/PublisherDisplayName | `EditorVisible` |

## 3. Generar el paquete

```powershell
.\publicar.ps1
.\store\empaquetar.ps1
```

Sale `dist\store\PDFbyWilly_1.0.0.0.msixbundle` (Intel/AMD y ARM dentro). Para cada envío nuevo hay que subir
`Version` en `identidad.json` (1.0.1.0, 1.0.2.0…; el último número siempre 0).

## 4. Envío en Partner Center

**Precios y disponibilidad:** Gratis; todos los mercados (o solo España).

**Propiedades:** categoría *Productividad*. Política de privacidad: **https://menork.es/politica-de-privacidad/**

**Clasificación por edades:** cuestionario; la app no tiene contenido para adultos ni comunicación entre usuarios.

**Paquetes:** sube el `.msixbundle`. Pedirá justificar la capacidad **runFullTrust**:

> PDFbyWilly es una aplicación de escritorio (WinForms con WebView2). Necesita confianza total para abrir y
> guardar los PDF que elige el usuario y para firmar con los certificados digitales instalados en Windows
> (almacén de certificados personal y tarjetas como el DNI electrónico).

**Ficha de Store (español):**

- **Descripción:**

  > Lector de PDF sin publicidad y sin cuentas. Rellena formularios, escribe a máquina en cualquier parte de la
  > página, añade tu firma manuscrita (escrita, dibujada o desde una imagen) y firma digitalmente con tu
  > certificado (FNMT, representante, DNI electrónico o archivo .pfx), con sello de tiempo. Comprueba también
  > las firmas de los PDF que recibes. Funciona en equipos Intel, AMD y ARM.

- **Características:** Rellenar formularios PDF · Escribir texto en el PDF · Firma manuscrita · Firma digital
  PAdES con certificado y sello de tiempo · Comprobar firmas digitales · Sin anuncios.
- **Palabras clave:** pdf, lector pdf, firmar pdf, firma digital, formulario pdf, certificado digital.
- **Capturas:** al menos una, de 1366×768 o mayor (la app con un PDF abierto, el formulario, la firma).
- **Logotipo de la Store:** `store\Assets\Square150x150Logo.scale-400.png` (600×600) sirve; si pide 300×300,
  `Square150x150Logo.scale-200.png`.

**Enviar para certificación.** Microsoft la revisa (normalmente de 1 a 3 días) y, si la aprueba, se publica.

## Política de privacidad

Publicada en <https://menork.es/politica-de-privacidad/>. El archivo está en el proyecto de la web:
`Documentos\proyectos\Menork\public\politica-de-privacidad\index.html` (se sube con el resto de menork.es).

## Licencia

La firma usa iText, que es AGPL: al repartir el programa hay que ofrecer su código fuente (por ejemplo,
publicando este repositorio en GitHub y poniendo el enlace en la descripción de la Store).
