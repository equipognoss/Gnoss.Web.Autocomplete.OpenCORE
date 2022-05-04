using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.Logica.AutoCompletarNombres;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.UtilServiciosWeb;
using System;
using System.Collections.Generic;

namespace Gnoss.Web.AutoComplete
{
    /// <summary>
    /// Servicio para autocompletar.
    /// </summary>
    public class ServicioAutocompletar
    {

        private UtilServicios mUtilServicios;
        private LoggingService mLoggingService;
        private EntityContext mEntityContext;
        private ConfigService mConfigService;
        private IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;

        public ServicioAutocompletar(UtilServicios utilServicios, LoggingService loggingService, EntityContext entityContext, ConfigService configService, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            mUtilServicios = utilServicios;
            mLoggingService = loggingService;
            mEntityContext = entityContext;
            mConfigService = configService;
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
        }

        #region Miembros

        /// <summary>
        /// Diccionario que contiene las facetas que deben leerse y escribirse en cache y hasta que letra se debe hacer.
        /// </summary>
        public static Dictionary<string, int> FacetaLetraCache;

        #endregion

        #region Métodos

        public Dictionary<Guid, string> RealizarConsulta(string pFiltro, Guid pIdentidad, Guid pProyecto)
        {
            Dictionary<Guid, string> resultados = new Dictionary<Guid, string>();

            try
            {
                ControladorAutoCompletarHilo contrHilo = new ControladorAutoCompletarHilo(pFiltro, pIdentidad, pProyecto, 5, mUtilServicios, mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);
                resultados = contrHilo.ObtenerResultados();

            }
            catch (Exception ex)
            {
                mUtilServicios.GuardarLog("Error: " + ex.Message + "\r\nPila: " + ex.StackTrace, "error");
            }

            return resultados;
        }

        #endregion
    }

    public class ControladorAutoCompletarHilo
    {
        #region Miembros

        /// <summary>
        /// Filtro búsqueda.
        /// </summary>
        private string mFiltro;

        /// <summary>
        /// ID de proyecto.
        /// </summary>
        private Guid mProyectoID;

        /// <summary>
        /// ID de la identidad que busca.
        /// </summary>
        private Guid mIdentidad;

        /// <summary>
        /// Número de resultados.
        /// </summary>
        private int mNumResultados;

        private UtilServicios mUtilServicios;

        private LoggingService mLoggingService;
        private EntityContext mEntityContext;
        private ConfigService mConfigService;
        private IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pFiltro">Filtro</param>
        /// <param name="pProyecto">ID de proyecto</param>
        /// <param name="pTablaPropia">Indica si el proyecto tiene tabla propia</param>
        /// <param name="pFaceta">Faceta de búsqueda</param>
        /// <param name="pResultados">Resultados finales</param>
        /// <param name="pOrigen">Origen de los datos</param>
        /// <param name="pIdentidad">ID de la identidad que busca</param>
        /// <param name="pNumResultados">Número de resultados</param>
        public ControladorAutoCompletarHilo(string pFiltro, Guid pIdentidad, Guid pProyecto, int pNumResultados, UtilServicios utilServicios, LoggingService loggingService, EntityContext entityContext, ConfigService configService, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            mFiltro = pFiltro;
            mProyectoID = pProyecto;
            mIdentidad = pIdentidad;
            mNumResultados = pNumResultados;
            mUtilServicios = utilServicios;
            mLoggingService = loggingService;
            mConfigService = configService;
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
        }

        #endregion

        #region Métodos

        /// <summary>
        /// Obtiene los resultados de autocompletar para un filtro.
        /// </summary>
        /// <returns>resultados de autocompletar para un filtro</returns>
        public Dictionary<Guid, string> ObtenerResultados()
        {
            Dictionary<Guid, string> resultados = new Dictionary<Guid, string>();

            try
            {
                AutoCompletarNombresCN autoCompetarCN = null;
                autoCompetarCN = new AutoCompletarNombresCN("acid", mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);

                //(string pFiltro, string pIdentidad, string pidentidadOrg, string pProyecto)
                resultados = autoCompetarCN.ObtenerNombresAutocompletar(mFiltro, mIdentidad, mProyectoID, mNumResultados);
                autoCompetarCN.Dispose();
            }
            catch (Exception ex)
            {
                try
                {
                    resultados = new Dictionary<Guid, string>();
                    mUtilServicios.GuardarLog("Error: " + ex.Message + "\r\nPila: " + ex.StackTrace, "error");
                }
                catch (Exception) { }
            }

            return resultados;
        }

        #endregion
    }
}
