using Es.Riam.Gnoss.AD.Amigos.Model;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModel.Models.Documentacion;
using Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS;
using Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Facetado;
using Es.Riam.Gnoss.AD.Facetado.Model;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.Amigos;
using Es.Riam.Gnoss.CL.Facetado;
using Es.Riam.Gnoss.CL.Identidad;
using Es.Riam.Gnoss.CL.ParametrosAplicacion;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.Elementos.Facetado;
using Es.Riam.Gnoss.Elementos.Identidad;
using Es.Riam.Gnoss.Logica.Documentacion;
using Es.Riam.Gnoss.Logica.Facetado;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Logica.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.Usuarios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.UtilServiciosWeb;
using Es.Riam.Gnoss.Web.Controles.Documentacion;
using Es.Riam.Gnoss.Web.MVC.Models.Administracion;
using Es.Riam.Semantica.OWL;
using Es.Riam.Util;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.AspNetCore.Cors;
using System.Net;
using Es.Riam.AbstractsOpen;
using System.IO;
using Gnoss.Web.Autocomplete;
using Microsoft.AspNetCore.Mvc;
using Es.Riam.Gnoss.AD.EntityModel.Models.UsuarioDS;
using Es.Riam.Gnoss.AD.Usuarios.Model;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Es.Riam.Interfaces.InterfacesOpen;
using Microsoft.Extensions.Logging;
using Serilog.Core;

namespace Gnoss.Web.AutoComplete
{
    [ApiController]
    [Route("[controller]")]
    [EnableCors("_myAllowSpecificOrigins")]

    public class AutoCompletarController : Autocomplete.ControllerBase
    {
        private EntityContext mEntityContext;
        private LoggingService mLoggingService;
        private ConfigService mConfigService;
        private RedisCacheWrapper mRedisCacheWrapper;
        private VirtuosoAD mVirtuosoAD;
        private IHttpContextAccessor mHttpContextAccessor;
        private UtilServicios mUtilServicios;
        private UtilServiciosFacetas mUtilServiciosFacetas;
        private GnossCache mGnossCache;
        private EntityContextBASE mEntityContextBASE;
        private IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;
        private IAvailableServices mAvailableServices;
        private static List<string> PropiedadesOntologiasBasicas = new List<string>() { "rdf:type" };
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        public AutoCompletarController(EntityContext entityContext, LoggingService loggingService, ConfigService configService, RedisCacheWrapper redisCacheWrapper, VirtuosoAD virtuosoAD, IHttpContextAccessor httpContextAccessor, GnossCache gnossCache, EntityContextBASE entityContextBASE, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication, IAvailableServices availableServices, ILogger<AutoCompletarController> logger, ILoggerFactory loggerFactory)
            : base(loggingService, configService, entityContext, redisCacheWrapper, gnossCache, virtuosoAD, httpContextAccessor, servicesUtilVirtuosoAndReplication,logger,loggerFactory)
        {
            mEntityContext = entityContext;
            mLoggingService = loggingService;
            mConfigService = configService;
            mVirtuosoAD = virtuosoAD;
            mRedisCacheWrapper = redisCacheWrapper;
            mHttpContextAccessor = httpContextAccessor;
            mGnossCache = gnossCache;
            mEntityContextBASE = entityContextBASE;
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
            mAvailableServices = availableServices;
            mlogger = logger;
            mLoggerFactory = loggerFactory;
            mUtilServicios = new UtilServicios(loggingService, entityContext, configService, redisCacheWrapper, gnossCache, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<UtilServicios>(), mLoggerFactory);
            mUtilServiciosFacetas = new UtilServiciosFacetas(loggingService, entityContext, configService, redisCacheWrapper, virtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<UtilServiciosFacetas>(), mLoggerFactory);
        }

        #region Miembros

        private static string mUrlIntragnoss = null;

        private List<string> mListaItemsExtra = null;

        private List<string> mPropiedadesRango = null;

        private List<string> mPropiedadesFecha = null;

        private Dictionary<string, List<string>> mInformacionOntologias = null;

        private DataWrapperFacetas mTConfiguracionOntologia = null;
        /// <summary>
        /// Gestor de facetas
        /// </summary>
        private GestionFacetas mGestorFacetas;

        /// <summary>
        /// Proyecto seleccionado
        /// </summary>
        private Guid mProyectoID;

        /// <summary>
        /// OrganizacionID seleccionado
        /// </summary>
        private Guid mOrganizacionID;

        /// <summary>
        /// DataSet con la tabla OrganizacionClase completa
        /// </summary>
        private static DataWrapperOrganizacion mOrganizacionDW;

        private Dictionary<string, string> mParametroProyecto = null;

        private static Guid mValorInvalidarCacheLocal = Guid.Empty;

        #endregion

        #region Metodos Web

