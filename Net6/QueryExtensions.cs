﻿using System.Data.Common;
using Com.H.Linq;
using Com.H.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Data;
using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using Com.H.Data;
using System.Reflection;
using Com.H.Text;
using Com.H.Text.Template;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Com.H.EF.Relational
{


    public static class QueryExtensions
    {
        // private static string PRegex { get; set; } = @"{{(?<param>.*?)?}}";
        

        #region synchronous



        public static void EnsureClosed(this DbConnection conn)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (conn.State != System.Data.ConnectionState.Closed) conn.Close();
        }

        public static void EnsureOpen(this DbConnection conn)
        {
            if (conn is null) throw new ArgumentNullException(nameof(conn));
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        }

        private static void EnsureClosed(this DbDataReader reader)
        {
            if (reader == null) return;
            if (reader.IsClosed) return;
            reader.Close();
        }


        private static IEnumerable<T?> ExecuteQueryDictionary<T>(
            this DbContext dc,
            string query,
            IDictionary<string, object>? queryParams = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));
            var conn = dc.Database.GetDbConnection();
            bool cont = true;
            DbDataReader? reader = null;
            DbCommand? command = null;
            try
            {
                conn.EnsureOpen();

                var paramList = Regex.Matches(query, openMarker + QueryParams.RegexPattern + closeMarker)
                    .Cast<Match>()
                    .Select(x => x.Groups["param"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x).Distinct().ToList();

                command = conn.CreateCommand();
                command.CommandType = CommandType.Text;


                if (paramList.Count > 0)
                {
                    var joined = paramList
                        .LeftJoin(queryParams??new Dictionary<string, object>(),
                        pl => pl.ToUpper(CultureInfo.InvariantCulture),
                        p => p.Key.ToUpper(CultureInfo.InvariantCulture),
                        (pl, p) => new { k = pl, v = p.Value }).ToList();

                    foreach (var item in joined)
                    {
                        if (item.v == null) query = query
                            .Replace(openMarker + item.k + closeMarker,
                                nullReplacement, true,
                                CultureInfo.InstalledUICulture);
                        else
                        {
                            query = query
                            .Replace(openMarker + item.k + closeMarker,
                                "@vxv_" + item.k, true,
                                CultureInfo.InvariantCulture);
                            var p = command.CreateParameter();
                            p.ParameterName = "@vxv_" + item.k;
                            p.Value = item.v;
                            command.Parameters.Add(p);

                        }
                    }

                }

                command.CommandText = query;

                reader = command.ExecuteReader();
            }
            catch(Exception ex)
            {
                reader?.EnsureClosed();
                if (closeConnectionOnExit)
                    conn.EnsureClosed();
                throw new Exception(ex.GenerateError(command, query, queryParams));
            }

            if (reader.HasRows)
            {
                while (cont)
                {
                    try
                    {
                        cont = reader.Read();
                        if (!cont) break;
                    }
                    catch
                    {
                        reader.EnsureClosed();
                        if (closeConnectionOnExit) conn.EnsureClosed();
                        // throw new Exception(ex.GenerateError(command));
                        throw;
                    }

                    

                    // special case for a single value for a primitive type (support columnless values)
                    if (typeof(T) == typeof(string)
                        ||
                        typeof(T) == typeof(int)
                        ||
                        typeof(T) == typeof(double)
                        ||
                        typeof(T) == typeof(decimal)
                        ||
                        typeof(T) == typeof(int?)
                        ||
                        typeof(T) == typeof(double?)
                        ||
                        typeof(T) == typeof(decimal?)
                        )
                    {
                        var value = reader.GetValue(0);
                        if (value == DBNull.Value)
                        {
                            yield return (T?)(object?)null;
                            continue;
                        }
                        yield return (T)(object)value;
                        continue;
                    }


                    if (typeof(T) == typeof(DateTime)
                        )
                    {
                        var value = reader.GetValue(0);
                        DateTime dt = new();
                        if (value != DBNull.Value)
                            _ = DateTime.TryParse(value.ToString(), out dt);
                        yield return (T)(object)dt;
                        continue;
                    }


                    if (typeof(T) == typeof(DateTime?))
                    {
                        var value = reader.GetValue(0);
                        DateTime dt = new();
                        if (value == DBNull.Value
                            ||
                            !DateTime.TryParse(value.ToString(), out dt))
                        {
                            yield return (T?)(object?)null;
                            continue;
                        }
                        yield return (T)(object)dt;
                        continue;
                    }


                    T result = Activator.CreateInstance<T>();
                    var joined =
                    typeof(T).GetCachedProperties()
                        .LeftJoin(
                            Enumerable.Range(0, reader.FieldCount)
                            .Select(x => new { Name = reader.GetName(x), Value = reader.GetValue(x) }),
                            dst => dst.Name.ToUpper(CultureInfo.InvariantCulture),
                            // see if schema name was applied
                            src => src.Name.ToUpper(CultureInfo.InvariantCulture),
                            (dst, src) => new { dst, src })
                            ;

                    foreach (var item in joined.Where(x => x?.src?.Value is not null))
                    {
                        try
                        {
                            // item.src.Value cannot be null here
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            item.dst.Info.SetValue(result,
                                Convert.ChangeType(item.src.Value,
                                item.dst.Info.PropertyType, CultureInfo.InvariantCulture));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        }
                        catch { }
                    }

                    yield return result;
                }
            }
            reader.EnsureClosed();

            if (closeConnectionOnExit) conn.EnsureClosed();
            yield break;
        }

        private static IEnumerable<dynamic> ExecuteQueryDictionary(
            this DbContext dc,
            string query,
            IDictionary<string, object>? queryParams = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false,
            bool keepColumnsOnEmpty = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            queryParams ??= new Dictionary<string, object>();
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));
            var conn = dc.Database.GetDbConnection();
            bool cont = true;
            DbDataReader? reader = null;
            DbCommand? command = null;
            try
            {
                conn.EnsureOpen();

                var paramList = Regex.Matches(query, openMarker + QueryParams.RegexPattern + closeMarker)
                    .Cast<Match>()
                    .Select(x => x.Groups["param"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x).Distinct().ToList();

                command = conn.CreateCommand();
                command.CommandType = CommandType.Text;

                if (paramList.Count > 0)
                {
                    var joined = paramList
                        .LeftJoin(queryParams,
                        pl => pl.ToUpper(CultureInfo.InvariantCulture),
                        p => p.Key.ToUpper(CultureInfo.InvariantCulture),
                        (pl, p) => new { k = pl, v = p.Value }).ToList();

                    foreach (var item in joined)
                    {
                        if (item.v == null) query = query
                            .Replace(openMarker + item.k + closeMarker,
                                nullReplacement, true,
                                CultureInfo.InvariantCulture);
                        else
                        {
                            query = query
                            .Replace(openMarker + item.k + closeMarker,
                                "@vxv_" + item.k, true,
                                CultureInfo.InvariantCulture);
                            var p = command.CreateParameter();
                            p.ParameterName = "@vxv_" + item.k;
                            p.Value = item.v;
                            command.Parameters.Add(p);
                        }
                    }
                }


                command.CommandText = query;

                
                reader = command.ExecuteReader();
            }
            catch(Exception ex)
            {
                reader?.EnsureClosed();
                if (closeConnectionOnExit) conn.EnsureClosed();
                string errMsg = ex.GenerateError(command, query, queryParams);

                throw new Exception(errMsg);
            }

            if (reader.HasRows)
            {
                while (cont)
                {
                    try
                    {
                        cont = reader.Read();
                        if (!cont) break;
                    }
                    catch(Exception ex)
                    {
                        reader.EnsureClosed();
                        if (closeConnectionOnExit) conn.EnsureClosed();
                        string errMsg = ex.GenerateError(command, query, queryParams);
                        //throw new Exception(errMsg);
                        throw;
                    }
                    ExpandoObject result = new();

                    foreach (var item in Enumerable.Range(0, reader.FieldCount)
                            .Select(x => new { Name = reader.GetName(x), Value = reader.GetValue(x) }))
                    {
                        try
                        {
                            result.TryAdd(item.Name, item.Value);
                        }
                        catch { }
                    }

                    yield return result;
                }
            }
            else
            {
                if (keepColumnsOnEmpty)
                {
                    //Console.WriteLine("reader column count: " + reader.FieldCount);
                    ExpandoObject result = new();
                    foreach (var item in Enumerable.Range(0, reader.FieldCount)
                                .Select(x => new { Name = reader.GetName(x), Type = reader.GetFieldType(x) }))
                    {

                        result.TryAdd(item.Name, null);
                        ////Console.WriteLine($"name = {item.Name}, type = {item.Value}");
                    }
                    yield return result;
                }
            }
            reader.EnsureClosed();
            if (closeConnectionOnExit) conn.EnsureClosed();
            yield break;
        }


        /// <summary>
        /// Takes parameters in the form of either IDictionary of a string and object key value pairs,
        /// or in the form of any object with properties, where the object properties serve as parameters
        /// to be passed to the query.
        /// The query string could have parameters placeholders in the form of property names enclosed with 
        /// double curley brackets {{property_name}}, so that the extension method could use those placeholders
        /// convert them to query parameters for security and pass them to the relational database for execution.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dc"></param>
        /// <param name="query"></param>
        /// <param name="queryParams"></param>
        /// <param name="closeConnectionOnExit"></param>
        /// <returns></returns>
        public static IEnumerable<T?> ExecuteQuery<T>(
            this DbContext dc,
            string query,
            object? queryParams = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (queryParams is null)
                return dc.ExecuteQueryDictionary<T>(query, new Dictionary<string, object>(), openMarker, closeMarker, nullReplacement, closeConnectionOnExit);
            // queryParams cannot be null here
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            IDictionary<string, object> dictionaryParams = queryParams.GetDataModelParameters();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            return dc.ExecuteQueryDictionary<T>(query, dictionaryParams, openMarker, closeMarker, nullReplacement, closeConnectionOnExit);
        }




        /// <summary>
        /// Dynamic results version of ExecuteCommand extension. Takes parameters in the form of either IDictionary of a string and object key value pairs,
        /// or in the form of any object with properties, where the object properties serve as parameters
        /// to be passed to the query.
        /// The query string could have parameters placeholders in the form of property names enclosed with 
        /// double curley brackets {{property_name}}, so that the extension method could use those placeholders
        /// convert them to query parameters for security and pass them to the relational database for execution.
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="cmdQuery"></param>
        /// <param name="cmdQueryParams"></param>
        /// <param name="closeConnectionOnExit"></param>
        /// <returns></returns>
        public static void ExecuteCommand(
            this DbContext dc,
            string cmdQuery,
            object? cmdQueryParams = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false,
            bool keepColumnsOnEmpty = false
            )
        {
            _ = ExecuteQuery(dc, cmdQuery, cmdQueryParams, openMarker, closeMarker,
                nullReplacement, closeConnectionOnExit, keepColumnsOnEmpty)
                .ToList();
        }



        /// <summary>
        /// Dynamic results version of ExecuteQuery extension. Takes parameters in the form of either IDictionary of a string and object key value pairs,
        /// or in the form of any object with properties, where the object properties serve as parameters
        /// to be passed to the query.
        /// The query string could have parameters placeholders in the form of property names enclosed with 
        /// double curley brackets {{property_name}}, so that the extension method could use those placeholders
        /// convert them to query parameters for security and pass them to the relational database for execution.
        /// The result is an IEnumerable of dynamic objects.
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="query"></param>
        /// <param name="queryParams"></param>
        /// <param name="closeConnectionOnExit"></param>
        /// <returns></returns>
        public static IEnumerable<dynamic> ExecuteQuery(
            this DbContext dc,
            string query,
            object? queryParams = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false,
            bool keepColumnsOnEmpty = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));


            if (queryParams is null)
                return dc.ExecuteQueryDictionary(query, new Dictionary<string, object>(),
                    openMarker, closeMarker, nullReplacement, closeConnectionOnExit,
                    keepColumnsOnEmpty
                    );
            else
            {
                if (typeof(IEnumerable<QueryParams>).IsAssignableFrom(queryParams.GetType()))
                    return ExecuteQuery(
                        dc,
                        query,
                        (IEnumerable<QueryParams>) queryParams,
                        closeConnectionOnExit);
            }

            // queryParams cannot be null here
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            IDictionary<string, object> dictionaryParams = queryParams.GetDataModelParameters();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            return dc.ExecuteQueryDictionary(query, dictionaryParams, openMarker,
                closeMarker, nullReplacement, closeConnectionOnExit, keepColumnsOnEmpty);
        }



        #endregion

        #region async
        private static async Task EnsureClosedAsync(this DbConnection conn)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (conn.State != System.Data.ConnectionState.Closed) await conn.CloseAsync();
        }

        private static async Task EnsureOpenAsync(this DbConnection conn)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        }

        private static async Task EnsureClosedAsync(this DbDataReader reader)
        {
            if (reader == null) return;
            if (reader.IsClosed) return;
            await reader.CloseAsync();
        }


        private static async IAsyncEnumerable<T?> ExecuteQueryDictionaryAsync<T>(
            this DbContext dc,
            string query,
            IDictionary<string, object>? queryParams = null,
            CancellationToken? cancellationToken = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));
            var conn = dc.Database.GetDbConnection();
            bool cont = true;
            DbDataReader? reader = null;
            DbCommand? command = null;
            try
            {
                await conn.EnsureOpenAsync();

                var paramList = Regex.Matches(query, openMarker + QueryParams.RegexPattern + closeMarker)
                    .Cast<Match>()
                    .Select(x => x.Groups["param"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x).Distinct().ToList();

                command = conn.CreateCommand();
                command.CommandType = CommandType.Text;


                if (paramList.Count > 0)
                {
                    var joined = paramList
                        .LeftJoin(queryParams??new Dictionary<string, object>(),
                        pl => pl.ToUpper(CultureInfo.InvariantCulture),
                        p => p.Key.ToUpper(CultureInfo.InvariantCulture),
                        (pl, p) => new { k = pl, v = p.Value }).ToList();

                    foreach (var item in joined)
                    {
                        if (item.v == null) query = query
                            .Replace(openMarker + item.k + closeMarker,
                                nullReplacement, true,
                                CultureInfo.InvariantCulture);
                        else
                        {
                            query = query
                            .Replace(openMarker + item.k + closeMarker,
                                "@vxv_" + item.k, true,
                                CultureInfo.InvariantCulture);
                            var p = command.CreateParameter();
                            p.ParameterName = "@vxv_" + item.k;
                            p.Value = item.v;
                            command.Parameters.Add(p);

                        }
                    }

                }

                command.CommandText = query;

                reader = await (cancellationToken == null
                        ? command.ExecuteReaderAsync()
                        : command.ExecuteReaderAsync((CancellationToken)cancellationToken));

            }
            catch(Exception ex)
            {
                if (reader is not null)
                    await reader.EnsureClosedAsync();
                if (closeConnectionOnExit)
                    await conn.EnsureClosedAsync();
                throw new Exception(ex.GenerateError(command, query, queryParams));
            }

            if (reader.HasRows)
            {
                while (cont)
                {
                    try
                    {
                        cont = reader.Read();
                        if (!cont) break;
                    }
                    catch
                    {
                        await reader.EnsureClosedAsync();
                        if (closeConnectionOnExit) await conn.EnsureClosedAsync();
                        // throw new Exception(ex.GenerateError(command));
                        throw;
                    }
                    T? result = (typeof(T) == typeof(string))?(T?) (object?) (string?) null: Activator.CreateInstance<T>();
                    var joined =
                    typeof(T).GetCachedProperties()
                        .LeftJoin(
                            Enumerable.Range(0, reader.FieldCount)
                            .Select(x => new { Name = reader.GetName(x), Value = reader.GetValue(x) }),
                            dst => dst.Name.ToUpper(CultureInfo.InvariantCulture),
                            // see if schema name was applied
                            src => src.Name.ToUpper(CultureInfo.InvariantCulture),
                            (dst, src) => new { dst, src })
                            ;

                    foreach (var item in joined.Where(x => x?.src?.Value is not null))
                    {
                        try
                        {
                            // item.src.Value cannot be null here
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            item.dst.Info.SetValue(result,
                                Convert.ChangeType(item.src.Value,
                                item.dst.Info.PropertyType, CultureInfo.InvariantCulture));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        }
                        catch { }
                    }

                    yield return result;
                }
            }
            await reader.EnsureClosedAsync();

            if (closeConnectionOnExit) await conn.EnsureClosedAsync();
            yield break;
        }



        public static async IAsyncEnumerable<T?> ExecuteQueryAsync<T>(
            this DbContext dc,
            string query,
            object? queryParams = null,
            CancellationToken? cancellationToken = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));



            if (queryParams is null)
                await foreach (var item in dc.ExecuteQueryDictionaryAsync<T>(
                        query,
                        new Dictionary<string, object>(),
                        cancellationToken, openMarker,
                        closeMarker, nullReplacement, closeConnectionOnExit))
                    yield return item;

            // queryParams cannot be null here
