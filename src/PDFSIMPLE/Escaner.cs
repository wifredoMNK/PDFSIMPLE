using System.Drawing.Imaging;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using Rectangle = iText.Kernel.Geom.Rectangle;

namespace PDFSIMPLE;

/// <summary>
/// Escanear a PDF con WIA, el sistema de escáneres que trae Windows (sin librerías ni drivers aparte).
/// Si el escáner tiene alimentador y hay hojas, las pasa todas; si no, escanea la del cristal. Después de
/// cada tanda pregunta si seguir o terminar, y al terminar pide nombre y carpeta (propone la de la última vez)
/// y hace un PDF con todo. Fotocopiar es lo mismo, pero al terminar se imprime en vez de guardarse.
/// Se escanea con lo que el escáner trae por defecto (color, página entera): tocar la resolución sin tocar
/// la zona de escaneo deja páginas cortadas en algunos drivers.
/// </summary>
static class Escaner
{
    // Propiedades de WIA (WIA_DPS_…): qué tiene el escáner, de dónde coge el papel y cuántas hojas por pasada.
    const int PropNombre = 7, PropPnp = 16, PropCapacidades = 3086, PropEstado = 3087, PropOrigen = 3088, PropHojas = 3096;
    const int Alimentador = 1, Cristal = 2;
    const string Jpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
    const string Bmp = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";

    const int SinPapel = unchecked((int)0x80210003);
    const int SinEscaner = unchecked((int)0x80210015);

    internal sealed record Pagina(byte[] Jpeg, float Ancho, float Alto); // ancho y alto en puntos

    /// <summary>Escanea las páginas que haga falta y devuelve el PDF guardado, o null si se cancela.</summary>
    public static string? Escanear(IWin32Window dueño)
    {
        string? destino = null;
        bool hecho = Tandas(dueño, fotocopia: false, paginas =>
        {
            destino = PedirDestino(dueño);
            if (destino is null) return false;
            File.WriteAllBytes(destino, Pdf(paginas));
            return true;
        });
        return hecho ? destino : null;
    }

    /// <summary>Escanea y lo imprime, sin guardar nada. False si se cancela.</summary>
    public static bool Fotocopiar(IWin32Window dueño) =>
        Tandas(dueño, fotocopia: true, paginas => Impresion.Imprimir(dueño, paginas));

