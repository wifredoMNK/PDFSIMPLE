using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Forms.Fields;
using iText.Forms.Form.Element;
using iText.Kernel.Crypto;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.X509;
using Rectangle = iText.Kernel.Geom.Rectangle;

namespace PDFSIMPLE;

/// <summary>
/// Firma PAdES (CAdES desprendida, SHA-256) añadida al final del PDF, sin reescribirlo: las firmas
/// que ya tuviera siguen valiendo. Con sello de tiempo si el servidor de sellado contesta.
/// </summary>
static class Firmador
{
    const string ServidorSellado = "http://timestamp.digicert.com";

    public sealed record Resultado(byte[] Pdf, string? Aviso);

    public static Resultado Firmar(byte[] pdf, X509Certificate2 cert, int pagina, float[]? rect)
    {
        var cadena = Cadena(cert);
        var firma = new FirmaWindows(cert);
        string campo = NombreLibre(pdf);
        try
        {
            return new(Una(pdf, firma, cadena, campo, pagina, rect, new TSAClientBouncyCastle(ServidorSellado)), null);
        }
        catch (Exception ex) when (EsDelSellado(ex))
        {
            return new(Una(pdf, firma, cadena, campo, pagina, rect, null),
                Idioma.T("sinSello"));
        }
    }

    static byte[] Una(byte[] pdf, FirmaWindows firma, IX509Certificate[] cadena, string campo, int pagina, float[]? rect, ITSAClient? sellado)
    {
        var lector = new PdfReader(new MemoryStream(pdf)).SetUnethicalReading(true);
        var salida = new MemoryStream();
        var firmante = new PdfSigner(lector, salida, new StampingProperties().UseAppendMode());

        string nombre = NombreComun(firma.Certificado);
        var props = new SignerProperties()
            .SetFieldName(campo)
            .SetSignatureCreator(Marca.Nombre)
            .SetClaimedSignDate(DateTime.Now);

        if (rect is { Length: 4 })
        {
            float x1 = rect[0], y1 = rect[1], x2 = rect[2], y2 = rect[3];
            var apariencia = new SignatureFieldAppearance(SignerProperties.IGNORED_ID)
                .SetContent($"{Idioma.T("firmadoPor")}\n{nombre}\n{Idioma.T("fecha")}: {DateTime.Now.ToString(Idioma.T("formatoFecha"))}");
            props.SetPageNumber(pagina)
                 .SetPageRect(new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)))
                 .SetSignatureAppearance(apariencia);
        }
        else
        {
            props.SetPageNumber(1).SetPageRect(new Rectangle(0, 0, 0, 0)); // firma invisible
        }

        firmante.SetSignerProperties(props);
        firmante.SignDetached(new BouncyCastleDigest(), firma, cadena, null, null, sellado, 0, PdfSigner.CryptoStandard.CADES);
        return salida.ToArray();
    }

    static bool EsDelSellado(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is System.Net.WebException or IOException or TimeoutException ||
                e.GetType().Name == "HttpRequestException" ||
                e.GetType().Name.Contiene("Tsa") ||
                e.Message.Contiene("timestamp") ||
                e.Message.Contains("TSA", StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Firma1, Firma2… el primero que no esté ya en el formulario.</summary>
    static string NombreLibre(byte[] pdf)
    {
        using var doc = new PdfDocument(new PdfReader(new MemoryStream(pdf)).SetUnethicalReading(true));
        var form = PdfFormCreator.GetAcroForm(doc, false);
        var usados = form?.GetAllFormFields().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        for (int i = 1; ; i++)
            if (!usados.Contains("Firma" + i)) return "Firma" + i;
    }

    /// <summary>El certificado y los de su emisor que se encuentren, para meterlos en la firma.</summary>
    static IX509Certificate[] Cadena(X509Certificate2 cert)
    {
        using var ch = new X509Chain();
        ch.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        ch.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
        ch.Build(cert);
        var lista = ch.ChainElements.Cast<X509ChainElement>().Select(e => e.Certificate).ToList();
        if (lista.Count == 0 || lista[0].Thumbprint != cert.Thumbprint) lista.Insert(0, cert);
        var parser = new X509CertificateParser();
        return lista.Select(c => (IX509Certificate)new X509CertificateBC(parser.ReadCertificate(c.RawData))).ToArray();
    }

    public static string NombreComun(X509Certificate2 c) => c.GetNameInfo(X509NameType.SimpleName, false);

    /// <summary>Firma con la clave del certificado tal como la tenga Windows (también tarjetas: pide el PIN).</summary>
    sealed class FirmaWindows(X509Certificate2 cert) : IExternalSignature
    {
        public X509Certificate2 Certificado => cert;
        readonly RSA? rsa = cert.GetRSAPrivateKey();
        readonly ECDsa? ec = cert.GetECDsaPrivateKey();

        public string GetDigestAlgorithmName() => DigestAlgorithms.SHA256;
        public string GetSignatureAlgorithmName() => rsa is not null ? "RSA" : "ECDSA";
        public ISignatureMechanismParams? GetSignatureMechanismParameters() => null;

        public byte[] Sign(byte[] mensaje)
        {
            if (rsa is not null) return rsa.SignData(mensaje, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (ec is not null) return DerDeEcdsa(ec.SignData(mensaje, HashAlgorithmName.SHA256));
            throw new CryptographicException(Idioma.T("claveNoValida"));
        }

        /// <summary>Windows da la firma ECDSA como r‖s; la firma del PDF la quiere en DER (SEQUENCE de dos INTEGER).</summary>
        static byte[] DerDeEcdsa(byte[] rs)
        {
            int mitad = rs.Length / 2;
            byte[] Entero(int desde)
            {
                int i = desde, fin = desde + mitad;
                while (i < fin - 1 && rs[i] == 0) i++;
                var v = rs.Skip(i).Take(fin - i).ToList();
                if ((v[0] & 0x80) != 0) v.Insert(0, 0);
                return new byte[] { 0x02 }.Concat(Largo(v.Count)).Concat(v).ToArray();
            }
            var cuerpo = Entero(0).Concat(Entero(mitad)).ToArray();
            return new byte[] { 0x30 }.Concat(Largo(cuerpo.Length)).Concat(cuerpo).ToArray();
        }

        static byte[] Largo(int n) => n < 0x80 ? new[] { (byte)n } : n < 0x100 ? new byte[] { 0x81, (byte)n } : new byte[] { 0x82, (byte)(n >> 8), (byte)n };
    }
}
