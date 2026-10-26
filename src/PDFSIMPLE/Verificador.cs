using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tsp;

namespace PDFSIMPLE;

/// <summary>
/// Comprueba una firma de un PDF para el panel de firmas de pdf.js: que el contenido firmado no ha
/// cambiado y que el certificado viene de una autoridad en la que confía Windows (FNMT, DNIe…).
/// Devuelve lo que espera pdf.js: status = verified | untrusted | expired | revoked | invalid | unknown.
/// </summary>
static class Verificador
{
    const string OidSelloEnFirma = "1.2.840.113549.1.9.16.2.14";

    public static object Verificar(byte[] contenido, byte[] pkcs7, string? subFilter, string? fechaPdf)
    {
        byte[] der = RecortarDer(pkcs7);
        X509Certificate2 cert;
        DateTime? fecha = null;
        bool sellada = false;

        try
        {
            if (subFilter == "ETSI.RFC3161")
            {
                // Sello de tiempo del documento entero (sin firmante persona).
                TimeStampToken tok;
                try { tok = new TimeStampToken(new CmsSignedData(der)); }
                catch (Exception) { return Invalida(Idioma.T("selloIlegible"), null); }
                var tsa = CertificadoTsa(tok);
                if (tsa is null || !HuellaCoincide(tok, contenido))
                    return Invalida(Idioma.T("cambiadoTrasSello"), tsa);
                cert = tsa;
                fecha = tok.TimeStampInfo.GenTime.ToLocalTime();
                sellada = true;
            }
            else
            {
                SignedCms cms;
                if (subFilter == "adbe.pkcs7.sha1")
                {
                    cms = new SignedCms();
                    cms.Decode(der);
                }
                else
                {
                    cms = new SignedCms(new ContentInfo(contenido), detached: true);
                    cms.Decode(der);
                }
                var si = cms.SignerInfos[0];
                cert = si.Certificate ?? throw new CryptographicException(Idioma.T("sinCertificadoFirmante"));
                try { si.CheckSignature(verifySignatureOnly: true); }
                catch (CryptographicException) { return Invalida(Idioma.T("cambiadoTrasFirma"), cert); }

                if (subFilter == "adbe.pkcs7.sha1")
                    using (var sha1 = SHA1.Create())
                        if (!Compat.Iguales(sha1.ComputeHash(contenido), cms.ContentInfo.Content))
                            return Invalida(Idioma.T("cambiadoTrasFirma"), cert);

                fecha = FechaDelSello(der, subFilter == "adbe.pkcs7.sha1" ? null : contenido);
                sellada = fecha is not null;
                if (fecha is null)
                    foreach (var attr in si.SignedAttributes)
                        if (attr.Values.Count > 0 && attr.Values[0] is Pkcs9SigningTime st) fecha = st.SigningTime.ToLocalTime();
                fecha ??= FechaPdf(fechaPdf);

                return Cadena(cert, cms.Certificates, fecha, sellada);
            }
        }
        catch (Exception ex)
        {
            return new { status = "unknown", errorCode = "PARSE_ERROR", message = ex.Message, certificate = (object?)null };
        }
        return Cadena(cert, new X509Certificate2Collection(), fecha, sellada);
    }

    static object Cadena(X509Certificate2 cert, X509Certificate2Collection extra, DateTime? fecha, bool sellada)
    {
        using var ch = new X509Chain();
        ch.ChainPolicy.ExtraStore.AddRange(extra);
        ch.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        ch.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        ch.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(8);
        ch.ChainPolicy.VerificationTime = fecha ?? DateTime.Now;
        ch.Build(cert);

        var fallos = ch.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (a, s) => a | s.Status);
        var datos = Datos(cert, ch);

        if (fallos.HasFlag(X509ChainStatusFlags.Revoked))
            return new { status = "revoked", errorCode = "REVOKED", message = Idioma.T("revocado"), certificate = datos };

        if ((fallos & (X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain)) != 0)
        {
            bool propio = Compat.Iguales(cert.SubjectName.RawData, cert.IssuerName.RawData);
            string codigo = propio ? "SELF_SIGNED" : fallos.HasFlag(X509ChainStatusFlags.PartialChain) ? "UNKNOWN_ISSUER" : "UNTRUSTED_ISSUER";
            return new { status = "untrusted", errorCode = codigo, message = (string?)null, certificate = datos };
        }

