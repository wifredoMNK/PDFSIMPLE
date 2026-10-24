using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PDFbyWilly;

/// <summary>
/// Elige el certificado con que firmar: los personales instalados en Windows (donde van el de la
/// FNMT, el del DNIe con su lector, etc.) o un archivo .pfx / .p12 con su contraseña.
/// </summary>
sealed class DialogoCertificado : Form
{
    readonly ListView lista = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
        HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable,
    };
    readonly List<X509Certificate2> certificados;
    X509Certificate2? elegido;

    public static X509Certificate2? Elegir(IWin32Window dueño)
    {
        using var d = new DialogoCertificado();
        return d.ShowDialog(dueño) == DialogResult.OK ? d.elegido : null;
    }

    DialogoCertificado()
    {
        Text = "Firmar con certificado";
        StartPosition = FormStartPosition.CenterParent;
        Size = LogicalToDeviceUnits(new Size(720, 380));
        MinimumSize = LogicalToDeviceUnits(new Size(520, 260));
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        Font = SystemFonts.MessageBoxFont;

        lista.Columns.Add("Titular", LogicalToDeviceUnits(330));
        lista.Columns.Add("Emitido por", LogicalToDeviceUnits(220));
        lista.Columns.Add("Caduca", LogicalToDeviceUnits(100));

        certificados = Personales();
        foreach (var c in certificados)
        {
            var it = new ListViewItem(c.GetNameInfo(X509NameType.SimpleName, false));
            it.SubItems.Add(c.GetNameInfo(X509NameType.SimpleName, true));
            it.SubItems.Add(c.NotAfter.ToString("dd/MM/yyyy"));
            it.Tag = c;
            lista.Items.Add(it);
            if (c.Thumbprint == Ajustes.Actual.Certificado) it.Selected = true;
        }
        if (lista.SelectedItems.Count == 0 && lista.Items.Count > 0) lista.Items[0].Selected = true;

        var firmar = new Button { Text = "Firmar", AutoSize = true, DialogResult = DialogResult.None };
        var cancelar = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ver = new Button { Text = "Ver certificado", AutoSize = true };
        var archivo = new Button { Text = "Usar archivo .pfx / .p12…", AutoSize = true };

        firmar.Click += (_, _) => Aceptar();
        lista.DoubleClick += (_, _) => Aceptar();
        ver.Click += (_, _) =>
        {
            if (lista.SelectedItems.Count == 1) X509Certificate2UI.DisplayCertificate((X509Certificate2)lista.SelectedItems[0].Tag!, Handle);
        };
        archivo.Click += (_, _) => DesdeArchivo();

        var aviso = new Label
        {
            Dock = DockStyle.Top, AutoSize = false, Height = LogicalToDeviceUnits(34), Padding = new Padding(2, 4, 2, 0),
            Text = certificados.Count == 0
                ? "No hay certificados personales instalados en Windows. Puedes usar un archivo .pfx / .p12."
                : "Certificados personales instalados en Windows:",
        };
        var botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(4) };
        botones.Controls.AddRange([cancelar, firmar, archivo, ver]);

        var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 0) };
        cuerpo.Controls.Add(lista);
        cuerpo.Controls.Add(aviso);
        Controls.Add(cuerpo);
        Controls.Add(botones);
        AcceptButton = firmar;
        CancelButton = cancelar;
    }

    void Aceptar()
    {
        if (lista.SelectedItems.Count != 1) return;
        elegido = (X509Certificate2)lista.SelectedItems[0].Tag!;
        Ajustes.Actual.Certificado = elegido.Thumbprint;
        Ajustes.Actual.Guardar();
        DialogResult = DialogResult.OK;
    }

    void DesdeArchivo()
    {
        using var dlg = new OpenFileDialog { Filter = "Certificado con clave (*.pfx;*.p12)|*.pfx;*.p12" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        while (true)
        {
            string? clave = PedirClave(Path.GetFileName(dlg.FileName));
            if (clave is null) return;
            try
            {
                var todos = X509CertificateLoader.LoadPkcs12CollectionFromFile(dlg.FileName, clave, X509KeyStorageFlags.EphemeralKeySet);
                elegido = todos.FirstOrDefault(c => c.HasPrivateKey)
                          ?? throw new CryptographicException("El archivo no lleva la clave privada.");
                DialogResult = DialogResult.OK;
                return;
            }
            catch (CryptographicException ex)
            {
                MessageBox.Show(this, "No se ha podido abrir el certificado: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    string? PedirClave(string nombre)
    {
        int E(int v) => LogicalToDeviceUnits(v);
        using var f = new Form
        {
            Text = "Contraseña del certificado", StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
            ShowInTaskbar = false, ClientSize = new Size(E(380), E(120)), Font = Font,
        };
        var et = new Label { Text = nombre, Location = new Point(E(12), E(12)), AutoSize = true };
        var caja = new TextBox { UseSystemPasswordChar = true, Location = new Point(E(12), E(38)), Width = E(356) };
        var ok = new Button { Text = "Aceptar", DialogResult = DialogResult.OK, Location = new Point(E(196), E(80)), AutoSize = true };
        var no = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(E(286), E(80)), AutoSize = true };
        f.Controls.AddRange([et, caja, ok, no]);
        f.AcceptButton = ok; f.CancelButton = no;
        return f.ShowDialog(this) == DialogResult.OK ? caja.Text : null;
    }

    /// <summary>Los del almacén personal que sirven para firmar: con clave privada, en vigor y con uso de firma.</summary>
    static List<X509Certificate2> Personales()
    {
        using var almacen = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        almacen.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var ahora = DateTime.Now;
        return almacen.Certificates
            .Where(c => c.HasPrivateKey && c.NotBefore <= ahora && c.NotAfter >= ahora && SirveParaFirmar(c))
            .OrderBy(c => c.GetNameInfo(X509NameType.SimpleName, false))
            .ThenByDescending(c => c.NotAfter)
            .ToList();
    }

    static bool SirveParaFirmar(X509Certificate2 c)
    {
        var uso = c.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        // Fuera los autofirmados: son de Windows, de Fiddler, de desarrollo… no de una persona.
        if (c.SubjectName.RawData.AsSpan().SequenceEqual(c.IssuerName.RawData))
            return false;
        if (uso is not null && (uso.KeyUsages & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation)) == 0)
            return false;
        // Fuera los de servidor web (p. ej. el de desarrollo de localhost), que también llevan clave.
        var eku = c.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (eku is not null && eku.EnhancedKeyUsages.Count > 0 &&
            eku.EnhancedKeyUsages.Cast<Oid>().All(o => o.Value == "1.3.6.1.5.5.7.3.1"))
            return false;
        return true;
    }
}
