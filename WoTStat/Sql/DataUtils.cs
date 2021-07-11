using System;
using System.Data;
using System.Diagnostics;

namespace Negri.Wot.Sql
{
    public static class DataUtils
    {
        /// <summary>
        ///     Gets the value or default.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dr">The dr.</param>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public static T GetValueOrDefault<T>(this IDataRecord dr, int index)
        {
            if (dr.IsDBNull(index))
            {
                return default(T);
            }

            try
            {
                var type = typeof(T);
                var nullableUnderlying = Nullable.GetUnderlyingType(type);
                if ((nullableUnderlying != null) && nullableUnderlying.IsEnum)
                {
                    var i = dr[index];
                    return (T)Enum.Parse(nullableUnderlying, i.ToString());
                }

                return (T)dr[index];
            }
            catch (InvalidCastException ex)
            {
                var dbType = dr[index].GetType();
                var expectedType = typeof(T);

                throw new ApplicationException(
                    string.Format("Erro de cast ao interpretar o campo {0} de {1} para {2}.", index, dbType,
                        expectedType), ex);
            }
        }

        /// <summary>
        ///     Retorna uma string que pode estar nula no BD. Nesse caso retornara string.Empty
        /// </summary>
        public static string GetNullableString(this IDataRecord dr, int index)
        {
            if (dr.IsDBNull(index)) return string.Empty;
            return (string)dr[index];
        }

        /// <summary>
        ///     Obtem um valor que NÃO pode estar nulo no BD.
        /// </summary>
        /// <remarks>
        ///     A vantagem sobre IDateRecord.GetValue é que caso o campo esteja nulo no BD a exceção mostrara o ordinal do
        ///     campo com problemas.
        /// </remarks>
        public static T GetNonNullValue<T>(this IDataRecord dr, int index)
        {
            if (dr.IsDBNull(index))
            {
                throw new NoNullAllowedException($"Null value on DB (at ordinal {index}) is not expected.");
            }
            try
            {
                return (T)dr[index];
            }
            catch (InvalidCastException ex)
            {
                var dbType = dr[index].GetType();
                var expectedType = typeof(T);

                throw new ApplicationException(
                    $"Cast error on field {index} de {dbType} para {expectedType}.", ex);
            }
        }

        /// <summary>
        ///     Retorna somente a DATA
        /// </summary>
        public static DateTime GetDate(this IDataRecord dr, int index)
        {
            if (dr.IsDBNull(index))
                throw new NoNullAllowedException(string.Format("Null value on DB (at ordinal {0}) is not expected.",
                    index));
            var d = (DateTime)dr[index];
            return d.Date.RemoveKind();
        }

        /// <summary>
        ///     Retorna somente a DATA (null caso seja nulo no BD)
        /// </summary>
        public static DateTime? GetNullableDate(this IDataRecord dr, int index)
        {
            if (dr.IsDBNull(index)) return null;
            var d = (DateTime)dr[index];
            return d.Date.RemoveKind();
        }

        /// <summary>
        ///     Gets the date time UTC.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="index">The field number.</param>
        /// <returns></returns>
        public static DateTime GetDateTimeUtc(this IDataRecord reader, int index)
        {
            if (reader.IsDBNull(index))
                throw new NoNullAllowedException(string.Format("Null value on DB (at ordinal {0}) is not expected.",
                    index));
            var d = new DateTime(reader.GetDateTime(index).Ticks, DateTimeKind.Utc);
            return d;
        }

        /// <summary>
        ///     Gets the date time UTC.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="index">The field number.</param>
        /// <returns></returns>
        public static DateTime? GetNullableDateTimeUtc(this IDataRecord reader, int index)
        {
            if (reader.IsDBNull(index)) return null;
            var d = new DateTime(reader.GetDateTime(index).Ticks, DateTimeKind.Utc);
            return d;
        }

        /// <summary>
        ///     Gets the bytes.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="index">The field number.</param>
        /// <returns></returns>
        public static byte[] GetBytes(this IDataRecord reader, int index)
        {
            if (reader.IsDBNull(index)) return new byte[0];
            var size = reader.GetBytes(index, 0, null, 0, 0);
            if (size <= 0) return new byte[0];
            var buffer = new byte[size];
            var read = reader.GetBytes(index, 0, buffer, 0, (int)size);
            Debug.Assert(read == size);
            return buffer;
        }

        public static double Normalize(this double d)
        {
            if (double.IsNaN(d)) return 0.0;
            if (double.IsNegativeInfinity(d)) return double.MinValue;
            if (double.IsPositiveInfinity(d)) return double.MaxValue;
            return d;
        }
        
    }
}