using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Elementos.ParametroAplicacion;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Util.Seguridad;
using System.Linq;
using Es.Riam.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.Recursos;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using ServicioAutoCompletarMVC;
using Es.Riam.Gnoss.Web.Controles.ParametroAplicacionGBD;
using Es.Riam.Gnoss.AD.Facetado;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Es.Riam.AbstractsOpen;
using Es.Riam.OpenReplication;
using Es.Riam.Gnoss.CL.RelatedVirtuoso;
using Es.Riam.Gnoss.UtilServiciosWeb;

namespace Gnoss.Web.AutoComplete
{
    public class Startup
    {
        public Startup(IConfiguration configuration, Microsoft.AspNetCore.Hosting.IHostingEnvironment environment)
        {
            Configuration = configuration;
            mEnvironment = environment;
        }

        public IConfiguration Configuration { get; }
        public Microsoft.AspNetCore.Hosting.IHostingEnvironment mEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)

        {
			ILoggerFactory loggerFactory =
			LoggerFactory.Create(builder =>
			{
				builder.AddConfiguration(Configuration.GetSection("Logging"));
				builder.AddSimpleConsole(options =>
				{
					options.IncludeScopes = true;
					options.SingleLine = true;
					options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
					options.UseUtcTimestamp = true;
				});
			});

			services.AddSingleton(loggerFactory);
			services.AddCors(options =>
            {
                options.AddPolicy(name: "_myAllowSpecificOrigins",
                                  builder =>
                                  {
                                      builder.SetIsOriginAllowed(UtilServicios.ComprobarDominioPermitidoCORS);
                                      builder.AllowAnyHeader();
                                      builder.AllowAnyMethod();
                                      builder.AllowCredentials();
                                  });
            });

            services.AddControllers();
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddScoped(typeof(UtilTelemetry));
            services.AddScoped(typeof(Usuario));
            services.AddScoped(typeof(UtilPeticion));
            services.AddScoped(typeof(Conexion));
            services.AddScoped(typeof(UtilGeneral));
            services.AddScoped(typeof(LoggingService));
            services.AddScoped(typeof(RedisCacheWrapper));
            services.AddScoped(typeof(Configuracion));
            services.AddScoped(typeof(GnossCache));
            services.AddScoped(typeof(VirtuosoAD));
            services.AddScoped(typeof(RelatedVirtuosoCL));
            services.AddScoped<IServicesUtilVirtuosoAndReplication, ServicesVirtuosoAndBidirectionalReplicationOpen>();
            string bdType = "";
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            if (environmentVariables.Contains("connectionType"))
            {
                bdType = environmentVariables["connectionType"] as string;
            }
            else
            {
                bdType = Configuration.GetConnectionString("connectionType");
            }
            if (bdType.Equals("2") || bdType.Equals("1"))
            {
                services.AddScoped(typeof(DbContextOptions<EntityContext>));
                services.AddScoped(typeof(DbContextOptions<EntityContextBASE>));
            }
            services.AddSingleton(typeof(ConfigService));
            string acid = "";
            if (environmentVariables.Contains("acid"))
            {
                acid = environmentVariables["acid"] as string;
            }
            else
            {
                acid = Configuration.GetConnectionString("acid");
            }
            string baseConnection = "";
            if (environmentVariables.Contains("base"))
            {
                baseConnection = environmentVariables["base"] as string;
            }
            else
            {
                baseConnection = Configuration.GetConnectionString("base");
            }
            if (bdType.Equals("0"))
            {
                services.AddDbContext<EntityContext>(options =>
                        options.UseSqlServer(acid)
                        );
                services.AddDbContext<EntityContextBASE>(options =>
                        options.UseSqlServer(baseConnection)

                        );
            }
			else if (bdType.Equals("1"))
			{
				services.AddDbContext<EntityContext, EntityContextOracle>(options =>
						options.UseOracle(acid)
						);
				services.AddDbContext<EntityContextBASE, EntityContextBASEOracle>(options =>
						options.UseOracle(baseConnection)

						);
			}
			else if (bdType.Equals("2"))
            {
                services.AddDbContext<EntityContext, EntityContextPostgres>(opt =>
                {
                    var builder = new NpgsqlDbContextOptionsBuilder(opt);
                    builder.SetPostgresVersion(new Version(9, 6));
                    opt.UseNpgsql(acid);

                });
                services.AddDbContext<EntityContextBASE, EntityContextBASEPostgres>(opt =>
                {
                    var builder = new NpgsqlDbContextOptionsBuilder(opt);
                    builder.SetPostgresVersion(new Version(9, 6));
                    opt.UseNpgsql(baseConnection);

                });
            }
            var sp = services.BuildServiceProvider();

            // Resolve the services from the service provider
            var configService = sp.GetService<ConfigService>();

            BaseCL.UsarCacheLocal = UsoCacheLocal.Siempre;

            string configLogStash = configService.ObtenerLogStashConnection();
            if (!string.IsNullOrEmpty(configLogStash))
            {
                LoggingService.InicializarLogstash(configLogStash);
            }
            var loggingService = sp.GetService<LoggingService>();
            var entity = sp.GetService<EntityContext>();
			var redisCacheWrapper = sp.GetService<RedisCacheWrapper>();
			var servicesUtilVirtuosoAndReplication = sp.GetService<IServicesUtilVirtuosoAndReplication>();
			//Establezco la ruta del fichero de error por defecto
			LoggingService.RUTA_DIRECTORIO_ERROR = Path.Combine(mEnvironment.ContentRootPath, "logs");

            EstablecerDominioCache(entity);

			UtilServicios.CargarIdiomasPlataforma(entity, loggingService, configService, servicesUtilVirtuosoAndReplication, redisCacheWrapper);

			CargarTextosPersonalizadosDominio(entity, loggingService, configService, redisCacheWrapper);

            CargarConfiguracionFacetado(loggingService, entity, configService);

            ConfigurarApplicationInsights(configService);

			UtilServicios.CargarDominiosPermitidosCORS(entity);
			services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gnoss.Web.AutoComplete", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gnoss.Web.AutoComplete v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors();

            app.UseAuthorization();
            app.UseGnossMiddleware();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private bool ComprobarDominioEnBD(string dominio)
        {
            return true;
        }

        /// <summary>
        /// Establece el dominio de la cache.
        /// </summary>
        private void EstablecerDominioCache(EntityContext entity)
        {
            string dominio = entity.ParametroAplicacion.FirstOrDefault(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).Valor;

            dominio = dominio.Replace("http://", "").Replace("https://", "").Replace("www.", "");

            if (dominio[dominio.Length - 1] == '/')
            {
                dominio = dominio.Substring(0, dominio.Length - 1);
            }

            BaseCL.DominioEstatico = dominio;
        }


        private void ConfigurarApplicationInsights(ConfigService configService)
        {
            string valor = configService.ObtenerImplementationKeyAutocompletar();

            if (!string.IsNullOrEmpty(valor))
            {
                Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.Active.InstrumentationKey = valor.ToLower();
            }

            if (UtilTelemetry.EstaConfiguradaTelemetria)
            {
                //Configuración de los logs
                string ubicacionLogs = configService.ObtenerUbicacionLogsAutocompletar();

                int valorInt = 0;
                if (int.TryParse(ubicacionLogs, out valorInt))
                {
                    if (Enum.IsDefined(typeof(UtilTelemetry.UbicacionLogsYTrazas), valorInt))
                    {
                        LoggingService.UBICACIONLOGS = (UtilTelemetry.UbicacionLogsYTrazas)valorInt;
                    }
                }

                //Configuración de las trazas
                string ubicacionTrazas = configService.ObtenerUbicacionTrazasAutocompletar();

                int valorInt2 = 0;
                if (int.TryParse(ubicacionTrazas, out valorInt2))
                {
                    if (Enum.IsDefined(typeof(UtilTelemetry.UbicacionLogsYTrazas), valorInt2))
                    {
                        LoggingService.UBICACIONTRAZA = (UtilTelemetry.UbicacionLogsYTrazas)valorInt2;
                    }
                }
            }
        }

        private void CargarTextosPersonalizadosDominio(EntityContext context, LoggingService loggingService, ConfigService configService, RedisCacheWrapper redisCacheWrapper)
        {
            string dominio = mEnvironment.ApplicationName;
            Guid personalizacionEcosistemaID = Guid.Empty;
            List<ParametroAplicacion> parametrosAplicacionPers = context.ParametroAplicacion.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.PersonalizacionEcosistemaID.ToString())).ToList();
            if (parametrosAplicacionPers.Count > 0)
            {
                personalizacionEcosistemaID = new Guid(parametrosAplicacionPers[0].Valor.ToString());
            }
            UtilIdiomas utilIdiomas = new UtilIdiomas("", loggingService, context, configService, redisCacheWrapper);
            utilIdiomas.CargarTextosPersonalizadosDominio(dominio, personalizacionEcosistemaID);
        }

        private void CargarConfiguracionFacetado(LoggingService loggingService, EntityContext context, ConfigService configService)
        {
            ParametroAplicacionGBD llenadoGestor = new ParametroAplicacionGBD(loggingService, context, configService);
            GestorParametroAplicacion gestorParametroAplicacion = new GestorParametroAplicacion();
            llenadoGestor.ObtenerConfiguracionGnoss(gestorParametroAplicacion);
            ParametroAplicacion filaParametroAplicacion = gestorParametroAplicacion.ParametroAplicacion.FirstOrDefault(item => item.Parametro.Equals("EscaparComillasDobles"));

            if (filaParametroAplicacion != null)
            {
                if (filaParametroAplicacion.Valor.Equals("true"))
                {
                    FacetadoAD.EscaparComillasDoblesEstatica = true;
                }
                else
                {
                    FacetadoAD.EscaparComillasDoblesEstatica = false;
                }
            }
        }
    }
}
