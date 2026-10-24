// Pruebas de firma y verificación sin ventana: genera un formulario, lo firma dos veces con un
// certificado de prueba, comprueba las firmas como lo haría el visor y estropea una copia para
// ver que se detecta. Deja los PDF y el .pfx de prueba (clave «prueba») en pruebas/salida.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Signatures;
using PDFbyWilly;
using Rectangle = iText.Kernel.Geom.Rectangle;

// «dotnet run -- comprobar archivo.pdf»: firmas y anotaciones de un PDF cualquiera.
if (args is ["comprobar", var archivoAComprobar])
{
    var bytes = File.ReadAllBytes(archivoAComprobar);
    foreach (var (n, estado, json) in Verificar(bytes)) Console.WriteLine($"{n}: {estado}  {json}");
    using var d = new PdfDocument(new PdfReader(new MemoryStream(bytes)));
    var su2 = new SignatureUtil(d);
    foreach (var n in su2.GetSignatureNames())
        Console.WriteLine($"{n}: iText íntegra={su2.ReadSignatureData(n).VerifySignatureIntegrityAndAuthenticity()} cubre todo={su2.SignatureCoversWholeDocument(n)} revisión {su2.GetRevision(n)}/{su2.GetTotalRevisions()}");
    for (int p = 1; p <= d.GetNumberOfPages(); p++)
        Console.WriteLine($"página {p}: " + string.Join(", ", d.GetPage(p).GetAnnotations().Select(a => a.GetSubtype().GetValue())));
    var campos = PdfFormCreator.GetAcroForm(d, false)?.GetAllFormFields();
    if (campos is not null) foreach (var (k, v) in campos) Console.WriteLine($"campo {k} = {v.GetValueAsString()}");
    return 0;
}

string salida = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "salida"));
Directory.CreateDirectory(salida);
int fallos = 0;
void Comprobar(bool ok, string que) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + que); if (!ok) fallos++; }

// 1. Formulario de prueba
string formulario = Path.Combine(salida, "formulario.pdf");
using (var doc = new PdfDocument(new PdfWriter(formulario)))
{
    var pag = doc.AddNewPage(iText.Kernel.Geom.PageSize.A4);
    var fuente = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
    var lienzo = new PdfCanvas(pag);
    lienzo.BeginText().SetFontAndSize(fuente, 16).MoveText(60, 780).ShowText("SOLICITUD DE PRUEBA").EndText();
    lienzo.BeginText().SetFontAndSize(fuente, 11)
        .MoveText(60, 730).ShowText("Nombre y apellidos:")
        .MoveText(0, -40).ShowText("NIF:")
        .MoveText(0, -40).ShowText("Provincia:")
        .MoveText(0, -40).ShowText("Acepto las condiciones")
        .MoveText(0, -40).ShowText("Observaciones:")
        .EndText();

    var form = PdfFormCreator.GetAcroForm(doc, true);
    form.AddField(new TextFormFieldBuilder(doc, "nombre").SetWidgetRectangle(new Rectangle(190, 724, 330, 20)).CreateText());
    form.AddField(new TextFormFieldBuilder(doc, "nif").SetWidgetRectangle(new Rectangle(190, 684, 140, 20)).CreateText());
    form.AddField(new ChoiceFormFieldBuilder(doc, "provincia").SetWidgetRectangle(new Rectangle(190, 644, 200, 20))
        .SetOptions(["Illes Balears", "Barcelona", "Madrid", "Valencia"]).CreateComboBox());
    form.AddField(new CheckBoxFormFieldBuilder(doc, "acepto").SetWidgetRectangle(new Rectangle(240, 604, 16, 16)).CreateCheckBox());
    var obs = new TextFormFieldBuilder(doc, "observaciones").SetWidgetRectangle(new Rectangle(60, 470, 460, 90)).CreateMultilineText();
    form.AddField(obs);
}
Console.WriteLine("Formulario: " + formulario);

