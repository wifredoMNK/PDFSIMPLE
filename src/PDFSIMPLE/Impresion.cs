using System.ComponentModel;
using System.Drawing.Printing;

namespace PDFSIMPLE;

/// <summary>
/// Imprimir lo escaneado, para fotocopiar: el diálogo de imprimir de Windows pregunta la impresora y cuántas
/// copias, y cada página sale a su tamaño real, centrada en la hoja (si no cabe en lo que la impresora llega a
/// pintar, se reduce lo justo). Las copias van ordenadas (1-2-3, 1-2-3) y las pone el programa: hay
/// impresoras que no hacen caso del número de copias que se les pide.
/// </summary>
static class Impresion
{
    /// <summary>True si se ha mandado a la impresora; false si se cancela o falla.</summary>
    public static bool Imprimir(IWin32Window dueño, List<Escaner.Pagina> paginas)
    {
        using var doc = new PrintDocument { DocumentName = Idioma.T("fotocopia") };
        using var dlg = new PrintDialog
        {
            Document = doc,
            UseEXDialog = true,
            AllowSomePages = false,
            AllowSelection = false,
            AllowCurrentPage = false,
        };
        doc.PrinterSettings.Copies = 1;
        if (dlg.ShowDialog(dueño) != DialogResult.OK) return false;

        Preparar(doc, paginas, Math.Max(1, (int)doc.PrinterSettings.Copies));
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            doc.Print();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidPrinterException)
        {
            MessageBox.Show(dueño, Idioma.T("noImprime", ex.Message), Idioma.T("fotocopiar"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        finally { Cursor.Current = Cursors.Default; }
    }

    /// <summary>Las páginas, tantas veces como copias, listas para <c>doc.Print()</c>.</summary>
    internal static void Preparar(PrintDocument doc, List<Escaner.Pagina> paginas, int copias)
    {
        doc.PrinterSettings.Copies = 1;
        doc.PrinterSettings.Collate = false;
        var orden = Enumerable.Range(0, copias).SelectMany(_ => paginas).ToList();
        int n = 0;

        doc.BeginPrint += (_, _) => n = 0;
        doc.PrintPage += (_, e) =>
        {
            var p = orden[n++];
            using var img = Image.FromStream(new MemoryStream(p.Jpeg));
            var g = e.Graphics!;
            var hoja = e.PageSettings;
            RectangleF area = hoja.PrintableArea; // centésimas de pulgada, sobre la hoja en vertical
            if (hoja.Landscape) area = new RectangleF(area.Y, area.X, area.Height, area.Width);

            // A tamaño real (los puntos son 1/72 de pulgada) y, si no cabe, reducida a lo que se puede pintar.
            float ancho = p.Ancho * 100f / 72f, alto = p.Alto * 100f / 72f;
            float escala = Math.Min(1f, Math.Min(area.Width / ancho, area.Height / alto));
            ancho *= escala; alto *= escala;

            // El origen de Graphics está en el margen físico de la impresora, no en la esquina de la hoja.
            float x = (e.PageBounds.Width - ancho) / 2f - hoja.HardMarginX;
            float y = (e.PageBounds.Height - alto) / 2f - hoja.HardMarginY;
            x = Math.Max(0, Math.Min(x, area.Width - ancho));
            y = Math.Max(0, Math.Min(y, area.Height - alto));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, x, y, ancho, alto);
            e.HasMorePages = n < orden.Count;
        };
    }
}
