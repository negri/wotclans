using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Negri.Wot
{
    public static class BasicExtensions
    {

        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Random Random = new();

        /// <summary>
        /// Muda o tipo de Data (não é o mesmo que ToLocal ou ToUniversal, só mexe no DateTimeKind)
        /// </summary>
        public static DateTime ChangeKind(this DateTime date, DateTimeKind kind)
        {
            return DateTime.SpecifyKind(date, kind);
        }



        public static DateTime RemoveKind(this DateTime date)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Converts a UNIX time to a DateTime
        /// </summary>
        /// <param name="unixTime"></param>
        /// <returns></returns>
        public static DateTime ToDateTime(this long unixTime)
        {
            return UnixEpoch.AddSeconds(unixTime);
        }

        /// <summary>
        /// Retorna o dia da semana imediatamente anterior a data passada
        /// </summary>
        /// <param name="date"></param>
        /// <param name="dayOfWeek"></param>
        /// <returns></returns>
        public static DateTime PreviousDayOfWeek(this DateTime date, DayOfWeek dayOfWeek)
        {
            date = date.AddDays(-1);
            while (date.DayOfWeek != dayOfWeek)
            {
                date = date.AddDays(-1);
            }
            return date.Date;
        }

        /// <summary>
        /// Converts to a flat string, without space, lower case, no diacritics
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string GetFlatString(this string s)
        {
            return s.RemoveDiacritics().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace(".", "")
                .Replace(",", "").Replace("(", "").Replace(")", "").Replace("/", "").Replace("’", "").Replace("'", "")
                .Replace("\"", "");
        }

        /// <summary>
        /// Faz o Equals de strings, desconsiderando Case e Acentos (Diacriticos)
        /// </summary>
        public static bool EqualsCiAi(this string a, string b)
        {
            if ((string.IsNullOrWhiteSpace(a)) && (string.IsNullOrWhiteSpace(b)))
            {
                return true;
            }

            if ((!string.IsNullOrWhiteSpace(a)) && (string.IsNullOrWhiteSpace(b)))
            {
                return false;
            }

            if ((string.IsNullOrWhiteSpace(a)) && (!string.IsNullOrWhiteSpace(b)))
            {
                return false;
            }

            return
                string.Compare(a, b, CultureInfo.InvariantCulture,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;
        }

        private static readonly List<string> RomanNumerals = new() { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        private static readonly List<int> Numerals = new() { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };


        /// <summary>
        /// Converte para romanos
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        /// <returns>
        /// from https://stackoverflow.com/questions/22392810/integer-to-roman-format
        /// </returns>
        public static string ToRomanNumeral(this int number)
        {
            var romanNumeral = string.Empty;
            while (number > 0)
            {
                // find biggest numeral that is less than equal to number
                var index = Numerals.FindIndex(x => x <= number);
                // subtract it's value from your number
                number -= Numerals[index];
                // tack it onto the end of your roman numeral
                romanNumeral += RomanNumerals[index];
            }
            return romanNumeral;
        }

        /// <summary>
        ///     Remove acentos e cedilhas
        /// </summary>
        public static string RemoveDiacritics(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var stFormD = input.Normalize(NormalizationForm.FormD);
            var len = stFormD.Length;
            var sb = new StringBuilder();
            for (var i = 0; i < len; i++)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[i]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(stFormD[i]);
                }
            }
            return (sb.ToString().Normalize(NormalizationForm.FormC));
        }        

        public static void CopyTo(this Stream src, Stream dest)
        {
            var bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Zip(this string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);

            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                CopyTo(msi, gs);
            }

            return mso.ToArray();
        }

        public static string Unzip(this byte[] bytes)
        {
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            {
                CopyTo(gs, mso);
            }

            return Encoding.UTF8.GetString(mso.ToArray());
        }

        /// <summary>
        ///     Normaliza a name so it can be used as a file name
        /// </summary>
        public static string NormalizeFileName(this string fileName, bool replaceWhiteSpace = true)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }
            fileName = fileName.RemoveDiacritics();

            var regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex($"[{Regex.Escape(regexSearch)}]");
            fileName = r.Replace(fileName, "");
            if (replaceWhiteSpace)
            {
                fileName = fileName.Replace(" ", ".");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(@"only invalid charcters!", nameof(fileName));
            }

            return fileName;
        }

        /// <summary>
        /// Get Const values of a certain kinf
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns>The constants values</returns>
        /// <remarks>From https://stackoverflow.com/questions/10261824/how-can-i-get-all-constants-of-a-type-by-reflection#10261848 </remarks>
        public static List<T> GetAllPublicConstantValues<T>(this Type type)
        {
            return type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(T))
                .Select(x => (T)x.GetRawConstantValue())
                .ToList();
        }

        public static T GetRandom<T>(this IEnumerable<T> itens)
        {
            if (itens == null)
            {
                return default(T);
            }

            var a = itens.ToArray();
            if (a.Length == 0)
            {
                return default(T);
            }

            var index = Random.Next(0, a.Length);
            return a[index];
        }

        /// <summary>
        /// The site for the platform, like https://wotclans.com.br or https://ps.wotclans.com.br
        /// </summary>
        /// <returns></returns>
        public static string SiteUrl()
        {
            return "https://wotclans.com.br";
        }

        /// <summary>
        /// Apply an action on every element
        /// </summary>
        /// <remarks>
        /// Use with extreme caution!
        /// </remarks>
        public static IEnumerable<T> ActOnEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null)
            {
                yield break;
            }

            if (action == null)
            {
                foreach (var e in source)
                {
                    yield return e;
                }

                yield break;
            }
            
            foreach (var e in source)
            {
                action(e);
                yield return e;
            }
        }

    }
}