using System.Globalization;

namespace PDFSIMPLE;

/// <summary>
/// El idioma del programa: español, francés o inglés. Lo elige el usuario con el icono de idiomas de la barra
/// (se guarda en los ajustes); la primera vez, el de Windows. El visor (pdf.js y lector.js) recibe el mismo
/// por una &lt;meta&gt; al servir viewer.html; aquí están los textos de ventanas, diálogos y firmas.
/// </summary>
static class Idioma
{
    public static readonly string[] Disponibles = ["es", "fr", "en"];

    public static string Actual
    {
        get
        {
            string? i = Ajustes.Actual.Idioma;
            if (i is not null && Disponibles.Contains(i)) return i;
            string w = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return w switch { "es" or "ca" or "gl" or "eu" => "es", "fr" => "fr", _ => "en" };
        }
    }

    /// <summary>La carpeta de traducciones de pdf.js.</summary>
    public static string DePdfjs => Actual switch { "fr" => "fr", "en" => "en-US", _ => "es-ES" };

    /// <summary>Para WebView2 (menús del navegador, diálogo de imprimir).</summary>
    public static string DeNavegador => Actual switch { "fr" => "fr-FR", "en" => "en-US", _ => "es-ES" };

    public static string T(string clave, params object[] args)
    {
        if (!Textos.TryGetValue(clave, out var t)) return clave;
        string s = Actual switch { "fr" => t.fr, "en" => t.en, _ => t.es };
        return args.Length > 0 ? string.Format(s, args) : s;
    }