#pragma warning disable CS8604 // Possible null reference argument.
            IDictionary<string, object>? dictionaryParams = queryParams.GetDataModelParameters();
#pragma warning restore CS8604 // Possible null reference argument.

            await foreach (var item in dc.ExecuteQueryDictionaryAsync<T>(
                query,
                dictionaryParams,
                cancellationToken,
                openMarker,
                closeMarker,
                nullReplacement,
                closeConnectionOnExit))
                yield return item;
        }





        // get here 1 done
        private static async IAsyncEnumerable<dynamic?> ExecuteQueryDictionaryAsync(
            this DbContext dc,
            string query,
            IDictionary<string, object>? queryParams = null,
            CancellationToken? cancellationToken = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            queryParams ??= new Dictionary<string, object>();
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));
            var conn = dc.Database.GetDbConnection();
            bool cont = true;
            DbDataReader? reader = null;
            DbCommand? command = null;
            try
            {
                await conn.EnsureOpenAsync();

                var paramList = Regex.Matches(query, openMarker + QueryParams.RegexPattern + closeMarker)
                    .Cast<Match>()
                    .Select(x => x.Groups["param"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x).Distinct().ToList();

                command = conn.CreateCommand();
                command.CommandType = CommandType.Text;

                if (paramList.Count > 0)
                {
                    var joined = paramList
                        .LeftJoin(queryParams,
                        pl => pl.ToUpper(CultureInfo.InvariantCulture),
                        p => p.Key.ToUpper(CultureInfo.InvariantCulture),
                        (pl, p) => new { k = pl, v = p.Value }).ToList();

                    foreach (var item in joined)
                    {
                        if (item.v == null) query = query
                            .Replace(openMarker + item.k + closeMarker,
                                nullReplacement, true,
                                CultureInfo.InstalledUICulture);
                        else
                        {
                            query = query
                            .Replace(openMarker + item.k + closeMarker,
                                "@vxv_" + item.k, true,
                                CultureInfo.InvariantCulture);
                            var p = command.CreateParameter();
                            p.ParameterName = "@vxv_" + item.k;
                            p.Value = item.v;
                            command.Parameters.Add(p);
                        }
                    }
                }

                
                command.CommandText = query;

                reader = await (cancellationToken == null
                        ? command.ExecuteReaderAsync()
                        : command.ExecuteReaderAsync((CancellationToken)cancellationToken));
            }
            catch(Exception ex)
            {
                if (reader is not null)
                    await reader.EnsureClosedAsync();
                if (closeConnectionOnExit) await conn.EnsureClosedAsync();
                throw new Exception(ex.GenerateError(command, query, queryParams));
            }

            if (reader.HasRows)
            {
                while (cont)
                {
                    try
                    {
                        cont = await reader.ReadAsync();
                        if (!cont) break;
                    }
                    catch(Exception ex)
                    {
                        await reader.EnsureClosedAsync();
                        if (closeConnectionOnExit) await conn.EnsureClosedAsync();
                        throw new Exception(ex.GenerateError(command, query, queryParams));
                    }
                    ExpandoObject result = new();

                    foreach (var item in Enumerable.Range(0, reader.FieldCount)
                            .Select(x => new { Name = reader.GetName(x), Value = reader.GetValue(x) }))
                    {
                        try
                        {
                            result.TryAdd(item.Name, item.Value);
                        }
                        catch { }
                    }

                    yield return result;
                }
            }
            await reader.EnsureClosedAsync();
            if (closeConnectionOnExit) await conn.EnsureClosedAsync();
            yield break;
        }


        public static async IAsyncEnumerable<dynamic?> ExecuteQueryAsync(
            this DbContext dc,
            string query,
            object? queryParams = null,
            CancellationToken? cancellationToken = null,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null",
            bool closeConnectionOnExit = false
            )
        {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));



            if (queryParams == null)
                await foreach (var item in dc.ExecuteQueryDictionaryAsync(
                    query,
                    new Dictionary<string, object>(),
                    cancellationToken,
                    openMarker,
                    closeMarker,
                    nullReplacement,
                    closeConnectionOnExit))
                    yield return item;
            // queryParams cannot be null here
