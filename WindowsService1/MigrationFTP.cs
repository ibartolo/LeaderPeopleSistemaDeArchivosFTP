using System;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsService1.Models;
using Serilog;
using System.Net.Mail;

namespace WindowsService1
{
    public class MigrationFTP
    {

        #region subir archivo al ftp
        public static void SubirArchivo(string server, string user, string pass, string filename, string path)
        {
            try
            {
                //Crear el objeto FtpWebRequest para subir el archivo
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(server + filename);
                request.Method = WebRequestMethods.Ftp.UploadFile;

                //Agregar las credenciales de autenticación
                request.Credentials = new NetworkCredential(user, pass);

                //Leer el archivo a subir
                byte[] fileContents;
                using (StreamReader reader = new StreamReader(path))
                {
                    fileContents = Encoding.UTF8.GetBytes(reader.ReadToEnd());
                }

                request.ContentLength = fileContents.Length;

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(fileContents, 0, fileContents.Length);
                }

                //Obtener la respuesta del servidor
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    Console.WriteLine($"La operacion fue exitosa: {response.StatusCode}");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al subir el archivo: {ex.Message}");
            }
        }
        #endregion

        #region Descargar el archivo del ftp
        public static void DescargarArchivo(string server, string user, string pass, string filename, string savePath)
        {
            try
            {
                string baseServer = server.TrimEnd('/') + "/";
                string cleanFilename = filename.TrimStart('/');
                string fullUri = baseServer + cleanFilename;

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fullUri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(user, pass);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream fileStream = new FileStream(savePath, FileMode.Create))
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                    }
                    Console.WriteLine($"Descargado: {filename}");
                }
                Log.Information("Archivo descargado correctamente: {filename}", filename);
            }
            catch(Exception ex)
            {
                // Omitir archivo no encontrado y continuar
                Log.Error("Error al descargar el archivo {filename}: {mensaje}", filename, ex.Message);
                throw;
            }

        }
        #endregion

        #region Eliminar un archivo del ftp
        public static void EliminarArchivo(string server, string filename, string user, string pass)
        {
            try
            {
                //Creamos el objeto
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(server + filename);
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(user, pass);

                //Obtener la respuesta del server
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    Log.Information("Archivo eliminado: {StatusCode}", response.StatusCode);
                }

            }
            catch (Exception ex)
            {
                Log.Error($"Error al eliminar el archivo: {ex.Message}");
            }
        }
        #endregion

        #region Listar archivos
        public static List<AttachmentProvider> ListarArchivosEnCarpeta(string server, string user, string pass, string remotePath)
        {
            List<AttachmentProvider> archivos = new List<AttachmentProvider>();
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(server + remotePath);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(user, pass);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            //crear el attachment provider para cada archivo encontrado
                            AttachmentProvider archivo = new AttachmentProvider
                            {
                                Name = line,
                                FullName = $"{remotePath}/{line}",
                                BaseUrl = server,
                                Id = 1, //Asignar un ID único si es necesario
                                CreatedBy = "ftpVictor",
                                CreatedDt = DateTime.Now,
                                UpdatedBy = "ftpVictor",
                                UpdatedDt = DateTime.Now,
                                Status = true
                            };
                            archivos.Add(archivo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"El error al listar los archivos es: {ex.Message}");
            }
            return archivos;
        }
        #endregion

        #region Descargar lista de archivos
        public static void DescargarListaArchivos(string server, string user, string pass, List<AttachmentProvider> archivos, string downloadPath, bool AfterDelete, string periodo, DateTime Inicio, DateTime Fin)
        {
            //le ingresaremos un log
            //Console.WriteLine("¿Borrar archivos del servidor después de descargarlos? (s/n)");
            //bool deleteAfter = Console.ReadLine().Trim().ToLower() == "s";
            //Log.Information("¿Borrar archivos del servidor después de descargarlos? {deleteAfter}", deleteAfter);
            Log.Information("Iniciando descarga de {cantidad} archivos.", archivos.Count);
            int exitosos = 0, fallidos = 0;

            foreach (var archivo in archivos)
            {
                // Construir la ruta local completa donde se guardará el archivo
                string rutaLocalCompleta = Path.Combine(downloadPath, archivo.FullName);
                // Crear el directorio si no existe
                string directorioLocal = Path.GetDirectoryName(rutaLocalCompleta);
                if (!Directory.Exists(directorioLocal))
                    Directory.CreateDirectory(directorioLocal);

                try
                {
                    DescargarArchivo(server, user, pass, archivo.FullName, rutaLocalCompleta);
                    exitosos++;
                    Log.Information("OK: {archivo}", archivo.FullName);

                    if (AfterDelete)
                    {
                        EliminarArchivo(server, archivo.FullName, user, pass);
                        Log.Information($"Archivo {archivo.FullName} eliminado del servidor FTP.");
                    }
                }
                catch (Exception ex)
                {
                    fallidos++;
                    Log.Error(ex, "FALLO: {archivo}", archivo.FullName);
                }
            }
            Log.Information("Descarga finalizada. Total: {total}, Exitosos: {exitosos}, Fallidos: {fallidos}", archivos.Count, exitosos, fallidos);
            // Dentro de DescargarListaArchivos, antes de llamar al correo:
            TimeSpan duracion = Fin - Inicio;
            string rutaLog = Path.Combine(ConfigurationManager.AppSettings["Logs"], $"log-{DateTime.Now:yyyyMMdd}.txt");
            EnviarCorreoNotificacion(periodo, Inicio, Fin, archivos.Count, exitosos, fallidos, duracion, rutaLog);
        }
        #endregion

        #region Consultar archivos a la base de datos
        public static List<AttachmentProvider> ConsultarArchivosBaseDatos(string fechaInicio, string fechaFin)
        {
            string conexion = ConfigurationManager.ConnectionStrings["cCon"].ConnectionString;
            string consulta = ConfigurationManager.AppSettings["Consultar"];
            var list = new List<AttachmentProvider>();
            string prefijo = @"prod\applicationmvc\"; 
            try
            {
                using (var conn = new SqlConnection(conexion))
                using (var cmd = new SqlCommand(consulta, conn))
                {
                    cmd.Parameters.AddWithValue("@inicio", fechaInicio);
                    cmd.Parameters.AddWithValue("@fin", fechaFin);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var obj = new AttachmentProvider();
                            obj.Id = reader.GetInt64(reader.GetOrdinal("Id")); // bigint → long
                            obj.Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader.GetString(reader.GetOrdinal("Name"));
                            string fullNameBD = reader.IsDBNull(reader.GetOrdinal("FullName")) ? null : reader.GetString(reader.GetOrdinal("FullName"));
                            obj.FullName = fullNameBD != null ? prefijo + fullNameBD.TrimStart('/') : null;
                            obj.PaymentRequestID = reader.IsDBNull(reader.GetOrdinal("PaymentRequestID")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("PaymentRequestID"));
                            obj.PurchaseOrderID = reader.IsDBNull(reader.GetOrdinal("PurchaseOrderID")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("PurchaseOrderID"));
                            obj.CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? null : reader.GetString(reader.GetOrdinal("CreatedBy"));
                            obj.CreatedDt = reader.IsDBNull(reader.GetOrdinal("CreatedDt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CreatedDt"));
                            obj.UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetString(reader.GetOrdinal("UpdatedBy"));
                            obj.UpdatedDt = reader.IsDBNull(reader.GetOrdinal("UpdatedDt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("UpdatedDt"));
                            obj.Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("Status"));
                            obj.TypeAttachment = reader.IsDBNull(reader.GetOrdinal("TypeAttachment")) ? null : reader.GetString(reader.GetOrdinal("TypeAttachment"));
                            obj.RefundOrderID = reader.IsDBNull(reader.GetOrdinal("RefundOrderID")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("RefundOrderID"));
                            obj.BillingOrderID = reader.IsDBNull(reader.GetOrdinal("BillingOrderID")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("BillingOrderID"));
                            obj.BaseUrl = reader.IsDBNull(reader.GetOrdinal("BaseUrl")) ? null : reader.GetString(reader.GetOrdinal("BaseUrl"));
                            obj.EmployeeID = reader.IsDBNull(reader.GetOrdinal("EmployeeID")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("EmployeeID"));
                            list.Add(obj);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error al consultar la base de datos: {ex.Message}", ex.Message);
            }
            return list;
        }
        #endregion

        #region Contar Archivos existentes
        public static int ContarArchivosExistentesEnFTP(string server, string user, string pass, List<AttachmentProvider> archivos)
        {
            int count = 0;
            string baseServer = server.TrimEnd('/') + "/";

            foreach (var archivo in archivos)
            {
                try
                {
                    string cleanFilename = archivo.FullName.TrimStart('/');
                    string fullUri = baseServer + cleanFilename;

                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fullUri);
                    request.Method = WebRequestMethods.Ftp.GetFileSize; // Solo verifica existencia
                    request.Credentials = new NetworkCredential(user, pass);

                    using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                    {
                        // Si llega aquí, el archivo existe
                        count++;
                        Log.Information("Archivo existente en FTP: {archivo}", archivo.FullName);
                    }
                }
                catch(Exception ex)
                {
                    // Archivo no existe, ignorar
                    Log.Warning("Archivo no encontrado en FTP: {archivo}", archivo.FullName);
                }
            }
            Log.Information("Total archivos existentes en FTP: {count} de {total}", count, archivos.Count);
            return count;
        }
        #endregion

        #region Enviar correo de notificación
        public static void EnviarCorreoNotificacion(string periodo, DateTime inicio, DateTime fin, int totalArchivos, int exitosos, int fallidos, TimeSpan duracion, string rutaLog)
        {
            try
            {
                // Leer configuración de correo
                string smtpServer = ConfigurationManager.AppSettings["smtpClient"];
                int port = int.Parse(ConfigurationManager.AppSettings["port"]);
                string userEmail = ConfigurationManager.AppSettings["userEmail"];
                string passEmail = ConfigurationManager.AppSettings["passEmail"];
                string destinatarios = ConfigurationManager.AppSettings["destinations"]; // Si lo tienes, si no, usa el mismo userEmail o agrega la clave
                
                // Leer el template HTML (ajusta la ruta si es necesario)
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "template.html");
                if (!File.Exists(templatePath))
                {
                    Log.Error("No se encontró el template de correo en: {templatePath}", templatePath);
                    return;
                }
                string template = File.ReadAllText(templatePath);

                int porcentaje = totalArchivos > 0 ? (exitosos * 100 / totalArchivos) : 0;

                // Reemplazar placeholders
                string cuerpo = template
                    .Replace("_Periodo", periodo)
                    .Replace("_FechaInicio", inicio.ToString("dd/MM/yyyy HH:mm:ss"))
                    .Replace("_FechaFin", fin.ToString("dd/MM/yyyy HH:mm:ss"))
                    .Replace("_TotalArchivos", totalArchivos.ToString())
                    .Replace("_Exitosos", exitosos.ToString())
                    .Replace("_Fallidos", fallidos.ToString())
                    .Replace("_PorcentajeExito", porcentaje.ToString())
                    .Replace("_RutaLog", rutaLog)
                    .Replace("_Duracion", duracion.ToString(@"hh\:mm\:ss"));

                // Crear el mensaje
                using (MailMessage mail = new MailMessage())
                using (SmtpClient smtp = new SmtpClient(smtpServer, port))
                {
                    if (!string.IsNullOrEmpty(destinatarios))
                    {
                        foreach (string correo in destinatarios.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            mail.To.Add(correo.Trim());
                        }
                    }
                    else
                    {
                        // Si no hay destinatarios, no se envía
                        Log.Warning("No hay destinatarios configurados para el correo.");
                        return;
                    }
                    //origen y destino
                    mail.From = new MailAddress(userEmail, "DESI - Sistema de Migración");

                    mail.Subject = $"Migración FTP - Período {ConfigurationManager.AppSettings["PeriodoMigracion"]}";
                    mail.Body = cuerpo;
                    mail.IsBodyHtml = true;

                    smtp.EnableSsl = true;
                    smtp.Credentials = new NetworkCredential(userEmail, passEmail);
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Timeout = 10000; // 10 segundos

                    smtp.Send(mail);
                    Log.Information("Correo de notificación enviado exitosamente a {destinatario}", destinatarios ?? userEmail);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al enviar el correo de notificación");
            }
        }
        #endregion

        #region Consultar periodo de migración
        public static string ObtenerProximoPeriodo()
        {
            string conexion = ConfigurationManager.ConnectionStrings["cCon"].ConnectionString;
            string consulta = "SELECT TOP 1 Periodo FROM DateMigration ORDER BY Periodo DESC"; //Ingresa el registro mas reciente 
            using (var conn = new SqlConnection(conexion))
            using (var cmd = new SqlCommand(consulta, conn))
            {
                conn.Open();
                var result = cmd.ExecuteScalar();
                if (result == null)
                {
                    // Si no hay registros, empezar por un período por defecto (ej. el actual)
                    DateTime hoy = DateTime.Now;
                    return hoy.ToString("yyyyMM");
                }
                else
                {
                    string ultimoPeriodo = result.ToString();
                    // Convertir a fecha, sumar un mes y devolver nuevo período
                    int año = int.Parse(ultimoPeriodo.Substring(0, 4));
                    int mes = int.Parse(ultimoPeriodo.Substring(4, 2));
                    DateTime fechaUltimo = new DateTime(año, mes, 1); // Tomamos el primer día del mes del último período = -20240401
                    DateTime siguiente = fechaUltimo.AddMonths(1); // Sumamos un mes para obtener el siguiente período = 20240501
                    return siguiente.ToString("yyyyMM");
                }
            }
        }
        #endregion

        #region Guardar nuevo periodo de migración
        public static void RegistrarSiguientePeriodo(string periodo)
        {
            string conexion = ConfigurationManager.ConnectionStrings["cCon"].ConnectionString;
            string consulta = "INSERT INTO DateMigration (Periodo) VALUES (@periodo)";
            using (var conn = new SqlConnection(conexion))
            using (var cmd = new SqlCommand(consulta, conn))
            {
                cmd.Parameters.AddWithValue("@periodo", periodo);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
        #endregion

        #region ActualizarBaseUrl
        public static void ActualizarBaseUrl(DateTime fechaInicio, DateTime fechaFin)
        {
            string conexion = ConfigurationManager.ConnectionStrings["cCon"].ConnectionString;
            using (var conn = new SqlConnection(conexion))
            using (var cmd = new SqlCommand("ActualizarBaseUrl", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@FechaInicio", fechaInicio);
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
        #endregion
    }
}