    static readonly Dictionary<string, (string es, string fr, string en)> Textos = new()
    {
        // Ventana
        ["guardarCambios"] = ("¿Guardar los cambios en {0}?", "Enregistrer les modifications de {0} ?", "Save changes to {0}?"),
        ["filtroPdf"] = ("Documento PDF (*.pdf)|*.pdf", "Document PDF (*.pdf)|*.pdf", "PDF document (*.pdf)|*.pdf"),
        ["filtroTodos"] = ("Todos los archivos (*.*)|*.*", "Tous les fichiers (*.*)|*.*", "All files (*.*)|*.*"),
        ["documento"] = ("documento", "document", "document"),
        ["firmado"] = ("firmado", "signe", "signed"),
        ["faltaWebView2"] = (
            "Para ver los PDF hace falta «Microsoft Edge WebView2», que no está en este equipo. ¿Descargarlo ahora?",
            "Pour afficher les PDF, « Microsoft Edge WebView2 » est nécessaire et n'est pas installé. Le télécharger maintenant ?",
            "Viewing PDFs needs “Microsoft Edge WebView2”, which is not installed on this computer. Download it now?"),

        // Certificado
        ["firmarConCertificado"] = ("Firmar con certificado", "Signer avec un certificat", "Sign with certificate"),
        ["titular"] = ("Titular", "Titulaire", "Holder"),
        ["emitidoPor"] = ("Emitido por", "Émis par", "Issued by"),
        ["caduca"] = ("Caduca", "Expire le", "Expires"),
        ["firmar"] = ("Firmar", "Signer", "Sign"),
        ["cancelar"] = ("Cancelar", "Annuler", "Cancel"),
        ["aceptar"] = ("Aceptar", "OK", "OK"),
        ["verCertificado"] = ("Ver certificado", "Voir le certificat", "View certificate"),
        ["usarArchivo"] = ("Usar archivo .pfx / .p12…", "Utiliser un fichier .pfx / .p12…", "Use a .pfx / .p12 file…"),
        ["sinCertificados"] = (
            "No hay certificados personales instalados en Windows. Puedes usar un archivo .pfx / .p12.",
            "Aucun certificat personnel n'est installé dans Windows. Vous pouvez utiliser un fichier .pfx / .p12.",
            "There are no personal certificates installed in Windows. You can use a .pfx / .p12 file."),
        ["conCertificados"] = ("Certificados personales instalados en Windows:", "Certificats personnels installés dans Windows :", "Personal certificates installed in Windows:"),
        ["filtroCertificado"] = ("Certificado con clave (*.pfx;*.p12)|*.pfx;*.p12", "Certificat avec clé (*.pfx;*.p12)|*.pfx;*.p12", "Certificate with key (*.pfx;*.p12)|*.pfx;*.p12"),
        ["certificadoNoAbre"] = ("No se ha podido abrir el certificado: {0}", "Impossible d'ouvrir le certificat : {0}", "The certificate could not be opened: {0}"),
        ["sinClavePrivada"] = ("El archivo no lleva la clave privada.", "Le fichier ne contient pas la clé privée.", "The file does not contain the private key."),
        ["contrasenaCertificado"] = ("Contraseña del certificado", "Mot de passe du certificat", "Certificate password"),
        ["claveNoValida"] = ("El certificado no tiene una clave RSA ni ECDSA utilizable.", "Le certificat n'a pas de clé RSA ou ECDSA utilisable.", "The certificate has no usable RSA or ECDSA key."),

        // Firma visible
        ["firmadoPor"] = ("Firmado digitalmente por:", "Signé numériquement par :", "Digitally signed by:"),
        ["fecha"] = ("Fecha", "Date", "Date"),
        ["formatoFecha"] = ("dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss"),
        ["sinSello"] = (
            "Firmado sin sello de tiempo: no se ha podido contactar con el servidor de sellado.",
            "Signé sans horodatage : le serveur d'horodatage n'a pas répondu.",
            "Signed without a timestamp: the timestamp server could not be reached."),

        // Comprobar firmas
        ["selloIlegible"] = ("El sello de tiempo no se puede leer.", "L'horodatage est illisible.", "The timestamp cannot be read."),
        ["cambiadoTrasSello"] = ("El documento ha cambiado después del sello de tiempo.", "Le document a été modifié après l'horodatage.", "The document has changed since it was timestamped."),
        ["cambiadoTrasFirma"] = ("El documento ha cambiado después de firmarlo.", "Le document a été modifié après sa signature.", "The document has changed since it was signed."),
        ["sinCertificadoFirmante"] = ("La firma no trae el certificado del firmante.", "La signature ne contient pas le certificat du signataire.", "The signature does not include the signer's certificate."),
        ["revocado"] = ("El certificado del firmante está revocado.", "Le certificat du signataire est révoqué.", "The signer's certificate has been revoked."),
        ["noEnVigor"] = ("El certificado no estaba en vigor el {0}", "Le certificat n'était pas valide le {0}", "The certificate was not valid on {0}"),
        ["segunSello"] = (" (según el sello de tiempo).", " (selon l'horodatage).", " (according to the timestamp)."),

        // Instalación
        ["documentoPdf"] = ("Documento PDF", "Document PDF", "PDF document"),
        ["descripcion"] = (
            "Lector de PDF sin publicidad: formularios, texto y firma digital",
            "Lecteur PDF sans publicité : formulaires, texte et signature numérique",
            "Ad-free PDF reader: forms, text and digital signatures"),
        ["desinstalar"] = ("¿Desinstalar {0}?", "Désinstaller {0} ?", "Uninstall {0}?"),
        ["desinstalado"] = ("{0} se ha desinstalado.", "{0} a été désinstallé.", "{0} has been uninstalled."),
        ["comoPredeterminado"] = (
            "En Configuración: escribe .pdf en el buscador de arriba, pulsa la casilla que sale debajo, elige {0} y «Establecer predeterminado».",
            "Dans Paramètres : tapez .pdf dans la recherche en haut, cliquez sur la case qui apparaît dessous, choisissez {0} puis « Définir par défaut ».",
            "In Settings: type .pdf in the search box at the top, click the box that appears below, choose {0} and “Set default”."),
    };
}
