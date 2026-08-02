﻿using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class UserRoleComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public UserRoleComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common
        )
    {
        _http = http;
        //_logger = logger;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common; 
    }


    public async Task<UserRoleReadDto> CreateAsync(UserRoleCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());


            if (input.UserId < 0)
                throw new CustomException("UserRole is required.", 400);

            // Check duplicate by UserId OR UserId
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM UserRoles
            WHERE UserId = {input.UserId}
              AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("UserRole already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO UserRoles
            (   CompanyId,
                UserId,
                RoleId,
                AssignedAt,
                AssignedBy,
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                {CompanyId}, 
                '{input.UserId}', 
                '{input.RoleId}',
                '{input.AssignedAt}',
                '{input.AssignedBy}',
                TRUE,
                FALSE,
                NOW(),
                '{empCode.Replace("'", "''")}',
                NOW(),
                '{empCode.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@" 
            SELECT u.*, c.Id AS CompanyId, c.Name AS Company
            FROM UserRoles u
            LEFT JOIN Companies c
            ON u.CompanyId = c.Id
            WHERE u.Id = {newId} AND u.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new UserRoleReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                RoleId = row.Field<int>("RoleId"),
                AssignedAt = row.Field<DateTime>("AssignedAt"),
                AssignedBy = row.Field<string>("AssignedBy"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch
        {
            throw;
        }
    }


    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM UserRoles
                WHERE UserId = {code}
                  AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("UserRole not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE UserRoles
                SET IsDeleted = True,
                    IsActive = False,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE UserId = {code} AND CompanyId = {CompanyId}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<UserRoleReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE u.IsDeleted = False 
                  AND u.CompanyId = " + CompanyId + @"
                  AND u.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(u.UserId) LIKE '%{search}%'
                    OR UPPER(u.UserId) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "u.UserId",
                "ROLEID" => "u.RoleId",
                "ISACTIVE" => "u.IsActive",
                "CREATEDAT" => "u.CreatedAt",
                "CREATEDBY" => "u.CreatedBy",
                "LASTMODIFIEDAT" => "u.LastModifiedAt",
                "LASTMODIFIEDBY" => "u.LastModifiedBy",
                _ => "u.UserId"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT u.*, c.Id AS CompanyId, c.Name AS Company
                        FROM UserRoles u
                        LEFT JOIN Companies c
                        ON u.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM UserRoles
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<UserRoleReadDto>
                {
                    Items = new List<UserRoleReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new UserRoleReadDto
                {
                    Id = row.Field<int>("Id"),
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
                    RoleId = row.Table.Columns.Contains("RoleId") ? row.Field<int>("RoleId") : 0,
                    AssignedBy = row.Table.Columns.Contains("AssignedBy") ? row.Field<string>("AssignedBy") : string.Empty,
                    AssignedAt = row.Table.Columns.Contains("AssignedAt") ? row.Field<DateTime>("AssignedAt") : DateTime.Now,
                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<UserRoleReadDto>
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

    public async Task<UserRoleReadDto> GetByUserIdAsync(string code)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT u.*, c.Id AS CompanyId, c.Name AS Company
                    FROM UserRoles u
                    LEFT JOIN Companies c
                    ON u.CompanyId = c.Id
                WHERE u.UserId = {code}
                  AND u.CompanyId = {CompanyId}
                  AND u.IsActive = True
                  AND u.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("UserRole not found", 404);

            DataRow row = dt.Rows[0];

            return new UserRoleReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                RoleId = row.Field<int>("RoleId"),
                AssignedAt = row.Field<DateTime>("AssignedAt"),
                AssignedBy = row.Field<string>("AssignedBy"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<UserRoleReadDto> GetByRoleIdAsync(string dUserId)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT u.*, c.Id AS CompanyId, c.Name AS Company
                        FROM UserRoles u
                        LEFT JOIN Companies c
                        ON u.CompanyId = c.Id
                WHERE u.UserId = {dUserId}
                  AND u.CompanyId = {CompanyId}
                  AND u.IsActive = True
                  AND u.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("UserRole not found", 404);

            DataRow row = dt.Rows[0];

            return new UserRoleReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                RoleId = row.Field<int>("RoleId"),
                AssignedAt = row.Field<DateTime>("AssignedAt"),
                AssignedBy = row.Field<string>("AssignedBy"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<UserRoleReadDto> UpdateAsync(UserRoleUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.UserId < 0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (UserId is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM UserRoles
            WHERE UserId = {input.UserId}
              AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("UserRole not found", 404);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE UserRoles
            SET 
                UserId = '{input.UserId}',
                RoleId = '{input.RoleId}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE UserId = {input.UserId} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT u.*, c.Id AS CompanyId, c.Name AS Company
            FROM UserRoles u
            LEFT JOIN Companies c
            ON u.CompanyId = c.Id
            WHERE u.UserId = {input.UserId} AND u.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new UserRoleReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                RoleId = row.Field<int>("RoleId"),
                AssignedAt = row.Field<DateTime>("AssignedAt"),
                AssignedBy = row.Field<string>("AssignedBy"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch
        {
            throw;
        }
    }

}