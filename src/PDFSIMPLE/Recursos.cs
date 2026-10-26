using System.Reflection;

namespace PDFSIMPLE;

/// <summary>
/// Los archivos de la carpeta web (pdf.js y lo nuestro) y el icono van incrustados en el .exe, para
/// que el programa sea un solo archivo. Se piden por su ruta con «/», p. ej. «web/lector/lector.js».
/// </summary>
static class Recursos
{
    static readonly Assembly Ensamblado = typeof(Recursos).Assembly;

    // MSBuild deja «\» en la parte de carpetas del nombre: se normaliza una vez.
    static readonly Dictionary<string, string> Nombres = Ensamblado.GetManifestResourceNames()
        .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.OrdinalIgnoreCase);

    public static byte[]? Leer(string ruta)
    {
        if (!Nombres.TryGetValue(ruta, out var nombre)) return null;
        using var s = Ensamblado.GetManifestResourceStream(nombre)!;
        var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static Icon Icono() => new(Ensamblado.GetManifestResourceStream(Nombres["icono.ico"])!);
}