        if (fallos.HasFlag(X509ChainStatusFlags.NotTimeValid))
            return new
            {
                status = "expired", errorCode = "EXPIRED",
                message = Idioma.T("noEnVigor", (fecha ?? DateTime.Now).ToString(Idioma.T("formatoFecha"))) + (sellada ? Idioma.T("segunSello") : "."),
                certificate = datos,
            };

        return new { status = "verified", errorCode = (string?)null, message = (string?)null, certificate = datos };
    }

    static object Invalida(string motivo, X509Certificate2? cert) =>
        new { status = "invalid", errorCode = "BAD_SIGNATURE", message = motivo, certificate = cert is null ? null : Datos(cert, null) };

    static object Datos(X509Certificate2 cert, X509Chain? ch) => new
    {
        subjectCN = cert.GetNameInfo(X509NameType.SimpleName, false),
        issuerCN = cert.GetNameInfo(X509NameType.SimpleName, true),
        notBefore = cert.NotBefore.ToString("o"),
        notAfter = cert.NotAfter.ToString("o"),
        serialNumber = cert.SerialNumber,
        der = Convert.ToBase64String(cert.RawData),
        chain = ch?.ChainElements.Cast<X509ChainElement>().Select(e => new
        {
            subjectCN = e.Certificate.GetNameInfo(X509NameType.SimpleName, false),
            issuerCN = e.Certificate.GetNameInfo(X509NameType.SimpleName, true),
            notAfter = e.Certificate.NotAfter.ToString("o"),
        }).ToArray(),
    };

    /// <summary>
    /// La hora del sello de tiempo que lleva la firma (atributo sin firmar), si lo lleva y es bueno: el sello es de
    /// la firma (su huella coincide) y la firma de la autoridad de sellado es válida. Con BouncyCastle, que ya va con iText.
    /// </summary>
    internal static DateTime? FechaDelSello(byte[] der, byte[]? contenido)
    {
        try
        {
            var cms = contenido is null ? new CmsSignedData(der) : new CmsSignedData(new CmsProcessableByteArray(contenido), der);
            foreach (SignerInformation firmante in cms.GetSignerInfos().GetSigners())
            {
                var atributo = firmante.UnsignedAttributes?[new DerObjectIdentifier(OidSelloEnFirma)];
                if (atributo is null || atributo.AttrValues.Count == 0) continue;
                var tok = new TimeStampToken(new CmsSignedData(atributo.AttrValues[0].GetEncoded()));
                if (HuellaCoincide(tok, firmante.GetSignature()) && CertificadoTsa(tok) is not null)
                    return tok.TimeStampInfo.GenTime.ToLocalTime();
            }
        }
        catch (Exception) { }
        return null;
    }

    static bool HuellaCoincide(TimeStampToken tok, byte[] datos) =>
        Compat.Iguales(DigestUtilities.CalculateDigest(tok.TimeStampInfo.MessageImprintAlgOid, datos), tok.TimeStampInfo.GetMessageImprintDigest());

    /// <summary>El certificado de la autoridad de sellado, si su firma del sello es válida.</summary>
    static X509Certificate2? CertificadoTsa(TimeStampToken tok)
    {
        foreach (var c in tok.GetCertificates().EnumerateMatches(tok.SignerID))
        {
            try { tok.Validate(c); return new X509Certificate2(c.GetEncoded()); }
            catch (Exception) { }
        }
        return null;
    }

    /// <summary>El hueco de /Contents viene relleno de ceros por detrás: se deja solo la estructura DER.</summary>
    internal static byte[] RecortarDer(byte[] b)
    {
        if (b.Length < 2 || b[0] != 0x30) return b;
        int largo, cab;
        if (b[1] < 0x80) { largo = b[1]; cab = 2; }
        else
        {
            int n = b[1] & 0x7f;
            if (n == 0 || n > 4 || b.Length < 2 + n) return b; // longitud indefinida (BER): tal cual
            largo = 0;
            for (int i = 0; i < n; i++) largo = (largo << 8) | b[2 + i];
            cab = 2 + n;
        }
        return cab + largo <= b.Length ? b.Trozo(0, cab + largo) : b;
    }

    /// <summary>Fecha PDF «D:20261024193000+02'00'».</summary>
    static DateTime? FechaPdf(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        s = s!.StartsWith("D:") ? s.Substring(2) : s;
        if (s.Length >= 14 && DateTime.TryParseExact(s.Substring(0, 14), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
            return d;
        return null;
    }
}
