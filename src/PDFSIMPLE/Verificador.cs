using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

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
                if (!Rfc3161TimestampToken.TryDecode(der, out var tok, out _))
                    return Invalida(Idioma.T("selloIlegible"), null);
                if (!tok!.VerifySignatureForData(contenido, out var tsa))
                    return Invalida(Idioma.T("cambiadoTrasSello"), tsa);
                cert = tsa!;
                fecha = tok.TokenInfo.Timestamp.LocalDateTime;
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

                if (subFilter == "adbe.pkcs7.sha1" && !SHA1.HashData(contenido).AsSpan().SequenceEqual(cms.ContentInfo.Content))
                    return Invalida(Idioma.T("cambiadoTrasFirma"), cert);

                foreach (var attr in si.UnsignedAttributes)
                    if (attr.Oid.Value == OidSelloEnFirma && attr.Values.Count > 0 &&
                        Rfc3161TimestampToken.TryDecode(attr.Values[0].RawData, out var tok, out _) &&
                        tok!.VerifySignatureForSignerInfo(si, out _))
                    {
                        fecha = tok.TokenInfo.Timestamp.LocalDateTime;
                        sellada = true;
                    }
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
        return Cadena(cert, [], fecha, sellada);
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
            bool propio = cert.SubjectName.RawData.AsSpan().SequenceEqual(cert.IssuerName.RawData);
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
        chain = ch?.ChainElements.Select(e => new
        {
            subjectCN = e.Certificate.GetNameInfo(X509NameType.SimpleName, false),
            issuerCN = e.Certificate.GetNameInfo(X509NameType.SimpleName, true),
            notAfter = e.Certificate.NotAfter.ToString("o"),
        }).ToArray(),
    };

    /// <summary>El hueco de /Contents viene relleno de ceros por detrás: se deja solo la estructura DER.</summary>
    static byte[] RecortarDer(byte[] b)
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
        return cab + largo <= b.Length ? b[..(cab + largo)] : b;
    }

    /// <summary>Fecha PDF «D:20261024193000+02'00'».</summary>
    static DateTime? FechaPdf(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        s = s.StartsWith("D:") ? s[2..] : s;
        if (s.Length >= 14 && DateTime.TryParseExact(s[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
            return d;
        return null;
    }
}
