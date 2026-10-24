namespace PDFbyWilly;

/// <summary>Lo que se elige al instalar.</summary>
sealed record OpcionesInstalar(bool Escritorio, bool Predeterminado, bool Anclar);

/// <summary>Ventana de instalación: casillas con lo que se quiere además del menú Inicio.</summary>
sealed class DialogoInstalar : Form
{
    readonly CheckBox escritorio = Casilla("Acceso directo en el escritorio", true);
    readonly CheckBox predeterminado = Casilla("Abrir siempre los PDF con PDFbyWilly", true);
    readonly CheckBox anclar = Casilla("Anclar a la barra de tareas", true);

    public static OpcionesInstalar? Preguntar()
    {
        using var d = new DialogoInstalar();
        return d.ShowDialog() == DialogResult.OK
            ? new(d.escritorio.Checked, d.predeterminado.Checked, d.anclar.Checked)
            : null;
    }

    static CheckBox Casilla(string texto, bool marcada) => new() { Text = texto, Checked = marcada, AutoSize = true };

    DialogoInstalar()
    {
        int E(int v) => LogicalToDeviceUnits(v);
        Text = "Instalar PDFbyWilly";
        Icon = Recursos.Icono();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;
        Font = SystemFonts.MessageBoxFont;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var titulo = new Label
        {
            Text = "Se instala para tu usuario, sin pedir permisos de administrador,\ny queda en el menú Inicio. Además:",
            AutoSize = true, Margin = new Padding(0, 0, 0, E(8)),
        };
        var notaAnclar = new Label
        {
            Text = "Windows no deja que un programa se ancle solo: al abrirse te diré dónde hacer clic.\nPara los PDF se abrirá Configuración de Windows y te diré qué pulsar.",
            AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(E(18), 0, 0, E(6)),
        };
        var instalar = new Button { Text = "Instalar", AutoSize = true, DialogResult = DialogResult.OK };
        var cancelar = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        var botones = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill,
            Margin = new Padding(0, E(10), 0, 0),
        };
        botones.Controls.AddRange([cancelar, instalar]);

        var col = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false,
            Padding = new Padding(E(16), E(14), E(16), E(10)),
        };
        foreach (var c in new CheckBox[] { escritorio, predeterminado, anclar })
            c.Margin = new Padding(0, E(3), 0, E(3));
        col.Controls.AddRange([titulo, escritorio, predeterminado, anclar, notaAnclar, botones]);
        Controls.Add(col);
        AcceptButton = instalar;
        CancelButton = cancelar;
    }
}