        [HttpPost]
        [Route("AutoCompletarFacetas")]
        public IActionResult AutoCompletarFacetas([FromForm] string orden, [FromForm] string nombreFaceta, [FromForm] string proyecto, [FromForm] string bool_esMyGnoss, [FromForm] string bool_estaEnProyecto, [FromForm] string bool_esUsuarioInvitado, [FromForm] string identidad, [FromForm] string parametros, [FromForm] string q, [FromForm] string lista, [FromForm] string tipo, [FromForm] string perfil, [FromForm] string organizacion, [FromForm] string filtrosContexto, [FromForm] string languageCode)
        {
            string resultados = "";
            try
            {
                if (tipo == null)
                {
                    tipo = "";
                }
                if (organizacion == null)
                {
                    organizacion = "";
                }

                mProyectoID = new Guid(proyecto);
                //mOrganizacionID = new Guid(organizacion);
                bool esMyGnoss = bool.Parse(bool_esMyGnoss);
                bool estaEnProyecto = bool.Parse(bool_estaEnProyecto);
                bool esUsuarioInvitado = bool.Parse(bool_esUsuarioInvitado);
                parametros = WebUtility.UrlDecode(parametros);
                filtrosContexto = WebUtility.UrlDecode(filtrosContexto);
                string autoCompletar = q;

                if (tipo == "MyGNOSS" + FacetadoAD.BUSQUEDA_AVANZADA)
                {
                    tipo = FacetadoAD.BUSQUEDA_AVANZADA;
                }
                else if (tipo == FacetadoAD.BUSQUEDA_CONTACTOS)
                {
                    nombreFaceta = "gnoss:hasnombrecompleto";
                }
                else if (tipo == FacetadoAD.BUSQUEDA_MENSAJES)
                {
                    UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<UsuarioCN>(), mLoggerFactory);
                    Guid? usuarioID = usuarioCN.ObtenerUsuarioIDPorIDPerfil(new Guid(perfil));
                    usuarioCN.Dispose();
                    if (!usuarioID.HasValue)
                    {
                        usuarioID = new Guid(organizacion);
                    }
                    proyecto = usuarioID.Value.ToString();
                }

                Dictionary<string, List<string>> listaFiltros = new Dictionary<string, List<string>>();
                List<string> listaFiltrosExtra = new List<string>();
                if (parametros != null)
                {
                    listaFiltros = ExtraerParametros(parametros);
                }

                if (!listaFiltros.ContainsKey("autocompletar"))
                {
                    listaFiltros.Add("autocompletar", new List<string>());
                }
                listaFiltros["autocompletar"].Add(autoCompletar);

                FacetadoCL facetadoCL = null;

                if (listaFiltros.ContainsKey("rdf:type") && (listaFiltros["rdf:type"].Contains("Mensaje") || listaFiltros["rdf:type"].Contains("Comentario") || listaFiltros["rdf:type"].Contains("Invitacion")))
                {
                    facetadoCL = new FacetadoCL("acidHome", "", UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoCL>(), mLoggerFactory);
                }
                else
                {
                    facetadoCL = new FacetadoCL(UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoCL>(), mLoggerFactory);
                }

                List<string> listaTagsAñadidos = new List<string>();

                if (!string.IsNullOrEmpty(lista))
                {
                    listaTagsAñadidos = lista.Split(',').Where(item => !item.Equals(autoCompletar)).Select(item => item.Trim()).ToList();

                    if (listaFiltros.ContainsKey("autocompletar") && nombreFaceta.Equals("sioc_t:Tag"))
                    {
                        facetadoCL.FacetadoCN.FacetadoAD.ObtenerContadorDeFaceta = false;
                    }
                }

                if (GestorFacetas != null)
                {
                    // Cargo el facetaDS para que pueda identificar las facetas con texto invariable. 
                    facetadoCL.FacetaDW = GestorFacetas.FacetasDW;
                }

                //Obtenemos las facetas excluyentes de esta busqueda...
                facetadoCL.DiccionarioFacetasExcluyentes = ObtenerDiccionarioFacetasExcluyentes();


                if (tipo != FacetadoAD.BUSQUEDA_CONTACTOS)
                {
                    CargarListaItemsBusquedaExtra(tipo, parametros, organizacion, proyecto);
                    facetadoCL.ListaItemsBusquedaExtra = ListaItemsBusquedaExtra;

                    CargarDicInformacionOntologias(tipo, organizacion, proyecto);
                    facetadoCL.InformacionOntologias = InformacionOntologias;
                }

                if (!nombreFaceta.Equals("sioc_t:Tag") && tipo != FacetadoAD.BUSQUEDA_CONTACTOS)
                {
                    CargarPropiedadesRango(GestorFacetas);
                    facetadoCL.PropiedadesRango = PropiedadesRango;
                    CargarPropiedadesFecha(GestorFacetas);
                    facetadoCL.PropiedadesFecha = PropiedadesFecha;
                }

                FacetadoDS facetadoDS = new FacetadoDS();

                bool esBusquedaPorSearch = false;

                List<string> formulariosSemanticos = null;

                if (!tipo.Equals(FacetadoAD.BUSQUEDA_PERSONASYORG) && !tipo.Equals(FacetadoAD.BUSQUEDA_RECURSOS) && !tipo.Equals(FacetadoAD.BUSQUEDA_PERSONA) && !tipo.Equals(FacetadoAD.BUSQUEDA_PREGUNTAS) && !tipo.Equals(FacetadoAD.BUSQUEDA_DEBATES) && !tipo.Equals(FacetadoAD.BUSQUEDA_ORGANIZACION) && !tipo.Equals(FacetadoAD.BUSQUEDA_CONTACTOS))
                {
                    FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
                    formulariosSemanticos = facetaCL.ObtenerPredicadosSemanticos(ProyectoAD.MetaOrganizacion, mProyectoID);
                }
                else
                {
                    formulariosSemanticos = new List<string>();
                }

                string grafo = proyecto;
                bool recortarElementos = false;
                string extraIdioma = null;

                if (nombreFaceta == "search")
                {
                    esBusquedaPorSearch = true;

                    List<string> listatipo = new List<string>();

                    if (tipo == FacetadoAD.BUSQUEDA_PERSONASYORG)
                    {
                        if ((!estaEnProyecto) && (!esMyGnoss))
                        {
                            //El usuario invitado no puede buscar personas en las comunidades
                            return Ok("");
                        }
                        listatipo.Add(FacetadoAD.BUSQUEDA_ORGANIZACION);
                        listatipo.Add(FacetadoAD.BUSQUEDA_PERSONA);
                    }
                    else if ((tipo == FacetadoAD.BUSQUEDA_AVANZADA) || (tipo == FacetadoAD.BUSQUEDA_RECURSOS))
                    {
                        //ObtenerOntologias
                        FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
                        DataWrapperFacetas tConfiguracion = new DataWrapperFacetas();
                        tConfiguracion.ListaOntologiaProyecto = facetaCL.ObtenerOntologiasProyecto(Guid.Empty, new Guid(proyecto)/*, "es"*/);
                        foreach (Es.Riam.Gnoss.AD.EntityModel.Models.Faceta.OntologiaProyecto myrow in tConfiguracion.ListaOntologiaProyecto)
                        {
                            listaFiltrosExtra.Add(myrow.OntologiaProyecto1);
                        }

                        if (tipo == FacetadoAD.BUSQUEDA_RECURSOS)
                        {
                            listatipo.Add(tipo);
                        }
                    }
                    else if (tipo == FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS)
                    {
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PUBLICADO);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPARTIDO);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PREGUNTA);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_DEBATE);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_FACTORDAFO);
                        listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_ENCUESTA);

                        if (proyecto.Equals(ProyectoAD.MetaProyecto.ToString()))
                        {
                            if (organizacion != "" && !organizacion.Equals(ProyectoAD.MetaOrganizacion.ToString()))
                            {
                                grafo = "contribuciones/" + organizacion;
                            }
                            else
                            {
                                grafo = "contribuciones/" + perfil;
                            }
                        }
                        else
                        {
                            grafo = "contribuciones/" + grafo;
                        }
                    }
                    else if (tipo == FacetadoAD.BUSQUEDA_RECURSOS_PERFIL)
                    {
                        grafo = "perfil/" + perfil;

                        if (!string.IsNullOrEmpty(organizacion))
                        {
                            IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
                            grafo = "perfil/" + identCN.ObtenerPerfilPersonalDePerfil(new Guid(perfil)).ToString().ToLower();
                        }
                    }
                    else if (!string.IsNullOrEmpty(tipo))
                    {
                        Guid pestanyaID;
                        if (Guid.TryParse(tipo, out pestanyaID))
                        {
                            //Es una pestaña, obtengo los filtros de la pestaña
                            if (!ObtenerFiltrosPestanya(pestanyaID, listaFiltros))
                            {
                                // La pestaña no es de tipo busqueda, añado todos los tipos a la lista de tipos
                                listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS);
                                listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PREGUNTA);
                                listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_DEBATE);
                                listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_FACTORDAFO);
                                listatipo.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_ENCUESTA);

                                CargarListaItemsBusquedaExtra("Meta", parametros, organizacion, proyecto);
                                listatipo.AddRange(ListaItemsBusquedaExtra);

                                if (estaEnProyecto)
                                {
                                    listatipo.Add(FacetadoAD.BUSQUEDA_ORGANIZACION);
                                    listatipo.Add(FacetadoAD.BUSQUEDA_PERSONA);
                                    listatipo.Add(FacetadoAD.BUSQUEDA_GRUPO);
                                }
                            }
                        }
                        else
                        {
                            listatipo.Add(tipo);
                        }
                    }

                    if (listatipo.Count > 0)
                    {
                        if (!listaFiltros.ContainsKey("rdf:type"))
                        {
                            listaFiltros.Add("rdf:type", listatipo);
                        }
                        else
                        {
                            listaFiltros["rdf:type"].AddRange(listatipo);
                        }
                    }

                    facetadoCL.ObtenerAutocompletar(grafo, facetadoDS, listaFiltros, listaFiltrosExtra, esMyGnoss, estaEnProyecto, esUsuarioInvitado, identidad, 0, 11 + listaTagsAñadidos.Count, formulariosSemanticos, filtrosContexto, mAvailableServices);
                    nombreFaceta = "Autocompletar";
                }
                else
                {
                    if (tipo == FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS)
                    {
                        if (organizacion != "" && !organizacion.Equals(ProyectoAD.MetaOrganizacion.ToString()))
                        {
                            grafo = "contribuciones/" + organizacion;
                        }
                        else
                        {
                            grafo = "contribuciones/" + perfil;
                        }
                    }
                    else if (tipo == FacetadoAD.BUSQUEDA_RECURSOS_PERFIL)
                    {
                        grafo = "perfil/" + perfil;
                    }
                    else if (tipo == FacetadoAD.BUSQUEDA_CONTACTOS)
                    {
                        grafo = "contactos";
                    }


                    ProyectoCL proyCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCL>(), mLoggerFactory);
                    TipoProyecto tipoProy = proyCL.ObtenerTipoProyecto(ProyectoID);

                    //TODO: No borrar, hay que recuperarlos cuando se cargen los tags con peso en DBLP

                    /*if (proyecto.ToLower() == "bab08a33-4041-4450-aef8-aafb6ea7d2a4" && tipo != "" && (nombreFaceta == "sioc_t:Tag" || nombreFaceta == "dc:creator@@@foaf:name")) //DBLP con ciertas facteas  q.Length<4 &&
                    {
                        facetadoCL.ObtenerFacetaEspecialDBLP(grafo, facetadoDS, nombreFaceta, listaFiltros, listaFiltrosExtra, esMyGnoss, estaEnProyecto, esUsuarioInvitado, identidad, 0, 11, formulariosSemanticos, filtrosContexto);
                        recortarElementos = true;
                    }
                    else if (proyecto.ToLower() == "bab08a33-4041-4450-aef8-aafb6ea7d2a4" && tipo != "" && (nombreFaceta == "swrc:journal@@@dc:title" || nombreFaceta == "dc:partOf")) //nombreFaceta == "sioc_t:Tag" || nombreFaceta == "dc:creator@@@foaf:name" ||
                    {
                        facetadoCL.ObtenerFacetaEspecialDBLPJournalPartOF(grafo, facetadoDS, nombreFaceta, listaFiltros, listaFiltrosExtra, esMyGnoss, estaEnProyecto, esUsuarioInvitado, identidad, orden, 0, 11, formulariosSemanticos, filtrosContexto);
                        recortarElementos = true;
                    }
                    else
                    {*/
                    if (proyecto.ToLower() == "bab08a33-4041-4450-aef8-aafb6ea7d2a4") //DBLP
                    {
                        facetadoCL.ObtenerFacetaSinOrdenDBLP(grafo, facetadoDS, nombreFaceta, listaFiltros, listaFiltrosExtra, esMyGnoss, estaEnProyecto, esUsuarioInvitado, identidad, TipoDisenio.ListaOrdCantidad, 0, 11, formulariosSemanticos, filtrosContexto, tipoProy, false, null, true, false, mAvailableServices);
                    }
                    else
                    {
                        if (GestorFacetas.ListaFacetasPorClave.ContainsKey(nombreFaceta) && (GestorFacetas.ListaFacetasPorClave[nombreFaceta].AlgoritmoTransformacion == TiposAlgoritmoTransformacion.MultiIdioma || (GestorFacetas.ListaFacetasPorClave[nombreFaceta].AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemantico && GestorFacetas.ListaFacetasPorClave[nombreFaceta].FilaElementoEntity is Es.Riam.Gnoss.AD.EntityModel.Models.Faceta.FacetaFiltroProyecto)))
                        {

                            filtrosContexto = ObtenerExtraContextoTesauroSemantico(GestorFacetas.ListaFacetasPorClave[nombreFaceta], listaFiltros, languageCode) + filtrosContexto;

                            if (GestorFacetas.ListaFacetasPorClave[nombreFaceta].MultiIdioma)
                            {
                                extraIdioma = "|||idioma|||@" + languageCode;
                            }
                        }
                        facetadoCL.FacetadoCN.FacetadoAD.ListaItemsBusquedaExtra = mUtilServiciosFacetas.ObtenerListaItemsBusquedaExtra(listaFiltros, TipoBusqueda.BusquedaAvanzada, mOrganizacionID, mProyectoID);
                        Guid pPestanyaID = new Guid();
                        if (Guid.TryParse(tipo, out pPestanyaID))
                        {
                            facetadoCL.FacetadoCN.ObtenerFaceta(mProyectoID.ToString(), facetadoDS, nombreFaceta, listaFiltros, listaFiltrosExtra, esMyGnoss, estaEnProyecto, esUsuarioInvitado, identidad, TipoDisenio.ListaOrdCantidad, 0, 10, ListaItemsBusquedaExtra, true, false, false, pPestanyaID);
                        }
                        else
                        {
                            facetadoCL.FacetadoCN.ObtenerFaceta(mProyectoID.ToString(), facetadoDS, nombreFaceta, listaFiltros, listaFiltrosExtra, esMyGnoss, estaEnProyecto, esUsuarioInvitado, identidad, TipoDisenio.ListaOrdCantidad, 0, 10, ListaItemsBusquedaExtra, true, false);
                        }
                    }

                    if (proyecto.ToLower() == "bab08a33-4041-4450-aef8-aafb6ea7d2a4") //DBLP
                    {
                        //No debe salir la cantidad:
                        esBusquedaPorSearch = true;
                    }
                    /*}*/
                }

                string parametrosConTub = parametros + "|";
                int count = 0;

                List<string> listaComprobacion = new List<string>();
                foreach (DataRow fila in facetadoDS.Tables[nombreFaceta].Rows)
                {
                    string elemento = (string)fila[0];

                    if (recortarElementos)
                    {
                        elemento = elemento.Substring(8);
                    }

                    if (!parametrosConTub.Contains(nombreFaceta + "=" + elemento + "|") && !listaTagsAñadidos.Contains(elemento) && !listaComprobacion.Contains(elemento.ToLower().Trim()))
                    {
                        listaComprobacion.Add(elemento.ToLower().Trim());
                        resultados += elemento;

                        if (!esBusquedaPorSearch && fila.ItemArray.Length > 1)
                        {
                            resultados += " (" + (string)fila[1] + ")" + extraIdioma;
                        }
                        resultados += Environment.NewLine;
                        count++;
                    }

                    if (count == 10)
                    {
                        break;
                    }
                }
                listaComprobacion.Clear();
                lista = null;
            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog(ex.Message + "\r\nPila: " + ex.StackTrace, "error");
            }

            return Ok(resultados);
        }
        [NonAction]
        private bool ObtenerFiltrosPestanya(Guid pPestanyaID, Dictionary<string, List<string>> pListaFiltros)
        {
            bool hayPestanyaBusqueda = false;
            ProyectoCL proyCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCL>(), mLoggerFactory);
            DataWrapperProyecto dataWrapperProyecto = proyCL.ObtenerPestanyasProyecto(mProyectoID);

            ProyectoPestanyaBusqueda filaPestanyaBusqueda = dataWrapperProyecto.ListaProyectoPestanyaBusqueda.FirstOrDefault(proy => proy.PestanyaID.Equals(pPestanyaID));
            ProyectoPestanyaMenu filaPestanya = dataWrapperProyecto.ListaProyectoPestanyaMenu.FirstOrDefault(proy => proy.PestanyaID.Equals(pPestanyaID));

            if (filaPestanya != null && filaPestanya.TipoPestanya.Equals((short)TipoPestanyaMenu.Recursos))
            {
                if (!pListaFiltros.ContainsKey("rdf:type"))
                {
                    pListaFiltros.Add("rdf:type", new List<string>());
                }
                pListaFiltros["rdf:type"].Add(FacetadoAD.BUSQUEDA_RECURSOS);

                //ObtenerOntologias
                FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
                DataWrapperFacetas tConfiguracion = new DataWrapperFacetas();
                tConfiguracion.ListaOntologiaProyecto = facetaCL.ObtenerOntologiasProyecto(Guid.Empty, mProyectoID);

                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.Faceta.OntologiaProyecto myrow in tConfiguracion.ListaOntologiaProyecto)
                {
                    if (!myrow.OntologiaProyecto1.StartsWith("@"))
                    {
                        pListaFiltros["rdf:type"].Add(myrow.OntologiaProyecto1);
                    }
                }

                hayPestanyaBusqueda = true;
            }

            if (filaPestanyaBusqueda != null)
            {
                if (!string.IsNullOrEmpty(filaPestanyaBusqueda.CampoFiltro))
                {
                    hayPestanyaBusqueda = true;
                    string[] filtros = filaPestanyaBusqueda.CampoFiltro.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string filtro in filtros)
                    {
                        int indiceIgual = filtro.IndexOf('=');
                        if (indiceIgual > 0)
                        {
                            string claveFiltro = filtro.Substring(0, indiceIgual);
                            string valorFiltro = filtro.Substring(indiceIgual + 1);

                            if (!pListaFiltros.ContainsKey(claveFiltro))
                            {
                                pListaFiltros.Add(claveFiltro, new List<string>());
                            }
                            pListaFiltros[claveFiltro].Add(valorFiltro);
                        }
                    }
                }
            }
            return hayPestanyaBusqueda;
        }
        [NonAction]
        private Dictionary<string, bool> ObtenerDiccionarioFacetasExcluyentes()
        {
            Dictionary<string, bool> dic = new Dictionary<string, bool>();
            foreach (Faceta fac in GestorFacetas.ListaFacetas)
            {
                if (!dic.ContainsKey(fac.ClaveFaceta))
                {
                    dic.Add(fac.ClaveFaceta, fac.Excluyente);
                }
            }

            return dic;
        }

        [NonAction]
        private string ObtenerExtraContextoTesauroSemantico(Faceta pFaceta, Dictionary<string, List<string>> pListaFiltros, string pIdioma)
        {
            string extraContexto = "";
            if (pFaceta != null && pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemantico)
            {
                string[] arrayTesSem = ObtenerDatosFacetaTesSem(pFaceta.ClaveFaceta);

                extraContexto = "AgreAFiltro=" + pFaceta.ClaveFaceta + ",";

                string nivelSemantico = null;
                string source = null;
                string idioma = null;
                if (pFaceta.FilaElementoEntity is Es.Riam.Gnoss.AD.EntityModel.Models.Faceta.FacetaFiltroProyecto && !string.IsNullOrEmpty(pFaceta.FiltroProyectoID) && pFaceta.FiltroProyectoID.Contains(";"))
                {
                    nivelSemantico = pFaceta.FiltroProyectoID.Split(';')[0];
                    source = pFaceta.FiltroProyectoID.Split(';')[1];

                    if (nivelSemantico.Contains("[MultiIdioma]"))
                    {
                        idioma = pIdioma;
                        nivelSemantico = nivelSemantico.Replace("[MultiIdioma]", "");
                    }
                }

                if (!string.IsNullOrEmpty(nivelSemantico))
                {
                    if (!nivelSemantico.Contains("-"))
                    {
                        string filtroIdioma = "";

                        if (idioma != null)
                        {
                            filtroIdioma = " AND lang(@o@)='" + idioma + "'";
                        }

                        extraContexto += " @s@ <" + arrayTesSem[5] + "> \"" + source + "\". @s@ <" + arrayTesSem[6] + "> ?nivelTesSem. FILTER(?nivelTesSem=" + nivelSemantico + "" + filtroIdioma + ") ";
                    }
                    else
                    {
                        int inicioRangoNivel = int.Parse(nivelSemantico.Split('-')[0]);
                        int finRangoNivel = int.Parse(nivelSemantico.Split('-')[1]);

                        extraContexto += "@o@ <" + arrayTesSem[5] + "> \"" + source + "\". {";

                        for (int iInicio = inicioRangoNivel; iInicio <= finRangoNivel; iInicio++)
                        {
                            extraContexto += "@o@ <" + arrayTesSem[6] + "> ?nivelTesSem. FILTER(?nivelTesSem=" + iInicio + ")";

                            if (iInicio < finRangoNivel)
                            {
                                extraContexto += " } UNION { ";
                            }
                            else
                            {
                                extraContexto += " } ";
                            }
                        }
                    }
                }
                else if (!pListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                {
                    extraContexto += "{?sujCollTaxo <" + arrayTesSem[1] + "> ";
                }
                else
                {
                    string catPadre = pListaFiltros[pFaceta.ClaveFaceta][pListaFiltros[pFaceta.ClaveFaceta].Count - 1];
                    extraContexto += "{<" + catPadre + "> <" + arrayTesSem[4] + "> ";
                }

                if (!extraContexto.EndsWith("|"))
                {
                    extraContexto += " |";
                }
            }
            else if (pFaceta != null && pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.MultiIdioma)
            {
                extraContexto = "idioma=" + pIdioma + "|";
            }

            return extraContexto;
        }

        /// <summary>
        /// Obtiene los datos de la faceta de tesauro semántica.
        /// </summary>
        /// <param name="pFaceta">Faceta</param>
        /// <returns>Array con la configuración: Grafo, Propiedad de unión de colección con categorías raíz, Propiedad con el Identificador de las categorías, Propiedad con el Nombre de las categorías, Propiedad que relaciona categorías padres con hijas</returns>
        [NonAction]
        public string[] ObtenerDatosFacetaTesSem(string pFaceta)
        {
            //TODO JAVIER: Leer de XML de ontología y poner en cada caso las propiedades y grafo correctos.

            string[] array = new string[7];
            array[0] = "taxonomy.owl";
            array[1] = "http://www.w3.org/2008/05/skos#member";
            array[2] = "http://purl.org/dc/elements/1.1/identifier";
            array[3] = "http://www.w3.org/2008/05/skos#prefLabel";
            array[4] = "http://www.w3.org/2008/05/skos#narrower";
            array[5] = "http://purl.org/dc/elements/1.1/source";
            array[6] = "http://www.w3.org/2008/05/skos#symbol";

            return array;
        }


        [HttpPost]
        [Route("AutoCompletarGrafoDocSem")]
        public IActionResult AutoCompletarGrafoDocSem([FromForm] string q, [FromForm] string pFaceta, [FromForm] string pTipoResultado)
        {
            string resultados = "";
            try
            {
                FacetadoCL facetadoCL = new FacetadoCL(UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoCL>(), mLoggerFactory);
                //Obtenemos las facetas excluyentes de esta busqueda...
                //facetadoCL.DiccionarioFacetasExcluyentes = ObtenerDiccionarioFacetasExcluyentes();

                FacetadoDS facetadoDS = new FacetadoDS();

                facetadoCL.ObtieneDatosAutocompletar(pFaceta, q, facetadoDS, Guid.Empty, mAvailableServices);
                foreach (DataRow fila in facetadoDS.Tables[0].Rows)
                {
                    string valor = fila[0].ToString();
                    if (pTipoResultado == "0")
                    {
                        resultados += valor;
                    }
                    else if (pTipoResultado == "1")
                    {
                        resultados += UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculas(valor);
                    }
                    else if (pTipoResultado == "2")
                    {
                        resultados += UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculasExceptoArticulos(valor);
                    }
                    else if (pTipoResultado == "3")
                    {
                        if (!string.IsNullOrEmpty(valor))
                        {
                            resultados += UtilCadenas.ConvertirPrimeraLetraDeFraseAMayúsculas(valor);
                        }
                    }
                    else
                    {
                        resultados += valor + "|||formSemGrafoAutocompletar|||" + pTipoResultado;
                    }

                    resultados += Environment.NewLine;
                }

                facetadoDS.Dispose();
                facetadoCL.Dispose();
            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog(ex.Message + "\r\nPila: " + ex.StackTrace, "error");
            }
            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarSeleccEntDocSem")]
        public IActionResult AutoCompletarSeleccEntDocSem([FromForm] string q, [FromForm] string pGrafo, [FromForm] string pEntContenedora, [FromForm] string pPropiedad, [FromForm] string pTipoEntidadSolicitada, [FromForm] string pPropSolicitadas, [FromForm] string pControlID, [FromForm] string pExtraWhere, [FromForm] string pIdioma, [FromForm] string pProyectoID, [FromForm] string identidad)
        {
            string resultados = "";
            string entidadID = "";

            try
            {
                if (pExtraWhere == null)
                {
                    pExtraWhere = "";
                }
                if (pIdioma == null)
                {
                    pIdioma = "";
                }

                if (pGrafo.StartsWith("\""))
                {
                    q = q.Substring(1, q.Length - 2);
                    pGrafo = pGrafo.Substring(1, pGrafo.Length - 2);
                    pEntContenedora = pEntContenedora.Substring(1, pEntContenedora.Length - 2);
                    pPropiedad = pPropiedad.Substring(1, pPropiedad.Length - 2);
                    pTipoEntidadSolicitada = pTipoEntidadSolicitada.Substring(1, pTipoEntidadSolicitada.Length - 2);
                    pPropSolicitadas = pPropSolicitadas.Substring(1, pPropSolicitadas.Length - 2);
                    pControlID = pControlID.Substring(1, pControlID.Length - 2);
                    pExtraWhere = pExtraWhere.Substring(1, pExtraWhere.Length - 2);
                    pIdioma = pIdioma.Substring(1, pIdioma.Length - 2);
                }

                FacetadoCN facetadoCN = new FacetadoCN(UrlIntragnoss, pGrafo, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoCN>(), mLoggerFactory);
                FacetadoDS facetadoDS = null;
                if (Guid.TryParse(q, out Guid docID))
                {
                    DocumentacionCN docCN = new DocumentacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<DocumentacionCN>(), mLoggerFactory);
                    string titulo = docCN.ObtenerTituloDocumentoPorID(docID);
                    if (!string.IsNullOrEmpty(titulo))
                    {
                        string query = $"SELECT DISTINCT ?s from <{UrlIntragnoss}{pGrafo}> WHERE {{ ?s ?p ?o. ?documento <http://gnoss/hasEntidad> ?s. FILTER (?documento = <{UrlIntragnoss}{docID}>)}}";
                        if (!UrlIntragnoss.EndsWith('/'))
                        {
                            query = $"SELECT DISTINCT ?s from <{UrlIntragnoss}{pGrafo}> WHERE {{ ?s ?p ?o. ?documento <http://gnoss/hasEntidad> ?s. FILTER (?documento = <{UrlIntragnoss}/{docID}>)}}";
                        }
                        string nombreTabla = "IdRecursoLargo";
                        facetadoDS = facetadoCN.LeerDeVirtuoso(query, nombreTabla, pGrafo);
                        if (facetadoDS.Tables[nombreTabla] != null)
                        {
                            if (facetadoDS.Tables[nombreTabla].Rows.Count > 0)
                            {
                                DataRow fila = facetadoDS.Tables[nombreTabla].Rows[0];
                                entidadID = (string)fila[0];

                                resultados = $"{titulo}|||formSem|||{entidadID}|||{pControlID}";
                            }
                        }
                    }

                    facetadoCN.Dispose();

                    return Ok(resultados);
                }

                pExtraWhere = pExtraWhere.Replace("[--C]", "<").Replace("[C--]", ">");
                q = "%" + q;

                string[] parametrosWhere = pExtraWhere.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

                if (pExtraWhere.Contains("|||"))
                {
                    pExtraWhere = pExtraWhere.Substring(0, pExtraWhere.IndexOf("|||"));
                }

                //Descartamos los borradores:
                pExtraWhere += $" OPTIONAL {{?s <{GestionOWL.PropBorradorGnossRdf}> ?borrador}} FILTER(!BOUND(?borrador))";

                #region configuración extra

                //Limitamos el número de resultados a 50 porque sino falla el servicio autocompletar.
                //http://stackoverflow.com/questions/1151987/can-i-set-an-unlimited-length-for-maxjsonlength-in-web-config
                int maximoLimite = 50;
                string sepPrin = " ";
                string sepFin = null;
                string sepEntreProps = " ";

                foreach (string parametroWhere in parametrosWhere)
                {
                    if (parametroWhere.StartsWith("SeparadorPropPrinc"))
                    {
                        sepPrin = parametroWhere.Substring(parametroWhere.IndexOf("=") + 1);
                    }
                    else if (parametroWhere.StartsWith("SeparadorFinal"))
                    {
                        sepFin = parametroWhere.Substring(parametroWhere.IndexOf("=") + 1);
                    }
                    else if (parametroWhere.StartsWith("SeparadorEntreProps"))
                    {
                        sepEntreProps = parametroWhere.Substring(parametroWhere.IndexOf("=") + 1);
                    }
                    else if (parametroWhere.StartsWith("Limite"))
                    {
                        maximoLimite = int.Parse(parametroWhere.Substring(parametroWhere.IndexOf("=") + 1));
                        q += $"|||{parametroWhere}";
                    }
                }

                #endregion

                List<string> propSelec = new List<string>(pPropSolicitadas.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));

                List<string> propsPrimeraConsulta = new List<string>();
                propsPrimeraConsulta.Add(propSelec[0]);

                facetadoDS = facetadoCN.ObtenerRDFXMLSelectorEntidadFormulario(pGrafo, pEntContenedora, pPropiedad, pTipoEntidadSolicitada, propsPrimeraConsulta, q, pExtraWhere, pIdioma, new Guid(identidad), new Guid(pProyectoID));

                #region Propiedades extra a la principal

                FacetadoDS facetadoAuxDS = null;

                if (propSelec.Count > 1)
                {
                    List<string> entidades = new List<string>();

                    foreach (DataRow fila in facetadoDS.Tables[0].Rows)
                    {
                        entidadID = (string)fila[0];
                        entidades.Add(entidadID);
                    }

                    propSelec.RemoveAt(0);

                    facetadoAuxDS = facetadoCN.ObtenerValoresPropiedadesEntidadesConJerarquiaYExternas(pGrafo, entidades, propSelec, false);
                }

                #endregion

                if (facetadoDS.Tables[0].Rows.Count > 0)
                {
                    List<string> listEntResul = new List<string>();

                    for (int i = 0; i < maximoLimite; i++)
                    {
                        if (facetadoDS.Tables[0].Rows.Count > i)
                        {
                            DataRow fila = facetadoDS.Tables[0].Rows[i];

                            entidadID = (string)fila[0];
                            string nombre = (string)fila[2];

                            #region Propiedades extra

                            if (facetadoAuxDS != null)
                            {
                                string extra = "";

                                foreach (string prop in propSelec)
                                {
                                    List<string> valores = FacetadoCN.ObtenerObjetosDataSetSegunPropiedad(facetadoAuxDS, (string)fila[0], prop, pIdioma);

                                    foreach (string valor in valores)
                                    {
                                        extra += valor + sepEntreProps;
                                    }
                                }

                                if (extra != "")
                                {
                                    if (sepEntreProps != null)
                                    {
                                        extra = extra.Substring(0, extra.Length - sepEntreProps.Length);
                                    }
                                    extra = sepPrin + extra + sepFin;
                                }

                                nombre += extra;
                            }

                            #endregion

                            if (resultados != "")
                            {
                                resultados += Environment.NewLine;
                            }

                            resultados += $"{nombre}|||formSem|||{entidadID}|||{pControlID}";
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                facetadoDS.Dispose();
                facetadoCN.Dispose();

                if (facetadoAuxDS != null)
                {
                    facetadoAuxDS.Dispose();
                }
            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog($"{ex.Message}\r\nPila: {ex.StackTrace}", "error");
            }
            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarGrafoDependienteDocSem")]
        public IActionResult AutoCompletarGrafoDependienteDocSem([FromForm] string q, [FromForm] string pGrafo, [FromForm] string pTipoEntDep, [FromForm] string pIDValorPadre, [FromForm] string pControlID)
        {
            string resultados = "";
            try
            {
                if (pGrafo.StartsWith("\""))
                {
                    q = q.Substring(1, q.Length - 2);
                    pGrafo = pGrafo.Substring(1, pGrafo.Length - 2);
                    pTipoEntDep = pTipoEntDep.Substring(1, pTipoEntDep.Length - 2);
                    pIDValorPadre = pIDValorPadre.Substring(1, pIDValorPadre.Length - 2);
                    pControlID = pControlID.Substring(1, pControlID.Length - 2);
                }

                q = "%" + q;

                FacetadoCN facetadoCN = new FacetadoCN(UrlIntragnoss, pGrafo, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoCN>(), mLoggerFactory);
                FacetadoDS facetadoDS = facetadoCN.ObtenerValoresGrafoDependientesFormulario(pGrafo, pTipoEntDep, pIDValorPadre, q);

                if (facetadoDS.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow fila in facetadoDS.Tables[0].Rows)
                    {
                        string entidadID = (string)fila[0];
                        string nombre = (string)fila[1];

                        if (resultados != "")
                        {
                            resultados += Environment.NewLine;
                        }

                        resultados += nombre + "|||formSemGrafoDependiente|||" + entidadID + "|||" + pControlID;
                    }
                }

                facetadoDS.Dispose();
                facetadoCN.Dispose();
            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog(ex.Message + "\r\nPila: " + ex.StackTrace, "error");
            }

            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarEnvioMensajes")]
        public IActionResult AutoCompletarEnvioMensajes([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string identidadOrg, [FromForm] string bool_esGnossOrganiza)
        {
            List<string> listaAnteriores = new List<string>();
            if (!string.IsNullOrWhiteSpace(lista))
            {
                listaAnteriores = lista.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            }

            string[] listaNombres;
            Guid identidadID = new Guid(identidad);
            Guid identidadOrganizacionID = Guid.Empty;

            if (!string.IsNullOrEmpty(identidadOrg))
            {
                // David: Identidad de la organización de la que se deben comprobar los permisos
                identidadOrganizacionID = new Guid(identidadOrg);
            }

            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
            List<string> resultadosPerfil = new List<string>();

            AmigosCL amigosCL = new AmigosCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<AmigosCL>(), mLoggerFactory);
            Guid valorCacheInvalidarLocal = amigosCL.ObtenerCacheAutocompletarInvalidar();

            if (valorCacheInvalidarLocal != Guid.Empty && !mValorInvalidarCacheLocal.Equals(valorCacheInvalidarLocal))
            {
                string fileName = $"{AppDomain.CurrentDomain.SetupInformation.ApplicationBase}/config/versionCacheLocal/{Guid.Empty}.config";
                FileInfo fileInfo = new FileInfo(fileName);
                if (fileInfo.Exists)
                {
                    using (StreamWriter sw = new StreamWriter(fileName, true, System.Text.Encoding.Default))
                    {
                        sw.WriteLine(Environment.NewLine + "Invalidar cache local, Fecha: " + DateTime.Now + Environment.NewLine + Environment.NewLine);
                    }
                }
                mValorInvalidarCacheLocal = valorCacheInvalidarLocal;
            }


            DataWrapperIdentidad idenDW = new DataWrapperIdentidad();
            DataWrapperAmigos amigosDW = new DataWrapperAmigos();
            bool amigosCargados = amigosCL.ObtenerAmigos(identidadID, idenDW, null, null, amigosDW, identidadOrganizacionID == identidadID, false);

            DataWrapperIdentidad idenDSpriv = new DataWrapperIdentidad();
            bool identProyPrivCargados = false;
            if (CargarIdentidadesDeProyectosPrivadosComoAmigos)
            {
                identProyPrivCargados = amigosCL.ObtenerAmigosEIdentidadesEnMisProyectosPrivados(identidadID, idenDSpriv, identidadOrganizacionID == identidadID, false);
            }

            DataWrapperIdentidad idenDWGruposProyectos = identidadCN.ObtenerGruposEnvios(identidadID);

            if (identProyPrivCargados || amigosCargados)
            {
                string busq = UtilCadenas.RemoveAccentsWithRegEx(q);
                List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Identidad> filasMiPefil = idenDW.ListaIdentidad.Where(ident => ident.IdentidadID.Equals(identidadID)).ToList();

                //Comprobar que hay más de una fila en el DS del Perfil
                if (idenDW.ListaPerfil.Count > 0)
                {
                    List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfiles = new List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil>();
                    if (filasMiPefil.Count > 0)
                    {
                        Guid perfilPropioID = filasMiPefil.First().PerfilID;
                        perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && !perfil.PerfilID.Equals(perfilPropioID)).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
                    }
                    else
                    {
                        perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
                    }

                    //bool identidadProfesor = IdentidadCN

                    foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfiles)
                    {
                        string nombrePerfil = perfil.NombrePerfil;
                        if (perfil.OrganizacionID.HasValue && perfil.PersonaID.HasValue)
                        {
                            nombrePerfil = perfil.NombrePerfil + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + perfil.NombreOrganizacion;
                        }
                        else if (perfil.OrganizacionID.HasValue)
                        {
                            nombrePerfil = perfil.NombreOrganizacion;
                        }

                        if (!listaAnteriores.Contains(nombrePerfil.ToLower()))
                        {
                            resultadosPerfil.Add(nombrePerfil);
                        }
                    }
                }
                else
                {
                    listaNombres = identidadCN.ObtenerNombresIdentidadesAmigosPorPrefijo(identidadID, q.Trim(), 10, identidadOrganizacionID != Guid.Empty, identidadOrganizacionID, listaAnteriores);
                    resultadosPerfil.AddRange(listaNombres);
                    listaAnteriores.AddRange(listaNombres);
                }

                //Comprobar que hay más de una fila en el DS del Perfil
                if (idenDSpriv.ListaPerfil.Count > 0)
                {
                    //Obtenemos los perfiles de las personas que pertenecen a proyectos privados.
                    List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfilesPryPriv = new List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil>();
                    if (filasMiPefil.Count > 0)
                    {
                        Guid perfilPropioID = filasMiPefil.First().PerfilID;
                        perfilesPryPriv = idenDSpriv.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && !perfil.PerfilID.Equals(perfilPropioID)).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
                    }
                    else
                    {
                        perfilesPryPriv = idenDSpriv.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
                    }

                    //Obtenemos los perfiles de las personas que pertenecen a proyectos privados.
                    foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfilesPryPriv)
                    {
                        string nombrePerfil = perfil.NombrePerfil;
                        if (perfil.OrganizacionID.HasValue && perfil.PersonaID.HasValue)
                        {
                            nombrePerfil = perfil.NombrePerfil + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + perfil.NombreOrganizacion;
                        }
                        else if (perfil.OrganizacionID.HasValue)
                        {
                            nombrePerfil = perfil.NombreOrganizacion;
                        }

                        if (!listaAnteriores.Contains(nombrePerfil.ToLower()))
                        {
                            resultadosPerfil.Add(nombrePerfil);
                        }
                    }
                }
                else if (CargarIdentidadesDeProyectosPrivadosComoAmigos)
                {
                    string[] listaNombres2 = identidadCN.ObtenerNombresIdentidadesPorPrefijoEnMisProyectosPrivados(identidadID, q.Trim(), 10, listaAnteriores);

                    resultadosPerfil.AddRange(listaNombres2);
                }

                List<GrupoAmigos> gruposAmigos = amigosDW.ListaGrupoAmigos.Where(item => item.Nombre.Contains(busq.Trim())).OrderByDescending(item => item.Nombre).ToList();

                //Comprobar que hay más de una fila en el DS de Grupo Amigos
                foreach (GrupoAmigos grupoAmigos in gruposAmigos)
                {
                    if (!listaAnteriores.Contains(grupoAmigos.Nombre.ToLower()))
                    {
                        resultadosPerfil.Add(grupoAmigos.Nombre);
                    }
                }
            }
            else
            {
                listaNombres = identidadCN.ObtenerNombresIdentidadesAmigosPorPrefijo(identidadID, q.Trim(), 10, identidadOrganizacionID != Guid.Empty, identidadOrganizacionID, listaAnteriores);
                resultadosPerfil.AddRange(listaNombres);
                listaAnteriores.AddRange(listaNombres);

                if (CargarIdentidadesDeProyectosPrivadosComoAmigos)
                {
                    string[] listaNombres2 = identidadCN.ObtenerNombresIdentidadesPorPrefijoEnMisProyectosPrivados(identidadID, q.Trim(), 10, listaAnteriores);

                    resultadosPerfil.AddRange(listaNombres2);
                }
                amigosCL.RefrescarCacheAmigos(identidadID, mEntityContextBASE, mAvailableServices, bool.Parse(bool_esGnossOrganiza));
            }
            listaNombres = resultadosPerfil.Distinct().ToArray();

            // GRUPOS
            List<string> listaGruposBIS = new List<string>();
            List<GrupoIdentidadesEnvio> grupoIdentidades = idenDWGruposProyectos.ListaGrupoIdentidadesEnvio.Where(grupoIdentidadesAutocompletado => UtilCadenas.RemoveAccentsWithRegEx(grupoIdentidadesAutocompletado.NombreBusqueda).ToLower().Contains(UtilCadenas.RemoveAccentsWithRegEx(q).Trim().ToLower())).OrderByDescending(grupoIdentidadesAutocompletado => grupoIdentidadesAutocompletado.NombreBusqueda).ToList();

            foreach (GrupoIdentidadesEnvio fila in grupoIdentidades)
            {
                if (!listaGruposBIS.Contains(fila.NombreBusqueda) && !listaAnteriores.Contains(fila.NombreBusqueda.ToString().ToLower()))
                {
                    listaGruposBIS.Add(fila.NombreBusqueda);
                }
            }
            listaGruposBIS.AddRange(listaNombres);
            listaNombres = listaGruposBIS.Distinct().ToArray();


            string resultados = "";
            resultados = string.Join(Environment.NewLine, listaNombres.Distinct().OrderBy(x => x).Take(10));

            return Ok(resultados);
        }

        [HttpGet]
        [Route("AutoCompletarEnvioMensajesNombre")]
        public IActionResult AutoCompletarEnvioMensajesNombre(string q, string identidad, string proyecto)
        {
            Guid proyectoID = Guid.Empty;
            Guid identidadID = Guid.Empty;

            string resultado = "";
            if (!(Guid.TryParse(identidad, out identidadID) && Guid.TryParse(proyecto, out proyectoID)))
            {
                //Quitamos las comillas
                identidad = identidad.Replace("\"", "");
                proyecto = proyecto.Replace("\"", "");
                q = q.Replace("\"", "");

                if (!(Guid.TryParse(identidad, out identidadID) && Guid.TryParse(proyecto, out proyectoID)))
                {
                    resultado = identidad + " " + proyecto;
                }
            }

            if (Guid.TryParse(identidad, out identidadID) && Guid.TryParse(proyecto, out proyectoID))
            {
                try
                {
                    ServicioAutocompletar contrAuto = new ServicioAutocompletar(mUtilServicios, mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ServicioAutocompletar>(), mLoggerFactory);
                    Dictionary<Guid, string> resultadosFacetas = contrAuto.RealizarConsulta(q, identidadID, proyectoID);

                    foreach (Guid identidadResultadosID in resultadosFacetas.Keys)
                    {
                        string nombre = resultadosFacetas[identidadResultadosID];
                        resultado += nombre + "|" + identidadResultadosID;
                        resultado += Environment.NewLine;
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception ex)
                {
                    mUtilServicios.GuardarLog("Error: " + ex.Message + "\r\nPila: " + ex.StackTrace, "error");
                }
            }

            return Ok(resultado);
        }

        [HttpPost]
        [Route("AutoCompletarEnvioEnlace")]
        public IActionResult AutoCompletarEnvioEnlace([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string identidadMyGnoss, [FromForm] string identidadOrg, [FromForm] string proyecto, [FromForm] string bool_esPrivada)
        {
            List<string> listaAnteriores = new List<string>();
            if (!string.IsNullOrEmpty(lista))
            {
                string[] arrayAnteriores = lista.Split(',');

                //Recorro el array para limpiar los espacios vacios.
                foreach (string elementoAnt in arrayAnteriores)
                {
                    string elemento = elementoAnt.Trim();
                    if (elemento != "" && !listaAnteriores.Contains(elemento))
                    {
                        listaAnteriores.Add(elemento);
                    }
                }
            }

            Guid identidadID = new Guid(identidad);
            Guid identidadMyGnossID = new Guid(identidadMyGnoss);
            Guid identidadOrganizacionID = Guid.Empty;

            if (!string.IsNullOrEmpty(identidadOrg))
            {
                // David: Identidad de la organización de la que se deben comprobar los permisos
                identidadOrganizacionID = new Guid(identidadOrg);
            }


            Guid proyectoID = new Guid(proyecto);

            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
            IdentidadCL identidadCL = new IdentidadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCL>(), mLoggerFactory);
            DataWrapperIdentidad idenDW = new DataWrapperIdentidad();
            DataWrapperAmigos amigosDW = new DataWrapperAmigos();

            if (bool_esPrivada.ToUpper() == "TRUE")
            {
                //TODO: Comprobar que la identidad pertenece al proyecto
                idenDW = identidadCL.ObtenerMiembrosComunidad(proyectoID);
            }
            else
            {
                AmigosCL amigosCL = new AmigosCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<AmigosCL>(), mLoggerFactory);

                if (!amigosCL.ObtenerAmigos(identidadMyGnossID, idenDW, null, null, amigosDW, identidadOrganizacionID == identidadID, false))
                {
                    idenDW = identidadCN.ObtenerIdentidadesAmigos(identidadMyGnossID);
                }
                amigosCL.Dispose();
            }
            identidadCL.Dispose();

            string busq = UtilCadenas.RemoveAccentsWithRegEx(q);
            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Identidad> filasMiPefil = idenDW.ListaIdentidad.Where(iden => iden.IdentidadID.Equals(identidadID)).ToList();
            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfiles = new List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil>();
            if (filasMiPefil.Count > 0)
            {
                Guid perfilPropioID = filasMiPefil.First().PerfilID;
                perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && !perfil.PerfilID.Equals(perfilPropioID)).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
            }
            else
            {
                perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
            }

            Dictionary<Guid, string> listaIdentidades = new Dictionary<Guid, string>();

            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfiles)
            {
                Guid identidadPerfil = idenDW.ListaIdentidad.Where(ident => ident.PerfilID.Equals(perfil.PerfilID)).First().IdentidadID;

                string nombrePerfil = perfil.NombrePerfil;
                if (perfil.OrganizacionID.HasValue && perfil.PersonaID.HasValue)
                {
                    nombrePerfil = perfil.NombrePerfil + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + perfil.NombreOrganizacion;
                }
                else if (perfil.OrganizacionID.HasValue)
                {
                    nombrePerfil = perfil.NombreOrganizacion;
                }

                if (!listaAnteriores.Contains(identidadPerfil.ToString()))
                {
                    listaIdentidades.Add(identidadPerfil, nombrePerfil);
                }
            }

            #region GRUPOS

            DataWrapperIdentidad idenDSGruposProyectos = identidadCN.ObtenerGruposEnvios(identidadMyGnossID);
            identidadCN.Dispose();
            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.GrupoIdentidades> grupos = idenDSGruposProyectos.ListaGrupoIdentidades.Where(item => UtilCadenas.RemoveAccentsWithRegEx(q).Trim().Contains(item.Nombre.Trim())).ToList();


            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.GrupoIdentidades fila in grupos)
            {
                if (!listaIdentidades.ContainsKey(fila.GrupoID) && fila.GrupoIdentidadesProyecto.ToList()[0].ProyectoID == proyectoID && !listaAnteriores.Contains(fila.GrupoID.ToString()))
                {
                    listaIdentidades.Add(fila.GrupoID, fila.Nombre);
                }
            }
            #endregion

            string resultados = "";
            foreach (Guid clave in listaIdentidades.Keys)
            {
                resultados += listaIdentidades[clave] + "|" + clave;
                resultados += Environment.NewLine;
            }

            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarEnvioEnlaceComunidad")]
        public IActionResult AutoCompletarEnvioEnlaceComunidad([FromForm] string q, [FromForm] string identidad, [FromForm] string proyecto)
        {
            Guid proyectoID = Guid.Empty;
            Guid identidadID = Guid.Empty;

            string resultado = "";
            if (!(Guid.TryParse(identidad, out identidadID) && Guid.TryParse(proyecto, out proyectoID)))
            {
                //Quitamos las comillas
                identidad = identidad.Replace("\"", "");
                proyecto = proyecto.Replace("\"", "");
                q = q.Replace("\"", "");

                if (!(Guid.TryParse(identidad, out identidadID) && Guid.TryParse(proyecto, out proyectoID)))
                {
                    resultado = identidad + " " + proyecto;
                }
            }

            if (Guid.TryParse(identidad, out identidadID) && Guid.TryParse(proyecto, out proyectoID))
            {
                try
                {
                    ServicioAutocompletar contrAuto = new ServicioAutocompletar(mUtilServicios, mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ServicioAutocompletar>(), mLoggerFactory);
                    Dictionary<Guid, string> resultadosFacetas = contrAuto.RealizarConsulta(q, identidadID, proyectoID);

                    foreach (Guid identidadResultadosID in resultadosFacetas.Keys)
                    {
                        string nombre = resultadosFacetas[identidadResultadosID];
                        resultado += nombre + "|" + identidadResultadosID;
                        resultado += Environment.NewLine;
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception ex)
                {
                    mUtilServicios.GuardarLog("Error: " + ex.Message + "\r\nPila: " + ex.StackTrace, "error");
                }
            }

            return Ok(resultado);
        }

        ConcurrentDictionary<string, string> DatosPorBaseRecursosPersona = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Autocompletar para la edición/creación de un recurso, para añadir lectores o editores
        /// </summary>
        /// <param name="q">Texto de entrada</param>
        /// <param name="lista">Lista de ID ya seleccionados</param>
        /// <param name="identidad">Identidad actual</param>
        /// <param name="proyecto">ProyectoActual</param>
        /// <param name="bool_edicion">True si es para editores, false para lectores</param>
        /// <returns></returns>
        [HttpPost]
        [Route("AutoCompletarLectoresEditoresPorBaseRecursos")]
        public IActionResult AutoCompletarLectoresEditoresPorBaseRecursos([FromForm] string q, [FromForm] string lista, [FromForm] string BaseRecursos, [FromForm] string Persona)
        {
            string proyecto;
            string identidad;
            string organizacion;

            string baseRecursosID = BaseRecursos.Replace("\"", "");
            string personaID = Persona.Replace("\"", "");

            if (DatosPorBaseRecursosPersona.ContainsKey(baseRecursosID + "_" + personaID))
            {
                string[] datos = DatosPorBaseRecursosPersona[baseRecursosID + "_" + personaID].Split(',');
                proyecto = datos[0];
                identidad = datos[1];
                organizacion = datos[2];
            }
            else
            {
                Guid identidadID;
                Guid organizacionID;
                Guid proyectoID;

                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCN>(), mLoggerFactory);
                proyCN.ObtenerDatosPorBaseRecursosPersona(new Guid(baseRecursosID), new Guid(personaID), out proyectoID, out identidadID, out organizacionID);

                proyecto = proyectoID.ToString();
                identidad = identidadID.ToString();
                organizacion = organizacionID.ToString();

                DatosPorBaseRecursosPersona.TryAdd(baseRecursosID + "_" + personaID, proyecto + "," + identidad + "," + organizacion);
            }
            return Ok(AutoCompletarLectoresEditores(q, lista, identidad, organizacion, proyecto, "true", "true", "", "true"));
        }

        [HttpPost]
        [Route("AutoCompletarSeleccEntPerYGruposGnoss")]
        public IActionResult AutoCompletarSeleccEntPerYGruposGnoss([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string organizacion, [FromForm] string proyecto, [FromForm] string tipoSolicitud)
        {
            return Ok(AutoCompletarLectoresEditoresInt(q, lista, identidad, organizacion, proyecto, "true", short.Parse(tipoSolicitud), null));
        }

        /// <summary>
        /// Autocompletar para la edición/creación de un recurso, para añadir lectores o editores
        /// </summary>
        /// <param name="q">Texto de entrada</param>
        /// <param name="lista">Lista de ID ya seleccionados</param>
        /// <param name="identidad">Identidad actual</param>
        /// <param name="proyecto">ProyectoActual</param>
        /// <param name="bool_edicion">True si es para editores, false para lectores</param>
        /// <returns></returns>

        [HttpPost]
        [Route("AutoCompletarLectoresEditores")]
        public IActionResult AutoCompletarLectoresEditores([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string organizacion, [FromForm] string proyecto, [FromForm] string bool_edicion, [FromForm] string bool_traergrupos, [FromForm] string grupo, [FromForm] string bool_traerperfiles)
        {
            short traerDatos = 0;//Todo

            if (bool_traergrupos.ToUpper() != "TRUE")
            {
                traerDatos = 1;
            }

            if (bool_traerperfiles.ToUpper() != "TRUE")
            {
                traerDatos = 2;
            }

            string respuesta = AutoCompletarLectoresEditoresInt(q, lista, identidad, organizacion, proyecto, bool_edicion, traerDatos, grupo);

            return Ok(respuesta);
        }

        /// <summary>
        /// Autocompletar para el buscador de administracion matomo
        /// </summary>
        /// <param name="q">Nombre de perfil a buscar</param>
        /// <returns></returns>
        [HttpPost]
        [Route("AutoCompletarSeleccionUsuariosMatomo")]
        public IActionResult AutoCompletarSeleccionUsuariosMatomo([FromForm] string q)
        {
            return Ok(AutoCompletarUsuariosMatomosInt(q, 10));
        }
        /// <summary>
        /// Autocompletar para la edición/creación de un recurso, para las ontologias
        /// </summary>
        /// <param name="q">Texto de entrada</param>
        /// <param name="lista">Lista de ID ya seleccionados</param>
        /// <param name="identidad">Identidad actual</param>
        /// <param name="proyecto">ProyectoActual</param>
        /// <returns></returns>
        [HttpPost]
        [Route("AutoCompletarOntologia")]
        public IActionResult AutoCompletarOntologia([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string organizacion, [FromForm] string proyecto)
        {
            string respuesta = ObtenerPropiedadesOntologia(q, lista, identidad, organizacion, proyecto);

            return Ok(respuesta);
        }
        /// <summary>
        /// Autocompletar para la edición/creación de un recurso, para añadir lectores o editores
        /// </summary>
        /// <param name="q">Texto de entrada</param>
        /// <param name="lista">Lista de ID ya seleccionados</param>
        /// <param name="identidad">Identidad actual</param>
        /// <param name="proyecto">ProyectoActual</param>
        /// <param name="bool_edicion">True si es para editores, false para lectores</param>
        /// <returns></returns>
        [HttpPost]
        [Route("AutoCompletarLectoresEditoresConUsuarioID")]
        public IActionResult AutoCompletarLectoresEditoresConUsuarioID([FromForm] AutocompletarLectoresEditoresModel autocompletarLectoresEditoresModel)
        {
            short traerDatos = 3;//Usuarios
            string respuesta = AutoCompletarLectoresEditoresInt(autocompletarLectoresEditoresModel.q, autocompletarLectoresEditoresModel.lista, autocompletarLectoresEditoresModel.identidad, autocompletarLectoresEditoresModel.organizacion, autocompletarLectoresEditoresModel.proyecto, autocompletarLectoresEditoresModel.bool_edicion, traerDatos, autocompletarLectoresEditoresModel.grupo);

            return Ok(respuesta);
        }

        /// <summary>
        /// Autocompletar que busca en los miembros de una organización
        /// </summary>
        /// <param name="q">Texto de entrada</param>
        /// <param name="lista">Lista de ID ya seleccionados</param>
        /// <param name="identidad">Identidad actual</param>
        /// <param name="proyecto">ProyectoActual</param>
        /// <param name="bool_edicion">True si es para editores, false para lectores</param>
        /// <returns></returns>
        [HttpPost]
        [Route("AutocompletarMiembrosOrganizacion")]
        public IActionResult AutocompletarMiembrosOrganizacion([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string organizacion, [FromForm] string grupo)
        {
            if (lista == null) { lista = ""; }
            string[] arrayAnteriores = lista.Split(',');
            List<string> listaAnteriores = new List<string>();
            //Recorro el array para limpiar los espacios vacios.
            foreach (string elementoAnt in arrayAnteriores)
            {
                string elemento = elementoAnt.Trim();
                if (elemento != "" && !listaAnteriores.Contains(elemento))
                {
                    listaAnteriores.Add(elemento);
                }
            }

            Guid identidadID = new Guid(identidad);

            Guid organizacionID = new Guid(organizacion);

            IdentidadCL identidadCL = new IdentidadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCL>(), mLoggerFactory);
            DataWrapperIdentidad idenDW = new DataWrapperIdentidad();

            //TODO: Comprobar que la identidad pertenece al proyecto
            idenDW = identidadCL.ObtenerMiembrosOrganizacionParaFiltroGrupos(organizacionID);
            string busq = UtilCadenas.RemoveAccentsWithRegEx(q);

            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfiles = null;
            string sql = "(NombreBusqueda LIKE '% " + busq.Trim() + "%' OR NombreBusqueda LIKE '" + busq.Trim() + "%') AND PersonaID IS NOT NULL";

            perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && perfil.PersonaID.HasValue).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();

            List<Guid> listaPerfilesGrupo = new List<Guid>();

            if (!string.IsNullOrEmpty(grupo))
            {
                GestionIdentidades gestorIdentidades = new GestionIdentidades(identidadCL.ObtenerGrupoPorNombreCortoYOrganizacion(grupo, organizacionID), mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);

                if (gestorIdentidades.DataWrapperIdentidad.ListaGrupoIdentidades.Count > 0)
                {
                    Guid grupoID = gestorIdentidades.DataWrapperIdentidad.ListaGrupoIdentidades[0].GrupoID;
                    Es.Riam.Gnoss.Elementos.Identidad.GrupoIdentidades Grupo = gestorIdentidades.ListaGrupos[grupoID];

                    foreach (Es.Riam.Gnoss.Elementos.Identidad.Identidad ident in Grupo.Participantes.Values)
                    {
                        listaPerfilesGrupo.Add(ident.PerfilID);
                    }
                }
            }

            #region Devolvemos los perfiles y grupos obtenidos
            Dictionary<Guid, string> listaPerfiles = new Dictionary<Guid, string>();

            int contador = 0;
            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfiles)
            {
                string nombrePerfil = perfil.NombrePerfil;

                if (!listaAnteriores.Contains(perfil.PerfilID.ToString()) && !listaPerfilesGrupo.Contains(perfil.PerfilID))
                {
                    listaPerfiles.Add(perfil.PerfilID, nombrePerfil);
                    contador++;
                    if (contador == 10)
                    {
                        break;
                    }
                }
            }

            string resultados = "";
            foreach (Guid clave in listaPerfiles.Keys)
            {
                resultados += listaPerfiles[clave] + "|" + clave;
                resultados += Environment.NewLine;
            }

            #endregion

            identidadCL.Dispose();

            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarInvitaciones")]
        public IActionResult AutoCompletarInvitaciones([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] string identidadMyGnoss, [FromForm] string identidadOrg, [FromForm] string proyecto)
        {
            string[] arrayAnteriores = lista.Split(',');
            List<string> listaAnteriores = new List<string>();
            //Recorro el array para limpiar los espacios vacios.
            foreach (string elementoAnt in arrayAnteriores)
            {
                string elemento = elementoAnt.Trim();
                if (elemento != "" && !listaAnteriores.Contains(elemento))
                {
                    listaAnteriores.Add(elemento);
                }
            }

            Guid identidadID = new Guid(identidad);
            Guid identidadMyGnossID = new Guid(identidadMyGnoss);
            Guid identidadOrganizacionID = Guid.Empty;

            if (identidadOrg != "")
            {
                // David: Identidad de la organización de la que se deben comprobar los permisos
                identidadOrganizacionID = new Guid(identidadOrg);
            }

            Guid proyectoID = new Guid(proyecto);

            IdentidadCL identidadCL = new IdentidadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCL>(), mLoggerFactory);
            DataWrapperIdentidad idenDW = new DataWrapperIdentidad();
            DataWrapperAmigos dataWrapperAmigos = new DataWrapperAmigos();
            AmigosCL amigosCL = new AmigosCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<AmigosCL>(), mLoggerFactory);

            if (!amigosCL.ObtenerAmigos(identidadMyGnossID, idenDW, null, null, dataWrapperAmigos, identidadOrganizacionID == identidadID, false))
            {
                //TODO: Cargar si no esta en cache
                return Ok("");
            }
            amigosCL.Dispose();

            string busq = UtilCadenas.RemoveAccentsWithRegEx(q);

            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfiles = null;
            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Identidad> filasMiPefil = idenDW.ListaIdentidad.Where(ident => ident.IdentidadID.Equals(identidadID)).ToList();
            if (filasMiPefil.Count > 0)
            {
                Guid perfilPropioID = filasMiPefil.First().PerfilID;
                perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && !perfil.PerfilID.Equals(perfilPropioID)).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
            }
            else
            {
                perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
            }


            Dictionary<Guid, string> listaPerfiles = new Dictionary<Guid, string>();

            // IdentidadDS identMiembrosDS = identidadCL.ObtenerMiembrosComunidad(proyectoID);

            IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);

            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfiles)
            {
                //Problemas con las organizaciones, ya que perfil.personaID = null
                //if (identMiembrosDS.Perfil.Select("PersonaID = '" + perfil.PersonaID.ToString() + "'").Length == 0)
                {
                    Guid identidadPerfil = idenDW.ListaIdentidad.FirstOrDefault(ident => ident.PerfilID.Equals(perfil.PerfilID)).IdentidadID;

                    string nombrePerfil = perfil.NombrePerfil;
                    if (perfil.OrganizacionID.HasValue && perfil.PersonaID.HasValue)
                    {
                        nombrePerfil = perfil.NombrePerfil + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + perfil.NombreOrganizacion;
                    }
                    else if (perfil.OrganizacionID.HasValue)
                    {
                        nombrePerfil = perfil.NombreOrganizacion;
                    }

                    if (!listaAnteriores.Contains(identidadPerfil.ToString()))
                    {
                        //Si es el nombre de una clase, no se debe mostrar porque no se tienen que mostrar las organizaciones de tipo clase.
                        if (identCN.ObtenerIdentidadProfesor(perfil.PerfilID) == null)
                        {
                            listaPerfiles.Add(identidadPerfil, nombrePerfil);
                        }
                    }
                }
            }

            identCN.Dispose();

            string resultados = "";
            foreach (Guid clave in listaPerfiles.Keys)
            {
                resultados += listaPerfiles[clave] + "|" + clave;
                resultados += Environment.NewLine;
            }

            return Ok(resultados);
        }

        [HttpGet]
        [Route("AutoCompletarInvitacionesGnoss")]
        public IActionResult AutoCompletarInvitacionesGnoss(string q, string lista, string identidad, string identidadMyGnoss, string identidadOrg)
        {
            string[] arrayAnteriores = lista.Split(',');
            List<string> listaAnteriores = new List<string>();
            //Recorro el array para limpiar los espacios vacios.
            foreach (string elementoAnt in arrayAnteriores)
            {
                string elemento = elementoAnt.Trim();
                if (elemento != "" && !listaAnteriores.Contains(elemento))
                {
                    listaAnteriores.Add(elemento);
                }
            }

            Guid identidadID = new Guid(identidad);
            Guid identidadMyGnossID = new Guid(identidadMyGnoss);
            Guid identidadOrganizacionID = Guid.Empty;

            if (identidadOrg != "")
            {
                // David: Identidad de la organización de la que se deben comprobar los permisos
                identidadOrganizacionID = new Guid(identidadOrg);
            }

            //Guid proyectoID = ProyectoAD.MetaProyecto;

            DataWrapperIdentidad idenDW = new DataWrapperIdentidad();
            DataWrapperAmigos dataWrapperAmigos = new DataWrapperAmigos();
            AmigosCL amigosCL = new AmigosCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<AmigosCL>(), mLoggerFactory);

            if (!amigosCL.ObtenerAmigos(identidadMyGnossID, idenDW, null, null, dataWrapperAmigos, identidadOrganizacionID == identidadID, false))
            {
                //TODO: Cargar si no esta en cache
                return Ok("");
            }
            amigosCL.Dispose();

            IdentidadCL identidadCL = new IdentidadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCL>(), mLoggerFactory);
            DataWrapperIdentidad identMiembrosDW = identidadCL.ObtenerMiembrosGnossVisibles();
            identidadCL.Dispose();

            string busq = UtilCadenas.RemoveAccentsWithRegEx(q);

            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfiles = null;
            //"IdentidadID = '" + identidadID.ToString() + "'"
            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Identidad> filasMiPefil = identMiembrosDW.ListaIdentidad.Where(ident => ident.IdentidadID.Equals(identidadID)).ToList();
            if (filasMiPefil.Count > 0)
            {
                Guid perfilPropioID = filasMiPefil.First().PerfilID;
                perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && !perfil.PerfilID.Equals(perfilPropioID)).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
            }
            else
            {
                perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
            }

            Dictionary<Guid, string> listaPerfiles = new Dictionary<Guid, string>();

            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfiles)
            {
                //Solo si no es tu contacto:

                if (idenDW.ListaIdentidad.Count(ident => ident.PerfilID.Equals(perfil.PerfilID)) == 0)
                {
                    Guid identidadPerfil = identMiembrosDW.ListaIdentidad.Where(ident => ident.PerfilID.Equals(perfil.PerfilID)).First().IdentidadID;

                    string nombrePerfil = perfil.NombrePerfil;
                    if (perfil.OrganizacionID.HasValue && perfil.PersonaID.HasValue)
                    {
                        nombrePerfil = perfil.NombrePerfil + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + perfil.NombreOrganizacion;
                    }
                    else if (perfil.OrganizacionID.HasValue)
                    {
                        nombrePerfil = perfil.NombreOrganizacion;
                    }

                    if (!listaAnteriores.Contains(identidadPerfil.ToString()))
                    {
                        listaPerfiles.Add(identidadPerfil, nombrePerfil);
                    }
                }
            }

            string resultados = "";
            foreach (Guid clave in listaPerfiles.Keys)
            {
                resultados += listaPerfiles[clave] + "|" + clave;
                resultados += Environment.NewLine;
            }

            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarContactosYGrupos")]
        public IActionResult AutoCompletarContactosYGrupos([FromForm] string q, [FromForm] string lista, [FromForm] string identidad, [FromForm] bool esOrganizacion, [FromForm] bool traerContactos, [FromForm] bool traerGrupos)
        {
            string[] arrayAnteriores = lista.Split(',');
            List<string> listaAnteriores = new List<string>();
            //Recorro el array para limpiar los espacios vacios.
            foreach (string elementoAnt in arrayAnteriores)
            {
                string elemento = elementoAnt.Trim();
                if (elemento != "" && !listaAnteriores.Contains(elemento))
                {
                    listaAnteriores.Add(elemento);
                }
            }

            Guid identidadID = new Guid(identidad);

            DataWrapperIdentidad idenDW = new DataWrapperIdentidad();
            DataWrapperAmigos amigosDW = new DataWrapperAmigos();
            AmigosCL amigosCL = new AmigosCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<AmigosCL>(), mLoggerFactory);


            //amigosDS = amigosCN.ObtenerAmigosDeIdentidad(identidadID);

            Dictionary<Guid, string> listaResultados = new Dictionary<Guid, string>();
            if (traerContactos)
            {

                if (!amigosCL.ObtenerAmigos(identidadID, idenDW, null, null, amigosDW, esOrganizacion, false))
                {
                    //TODO: Cargar si no esta en cache
                    return Ok("");
                }

                List<Guid> listaIDentidadesContactos = new List<Guid>();
                foreach (Amigo filaAmigo in amigosDW.ListaAmigo)
                {
                    if (!listaIDentidadesContactos.Contains(filaAmigo.IdentidadAmigoID))
                    {
                        listaIDentidadesContactos.Add(filaAmigo.IdentidadAmigoID);
                    }
                }

                string busq = UtilCadenas.RemoveAccentsWithRegEx(q);

                List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil> perfiles = null;
                List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Identidad> filasMiPefil = idenDW.ListaIdentidad.Where(ident => ident.IdentidadID.Equals(identidadID)).ToList();
                if (filasMiPefil.Count > 0)
                {
                    Guid perfilPropioID = filasMiPefil.First().PerfilID;
                    perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower()) && !perfil.PerfilID.Equals(perfilPropioID)).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
                }
                else
                {
                    perfiles = idenDW.ListaPerfil.Where(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(perfil => UtilCadenas.RemoveAccentsWithRegEx(perfil.NombrePerfil)).ToList();
                }

                //Dictionary<Guid, string> listaPerfiles = new Dictionary<Guid, string>();

                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in perfiles)
                {
                    Guid identidadPerfil = idenDW.ListaIdentidad.Where(ident => ident.PerfilID.Equals(perfil.PerfilID)).First().IdentidadID;

                    if (listaIDentidadesContactos.Contains(identidadPerfil))
                    {

                        string nombrePerfil = perfil.NombrePerfil;
                        if (perfil.OrganizacionID.HasValue && perfil.PersonaID.HasValue)
                        {
                            nombrePerfil = perfil.NombrePerfil + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + perfil.NombreOrganizacion;
                        }
                        else if (perfil.OrganizacionID.HasValue)
                        {
                            nombrePerfil = perfil.NombreOrganizacion;
                        }

                        if (!listaAnteriores.Contains(identidadPerfil.ToString()))
                        {
                            listaResultados.Add(identidadPerfil, nombrePerfil);
                        }
                    }
                }
            }

            if (traerGrupos)
            {

                if (!amigosCL.ObtenerAmigos(identidadID, idenDW, null, null, amigosDW, esOrganizacion, false))
                {
                    //TODO: Cargar si no esta en cache
                    return Ok("");
                }
                string busq = UtilCadenas.RemoveAccentsWithRegEx(q);

                List<GrupoAmigos> grupos = new List<GrupoAmigos>();

                string sql = "(Nombre LIKE '% " + busq.Trim() + "%' OR Nombre LIKE '" + busq.Trim() + "%')";

                grupos = amigosDW.ListaGrupoAmigos.Where(item => item.Nombre.Contains(busq.Trim())).OrderByDescending(item => item.Nombre).ToList();


                //Dictionary<Guid, string> listaGrupos = new Dictionary<Guid,string>();

                foreach (GrupoAmigos grupo in grupos)
                {
                    Guid identidadGrupo = grupo.GrupoID;
                    string nombreGrupo = grupo.Nombre;

                    if (!listaAnteriores.Contains(identidadGrupo.ToString()))
                    {
                        listaResultados.Add(identidadGrupo, nombreGrupo);
                    }
                }

            }

            amigosCL.Dispose();

            string resultados = "";
            foreach (Guid clave in listaResultados.Keys)
            {
                resultados += listaResultados[clave] + "|" + clave;
                resultados += Environment.NewLine;
            }

            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarGruposInvitaciones")]
        public IActionResult AutoCompletarGruposInvitaciones([FromForm] string q, [FromForm] string lista, [FromForm] string proyecto)
        {
            string[] arrayAnteriores = lista.Split(',');
            List<string> listaAnteriores = new List<string>();
            //Recorro el array para limpiar los espacios vacios.
            foreach (string elementoAnt in arrayAnteriores)
            {
                string elemento = elementoAnt.Trim();
                if (elemento != "" && !listaAnteriores.Contains(elemento))
                {
                    listaAnteriores.Add(elemento);
                }
            }

            Guid proyectoID = new Guid(proyecto);

            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
            DataWrapperIdentidad dataWrapperIdentidad = identidadCN.ObtenerGruposDeProyecto(proyectoID, false);
            identidadCN.Dispose();

            string busq = UtilCadenas.RemoveAccentsWithRegEx(q);
            busq = busq.ToLower();
            string sql = "(Nombre LIKE '% " + busq.Trim() + "%' OR Nombre LIKE '" + busq.Trim() + "%')";

            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.GrupoIdentidades> filasGrupos = dataWrapperIdentidad.ListaGrupoIdentidades.Where(grupoIden => grupoIden.Nombre.ToLower().Contains(busq.Trim().ToLower())).OrderByDescending(grupoIden => grupoIden.Nombre).ToList();

            Dictionary<Guid, string> listaGrupoIdentidades = new Dictionary<Guid, string>();

            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.GrupoIdentidades GrupoIdentidades in filasGrupos)
            {
                listaGrupoIdentidades.Add(GrupoIdentidades.GrupoID, GrupoIdentidades.Nombre);
            }

            string resultados = "";
            foreach (Guid clave in listaGrupoIdentidades.Keys)
            {
                if (!listaAnteriores.Contains(clave.ToString()))
                {
                    resultados += listaGrupoIdentidades[clave] + "|" + clave;
                    resultados += Environment.NewLine;
                }
            }

            return Ok(resultados);
        }

        /// <summary>
        /// Para los registros en comunidades, devuelve los datos consultados por querys a virtuoso.
        /// </summary>
        /// <param name="q">texto a consultar</param>
        /// <param name="pProyecto">Proyecto desde el que se hace el registro</param>
        /// <param name="pOrigen">AutocompletarID que llama al servicio</param>
        /// <param name="pArgumentos">Arugmentos extras para la query a virtuoso</param>
        /// <returns></returns>
        [HttpPost]
        [Route("AutoCompletarDatoExtraProyectoVirtuoso")]
        public IActionResult AutoCompletarDatoExtraProyectoVirtuoso([FromForm] string q, [FromForm] string lista, [FromForm] string pProyectoID, [FromForm] string pOrigen, [FromForm] string pArgumentos)
        {
            string resultados = "";

            try
            {
                q = LimpiarComillasSimples(q);
                pProyectoID = LimpiarComillasSimples(pProyectoID);
                pOrigen = LimpiarComillasSimples(pOrigen);
                pArgumentos = LimpiarComillasSimples(pArgumentos);

                //Obtener el DS con los datosextraproyecto
                ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCN>(), mLoggerFactory);
                DataWrapperProyecto datosExtraProyectoDW = proyectoCN.ObtenerDatosExtraProyectoPorID(new Guid(pProyectoID));
                proyectoCN.Dispose();

                if (datosExtraProyectoDW.ListaDatoExtraProyectoVirtuoso.Count > 0 || datosExtraProyectoDW.ListaDatoExtraEcosistemaVirtuoso.Count > 0)
                {
                    //Limpiamos el pOrigen para que quede el ID del input.
                    if (pOrigen.Contains("_"))
                    {
                        pOrigen = pOrigen.Substring(pOrigen.LastIndexOf("_") + 1);
                    }
                    string[] delimiter = { "|" };
                    string[] argumentosSplitted = pArgumentos.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                    string queryVirtuoso = "";
                    DatoExtraProyectoVirtuoso datoExtraProyectoVirtuoso = datosExtraProyectoDW.ListaDatoExtraProyectoVirtuoso.FirstOrDefault(dato => dato.InputID.Equals(pOrigen));
                    DatoExtraEcosistemaVirtuoso datoExtraEcosistemaVirtuoso = datosExtraProyectoDW.ListaDatoExtraEcosistemaVirtuoso.FirstOrDefault(dato => dato.InputID.Equals(pOrigen));
                    if (datoExtraProyectoVirtuoso != null)
                    {
                        queryVirtuoso = ObtenerQueryFinal(datoExtraProyectoVirtuoso.QueryVirtuoso, argumentosSplitted, q);
                    }
                    else if (datoExtraEcosistemaVirtuoso != null)
                    {
                        queryVirtuoso = ObtenerQueryFinal(datoExtraEcosistemaVirtuoso.QueryVirtuoso, argumentosSplitted, q);
                    }

                    if (!string.IsNullOrEmpty(queryVirtuoso))
                    {
                        FacetadoCN facCN = new FacetadoCN("acid", UrlIntragnoss, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoCN>(), mLoggerFactory);
                        FacetadoDS facetadoDS = facCN.LeerDeVirtuoso(queryVirtuoso, "aaa", "");
                        facCN.Dispose();

                        if (facetadoDS.Tables["aaa"] != null)
                        {
                            int i = 0;
                            foreach (DataRow fila in facetadoDS.Tables["aaa"].Rows)
                            {
                                if (i < 50)
                                {
                                    string nombre = (string)fila[0];

                                    if (fila.ItemArray.Length == 3)
                                    {
                                        if (!string.IsNullOrEmpty((string)fila[1])) { nombre += " - " + fila[1]; }
                                        string url = (string)fila[2];
                                        resultados += nombre + "|||datoextraproyectovirtuoso|||" + url;
                                    }
                                    else if (fila.ItemArray.Length == 2)
                                    {
                                        string url = (string)fila[1];
                                        resultados += nombre + "|||datoextraproyectovirtuoso|||" + url;
                                    }
                                    else
                                    {
                                        resultados += nombre;
                                    }
                                    resultados += Environment.NewLine;
                                    i++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog(ex.Message + "\r\nPila: " + ex.StackTrace, "error");
            }

            return Ok(resultados);
        }

        [HttpPost]
        [Route("AutoCompletarSupervisoresProyecto")]
        public IActionResult AutoCompletarSupervisoresProyecto([FromForm] string q, [FromForm] string lista, [FromForm] string proyecto)
        {
            string resultados = "";

            try
            {
                string[] arrayAnteriores = lista.Split(',');
                List<string> listaAnteriores = new List<string>();

                //Recorro el array para limpiar los espacios vacios.
                foreach (string elementoAnt in arrayAnteriores)
                {
                    string elemento = elementoAnt.Trim();
                    if (elemento != "" && !listaAnteriores.Contains(elemento))
                    {
                        listaAnteriores.Add(elemento);
                    }
                }

                proyecto = LimpiarComillasSimples(proyecto);

                if (!string.IsNullOrEmpty(proyecto))
                {
                    Guid proyectoID = Guid.Empty;
                    Guid identidadID = Guid.Empty;
                    if (Guid.TryParse(proyecto, out proyectoID) && !proyectoID.Equals(Guid.Empty))
                    {
                        ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCN>(), mLoggerFactory);
                        List<Guid> identidadesSupervisores = proyCN.ObtenerListaIdentidadesSupervisoresPorProyecto(proyectoID);
                        proyCN.Dispose();

                        //Obtenemos los perfiles relacionados con la búsqueda
                        string busq = UtilCadenas.RemoveAccentsWithRegEx(q);
                        Dictionary<Guid, Tuple<string, string, Guid?, Guid?>> listaPerfilesBusqueda = new Dictionary<Guid, Tuple<string, string, Guid?, Guid?>>();
                        IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
                        listaPerfilesBusqueda = identCN.ObtenerPerfilesParaAutocompletarDeIdentidadesID(identidadesSupervisores, proyectoID, identidadID, busq, 10, true);
                        //como sólo se muestran 10 no necesito traer más perfiles
                        identCN.Dispose();

                        Dictionary<Guid, string> listaPerfiles = new Dictionary<Guid, string>();
                        int contador = 0;
                        if (listaPerfilesBusqueda.Count > 0)
                        {
                            foreach (Guid idPerfil in listaPerfilesBusqueda.Keys)
                            {
                                if (!listaAnteriores.Contains(idPerfil.ToString()))
                                {
                                    string nombrePerfil = listaPerfilesBusqueda[idPerfil].Item1;
                                    if (listaPerfilesBusqueda[idPerfil].Item4.HasValue && listaPerfilesBusqueda[idPerfil].Item3.HasValue)
                                    {
                                        nombrePerfil = listaPerfilesBusqueda[idPerfil].Item1 + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + listaPerfilesBusqueda[idPerfil].Item2;
                                    }
                                    else if (listaPerfilesBusqueda[idPerfil].Item4.HasValue)
                                    {
                                        nombrePerfil = listaPerfilesBusqueda[idPerfil].Item2;
                                    }
                                    listaPerfiles.Add(idPerfil, nombrePerfil);
                                    contador++;
                                    if (contador == 10)
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        foreach (Guid clave in listaPerfiles.Keys)
                        {
                            resultados += listaPerfiles[clave] + "|" + clave;
                            resultados += Environment.NewLine;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog(ex.Message + "\r\nPila: " + ex.StackTrace, "error");
            }

            return Ok(resultados);
        }

        [HttpPost]
		[Route("AutocompletarNombresProyectos")]
		public IActionResult AutocompletarNombresProyectos([FromForm] string q)
        {
            string resultados = "";
            try
            {
                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCN>(), mLoggerFactory);
                List<string> listaNombres = proyCN.ObtenerNombresDeProyectoPorBusquedaAutocompletar(q);
                foreach (string nombreProyecto in listaNombres)
                {
                    resultados += nombreProyecto;
                    resultados += Environment.NewLine;
				}
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al obtener los proyectos para la consulta: '{q}'",mlogger);
            }

            return Ok(resultados);
        }

        [NonAction]
        private string LimpiarComillasSimples(string pParametro)
        {
            if (pParametro.StartsWith("\""))
            {
                pParametro = pParametro.Substring(1);
            }
            if (pParametro.EndsWith("\""))
            {
                pParametro = pParametro.Substring(0, pParametro.Length - 1);
            }
            return pParametro;
        }

        private string ObtenerQueryFinal(string pQueryVirtuoso, string[] pArgumentosSplitted, string q)
        {
            string queryVirtuoso = pQueryVirtuoso;
            for (int i = 0; i < pArgumentosSplitted.Length; i++)
            {
                string argumentosFinales = ObtenerArgumentoQueryVirtuoso(pArgumentosSplitted[i]);

                queryVirtuoso = queryVirtuoso.Replace("@" + i + "@", UtilCadenas.ToSparql(argumentosFinales));
            }

            queryVirtuoso = queryVirtuoso.Replace("@letraAutocompletar@", UtilCadenas.ToSparql(q.ToLower()));

            return queryVirtuoso;
        }
        [NonAction]
        private string ObtenerArgumentoQueryVirtuoso(string pArgumentoSeparado)
        {
            Guid paisID;
            string argumentoFinal = pArgumentoSeparado;
            if (Guid.TryParse(pArgumentoSeparado, out paisID))
            {
                argumentoFinal = ObtenerPaisPorID(paisID);
            }

            return argumentoFinal;
        }
        [NonAction]
        private string ObtenerPaisPorID(Guid paisID)
        {
            PaisCL paisCL = new PaisCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<PaisCL>(), mLoggerFactory);
            DataWrapperPais paisDW = paisCL.ObtenerPaisesProvincias();

            string paisTexto = "";

            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.Pais.Pais fila in paisDW.ListaPais)
            {
                if (fila.PaisID.Equals(paisID))
                {
                    paisTexto = fila.Nombre;
                }
            }

            paisCL.Dispose();

            return paisTexto;
        }

        #endregion

        #region Propiedades

        /// <summary>
        /// Obtiene la url de Intragnoss
        /// </summary>
        private string UrlIntragnoss
        {
            get
            {
                if (string.IsNullOrEmpty(mUrlIntragnoss))
                {
                    ParametroAplicacionCL paramCL = new ParametroAplicacionCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ParametroAplicacionCL>(), mLoggerFactory);
                    List<ParametroAplicacion> parametrosAplicacion = paramCL.ObtenerParametrosAplicacionPorContext();
                    mUrlIntragnoss = parametrosAplicacion.Find(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).Valor;
                }
                return mUrlIntragnoss;
            }
        }

        private bool? mCargarIdentidadesDeProyectosPrivadosComoAmigos;
        public bool CargarIdentidadesDeProyectosPrivadosComoAmigos
        {
            get
            {
                if (!mCargarIdentidadesDeProyectosPrivadosComoAmigos.HasValue)
                {
                    ParametroAplicacionCL paramCL = new ParametroAplicacionCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ParametroAplicacionCL>(), mLoggerFactory);
                    List<ParametroAplicacion> parametrosAplicacion = paramCL.ObtenerParametrosAplicacionPorContext();
                    List<ParametroAplicacion> parametrosAplicacionPrivAmigos = parametrosAplicacion.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.CargarIdentidadesDeProyectosPrivadosComoAmigos)).ToList();
                    mCargarIdentidadesDeProyectosPrivadosComoAmigos = parametrosAplicacionPrivAmigos.Count > 0 && (parametrosAplicacionPrivAmigos[0].Equals("1") || parametrosAplicacionPrivAmigos[0].Valor.ToString().ToLower().Equals("true"));
                }

                return mCargarIdentidadesDeProyectosPrivadosComoAmigos.Value;
            }
        }
        [NonAction]
        private Dictionary<string, List<string>> CargarDicInformacionOntologias(string tipo, string organizacion, string proyecto)
        {
            mInformacionOntologias = new Dictionary<string, List<string>>();

            if (!tipo.Equals(FacetadoAD.BUSQUEDA_PERSONASYORG) && !tipo.Equals(FacetadoAD.BUSQUEDA_PERSONA) && !tipo.Equals(FacetadoAD.BUSQUEDA_PREGUNTAS) && !tipo.Equals(FacetadoAD.BUSQUEDA_DEBATES) && !tipo.Equals(FacetadoAD.BUSQUEDA_ORGANIZACION))
            {
                //ObtenerOntologias
                Guid orgID = Guid.Empty;
                if (!string.IsNullOrEmpty(organizacion))
                {
                    orgID = new Guid(organizacion);
                }

                mInformacionOntologias = mUtilServiciosFacetas.ObtenerInformacionOntologias(orgID, new Guid(proyecto));
            }

            return mInformacionOntologias;
        }

        [NonAction]
        private List<string> CargarListaItemsBusquedaExtra(string tipo, string parametros, string organizacion, string proyecto)
        {
            mListaItemsExtra = new List<string>();
            Guid auxId;
            if (tipo == null)
            {
                tipo = "";
            }

            if (tipo.Equals("Recurso") || tipo.Equals("Meta") || tipo.Equals("") || (Guid.TryParse(tipo, out auxId) && !auxId.Equals(Guid.Empty)))
            {
                //ObtenerOntologias
                FacetaCL tablasDeConfiguracionCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
                mTConfiguracionOntologia = new DataWrapperFacetas();
                Guid orgID = Guid.Empty;
                if (!string.IsNullOrEmpty(organizacion))
                {
                    orgID = new Guid(organizacion);
                }
                List<Es.Riam.Gnoss.AD.EntityModel.Models.Faceta.OntologiaProyecto> listaOntologiaProyecto = tablasDeConfiguracionCL.ObtenerOntologiasProyecto(orgID, new Guid(proyecto)/*, "es"*/);
                mTConfiguracionOntologia.ListaOntologiaProyecto = listaOntologiaProyecto;
                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.Faceta.OntologiaProyecto myrow in mTConfiguracionOntologia.ListaOntologiaProyecto)
                {
                    if (!myrow.OntologiaProyecto1.StartsWith("@"))
                    {
                        mListaItemsExtra.Add(myrow.OntologiaProyecto1);
                    }
                }
            }

            return mListaItemsExtra;
        }



        /// <summary>
        /// Obtiene la lista con las propiedades de tipo rango
        /// </summary>
        [NonAction]
        private List<string> CargarPropiedadesRango(GestionFacetas pGestorFacetas)
        {
            mPropiedadesRango = new List<string>();

            FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
            List<Faceta> lista = pGestorFacetas.ListaFacetas.Where(faceta => faceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Numero)).ToList();

            foreach (Faceta fac in lista)
            {
                mPropiedadesRango.Add(fac.ClaveFaceta.Substring(fac.ClaveFaceta.LastIndexOf(":") + 1));
            }

            return mPropiedadesRango;
        }


        /// <summary>
        /// <summary>
        /// Obtiene la lista con las propiedades de tipo rango
        /// </summary>
        private List<string> PropiedadesRango
        {
            get
            {
                return mPropiedadesRango;
            }
        }


        /// <summary>
        /// Obtiene la lista con las propiedades de tipo Fecha
        /// </summary>
        [NonAction]
        private List<string> CargarPropiedadesFecha(GestionFacetas pGestorFacetas)
        {
            mPropiedadesFecha = new List<string>();
            FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
            List<Faceta> lista = pGestorFacetas.ListaFacetas.Where(faceta => faceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Fecha)).ToList();

            foreach (Faceta fac in lista)
            {
                mPropiedadesFecha.Add(fac.ClaveFaceta.Substring(fac.ClaveFaceta.LastIndexOf(":") + 1));
            }

            return mPropiedadesFecha;
        }


        /// <summary>
        /// <summary>
        /// Obtiene la lista con las propiedades de tipo Fecha
        /// </summary>
        private List<string> PropiedadesFecha
        {
            get
            {
                return mPropiedadesFecha;
            }
        }
        /// <summary>
        /// Obtiene la lista de items extra que se obtendrá de la búsqueda (recetas, peliculas, etc)
        /// </summary>
        private List<string> ListaItemsBusquedaExtra
        {
            get
            {
                return mListaItemsExtra;
            }
        }

        private Dictionary<string, List<string>> InformacionOntologias
        {
            get
            {
                return mInformacionOntologias;
            }
        }

        #endregion

        public GestionFacetas GestorFacetas
        {
            get
            {
                if (mGestorFacetas == null)
                {
                    FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetaCL>(), mLoggerFactory);
                    mGestorFacetas = new GestionFacetas(facetaCL.ObtenerFacetasDeProyecto(null, ProyectoID, false));
                }
                return mGestorFacetas;
            }
        }

        /// <summary>
        /// Obtiene el mProyectoID seleccionado
        /// </summary>
        public Guid ProyectoID
        {
            get
            {
                return mProyectoID;
            }
        }

        /// <summary>
        /// Obtiene el mOrganizacionID seleccionado
        /// </summary>
        public Guid OrganizacionID
        {
            get
            {
                return mOrganizacionID;
            }
        }

        /// <summary>
        /// Parámetros de un proyecto.
        /// </summary>
        public Dictionary<string, string> ParametroProyecto
        {
            get
            {
                if (mParametroProyecto == null)
                {
                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCL>(), mLoggerFactory);
                    mParametroProyecto = proyectoCL.ObtenerParametrosProyecto(ProyectoID);
                    proyectoCL.Dispose();
                }

                return mParametroProyecto;
            }
        }

        #region Métodos generales

        /// <summary>
        /// Autocompletar para la edición/creación de un recurso, para añadir lectores o editores
        /// </summary>
        /// <param name="q">Texto de entrada</param>
        /// <param name="lista">Lista de ID ya seleccionados</param>
        /// <param name="identidad">Identidad actual</param>
        /// <param name="proyecto">ProyectoActual</param>
        /// <param name="bool_edicion">True si es para editores, false para lectores</param>
        /// <param name="traerDatos">Indica que traer: 0 Todo, 1 Solo Personas, 2 Solo Grupos, 3 Solo personas usando UsuarioID</param>
        /// <param name="grupo">El grupo es para determinar que se quieren los usuarios del grupo.</param>
        /// <param name="organizacion">ID de la organización a la que pertence el usuario o GUID.Empty si no pertenece</param>
        /// <returns></returns>
        [NonAction]
        public string AutoCompletarLectoresEditoresInt(string q, string lista, string identidad, string organizacion, string proyecto, string bool_edicion, short traerDatos, string grupo)
        {
            string[] arrayAnteriores = lista?.Split(',');
            List<string> listaAnteriores = new List<string>();
            //Recorro el array para limpiar los espacios vacios.
            if (arrayAnteriores != null)
            {
                foreach (string elementoAnt in arrayAnteriores)
                {
                    string elemento = elementoAnt.Trim();
                    if (elemento != "" && !listaAnteriores.Contains(elemento))
                    {
                        listaAnteriores.Add(elemento);
                    }
                }
            }

            Guid identidadID = new Guid(identidad);

            Guid organizacionID = Guid.Empty;
            if (!string.IsNullOrEmpty(organizacion))
            {
                organizacionID = new Guid(organizacion);
            }

            Guid proyectoID = new Guid(proyecto);

            IdentidadCL identidadCL = new IdentidadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCL>(), mLoggerFactory);
            DataWrapperIdentidad idenDS = new DataWrapperIdentidad();
            List<Guid> listaPerAnterioresID = null;

            if (traerDatos == 3)
            {
                List<Guid> listaUsuID = new List<Guid>();

                foreach (string usuID in listaAnteriores)
                {
                    listaUsuID.Add(new Guid(usuID));
                }

                PersonaCN personaCN = new PersonaCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<PersonaCN>(), mLoggerFactory);
                Dictionary<Guid, Guid> usuPerID = personaCN.ObtenerPersonasIDDeUsuariosID(listaUsuID);
                personaCN.Dispose();

                listaPerAnterioresID = new List<Guid>(usuPerID.Values);
            }


            // string busq = UtilCadenas.RemoveAccentsWithRegEx(q);

            string busq = q;
            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
            ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ProyectoCN>(), mLoggerFactory);

            //Obtenemos los perfiles
            Dictionary<Guid, Tuple<string, string, Guid?, Guid?>> listaPerfilesBusqueda = new Dictionary<Guid, Tuple<string, string, Guid?, Guid?>>();
            if (traerDatos == 0 || traerDatos == 1 || traerDatos == 3)
            {
                bool esEdicion = bool_edicion.ToUpper() == "TRUE";
                listaPerfilesBusqueda = identidadCN.ObtenerPerfilesParaAutocompletar(proyectoID, identidadID, busq, 30, !esEdicion);
            }

            DataWrapperIdentidad dwIdentidad = null;
            if (traerDatos == 0 || traerDatos == 1 || traerDatos == 3)
            {
                // Obtenemos las organizaciones que administra el usuario
                UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<UsuarioCN>(), mLoggerFactory);
                Guid usuarioID = usuarioCN.ObtenerGuidUsuarioIDporIdentidadID(identidadID);
                OrganizacionCN organizacionCN = new OrganizacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<OrganizacionCN>(), mLoggerFactory);
                List<Guid> organizacionesAdministradasUsuario = usuarioCN.ObtenerOrganizacionesAdministradasPorUsuario(usuarioID);
                // Obtenemos los usuarios que pertenecen a la organización y a esta comunidad
                dwIdentidad = identidadCN.ObtenerIdentidadesDeOrganizaciones(organizacionesAdministradasUsuario, proyectoID);

                usuarioCN.Dispose();
                organizacionCN.Dispose();
            }

            //Obtenemos los grupos
            Dictionary<Guid, string> listaGruposBusqueda = new Dictionary<Guid, string>();
            if (traerDatos == 0 || traerDatos == 2)
            {
                bool esSupervisorProyecto = proyectoCN.EsIdentidadAdministradorProyecto(new Guid(identidad), new Guid(proyecto), TipoRolUsuario.Supervisor);
                listaGruposBusqueda = identidadCN.ObtenerGruposParaAutocompletar(proyectoID, identidadID, busq, 30, esSupervisorProyecto);
            }

            //Obtenemos los grupos de Org
            List<Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.GrupoIdentidades> gruposOrgBusqueda = null;
            if (organizacionID != Guid.Empty && (traerDatos == 0 || traerDatos == 2))
            {
                DataWrapperIdentidad identidadGruposOrgDW = identidadCL.ObtenerMiembrosOrganizacionParaFiltroGrupos(organizacionID);

                gruposOrgBusqueda = identidadGruposOrgDW.ListaGrupoIdentidades.Where(grupoIden => UtilCadenas.RemoveAccentsWithRegEx(grupoIden.Nombre.ToLower()).Contains(busq.Trim().ToLower())).OrderByDescending(grupoIden => UtilCadenas.RemoveAccentsWithRegEx(grupoIden.Nombre)).ToList();
            }

            //Obtenemos los miembros de un grupo
            List<Guid> listaPerfilesGrupoBusqueda = new List<Guid>();
            if (!string.IsNullOrEmpty(grupo))
            {
                GestionIdentidades gestorIdentidades = new GestionIdentidades(identidadCL.ObtenerGrupoPorNombreCortoYProyecto(grupo, proyectoID), mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);

                if (gestorIdentidades.DataWrapperIdentidad.ListaGrupoIdentidades.Count > 0)
                {
                    Guid grupoID = gestorIdentidades.DataWrapperIdentidad.ListaGrupoIdentidades[0].GrupoID;
                    Es.Riam.Gnoss.Elementos.Identidad.GrupoIdentidades grupoIdentidades = gestorIdentidades.ListaGrupos[grupoID];

                    foreach (Es.Riam.Gnoss.Elementos.Identidad.Identidad ident in grupoIdentidades.Participantes.Values)
                    {
                        listaPerfilesGrupoBusqueda.Add(ident.PerfilID);
                    }
                }

                listaPerfilesBusqueda = listaPerfilesBusqueda.Where(p => !listaPerfilesGrupoBusqueda.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);

                gestorIdentidades.Dispose();
                gestorIdentidades = null;
            }

            proyectoCN.Dispose();


            #region Devolvemos los perfiles y grupos obtenidos
            Dictionary<Guid, string> listaPerfiles = new Dictionary<Guid, string>();
            int contador = 0;
            if (listaPerfilesBusqueda.Count > 0)
            {
                foreach (Guid idPerfil in listaPerfilesBusqueda.Keys)
                {
                    string nombrePerfil = listaPerfilesBusqueda[idPerfil].Item1;
                    if (listaPerfilesBusqueda[idPerfil].Item4.HasValue && listaPerfilesBusqueda[idPerfil].Item3.HasValue)
                    {
                        nombrePerfil = listaPerfilesBusqueda[idPerfil].Item1 + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + listaPerfilesBusqueda[idPerfil].Item2;
                    }
                    else if (listaPerfilesBusqueda[idPerfil].Item4.HasValue)
                    {
                        nombrePerfil = listaPerfilesBusqueda[idPerfil].Item2;
                    }
                    if (traerDatos != 3)
                    {
                        if (!listaAnteriores.Contains(idPerfil.ToString()) && !listaPerfilesGrupoBusqueda.Contains(idPerfil))
                        {
                            listaPerfiles.Add(idPerfil, nombrePerfil);
                            contador++;
                        }
                    }
                    else if (listaPerfilesBusqueda[idPerfil].Item3.HasValue && !listaPerAnterioresID.Contains(listaPerfilesBusqueda[idPerfil].Item3.Value))
                    {
                        listaPerfiles.Add(listaPerfilesBusqueda[idPerfil].Item3.Value, nombrePerfil);
                        contador++;
                    }
                    if (contador == 10)
                    {
                        break;
                    }
                }
            }

            if (dwIdentidad != null)
            {
                PersonaCN personaCN = new PersonaCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<PersonaCN>(), mLoggerFactory);
                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil in dwIdentidad.ListaPerfil)
                {
                    if (!listaPerfiles.ContainsKey(perfil.PerfilID) && (perfil.NombrePerfil.ToLower().Contains(busq.ToLower()) || perfil.NombrePerfil.ToLower().StartsWith(busq.ToLower())))
                    {
                        listaPerfiles.Add(perfil.PerfilID, $"{perfil.NombrePerfil} @ {perfil.NombreOrganizacion}");
                    }
                }
            }

            Dictionary<Guid, string> listaGrupos = new Dictionary<Guid, string>();
            contador = 0;
            if (listaGruposBusqueda.Count > 0 && (traerDatos == 0 || traerDatos == 2))
            {
                foreach (Guid idGrupo in listaGruposBusqueda.Keys)
                {
                    string nombreGrupo = listaGruposBusqueda[idGrupo];

                    if (!listaAnteriores.Contains("g_" + idGrupo.ToString()))
                    {
                        listaGrupos.Add(idGrupo, nombreGrupo);

                        contador++;
                        if (contador == 10)
                        {
                            break;
                        }
                    }
                }
            }

            contador = 0;
            if (gruposOrgBusqueda != null)
            {
                foreach (Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.GrupoIdentidades filaGrupo in gruposOrgBusqueda)
                {
                    OrganizacionCN orgCN = new OrganizacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<OrganizacionCN>(), mLoggerFactory);
                    string nombreOrg = orgCN.ObtenerNombreOrganizacionPorID(organizacionID).Nombre;

                    string nombreGrupo = filaGrupo.Nombre + " " + ConstantesDeSeparacion.SEPARACION_CONCATENADOR + " " + nombreOrg;

                    if (!listaAnteriores.Contains("g_" + filaGrupo.GrupoID.ToString()))
                    {
                        listaGrupos.Add(filaGrupo.GrupoID, nombreGrupo);

                        contador++;
                        if (contador == 10)
                        {
                            break;
                        }
                    }
                }
            }

            if (traerDatos == 3)
            {
                PersonaCN personaCN = new PersonaCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<PersonaCN>(), mLoggerFactory);
                Dictionary<Guid, KeyValuePair<Guid, string>> perUsuID = personaCN.ObtenerUsuariosIDYDNIsDePersonasID(new List<Guid>(listaPerfiles.Keys));
                personaCN.Dispose();

                Dictionary<Guid, string> listaPerfilesAux = new Dictionary<Guid, string>();

                foreach (Guid clave in listaPerfiles.Keys)
                {
                    string extraDNI = "";
                    if (perUsuID.ContainsKey(clave))
                    {
                        if (!string.IsNullOrEmpty(perUsuID[clave].Value))
                        {
                            extraDNI = " - " + perUsuID[clave].Value;
                        }

                        listaPerfilesAux.Add(perUsuID[clave].Key, listaPerfiles[clave] + extraDNI);
                    }
                }

                listaPerfiles = listaPerfilesAux;
            }

            string resultados = "";
            foreach (Guid clave in listaPerfiles.Keys)
            {
                resultados += listaPerfiles[clave] + "|" + clave;
                resultados += Environment.NewLine;
            }

            foreach (Guid clave in listaGrupos.Keys)
            {
                resultados += listaGrupos[clave] + "|g_" + clave;
                resultados += Environment.NewLine;
            }
            #endregion

            identidadCN.Dispose();

            return resultados;
        }

        /// <summary>
        /// Autocompletar para el buscador de administracion matomo
        /// </summary>
        /// <param name="pNombrePerfil">Nombre a buscar</param>
        /// <param name="pNumero">Numero de resultados a devolver</param>
        /// <returns></returns>
        [NonAction]
        public string AutoCompletarUsuariosMatomosInt(string pNombrePerfil, int pNumero)
        {
            Dictionary<Guid, string> listaUsuariosIDBusqueda = new Dictionary<Guid, string>();
            UsuarioCN usuarioCN = new UsuarioCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<UsuarioCN>(), mLoggerFactory);
            listaUsuariosIDBusqueda = usuarioCN.ObtenerUsuariosIDParaAutocompletar(pNombrePerfil, pNumero);
            usuarioCN.Dispose();
            string resultados = "";
            foreach (Guid clave in listaUsuariosIDBusqueda.Keys)
            {
                resultados += $"{listaUsuariosIDBusqueda[clave]}|{clave}";
                resultados += Environment.NewLine;
            }

            return resultados;
        }

        private ControladorDocumentacion mControladorDocumentacion;
        private ControladorDocumentacion ControladorDocumentacion
        {
            get
            {
                if (mControladorDocumentacion == null)
                {
                    mControladorDocumentacion = new ControladorDocumentacion(mLoggingService, mEntityContext, mConfigService, mRedisCacheWrapper, mGnossCache, mEntityContextBASE, mVirtuosoAD, mHttpContextAccessor, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<ControladorDocumentacion>(), mLoggerFactory);
                }
                return mControladorDocumentacion;
            }
        }

        /// <summary>
        /// Deprecated
        /// </summary>
        /// <param name="q"> Sentencia a autocompletar</param>
        /// <param name="lista"></param>
        /// <param name="identidad"></param>
        /// <param name="organizacion"></param>
        /// <param name="proyecto"></param>
        /// <returns></returns>
        [NonAction]
        private string ObtenerPropiedadesOntologia(string q, string lista, string identidad, string organizacion, string proyecto)
        {
            List<string> listaAnteriores = new List<string>();
            if (lista != null)
            {
                string[] arrayAnteriores = lista.Split(',');

                //Recorro el array para limpiar los espacios vacios.
                foreach (string elementoAnt in arrayAnteriores)
                {
                    string elemento = elementoAnt.Trim();
                    if (elemento != "" && !listaAnteriores.Contains(elemento))
                    {
                        listaAnteriores.Add(elemento);
                    }
                }
            }
            Guid organizacionID = Guid.Empty;
            if (!string.IsNullOrEmpty(organizacion))
            {
                organizacionID = new Guid(organizacion);
            }

            Guid proyectoID = new Guid(proyecto);

            DataWrapperDocumentacion dataWrapperDocumentacion = new DataWrapperDocumentacion();
            DocumentacionCN docCN = new DocumentacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<DocumentacionCN>(), mLoggerFactory);
            docCN.ObtenerOntologiasProyecto(proyectoID, dataWrapperDocumentacion, false, false, true);
            docCN.Dispose();

            List<string> nombresObjectProperty = new List<string>();
            List<string> nombresDatatypeProperty = new List<string>();

            List<Documento> filasDoc = dataWrapperDocumentacion.ListaDocumento.Where(doc => doc.Tipo.Equals((short)Es.Riam.Gnoss.Web.MVC.Models.TiposDocumentacion.Ontologia)).ToList();
            foreach (Documento filaDoc in filasDoc)
            {
                Dictionary<string, List<Es.Riam.Semantica.Plantillas.EstiloPlantilla>> listaEstilos = new Dictionary<string, List<Es.Riam.Semantica.Plantillas.EstiloPlantilla>>();
                byte[] arrayOnto = ControladorDocumentacion.ObtenerOntologia(filaDoc.DocumentoID, out listaEstilos, filaDoc.ProyectoID.Value, null, null, false);

                Ontologia ontologia = new Ontologia(arrayOnto, true);
                ontologia.LeerOntologia();

                List<Propiedad> listaPropiedades = ontologia.Propiedades;

                foreach (Propiedad propiedad in listaPropiedades)
                {
                    if (propiedad.Tipo.Equals(Es.Riam.Semantica.OWL.TipoPropiedad.ObjectProperty))
                    {
                        nombresObjectProperty.Add(propiedad.NombreConNamespace);
                    }
                    else if (propiedad.Tipo.Equals(Es.Riam.Semantica.OWL.TipoPropiedad.DatatypeProperty))
                    {
                        if (propiedad.RangoRelativo.Equals("string"))
                        {
                            nombresDatatypeProperty.Add(propiedad.NombreConNamespace);
                        }
                    }
                }
            }
            string resultados = "";
            List<string> opcionesAutocompletado = new List<string>();

            String propertyName = q.Split("@@@").Last();

            opcionesAutocompletado = nombresDatatypeProperty.Where(item => item.ToLower().Contains(propertyName)).Union(nombresObjectProperty.Where(item => item.ToLower().Contains(propertyName))).ToList();
            opcionesAutocompletado.AddRange(PropiedadesOntologiasBasicas.Where(item => item.ToLower().Contains(propertyName)));


            for (int i = 0; i < opcionesAutocompletado.Count; i++)
            {
                resultados += opcionesAutocompletado[i];
                resultados += Environment.NewLine;
            }
            return resultados;
        }
        [NonAction]
        private Dictionary<string, List<string>> ExtraerParametros(string pParametros)
        {
            char[] separador = { '|' };
            string[] args = pParametros.Split(separador, StringSplitOptions.RemoveEmptyEntries);

            char[] separadores = { '=' };
            Dictionary<string, List<string>> listaFiltros = new Dictionary<string, List<string>>();

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.IsNullOrEmpty(args[i]))
                {
                    string[] filtro = args[i].Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                    string key = filtro[0];
                    if (!key.Contains(":") && !key.Contains(";") && !key.StartsWith("http") && key.ToLower() != "search")
                    {
                        continue;
                    }
                    if (!listaFiltros.ContainsKey(key))
                    {
                        listaFiltros.TryAdd(key, new List<string>());
                    }
                    listaFiltros[key].Add(filtro[1]);
                }
            }

            return listaFiltros;
        }

        #endregion
    }

    public class AutocompletarLectoresEditoresModel
    {
        public string q { get; set; }
        public string lista { get; set; }
        public string identidad { get; set; }
        public string organizacion { get; set; }
        public string proyecto { get; set; }
        public string bool_edicion { get; set; }
        public string grupo { get; set; }
    }
}

