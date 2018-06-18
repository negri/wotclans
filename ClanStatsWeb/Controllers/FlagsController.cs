using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using Negri.Wot.Models;

namespace Negri.Wot.Site.Controllers
{
    public class FlagsController : Controller
    {
        // GET: Flags
        [OutputCache(CacheProfile = "Normal")]
        public ActionResult Index()
        {
            var path = Server.MapPath("~/Images/Flags/");
            var di = new DirectoryInfo(path);

            var model = new FlagsPage
            {
                Codes = di.EnumerateFiles("??.png", SearchOption.TopDirectoryOnly).Select(fi => fi.Name.Substring(0, 2)).OrderBy(c => c).ToArray()
            };

            return View(model);
        }
    }
}