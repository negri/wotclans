using System;

namespace Negri.Wot.Tanks
{
    public static class PremiumSelectionExtensions
    {
        public static bool Filter(this PremiumSelection premiumSelection, bool isPremium)
        {
            switch (premiumSelection)
            {
                case PremiumSelection.OnlyRegular:
                    return !isPremium;
                case PremiumSelection.OnlyPremium:
                    return isPremium;
                case PremiumSelection.RegularAndPremium:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(premiumSelection), premiumSelection, @"Not supported");
            }
        }

        public static bool TryParse(this string s, out PremiumSelection premiumSelection)
        {
            
            if (string.IsNullOrWhiteSpace(s))
            {
                premiumSelection = PremiumSelection.OnlyRegular;
                return true;
            }

            if (Enum.TryParse(s, true, out premiumSelection))
            {
                return true;
            }

            if (bool.TryParse(s.ToLowerInvariant(), out var b))
            {
                premiumSelection = b ? PremiumSelection.RegularAndPremium : PremiumSelection.OnlyRegular;
                return true;
            }

            switch (s.ToLowerInvariant())
            {
                case "r":
                case "regular":
                case "tree":
                    premiumSelection = PremiumSelection.OnlyRegular;
                    break;

                case "p":
                case "premium":
                case "gold":
                    premiumSelection = PremiumSelection.OnlyPremium;
                    break;

                case "a":
                case "all":
                case "b":
                case "both":
                case "rp":
                case "regular,premium":
                // ReSharper disable once StringLiteralTypo
                case "regularpremium":
                case "any":
                    premiumSelection = PremiumSelection.RegularAndPremium;
                    break;

                default:
                    return false;
            }

            return true;
        }
    }
}