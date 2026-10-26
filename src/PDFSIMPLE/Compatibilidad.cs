using System.Text;
using System.Web.Script.Serialization;

namespace PDFSIMPLE
{
    /// <summary>JSON con el serializador que ya trae .NET Framework (System.Web.Extensions): sin librerías aparte.</summary>
    static class Json
    {
        static readonly JavaScriptSerializer S = new() { MaxJsonLength = int.MaxValue };

        public static byte[] Bytes(object o) => Encoding.UTF8.GetBytes(S.Serialize(o));
        public static string Texto(object o) => S.Serialize(o);
        public static T Leer<T>(string json) => S.Deserialize<T>(json);

        /// <summary>Un objeto JSON como diccionario (números como int o decimal, listas como object[]).</summary>
        public static Dictionary<string, object> Objeto(string? json) =>
            string.IsNullOrWhiteSpace(json) ? new() : S.Deserialize<Dictionary<string, object>>(json!);

        public static string? Cadena(this Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v?.ToString() : null;
        public static bool Si(this Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool b && b;
        public static int Entero(this Dictionary<string, object> d, string k, int porDefecto = 0) =>
            d.TryGetValue(k, out var v) && v is not null ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture) : porDefecto;
    }

    static class Compat
    {
        public static int Ajustar(int v, int min, int max) => v < min ? min : v > max ? max : v;

        public static bool Contiene(this string s, string que) => s.IndexOf(que, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Mover pisando el destino si existe (File.Move de .NET Framework no tiene «overwrite»).</summary>
        public static void MoverPisando(string origen, string destino)
        {
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(origen, destino);
        }

        public static T[] Trozo<T>(this T[] a, int desde, int largo)
        {
            var r = new T[largo];
            Array.Copy(a, desde, r, 0, largo);
            return r;
        }

        public static bool Iguales(byte[] a, byte[] b) => a.Length == b.Length && a.SequenceEqual(b);
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>Para los «record» de C# en .NET Framework.</summary>
    static class IsExternalInit { }
}
