
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
}

