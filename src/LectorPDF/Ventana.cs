using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LectorPDF;

/// <summary>
/// Una ventana por documento. Dentro va el visor de pdf.js (carpeta web/pdfjs), servido desde
/// https://visor.local/ por este mismo programa; el visor le pide aquí lo que no puede hacer una
/// página web: abrir y guardar en disco, firmar con certificado y comprobar firmas.
/// </summary>
sealed class Ventana : Form
{
    const string Origen = "https://visor.local/";
    const string Visor = Origen + "pdfjs/web/viewer.html";

    readonly WebView2 web = new() { Dock = DockStyle.Fill };
    readonly string carpetaWeb = Path.Combine(AppContext.BaseDirectory, "web");

    string? ruta;          // archivo abierto
    byte[]? datos;         // su contenido tal como se cargó
    int version;           // cambia en cada carga para que el visor no tire de caché
    bool cerrarYa;

    public Ventana(string? ruta)
    {
        Text = "Lector PDF";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "lector.ico"));
        KeyPreview = true;
        Ajustes.Actual.AplicarA(this);
        Controls.Add(web);
        if (ruta is not null) Cargar(ruta);
        Load += async (_, _) => await IniciarAsync();
        FormClosing += AlCerrar;
    }

    async Task IniciarAsync()
    {
        var opciones = new CoreWebView2EnvironmentOptions { Language = "es-ES" };
        var entorno = await CoreWebView2Environment.CreateAsync(null, Ajustes.CarpetaDatos("WebView2"), opciones);
        await web.EnsureCoreWebView2Async(entorno);
        var cw = web.CoreWebView2;

        cw.Settings.AreBrowserAcceleratorKeysEnabled = false; // sin F5 / Ctrl+R que tiren los cambios
        cw.Settings.IsZoomControlEnabled = false;             // el zoom lo lleva el visor
        cw.Settings.IsStatusBarEnabled = false;
        cw.Settings.IsPasswordAutosaveEnabled = false;
        cw.Settings.IsGeneralAutofillEnabled = false;

        cw.AddWebResourceRequestedFilter(Origen + "*", CoreWebView2WebResourceContext.All);
        cw.WebResourceRequested += AlPedir;
        cw.WebMessageReceived += AlRecibirMensaje;
        cw.NewWindowRequested += (_, e) => { e.Handled = true; AbrirFuera(e.Uri); };
        cw.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith(Origen, StringComparison.OrdinalIgnoreCase)) { e.Cancel = true; AbrirFuera(e.Uri); }
        };
        cw.ContainsFullScreenElementChanged += (_, _) => PantallaCompleta(cw.ContainsFullScreenElement);
        cw.Navigate(Visor);
    }

    FormWindowState estadoPrevio;
    void PantallaCompleta(bool si)
    {
        if (si)
        {
            estadoPrevio = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = estadoPrevio;
        }
    }

    static void AbrirFuera(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ---------------------------------------------------------------- documento

    void Cargar(string archivo)
    {
        using (var fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var ms = new MemoryStream();
            fs.CopyTo(ms);
            datos = ms.ToArray();
        }
        ruta = archivo;
        version++;
        Text = $"{Path.GetFileName(archivo)} — Lector PDF";
        Ajustes.Actual.AnotarReciente(archivo);
    }

    /// <summary>Abre aquí si la ventana está vacía; si ya hay un documento, en una ventana nueva.</summary>
    void Abrir(string archivo)
    {
        if (ruta is null)
        {
            Cargar(archivo);
            _ = web.CoreWebView2?.ExecuteScriptAsync("lector.recargar()");
        }
        else Process.Start(Environment.ProcessPath!, $"\"{archivo}\"");
    }

    string? PedirRutaGuardar(string sugerido)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Documento PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = Path.GetFileName(sugerido),
            InitialDirectory = Path.GetDirectoryName(sugerido) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
    }

    static void Escribir(string destino, byte[] contenido)
    {
        // Primero a un temporal en la misma carpeta y luego se cambia por el bueno: si algo falla
        // a medias, el original sigue entero.
        string tmp = destino + ".guardando";
        File.WriteAllBytes(tmp, contenido);
        File.Move(tmp, destino, overwrite: true);
    }

    // ---------------------------------------------------------------- cierre

    async void AlCerrar(object? sender, FormClosingEventArgs e)
    {
        Ajustes.Actual.RecordarDe(this);
        if (cerrarYa || web.CoreWebView2 is null || ruta is null) return;

        e.Cancel = true;
        string r = await web.CoreWebView2.ExecuteScriptAsync("window.lector ? lector.hayCambios() : false");
        if (r != "true") { cerrarYa = true; Close(); return; }

        var resp = MessageBox.Show(this, $"¿Guardar los cambios en {Path.GetFileName(ruta)}?", "Lector PDF",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (resp == DialogResult.Cancel) return;
        if (resp == DialogResult.No) { cerrarYa = true; Close(); return; }
        await web.CoreWebView2.ExecuteScriptAsync("lector.guardar(false, true)");
    }

    // ---------------------------------------------------------------- arrastrar archivos

    void AlRecibirMensaje(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.TryGetWebMessageAsString() != "soltar" || e.AdditionalObjects is null) return;
        foreach (var o in e.AdditionalObjects)
            if (o is CoreWebView2File f && f.Path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                Abrir(f.Path);
    }

    // ---------------------------------------------------------------- servidor de visor.local

    void AlPedir(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        string camino = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');

        if (camino.StartsWith("api/"))
        {
            byte[] cuerpo = Array.Empty<byte>();
            if (e.Request.Content is { } s) { var ms = new MemoryStream(); s.CopyTo(ms); cuerpo = ms.ToArray(); }
            var cabeceras = e.Request.Headers;
            string? arg = cabeceras.Contains("X-Args") ? Uri.UnescapeDataString(cabeceras.GetHeader("X-Args")) : null;

            // Las ventanas modales no pueden salir dentro del evento: se cogen la respuesta en diferido.
            var diferido = e.GetDeferral();
            BeginInvoke(async () =>
            {
                try
                {
                    object res = await Api(camino[4..], cuerpo, arg);
                    e.Response = Json(res);
                }
                catch (Exception ex)
                {
                    e.Response = Json(new { error = ex.Message });
                }
                finally { diferido.Complete(); }
            });
            return;
        }

        if (camino.StartsWith("doc/"))
        {
            e.Response = datos is null ? Respuesta(404, "text/plain", Array.Empty<byte>())
                                       : Respuesta(200, "application/pdf", datos);
            return;
        }

        string archivo = Path.GetFullPath(Path.Combine(carpetaWeb, camino));
        if (!archivo.StartsWith(carpetaWeb, StringComparison.OrdinalIgnoreCase) || !File.Exists(archivo))
        {
            e.Response = Respuesta(404, "text/plain", Array.Empty<byte>());
            return;
        }
        byte[] contenido = File.ReadAllBytes(archivo);
        if (camino == "pdfjs/web/viewer.html")
        {
            // Se engancha lo nuestro sin tocar el viewer.html de pdf.js, para poder actualizarlo tal cual.
            string html = Encoding.UTF8.GetString(contenido).Replace(
                "<script src=\"viewer.mjs\" type=\"module\"></script>",
                "<link rel=\"stylesheet\" href=\"../../lector/lector.css\" />\n  <script src=\"../../lector/lector.js\"></script>\n  <script src=\"viewer.mjs\" type=\"module\"></script>");
            contenido = Encoding.UTF8.GetBytes(html);
        }
        else if (camino == "pdfjs/web/locale/es-ES/viewer.ftl")
        {
            contenido = [.. contenido, .. File.ReadAllBytes(Path.Combine(carpetaWeb, "lector", "es-ES.ftl"))];
        }
        e.Response = Respuesta(200, TipoDe(archivo), contenido);
    }

    async Task<object> Api(string metodo, byte[] cuerpo, string? arg)
    {
        switch (metodo)
        {
            case "estado":
                return new { hay = datos is not null, nombre = ruta is null ? null : Path.GetFileName(ruta), version };

            case "abrir":
            {
                using var dlg = new OpenFileDialog
                {
                    Filter = "Documento PDF (*.pdf)|*.pdf|Todos los archivos (*.*)|*.*",
                    InitialDirectory = ruta is null ? Ajustes.Actual.UltimaCarpeta() : Path.GetDirectoryName(ruta),
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return new { cancelado = true };
                bool aqui = ruta is null;
                Abrir(dlg.FileName);
                return new { ok = true, aqui };
            }

            case "guardar":
            {
                var a = JsonNode.Parse(arg ?? "{}")!;
                bool como = a["como"]?.GetValue<bool>() ?? false;
                bool cerrar = a["cerrar"]?.GetValue<bool>() ?? false;
                string? destino = ruta;
                if (como || destino is null)
                {
                    destino = PedirRutaGuardar(ruta ?? Path.Combine(Ajustes.Actual.UltimaCarpeta(), a["nombre"]?.GetValue<string>() ?? "documento.pdf"));
                    if (destino is null) return new { cancelado = true };
                }
                Escribir(destino, cuerpo);
                if (cerrar) { cerrarYa = true; BeginInvoke(Close); return new { ok = true }; }
                Cargar(destino);
                return new { ok = true, nombre = Path.GetFileName(destino), version };
            }

            case "firmar":
            {
                var a = JsonNode.Parse(arg ?? "{}")!;
                var cert = DialogoCertificado.Elegir(this);
                if (cert is null) return new { cancelado = true };

                int pagina = a["pagina"]?.GetValue<int>() ?? 1;
                float[]? rect = a["rect"] is JsonArray r ? r.Select(v => v!.GetValue<float>()).ToArray() : null;

                Cursor = Cursors.WaitCursor;
                Firmador.Resultado firmado;
                try { firmado = await Task.Run(() => Firmador.Firmar(cuerpo, cert, pagina, rect)); }
                finally { Cursor = Cursors.Default; }

                string sugerido = ruta is null
                    ? Path.Combine(Ajustes.Actual.UltimaCarpeta(), "documento-firmado.pdf")
                    : Path.Combine(Path.GetDirectoryName(ruta)!, Path.GetFileNameWithoutExtension(ruta) + "-firmado.pdf");
                string? destino = PedirRutaGuardar(sugerido);
                if (destino is null) return new { cancelado = true };
                Escribir(destino, firmado.Pdf);
                Cargar(destino);
                return new { ok = true, nombre = Path.GetFileName(destino), version, aviso = firmado.Aviso };
            }

            case "verificar":
            {
                var a = JsonNode.Parse(arg ?? "{}")!;
                int largo = a["largo"]!.GetValue<int>();
                return await Task.Run(() => Verificador.Verificar(
                    cuerpo.AsSpan(0, largo).ToArray(), cuerpo.AsSpan(largo).ToArray(),
                    a["subFilter"]?.GetValue<string>(), a["signingTime"]?.GetValue<string>()));
            }

            case "vercert":
            {
                var a = JsonNode.Parse(Encoding.UTF8.GetString(cuerpo))!;
                var c = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(Convert.FromBase64String(a["der"]!.GetValue<string>()));
                System.Security.Cryptography.X509Certificates.X509Certificate2UI.DisplayCertificate(c, Handle);
                return new { ok = true };
            }

            case "cerrar":
                cerrarYa = true;
                BeginInvoke(Close);
                return new { ok = true };
        }
        throw new InvalidOperationException("Orden desconocida: " + metodo);
    }

    CoreWebView2WebResourceResponse Json(object o) =>
        Respuesta(200, "application/json", JsonSerializer.SerializeToUtf8Bytes(o));

    CoreWebView2WebResourceResponse Respuesta(int estado, string tipo, byte[] cuerpo) =>
        web.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(cuerpo), estado, estado == 200 ? "OK" : "Not Found",
            $"Content-Type: {tipo}\r\nContent-Length: {cuerpo.Length}\r\nCache-Control: no-store");

    static string TipoDe(string archivo) => Path.GetExtension(archivo).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".mjs" or ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".ftl" => "text/plain; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".wasm" => "application/wasm",
        _ => "application/octet-stream",
    };
}
