using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using MafiaDiscordBot.Attributes.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using Serilog;
using SqlKata;
using SqlKata.Compilers;
using Model = MafiaDiscordBot.Models.Database;

namespace MafiaDiscordBot.Services
{
    public class DatabaseService
    {
        private MySqlConnection _connection;
        private MySqlCompiler _compiler;
        private IConfigurationSection _databaseKeys;

        public DatabaseService(IServiceProvider services)
        {
            Log.Debug("Initializing database service");
            var config = services.GetRequiredService<IConfigurationRoot>();
            var credentials = config.GetSection("database");
            
            MySqlConnectionStringBuilder connectionStringBuilder = new MySqlConnectionStringBuilder();
            if (
                string.IsNullOrWhiteSpace(connectionStringBuilder.Server = credentials["server"]) ||
                string.IsNullOrWhiteSpace(connectionStringBuilder.UserID = credentials["user"]) ||
                (connectionStringBuilder.Password = credentials["password"]) == null ||
                string.IsNullOrWhiteSpace(connectionStringBuilder.Database = credentials["db"])
            )
            {
                Log.Fatal("You have to set the database credentials properly");
                Environment.Exit(0);
                return;
            }
            
            _connection = new MySqlConnection(connectionStringBuilder.GetConnectionString(true));
            _compiler = new MySqlCompiler();
            try
            {
                _connection.Open();
                _databaseKeys = credentials.GetSection("keys");
            }
            catch (Exception e)
            {
                Log.Fatal("Unable to start the database service", e);
                Environment.Exit(0);
                return;
            }
            
            // prepare all the contexts
            var contextServiceType = typeof(ContextBase);
            foreach (var prop in
                GetType().GetProperties()
                .Where(prop => prop.CanWrite && prop.PropertyType.IsSubclassOf(contextServiceType)))
            {
                // create an instance of the context
                var instance = Activator.CreateInstance(prop.PropertyType, this);
                // set the property in the database service to the new instance
                prop.SetValue(this, instance);
            }


            Log.Debug("Database service successfully initialized");
        }

        private T ParseResult<T>(DbDataReader reader) where T : class, Model.IDatabaseObject
        {
            try
            {
                if (!reader.HasRows) return null;
                reader.Read();

                bool HasColumn(DbDataReader dr, string columnName, out int ordinal)
                {
                    for (int i = 0; i < dr.FieldCount; i++)
                    {
                        if (dr.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            ordinal = i;
                            return true;
                        }
                    }

                    ordinal = -1;
                    return false;
                }

                Type destinationType = typeof(T);
                var empty = (T) Activator.CreateInstance(destinationType);
                var props = destinationType.GetProperties();
                var columns = reader.GetColumnSchema();
                foreach (var prop in props)
                {
                    if (!prop.CanWrite) continue;
                    var attributes = prop.GetCustomAttributes(false);
                    if (attributes.Any(x => x is SqlIgnoreAttribute)) continue;
                    var sqlColumnAttr = (SqlColumnAttribute) attributes.FirstOrDefault(x => x is SqlColumnAttribute);
                    string sqlName = sqlColumnAttr?.Name ?? prop.Name;
                    if (HasColumn(reader, sqlName, out int ordinal))
                    {
                        var sqlConverterAttr =
                            (SqlConverterAttribute) attributes.FirstOrDefault(x => x is SqlConverterAttribute);
                        var dbvalue = reader.IsDBNull(ordinal) ? null : reader[ordinal];
                        object gstate = null;
                        if (sqlConverterAttr != null)
                        {
                            (object value, object state) = sqlConverterAttr.ReadWithState(dbvalue);
                            gstate = state;
                            prop.SetValue(empty, value);
                        }
                        else if (columns[ordinal].DataType == prop.PropertyType)
                            prop.SetValue(empty, dbvalue);

                        var _afterParseAttr =
                            (AfterSqlParseAttribute) attributes.FirstOrDefault(x => x is AfterSqlParseAttribute);
                        if (_afterParseAttr?.MethodName != null)
                            destinationType.GetMethods()
                                .FirstOrDefault(m =>
                                    m.Name == _afterParseAttr.MethodName && m.GetParameters().Length == 1)
                                ?.Invoke(empty, new[] {gstate});
                    }
                }

                reader.Close();

                empty.Filled = true;
                empty.LastAccessed = DateTime.UtcNow;

                var afterParseAttr = destinationType.GetCustomAttributes(typeof(AfterSqlParseAttribute), false);
                if (afterParseAttr.Length > 0)
                {
                    var attr = (AfterSqlParseAttribute) afterParseAttr[0];
                    if (attr.MethodName != null)
                        destinationType.GetMethods()
                            .FirstOrDefault(m =>
                                m.Name == attr.MethodName &&
                                m.GetParameters().Length == (attr.SqlColumnsAsArgs?.Length ?? 0))?.Invoke(empty,
                                attr.SqlColumnsAsArgs?.Select(cln =>
                                {
                                    if (HasColumn(reader, cln, out int ordinal)) return reader[ordinal];
                                    return null;
                                })?.ToArray());
                }

                return empty;
            }
            catch
            {
                reader.Dispose();
                throw;
            }
        }

        private string GetSqlColumnName<T>(string propertyName) => (typeof(T).GetProperty(propertyName)?.GetCustomAttributes(typeof(SqlColumnAttribute), false).FirstOrDefault() as SqlColumnAttribute)?.Name ?? propertyName;
        
        private MySqlCommand GetExecuter(Query query) => new MySqlCommand(_compiler.Compile(query).ToString(), _connection);

        public class ContextBase
        {
            protected readonly DatabaseService _service;
            protected readonly string _tableName;
            protected bool CanWork => !string.IsNullOrWhiteSpace(_tableName);

            protected ContextBase(DatabaseService service, string tableNameKey)
            {
                _service = service;
                if (string.IsNullOrWhiteSpace(_tableName = service._databaseKeys[$"TABLE_{tableNameKey}"]))
                    Log.Error($"{tableNameKey} table name is missing, no database service will be provided to this table");
            }

            public class Guilds : ContextBase
            {
                private readonly ConcurrentDictionary<ulong, Model.Guild> _guilds;
                
                public Guilds(DatabaseService service) : base(service, "guilds")
                {
                    _guilds = new ConcurrentDictionary<ulong, Model.Guild>();
                }
                
                public async Task<Model.Guild> GetGuild(ulong guildId)
                {
                    if (!CanWork) return null;
                    if (_guilds.TryGetValue(guildId, out Model.Guild cachedGuild))
                        return cachedGuild;
                    
                    cachedGuild = _service.ParseResult<Model.Guild>(await _service.GetExecuter(new Query(_tableName)
                            .Where(_service.GetSqlColumnName<Model.Guild>("id"), guildId))
                        .ExecuteReaderAsync().ConfigureAwait(false));
                    
                    return cachedGuild == null ? null : _guilds.AddOrUpdate(guildId, cachedGuild, (KeyStatus, guild) => cachedGuild);
                }

                public Task<Model.Guild> GetGuild(Discord.IGuild guild) => GetGuild(guild.Id);
                public Task<Model.Guild> GetGuild(Discord.IGuildChannel guildChannel) => GetGuild(guildChannel.Guild);
            }
        }

        public ContextBase.Guilds Guilds { get; private set; }
    }
}