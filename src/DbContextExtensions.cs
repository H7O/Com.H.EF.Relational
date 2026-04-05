using System.Data.Common;
using Com.H.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Com.H.EF.Relational
{
    /// <summary>
    /// Extension methods for <see cref="DbContext"/> that provide raw SQL query execution
    /// with dynamic and strongly-typed results. Delegates to Com.H.Data.Common extension
    /// methods on the underlying <see cref="DbConnection"/>.
    /// </summary>
    public static class DbContextExtensions
    {
        #region ExecuteQueryAsync (dynamic)

        /// <summary>
        /// Executes a SQL query asynchronously on the DbContext's underlying connection
        /// and returns a disposable result that implements <see cref="IAsyncEnumerable{T}"/> of dynamic.
        /// </summary>
        /// <param name="dbContext">The EF Core DbContext.</param>
        /// <param name="query">SQL query text. Use {{paramName}} syntax for parameters.</param>
        /// <param name="queryParams">Parameters object (anonymous object, Dictionary, JsonElement, JSON string, or custom object).</param>
        /// <param name="queryParamsRegex">Regex pattern for parameter delimiters.</param>
        /// <param name="commandTimeout">Command timeout in seconds. If null, uses the default.</param>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>A <see cref="DbAsyncQueryResult{T}"/> that implements IAsyncEnumerable&lt;dynamic&gt; and IAsyncDisposable.</returns>
        public static Task<DbAsyncQueryResult<dynamic>> ExecuteQueryAsync(
            this DbContext dbContext,
            string query,
            object? queryParams = null,
            string queryParamsRegex = @"(?<open_marker>\{\{)(?<param>.*?)?(?<close_marker>\}\})",
            int? commandTimeout = null,
            CancellationToken cToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            var connection = dbContext.Database.GetDbConnection();
            return connection.ExecuteQueryAsync(query, queryParams, queryParamsRegex, commandTimeout, false, cToken);
        }

        #endregion

        #region ExecuteQuery (dynamic)

        /// <summary>
        /// Executes a SQL query synchronously on the DbContext's underlying connection
        /// and returns a disposable result that implements <see cref="IEnumerable{T}"/> of dynamic.
        /// </summary>
        /// <param name="dbContext">The EF Core DbContext.</param>
        /// <param name="query">SQL query text. Use {{paramName}} syntax for parameters.</param>
        /// <param name="queryParams">Parameters object (anonymous object, Dictionary, JsonElement, JSON string, or custom object).</param>
        /// <param name="queryParamsRegex">Regex pattern for parameter delimiters.</param>
        /// <param name="commandTimeout">Command timeout in seconds. If null, uses the default.</param>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>A <see cref="DbQueryResult{T}"/> that implements IEnumerable&lt;dynamic&gt; and IDisposable.</returns>
        public static DbQueryResult<dynamic> ExecuteQuery(
            this DbContext dbContext,
            string query,
            object? queryParams = null,
            string queryParamsRegex = @"(?<open_marker>\{\{)(?<param>.*?)?(?<close_marker>\}\})",
            int? commandTimeout = null,
            CancellationToken cToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            var connection = dbContext.Database.GetDbConnection();
            return connection.ExecuteQuery(query, queryParams, queryParamsRegex, commandTimeout, false, cToken);
        }

        #endregion

        #region ExecuteQueryAsync<T>

        /// <summary>
        /// Executes a SQL query asynchronously on the DbContext's underlying connection
        /// and returns a typed disposable result that implements <see cref="IAsyncEnumerable{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type to map query results to.</typeparam>
        /// <param name="dbContext">The EF Core DbContext.</param>
        /// <param name="query">SQL query text. Use {{paramName}} syntax for parameters.</param>
        /// <param name="queryParams">Parameters object (anonymous object, Dictionary, JsonElement, JSON string, or custom object).</param>
        /// <param name="queryParamsRegex">Regex pattern for parameter delimiters.</param>
        /// <param name="commandTimeout">Command timeout in seconds. If null, uses the default.</param>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>A <see cref="DbAsyncQueryResult{T}"/> that implements IAsyncEnumerable&lt;T&gt; and IAsyncDisposable.</returns>
        public static Task<DbAsyncQueryResult<T>> ExecuteQueryAsync<T>(
            this DbContext dbContext,
            string query,
            object? queryParams = null,
            string queryParamsRegex = @"(?<open_marker>\{\{)(?<param>.*?)?(?<close_marker>\}\})",
            int? commandTimeout = null,
            CancellationToken cToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            var connection = dbContext.Database.GetDbConnection();
            return connection.ExecuteQueryAsync<T>(query, queryParams, queryParamsRegex, commandTimeout, false, cToken);
        }

        #endregion

        #region ExecuteQuery<T>

        /// <summary>
        /// Executes a SQL query synchronously on the DbContext's underlying connection
        /// and returns a typed disposable result that implements <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type to map query results to.</typeparam>
        /// <param name="dbContext">The EF Core DbContext.</param>
        /// <param name="query">SQL query text. Use {{paramName}} syntax for parameters.</param>
        /// <param name="queryParams">Parameters object (anonymous object, Dictionary, JsonElement, JSON string, or custom object).</param>
        /// <param name="queryParamsRegex">Regex pattern for parameter delimiters.</param>
        /// <param name="commandTimeout">Command timeout in seconds. If null, uses the default.</param>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>A <see cref="DbQueryResult{T}"/> that implements IEnumerable&lt;T&gt; and IDisposable.</returns>
        public static DbQueryResult<T> ExecuteQuery<T>(
            this DbContext dbContext,
            string query,
            object? queryParams = null,
            string queryParamsRegex = @"(?<open_marker>\{\{)(?<param>.*?)?(?<close_marker>\}\})",
            int? commandTimeout = null,
            CancellationToken cToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            var connection = dbContext.Database.GetDbConnection();
            return connection.ExecuteQuery<T>(query, queryParams, queryParamsRegex, commandTimeout, false, cToken);
        }

        #endregion

        #region ExecuteCommandAsync

        /// <summary>
        /// Executes a non-query command asynchronously (INSERT, UPDATE, DELETE, etc.) on the DbContext's underlying connection.
        /// </summary>
        /// <param name="dbContext">The EF Core DbContext.</param>
        /// <param name="query">SQL command text.</param>
        /// <param name="queryParams">Parameters object (anonymous object, Dictionary, JsonElement, JSON string, or custom object).</param>
        /// <param name="queryParamsRegex">Regex pattern for parameter delimiters.</param>
        /// <param name="commandTimeout">Command timeout in seconds. If null, uses the default.</param>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ExecuteCommandAsync(
            this DbContext dbContext,
            string query,
            object? queryParams = null,
            string queryParamsRegex = @"(?<open_marker>\{\{)(?<param>.*?)?(?<close_marker>\}\})",
            int? commandTimeout = null,
            CancellationToken cToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            var connection = dbContext.Database.GetDbConnection();
            return connection.ExecuteCommandAsync(query, queryParams, queryParamsRegex, commandTimeout, false, cToken);
        }

        #endregion

        #region ExecuteCommand

        /// <summary>
        /// Executes a non-query command synchronously (INSERT, UPDATE, DELETE, etc.) on the DbContext's underlying connection.
        /// </summary>
        /// <param name="dbContext">The EF Core DbContext.</param>
        /// <param name="query">SQL command text.</param>
        /// <param name="queryParams">Parameters object (anonymous object, Dictionary, JsonElement, JSON string, or custom object).</param>
        /// <param name="queryParamsRegex">Regex pattern for parameter delimiters.</param>
        /// <param name="commandTimeout">Command timeout in seconds. If null, uses the default.</param>
        /// <param name="cToken">Cancellation token.</param>
        public static void ExecuteCommand(
            this DbContext dbContext,
            string query,
            object? queryParams = null,
            string queryParamsRegex = @"(?<open_marker>\{\{)(?<param>.*?)?(?<close_marker>\}\})",
            int? commandTimeout = null,
            CancellationToken cToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            var connection = dbContext.Database.GetDbConnection();
            connection.ExecuteCommand(query, queryParams, queryParamsRegex, commandTimeout, false, cToken);
        }

        #endregion
    }
}
