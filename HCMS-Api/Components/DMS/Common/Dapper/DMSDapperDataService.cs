using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;

namespace HCMS_Api.Components.DMS.Common.Dapper;

public class DMSDapperDataService : IDMSDapperDataService, IDisposable
{
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;

    public DMSDapperDataService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DMSConnectionString");
        _connection = new NpgsqlConnection(connectionString);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        EnsureConnectionOpen();
        return await _connection.QueryAsync<T>(sql, parameters, _transaction);
    }

    public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null)
    {
        EnsureConnectionOpen();
        return await _connection.QueryFirstOrDefaultAsync<T>(sql, parameters, _transaction);
    }

    public async Task<T> QueryMultipleAsync<T>(
        string sql,
        object? parameters,
        Func<SqlMapper.GridReader, Task<T>> map)
    {
        EnsureConnectionOpen();
        using var grid = await _connection.QueryMultipleAsync(sql, parameters, _transaction);
        return await map(grid);
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        EnsureConnectionOpen();
        return await _connection.ExecuteAsync(sql, parameters, _transaction);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null)
    {
        EnsureConnectionOpen();
        return await _connection.ExecuteScalarAsync<T>(sql, parameters, _transaction);
    }

    public void BeginTransaction()
    {
        EnsureConnectionOpen();
        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        _transaction?.Commit();
        _transaction?.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _transaction = null;
    }

    private void EnsureConnectionOpen()
    {
        if (_connection.State == ConnectionState.Broken)
        {
            _connection.Close();
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();

        if (_connection.State == ConnectionState.Open)
            _connection.Close();

        _connection.Dispose();
    }
}
