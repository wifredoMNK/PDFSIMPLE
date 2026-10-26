using System.Reflection;

namespace PDFbyWilly;

/// <summary>
/// El nombre con que sale el programa. Del mismo código salen dos: PDFbyWilly (el instalador propio) y
/// PDFSIMPLE (la Microsoft Store). Lo fija la compilación (-p:Marca=…, ver PDFbyWilly.csproj) y llega aquí
/// como «Product» del ensamblado: título de la ventana, carpeta de ajustes, registro de Windows, icono…
/// </summary>
static class Marca
{
    public static readonly string Nombre =
        typeof(Marca).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "PDFbyWilly";
}
