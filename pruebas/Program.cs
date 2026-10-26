// Pruebas de firma y verificación sin ventana: genera un formulario, lo firma dos veces con un
// certificado de prueba, comprueba las firmas como lo haría el visor y estropea una copia para
// ver que se detecta. Deja los PDF y el .pfx de prueba (clave «prueba») en pruebas/salida.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Signatures;
using PDFSIMPLE;
using Rectangle = iText.Kernel.Geom.Rectangle;

// «dotnet run -- comprobar archivo.pdf»: firmas y anotaciones de un PDF cualquiera.
if (args.Length == 2 && args[0] == "comprobar")
{
    var archivoAComprobar = args[1];
    var bytes = File.ReadAllBytes(archivoAComprobar);
    foreach (var (n, estado, json) in Verificar(bytes)) Console.WriteLine($"{n}: {estado}  {json}");
    using var d = new PdfDocument(new PdfReader(new MemoryStream(bytes)));
    var su2 = new SignatureUtil(d);
    foreach (var n in su2.GetSignatureNames())
        Console.WriteLine($"{n}: iText íntegra={su2.ReadSignatureData(n).VerifySignatureIntegrityAndAuthenticity()} cubre todo={su2.SignatureCoversWholeDocument(n)} revisión {su2.GetRevision(n)}/{su2.GetTotalRevisions()}");
    for (int p = 1; p <= d.GetNumberOfPages(); p++)
        Console.WriteLine($"página {p}: " + string.Join(", ", d.GetPage(p).GetAnnotations().Select(a => a.GetSubtype().GetValue())));
    var campos = PdfFormCreator.GetAcroForm(d, false)?.GetAllFormFields();
    if (campos is not null) foreach (var kv in campos) Console.WriteLine($"campo {kv.Key} = {kv.Value.GetValueAsString()}");
    return 0;
}

