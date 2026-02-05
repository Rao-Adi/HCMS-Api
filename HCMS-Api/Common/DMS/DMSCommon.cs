
using Dapper;
using global::HCMS_Api.Models;
using HCMS_Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Text.Json;


namespace HCMS_Api.Common.DMS;
public class DMSCommon
{
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DMSCommon(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _connectionString = configuration.GetConnectionString("DMSConnectionString");
        _httpContextAccessor = httpContextAccessor;
    }

    //------------------------------------------------
    // CONNECTION FACTORY
    //------------------------------------------------

    public async Task<NpgsqlConnection> CreateOpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    //------------------------------------------------
    // TRANSACTION FACTORY
    //------------------------------------------------

    public async Task<NpgsqlTransaction> BeginTransactionAsync()
    {
        var conn = await CreateOpenConnectionAsync();
        return await conn.BeginTransactionAsync();
    }



    // ============================
    // DataTable → JSON Helper
    // ============================
    public List<Dictionary<string, object>> GetJsonDatatable(DataTable table)
    {
        var rows = new List<Dictionary<string, object>>();

        foreach (DataRow row in table.Rows)
        {
            var rowDict = new Dictionary<string, object>();

            foreach (DataColumn col in table.Columns)
            {
                var value = row[col];
                rowDict[col.ColumnName] =
                    value == DBNull.Value || (value is string s && string.IsNullOrWhiteSpace(s))
                    ? null
                    : value;
            }

            rows.Add(rowDict);
        }

        return rows;
    }

