using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.Usuarios;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.Trazas;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Util;
using Es.Riam.Web.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using static Es.Riam.Web.Util.UtilCookies;

namespace Gnoss.Web.Autocomplete
{
    public class ControllerBase : Controller
    {
        protected EntityContext mEntityContext;
        protected LoggingService mLoggingService;
        protected VirtuosoAD mVirtuosoAD;
        protected ConfigService mConfigService;
        protected RedisCacheWrapper mRedisCacheWrapper;
        protected GnossCache mGnossCache;
        protected IHttpContextAccessor mHttpContextAccessor;
        protected UtilWeb mUtilWeb;
        protected IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;
        private static object BLOQUEO_COMPROBACION_TRAZA = new object();
        private static DateTime HORA_COMPROBACION_TRAZA;

        public ControllerBase(LoggingService loggingService, ConfigService configService, EntityContext entityContext, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHttpContextAccessor httpContextAccessor, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            mLoggingService = loggingService;
            mVirtuosoAD = virtuosoAD;
            mConfigService = configService;
            mEntityContext = entityContext;
            mRedisCacheWrapper = redisCacheWrapper;
            mGnossCache = gnossCache;
            mHttpContextAccessor = httpContextAccessor;
            mUtilWeb = new UtilWeb(mHttpContextAccessor);
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
        }

        #region Métodos de trazas
        [NonAction]
        private void IniciarTraza()
        {
            if (DateTime.Now > HORA_COMPROBACION_TRAZA)
            {
                lock (BLOQUEO_COMPROBACION_TRAZA)
                {
                    if (DateTime.Now > HORA_COMPROBACION_TRAZA)
                    {
                        HORA_COMPROBACION_TRAZA = DateTime.Now.AddSeconds(15);
                        TrazasCL trazasCL = new TrazasCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
                        string tiempoTrazaResultados = trazasCL.ObtenerTrazaEnCache("autocomplete");

                        if (!string.IsNullOrEmpty(tiempoTrazaResultados))
                        {
                            int valor = 0;
                            int.TryParse(tiempoTrazaResultados, out valor);
                            LoggingService.TrazaHabilitada = true;
                            LoggingService.TiempoMinPeticion = valor; //Para sacar los segundos
                        }
                        else
                        {
                            LoggingService.TrazaHabilitada = false;
                            LoggingService.TiempoMinPeticion = 0;
                        }
                    }
                }
            }
        }
        #endregion

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            IniciarTraza();
#if !DEBUG
            if (mConfigService.PeticionHttps())
            {


                Guid identidadID = Guid.Parse(Request.Form["identidad"]);

                if (!identidadID.Equals(UsuarioAD.Invitado))
                {
                    try
                    {
                        Dictionary<string, string> cookie = UtilCookies.FromLegacyCookieString(Request.Cookies["_UsuarioActual"], mEntityContext);
                        if (cookie != null && cookie.Count != 0)
                        {

                            Guid usuarioID = new Guid(cookie["usuarioID"]);
                            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Guid usuarioIDDeBD = identidadCN.ObtenerUsuarioIDConIdentidadID(identidadID);
                            if (!usuarioIDDeBD.Equals(usuarioID))
                            {
                                filterContext.Result = new UnauthorizedResult();
                            }
                        }
                        else
                        {
                            string identidadIDCadena = Request.Headers["Authorization"];
                            if (string.IsNullOrEmpty(identidadIDCadena))
                            {
                                filterContext.Result = new UnauthorizedResult();
                            }
                            else
                            {
                                if(!identidadID.Equals(Guid.Parse(identidadIDCadena.Split("bearer ")[1])))
                                {
                                    filterContext.Result = new UnauthorizedResult();
                                }
                            }
                        }
                    }
                    catch (InvalidCypherTextException)
                    {
                        if (mHttpContextAccessor.HttpContext.Request.Cookies.ContainsKey("_UsuarioActual"))
                        {
                            Response.Cookies.Append("_UsuarioActual", Request.Cookies["_UsuarioActual"], new CookieOptions { Expires = new DateTime(2000, 1, 1) });
                        }
                        filterContext.Result = new UnauthorizedResult();
                    }
                    catch (Exception)
                    {
                        filterContext.Result = new UnauthorizedResult();
                    }

                }
            }
#endif
        }
    }
}
