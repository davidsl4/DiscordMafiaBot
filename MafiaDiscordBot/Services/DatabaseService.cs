using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
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
        private readonly MySqlConnection _connection;
        private readonly MySqlCompiler _compiler;
        private readonly IConfigurationSection _databaseKeys;

        public DatabaseService(IServiceProvider services)
        {
            Log.Debug("Initializing database service");
            var config = services.GetRequiredService<IConfigurationRoot>();
            var credentials = config.GetSection("database");

            var connectionStringBuilder = new MySqlConnectionStringBuilder();
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

        private static T ParseResult<T>(DbDataReader reader) where T : class, Model.IDatabaseObject
        {
            try
            {
                if (!reader.HasRows) return null;
                reader.Read();

                static bool HasColumn(IDataRecord dr, string columnName, out int ordinal)
                {
                    for (var i = 0; i < dr.FieldCount; i++)
                    {
                        if (!dr.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase)) continue;
                        ordinal = i;
                        return true;
                    }

                    ordinal = -1;
                    return false;
                }

                var destinationType = typeof(T);
                var idbType = typeof(Model.IDatabaseObject);
                var empty = (T) Activator.CreateInstance(destinationType);
                if (empty == null) return null;
                var props = destinationType.GetProperties();
                var columns = reader.GetColumnSchema();
                foreach (var prop in props)
                {
                    if (!prop.CanWrite) continue;
                    if (idbType.GetProperty(prop.Name) != null) continue;
                    var attributes = prop.GetCustomAttributes(false);
                    if (attributes.Any(x => x is IgnoreAttribute)) continue;
                    var sqlColumnAttr = (ColumnAttribute) attributes.FirstOrDefault(x => x is ColumnAttribute);
                    var sqlName = sqlColumnAttr?.Name ?? prop.Name;
                    if (!HasColumn(reader, sqlName, out var ordinal)) continue;
                    var sqlConverterAttr =
                        (SqlConverterAttribute) attributes.FirstOrDefault(x => x is SqlConverterAttribute);
                    var dbValue = reader.IsDBNull(ordinal) ? null : reader[ordinal];
                    object gState = null;
                    if (sqlConverterAttr != null)
                    {
                        var (value, state) = sqlConverterAttr.ReadWithState(dbValue);
                        gState = state;
                        prop.SetValue(empty, value);
                    }
                    else if (columns[ordinal].DataType == prop.PropertyType)
                        prop.SetValue(empty, dbValue);

                    var _afterParseAttr =
                        (AfterSqlParseAttribute) attributes.FirstOrDefault(x => x is AfterSqlParseAttribute);
                    if (_afterParseAttr?.MethodName != null)
                        destinationType.GetMethods()
                            .FirstOrDefault(m =>
                                m.Name == _afterParseAttr.MethodName && m.GetParameters().Length == 1)
                            ?.Invoke(empty, new[] {gState});
                }

                reader.Close();

                empty.Filled = true;
                empty.LastAccessed = DateTime.Now;

                var afterParseAttr = destinationType.GetCustomAttributes(typeof(AfterSqlParseAttribute), false);
                if (afterParseAttr.Length <= 0) return empty;
                var attr = (AfterSqlParseAttribute) afterParseAttr[0];
                if (attr.MethodName != null)
                    destinationType.GetMethods()
                        .FirstOrDefault(m =>
                            m.Name == attr.MethodName &&
                            m.GetParameters().Length == (attr.SqlColumnsAsArgs?.Length ?? 0))?.Invoke(empty,
                            attr.SqlColumnsAsArgs?.Select(cln =>
                                HasColumn(reader, cln, out var ordinal) ? reader[ordinal] : null).ToArray());

                return empty;
            }
            catch
            {
                reader.Dispose();
                throw;
            }
        }

        private string QuerySaveResult<T>(string tableName, T data) where T : class, Model.IDatabaseObject
        {
            var type = typeof(T);
            var idbType = typeof(Model.IDatabaseObject);
            var dictionary = new Dictionary<string, object>();
            foreach (var runtimeProperty in type.GetProperties())
            {
                if (idbType.GetProperty(runtimeProperty.Name) != null) continue;
                if (runtimeProperty.GetCustomAttribute(typeof(IgnoreAttribute)) != null) continue;
                var obj = runtimeProperty.GetValue(data);
                var str = (runtimeProperty.GetCustomAttribute(typeof (ColumnAttribute)) is ColumnAttribute customAttribute ? customAttribute.Name : null) ?? runtimeProperty.Name;
                if (runtimeProperty.GetCustomAttribute(typeof(SqlConverterAttribute)) is SqlConverterAttribute sqlConverterAttr)
                    obj = sqlConverterAttr.Write(obj);
                dictionary.Add(str, obj);
            }

            // ReSharper disable once VariableHidesOuterVariable
            SqlResult CompileUpdateQuery(Query query)
            {
                var ctx = new SqlResult()
                {
                    Query = query
                };
                if (!ctx.Query.HasComponent("from", _compiler.EngineCode))
                    throw new InvalidOperationException("No table set to update");
                var oneComponent1 = ctx.Query.GetOneComponent<AbstractFrom>("from", _compiler.EngineCode);
                if (oneComponent1 is RawFromClause rawFromClause)
                {
                    ctx.Bindings.AddRange(rawFromClause.Bindings);
                }
                var oneComponent2 = ctx.Query.GetOneComponent<InsertClause>("update", _compiler.EngineCode);
                var stringList = new List<string>();
                for (var index = oneComponent2.Columns.Count - 1; index >= 0; --index)
                    stringList.Add(_compiler.Wrap(oneComponent2.Columns[index]) + " = " + _compiler.Parameter(ctx, oneComponent2.Values[index]));
                var str2 = _compiler.CompileWheres(ctx);
                if (!string.IsNullOrEmpty(str2))
                    str2 = " " + str2;
                var str3 = string.Join(", ", stringList);
                ctx.RawSql = "UPDATE " + str3 + str2;
                return ctx;
            }
            
            var query = _compiler.Compile(new Query(tableName)
                .AsInsert(dictionary)) + " ON DUPLICATE KEY  " + CompileUpdateQuery(new Query(tableName)
                .AsUpdate(dictionary));
            Console.WriteLine(query);
            return query;
        }

        private static string GetSqlColumnName<T>(string propertyName) =>
            (typeof(T).GetProperty(propertyName)?.GetCustomAttributes(typeof(ColumnAttribute), false)
                .FirstOrDefault() as ColumnAttribute)?.Name ?? propertyName;

        private MySqlCommand GetExecute(Query query) => new MySqlCommand(_compiler.Compile(query).ToString(), _connection);
        private MySqlCommand GetExecute(string query) => new MySqlCommand(query, _connection);

        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
        public abstract class ContextBase
        {
            private readonly DatabaseService _service;
            private readonly string _tableName;
            private bool CanWork => !string.IsNullOrWhiteSpace(_tableName);

            private ContextBase(DatabaseService service, string tableNameKey)
            {
                _service = service;
                if (string.IsNullOrWhiteSpace(_tableName = service._databaseKeys[$"TABLE_{tableNameKey}"]))
                    Log.Error(
                        $"{tableNameKey} table name is missing, no database service will be provided to this table");
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
                    if (_guilds.TryGetValue(guildId, out var cachedGuild))
                        return cachedGuild;

                    Log.Verbose("Looking for guild {id} in database", guildId);
                    cachedGuild = ParseResult<Model.Guild>(await _service.GetExecute(new Query(_tableName)
                            .Where(GetSqlColumnName<Model.Guild>("id"), guildId))
                        .ExecuteReaderAsync().ConfigureAwait(false));

                    return cachedGuild == null
                        ? null
                        : _guilds.AddOrUpdate(guildId, cachedGuild, (KeyStatus, guild) => cachedGuild);
                }

                public Task<Model.Guild> GetGuild(Discord.IGuild guild) => GetGuild(guild.Id);
                public Task<Model.Guild> GetGuild(Discord.IGuildChannel guildChannel) => GetGuild(guildChannel.Guild);

                public async Task SaveGuild(Model.Guild guild)
                {
                   await _service.GetExecute(_service.QuerySaveResult(_tableName, guild)).ExecuteNonQueryAsync().ConfigureAwait(false);
                   guild.LastModified = null;
                }

                public void ClearCachedData() => _guilds.Clear();
            }
        }

        public ContextBase.Guilds Guilds { get; private set; }

        public void ClearCachedData()
        {
            Guilds?.ClearCachedData();
        }
    }
}