    // ============================
    // Execute Query → DataTable
    // ============================
    public async Task<DataTable> ExecuteSqlQuery(string query)
    {
        var dataTable = new DataTable();
        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(query, connection))
                using (var adapter = new NpgsqlDataAdapter(command))
                {
                    adapter.Fill(dataTable);
                }
            }

            return dataTable;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }


    public async Task<DataSet> ExecuteSqlQueryMultiple(string query)
    {
        var dataSet = new DataSet();

        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(query, connection))
                using (var adapter = new NpgsqlDataAdapter(command))
                {
                    adapter.Fill(dataSet);
                }
            }

            return dataSet;
        }
        catch (Exception ex)
        {
            throw;  // no need to "throw ex;" just "throw;"
        }
    }


    // ============================
    // Execute Query → Single Row
    // ============================
    public async Task<Dictionary<string, object>> ExecuteSqlQuerySingleRow(string query)
    {
        var result = new Dictionary<string, object>();

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = new NpgsqlCommand(query, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        result[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                }
            }
        }

        return result;
    }

    // ============================
    // JWT User
    // ============================
    public async Task<JwtArray> GetJwtUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userDataClaim = user?.FindFirst("UserData")?.Value;

        if (string.IsNullOrEmpty(userDataClaim))
            throw new Exception("UserData claim not found.");

        return JsonSerializer.Deserialize<JwtArray>(userDataClaim);
    }

    // ============================
    // Execute Scalar (string)
    // ============================
    public string ExecuteScalarQuery(string query)
    {
        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new NpgsqlCommand(query, connection))
                {
                    var result = command.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }
        catch
        {
            return null;
        }
    }

    // ────────────────────────────────────────────────
    // New / improved version that accepts parameters
    // ────────────────────────────────────────────────
    public string ExecuteScalarQuery(string query, IDictionary<string, object> parameters = null)
    {
        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new NpgsqlCommand(query, connection))
                {
                    if (parameters != null && parameters.Count > 0)
                    {
                        foreach (var param in parameters)
                        {
                            // Npgsql handles nulls, DBNull, bool → correct PostgreSQL types, etc.
                            var npgsqlParam = new NpgsqlParameter(param.Key, param.Value ?? DBNull.Value);
                            command.Parameters.Add(npgsqlParam);
                        }
                    }

                    var result = command.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            // Consider proper logging here instead of silent fail
            // e.g. _logger.LogError(ex, "ExecuteScalarQuery failed: {Query}", query);
            return null;
        }
    }

    // ============================
    // Execute Scalar (generic)
    // ============================
    public object ExecuteScalarQuery(string colName, string tableName, string whereClause)
    {
        try
        {
            string query = $"SELECT {colName} FROM {tableName} WHERE {whereClause}";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new NpgsqlCommand(query, connection))
                {
                    return command.ExecuteScalar();
                }
            }
        }
        catch
        {
            return null;
        }
    }

    // ============================
    // Execute Non Query
    // ============================
    public bool ExecuteNonQuery(string query)
    {
        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new NpgsqlCommand(query, connection))
                {
                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    // ============================
    // Date Validator
    // ============================
    public bool CheckDate(string dateVal)
    {
        if (!DateTime.TryParse(dateVal, out DateTime dt))
            return false;

        return dt.Year >= 1900 && dt.Year <= 2099;
    }



    //------------------------------------------------
    // EXECUTE SCALAR
    //------------------------------------------------

    public async Task<T> ExecuteScalarAsync<T>(
        string query,
        object parameters = null,
        IDbTransaction tx = null)
    {
        try
        {
            if (tx != null)
                return await tx.Connection.ExecuteScalarAsync<T>(query, parameters, tx);

            await using var conn = await CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<T>(query, parameters);
        }
        catch
        {
            throw; // NEVER use throw ex;
        }
    }

    //------------------------------------------------
    // EXECUTE (INSERT/UPDATE/DELETE)
    //------------------------------------------------

    public async Task<int> ExecuteAsync(
        string query,
        object parameters = null,
        IDbTransaction tx = null)
    {
        try
        {
            if (tx != null)
                return await tx.Connection.ExecuteAsync(query, parameters, tx);

            await using var conn = await CreateOpenConnectionAsync();
            return await conn.ExecuteAsync(query, parameters);
        }
        catch
        {
            throw;
        }
    }

    //------------------------------------------------
    // QUERY SINGLE
    //------------------------------------------------

    public async Task<T> QuerySingleAsync<T>(
        string query,
        object parameters = null,
        IDbTransaction tx = null)
    {
        try
        {
            if (tx != null)
                return await tx.Connection.QuerySingleAsync<T>(query, parameters, tx);

            await using var conn = await CreateOpenConnectionAsync();
            return await conn.QuerySingleAsync<T>(query, parameters);
        }
        catch
        {
            throw;
        }
    }

    //------------------------------------------------
    // QUERY FIRST OR DEFAULT
    //------------------------------------------------

    public async Task<T> QueryFirstOrDefaultAsync<T>(
        string query,
        object parameters = null,
        IDbTransaction tx = null)
    {
        try
        {
            if (tx != null)
                return await tx.Connection.QueryFirstOrDefaultAsync<T>(query, parameters, tx);

            await using var conn = await CreateOpenConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<T>(query, parameters);
        }
        catch
        {
            throw;
        }
    }


    public async Task<IEnumerable<T>> QueryAsync<T>(
    string query,
    object parameters = null,
    IDbTransaction tx = null)
    {
        try
        {
            if (tx != null)
                return await tx.Connection.QueryAsync<T>(query, parameters, tx);

            await using var conn = await CreateOpenConnectionAsync();
            return await conn.QueryAsync<T>(query, parameters);
        }
        catch
        {
            throw;
        }
    }



    //public async Task<T> ExecuteScalarAsync<T>(
    //string query,
    //object parameters = null,
    //NpgsqlTransaction transaction = null)
    //{
    //    try
    //    {
    //        var connection = transaction?.Connection
    //                         ?? new NpgsqlConnection(_connectionString);

    //        if (connection.State != ConnectionState.Open)
    //            await connection.OpenAsync();

    //        using var command = new NpgsqlCommand(query, connection);

    //        if (transaction != null)
    //            command.Transaction = transaction;

    //        if (parameters != null)
    //        {
    //            foreach (var prop in parameters.GetType().GetProperties())
    //            {
    //                var value = prop.GetValue(parameters) ?? DBNull.Value;
    //                command.Parameters.AddWithValue(prop.Name, value);
    //            }
    //        }

    //        var result = await command.ExecuteScalarAsync();

    //        if (result == null || result == DBNull.Value)
    //            return default;

    //        return (T)Convert.ChangeType(result, typeof(T));
    //    }
    //    catch (Exception ex)
    //    {
    //        throw ex;
    //        // DO NOT swallow exceptions in governance systems
    //        //throw new DataAccessException("ExecuteScalarAsync failed.", ex);
    //    }
    //}


    //public async Task<T> ExecuteScalarAsync<T>(
    //string query,
    //object parameters = null,
    //IDbTransaction tx = null)
    //{
    //    try
    //    {
    //        if (tx != null)
    //            return await tx.Connection.ExecuteScalarAsync<T>(query, parameters, tx);

    //        await using var conn = new NpgsqlConnection(_connectionString);
    //        return await conn.ExecuteScalarAsync<T>(query, parameters);
    //    }
    //    catch (Exception ex)
    //    {
    //        throw ex;
    //        //throw new DataAccessException("Database scalar execution failed.", ex);
    //    }
    //}


}