// «dotnet run -- demo es|fr|en archivo.pdf»: formulario de ejemplo en ese idioma, relleno y firmado con un
// certificado de prueba de nombre inventado. Es lo que sale en las capturas de la Microsoft Store.
if (args.Length == 3 && args[0] == "demo")
{
    string lengua = args[1], archivoDemo = args[2];
    var x = lengua switch
    {
        "fr" => new { Titulo = "DEMANDE D'INSCRIPTION", Nombre = "Nom et prénom", NombreV = "CLAIRE DUBOIS", Id = "N° d'identité", IdV = "FR 12 345 678",
                      Dir = "Adresse", DirV = "12 RUE DE LA PAIX · PARIS", Zona = "Région", ZonaV = "Île-de-France", Zonas = new[] { "Île-de-France", "Bretagne", "Normandie" },
                      Acepto = "J'accepte les conditions", Obs = "Observations", ObsV = "Formulaire rempli et signé avec PDFSIMPLE.", Pie = "Signature du demandeur :" },
        "en" => new { Titulo = "APPLICATION FORM", Nombre = "Full name", NombreV = "JOHN SMITH", Id = "ID number", IdV = "AB 12 34 56 C",
                      Dir = "Address", DirV = "12 HIGH STREET · LONDON", Zona = "Region", ZonaV = "Greater London", Zonas = new[] { "Greater London", "Kent", "Surrey" },
                      Acepto = "I accept the terms", Obs = "Notes", ObsV = "Form filled in and signed with PDFSIMPLE.", Pie = "Applicant's signature:" },
        _ => new { Titulo = "SOLICITUD DE ALTA", Nombre = "Nombre y apellidos", NombreV = "LAURA MARTÍNEZ PONS", Id = "NIF", IdV = "12345678Z",
                   Dir = "Dirección", DirV = "C/ MAJOR, 12 · MAÓ", Zona = "Provincia", ZonaV = "Illes Balears", Zonas = new[] { "Illes Balears", "Barcelona", "Madrid" },
                   Acepto = "Acepto las condiciones", Obs = "Observaciones", ObsV = "Documento rellenado y firmado con PDFSIMPLE.", Pie = "Firma del solicitante:" },
    };
    var ms = new MemoryStream();
    using (var doc = new PdfDocument(new PdfWriter(ms)))
    {
        var pag = doc.AddNewPage(iText.Kernel.Geom.PageSize.A4);
        var normal = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
        var negrita = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
        var c = new PdfCanvas(pag);
        c.SetFillColor(new iText.Kernel.Colors.DeviceRgb(214, 32, 39)).Rectangle(0, 800, 595, 42).Fill();
        c.BeginText().SetFillColor(iText.Kernel.Colors.ColorConstants.WHITE).SetFontAndSize(negrita, 18).MoveText(60, 814).ShowText(x.Titulo).EndText();
        c.BeginText().SetFillColor(iText.Kernel.Colors.ColorConstants.BLACK).SetFontAndSize(normal, 11)
            .MoveText(60, 742).ShowText(x.Nombre + ":").MoveText(0, -40).ShowText(x.Id + ":").MoveText(0, -40).ShowText(x.Dir + ":")
            .MoveText(0, -40).ShowText(x.Zona + ":").MoveText(0, -40).ShowText(x.Acepto).MoveText(0, -40).ShowText(x.Obs + ":")
            .MoveText(0, -150).ShowText(x.Pie).EndText();
        var form = PdfFormCreator.GetAcroForm(doc, true);
        void Texto(string n, Rectangle r, string v) { var f = new TextFormFieldBuilder(doc, n).SetWidgetRectangle(r).CreateText(); f.SetValue(v); form.AddField(f); }
        Texto("nombre", new Rectangle(200, 736, 320, 20), x.NombreV);
        Texto("id", new Rectangle(200, 696, 160, 20), x.IdV);
        Texto("direccion", new Rectangle(200, 656, 320, 20), x.DirV);
        var zona = new ChoiceFormFieldBuilder(doc, "zona").SetWidgetRectangle(new Rectangle(200, 616, 200, 20)).SetOptions(x.Zonas).CreateComboBox();
        zona.SetValue(x.ZonaV); form.AddField(zona);
        var acepto = new CheckBoxFormFieldBuilder(doc, "acepto").SetWidgetRectangle(new Rectangle(260, 578, 16, 16)).CreateCheckBox();
        acepto.SetValue("Yes"); form.AddField(acepto);
        var obs = new TextFormFieldBuilder(doc, "observaciones").SetWidgetRectangle(new Rectangle(60, 450, 460, 80)).CreateMultilineText();
        obs.SetValue(x.ObsV); form.AddField(obs);
    }
    using var rsaDemo = RSA.Create(2048);
    var reqDemo = new CertificateRequest($"CN={x.NombreV}, C=ES", rsaDemo, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var autoDemo = reqDemo.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
    var certDemo = new X509Certificate2(autoDemo.Export(X509ContentType.Pfx, "demo"), "demo", X509KeyStorageFlags.EphemeralKeySet);
    Ajustes.Actual.Idioma = lengua; // el texto de la firma visible, en ese idioma (no se guarda)
    var firmado = Firmador.Firmar(ms.ToArray(), certDemo, 1, [300, 250, 520, 310]);
    File.WriteAllBytes(archivoDemo, firmado.Pdf);
    Console.WriteLine(archivoDemo + (firmado.Aviso is null ? "" : "  (" + firmado.Aviso + ")"));
    return 0;
}

// «dotnet run -- inspeccionar archivo.pdf»: estructura del formulario (tipos, banderas, scripts), sin enseñar valores.
if (args.Length == 2 && args[0] == "inspeccionar")
{
    using var d = new PdfDocument(new PdfReader(args[1]).SetUnethicalReading(true));
    var form = PdfFormCreator.GetAcroForm(d, false);
    Console.WriteLine($"páginas {d.GetNumberOfPages()}  campos {form?.GetAllFormFields().Count}");
    foreach (var kv in form?.GetAllFormFields() ?? new Dictionary<string, PdfFormField>())
    {
        var f = kv.Value; var w = f.GetWidgets().FirstOrDefault();
        var r = w?.GetRectangle()?.ToRectangle();
        int flags = w?.GetFlags() ?? 0;
        string da = (f.GetPdfObject().GetAsString(PdfName.DA) ?? w?.GetPdfObject().GetAsString(PdfName.DA))?.ToUnicodeString() ?? "";
        var aa = f.GetPdfObject().GetAsDictionary(PdfName.AA);
        var ap = w?.GetAppearanceDictionary();
        Console.WriteLine($"{kv.Key,-40} {f.GetFormType()?.GetValue(),-4} valor={f.GetValueAsString().Length,3}c  F={flags,-3} rect={(r is null ? "-" : $"{r.GetX():0},{r.GetY():0},{r.GetWidth():0}x{r.GetHeight():0}")}  DA='{da}'  AA={(aa is null ? "-" : string.Join(",", aa.KeySet().Select(k => k.GetValue())))}  AP={(ap is null ? "no" : "sí")}");
    }
    var nombresJs = d.GetCatalog().GetPdfObject().GetAsDictionary(PdfName.Names)?.GetAsDictionary(PdfName.JavaScript)?.GetAsArray(PdfName.Names);
    for (int i = 0; nombresJs != null && i < nombresJs.Size(); i += 2) Console.WriteLine("  script de documento: " + nombresJs.GetAsString(i));
    var alImprimir = d.GetCatalog().GetPdfObject().GetAsDictionary(PdfName.AA)?.GetAsDictionary(PdfName.WP);
    if (alImprimir != null) Console.WriteLine("  script al imprimir (WP)");
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
var req = new CertificateRequest("CN=PRUEBA PDFSIMPLE - 00000000T, O=Pruebas, C=ES", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
using var autofirmado = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(2));
string pfx = Path.Combine(salida, "prueba.pfx");
File.WriteAllBytes(pfx, autofirmado.Export(X509ContentType.Pfx, "prueba"));
var cert = new X509Certificate2(pfx, "prueba", X509KeyStorageFlags.EphemeralKeySet);

// 3. Primera firma, visible
var r1 = Firmador.Firmar(File.ReadAllBytes(formulario), cert, 1, [320, 60, 540, 120]);
File.WriteAllBytes(Path.Combine(salida, "firmado.pdf"), r1.Pdf);
Console.WriteLine("Primera firma" + (r1.Aviso is null ? " (con sello de tiempo)" : ": " + r1.Aviso));

// 4. Segunda firma encima: la primera debe seguir valiendo
var r2 = Firmador.Firmar(r1.Pdf, cert, 1, [60, 60, 280, 120]);
File.WriteAllBytes(Path.Combine(salida, "doble.pdf"), r2.Pdf);

if (r1.Aviso is null)
    using (var docSello = new PdfDocument(new PdfReader(new MemoryStream(r1.Pdf))))
    {
        var dic = new SignatureUtil(docSello).GetSignatureDictionary("Firma1");
        var br = dic.GetAsArray(PdfName.ByteRange).ToLongArray();
        byte[] firmado = r1.Pdf.Trozo((int)br[0], (int)br[1]).Concat(r1.Pdf.Trozo((int)br[2], (int)br[3])).ToArray();
        var hora = Verificador.FechaDelSello(Verificador.RecortarDer(dic.GetAsString(PdfName.Contents).GetValueBytes()), firmado);
        Comprobar(hora is not null && Math.Abs((hora.Value - DateTime.Now).TotalMinutes) < 10, "lee y valida el sello de tiempo de la firma: " + hora);
    }
Comprobar(Compat.Iguales(r2.Pdf.Trozo(0, r1.Pdf.Length), r1.Pdf), "la segunda firma se añade al final sin tocar lo anterior");

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
        byte[] contenido = pdf.Trozo((int)br[0], (int)br[1]).Concat(pdf.Trozo((int)br[2], (int)br[3])).ToArray();
        byte[] p7 = dic.GetAsString(PdfName.Contents).GetValueBytes();
        string sub = dic.GetAsName(PdfName.SubFilter).GetValue();
        var res = Verificador.Verificar(contenido, p7, sub, dic.GetAsString(PdfName.M)?.ToUnicodeString());
        string json = Json.Texto(res);
        yield return (n, Json.Objeto(json).Cadena("status")!, json.Length > 300 ? json.Substring(0, 300) + "…" : json);
    }
}
