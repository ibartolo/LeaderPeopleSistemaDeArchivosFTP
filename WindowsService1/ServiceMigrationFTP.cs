using WindowsService1;
using WindowsService1.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Timers;
using Serilog;

namespace WindowsService1
{
    public partial class ServiceMigrationFTP : ServiceBase
    {
        private Timer timer;
        public ServiceMigrationFTP()
        {
            InitializeComponent();
        }

        #region OnStart
        protected override void OnStart(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(ConfigurationManager.AppSettings["Logs"],
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
                .WriteTo.Console() // opcional, útil para depurar
                .CreateLogger();

            Log.Information("Servicio iniciado.");
            Log.Information("Servicio iniciado a las {Hora}", DateTime.Now.ToString("HH:mm:ss"));

            timer = new Timer();
            timer.AutoReset = false; // Importante: control manual del reinicio
            timer.Elapsed += Timer_Elapsed;

            bool debugMode = Convert.ToBoolean(ConfigurationManager.AppSettings["DebugMode"]);
            bool testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);

            if (testMode)
            {
                // Modo pruebas: se ejecuta una sola vez, en 5 segundos (para dar tiempo a que el servicio termine de iniciar)
                Log.Information("Modo pruebas activado: se ejecutará una única vez.");
                timer.Interval = 5000; // 5 segundos
            }
            else if (debugMode)
            {
                // Modo debug: ejecución diaria a la hora configurada
                Log.Information("Modo debug activado: ejecución diaria a las {Hora}", ConfigurationManager.AppSettings["HoraEjecucion"]);
                TimeSpan intervalo = CalcularProximoDisparoDiario(TimeSpan.Parse(ConfigurationManager.AppSettings["HoraEjecucion"]));
                timer.Interval = intervalo.TotalMilliseconds;
            }
            else
            {
                // Modo normal (ejemplo: cada 24 horas o como lo tenías antes)
                Log.Information("Modo normal: ejecución diaria a las 11:20 (o según tu lógica)");
                timer.Interval = CalcularProximoDisparoDiario(new TimeSpan(11, 20, 0)).TotalMilliseconds;
            }

            timer.Start();
        }
        
        #endregion
        
        #region OnStop
        protected override void OnStop()
        {
            Log.Information("Servicio detenido.");
            Log.CloseAndFlush();
            timer?.Stop();
            timer?.Dispose();
        }
        #endregion

        #region Timer Elapsed
        protected void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            
            // Detener el timer mientras se ejecuta para evitar solapamientos
            timer.Stop();

            try
            {
                EjecutarTarea(); // Aquí va toda la lógica que ya tienes en la consola (server, pass, rutas, etc.)
            }
            catch (Exception ex)
            {
                // Registrar error (puedes usar EventLog)
                Log.Error(ex, "Error en servicio: {Message}", ex.Message);

            }
            finally
            {
                // Reconfigurar el timer según el modo
                bool testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
                bool debugMode = Convert.ToBoolean(ConfigurationManager.AppSettings["DebugMode"]);

                if (testMode)
                {
                    // Modo pruebas: no se reprograma, el servicio queda detenido
                    Log.Information("Modo pruebas: ejecución única completada. El servicio no se volverá a ejecutar hasta reiniciar.");
                    // No se llama a timer.Start()
                }
                else if (debugMode)
                {
                    // Modo debug: reprogramar para la próxima 6 AM
                    TimeSpan intervalo = CalcularProximoDisparoDiario(TimeSpan.Parse(ConfigurationManager.AppSettings["HoraEjecucion"]));
                    timer.Interval = intervalo.TotalMilliseconds;
                    timer.Start();
                    Log.Information("Próxima ejecución programada para las {Hora}", DateTime.Now.Add(intervalo).ToString("HH:mm"));
                }
                else
                {
                    // Modo normal: reprogramar según tu lógica anterior
                    TimeSpan intervalo = CalcularProximoDisparoDiario(new TimeSpan(11, 20, 0));
                    timer.Interval = intervalo.TotalMilliseconds;
                    timer.Start();
                }
            }
        }
        #endregion

        #region Calcular proximo disparo
        private TimeSpan CalcularProximoDisparo()
        {
            //Obtener la fecha y hora actual para calcular el tiempo restante hasta el próximo primer día del mes a las 5:00 AM
            DateTime ahora = DateTime.Now; //dia y hora actual
                                           //Próximo primer día del mes a las 5:00 AM
                                           //(Obtenemos el primer dia del mes actual a las 5:00 AM).Lugo le agregamos un mes para obtener el próximo primer día del mes a las 5:00 AM
            DateTime proximo = new DateTime(ahora.Year, ahora.Month, 1, 6, 0, 0).AddMonths(1);

            //Si ya pasó el primer día del mes a las 5 AM, pasa al mes siguiente
            if (ahora > proximo)
                proximo = proximo.AddMonths(1);

            return proximo - ahora; //Tiempo restante para el próximo disparo

        }
        #endregion