    /// <summary>
    /// Escanea tandas hasta que se pulsa terminar y entonces llama a <paramref name="terminar"/>; si esta devuelve
    /// false (se ha cancelado el nombre o la impresión) se vuelve a la ventana sin perder lo escaneado.
    /// </summary>
    static bool Tandas(IWin32Window dueño, bool fotocopia, Func<List<Pagina>, bool> terminar)
    {
        dynamic? escaner = Conectar(dueño, elegir: false);
        if (escaner is null) return false;

        var paginas = new List<Pagina>();
        bool escanear = true;
        while (true)
        {
            if (escanear)
            {
                try
                {
                    Cursor.Current = Cursors.WaitCursor;
                    if (!Tanda(escaner, paginas) && paginas.Count == 0) return false; // cancelado antes de la primera
                }
                catch (COMException ex)
                {
                    MessageBox.Show(dueño, Error(ex), Idioma.T("escanear"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (paginas.Count == 0) return false;
                }
                finally { Cursor.Current = Cursors.Default; }
            }
            escanear = true;

            using var d = new DialogoEscaneo(paginas.Count, (string)Nombre(escaner), fotocopia);
            var r = d.ShowDialog(dueño);
            if (d.CambiarEscaner)
            {
                escaner = Conectar(dueño, elegir: true) ?? escaner;
                continue;
            }
            if (r == DialogResult.Retry) continue;
            if (r != DialogResult.OK) return false; // descartado
            if (terminar(paginas)) return true;
            escanear = false;
        }
    }

    /// <summary>
    /// Una pasada: todas las hojas del alimentador o, si está vacío o no hay, la del cristal.
    /// Devuelve false si se ha cancelado sin escanear nada.
    /// </summary>
    internal static bool Tanda(dynamic escaner, List<Pagina> paginas)
    {
        int antes = paginas.Count;
        if ((Leer(escaner.Properties, PropCapacidades) & Alimentador) != 0)
        {
            // Al alimentador solo si hay hojas, y se pregunta antes de cada una: pedirle una con él vacío deja a
            // algunos (el Canon GX4000) un minuto esperando y luego bloqueados hasta que se pulsa en el aparato.
            if (HayHojas((object)escaner))
            {
                try
                {
                    Poner(escaner.Properties, PropOrigen, Alimentador);
                    Poner(escaner.Properties, PropHojas, 1);
                    do
                    {
                        var p = Transferir(escaner);
                        if (p is null) return paginas.Count > antes;
                        paginas.Add(p);
                    } while (HayHojas((object)escaner));
                }
                catch (COMException ex) when (ex.ErrorCode == SinPapel) { } // se acabaron las hojas
                if (paginas.Count > antes) return true;
            }
            Poner(escaner.Properties, PropOrigen, Cristal);
        }
        var q = Transferir(escaner);
        if (q is null) return false;
        paginas.Add(q);
        return true;
    }

    /// <summary>Una hoja, con la barra de progreso de Windows. Null si se cancela.</summary>
    static Pagina? Transferir(dynamic escaner)
    {
        dynamic? item = null;
        foreach (dynamic i in escaner.Items) { item = i; break; }
        if (item is null) throw new COMException(Idioma.T("sinEscaner"), SinEscaner);

        var formatos = new List<string>();
        foreach (object f in item.Formats) formatos.Add(f.ToString().ToUpperInvariant());
        string formato = formatos.Contains(Jpeg) ? Jpeg : Bmp;

        dynamic dialogo = Com("WIA.CommonDialog");
        dynamic? imagen = dialogo.ShowTransfer(item, formato, false);
        if (imagen is null) return null;

        byte[] bytes = (byte[])imagen.FileData.BinaryData;
        float pppX = Convert.ToSingle(imagen.HorizontalResolution), pppY = Convert.ToSingle(imagen.VerticalResolution);
        using var bmp = new Bitmap(new MemoryStream(bytes));
        if (pppX <= 1) pppX = bmp.HorizontalResolution;
        if (pppY <= 1) pppY = bmp.VerticalResolution;
        if (!bmp.RawFormat.Equals(ImageFormat.Jpeg)) bytes = AJpeg(bmp);
        return new Pagina(bytes, bmp.Width * 72f / pppX, bmp.Height * 72f / pppY);
    }

    static byte[] AJpeg(Bitmap bmp)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var calidad = new EncoderParameters(1);
        calidad.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
        var ms = new MemoryStream();
        bmp.Save(ms, codec, calidad);
        return ms.ToArray();
    }

    internal static byte[] Pdf(List<Pagina> paginas)
    {
        var ms = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfWriter(ms)))
        {
            pdf.GetDocumentInfo().SetCreator(Marca.Nombre);
            foreach (var p in paginas)
            {
                var hoja = pdf.AddNewPage(new PageSize(p.Ancho, p.Alto));
                new PdfCanvas(hoja).AddImageFittedIntoRectangle(ImageDataFactory.Create(p.Jpeg), new Rectangle(0, 0, p.Ancho, p.Alto), false);
            }
        }
        return ms.ToArray();
    }

    // ---------------------------------------------------------------- alimentador

    /// <summary>
    /// ¿Hay hojas en el alimentador? Se le pregunta al aparato por eSCL si se puede, porque WIA dice que sí aunque
    /// esté vacío en algunos (el Canon GX4000); si no, lo que diga WIA.
    /// </summary>
    internal static bool HayHojas(object escaner)
    {
        dynamic e = escaner;
        if (UrlEscl(escaner) is { } url)
        {
            try
            {
                var pet = (HttpWebRequest)WebRequest.Create(url.TrimEnd('/') + "/ScannerStatus");
                pet.Timeout = pet.ReadWriteTimeout = 3000;
                using var resp = pet.GetResponse();
                using var lector = new StreamReader(resp.GetResponseStream()!);
                var m = Regex.Match(lector.ReadToEnd(), @"<scan:AdfState>(\w+)</scan:AdfState>");
                if (m.Success) return m.Groups[1].Value == "ScannerAdfLoaded";
            }
            catch (WebException) { }
            catch (IOException) { }
        }
        return (Leer(e.Properties, PropEstado) & Alimentador) != 0;
    }

    static readonly Dictionary<string, string?> urls = new();

    /// <summary>
    /// La dirección eSCL (http://…/eSCL) del aparato, de Windows: la tiene el escáner eSCL de red y la comparten
    /// los demás del mismo aparato (el de WSD), que Windows agrupa en un «contenedor». Por USB no hay.
    /// </summary>
    internal static string? UrlEscl(object escaner)
    {
        dynamic e = escaner;
        string id = (string)e.DeviceID;
        if (urls.TryGetValue(id, out var url)) return url;
        url = null;
        try
        {
            // «\\?\swd#escl#…#{6bdd…}»: el id del dispositivo con «#» en vez de «\» y «/». Lo tiene su ficha en la
            // lista de dispositivos, no el escáner conectado.
            string ruta = "";
            dynamic gestor = Com("WIA.DeviceManager");
            foreach (dynamic info in gestor.DeviceInfos)
                if ((string)info.DeviceID == id) ruta = Normalizar(Convert.ToString(Propiedad(info.Properties, PropPnp)?.Value) ?? "");
            if (ruta.Length == 0) return urls[id] = null;
            using var buscador = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Image'");
            var equipos = buscador.Get().Cast<ManagementObject>().Select(m =>
            {
                var datos = new Dictionary<string, string>();
                var entrada = m.GetMethodParameters("GetDeviceProperties");
                entrada["devicePropertyKeys"] = new[] { ClaveContenedor, ClaveUrl };
                var salida = m.InvokeMethod("GetDeviceProperties", entrada, null);
                foreach (var p in (ManagementBaseObject[]?)salida?["deviceProperties"] ?? [])
                    // Una clave que el dispositivo no tiene viene sin «Data» (y pedirla da «No encontrado»).
                    if (p.Properties.Cast<PropertyData>().FirstOrDefault(d => d.Name == "Data")?.Value is { } dato)
                        datos[(string)p["KeyName"]] = dato.ToString();
                return new { Id = Normalizar((string)m["PNPDeviceID"]), Datos = datos };
            }).ToList();
            var este = equipos.FirstOrDefault(x => ruta.Contains(x.Id));
            if (este is not null && este.Datos.TryGetValue(ClaveContenedor, out var contenedor))
                url = equipos.Where(x => x.Datos.TryGetValue(ClaveContenedor, out var c) && c == contenedor)
                             .Select(x => x.Datos.TryGetValue(ClaveUrl, out var u) ? u : null)
                             .FirstOrDefault(u => u is not null);
        }
        catch (ManagementException) { }
        catch (COMException) { }
        return urls[id] = url;
    }

    const string ClaveContenedor = "DEVPKEY_Device_ContainerId";
    const string ClaveUrl = "{A35996AB-11CF-4935-8B61-A6761081ECDF} 12";

    static string Normalizar(string id) => id.ToLowerInvariant().Replace('\\', '#').Replace('/', '#');

    // ---------------------------------------------------------------- escáner

    /// <summary>
    /// El escáner de la última vez; la primera, el eSCL de red si lo hay (el mismo aparato sale también por USB y
    /// por WSD, y este es el que da JPEG y dice si hay hojas en el alimentador); si no, o si se pide cambiar, el
    /// selector de Windows, que con uno solo no pregunta. Null si se cancela o no hay ninguno.
    /// </summary>
    internal static dynamic? Conectar(IWin32Window dueño, bool elegir)
    {
        try
        {
            if (!elegir)
            {
                string? id = Ajustes.Actual.Escaner;
                dynamic gestor = Com("WIA.DeviceManager");
                foreach (dynamic info in gestor.DeviceInfos)
                {
                    string este = (string)info.DeviceID;
                    if (id is null ? !este.StartsWith(@"SWD\Escl\", StringComparison.OrdinalIgnoreCase) : este != id) continue;
                    try
                    {
                        dynamic conectado = info.Connect();
                        if (id is null) { Ajustes.Actual.Escaner = este; Ajustes.Actual.Guardar(); }
                        return conectado;
                    }
                    catch (COMException) { }
                }
            }
            dynamic dialogo = Com("WIA.CommonDialog");
            dynamic? escaner = dialogo.ShowSelectDevice(1 /* escáner */, elegir, false);
            if (escaner is null) return null;
            Ajustes.Actual.Escaner = (string)escaner.DeviceID;
            Ajustes.Actual.Guardar();
            return escaner;
        }
        catch (COMException ex)
        {
            MessageBox.Show(dueño, Error(ex), Idioma.T("escanear"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
    }

    static string Nombre(dynamic escaner)
    {
        try { return Convert.ToString(Propiedad(escaner.Properties, PropNombre)?.Value) ?? ""; }
        catch (COMException) { return ""; }
    }

    static dynamic Com(string progId) =>
        Activator.CreateInstance(Type.GetTypeFromProgID(progId) ?? throw new COMException(Idioma.T("sinWia")))!;

    static dynamic? Propiedad(dynamic propiedades, int id)
    {
        foreach (dynamic p in propiedades)
            if ((int)p.PropertyID == id) return p;
        return null;
    }

    static int Leer(dynamic propiedades, int id)
    {
        dynamic? p = Propiedad(propiedades, id);
        return p is null ? 0 : Convert.ToInt32(p.Value);
    }

    static void Poner(dynamic propiedades, int id, int valor)
    {
        dynamic? p = Propiedad(propiedades, id);
        if (p is not null) p.Value = valor;
    }

    static string Error(COMException ex) => (uint)ex.ErrorCode switch
    {
        0x80210015 => Idioma.T("sinEscaner"),
        0x80210002 => Idioma.T("escanerAtasco"),
        0x80210003 => Idioma.T("escanerSinPapel"),
        0x80210005 or 0x8021000A => Idioma.T("escanerApagado"),
        0x80210006 or 0x80210007 => Idioma.T("escanerOcupado"),
        0x80210016 => Idioma.T("escanerTapa"),
        _ => Idioma.T("escanerError", ex.Message),
    };

    // ---------------------------------------------------------------- carpeta

    public static string? Carpeta() =>
        Ajustes.Actual.CarpetaEscaneos is { } c && Directory.Exists(c) ? c : null;

    public static string? PedirCarpeta(IWin32Window dueño)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = Idioma.T("carpetaEscaneos"),
            ShowNewFolderButton = true,
            SelectedPath = Carpeta() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(dueño) != DialogResult.OK) return null;
        Ajustes.Actual.CarpetaEscaneos = dlg.SelectedPath;
        Ajustes.Actual.Guardar();
        return dlg.SelectedPath;
    }

    /// <summary>
    /// Nombre y carpeta del PDF, con el «Guardar como» de Windows: propone la carpeta de escaneos y
    /// «Escaneo 2026-10-31 10.22», ya seleccionado para escribir encima. La carpeta elegida queda como la de escaneos.
    /// </summary>
    static string? PedirDestino(IWin32Window dueño)
    {
        string carpeta = Carpeta() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        using var dlg = new SaveFileDialog
        {
            Title = Idioma.T("guardarEscaneo"),
            Filter = Idioma.T("filtroPdf"),
            DefaultExt = "pdf",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = carpeta,
            FileName = System.IO.Path.GetFileName(NombreLibre(carpeta)),
        };
        if (dlg.ShowDialog(dueño) != DialogResult.OK) return null;
        string ruta = dlg.FileName;
        // «Factura 10.22» tiene ya «extensión» para Windows y no le pondría el .pdf.
        if (!ruta.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) ruta += ".pdf";
        Ajustes.Actual.CarpetaEscaneos = System.IO.Path.GetDirectoryName(ruta);
        Ajustes.Actual.Guardar();
        return ruta;
    }

    /// <summary>«Escaneo 2026-10-31 10.22.pdf»; si ya existe, con (2), (3)…</summary>
    static string NombreLibre(string carpeta)
    {
        string baseNombre = $"{Idioma.T("escaneo")} {DateTime.Now:yyyy-MM-dd HH.mm}";
        string ruta = System.IO.Path.Combine(carpeta, baseNombre + ".pdf");
        for (int n = 2; File.Exists(ruta); n++) ruta = System.IO.Path.Combine(carpeta, $"{baseNombre} ({n}).pdf");
        return ruta;
    }
}

/// <summary>Después de cada tanda: cuántas páginas van, seguir escaneando, terminar o descartar.</summary>
sealed class DialogoEscaneo : Form
{
    public bool CambiarEscaner { get; private set; }

    readonly Label carpeta;

    public DialogoEscaneo(int paginas, string escaner, bool fotocopia)
    {
        Text = Idioma.T(fotocopia ? "fotocopiar" : "escanear");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        Font = SystemFonts.MessageBoxFont;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(LogicalToDeviceUnits(14));

        var cuantas = new Label
        {
            Text = paginas == 1 ? Idioma.T("unaPagina") : Idioma.T("paginas", paginas),
            Font = new Font(Font.FontFamily, Font.Size * 1.25f, FontStyle.Bold), AutoSize = true,
        };
        var siguiente = new Label
        {
            Text = Idioma.T(fotocopia ? "siguienteHojaCopia" : "siguienteHoja"), AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(460), 0),
            Margin = new Padding(3, LogicalToDeviceUnits(6), 3, LogicalToDeviceUnits(12)),
        };

        carpeta = new Label { AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(360), 0) };
        PonerCarpeta();
        var cambiarCarpeta = Enlace(() =>
        {
            if (Escaner.PedirCarpeta(this) is not null) PonerCarpeta();
        });
        var cambiarEscaner = Enlace(() =>
        {
            CambiarEscaner = true;
            DialogResult = DialogResult.Retry;
        });

        var datos = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(12)) };
        if (!fotocopia)
        {
            datos.Controls.Add(new Label { Text = Idioma.T("seGuardaEn"), AutoSize = true }, 0, 0);
            datos.Controls.Add(carpeta, 1, 0);
            datos.Controls.Add(cambiarCarpeta, 2, 0);
        }
        datos.Controls.Add(new Label { Text = Idioma.T("escaner"), AutoSize = true }, 0, 1);
        datos.Controls.Add(new Label { Text = escaner, AutoSize = true, MaximumSize = new Size(LogicalToDeviceUnits(360), 0) }, 1, 1);
        datos.Controls.Add(cambiarEscaner, 2, 1);

        var seguir = new Button { Text = Idioma.T("seguirEscaneando"), AutoSize = true, DialogResult = DialogResult.Retry };
        var terminar = new Button { Text = Idioma.T(fotocopia ? "imprimir" : "terminar"), AutoSize = true, DialogResult = DialogResult.OK };
        var descartar = new Button { Text = Idioma.T("descartar"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var botones = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        botones.Controls.AddRange(new Control[] { seguir, terminar, descartar });

        var todo = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        todo.Controls.AddRange(new Control[] { cuantas, siguiente, datos, botones });
        Controls.Add(todo);

        AcceptButton = seguir;
        CancelButton = descartar;
        FormClosing += (_, e) =>
        {
            // Descartar (o la X, o Esc) con páginas escaneadas: se pregunta antes de tirarlas.
            if (DialogResult == DialogResult.Cancel && !CambiarEscaner &&
                MessageBox.Show(this, Idioma.T("descartarPaginas"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                e.Cancel = true;
        };
        Shown += (_, _) => seguir.Focus();
    }

    void PonerCarpeta() => carpeta.Text = Escaner.Carpeta() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    LinkLabel Enlace(Action alPulsar)
    {
        var l = new LinkLabel { Text = Idioma.T("cambiar"), AutoSize = true };
        l.LinkClicked += (_, _) => alPulsar();
        return l;
    }
}
