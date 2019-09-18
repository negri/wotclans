using System;

namespace Negri.Wot.Achievements
{
    /// <summary>
    /// Section of the Award
    /// </summary>
    public enum Section
    {
        /// <summary>
        /// Unknown 
        /// </summary>
        Unknown = 0,

        Epic = 1,

        Special= 2,

        Battle = 3,

        Action = 4,

        Group = 5,

        Class = 6,

        Memorial = 7,
    }

    public static class SectionExtensions
    {
        public static Section Parse(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Section.Unknown;
            }

            if (Enum.TryParse(s, true, out Section section))
            {
                return section;
            }

            return Section.Unknown;
        }
    }
}