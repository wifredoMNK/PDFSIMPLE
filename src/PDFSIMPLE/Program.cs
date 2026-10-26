using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace PDFSIMPLE;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Las llama el instalador (PDFSIMPLE-Setup.exe) y Configuración → Aplicaciones.
        string primero = args.Length > 0 ? args[0] : "";
        if (primero == "--registrar")
        {
            Instalacion.Registrar(escritorio: args.Contains("--escritorio"));
            return 0;
        }
        if (primero == "--desinstalar")
        {
            Instalacion.Desinstalar();
            return 0;
        }

        Recursos.PrepararWebView2();
        if (!HayWebView2())
        {
            // Viene con Windows 10/11 al día; en equipos viejos o recortados hay que instalarlo.
            if (MessageBox.Show(Idioma.T("faltaWebView2"),
                    Instalacion.Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            return 1;
        }

        string? ruta = args.FirstOrDefault(a => !a.StartsWith("--"));
        // «--vista=zoom=page-fit&panel=1&barra=completa»: vista inicial (para las capturas de la Store; ver lector.js).
        string? vista = args.FirstOrDefault(a => a.StartsWith("--vista="))?.Substring("--vista=".Length);
        Application.Run(new Ventana(ruta is null ? null : Path.GetFullPath(ruta), vista));
        return 0;
    }

    static bool HayWebView2()
    {
        try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch { return false; }
    }
}
