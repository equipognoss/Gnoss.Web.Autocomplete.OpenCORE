using Es.Riam.Gnoss.AD;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Gnoss.Web.AutoComplete.Controllers
{
    [ApiController]
    public class VersionController : Controller
    {
        [HttpGet]
        [Route("versioninfo")]
        public ActionResult Index()
        {
            return View(VersionEnsambladoAD);
        }

        /// <summary>
        /// Obtiene la versión del ensamblado Es.Riam.Gnoss.AD
        /// </summary>
        public static Version VersionEnsambladoAD
        {
            get
            {
                return typeof(BaseAD).Assembly.GetName().Version;
            }
        }
    }
}
