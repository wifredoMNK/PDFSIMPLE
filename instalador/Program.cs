using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PDFSIMPLESetup
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
                MessageBox.Show(Textos.T("necesita64"), Textos.T("instalar") + " PDFSIMPLE", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    /// El instalador habla el idioma de Windows: español, francés o inglés. El programa hace lo mismo
    /// mientras el usuario no elija otro con su icono de idiomas.
    /// </summary>
    static class Textos
    {
        static readonly int I = Indice();

        static int Indice()
        {
            switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
            {
                case "es": case "ca": case "gl": case "eu": return 0;
                case "fr": return 1;
                default: return 2;
            }
        }

        public static string T(string clave, params object[] args)
        {
            string s = Tabla.TryGetValue(clave, out var t) ? t[I] : clave;
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        static readonly Dictionary<string, string[]> Tabla = new Dictionary<string, string[]>
        {
            ["instalar"] = new[] { "Instalar", "Installer", "Install" },
            ["actualizar"] = new[] { "Actualizar", "Mettre à jour", "Update" },
            ["cancelar"] = new[] { "Cancelar", "Annuler", "Cancel" },
            ["terminar"] = new[] { "Terminar", "Terminer", "Finish" },
            ["necesita64"] = new[] { "PDFSIMPLE necesita Windows de 64 bits.", "PDFSIMPLE nécessite Windows 64 bits.", "PDFSIMPLE needs 64-bit Windows." },
            ["lema"] = new[] {
                "Lector de PDF sin publicidad: formularios, escribir a máquina y firma digital.",
                "Lecteur PDF sans publicité : formulaires, saisie de texte et signature numérique.",
                "Ad-free PDF reader: forms, typing on the page and digital signatures." },
            ["arm"] = new[] {
                "Este equipo es ARM (Snapdragon): se instala la versión para ARM.",
                "Ce PC est ARM (Snapdragon) : la version ARM sera installée.",
                "This PC is ARM (Snapdragon): the ARM version will be installed." },
            ["intel"] = new[] {
                "Este equipo es Intel / AMD de 64 bits: se instala esa versión.",
                "Ce PC est Intel / AMD 64 bits : cette version sera installée.",
                "This PC is 64-bit Intel / AMD: that version will be installed." },
            ["donde"] = new[] {
                "Se instala en {0}, para todos los usuarios del equipo.",
                "Installation dans {0}, pour tous les utilisateurs du PC.",
                "It is installed in {0}, for all users of this PC." },
            ["escritorio"] = new[] { "Acceso directo en el escritorio", "Raccourci sur le bureau", "Desktop shortcut" },
            ["anclar"] = new[] { "Anclar a la barra de tareas", "Épingler à la barre des tâches", "Pin to the taskbar" },
            ["notaAnclar"] = new[] {
                "Windows no deja que un programa se ancle solo: al terminar te digo dónde hacer clic.",
                "Windows ne permet pas à un programme de s'épingler lui-même : à la fin, je vous indique où cliquer.",
                "Windows does not let a program pin itself: when it finishes, I will tell you where to click." },
            ["abiertas"] = new[] {
                "Hay ventanas de PDFSIMPLE abiertas: se van a cerrar y se perderá lo que no esté guardado.",
                "Des fenêtres de PDFSIMPLE sont ouvertes : elles vont être fermées et ce qui n'est pas enregistré sera perdu.",
                "PDFSIMPLE windows are open: they will be closed and anything unsaved will be lost." },
            ["instalando"] = new[] { "Instalando…", "Installation…", "Installing…" },
            ["registroFallo"] = new[] {
                "El registro en Windows ha fallado (código {0}).",
                "L'enregistrement dans Windows a échoué (code {0}).",
                "Registering with Windows failed (code {0})." },
            ["noInstalado"] = new[] { "No se ha podido instalar: {0}", "Installation impossible : {0}", "Could not install: {0}" },
            ["instalado"] = new[] { "PDFSIMPLE está instalado", "PDFSIMPLE est installé", "PDFSIMPLE is installed" },
            ["tituloInstalado"] = new[] { "PDFSIMPLE instalado", "PDFSIMPLE installé", "PDFSIMPLE installed" },
            ["pregunta"] = new[] {
                "¿Quieres que los PDF se abran siempre con PDFSIMPLE?",
                "Voulez-vous toujours ouvrir les PDF avec PDFSIMPLE ?",
                "Do you want PDFs to always open with PDFSIMPLE?" },
            ["si"] = new[] { "Sí, elegirlo ahora", "Oui, le choisir maintenant", "Yes, choose it now" },
            ["no"] = new[] { "No", "Non", "No" },
            ["comoPredeterminado"] = new[] {
                "En la ventana de Configuración que se ha abierto: escribe .pdf en el buscador de arriba, pulsa la casilla que sale debajo, elige PDFSIMPLE y pulsa «Establecer predeterminado».",
                "Dans la fenêtre Paramètres qui vient de s'ouvrir : tapez .pdf dans la recherche en haut, cliquez sur la case qui apparaît dessous, choisissez PDFSIMPLE puis « Définir par défaut ».",
                "In the Settings window that has just opened: type .pdf in the search box at the top, click the box that appears below, choose PDFSIMPLE and click “Set default”." },
            ["cuandoQuieras"] = new[] {
                "Cuando quieras: en PDFSIMPLE, el botón «Abrir siempre los PDF con PDFSIMPLE…» (al final de la barra o en el menú »).",
                "Quand vous voulez : dans PDFSIMPLE, le bouton « Toujours ouvrir les PDF avec PDFSIMPLE… » (au bout de la barre ou dans le menu »).",
                "Whenever you like: in PDFSIMPLE, the “Always open PDFs with PDFSIMPLE…” button (at the end of the toolbar or in the » menu)." },
            ["comoAnclar"] = new[] {
                "Para anclarlo a la barra de tareas: clic derecho en el icono de PDFSIMPLE de la barra de abajo (se acaba de abrir) → «Anclar a la barra de tareas».",
                "Pour l'épingler : clic droit sur l'icône de PDFSIMPLE dans la barre des tâches (il vient de s'ouvrir) → « Épingler à la barre des tâches ».",
                "To pin it: right-click the PDFSIMPLE icon on the taskbar (it has just opened) → “Pin to taskbar”." },
        };
    }

    /// <summary>
    /// Una ventana con dos pasos: qué se instala (y opciones) y, al acabar, la propuesta de dejarlo
    /// para los PDF. El registro en Windows lo hace el propio PDFSIMPLE.exe con «--registrar».
    /// </summary>
    sealed class VentanaSetup : Form
    {
        const string Nombre = "PDFSIMPLE";

        readonly Arq arq;
        readonly string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Nombre);
        string Exe => Path.Combine(carpeta, Nombre + ".exe");

        readonly FlowLayoutPanel paso1 = Columna();
        readonly FlowLayoutPanel paso2 = Columna();
        readonly CheckBox escritorio = new CheckBox { Text = Textos.T("escritorio"), Checked = true, AutoSize = true };
        readonly CheckBox anclar = new CheckBox { Text = Textos.T("anclar"), Checked = true, AutoSize = true };
        readonly Button instalar = new Button { AutoSize = true };
        readonly Label estado = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };

        public VentanaSetup(Arq arq)
        {
            this.arq = arq;
            bool actualizar = File.Exists(Exe);

            Text = (actualizar ? Textos.T("actualizar") : Textos.T("instalar")) + " PDFSIMPLE";
            Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("icono.ico"));
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = SystemFonts.MessageBoxFont;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // ---- paso 1
            var titulo = new Label { Text = Nombre, AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.8f, FontStyle.Bold) };
            var lema = Texto(Textos.T("lema"));
            var equipo = Texto(Textos.T(arq == Arq.Arm64 ? "arm" : "intel"));
            var donde = Texto(Textos.T("donde", carpeta));
            donde.ForeColor = SystemColors.GrayText;
            var notaAnclar = Texto(Textos.T("notaAnclar"));
            notaAnclar.ForeColor = SystemColors.GrayText;
            notaAnclar.Margin = new Padding(E(20), 0, 0, E(8));

            instalar.Text = actualizar ? Textos.T("actualizar") : Textos.T("instalar");
            instalar.Click += (s, e) => Instalar();
            // La ventana no es un diálogo (va con Application.Run): los botones la cierran ellos.
            var cancelar = new Button { Text = Textos.T("cancelar"), AutoSize = true };
            cancelar.Click += (s, e) => Close();
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
                if (MessageBox.Show(this, Textos.T("abiertas"), Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
                foreach (var p in abiertos) try { p.Kill(); p.WaitForExit(5000); } catch { }
            }

            instalar.Enabled = false;
            UseWaitCursor = true;
            estado.Text = Textos.T("instalando");
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
                if (reg.ExitCode != 0) throw new Exception(Textos.T("registroFallo", reg.ExitCode));
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                instalar.Enabled = true;
                estado.Text = "";
                MessageBox.Show(this, Textos.T("noInstalado", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            UseWaitCursor = false;

            // Se abre ya, sin permisos de administrador (a través del Explorador), para que salga su icono abajo.
            try { Process.Start("explorer.exe", "\"" + Exe + "\""); } catch { }
            MostrarFinal();
        }

        void MostrarFinal()
        {
            var hecho = new Label { Text = "✔  " + Textos.T("instalado"), AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold), ForeColor = Color.FromArgb(31, 111, 63) };
            var pregunta = Texto(Textos.T("pregunta"));
            pregunta.Font = new Font(Font, FontStyle.Bold);
            pregunta.Margin = new Padding(0, E(14), 0, E(4));
            var instrucciones = Texto("");
            instrucciones.Visible = false;

            var si = new Button { Text = Textos.T("si"), AutoSize = true };
            var no = new Button { Text = Textos.T("no"), AutoSize = true };
            var filaPregunta = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, E(2), 0, E(6)) };
            filaPregunta.Controls.AddRange(new Control[] { si, no });
            si.Click += (s, e) =>
            {
                AbrirPredeterminadas();
                instrucciones.Text = Textos.T("comoPredeterminado");
                instrucciones.Visible = true;
                filaPregunta.Visible = false;
            };
            no.Click += (s, e) =>
            {
                instrucciones.Text = Textos.T("cuandoQuieras");
                instrucciones.Visible = true;
                filaPregunta.Visible = false;
            };

            var terminar = new Button { Text = Textos.T("terminar"), AutoSize = true };
            terminar.Click += (s, e) => Close();
            paso2.Controls.AddRange(new Control[] { hecho, pregunta, filaPregunta, instrucciones });
            if (anclar.Checked)
            {
                var a = Texto(Textos.T("comoAnclar"));
                a.Margin = new Padding(0, E(12), 0, E(2));
                paso2.Controls.Add(a);
            }
            paso2.Controls.Add(Botones(terminar));
            AcceptButton = terminar;
            CancelButton = terminar;

            paso1.Visible = false;
            paso2.Visible = true;
            Text = Textos.T("tituloInstalado");
            Activate();
        }

        static void AbrirPredeterminadas()
        {
            try { Process.Start("ms-settings:defaultapps?registeredAppMachine=" + Nombre); }
            catch { try { Process.Start("ms-settings:defaultapps"); } catch { } }
        }
    }
}
