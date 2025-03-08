using Amazon.Lambda.Core;
using MateoAPI.Helpers;
using Newtonsoft.Json;
using Npgsql;
using System.Diagnostics;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace InitialCreationLambda;

public class Function {
    
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public string FunctionHandler(string input, ILambdaContext context) {
        Stopwatch sw = Stopwatch.StartNew();
        LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Iniciando proceso de creacion inicial del schema y sus usuarios de aplicacion...");

        string secretArnConnectionString = Environment.GetEnvironmentVariable("SECRET_ARN_CONNECTION_STRING") ?? throw new ArgumentNullException("SECRET_ARN_CONNECTION_STRING");
        string appName = Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
        string appSchemaName = Environment.GetEnvironmentVariable("APP_SCHEMA_NAME") ?? throw new ArgumentNullException("APP_SCHEMA_NAME");
        string migrationEFBundle = Environment.GetEnvironmentVariable("MIGRATION_EFBUNDLE") ?? throw new ArgumentNullException("MIGRATION_EFBUNDLE");

        if (appSchemaName.Contains('"')) {
            throw new Exception($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Error con el nombre del schema para app \"{appName}\" - Caracteres invalidos...");
        }

        LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Obteniendo secreto de conexion a base de datos...");

        Dictionary<string, string> connectionStringSecrets = SecretManager.ObtenerSecreto(secretArnConnectionString).Result;
        string connectionString = $"Server={connectionStringSecrets["Host"]};Port={connectionStringSecrets["Port"]};SslMode=prefer;" +
            $"Database={connectionStringSecrets[$"{appName}Database"]};User Id={connectionStringSecrets[$"{appName}AdmUsername"]}; Password='{connectionStringSecrets[$"{appName}AdmPassword"]}';";

        List<string> retorno = [];

        LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Conectandose a base de datos RDS PostgreSQL [{connectionStringSecrets["Host"]}]...");



        using (NpgsqlConnection conn = new(connectionString)) {
            conn.Open();

            // Se crea schema...
            LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Creando el schema para app \"{appName}\" - [Schema: {appSchemaName}]...");
            try {
                using NpgsqlCommand cmd = new($"CREATE SCHEMA IF NOT EXISTS \"{appSchemaName}\"", conn);
                cmd.ExecuteNonQuery();
            } catch (Exception ex) {
                string mensaje = $"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Error al crear schema de la app: " + ex;
                LambdaLogger.Log(mensaje);
                retorno.Add(mensaje);
            }

            // Se crea usuario de aplicación...
            string appUsername = connectionStringSecrets[$"{appName}AppUsername"];
            if (appUsername.Contains('"')) {
                throw new Exception($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Error con el nombre de usuario de aplicacion para app \"{appName}\" - Caracteres invalidos...");
            }
            string appPassword = connectionStringSecrets[$"{appName}AppPassword"];
            if (appPassword.Contains('\'')) {
                throw new Exception($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Error con la contraseña del usuario de aplicacion para app \"{appName}\" - Caracteres invalidos...");
            }

            LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Creando usuario de aplicacion para app \"{appName}\"...");
            try {
                using NpgsqlCommand cmd = new($"CREATE USER \"{appUsername}\" WITH ENCRYPTED PASSWORD '{appPassword}'", conn);
                cmd.ExecuteNonQuery();
            } catch (Exception ex) {
                string mensaje = $"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Error al crear usuario de aplicacion de la app: " + ex;
                LambdaLogger.Log(mensaje);
                retorno.Add(mensaje);
            }

            // Se otorgan permisos sobre el usuario dew aplicación...
            LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Otorgando permisos para usuario de aplicacion para app \"{appName}\"...");
            try {
                // Se habilita que usuario de aplicación pueda usar el nuevo esquema...
                using NpgsqlCommand cmd = new($"GRANT USAGE ON SCHEMA \"{appSchemaName}\" TO \"{appUsername}\"", conn);
                cmd.ExecuteNonQuery();

                // Se definen los permisos por defecto para el usuario de aplicación, sobre las tablas creadas por el usuario administrador en el nuevo esquema...
                using NpgsqlCommand cmd2 = new($"ALTER DEFAULT PRIVILEGES FOR USER \"{connectionStringSecrets[$"{appName}AdmUsername"]}\" IN SCHEMA \"{appSchemaName}\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO \"{appUsername}\"", conn);
                cmd2.ExecuteNonQuery();

                // Se definen los permisos por defecto para el usuario de aplicación, sobre las secuencias creadas por el usuario administrador en el nuevo esquema..
                using NpgsqlCommand cmd3 = new($"ALTER DEFAULT PRIVILEGES FOR USER \"{connectionStringSecrets[$"{appName}AdmUsername"]}\" IN SCHEMA \"{appSchemaName}\" GRANT USAGE ON SEQUENCES TO \"{appUsername}\"", conn);
                cmd3.ExecuteNonQuery();
            } catch (Exception ex) {
                string mensaje = $"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Error al otorgar permisos para usuario de aplicacion de la app: " + ex;
                LambdaLogger.Log(mensaje);
                retorno.Add(mensaje);
            }
        }

        LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Se inicia con proceso de migracion EFCore del modelo de datos...");
        Process process = new() { 
            StartInfo = new ProcessStartInfo {
                FileName = "/bin/bash",
                Arguments = $"-c \"{migrationEFBundle} --connection '{connectionString}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                
            },
        };
        process.ErrorDataReceived += (sender, e) => {
            LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Ejecución de migración en proceso - Error Data Received: {e.Data}");
        };
        process.OutputDataReceived += (sender, e) => {
            LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Ejecución de migración en proceso - Output Data Received: {e.Data}");
        };
        process.Exited += (sender, e) => {
            LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Ejecución de migración en proceso - Exited: {e}");
        };
        process.Start();

        LambdaLogger.Log($"[Elapsed Time: {sw.ElapsedMilliseconds} ms] - Ha terminado el proceso de creacion inicial del schema y sus usuarios de aplicacion...");

        return JsonConvert.SerializeObject(retorno);
    }
}
