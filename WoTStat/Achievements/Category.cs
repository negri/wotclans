using System;

namespace Negri.Wot.Achievements
{
    /// <summary>
    /// Category of the Award
    /// </summary>
    public enum Category
    {
        /// <summary>
        /// Unknown 
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Medals
        /// </summary>
        Achievements = 1,

        /// <summary>
        /// The common ribbons
        /// </summary>
        Ribbons = 2
    }

    public static class CategoryExtensions
    {
        public static Category Parse(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Category.Unknown;
            }

            if (Enum.TryParse(s, true, out Category category))
            {
                return category;
            }

            return Category.Unknown;

        }
    }
}