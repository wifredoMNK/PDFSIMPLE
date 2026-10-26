using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace PDFSIMPLE;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Las llama el instalador (PDFSIMPLE-Setup.exe) y Configuración → Aplicaciones.
        if (args is ["--registrar", ..])
        {
            Instalacion.Registrar(escritorio: args.Contains("--escritorio"));
            return 0;
        }
        if (args is ["--desinstalar", ..])
        {
            Instalacion.Desinstalar();
            return 0;
        }

        if (!HayWebView2())
        {
            // Viene con Windows 10/11 al día; en equipos viejos o recortados hay que instalarlo.
            if (MessageBox.Show(Idioma.T("faltaWebView2"),
                    Instalacion.Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            return 1;
        }

        string? ruta = args.FirstOrDefault(a => !a.StartsWith("--"));
        Application.Run(new Ventana(ruta is null ? null : Path.GetFullPath(ruta)));
        return 0;
    }

    static bool HayWebView2()
    {
        try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch { return false; }
    }
}
