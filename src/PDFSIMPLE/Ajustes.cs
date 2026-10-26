using System.Text.Json;

namespace PDFSIMPLE;

/// <summary>Lo que se recuerda entre sesiones, en %LOCALAPPDATA%\<marca>\ajustes.json.</summary>
sealed class Ajustes
{
    public int[]? Ventana { get; set; }          // x, y, ancho, alto
    public bool Maximizada { get; set; }
    public string? Carpeta { get; set; }         // la del último documento abierto
    public string? Certificado { get; set; }     // huella del último certificado con que se firmó
    public string? Idioma { get; set; }          // es / fr / en; sin elegir, el de Windows

    static readonly string Archivo = Path.Combine(CarpetaDatos(), "ajustes.json");
    static Ajustes? actual;

    public static Ajustes Actual => actual ??= Leer();

    public static string CarpetaDatos(string? sub = null)
    {
        string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Marca.Nombre);
        if (sub is not null) d = Path.Combine(d, sub);
        Directory.CreateDirectory(d);
        return d;
    }

    static Ajustes Leer()
    {
        try { return JsonSerializer.Deserialize<Ajustes>(File.ReadAllText(Archivo)) ?? new(); }
        catch { return new(); }
    }

    public void Guardar()
    {
        try
        {
            // Con varias ventanas abiertas se relee antes para no pisar lo que haya dejado otra.
            var disco = Leer();
            if (Ventana is not null) { disco.Ventana = Ventana; disco.Maximizada = Maximizada; }
            disco.Carpeta = Carpeta ?? disco.Carpeta;
            disco.Certificado = Certificado ?? disco.Certificado;
            disco.Idioma = Idioma ?? disco.Idioma;
            File.WriteAllText(Archivo, JsonSerializer.Serialize(disco));
        }
        catch { }
    }

    public void AplicarA(Form f)
    {
        f.StartPosition = FormStartPosition.CenterScreen;
        var pantalla = Screen.PrimaryScreen!.WorkingArea;
        var tamaño = f.LogicalToDeviceUnits(new Size(1100, 850));
        f.Size = new Size(Math.Min(tamaño.Width, pantalla.Width * 9 / 10), Math.Min(tamaño.Height, pantalla.Height * 9 / 10));
        if (Ventana is [var x, var y, var w, var h])
        {
            var r = new Rectangle(x, y, w, h);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
            {
                // Que no se meta debajo de la barra de tareas ni se salga de la pantalla.
                var zona = Screen.FromRectangle(r).WorkingArea;
                r.Width = Math.Min(r.Width, zona.Width);
                r.Height = Math.Min(r.Height, zona.Height);
                r.X = Math.Clamp(r.X, zona.Left, zona.Right - r.Width);
                r.Y = Math.Clamp(r.Y, zona.Top, zona.Bottom - r.Height);
                f.StartPosition = FormStartPosition.Manual;
                f.Bounds = r;
            }
        }
        if (Maximizada) f.WindowState = FormWindowState.Maximized;
    }

    public void RecordarDe(Form f)
    {
        var r = f.WindowState == FormWindowState.Normal ? f.Bounds : f.RestoreBounds;
        Ventana = [r.X, r.Y, r.Width, r.Height];
        Maximizada = f.WindowState == FormWindowState.Maximized;
        Guardar();
    }

    public void AnotarReciente(string archivo)
    {
        Carpeta = Path.GetDirectoryName(archivo);
        Guardar();
    }

    public string UltimaCarpeta() =>
        Carpeta is not null && Directory.Exists(Carpeta) ? Carpeta : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
}
