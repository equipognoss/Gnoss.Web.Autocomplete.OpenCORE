using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ServicioAutoCompletarMVC
{
    public class GnossMiddleware
    {
        private IHostingEnvironment mEnv;
        private readonly RequestDelegate _next;

        public GnossMiddleware(RequestDelegate next, IHostingEnvironment env)
        {
            _next = next;
            mEnv = env;
        }

        public async Task Invoke(HttpContext context, LoggingService loggingService, EntityContext entityContext)
        {
            entityContext.SetTrackingFalse();
            Application_BeginRequest(loggingService);
            await _next(context);
            Application_EndRequest(loggingService);
        }


        protected void Application_BeginRequest(LoggingService pLoggingService)
        {
            pLoggingService.AgregarEntrada("TiemposMVC_Application_BeginRequest");


            pLoggingService.AgregarEntrada("TiemposMVC_Application_FinnRequest");
        }

        protected void Application_EndRequest(LoggingService pLoggingService)
        {
            try
            {
                pLoggingService.AgregarEntrada("TiemposMVC_Application_EndRequest");

                pLoggingService.GuardarTraza(ObtenerRutaTraza());
            }
            catch (Exception) { }
        }

        protected string ObtenerRutaTraza()
        {
            string ruta = Path.Combine(mEnv.ContentRootPath, "trazas");
            if (!Directory.Exists(ruta))
            {
                Directory.CreateDirectory(ruta);
            }
            ruta += "\\traza_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            return ruta;
        }
    }

    public static class GlobalAsaxExtensions
    {
        public static IApplicationBuilder UseGnossMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GnossMiddleware>();
        }
    }

}