#pragma warning disable CS8604 // Possible null reference argument.
            IDictionary<string, object>? dictionaryParams = queryParams.GetDataModelParameters();
#pragma warning restore CS8604 // Possible null reference argument.

            await foreach (var item in dc.ExecuteQueryDictionaryAsync(
                query,
                dictionaryParams,
                cancellationToken,
                openMarker,
                closeMarker,
                nullReplacement,
                closeConnectionOnExit))
                yield return item;

        }

        #endregion
        #region query generation
        public static string? GenerateInsertCmd<T>(
            this T obj,
            string tableName,
            string openMarker = "{{",
            string closeMarker = "}}"
            ) where T : class
        {
            if (obj == null) return null;
            var queryParams = obj.GetDataModelParameters();
            if (queryParams == null) return null;
            return queryParams.Keys.ToList()
                .GenerateInsertCmd(tableName, openMarker, closeMarker);
        }


        public static string? GenerateInsertCmd(
            this List<string> fields,
            string tableName,
            string openMarker = "{{",
            string closeMarker = "}}"
            )
        {
            if (fields == null
                ||
                fields.Count < 1
                ||
                string.IsNullOrWhiteSpace(tableName)
                ) return null;

            string query = "insert into [" + tableName
                + "]\r\n\t(";
            bool first = true;
            foreach (var item in fields)
            {
                if (first)
                {
                    query += "[" + item + "]";
                    first = false;
                }
                else
                    query += ",\r\n\t[" + item + "]";
            }

            query += ")\r\nvalues\r\n\t(";
            first = true;

            foreach (var item in fields)
            {
                if (first)
                {
                    query += openMarker + item + closeMarker;
                    first = false;
                }
                else
                    query += ",\r\n\t" + openMarker + item + closeMarker;
            }
            return query + ")";
        }
        public static string? GenerateInsertCmd(
            this Dictionary<string, object> fields,
            string tableName,
            string openMarker = "{{",
            string closeMarker = "}}"
            )
        {
            if (fields == null
                ||
                fields.Count < 1
                ||
                string.IsNullOrWhiteSpace(tableName)
                ) return null;
            return GenerateInsertCmd(fields.Keys.ToList(), tableName, openMarker, closeMarker);
        }


        public static string? GenerateUpdateCmd(
            this List<string> fields,
            string tableName,
            string whereClause = "where [id] = {{id}}",
            List<string>? ignoreProperties = null,
            string openMarker = "{{",
            string closeMarker = "}}"
            )
        {
            if (fields == null
                ||
                fields.Count < 1
                ||
                string.IsNullOrWhiteSpace(tableName)
                ||
                string.IsNullOrWhiteSpace(whereClause)
                ) return null;

            string query = "update [" + tableName
                + "] set";
            if (ignoreProperties != null && ignoreProperties.Count > 0)
            {
                fields = fields.Except(ignoreProperties).ToList();
            }
            bool first = true;
            foreach (var item in fields)
            {
                if (first)
                {
                    query += "\r\n\t[" + item + "] = " + openMarker + item + closeMarker;
                    first = false;
                }
                else
                    query += ",\r\n\t[" + item + "] = " + openMarker + item + closeMarker;

            }
            return query + "\r\n" + whereClause;

        }

        public static string? GenerateUpdateCmd(
            this Dictionary<string, object> fields,
            string tableName,
            string whereClause = "where [id] = {{id}}",
            List<string>? ignoreProperties = null,
            string openMarker = "{{",
            string closeMarker = "}}"
            )
        {
            if (fields == null
                ||
                fields.Count < 1
                ||
                string.IsNullOrWhiteSpace(tableName)
                ||
                string.IsNullOrWhiteSpace(whereClause)
                ) return null;
            return GenerateUpdateCmd(fields.Keys.ToList(),
                tableName, whereClause, ignoreProperties, openMarker, closeMarker);
        }



        public static string? GenerateUpdateCmd<T>(
            this T obj,
            string tableName,
            string whereClause = "where [id] = {{id}}",
            List<string>? ignoreProperties = null,
            string openMarker = "{{",
            string closeMarker = "}}"
            ) where T : class
        {
            if (obj == null) return null;
            var queryParams = obj.GetDataModelParameters();
            if (queryParams == null) return null;
            return queryParams.Keys.ToList()
                .GenerateUpdateCmd<List<string>>(tableName,
                whereClause, ignoreProperties, openMarker, closeMarker);
        }

        public static (string?, IDictionary<string, object>?) GenerateUpdateCmdAndParams<T>(
            this T obj,
            string tableName,
            string whereClause = "where [id] = {{id}}",
            List<string>? ignoreProperties = null,
            string openMarker = "{{",
            string closeMarker = "}}"
            ) where T : class
        {
            if (obj == null) return (null, null);
            var fields = obj.GetDataModelParameters();
            if (fields == null || fields.Keys.Count < 1) return (null, null);
            return (
                fields.Keys.ToList().GenerateUpdateCmd(tableName,
                whereClause, ignoreProperties, openMarker, closeMarker),
                fields);
        }

        public static (string?, IDictionary<string, object>?) GenerateInsertCmdAndParams<T>(
            this T obj,
            string tableName,
            string openMarker = "{{",
            string closeMarker = "}}"
            ) where T : class
        {
            if (obj == null) return (null, null);
            var fields = obj.GetDataModelParameters();
            if (fields == null || fields.Keys.Count < 1) return (null, null);
            return (
                fields.Keys.ToList().GenerateInsertCmd(tableName, openMarker, closeMarker),
                fields);
        }

        #endregion


        #region execute query IEnumerable of QueryParams

        /// <summary>
        /// Don't use, not tested yet.
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="query"></param>
        /// <param name="queryParams"></param>
        /// <param name="closeConnectionOnExit"></param>
        /// <returns></returns>
        public static IEnumerable<dynamic> ExecuteQuery(
            this DbContext dc,
            string query,
            IEnumerable<QueryParams>? queryParams,
            bool closeConnectionOnExit = false
            )
        {
            if (queryParams == null) return ExecuteQuery(dc, query);
            return ExecuteQueryNested(dc, query, queryParams, closeConnectionOnExit);
        }



        private static IEnumerable<dynamic> ExecuteQueryNested(
            this DbContext dc,
            string query,
            IEnumerable<QueryParams>? queryParams,
            bool closeConnectionOnExit = false
            )
        {
            if (dc is null) throw new ArgumentNullException(nameof(dc));
            queryParams ??= new List<QueryParams>() { new QueryParams() };
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));
            if (dc.Database is null) throw new NullReferenceException("Com.H.EF.Relational.QueryExtensions.ExecuteQueryNested(): dc.Database is null");
            var conn = dc.Database.GetDbConnection();
            bool cont = true;
            DbDataReader? reader = null;
            DbCommand? command = null;
            try
            {
                conn.EnsureOpen();
                Dictionary<string, int> varNameCount = new();
                var paramList = queryParams
                    .SelectMany(x =>
                    {
                        var dicParams = x.DataModel?.GetDataModelParameters();
                        return Regex.Matches(query, x.OpenMarker + QueryParams.RegexPattern + x.CloseMarker)
                            .Cast<Match>()
                            .Select(x => x.Groups["param"].Value)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct()
                            .Select(varName => new
                            {
                                VarName = varName,
                                DbParamName =
                                $"@vxv_{(varNameCount.ContainsKey(varName) ? ++varNameCount[varName] : varNameCount[varName] = 1) }_{varName}"
                                ,
                                x.OpenMarker,
                                x.CloseMarker,
                                x.NullReplacement,
                                Value = dicParams?.ContainsKey(varName) == true ? dicParams[varName] : null
                            });
                    }).ToList();

                command = conn.CreateCommand();
                if (paramList.Count > 0)
                {
                    foreach (var item in paramList)
                    {
                        if (item.Value == null)
                        {
                            query = query
                            .Replace(item.OpenMarker + item.VarName + item.CloseMarker,
                                item.NullReplacement, true,
                                CultureInfo.InstalledUICulture);
                            // continue;
                        }
                        else
                        {
                            query = query
                            .Replace(item.OpenMarker + item.VarName + item.CloseMarker,
                            item.DbParamName, true,
                                CultureInfo.InvariantCulture);

                            var p = command.CreateParameter();
                            p.ParameterName = item.DbParamName;
                            p.Value = item.Value;
                            command.Parameters.Add(p);

                        }
                    }
                }
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                reader = command.ExecuteReader();
            }
            catch(Exception ex)
            {
                reader?.EnsureClosed();
                if (closeConnectionOnExit) conn.EnsureClosed();
                throw new Exception(ex.GenerateError(command, query, queryParams));
            }

            if (reader.HasRows)
            {
                while (cont)
                {
                    try
                    {
                        cont = reader.Read();
                        if (!cont) break;
                    }
                    catch
                    {
                        reader.EnsureClosed();
                        if (closeConnectionOnExit) conn.EnsureClosed();
                        // throw new Exception(ex.GenerateError(command));
                        throw;
                    }
                    ExpandoObject result = new();
                    foreach (var item in Enumerable.Range(0, reader.FieldCount)
                            .Select(x => new { Name = reader.GetName(x), Value = reader.GetValue(x) }))
                    {
                        try
                        {
                            result.TryAdd(item.Name, item.Value);
                        }
                        catch { }
                    }

                    yield return result;
                }
            }
            reader.EnsureClosed();
            if (closeConnectionOnExit) conn.EnsureClosed();
            yield break;
        }

        private static string GenerateError(
            this Exception ex,
            DbCommand? command,
            string query, 
            IDictionary<string, object>? queryParams
            )
        {
            string errMsg = "Error executing query:";

            if (command is not null)
                errMsg +=
                "\r\n-----------\r\n"
                + "Parameters:\r\n"
                + string.Join("\r\n",
                command.Parameters.Cast<DbParameter>().Select(x => $"{x.ParameterName} = {x.Value}"));
            else if (queryParams is not null)
                errMsg +=
                "\r\n-----------\r\n"
                + "Parameters:\r\n"
                + string.Join("\r\n",
                queryParams.Select(x=> $"{x.Key} = {x.Value}"));

            if (command is not null)
                errMsg += "\r\n-----\r\nQuery\r\n"
                + command.CommandText + "\r\n-------\r\n";
            else if (query is not null)
                errMsg += "\r\n-----\r\nQuery\r\n"
                + query + "\r\n-------\r\n";

            errMsg += "Error msg:\r\n"
                + ex.Message;
            return errMsg;

        }

        private static string GenerateError(
            this Exception ex,
            DbCommand? command,
            string query,
            IEnumerable<QueryParams>? queryParams
            )
        {
            string errMsg = "Error executing query:";

            if (command is not null)
                errMsg +=
                "\r\n-----------\r\n"
                + "Parameters:\r\n"
                + string.Join("\r\n",
                command.Parameters.Cast<DbParameter>().Select(x => $"{x.ParameterName} = {x.Value}"));
            else if (queryParams is not null)
                errMsg +=
                "\r\n-----------\r\n"
                + "Parameters:\r\n"
                + string.Join("\r\n",
                queryParams
                // .Where(x=>x?.DataModel is not null)
                .SelectMany(x=>x?.DataModel?.GetDataModelParameters()??new Dictionary<string, object>())
                .Select(x => $"{x.Key} = {x.Value}"));

            if (command is not null)
                errMsg += "\r\n-----\r\nQuery\r\n"
                + command.CommandText + "\r\n-------\r\n";
            else if (query is not null)
                errMsg += "\r\n-----\r\nQuery\r\n"
                + query + "\r\n-------\r\n";

            errMsg += "Error msg:\r\n"
                + ex.Message;
            return errMsg;

        }



        #endregion

        #region Creating DbContext for a given connection string

        /// <summary>
        /// Creates a DbContext for a given connection string. Support currently for SQL Server and SQLite
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="providerClientAssemblyNameOrDllPath">If not provided defaults to Microsoft.EntityFrameworkCore.SqlServer, 
        /// then to Microsoft.EntityFrameworkCore.Sqlite if SqlServer is not found</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static DbContext CreateDbContext(this string connectionString, 
            string? providerClientAssemblyNameOrDllPath = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            var clientAssembly = string.IsNullOrWhiteSpace(providerClientAssemblyNameOrDllPath) 
                ? "Microsoft.EntityFrameworkCore.SqlServer": providerClientAssemblyNameOrDllPath;

            Assembly? assembly = null;
            try
            {
                assembly = Com.H.Reflection.ReflectionExtensions.LoadAssembly(clientAssembly);
            }
            catch { }
            
            if (assembly is null && string.IsNullOrWhiteSpace(providerClientAssemblyNameOrDllPath))
            {
                clientAssembly = "Microsoft.EntityFrameworkCore.Sqlite";
                try
                {
                    assembly = Com.H.Reflection.ReflectionExtensions.LoadAssembly(clientAssembly);
                }
                catch { }
            }

            if (assembly is null)
            {
                if (string.IsNullOrWhiteSpace(providerClientAssemblyNameOrDllPath))
                    throw new NotSupportedException("Couldn't find default assemblies" 
                        + " Microsoft.EntityFrameworkCore.SqlServer nor Microsoft.EntityFrameworkCore.Sqlite." 
                        + " Either add NuGet packages for any of the default assemblies"
                        + $" or provide your custom assembly name or dll path in {nameof(providerClientAssemblyNameOrDllPath)} parameter");
                else throw new NotSupportedException($"Couldn't find assembly {providerClientAssemblyNameOrDllPath}");
            }
            var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
            if (clientAssembly?.EqualsIgnoreCase("Microsoft.EntityFrameworkCore.SqlServer") == true)
            {

                var extClassType = assembly.GetType("Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions");
                if (extClassType is null)
                    throw new NotSupportedException("Couldn't find class Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions"
                        + " in assembly Microsoft.EntityFrameworkCore.SqlServer");
                var thirdParam = Type.GetType("System.Action`1[[Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder, Microsoft.EntityFrameworkCore.SqlServer]]");
                if (thirdParam is null) throw new NotSupportedException("Couldn't find type System.Action`1[[Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder, Microsoft.EntityFrameworkCore.SqlServer]]");
                var method = extClassType.GetMethod("UseSqlServer", new Type[] { typeof(DbContextOptionsBuilder<DbContext>), typeof(string), thirdParam });
                if (method is null)
                    throw new NotSupportedException("Couldn't find method UseSqlServer in class Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions"
                        + " of assembly Microsoft.EntityFrameworkCore.SqlServer");

                var args = new object[] { optionsBuilder, connectionString, null! };
                method.Invoke(null, args);
                return new DbContext(optionsBuilder.Options);
            }
            else if (providerClientAssemblyNameOrDllPath?.EqualsIgnoreCase("Microsoft.EntityFrameworkCore.Sqlite") == true)
            {
                // https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.sqlitedbcontextoptionsbuilderextensions.usesqlite?view=efcore-6.0#microsoft-entityframeworkcore-sqlitedbcontextoptionsbuilderextensions-usesqlite-1(microsoft-entityframeworkcore-dbcontextoptionsbuilder((-0))-system-string-system-action((microsoft-entityframeworkcore-infrastructure-sqlitedbcontextoptionsbuilder)))

                var extClassType = assembly.GetType("Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions");
                if (extClassType is null)
                    throw new NotSupportedException("Couldn't find class Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions"
                        + " in assembly Microsoft.EntityFrameworkCore.Sqlite");
                var thirdParam = Type.GetType("System.Action`1[[Microsoft.EntityFrameworkCore.Infrastructure.SqliteDbContextOptionsBuilder, Microsoft.EntityFrameworkCore.Sqlite]]");
                if (thirdParam is null) throw new NotSupportedException("Couldn't find type System.Action`1[[Microsoft.EntityFrameworkCore.Infrastructure.SqliteDbContextOptionsBuilder, Microsoft.EntityFrameworkCore.Sqlite]]");
                var method = extClassType.GetMethod("UseSqlite", new Type[] { typeof(DbContextOptionsBuilder<DbContext>), typeof(string), thirdParam });
                if (method is null)
                    throw new NotSupportedException("Couldn't find method UseSqlite in class Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions"
                        + " of assembly Microsoft.EntityFrameworkCore.Sqlite");

                var args = new object[] { optionsBuilder, connectionString, null! };
                method.Invoke(null, args);
                return new DbContext(optionsBuilder.Options);
            }
            else throw new NotSupportedException($"Provider {providerClientAssemblyNameOrDllPath} is not supported");

        }
        #endregion

        #region creating default template data processor

        // TODO: Consider moving it to Com.H.Text.Template.TemplateExtensions.cs
        public static IEnumerable<dynamic>? GetDefaultDataProcessors(TemplateMultiDataRequest req)
        {
            // todo: get ConnectionString, ContentType, and PreRender from attributes instead of properties within the request
            // as it is now. Once done, remove the implementation of those properties from the request

            // return null if no data is requested
            if (req?.Request is null
                || string.IsNullOrEmpty(req.ConnectionString))
                return null;

            #region DbContext
            DbContext? dc = null;
            if (req.ContentType.EqualsIgnoreCase("sql"))
                dc = CreateDbContext(req.ConnectionString, "Microsoft.EntityFrameworkCore.SqlServer");
            else if (req.ContentType.EqualsIgnoreCase("sql-lite") 
                || req.ContentType.EqualsIgnoreCase("sql_lite")
                || req.ContentType.EqualsIgnoreCase("sqllite")
                )
                dc = CreateDbContext(req.ConnectionString, "Microsoft.EntityFrameworkCore.Sqlite");

            #endregion
            if (dc is null) throw new NotSupportedException($"Content type {req.ContentType} is not supported in default data processors."
                + " Visit https://github.com/H7O/Com.H.EF.Relational/blob/master/Net6/QueryExtensions.cs and check GetDefaultDataProcessors()"
                + " to use as an example of how to create your own data processor");
            if (req.PreRender) req.Request = req.Request.Fill(req.QueryParamsList);
            return dc.ExecuteQuery(req.Request, req.QueryParamsList);
        }

        //public static IEnumerable<dynamic>? GetDefaultDataProcessors(TemplateDataRequest req)
        //{
        //    // return null if no data is requested
        //    if (req?.Request is null
        //        || string.IsNullOrEmpty(req.ConnectionString))
        //        return null;

        //    #region DbContext
        //    DbContext? dc = null;
        //    if (req.ContentType.EqualsIgnoreCase("sql"))
        //        dc = CreateDbContext(req.ConnectionString, "Microsoft.EntityFrameworkCore.SqlServer");
        //    else if (req.ContentType.EqualsIgnoreCase("sql-lite")
        //        || req.ContentType.EqualsIgnoreCase("sql_lite")
        //        || req.ContentType.EqualsIgnoreCase("sqllite")
        //        )
        //        dc = CreateDbContext(req.ConnectionString, "Microsoft.EntityFrameworkCore.Sqlite");

        //    #endregion
        //    if (dc is null) throw new NotSupportedException($"Content type {req.ContentType} is not supported in default data processors."
        //        + " Visit https://github.com/H7O/Com.H.EF.Relational/blob/master/Net6/QueryExtensions.cs and check GetDefaultDataProcessors()"
        //        + " to use as an example of how to create your own data processor");
            
        //    return dc.ExecuteQuery(req.Request, (object?) req.DataModel, req.OpenMarker!, req.CloseMarker!, req.NullReplacement!);
        //}


        #endregion


    }
}

