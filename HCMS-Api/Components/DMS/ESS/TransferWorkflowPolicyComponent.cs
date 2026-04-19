﻿using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class TransferWorkflowPolicyComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public TransferWorkflowPolicyComponent(
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
        string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        _dataservice.BeginProcess(connectionString);

    }


    public async Task<TransferWorkflowPolicyReadDto> CreateAsync(TransferWorkflowPolicyCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            if (input.Id < 0)
                throw new CustomException("TransferWorkflowPolicy is required.", 400);

            // UC-18 Business Rule: A specific Division can only have one active transfer approval policy defined here.
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferWorkflowPolicies
            WHERE DivisionCode = '{input.DivisionCode?.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("A transfer policy already exists for this division.", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO TransferWorkflowPolicies
            (   CompanyId,
                DivisionCode, 
                ApprovalRoleId,
                ApprovalUserId, 
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{CompanyId}', 
                '{input.DivisionCode!.Replace("'", "''")}',  
                {input.ApprovalRoleId},
                {input.ApprovalUserId},
                TRUE,
                FALSE,
                NOW(),
                '{userId.Replace("'", "''")}',
                NOW(),
                '{userId.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"  
            SELECT  t.*, div.Name AS Division, c.Name AS Company
                        FROM TransferWorkflowPolicies t
                        LEFT JOIN Divisions div
                        ON t.DivisionCode = div.Code 
                        LEFT JOIN Companies c
                        ON t.CompanyId = c.Id
            WHERE t.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),
                 
                ApprovalRoleId = row.Field<int?>("ApprovalRoleId") ?? 0,
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
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM TransferWorkflowPolicies
                WHERE DivisionCode = '{code?.Replace("'", "''")}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE TransferWorkflowPolicies
                SET IsDeleted = TRUE
                WHERE DivisionCode = '{code?.Replace("'", "''")}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<TransferWorkflowPolicyReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE t.IsDeleted = False 
                  AND t.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(t.DivisionCode) LIKE '%{search}%'
                    OR UPPER(t.DivisionCode) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "t.DivisionCode",
                "DESCRIPTION" => "t.ApprovalRoleId",
                "ISACTIVE" => "t.IsActive",
                _ => "t.DivisionCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT  t.*, div.Name AS Division, c.Name AS Company
                        FROM TransferWorkflowPolicies t
                        LEFT JOIN Divisions div
                        ON t.DivisionCode = div.Code 
                        LEFT JOIN Companies c
                        ON t.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM TransferWorkflowPolicies t
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<TransferWorkflowPolicyReadDto>
                {
                    Items = new List<TransferWorkflowPolicyReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new TransferWorkflowPolicyReadDto
                {
                    Id = row.Field<int>("Id"),
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"), 

                    ApprovalRoleId = row.Table.Columns.Contains("ApprovalRoleId") ? (row.Field<int?>("ApprovalRoleId") ?? 0) : 0,
                    ApprovalUserId = row.Table.Columns.Contains("ApprovalUserId") ? (row.Field<int?>("ApprovalUserId") ?? 0) : 0,
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

            return new PaginationResult<TransferWorkflowPolicyReadDto>
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

    public async Task<TransferWorkflowPolicyReadDto> GetByTransferWorkflowPolicyCodeAsync(string code)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT  t.*, div.Name AS Division, c.Name AS Company
                        FROM TransferWorkflowPolicies t
                        LEFT JOIN Divisions div
                        ON t.DivisionCode = div.Code 
                        LEFT JOIN Companies c
                        ON t.CompanyId = c.Id
                WHERE t.DivisionCode = '{code?.Replace("'", "''")}'
                  AND t.CompanyId = {CompanyId}
                  AND t.IsActive = True
                  AND t.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 200);

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"), 

                ApprovalRoleId = row.Field<int?>("ApprovalRoleId") ?? 0,
                ApprovalUserId = row.Field<int?>("ApprovalUserId") ?? 0,
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

    public async Task<TransferWorkflowPolicyReadDto> UpdateAsync(TransferWorkflowPolicyUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            if (input.Id <= 0)
                throw new CustomException("Invalid policy Id.", 400);

            // Check existence
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferWorkflowPolicies
            WHERE Id = {input.Id}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 404);

            // Prevent assigning a DivisionCode that is already linked to another active policy
            string dupQuery = $@"
            SELECT COUNT(1)
            FROM TransferWorkflowPolicies
            WHERE DivisionCode = '{input.DivisionCode?.Replace("'", "''")}'
              AND Id != {input.Id}
              AND IsDeleted = FALSE";

            int dupExists = Convert.ToInt32(_common.ExecuteScalarQuery(dupQuery));

            if (dupExists > 0)
                throw new CustomException("A transfer policy already exists for this division.", 409);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE TransferWorkflowPolicies
            SET 
                DivisionCode = '{input.DivisionCode?.Replace("'", "''")}', 
                ApprovalRoleId = {input.ApprovalRoleId},
                ApprovalUserId = {input.ApprovalUserId},
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT  t.*, div.Name AS Division, c.Name AS Company
                        FROM TransferWorkflowPolicies t
                        LEFT JOIN Divisions div
                        ON t.DivisionCode = div.Code 
                        LEFT JOIN Companies c
                        ON t.CompanyId = c.Id
            WHERE t.Id = {input.Id}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                ApprovalRoleId = row.Field<int?>("ApprovalRoleId") ?? 0,
                ApprovalUserId = row.Field<int?>("ApprovalUserId") ?? 0,
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
