namespace LectorPDF;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--registrar")
        {
            Registro.Registrar();
            return 0;
        }
        if (args.Length > 0 && args[0] == "--desregistrar")
        {
            Registro.Desregistrar();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        string? ruta = args.FirstOrDefault(a => !a.StartsWith("--"));
        Application.Run(new Ventana(ruta is null ? null : Path.GetFullPath(ruta)));
        return 0;
    }
}
