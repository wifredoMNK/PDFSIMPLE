using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PDFbyWilly;

/// <summary>
/// El .exe se instala solo, para el usuario actual y sin pedir administrador: se copia a
/// %LOCALAPPDATA%\Programs\PDFbyWilly, pone su acceso en el menú Inicio, se da de alta para abrir .pdf
/// y aparece en Configuración → Aplicaciones para desinstalarlo. Windows no deja que un programa se
/// ponga solo como predeterminado: al terminar se abre la página donde el usuario lo elige.
/// </summary>
static class Instalacion
{
    public const string Nombre = "PDFbyWilly";
    const string ProgId = "PDFbyWilly.Documento";
    const string ClaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PDFbyWilly";

    public static string Carpeta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Nombre);
    public static string Exe => Path.Combine(Carpeta, Nombre + ".exe");
    static string Acceso => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Nombre + ".lnk");
    static string AccesoEscritorio => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Nombre + ".lnk");
    static string Actual => Environment.ProcessPath!;

    public static bool EsLaInstalada => string.Equals(Path.GetFullPath(Actual), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase);

    /// <summary>¿Hay que ofrecer instalar? Si no está instalado, o lo instalado es otra versión.</summary>
    public static bool HaceFalta()
    {
        if (EsLaInstalada) return false;
        if (!File.Exists(Exe)) return true;
        var a = new FileInfo(Actual);
        var b = new FileInfo(Exe);
        return a.Length != b.Length || FileVersionInfo.GetVersionInfo(Actual).FileVersion != FileVersionInfo.GetVersionInfo(Exe).FileVersion;
    }

    /// <summary>Al arrancar desde fuera (Descargas, un USB…): ofrece instalar o actualizar. Devuelve true si se ha instalado y relanzado.</summary>
    public static bool Ofrecer(string? archivo)
    {
        if (!HaceFalta() || (Ajustes.Actual.NoInstalar && !File.Exists(Exe))) return false;

        OpcionesInstalar? op;
        if (File.Exists(Exe))
        {
            // Actualizar: se cambia el .exe y se respeta lo que ya hubiera (escritorio, predeterminado…).
            if (MessageBox.Show($"Hay otra versión de {Nombre} instalada en este equipo. ¿Cambiarla por esta?",
                    Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return false;
            op = null;
        }
        else
        {
            op = DialogoInstalar.Preguntar();
            if (op is null) { Ajustes.Actual.NoInstalar = true; Ajustes.Actual.Guardar(); return false; }
        }
        if (!Instalar(op)) return false;

        var args = new List<string>();
        if (op?.Anclar == true) args.Add("--aviso-anclar");
        if (archivo is not null) args.Add($"\"{archivo}\"");
        Process.Start(Exe, string.Join(' ', args));
        if (op?.Predeterminado == true)
        {
            AbrirPredeterminadas();
            MessageBox.Show(ComoPredeterminado, Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return true;
    }

    /// <summary>Copia el .exe a su sitio y lo da de alta. Sin opciones (actualizar o «predeterminado» del menú), solo lo imprescindible.</summary>
    public static bool Instalar(OpcionesInstalar? op)
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            if (!EsLaInstalada) File.Copy(Actual, Exe, overwrite: true);
        }
        catch (IOException)
        {
            MessageBox.Show($"No se ha podido copiar: cierra las ventanas de {Nombre} que estén abiertas y vuelve a intentarlo.",
                Nombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        CrearAcceso(Acceso);
        if (op?.Escritorio == true || File.Exists(AccesoEscritorio)) CrearAcceso(AccesoEscritorio);
        Registrar();
        return true;
    }

    /// <summary>
    /// Windows no deja que un programa se ponga solo como predeterminado: se abre su página de
    /// Aplicaciones predeterminadas y el usuario elige PDFbyWilly para «.pdf» (se le dice cómo).
    /// </summary>
    public static void AbrirPredeterminadas()
    {
        try { Process.Start(new ProcessStartInfo($"ms-settings:defaultapps?registeredAppUser={Nombre}") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
    }

    public const string ComoPredeterminado =
        "En Configuración: escribe .pdf en el buscador de arriba, pulsa la casilla que sale debajo, elige PDFbyWilly y «Establecer predeterminado».";
    public static void Desinstalar()
    {
        if (MessageBox.Show($"¿Desinstalar {Nombre}? También se borran sus ajustes y las firmas manuscritas guardadas.",
                Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        Desregistrar();
        try { File.Delete(Acceso); File.Delete(AccesoEscritorio); } catch { }
        Registry.CurrentUser.DeleteSubKeyTree(ClaveDesinstalar, false);

        // El .exe que corre no se puede borrar a sí mismo: lo hace un cmd cuando este proceso ya ha salido.
        string datos = Ajustes.CarpetaDatos();
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{Carpeta}\" & rmdir /s /q \"{datos}\"")
        { CreateNoWindow = true, UseShellExecute = false });
        MessageBox.Show($"{Nombre} se ha desinstalado.", Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    static void Registrar()
    {
        string icono = $"\"{Exe}\",0";
        string orden = $"\"{Exe}\" \"%1\"";

        using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            k.SetValue("", "Documento PDF");
            k.CreateSubKey("DefaultIcon").SetValue("", icono);
            k.CreateSubKey(@"shell\open\command").SetValue("", orden);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids"))
            k.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);

        using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{Nombre}.exe"))
        {
            k.SetValue("FriendlyAppName", Nombre);
            k.CreateSubKey(@"shell\open\command").SetValue("", orden);
            k.CreateSubKey("SupportedTypes").SetValue(".pdf", "");
        }

        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\PDFbyWilly\Capabilities"))
        {
            k.SetValue("ApplicationName", Nombre);
            k.SetValue("ApplicationDescription", "Lector de PDF sin publicidad: formularios, texto y firma digital");
            k.CreateSubKey("FileAssociations").SetValue(".pdf", ProgId);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue(Nombre, @"Software\PDFbyWilly\Capabilities");

        using (var k = Registry.CurrentUser.CreateSubKey(ClaveDesinstalar))
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

    static void Desregistrar()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", true))
            k?.DeleteValue(ProgId, false);
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Applications\{Nombre}.exe", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\PDFbyWilly", false);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue(Nombre, false);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}
