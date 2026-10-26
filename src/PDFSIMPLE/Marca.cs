using System.Reflection;

namespace PDFSIMPLE;

/// <summary>El nombre del programa, el «Product» del ensamblado (PDFSIMPLE.csproj): título, carpeta de ajustes, registro…</summary>
static class Marca
{
    public static readonly string Nombre =
        typeof(Marca).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "PDFSIMPLE";
}
