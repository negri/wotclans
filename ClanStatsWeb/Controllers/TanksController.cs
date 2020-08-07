using System;
using System.Web;
using System.Web.Mvc;
using Negri.Wot.Models;

namespace Negri.Wot.Site.Controllers
{
    public class TanksController : Controller
    {
        /// <summary>
        ///     Devolve as MoE dos tanques
        /// </summary>
        /// <param name="date">Data, opcional. Se não informado será a data mais recente disponível</param>
        [OutputCache(CacheProfile = "Normal")]
        public ActionResult Moe(DateTime? date = null)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var moes = getter.GetTanksMoe(date);
            var model = new TanksMoe(moes);

            return View(model);
        }

        /// <summary>
        ///     Devolve os WN8 dos tanques
        /// </summary>
        /// <param name="date">Data, opcional. Se não informado será a data mais recente disponível</param>
        [OutputCache(CacheProfile = "Normal")]
        public ActionResult WN8(DateTime? date = null)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var d = getter.GetTanksWN8ReferenceValues(date);
            var model = new TanksWn8(d);

            return View(model);
        }

        /// <summary>
        ///     Devolve os WN8 dos tanques
        /// </summary>
        /// <param name="date">Data, opcional. Se não informado será a data mais recente disponível</param>
        [OutputCache(CacheProfile = "Normal")]
        public ActionResult WN8Excel(DateTime? date = null)
        {
            try
            {
                var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                    () => new FileGetter(GlobalHelper.DataFolder));

                var d = getter.GetTanksWN8ReferenceValues(date);
                byte[] bytes = d.GetExcel();
                return new FileContentResult(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = $"WN8.{d.Date:yyyy-MM-dd}.xlsx"
                };
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, ex.Message);
            }
        }

        /// <summary>
        ///     Estatísticas dos tanques
        /// </summary>
        /// <param name="tankId">Id do tanque</param>
        [OutputCache(CacheProfile = "Normal")]
        public ActionResult Stats(long tankId)
        {

            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var tr = getter.GetTankReference(tankId);

            if (tr == null)
            {
                return HttpNotFound($"Not found a tank with id {tankId}");
            }

            return View(tr);
        }
    }
}