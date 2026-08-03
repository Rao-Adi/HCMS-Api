﻿using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
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
    private readonly NotificationComponent _notificationComponent;
    public TransferWorkflowPolicyComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common,
        NotificationComponent notificationComponent
        )
    {
        _http = http;
        //_logger = logger;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _notificationComponent = notificationComponent;
        _dapperService = dapper;
        _common = common; 
    }


    public async Task<TransferWorkflowPolicyReadDto> CreateAsync(TransferWorkflowPolicyCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("TransferWorkflowPolicy is required.", 400);

            // UC-18 Business Rule: A specific Division can only have one active transfer approval policy defined here.
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferWorkflowPolicies
            WHERE DivisionCode = '{input.DivisionCode?.Replace("'", "''")}'
              AND CompanyId = {CompanyId}
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
                LastModifiedBy,
                ApproverEmpCode
            )
            VALUES
            (
                '{CompanyId}', 
                '{input.DivisionCode!.Replace("'", "''")}',  
                '{input.ApprovalRoleId}',
                '{input.ApprovalUserId}',
                TRUE,
                FALSE,
                NOW(),
                '{empCode.Replace("'", "''")}',
                NOW(),
                '{empCode.Replace("'", "''")}',
                '{input.ApproverEmpCode!.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"  
                SELECT 
                            t.*, 
                            div_setup.Name AS Division, 
                            c.Name AS Company,
                            head.FullName AS DivisionHeadName,
                            head.Designation AS DivisionHeadDesignation
                        FROM TransferWorkflowPolicies t
                        -- Join on sdlid because divisioncode contains the ID '12916'
                        LEFT JOIN public.tblsetupsdetail div_setup 
                            ON t.DivisionCode::int = div_setup.sdlid 
                            AND div_setup.smsid = 70
                        LEFT JOIN Companies c 
                            ON t.CompanyId = c.Id
                        -- Resolve the actual configured Approval Authority (t.ApprovalUserId),
                        -- not the most senior person in the division -- those are not the same person.
                        LEFT JOIN LATERAL (
                            SELECT
                                REGEXP_REPLACE(TRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, '')), '\s+', ' ', 'g') AS FullName,
                                dsg.name AS Designation
                            FROM public.tblemployee e
                            LEFT JOIN public.tblsetupsdetail dsg ON e.dsgid = dsg.sdlid
                            WHERE LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(t.ApprovalUserId::text), '0')
                            LIMIT 1
                        ) head ON TRUE
                WHERE t.Id = {newId} AND t.CompanyId = {CompanyId}";

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
                
                DivisionHeadName = row.Field<string>("DivisionHeadName"),
                DivisionHeadDesignation = row.Field<string>("DivisionHeadDesignation"),
                ApprovalRoleId = row.Field<string>("ApprovalRoleId"),
                ApprovalUserId = row.Field<string>("ApprovalUserId"),
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
                FROM TransferWorkflowPolicies
                WHERE DivisionCode = '{code?.Replace("'", "''")}'
                  AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE TransferWorkflowPolicies
                SET IsDeleted = True,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE DivisionCode = '{code?.Replace("'", "''")}' AND CompanyId = {CompanyId}";

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
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE t.IsDeleted = False AND t.CompanyId = " + CompanyId + @"
                  AND t.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(t.DivisionCode) LIKE '%{search}%'
                    OR UPPER(div_setup.Name) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "div_setup.Name",
                "DESCRIPTION" => "t.ApprovalRoleId",
                "ISACTIVE" => "t.IsActive",
                "CREATEDAT" => "t.CreatedAt",
                "CREATEDBY" => "t.CreatedBy",
                "LASTMODIFIEDAT" => "t.LastModifiedAt",
                "LASTMODIFIEDBY" => "t.LastModifiedBy",
                _ => "t.DivisionCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT 
                            t.*, 
                            div_setup.Name AS Division, 
                            c.Name AS Company,
                            head.FullName AS DivisionHeadName,
                            head.Designation AS DivisionHeadDesignation
                        FROM TransferWorkflowPolicies t
                        -- Join on sdlid because divisioncode contains the ID '12916'
                        LEFT JOIN public.tblsetupsdetail div_setup 
                            ON t.DivisionCode::int = div_setup.sdlid 
                            AND div_setup.smsid = 70
                        LEFT JOIN Companies c 
                            ON t.CompanyId = c.Id
                        -- Resolve the actual configured Approval Authority (t.ApprovalUserId),
                        -- not the most senior person in the division -- those are not the same person.
                        LEFT JOIN LATERAL (
                            SELECT
                                REGEXP_REPLACE(TRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, '')), '\s+', ' ', 'g') AS FullName,
                                dsg.name AS Designation
                            FROM public.tblemployee e
                            LEFT JOIN public.tblsetupsdetail dsg ON e.dsgid = dsg.sdlid
                            WHERE LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(t.ApprovalUserId::text), '0')
                            LIMIT 1
                        ) head ON TRUE
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM TransferWorkflowPolicies t
                        LEFT JOIN public.tblsetupsdetail div_setup 
                            ON t.DivisionCode::int = div_setup.sdlid 
                            AND div_setup.smsid = 70
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
                    DivisionHeadName = row.Table.Columns.Contains("DivisionHeadName") ? row.Field<string>("DivisionHeadName") : string.Empty,
                    DivisionHeadDesignation = row.Table.Columns.Contains("DivisionHeadDesignation") ? row.Field<string>("DivisionHeadDesignation") : string.Empty,

                    ApprovalRoleId = row.Table.Columns.Contains("ApprovalRoleId") ? row.Field<string>("ApprovalRoleId") : string.Empty,
                    ApprovalUserId = row.Table.Columns.Contains("ApprovalUserId") ? row.Field<string>("ApprovalUserId") : string.Empty,

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
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT 
                    t.*, 
                    div_setup.Name AS Division, 
                    c.Name AS Company,
                    head.FullName AS DivisionHeadName,
                    head.Designation AS DivisionHeadDesignation
                FROM TransferWorkflowPolicies t
                LEFT JOIN public.tblsetupsdetail div_setup 
                    ON t.DivisionCode::int = div_setup.sdlid 
                    AND div_setup.smsid = 70
                LEFT JOIN Companies c 
                    ON t.CompanyId = c.Id
                LEFT JOIN LATERAL (
                    SELECT
                        REGEXP_REPLACE(TRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, '')), '\s+', ' ', 'g') AS FullName,
                        dsg.name AS Designation
                    FROM public.tblemployee e
                    LEFT JOIN public.tblsetupsdetail dsg ON e.dsgid = dsg.sdlid
                    WHERE LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(t.ApprovalUserId::text), '0')
                    LIMIT 1
                ) head ON TRUE
                WHERE t.DivisionCode = '{code?.Replace("'", "''")}'
                  AND t.CompanyId = {CompanyId}
                  AND t.IsActive = True
                  AND t.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 404);

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"), 
                DivisionHeadName = row.Table.Columns.Contains("DivisionHeadName") ? row.Field<string>("DivisionHeadName") : string.Empty,
                DivisionHeadDesignation = row.Table.Columns.Contains("DivisionHeadDesignation") ? row.Field<string>("DivisionHeadDesignation") : string.Empty,

                ApprovalRoleId = row.Field<string>("ApprovalRoleId"),
                ApprovalUserId = row.Field<string>("ApprovalUserId"),
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
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <= 0)
                throw new CustomException("Invalid policy Id.", 400);

            // Check existence
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferWorkflowPolicies
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}
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
              AND IsDeleted = FALSE
              AND CompanyId = {CompanyId}";

            int dupExists = Convert.ToInt32(_common.ExecuteScalarQuery(dupQuery));

            if (dupExists > 0)
                throw new CustomException("A transfer policy already exists for this division.", 409);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE TransferWorkflowPolicies
            SET
                DivisionCode = '{input.DivisionCode?.Replace("'", "''")}',
                ApprovalRoleId = '{input.ApprovalRoleId?.Replace("'", "''")}',
                ApprovalUserId = '{input.ApprovalUserId?.Replace("'", "''")}',
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT 
                t.*, 
                div_setup.Name AS Division, 
                c.Name AS Company,
                head.FullName AS DivisionHeadName,
                head.Designation AS DivisionHeadDesignation
            FROM TransferWorkflowPolicies t
            LEFT JOIN public.tblsetupsdetail div_setup 
                ON t.DivisionCode::int = div_setup.sdlid 
                AND div_setup.smsid = 70
            LEFT JOIN Companies c 
                ON t.CompanyId = c.Id
            LEFT JOIN LATERAL (
                SELECT
                    REGEXP_REPLACE(TRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, '')), '\s+', ' ', 'g') AS FullName,
                    dsg.name AS Designation
                FROM public.tblemployee e
                LEFT JOIN public.tblsetupsdetail dsg ON e.dsgid = dsg.sdlid
                WHERE LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(t.ApprovalUserId::text), '0')
                LIMIT 1
            ) head ON TRUE
            WHERE t.Id = {input.Id} AND t.CompanyId = {CompanyId}";

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
                DivisionHeadName = row.Table.Columns.Contains("DivisionHeadName") ? row.Field<string>("DivisionHeadName") : string.Empty,
                DivisionHeadDesignation = row.Table.Columns.Contains("DivisionHeadDesignation") ? row.Field<string>("DivisionHeadDesignation") : string.Empty,

                ApprovalRoleId = row.Field<string>("ApprovalRoleId"),
                ApprovalUserId = row.Field<string>("ApprovalUserId"),
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

    public async Task<PaginationResult<dynamic>> GetMyResponsibilityTransfersApprovalsAsync(GetMyResponsibilityTransfersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());
             

            // Zero-trimmed comparison, not exact equality: @UserId (from GetEmpCodeForHCMS) and
            // rt.ApproverId (stored from tblEmployee.empcode at request-creation time) can arrive
            // differently zero-padded for the same employee, same as every other empcode comparison
            // in this codebase.
            var whereClause = @"
                WHERE rt.IsDeleted = FALSE
                  AND rt.CompanyId = @CompanyId
                  AND rt.Status = @Status
                  AND LTRIM(RTRIM(rt.ApproverId::text), '0') = LTRIM(RTRIM(@UserId::text), '0')";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(rt.Remarks) LIKE '%{search}%'
                    OR UPPER(uf.firstname) LIKE '%{search}%'
                    OR UPPER(ut.firstname) LIKE '%{search}%'
                )";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "ACTIONDATE" => "rt.ActionDate",
                "EFFECTIVEDATEFROM" => "rt.EffectiveDateFrom",
                _ => "rt.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT rt.Id,
                       COALESCE(NULLIF(REGEXP_REPLACE(TRIM(COALESCE(uc.firstname, '') || ' ' || COALESCE(uc.midname, '') || ' ' || COALESCE(uc.lastname, '')), '\s+', ' ', 'g'), ''), rt.CreatedBy) AS CreatedBy,
                       REGEXP_REPLACE(TRIM(COALESCE(uf.firstname, '') || ' ' || COALESCE(uf.midname, '') || ' ' || COALESCE(uf.lastname, '')), '\s+', ' ', 'g') AS employeefromname,
                       REGEXP_REPLACE(TRIM(COALESCE(ut.firstname, '') || ' ' || COALESCE(ut.midname, '') || ' ' || COALESCE(ut.lastname, '')), '\s+', ' ', 'g') AS employeetoname,
                       rt.ReasonForTransfer,
                       rt.EffectiveDateFrom,
                       rt.EffectiveDateTo,
                       rt.Remarks,
                       rt.ActionDate,
                       rt.Status
                FROM ResponsibilityTransfers rt
                LEFT JOIN tblEmployee uf ON LTRIM(RTRIM(rt.EmployeeFrom::text), '0') = LTRIM(RTRIM(uf.empcode::text), '0') AND uf.CompanyId = rt.CompanyId 
                LEFT JOIN tblEmployee ut ON LTRIM(RTRIM(rt.EmployeeTo::text), '0') = LTRIM(RTRIM(ut.empcode::text), '0') AND ut.CompanyId = rt.CompanyId 
                LEFT JOIN tblEmployee uc ON LTRIM(RTRIM(rt.CreatedBy::text), '0') = LTRIM(RTRIM(uc.empcode::text), '0') AND uc.CompanyId = rt.CompanyId 
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1) 
                FROM ResponsibilityTransfers rt
                LEFT JOIN tblEmployee uf ON LTRIM(RTRIM(rt.EmployeeFrom::text), '0') = LTRIM(RTRIM(uf.empcode::text), '0') AND uf.CompanyId = rt.CompanyId 
                LEFT JOIN tblEmployee ut ON LTRIM(RTRIM(rt.EmployeeTo::text), '0') = LTRIM(RTRIM(ut.empcode::text), '0') AND ut.CompanyId = rt.CompanyId 
                {whereClause};";

            var queryParams = new { CompanyId = CompanyId, Status = input.Status, UserId = empCode };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
        }
        catch (Exception)
        {
            throw;
        }
    }

    // Same scope as GetMyResponsibilityTransfersApprovalsAsync (ApproverId = current user, not deleted)
    // but counts every status in one query instead of returning full rows for one status at a time --
    // for showing counts on tab labels without fetching the paginated list per tab.
    public async Task<ResponsibilityTransferApprovalCountsDto> GetMyResponsibilityTransfersApprovalsCountAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Scoped to ApproverId only -- "requests pending MY approval", not requests I submitted
            // myself. A prior version OR'd in CreatedBy = @UserId here, which (on top of an operator
            // precedence bug that bypassed CompanyId/IsDeleted scoping) meant a user's own submitted
            // request counted as "pending my approval" even when they weren't the assigned approver.
            // Submitted-request counts now come from the separate
            // GetMySubmittedResponsibilityTransfersCountAsync below.
            string sql = @"
                SELECT
                    COUNT(1) FILTER (WHERE rt.Status = 1) AS PendingCount,
                    COUNT(1) FILTER (WHERE rt.Status = 2) AS ApprovedCount,
                    COUNT(1) FILTER (WHERE rt.Status = 3) AS RejectedCount,
                    COUNT(1) FILTER (WHERE rt.Status = 4) AS RevertedCount,
                    COUNT(1) AS TotalCount
                FROM ResponsibilityTransfers rt
                WHERE rt.IsDeleted = FALSE
                  AND rt.CompanyId = @CompanyId
                  AND LTRIM(RTRIM(rt.ApproverId::text), '0') = LTRIM(RTRIM(@UserId::text), '0');";

            var queryParams = new { CompanyId = CompanyId, UserId = empCode };

            var result = await _common.QuerySingleAsync<ResponsibilityTransferApprovalCountsDto>(sql, queryParams);
            return result ?? new ResponsibilityTransferApprovalCountsDto();
        }
        catch (Exception)
        {
            throw;
        }
    }

    // Same scope as GetMySubmittedResponsibilityTransfersAsync (CreatedBy = current user) but counts
    // every status in one query -- the "My Submitted Requests" tab's own badge, kept separate from
    // GetMyResponsibilityTransfersApprovalsCountAsync's ApproverId-scoped count above so a user's own
    // submission is never miscounted as something pending for them to approve.
    public async Task<ResponsibilityTransferApprovalCountsDto> GetMySubmittedResponsibilityTransfersCountAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            string sql = @"
                SELECT
                    COUNT(1) FILTER (WHERE rt.Status = 1) AS PendingCount,
                    COUNT(1) FILTER (WHERE rt.Status = 2) AS ApprovedCount,
                    COUNT(1) FILTER (WHERE rt.Status = 3) AS RejectedCount,
                    COUNT(1) FILTER (WHERE rt.Status = 4) AS RevertedCount,
                    COUNT(1) AS TotalCount
                FROM ResponsibilityTransfers rt
                WHERE rt.IsDeleted = FALSE
                  AND rt.CompanyId = @CompanyId
                  AND LTRIM(RTRIM(rt.CreatedBy::text), '0') = LTRIM(RTRIM(@UserId::text), '0');";

            var queryParams = new { CompanyId = CompanyId, UserId = empCode };

            var result = await _common.QuerySingleAsync<ResponsibilityTransferApprovalCountsDto>(sql, queryParams);
            return result ?? new ResponsibilityTransferApprovalCountsDto();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<PaginationResult<dynamic>> GetMySubmittedResponsibilityTransfersAsync(GetMyResponsibilityTransfersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string empCode = string.IsNullOrWhiteSpace(input.UserId) 
                ? _utilities.GetEmpCodeForHCMS(_utilities.GetEmpid(_clientContextService.GetClientIP()).ToString()) 
                : input.UserId;

            var whereClause = @"
                WHERE rt.IsDeleted = FALSE 
                  AND rt.CompanyId = @CompanyId
                  AND rt.CreatedBy = @UserId
                  AND rt.Status = @Status";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(rt.Remarks) LIKE '%{search}%'
                    OR UPPER(uf.firstname) LIKE '%{search}%'
                    OR UPPER(ut.firstname) LIKE '%{search}%'
                )";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "ACTIONDATE" => "rt.ActionDate",
                "EFFECTIVEDATEFROM" => "rt.EffectiveDateFrom",
                _ => "rt.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT rt.Id,
                       COALESCE(NULLIF(REGEXP_REPLACE(TRIM(COALESCE(uc.firstname, '') || ' ' || COALESCE(uc.midname, '') || ' ' || COALESCE(uc.lastname, '')), '\s+', ' ', 'g'), ''), rt.CreatedBy) AS CreatedBy,
                       REGEXP_REPLACE(TRIM(COALESCE(uf.firstname, '') || ' ' || COALESCE(uf.midname, '') || ' ' || COALESCE(uf.lastname, '')), '\s+', ' ', 'g') AS employeefromname,
                       REGEXP_REPLACE(TRIM(COALESCE(ut.firstname, '') || ' ' || COALESCE(ut.midname, '') || ' ' || COALESCE(ut.lastname, '')), '\s+', ' ', 'g') AS employeetoname,
                       rt.ReasonForTransfer,
                       rt.EffectiveDateFrom,
                       rt.EffectiveDateTo,
                       rt.Remarks,
                       rt.ActionDate,
                       rt.Status
                FROM ResponsibilityTransfers rt
                LEFT JOIN tblEmployee uf ON LTRIM(RTRIM(rt.EmployeeFrom::text), '0') = LTRIM(RTRIM(uf.empcode::text), '0') AND uf.CompanyId = rt.CompanyId 
                LEFT JOIN tblEmployee ut ON LTRIM(RTRIM(rt.EmployeeTo::text), '0') = LTRIM(RTRIM(ut.empcode::text), '0') AND ut.CompanyId = rt.CompanyId 
                LEFT JOIN tblEmployee uc ON LTRIM(RTRIM(rt.CreatedBy::text), '0') = LTRIM(RTRIM(uc.empcode::text), '0') AND uc.CompanyId = rt.CompanyId 
                
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1) 
                FROM ResponsibilityTransfers rt
                LEFT JOIN tblEmployee uf ON LTRIM(RTRIM(rt.EmployeeFrom::text), '0') = LTRIM(RTRIM(uf.empcode::text), '0') AND uf.CompanyId = rt.CompanyId  
                LEFT JOIN tblEmployee ut ON LTRIM(RTRIM(rt.EmployeeTo::text), '0') = LTRIM(RTRIM(ut.empcode::text), '0') AND ut.CompanyId = rt.CompanyId 
                {whereClause};";

            var queryParams = new { CompanyId = CompanyId, Status = input.Status, UserId = empCode };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> TakeActionAsync(ResponsibilityTransferActionDto input)
    {
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        var clientIp = _clientContextService.GetClientIP();
        int CompanyId = int.Parse(_CompanyId);
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(input.Observation))
                throw new CustomException("Observation is required to submit action.", 400);

            int newStatus = input.Action.ToUpper() switch
            {
                "APPROVE" => 2,
                "REJECT" => 3,
                "REVERT" => 4,
                _ => throw new CustomException("Invalid action specified.", 400)
            };

            var transfer = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT EmployeeFrom, EmployeeTo, Status, EffectiveDateFrom, EffectiveDateTo FROM ResponsibilityTransfers WHERE Id = @Id AND CompanyId = @CompanyId FOR UPDATE;", 
                new { Id = input.TransferId, CompanyId = CompanyId }, tx);

            if (transfer == null) throw new CustomException("Transfer request not found.", 404);
            if (transfer.status != 1) throw new CustomException("This request has already been processed.", 400);

            await _common.ExecuteAsync(@"
                UPDATE ResponsibilityTransfers
                SET Status = @Status,
                    Observation = @Observation,
                    ActionDate = NOW(),
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @empCode
                WHERE Id = @Id AND CompanyId = @CompanyId;", 
                new { Status = newStatus, input.Observation, empCode, Id = input.TransferId, CompanyId = CompanyId }, tx);

            // UC-17: Workflow Transfer Logic
            if (newStatus == 2)
            {
                await _common.ExecuteAsync(@"
                    UPDATE WorkflowExecutionSteps
                    SET AssignedUserId = @EmpToCode
                    WHERE AssignedUserId = @EmpFromCode
                    AND Decision IS NULL
                    AND CompanyId = @CompanyId;",
                    new { EmpToCode = transfer.employeeto, EmpFromCode = transfer.employeefrom, CompanyId = CompanyId }, tx);

                // Send notifications to both parties
                var placeholders = new Dictionary<string, string> {
                    { "Emp From", transfer.employeefrom },
                    { "Emp To", transfer.employeeto },
                    { "Date From", transfer.effectivedatefrom?.ToString("yyyy-MM-dd") ?? "Now" },
                    { "Date To", transfer.effectivedateto != null ? transfer.effectivedateto.ToString("yyyy-MM-dd") : "Permanent" }
                };
                 
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.TransferRequestApproval, CompanyId, input.TransferId, transfer.employeefrom, placeholders);
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.TransferRequestApproval, CompanyId, input.TransferId, transfer.employeeto, placeholders);
            }

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}

public class GetMyResponsibilityTransfersDto : TableFiltersDto
{
    public int Status { get; set; }
    public string? UserId { get; set; }
}

public class ResponsibilityTransferApprovalCountsDto
{
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int RevertedCount { get; set; }
    public int TotalCount { get; set; }
}
