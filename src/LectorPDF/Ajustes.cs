using System.Text.Json;

namespace LectorPDF;

/// <summary>Lo que se recuerda entre sesiones, en %LOCALAPPDATA%\LectorPDF\ajustes.json.</summary>
sealed class Ajustes
{
    public int[]? Ventana { get; set; }          // x, y, ancho, alto
    public bool Maximizada { get; set; }
    public string? Carpeta { get; set; }         // la del último documento abierto
    public string? Certificado { get; set; }     // huella del último certificado con que se firmó

    static readonly string Archivo = Path.Combine(CarpetaDatos(), "ajustes.json");
    static Ajustes? actual;

    public static Ajustes Actual => actual ??= Leer();

    public static string CarpetaDatos(string? sub = null)
    {
        string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LectorPDF");
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
            disco.Ventana = Ventana; disco.Maximizada = Maximizada;
            disco.Carpeta = Carpeta ?? disco.Carpeta;
            disco.Certificado = Certificado ?? disco.Certificado;
            File.WriteAllText(Archivo, JsonSerializer.Serialize(disco));
        }
        catch { }
    }

    public void AplicarA(Form f)
    {
        f.StartPosition = FormStartPosition.WindowsDefaultLocation;
        var pantalla = Screen.PrimaryScreen!.WorkingArea;
        var tamaño = f.LogicalToDeviceUnits(new Size(1100, 850));
        f.Size = new Size(Math.Min(tamaño.Width, pantalla.Width * 9 / 10), Math.Min(tamaño.Height, pantalla.Height * 9 / 10));
        if (Ventana is [var x, var y, var w, var h])
        {
            var r = new Rectangle(x, y, w, h);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
            {
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