// 2. Certificado de prueba (autofirmado, no lo reconoce nadie: debe salir «no de confianza»)
using var rsa = RSA.Create(2048);
var req = new CertificateRequest("CN=PRUEBA PDFBYWILLY - 00000000T, O=Pruebas, C=ES", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
using var autofirmado = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(2));
string pfx = Path.Combine(salida, "prueba.pfx");
File.WriteAllBytes(pfx, autofirmado.Export(X509ContentType.Pfx, "prueba"));
var cert = X509CertificateLoader.LoadPkcs12FromFile(pfx, "prueba", X509KeyStorageFlags.EphemeralKeySet);

// 3. Primera firma, visible
var r1 = Firmador.Firmar(File.ReadAllBytes(formulario), cert, 1, [320, 60, 540, 120]);
File.WriteAllBytes(Path.Combine(salida, "firmado.pdf"), r1.Pdf);
Console.WriteLine("Primera firma" + (r1.Aviso is null ? " (con sello de tiempo)" : ": " + r1.Aviso));

// 4. Segunda firma encima: la primera debe seguir valiendo
var r2 = Firmador.Firmar(r1.Pdf, cert, 1, [60, 60, 280, 120]);
File.WriteAllBytes(Path.Combine(salida, "doble.pdf"), r2.Pdf);

Comprobar(r2.Pdf.AsSpan(0, r1.Pdf.Length).SequenceEqual(r1.Pdf), "la segunda firma se añade al final sin tocar lo anterior");

// 5. Verificación, como la pide el visor (ByteRange + Contents)
foreach (var (nombre, firma, json) in Verificar(r2.Pdf))
{
    Console.WriteLine($"  {nombre}: {json}");
    Comprobar(firma == "untrusted", $"{nombre} íntegra y con certificado no reconocido (autofirmado)");
}
using (var doc = new PdfDocument(new PdfReader(new MemoryStream(r2.Pdf))))
{
    var su = new SignatureUtil(doc);
    var nombres = su.GetSignatureNames();
    Comprobar(nombres.SequenceEqual(["Firma1", "Firma2"]), "campos Firma1 y Firma2: " + string.Join(", ", nombres));
    foreach (var n in nombres)
        Comprobar(su.ReadSignatureData(n).VerifySignatureIntegrityAndAuthenticity(), $"iText da {n} por íntegra");
    Comprobar(!su.SignatureCoversWholeDocument("Firma1") && su.SignatureCoversWholeDocument("Firma2"), "Firma2 cubre todo y Firma1 su revisión");
}

// 6. Documento manipulado: un byte cambiado dentro de lo firmado
var malo = (byte[])r1.Pdf.Clone();
int pos = Array.IndexOf(malo, (byte)'S', 400); // dentro del texto de la página
malo[pos] = (byte)'X';
foreach (var (nombre, firma, json) in Verificar(malo))
{
    Console.WriteLine($"  manipulado {nombre}: {json}");
    Comprobar(firma == "invalid", "el cambio se detecta: firma no válida");
}

Console.WriteLine(fallos == 0 ? "\nTodo bien." : $"\n{fallos} fallo(s).");
return fallos == 0 ? 0 : 1;

static IEnumerable<(string nombre, string estado, string json)> Verificar(byte[] pdf)
{
    using var doc = new PdfDocument(new PdfReader(new MemoryStream(pdf)));
    var su = new SignatureUtil(doc);
    foreach (var n in su.GetSignatureNames())
    {
        var dic = su.GetSignatureDictionary(n);
        var br = dic.GetAsArray(PdfName.ByteRange).ToLongArray();
        byte[] contenido = [.. pdf.AsSpan((int)br[0], (int)br[1]), .. pdf.AsSpan((int)br[2], (int)br[3])];
        byte[] p7 = dic.GetAsString(PdfName.Contents).GetValueBytes();
        string sub = dic.GetAsName(PdfName.SubFilter).GetValue();
        var res = Verificador.Verificar(contenido, p7, sub, dic.GetAsString(PdfName.M)?.ToUnicodeString());
        string json = JsonSerializer.Serialize(res);
        yield return (n, JsonDocument.Parse(json).RootElement.GetProperty("status").GetString()!, json.Length > 300 ? json[..300] + "…" : json);
    }
}
