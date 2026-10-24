using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace PDFbyWilly;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args is ["--instalar", ..])
        {
            var op = DialogoInstalar.Preguntar();
            if (op is not null && Instalacion.Instalar(op) && op.Predeterminado)
            {
                Instalacion.AbrirPredeterminadas();
                MessageBox.Show(Instalacion.ComoPredeterminado, Instalacion.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }
        if (args is ["--desinstalar", ..])
        {
            Instalacion.Desinstalar();
            return 0;
        }

        string? ruta = args.FirstOrDefault(a => !a.StartsWith("--"));
        if (ruta is not null) ruta = Path.GetFullPath(ruta);
        if (Instalacion.Ofrecer(ruta)) return 0;

        if (!HayWebView2())
        {
            // Viene con Windows 10/11 al día; en equipos viejos o recortados hay que instalarlo.
            if (MessageBox.Show("Para ver los PDF hace falta «Microsoft Edge WebView2», que no está en este equipo. ¿Descargarlo ahora?",
                    Instalacion.Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            return 1;
        }
        Application.Run(new Ventana(ruta, avisoAnclar: args.Contains("--aviso-anclar")));
        return 0;
    }

    static bool HayWebView2()
    {
        try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch { return false; }
    }
}
