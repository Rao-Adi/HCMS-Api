using Dapper;
using System.Threading.Tasks;

namespace HCMS_Api.Components.DMS.Common.Dapper;

public interface IDMSDapperDataService
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null);
    Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null);
    Task<T> QueryMultipleAsync<T>(string sql, object? parameters, Func<SqlMapper.GridReader, Task<T>> map);
    Task<int> ExecuteAsync(string sql, object? parameters = null);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null);

    void BeginTransaction();
    void Commit();
    void Rollback();
}
