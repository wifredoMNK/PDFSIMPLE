using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PDFbyWillySetup
{
    static class Program
    {
        [STAThread]
        static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var arq = Arquitectura.DeEsteEquipo();
            if (arq == null)
            {
                MessageBox.Show("PDFbyWilly necesita Windows de 64 bits.", "Instalar PDFbyWilly", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            Application.Run(new VentanaSetup(arq.Value));
            return 0;
        }
    }

    enum Arq { X64, Arm64 }

    static class Arquitectura
    {
        /// <summary>El procesador de verdad, aunque este instalador corra emulado.</summary>
        public static Arq? DeEsteEquipo()
        {
            try
            {
                if (IsWow64Process2(Process.GetCurrentProcess().Handle, out _, out ushort nativo))
                {
                    if (nativo == 0xAA64) return Arq.Arm64;
                    if (nativo == 0x8664) return Arq.X64;
                    return null; // 32 bits
                }
            }
            catch (EntryPointNotFoundException) { } // Windows 10 antiguo: no hay ARM64 que distinguir
            return Environment.Is64BitOperatingSystem ? Arq.X64 : (Arq?)null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool IsWow64Process2(IntPtr proceso, out ushort maquinaProceso, out ushort maquinaNativa);
    }

    /// <summary>
    /// Una ventana con dos pasos: qué se instala (y opciones) y, al acabar, la propuesta de dejarlo
    /// para los PDF. El registro en Windows lo hace el propio PDFbyWilly.exe con «--registrar».
    /// </summary>
    sealed class VentanaSetup : Form
    {
        const string Nombre = "PDFbyWilly";
        const string ComoPredeterminado =
            "En la ventana de Configuración que se ha abierto: escribe .pdf en el buscador de arriba, pulsa la casilla " +
            "que sale debajo, elige PDFbyWilly y pulsa «Establecer predeterminado».";

        readonly Arq arq;
        readonly string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Nombre);
        string Exe => Path.Combine(carpeta, Nombre + ".exe");

        readonly FlowLayoutPanel paso1 = Columna();
        readonly FlowLayoutPanel paso2 = Columna();
        readonly CheckBox escritorio = new CheckBox { Text = "Acceso directo en el escritorio", Checked = true, AutoSize = true };
        readonly CheckBox anclar = new CheckBox { Text = "Anclar a la barra de tareas", Checked = true, AutoSize = true };
        readonly Button instalar = new Button { AutoSize = true };
        readonly Label estado = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };

        public VentanaSetup(Arq arq)
        {
            this.arq = arq;
            bool actualizar = File.Exists(Exe);

            Text = actualizar ? "Actualizar PDFbyWilly" : "Instalar PDFbyWilly";
            Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("icono.ico"));
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = SystemFonts.MessageBoxFont;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // ---- paso 1
            var titulo = new Label { Text = Nombre, AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.8f, FontStyle.Bold) };
            var lema = Texto("Lector de PDF sin publicidad: formularios, escribir a máquina y firma digital.");
            var equipo = Texto(arq == Arq.Arm64
                ? "Este equipo es ARM (Snapdragon): se instala la versión para ARM."
                : "Este equipo es Intel / AMD de 64 bits: se instala esa versión.");
            var donde = Texto("Se instala en " + carpeta + ", para todos los usuarios del equipo.");
            donde.ForeColor = SystemColors.GrayText;
            var notaAnclar = Texto("Windows no deja que un programa se ancle solo: al terminar te digo dónde hacer clic.");
            notaAnclar.ForeColor = SystemColors.GrayText;
            notaAnclar.Margin = new Padding(E(20), 0, 0, E(8));

            instalar.Text = actualizar ? "Actualizar" : "Instalar";
            instalar.Click += (s, e) => Instalar();
            var cancelar = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
            paso1.Controls.AddRange(new Control[] { titulo, lema, equipo, donde, Hueco(), escritorio, anclar, notaAnclar, estado, Botones(cancelar, instalar) });
            AcceptButton = instalar;
            CancelButton = cancelar;

            Controls.Add(paso1);
            paso2.Visible = false;
            Controls.Add(paso2);
        }

        int E(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

        static FlowLayoutPanel Columna() => new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        Label Texto(string t) => new Label { Text = t, AutoSize = true, MaximumSize = new Size(E(460), 0), Margin = new Padding(0, E(4), 0, E(2)) };
        Control Hueco() => new Panel { Height = E(8), Width = 1 };

        Control Botones(params Button[] b)
        {
            var f = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Width = E(460), Margin = new Padding(0, E(10), 0, 0) };
            f.Controls.AddRange(b);
            return f;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            paso1.Padding = paso2.Padding = new Padding(E(20), E(16), E(20), E(14));
        }

        void Instalar()
        {
            var abiertos = Process.GetProcessesByName(Nombre);
            if (abiertos.Length > 0)
            {
                if (MessageBox.Show(this, "Hay ventanas de PDFbyWilly abiertas: se van a cerrar y se perderá lo que no esté guardado.",
                        Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
                foreach (var p in abiertos) try { p.Kill(); p.WaitForExit(5000); } catch { }
            }

            instalar.Enabled = false;
            UseWaitCursor = true;
            estado.Text = "Instalando…";
            Refresh();
            try
            {
                Directory.CreateDirectory(carpeta);
                string tmp = Exe + ".nuevo";
                using (var origen = Assembly.GetExecutingAssembly().GetManifestResourceStream(arq == Arq.Arm64 ? "arm64.exe" : "x64.exe"))
                using (var destino = File.Create(tmp))
                    origen.CopyTo(destino);
                if (File.Exists(Exe)) File.Delete(Exe);
                File.Move(tmp, Exe);

                var reg = Process.Start(new ProcessStartInfo(Exe, "--registrar" + (escritorio.Checked ? " --escritorio" : "")) { UseShellExecute = false });
                reg.WaitForExit();
                if (reg.ExitCode != 0) throw new Exception("El registro en Windows ha fallado (código " + reg.ExitCode + ").");
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                instalar.Enabled = true;
                estado.Text = "";
                MessageBox.Show(this, "No se ha podido instalar: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            UseWaitCursor = false;

            // Se abre ya, sin permisos de administrador (a través del Explorador), para que salga su icono abajo.
            try { Process.Start("explorer.exe", "\"" + Exe + "\""); } catch { }
            MostrarFinal();
        }

        void MostrarFinal()
        {
            var hecho = new Label { Text = "✔  PDFbyWilly está instalado", AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold), ForeColor = Color.FromArgb(31, 111, 63) };
            var pregunta = Texto("¿Quieres que los PDF se abran siempre con PDFbyWilly?");
            pregunta.Font = new Font(Font, FontStyle.Bold);
            pregunta.Margin = new Padding(0, E(14), 0, E(4));
            var instrucciones = Texto("");
            instrucciones.Visible = false;

            var si = new Button { Text = "Sí, elegirlo ahora", AutoSize = true };
            var no = new Button { Text = "No", AutoSize = true };
            var filaPregunta = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, E(2), 0, E(6)) };
            filaPregunta.Controls.AddRange(new Control[] { si, no });
            si.Click += (s, e) =>
            {
                AbrirPredeterminadas();
                instrucciones.Text = ComoPredeterminado;
                instrucciones.Visible = true;
                filaPregunta.Visible = false;
            };
            no.Click += (s, e) =>
            {
                instrucciones.Text = "Cuando quieras: en PDFbyWilly, menú » → «Abrir siempre los PDF con PDFbyWilly…».";
                instrucciones.Visible = true;
                filaPregunta.Visible = false;
            };

            var terminar = new Button { Text = "Terminar", AutoSize = true, DialogResult = DialogResult.OK };
            paso2.Controls.AddRange(new Control[] { hecho, pregunta, filaPregunta, instrucciones });
            if (anclar.Checked)
            {
                var a = Texto("Para anclarlo a la barra de tareas: clic derecho en el icono de PDFbyWilly de la barra de abajo " +
                              "(se acaba de abrir) → «Anclar a la barra de tareas».");
                a.Margin = new Padding(0, E(12), 0, E(2));
                paso2.Controls.Add(a);
            }
            paso2.Controls.Add(Botones(terminar));
            AcceptButton = terminar;
            CancelButton = terminar;

            paso1.Visible = false;
            paso2.Visible = true;
            Text = "PDFbyWilly instalado";
            Activate();
        }

        static void AbrirPredeterminadas()
        {
            try { Process.Start("ms-settings:defaultapps?registeredAppMachine=" + Nombre); }
            catch { try { Process.Start("ms-settings:defaultapps"); } catch { } }
        }
    }
}
