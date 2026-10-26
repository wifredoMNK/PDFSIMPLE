using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PDFSIMPLE;

/// <summary>
/// Alta y baja en Windows, para todo el equipo (como se registran los lectores que Windows enseña en
/// su lista de «abrir los PDF con…»). Lo instala PDFSIMPLE-Setup.exe, que copia el .exe a Archivos de
/// programa y lo llama con «--registrar»; se quita desde Configuración → Aplicaciones, que lo llama con
/// «--desinstalar». Las dos cosas necesitan administrador.
/// Windows no deja que un programa se ponga solo como predeterminado: se abre la página donde se elige.
/// </summary>
static class Instalacion
{
    public static readonly string Nombre = Marca.Nombre;
    static readonly string ProgId = Nombre + ".Documento";
    static string Descripcion => Idioma.T("descripcion");
    static readonly string ClaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Nombre;

    static string Exe => Application.ExecutablePath;
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
            k.SetValue("", Idioma.T("documentoPdf"));
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

        using (var k = hklm.CreateSubKey($@"Software\{Nombre}\Capabilities"))
        {
            k.SetValue("ApplicationName", Nombre);
            k.SetValue("ApplicationDescription", Descripcion);
            k.SetValue("ApplicationIcon", icono);
            k.CreateSubKey("FileAssociations").SetValue(".pdf", ProgId);
        }
        using (var k = hklm.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue(Nombre, $@"Software\{Nombre}\Capabilities");

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
        if (MessageBox.Show(Idioma.T("desinstalar", Nombre), Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        foreach (var p in Process.GetProcessesByName(Nombre))
            if (p.Id != Process.GetCurrentProcess().Id) try { p.Kill(); p.WaitForExit(3000); } catch { }

        var hklm = Registry.LocalMachine;
        hklm.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        using (var k = hklm.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", true))
            k?.DeleteValue(ProgId, false);
        hklm.DeleteSubKeyTree($@"Software\Classes\Applications\{Nombre}.exe", false);
        hklm.DeleteSubKeyTree($@"Software\{Nombre}", false);
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
        MessageBox.Show(Idioma.T("desinstalado", Nombre), Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Quita la instalación antigua solo para el usuario (versiones anteriores al instalador).</summary>
    static void LimpiarDeUsuario()
    {
        var hkcu = Registry.CurrentUser;
        hkcu.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        using (var k = hkcu.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", true))
            k?.DeleteValue(ProgId, false);
        hkcu.DeleteSubKeyTree($@"Software\Classes\Applications\{Nombre}.exe", false);
        hkcu.DeleteSubKeyTree($@"Software\{Nombre}", false);
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
    /// Aplicaciones predeterminadas y el usuario elige el programa para «.pdf» (se le dice cómo).
    /// </summary>
    public static void AbrirPredeterminadas()
    {
        // Instalado desde la Microsoft Store va en WindowsApps y Windows lo conoce por el paquete, no por el registro.
        bool deLaStore = Exe.Contiene(@"\WindowsApps\");
        try { Process.Start(new ProcessStartInfo(deLaStore ? "ms-settings:defaultapps" : $"ms-settings:defaultapps?registeredAppMachine={Nombre}") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
    }

    public static string ComoPredeterminado => Idioma.T("comoPredeterminado", Nombre);

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
        lnk.Description = Descripcion;
        lnk.Save();
        Marshal.FinalReleaseComObject(lnk);
        Marshal.FinalReleaseComObject(shell);
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}
