using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LectorPDF;

/// <summary>
/// Da de alta el programa como lector de .pdf para el usuario actual (sin permisos de administrador).
/// Windows no deja que un programa se ponga solo como predeterminado: queda en «Abrir con» y en
/// Configuración → Aplicaciones predeterminadas, y el usuario lo elige una vez.
/// </summary>
static class Registro
{
    const string ProgId = "LectorPDF.Documento";
    const string Nombre = "Lector PDF";

    public static void Registrar()
    {
        string exe = Environment.ProcessPath!;
        string icono = $"\"{exe}\",0";
        string orden = $"\"{exe}\" \"%1\"";

        using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            k.SetValue("", "Documento PDF");
            k.CreateSubKey("DefaultIcon").SetValue("", icono);
            k.CreateSubKey(@"shell\open\command").SetValue("", orden);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids"))
            k.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);

        string app = Path.GetFileName(exe);
        using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{app}"))
        {
            k.SetValue("FriendlyAppName", Nombre);
            k.CreateSubKey(@"shell\open\command").SetValue("", orden);
            k.CreateSubKey("SupportedTypes").SetValue(".pdf", "");
        }

        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\LectorPDF\Capabilities"))
        {
            k.SetValue("ApplicationName", Nombre);
            k.SetValue("ApplicationDescription", "Lector de PDF sin publicidad: formularios, texto y firma digital");
            k.CreateSubKey("FileAssociations").SetValue(".pdf", ProgId);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue(Nombre, @"Software\LectorPDF\Capabilities");

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Desregistrar()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", true))
            k?.DeleteValue(ProgId, false);
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Applications\{Path.GetFileName(Environment.ProcessPath!)}", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\LectorPDF", false);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue(Nombre, false);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}
