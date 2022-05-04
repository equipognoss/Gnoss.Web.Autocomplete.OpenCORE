using System;
using System.Collections.Generic;
using System.Threading;
using Es.Riam.Gnoss.Elementos.Identidad;
using Es.Riam.Gnoss.CL.Amigos;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.CL;
using System.Threading.Tasks;
using Es.Riam.AbstractsOpen;

namespace Gnoss.Web.AutoComplete
{
    /// <summary>
    /// Para cargar las identidades para mostrar en el autocompletar
    /// </summary>
    public class CargaIndentidadesAutocompletar : IDisposable
    {
        #region Miembros

        private Guid mIdentidadID;

        private Guid mIdentidadOrgID;

        private bool mCargarIdentidadesPrivadas;

        private LoggingService mLoggingService;
        private EntityContext mEntityContext;
        private ConfigService mConfigService;
        private RedisCacheWrapper mRedisCacheWrapper;
        private IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor público.
        /// </summary>
        /// <param name="pIdentidadID">ID de identidad</param>
        /// <param name="pIdentidadOrgID">ID de identidad de organización</param>
        public CargaIndentidadesAutocompletar(Guid pIdentidadID, Guid pIdentidadOrgID, bool pCargarIdentidadesPrivadas, LoggingService loggingService, EntityContext entityContext, ConfigService configService, RedisCacheWrapper redisCacheWrapper, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            mRedisCacheWrapper = redisCacheWrapper;
            mLoggingService = loggingService;
            mEntityContext = entityContext;
            mConfigService = configService;

            mIdentidadID = pIdentidadID;
            mIdentidadOrgID = pIdentidadOrgID;
            mCargarIdentidadesPrivadas = pCargarIdentidadesPrivadas;
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
        }

        #endregion

        #region Métodos

        /// <summary>
        /// Carga los datos
        /// </summary>
        public void Cargar()
        {
            Task.Factory.StartNew(_CargarAsync);
        }

        /// <summary>
        /// Captura la imagen de una web.
        /// </summary>
        private void _CargarAsync()
        {
            try
            {
                //Thread.Sleep(5000);
                Identidad identidad = null;
                IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                List<Guid> listID = new List<Guid>();
                listID.Add(mIdentidadID);
                GestionIdentidades gestorIdent = new GestionIdentidades(identCN.ObtenerIdentidadesPorID(listID, false), mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);

                identidad = gestorIdent.ListaIdentidades[mIdentidadID];

                if (mIdentidadID != mIdentidadOrgID)
                {
                    if (identidad.TrabajaConOrganizacion)
                    {
                        gestorIdent.DataWrapperIdentidad.Merge(identCN.ObtenerIdentidadDeOrganizacion(identidad.OrganizacionID.Value, ProyectoAD.MetaProyecto, false));
                        gestorIdent.RecargarHijos();
                    }
                }

                identCN.Dispose();

                if (mCargarIdentidadesPrivadas)
                {
                    CargarAmigosEIdentidadesEnMisProyectosPrivados(identidad, mIdentidadID == mIdentidadOrgID);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Carga los amigos de una identidad y los contactos de comunidades privadas.
        /// </summary>
        /// <param name="pIdentidad">Identidad a cargar los amigos</param>
        /// <param name="pCargarAmigosIdentidadOrganizacion">TRUE si se deben cargar los amigos de la organización o FALSE si se deben cargar los de la persona</param>
        public void CargarAmigosEIdentidadesEnMisProyectosPrivados(Identidad pIdentidad, bool pCargarAmigosIdentidadOrganizacion)
        {
            bool esAdministrador = false;
            if (pIdentidad.TrabajaConOrganizacion)
            {
                esAdministrador = pCargarAmigosIdentidadOrganizacion;
            }

            Guid identidad = pIdentidad.IdentidadMyGNOSS.Clave;
            if (pIdentidad.TrabajaConOrganizacion && pCargarAmigosIdentidadOrganizacion && esAdministrador)
            {
                identidad = pIdentidad.IdentidadOrganizacion.IdentidadMyGNOSS.Clave;
            }

            DataWrapperIdentidad dataWrapperIdentidad = new DataWrapperIdentidad();
            AmigosCL amigosCL = new AmigosCL(/*TODO Javier Conexion.ObtenerUrlFicheroConfigXML() + */"@@@acid", /*TODO Javier Conexion.ObtenerUrlFicheroConfigXML() + */"@@@acid", mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            if (!amigosCL.ObtenerAmigosEIdentidadesEnMisProyectosPrivados(identidad, dataWrapperIdentidad, pCargarAmigosIdentidadOrganizacion, false))
            {
                IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);

                List<Guid> identEnMisProyPriv = identidadCN.ObtenerIdentidadesIDEnMisProyectosPrivados(identidad);

                // Cargar las identidades de los amigos en el gestor
                dataWrapperIdentidad = identidadCN.ObtenerIdentidadesPorID(identEnMisProyPriv, true);
                identidadCN.Dispose();

                amigosCL.AgregarAmigosEIdentidadesEnMisProyectosPrivados(identidad, dataWrapperIdentidad, pCargarAmigosIdentidadOrganizacion);
            }
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Determina si está disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Destructor
        /// </summary>
        ~CargaIndentidadesAutocompletar()
        {
            //Libero los recursos
            Dispose(false);
        }

        /// <summary>
        /// Libera los recursos
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            //impido que se finalice dos veces este objeto
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Libera los recursos
        /// </summary>
        /// <param name="disposing">Determina si se está llamando desde el Dispose()</param>
        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
            }
        }

        #endregion
    }
}
