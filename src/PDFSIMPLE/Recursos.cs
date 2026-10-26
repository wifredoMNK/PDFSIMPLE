using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace PDFSIMPLE;

/// <summary>
/// Lo que va dentro del .exe: el visor (pdf.js y lo nuestro) en un zip, el icono y el cargador nativo de
/// WebView2 de cada procesador. Los archivos del visor se piden por su ruta con «/», p. ej. «web/lector/lector.js».
/// </summary>
static class Recursos
{
    static readonly Assembly Ensamblado = typeof(Recursos).Assembly;
    static readonly object Cerrojo = new();
    static ZipArchive? visor;
    static Dictionary<string, ZipArchiveEntry>? entradas;

    public static byte[]? Leer(string ruta)
    {
        lock (Cerrojo)
        {
            if (entradas is null)
            {
                visor = new ZipArchive(Ensamblado.GetManifestResourceStream("web.zip")!, ZipArchiveMode.Read);
                entradas = visor.Entries.ToDictionary(e => "web/" + e.FullName.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
            }
            if (!entradas.TryGetValue(ruta, out var entrada)) return null;
            using var s = entrada.Open();
            var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }

    public static Icon Icono() => new(Ensamblado.GetManifestResourceStream("icono.ico")!);

    /// <summary>
    /// WebView2 necesita su cargador nativo (WebView2Loader.dll) del mismo procesador que este proceso.
    /// Va dentro del .exe; se deja en la carpeta de datos y se le dice a WebView2 dónde está. Antes de usar WebView2.
    /// </summary>
    public static void PrepararWebView2()
    {
        string arq = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        using var origen = Ensamblado.GetManifestResourceStream($"loader/{arq}/WebView2Loader.dll")!;
        string carpeta = Ajustes.CarpetaDatos(Path.Combine("loader", arq));
        string destino = Path.Combine(carpeta, "WebView2Loader.dll");
        if (!File.Exists(destino) || new FileInfo(destino).Length != origen.Length)
        {
            try
            {
                using var f = File.Create(destino);
                origen.CopyTo(f);
            }
            catch (IOException) { } // otra ventana lo está usando: ya está ahí
        }
        CoreWebView2Environment.SetLoaderDllFolderPath(carpeta);
    }
}
