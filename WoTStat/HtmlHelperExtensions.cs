using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Negri.Wot
{
    public static class HtmlHelperExtensions
    {
        public static bool HasWarnings(this HtmlHelper helper, CultureInfo uiCulture = null)
        {
            if (HttpContext.Current == null)
            {
                return false;
            }

            if (uiCulture == null)
            {
                uiCulture = CultureInfo.CurrentUICulture;
            }

            var language = uiCulture.TwoLetterISOLanguageName.ToLowerInvariant();
            var fileName = $"Warning.{language}.html";
            var path = Path.Combine(HttpContext.Current.Server.MapPath("~"), fileName);
            if (File.Exists(path))
            {
                return true;
            }

            path = Path.Combine(HttpContext.Current.Server.MapPath("~"), "Warning.html");
            if (File.Exists(path))
            {
                return true;
            }

            return false;
        }

        public static MvcHtmlString GetWarning(this HtmlHelper helper, CultureInfo uiCulture = null)
        {
            if (HttpContext.Current == null)
            {
                return MvcHtmlString.Empty;
            }

            if (uiCulture == null)
            {
                uiCulture = CultureInfo.CurrentUICulture;                
            }

            var language = uiCulture.TwoLetterISOLanguageName.ToLowerInvariant();
            var fileName = $"Warning.{language}.html";
            var path = Path.Combine(HttpContext.Current.Server.MapPath("~"), fileName);
            if (File.Exists(path))
            {
                return MvcHtmlString.Create(File.ReadAllText(path, Encoding.UTF8));
            }

            path = Path.Combine(HttpContext.Current.Server.MapPath("~"), "Warning.html");
            if (File.Exists(path))
            {
                return MvcHtmlString.Create(File.ReadAllText(path, Encoding.UTF8));
            }

            return MvcHtmlString.Empty;
        }
    }
}