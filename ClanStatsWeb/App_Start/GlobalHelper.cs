using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using log4net;
using Negri.Wot.Properties;

namespace Negri.Wot.Site
{
    /// <summary>
    ///     Variáveis globais da Aplicação
    /// </summary>
    public static class GlobalHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GlobalHelper));

        /// <summary>
        ///     A plataforma para essa instância do site
        /// </summary>
        public static Platform Platform { get; set; }

        /// <summary>
        ///     O diretorio raiz de dados
        /// </summary>
        public static string DataFolder { get; set; }

        /// <summary>
        ///     Tempo de vida dos caches, em minutos
        /// </summary>
        public static int CacheMinutes { get; set; }

        /// <summary>
        /// The default data provider to direct players seaching for details
        /// </summary>
        public static PlayerDataOrigin DefaultPlayerDetails { get; set; } = PlayerDataOrigin.WotInfo;

        /// <summary>
        /// A língua que esta sendo servida
        /// </summary>
        public static string Language
        {
            get
            {
                string lang = string.Empty;

                if (HttpContext.Current != null)
                {
                    string requestLang = HttpContext.Current.Request["lang"];
                    if (!string.IsNullOrEmpty(requestLang))
                    {
                        lang = requestLang;
                    }
                }

                if (string.IsNullOrWhiteSpace(lang))
                {
                    lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                }

                switch (lang)
                {
                    case "pt":
                        return "pt";
                    case "es":
                        return "es";
                    case "de":
                        return "de";
                    case "pl":
                        return "pl";
                    case "ru":
                        return "ru";
                    case "fr":
                        return "fr";
                    default:
                        return "en";
                }
            }
        }

        /// <summary>
        /// A cultura que está sendo servida
        /// </summary>
        public static string Culture
        {
            get
            {
                string culture = CultureInfo.CurrentUICulture.ToString().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(culture))
                {
                    return "en-us";
                }
                if (culture.Length != 5)
                {
                    return "en-us";
                }
                switch (culture.Substring(0, 2))
                {
                    case "pt":
                        return "pt-" + culture.Substring(3, 2);
                    case "fr":
                        return "fr-" + culture.Substring(3, 2);
                    default:
                        return "en-" + culture.Substring(3, 2);
                }
            }
        }

        public static string PlataformTag
        {
            get
            {
                switch (Platform)
                {
                    case Platform.Virtual:
                        return "xxx";
                    case Platform.XBOX:
                        return "xbox";
                    case Platform.PS:
                        return "ps4";
                    default:
                        return "xxx";
                }
            }
        }

        public static string ForPlataform
        {
            get
            {
                switch (Platform)
                {
                    case Platform.Virtual:
                        return "?";
                    case Platform.XBOX:
                        return Resources.ForXbox;
                    case Platform.PS:
                        return Resources.ForPs;
                    default:
                        return "?";
                }
            }
        }

        public static string ExternalTarget => IsMobile() ? "target=\"_blank\"" : string.Empty;

        /// <summary>
        /// If a external, by player performance page, should be used
        /// </summary>
        public static bool UseExternalPlayerPage { get; set; } = true;

        /// <summary>
        /// Url para detalhes do jogador em site externo
        /// </summary>
        public static string GetPlayerUrl(Player player, bool isRecent = false)
        {
            return GetPlayerUrl(player.ClanTag, player.Id, isRecent);
        }

        public static string GetPlayerUrl(string clanTag, long playerId, bool isRecent = false)
        {                        
            return VirtualPathUtility.ToAbsolute(isRecent
                ? $"~/Clan/{clanTag}/Commanders/{playerId}/Recent"
                : $"~/Clan/{clanTag}/Commanders/{playerId}/All");
        }

        private static readonly Regex DetectMobile1 = new Regex(@"(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows ce|xda|xiino", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
        private static readonly Regex DetectMobile2 = new Regex(@"1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

        /// <summary>
        /// Se o cliente é mobile
        /// </summary>
        /// <remarks>
        /// From http://detectmobilebrowsers.com/
        /// </remarks>        
        public static bool IsMobile()
        {
            try
            {
                if (HttpContext.Current == null)
                {
                    return false;
                }

                string u = HttpContext.Current.Request.ServerVariables["HTTP_USER_AGENT"];
                if (string.IsNullOrWhiteSpace(u))
                {
                    return false;
                }

                var uaCache = HttpRuntime.Cache.Get("UserAgentMobile", CacheMinutes,
                    () => new ConcurrentDictionary<string, bool>());
                if (uaCache.TryGetValue(u.ToLowerInvariant(), out var isMobile))
                {
                    return isMobile;
                }

                if (DetectMobile1.IsMatch(u))
                {
                    uaCache.TryAdd(u.ToLowerInvariant(), true);
                    return true;
                }

                if (u.Length < 4)
                {
                    uaCache.TryAdd(u.ToLowerInvariant(), false);
                    return false;
                }

                if (DetectMobile2.IsMatch(u.Substring(0, 4)))
                {
                    uaCache.TryAdd(u.ToLowerInvariant(), true);
                    return true;
                }

                uaCache.TryAdd(u.ToLowerInvariant(), false);
                return false;
            }
            catch (RegexMatchTimeoutException ex)
            {
                Log.WarnFormat("IsMobile timeout of {0} for '{1}' using the pattern '{2}'", ex.MatchTimeout, ex.Input, ex.Pattern);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("IsMobile", ex);
                return false;
            }
        }

   

    }
}