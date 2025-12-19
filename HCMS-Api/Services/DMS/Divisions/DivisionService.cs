using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Divisions;
using Org.BouncyCastle.Crypto;
using System.Data;
using static Azure.Core.HttpHeader;

namespace HCMS_Api.Services.DMS.Divisions;

public class DivisionService : IDivisionService
{

    private readonly DMSCommon _common;
    public DivisionService(DMSCommon common)
    {
        _common = common;
    }

    public async Task<DivisionReadDto> CreateAsync(DivisionCreateDto input)
    {
        try
        {
            // Check duplicate
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM Divisions
                WHERE Name = '{input.Name.Replace("'", "''")}'
                  AND IsActive = 1
                  AND IsDeleted = 0";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Division already exist", 200);

            // Insert
            string insertQuery = $@"
                INSERT INTO Divisions (Code, Name, IsActive, IsDeleted)
                VALUES (
                    '{input.Code.Replace("'", "''")}',
                    '{input.Name.Replace("'", "''")}',
                    1,
                    0
                );

            SELECT SCOPE_IDENTITY();";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Return created record
            string selectQuery = $@"
                SELECT Id, Name, Code, IsActive
                FROM Divisions
                WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);
            DataRow row = dt.Rows[0];

            return new DivisionReadDto
            {
                Name = row.Field<string>("Name"),
                Code = row.Field<string>("Code"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM Divisions
                WHERE Code = {code}
                  AND IsDeleted = 0";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Division not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Divisions
                SET IsDeleted = 1
                WHERE Code = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DivisionReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE IsDeleted = 0 
                  AND IsActive = " + (input.IsActive ? "1" : "0");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(Name) LIKE '%{search}%'
                    OR UPPER(Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "Name",
                "PREFIX" => "Code",
                "ISACTIVE" => "IsActive",
                _ => "Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.pageNo - 1) * input.pageSize;

            string query = $@"
                        SELECT *
                        FROM Divisions
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.pageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Divisions
                        {whereClause};
                    ";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            // Split result sets
            var divisions = dt.AsEnumerable()
                 .Select(row => new DivisionReadDto
                 {
                     Code = row.Table.Columns.Contains("Code")
                         ? row.Field<string>("Code")
                         : string.Empty,

                     Name = row.Table.Columns.Contains("Name")
                         ? row.Field<string>("Name")
                         : string.Empty,

                     IsActive = row.Table.Columns.Contains("IsActive")
                         && row.Field<bool?>("IsActive") == true,

                     IsDeleted = row.Table.Columns.Contains("IsDeleted")
                         && row.Field<bool?>("IsDeleted") == true,

                     CreatedAt = row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt")
                         ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
                         : null,

                     CreatedBy = row.Table.Columns.Contains("CreatedBy")
                         ? row.Field<string>("CreatedBy")
                         : string.Empty,

                     LastModifiedAt = row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt")
                         ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss")
                         : null,

                     LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy")
                         ? row.Field<string>("LastModifiedBy")
                         : string.Empty
                 })
                 .ToList();

            // Second result set → Total Count
            int totalCount = 0;
            if (dt.DataSet!.Tables.Count > 1 && dt.DataSet.Tables[1].Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(dt.DataSet.Tables[1].Rows[0][0]);
            }

            return new PaginationResult<DivisionReadDto>
            {
                Items = divisions,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IQueryable<SelectListDto>> GetAllSelectList()
    {
        try
        {
            string query = @"
            SELECT Code, Name
            FROM Divisions
            WHERE IsActive = 1
              AND IsDeleted = 0
            ORDER BY Name";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Code"),
                    Value = row.Field<string>("Name")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DivisionReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT Id, Name, Code, IsActive
                FROM Divisions
                WHERE Code = {code}
                  AND IsActive = 1
                  AND IsDeleted = 0";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Division not found", 200);

            DataRow row = dt.Rows[0];

            return new DivisionReadDto
            {
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DivisionReadDto> UpdateAsync(DivisionUpdateDto input)
    {
        try
        {
            // Check existence
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Divisions
            WHERE Code = {input.Code}
              AND IsDeleted = 0";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Division not found", 200);

            // Update
            string updateQuery = $@"
                UPDATE Divisions
                SET 
                    Name = '{input.Name.Replace("'", "''")}',
                    IsActive = {(input.IsActive ? 1 : 0)}
                WHERE Code = {input.Code}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                SELECT Code, Name, Code, IsActive
                FROM Divisions
                WHERE Code = {input.Code}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);
            DataRow row = dt.Rows[0];

            return new DivisionReadDto
            {
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

}