        #region Proximo dispara para pruebas
        private TimeSpan CalcularProximoDisparoDiario(TimeSpan horaEjecucion)
        {
            DateTime ahora = DateTime.Now;
            DateTime proximo = new DateTime(ahora.Year, ahora.Month, ahora.Day, horaEjecucion.Hours, horaEjecucion.Minutes, 0);

            if (proximo <= ahora)
                proximo = proximo.AddDays(1);

            return proximo - ahora;
        }
        #endregion

        private void EjecutarTarea()
        {
            Log.Information("=== INICIO DE PROCESO DE DESCARGA ===");
            DateTime Inicio = DateTime.Now;
            Log.Information("Proceso iniciado a las {inicio:yyyy-MM-dd HH:mm:ss}", Inicio);
            // Calcular fechas
            DateTime fechaInicio, fechaFin;
            string periodo = null;

            try
            {
                // Cargar configuración (igual que antes)
                string ftpServer = ConfigurationManager.AppSettings["ftpServerTo"];
                string username = ConfigurationManager.AppSettings["ftpUserNameTo"];
                string password = ConfigurationManager.AppSettings["ftpPasswordTo"];
                string downloadPath = ConfigurationManager.AppSettings["downloadPath"];
                bool deleteAfter = Convert.ToBoolean(ConfigurationManager.AppSettings["deleteAfterDownload"]);
                //string periodo = ConfigurationManager.AppSettings["PeriodoMigracion"]; // Ejemplo: "202602"

                //Va a obtener el periodo de abril 2024, luego de ejecutar el proceso, va a registrar el periodo de mayo 2024 para que se ejecute la próxima vez.
                periodo = MigrationFTP.ObtenerProximoPeriodo(); //si tiene 202404, descargara lo de mayo  registrara
                // Log de parámetros
                Log.Information("Servidor FTP: {ftpServer}", ftpServer);
                Log.Information("Período: {periodo}", periodo);
                Log.Information("Borrar después de descargar: {deleteAfter}", deleteAfter);

                
                if (!string.IsNullOrEmpty(periodo) && periodo.Length == 6)
                {
                    int año = int.Parse(periodo.Substring(0, 4));
                    int mes = int.Parse(periodo.Substring(4, 2));
                    fechaInicio = new DateTime(año, mes, 1, 0, 0, 0);
                    fechaFin = fechaInicio.AddMonths(1).AddSeconds(-1);
                }
                else
                {
                    // Valor por defecto (o podrías tomar el mes actual)
                    fechaInicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    fechaFin = fechaInicio.AddMonths(1).AddSeconds(-1);
                    periodo = fechaInicio.ToString("yyyyMM"); // Actualizar periodo
                    Log.Warning("Período no válido, se usará mes actual: {periodo}", periodo);
                }
                // ... cálculo (igual que antes)
                Log.Information("Rango de fechas: {fechaInicio} a {fechaFin}", fechaInicio.ToString("yyyy-MM-dd HH:mm:ss"), fechaFin.ToString("yyyy-MM-dd HH:mm:ss"));

                // Consultar archivos en BD
                List<AttachmentProvider> lista = MigrationFTP.ConsultarArchivosBaseDatos(
                    fechaInicio.ToString("yyyy-MM-dd HH:mm:ss"), fechaFin.ToString("yyyy-MM-dd HH:mm:ss"));
                Log.Information("Archivos encontrados en BD: {cantidad}", lista.Count);

                if (lista.Count > 0)
                {
                    // Contar archivos existentes en FTP
                    int existentes = MigrationFTP.ContarArchivosExistentesEnFTP(fechaInicio.ToString("yyyy-MM-dd HH:mm:ss"), fechaFin.ToString("yyyy-MM-dd HH:mm:ss"));
                    Log.Information("Archivos encontrados en la base de datos: {existentes}", existentes);

                    // Descargar archivos (algo pasa con el correo)
                    MigrationFTP.DescargarListaArchivos(ftpServer, username, password, lista, downloadPath, deleteAfter, periodo, fechaInicio, fechaFin);
                }
                else
                {
                    Log.Warning("No se encontraron archivos en la BD para el período.");
                }

                //MigrationFTP.ActualizarBaseUrl(fechaInicio.ToString("yyyy-MM-dd HH:mm:ss"), fechaFin.ToString("yyyy-MM-dd HH:mm:ss"));
                //Enviamos el periodo que ya ha sido descargado.
                MigrationFTP.RegistrarSiguientePeriodo(periodo);
                Log.Information("Período {periodo} registrado y BaseUrl actualizada.", periodo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error general en el proceso de descarga.");
            }
            finally
            {
                DateTime fin = DateTime.Now;
                TimeSpan duracion = fin - Inicio;
                Log.Information("Hora de fin: {fin:yyyy-MM-dd HH:mm:ss}", fin);
                Log.Information("Duración total: {duracion:hh\\:mm\\:ss}", duracion);
                Log.Information("=== FIN DE PROCESO DE DESCARGA ===");
            }
        }
    }
}
