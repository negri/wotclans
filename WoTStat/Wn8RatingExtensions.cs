using System.Drawing;
using System.Globalization;
using Negri.Wot.Properties;

namespace Negri.Wot
{
    public static class Wn8RatingExtensions
    {
        public static Wn8Rating GetRating(this double wn8)
        {
            if (wn8 >= (int) Wn8Rating.SuperUnicum) return Wn8Rating.SuperUnicum;
            if (wn8 >= (int) Wn8Rating.Unicum) return Wn8Rating.Unicum;
            if (wn8 >= (int) Wn8Rating.Great) return Wn8Rating.Great;
            if (wn8 >= (int) Wn8Rating.VeryGood) return Wn8Rating.VeryGood;
            if (wn8 >= (int) Wn8Rating.Good) return Wn8Rating.Good;
            if (wn8 >= (int) Wn8Rating.AboveAverage) return Wn8Rating.AboveAverage;
            if (wn8 >= (int) Wn8Rating.Average) return Wn8Rating.Average;
            if (wn8 >= (int) Wn8Rating.BelowAverage) return Wn8Rating.BelowAverage;
            if (wn8 >= (int) Wn8Rating.Bad) return Wn8Rating.Bad;
            return Wn8Rating.VeryBad;
        }


        public static string ToRatingString(this double wn8, CultureInfo culture = null)
        {
            return ToRatingString(wn8.GetRating(), culture);
        }

        public static string ToRatingString(this Wn8Rating rating, CultureInfo culture = null)
        {
            if (culture == null)
            {
                culture = CultureInfo.CurrentUICulture;
            }

            var resId = "Wn8" + rating;
            return Resources.ResourceManager.GetString(resId, culture);
        }

        public static string ToLabelClass(this double wn8)
        {
            return $"label-{wn8.GetRating().ToString().ToLowerInvariant()}";
        }

        public static string ToLabelClass(this Wn8Rating wn8)
        {
            return $"label-{wn8.ToString().ToLowerInvariant()}";
        }

        public static Color ToColor(this double wn8)
        {
            Color color;
            if (wn8 < (int)Wn8Rating.VeryBad)
            {
                color = Wn8Rating.VeryBad.GetColor();
            }
            else if ((int)Wn8Rating.VeryBad <= wn8 && wn8 < (int)Wn8Rating.Bad)
            {
                color = MixColor(Wn8Rating.VeryBad, Wn8Rating.Bad, wn8);
            }
            else if ((int)Wn8Rating.Bad <= wn8 && wn8 < (int)Wn8Rating.BelowAverage)
            {
                color = MixColor(Wn8Rating.Bad, Wn8Rating.BelowAverage, wn8);
            }
            else if ((int)Wn8Rating.BelowAverage <= wn8 && wn8 < (int)Wn8Rating.Average)
            {
                color = MixColor(Wn8Rating.BelowAverage, Wn8Rating.Average, wn8);
            }
            else if ((int)Wn8Rating.Average <= wn8 && wn8 < (int)Wn8Rating.AboveAverage)
            {
                color = MixColor(Wn8Rating.Average, Wn8Rating.AboveAverage, wn8);
            }
            else if ((int)Wn8Rating.AboveAverage <= wn8 && wn8 < (int)Wn8Rating.Good)
            {
                color = MixColor(Wn8Rating.AboveAverage, Wn8Rating.Good, wn8);
            }
            else if ((int)Wn8Rating.Good <= wn8 && wn8 < (int)Wn8Rating.VeryGood)
            {
                color = MixColor(Wn8Rating.Good, Wn8Rating.VeryGood, wn8);
            }
            else if ((int)Wn8Rating.VeryGood <= wn8 && wn8 < (int)Wn8Rating.Great)
            {
                color = MixColor(Wn8Rating.VeryGood, Wn8Rating.Great, wn8);
            }
            else if ((int)Wn8Rating.Great <= wn8 && wn8 < (int)Wn8Rating.Unicum)
            {
                color = MixColor(Wn8Rating.Great, Wn8Rating.Unicum, wn8);
            }
            else if ((int)Wn8Rating.Unicum <= wn8 && wn8 < (int)Wn8Rating.SuperUnicum)
            {
                color = MixColor(Wn8Rating.Unicum, Wn8Rating.SuperUnicum, wn8);
            }
            else
            {
                color = Wn8Rating.SuperUnicum.GetColor();
            }

            return color;
        }

        public static string ToWebColor(this double wn8, double? alpha = null, double? colorCorrection = null)
        {
            var color = wn8.ToColor();

            if (colorCorrection != null)
            {
                color = color.ChangeColorBrightness(colorCorrection.Value);
            }

            if (alpha == null)
            {
                return $"rgb({color.R}, {color.G}, {color.B})";
            }
            else
            {
                return $"rgba({color.R}, {color.G}, {color.B}, {alpha.Value.ToString("0.00", CultureInfo.InvariantCulture)})";
            }            
        }

        private static Color ChangeColorBrightness(this Color color, double correctionFactor)
        {
            var red = (double)color.R;
            var green = (double)color.G;
            var blue = (double)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
        }

        private static Color MixColor(Wn8Rating a, Wn8Rating b, double wn8)
        {
            double initial = (int) a;
            double final = (int) b;
            var factor = (wn8 - initial) / (final - initial);

            var colorA = a.GetColor();
            var colorB = b.GetColor();

            var rr = (int) ((colorB.R - colorA.R) * factor + colorA.R);
            var gg = (int) ((colorB.G - colorA.G) * factor + colorA.G);
            var bb = (int) ((colorB.B - colorA.B) * factor + colorA.B);

            return Color.FromArgb(rr, gg, bb);
        }

        private static Color GetColor<TEnumType>(this TEnumType enumItem)
        {
            var name = enumItem.ToString();
            var fi = enumItem.GetType().GetField(name);
            if (!(fi.GetCustomAttributes(typeof(RgbColorAttribute), false) is RgbColorAttribute[] attributes) || attributes.Length == 0)
            {
                return Color.Black;
            }

            return Color.FromArgb(attributes[0].Red, attributes[0].Green, attributes[0].Blue);
        }
    }
}