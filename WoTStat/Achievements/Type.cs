using System;

namespace Negri.Wot.Achievements
{
    /// <summary>
    /// Type of the Award
    /// </summary>
    public enum Type
    {
        /// <summary>
        /// Unknown 
        /// </summary>
        Unknown = 0,

        Repeatable = 1,

        Class = 2,

        Custom = 3,

        Series = 4,
    }

    public static class TypeExtensions
    {
        public static Type Parse(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Type.Unknown;
            }

            if (Enum.TryParse(s, true, out Type type))
            {
                return type;
            }

            return Type.Unknown;
        }
    }
}