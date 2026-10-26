using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PDFbyWilly;

/// <summary>
/// Alta y baja en Windows, para todo el equipo (como se registran los lectores que Windows enseña en
/// su lista de «abrir los PDF con…»). Lo instala PDFbyWilly-Setup.exe, que copia el .exe a Archivos de
/// programa y lo llama con «--registrar»; se quita desde Configuración → Aplicaciones, que lo llama con
/// «--desinstalar». Las dos cosas necesitan administrador.
/// Windows no deja que un programa se ponga solo como predeterminado: se abre la página donde se elige.
/// </summary>
static class Instalacion
{
    public const string Nombre = "PDFbyWilly";
    const string ProgId = "PDFbyWilly.Documento";
    const string Descripcion = "Lector de PDF sin publicidad: formularios, texto y firma digital";
    const string ClaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PDFbyWilly";

    static string Exe => Environment.ProcessPath!;
    static string Carpeta => Path.GetDirectoryName(Exe)!;
    static string Acceso => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Nombre + ".lnk");
    static string AccesoEscritorio => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Nombre + ".lnk");

    /// <summary>Lo llama el instalador, ya con permisos de administrador, sobre el .exe recién copiado.</summary>
    public static void Registrar(bool escritorio)
    {
        LimpiarDeUsuario();
        CrearAcceso(Acceso);
        if (escritorio) CrearAcceso(AccesoEscritorio);
        else BorrarSiHay(AccesoEscritorio);

        string icono = $"\"{Exe}\",0";
        string orden = $"\"{Exe}\" \"%1\"";
        var hklm = Registry.LocalMachine;

        using (var k = hklm.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            k.SetValue("", "Documento PDF");
            k.CreateSubKey("DefaultIcon").SetValue("", icono);
            k.CreateSubKey(@"shell\open\command").SetValue("", orden);
            using var app = k.CreateSubKey("Application");
            app.SetValue("ApplicationName", Nombre);
            app.SetValue("ApplicationIcon", icono);
            app.SetValue("ApplicationCompany", "Willy");
            app.SetValue("ApplicationDescription", Descripcion);
        }
        using (var k = hklm.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids"))
            k.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);

        using (var k = hklm.CreateSubKey($@"Software\Classes\Applications\{Nombre}.exe"))
        {
            k.SetValue("FriendlyAppName", Nombre);
            k.CreateSubKey("DefaultIcon").SetValue("", icono);
            k.CreateSubKey(@"shell\open\command").SetValue("", orden);
            k.CreateSubKey("SupportedTypes").SetValue(".pdf", "");
        }

        using (var k = hklm.CreateSubKey(@"Software\PDFbyWilly\Capabilities"))
        {
            k.SetValue("ApplicationName", Nombre);
            k.SetValue("ApplicationDescription", Descripcion);
            k.SetValue("ApplicationIcon", icono);
            k.CreateSubKey("FileAssociations").SetValue(".pdf", ProgId);
        }
        using (var k = hklm.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue(Nombre, @"Software\PDFbyWilly\Capabilities");

        using (var k = hklm.CreateSubKey(ClaveDesinstalar))
        {
            k.SetValue("DisplayName", Nombre);
            k.SetValue("DisplayIcon", icono);
            k.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(Exe).ProductVersion ?? "");
            k.SetValue("Publisher", "Willy");
            k.SetValue("InstallLocation", Carpeta);
            k.SetValue("UninstallString", $"\"{Exe}\" --desinstalar");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024), RegistryValueKind.DWord);
        }

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Desinstalar()
    {
        if (!EsAdministrador())
        {
            // Desde Configuración llega sin permisos: se pide elevación y se repite.
            try { Process.Start(new ProcessStartInfo(Exe, "--desinstalar") { UseShellExecute = true, Verb = "runas" }); }
            catch { }
            return;
        }
        if (MessageBox.Show($"¿Desinstalar {Nombre}?", Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        foreach (var p in Process.GetProcessesByName(Nombre))
            if (p.Id != Environment.ProcessId) try { p.Kill(); p.WaitForExit(3000); } catch { }

        var hklm = Registry.LocalMachine;
        hklm.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        using (var k = hklm.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", true))
            k?.DeleteValue(ProgId, false);
        hklm.DeleteSubKeyTree($@"Software\Classes\Applications\{Nombre}.exe", false);
        hklm.DeleteSubKeyTree(@"Software\PDFbyWilly", false);
        using (var k = hklm.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue(Nombre, false);
        hklm.DeleteSubKeyTree(ClaveDesinstalar, false);
        BorrarSiHay(Acceso);
        BorrarSiHay(AccesoEscritorio);
        LimpiarDeUsuario();
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

        // El .exe que corre no se puede borrar a sí mismo: lo hace un cmd cuando este proceso ya ha salido.
        // Los ajustes y las firmas manuscritas guardadas se quedan, por si se vuelve a instalar.
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{Carpeta}\"")
        { CreateNoWindow = true, UseShellExecute = false });
        MessageBox.Show($"{Nombre} se ha desinstalado.", Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Quita la instalación antigua solo para el usuario (versiones anteriores al instalador).</summary>
    static void LimpiarDeUsuario()
    {
        var hkcu = Registry.CurrentUser;
        hkcu.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        using (var k = hkcu.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", true))
            k?.DeleteValue(ProgId, false);
        hkcu.DeleteSubKeyTree($@"Software\Classes\Applications\{Nombre}.exe", false);
        hkcu.DeleteSubKeyTree(@"Software\PDFbyWilly", false);
        using (var k = hkcu.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue(Nombre, false);
        hkcu.DeleteSubKeyTree(ClaveDesinstalar, false);
        BorrarSiHay(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Nombre + ".lnk"));
        BorrarSiHay(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Nombre + ".lnk"));
        string vieja = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Nombre);
        if (Directory.Exists(vieja) && !string.Equals(vieja, Carpeta, StringComparison.OrdinalIgnoreCase))
            try { Directory.Delete(vieja, true); } catch { }
    }

    /// <summary>
    /// Windows no deja que un programa se ponga solo como predeterminado: se abre su página de
    /// Aplicaciones predeterminadas y el usuario elige PDFbyWilly para «.pdf» (se le dice cómo).
    /// </summary>
    public static void AbrirPredeterminadas()
    {
        try { Process.Start(new ProcessStartInfo($"ms-settings:defaultapps?registeredAppMachine={Nombre}") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
    }

    public const string ComoPredeterminado =
        "En Configuración: escribe .pdf en el buscador de arriba, pulsa la casilla que sale debajo, elige PDFbyWilly y «Establecer predeterminado».";

    static bool EsAdministrador() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    static void BorrarSiHay(string archivo)
    {
        try { if (File.Exists(archivo)) File.Delete(archivo); } catch { }
    }

    static void CrearAcceso(string donde)
    {
        var tipo = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(tipo)!;
        dynamic lnk = shell.CreateShortcut(donde);
        lnk.TargetPath = Exe;
        lnk.WorkingDirectory = Carpeta;
        lnk.IconLocation = Exe + ",0";
        lnk.Description = "Lector de PDF: formularios, texto y firma digital";
        lnk.Save();
        Marshal.FinalReleaseComObject(lnk);
        Marshal.FinalReleaseComObject(shell);
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}
