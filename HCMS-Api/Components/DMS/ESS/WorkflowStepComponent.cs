using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.HCMS.ESS;
using StackExchange.Redis;
using System.ComponentModel.Design;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Transactions;
using static HCMS_Api.Controllers.HCMS.Common.SecurityController;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace HCMS_Api.Components.DMS.ESS;

public class WorkflowStepComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public WorkflowStepComponent(
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


    //public async Task<WorkflowStepReadDto> CreateAsync(WorkflowStepCreateDto input)
    //{
    //    try
    //    {
    //        //var clientIp = _clientContextService.GetClientIP();
    //        //var prefix = _utilities.GetPrefix(clientIp);
    //        var userId = "manual"; //_utilities.GetUserid(prefix);
    //        if (input.Id < 0)
    //            throw new CustomException("WorkflowStepDefinitions ID is required.", 400);

    //        // Check duplicate by Id OR Name
    //        string checkQuery = $@"
    //        SELECT COUNT(1)
    //        FROM WorkflowStepDefinitions
    //        WHERE (Id = '{input.Id}' 
    //          AND IsDeleted = FALSE";

    //        int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

    //        if (exists > 0)
    //            throw new CustomException("WorkflowStepDefinitions already exists", 409);

    //        // Insert (PostgreSQL syntax)
    //        string insertQuery = $@"
    //        INSERT INTO WorkflowStepDefinitions
    //        (   CompanyId,
    //            WorkflowPolicyId, 
    //            Sequence,
    //            RoleId,
    //            UserId,
    //            ApprovalLevel, 
    //            IsActive,
    //            IsDeleted,
    //            CreatedAt,
    //            CreatedBy,
    //            LastModifiedAt,
    //            LastModifiedBy
    //        )
    //        VALUES
    //        (
    //            '{input.CompanyId}', 
    //            '{input.WorkflowPolicyId}', 
    //            '{input.Sequence}', 
    //            '{input.RoleId}', 
    //            '{input.UserId}', 
    //            '{input.ApprovalLevel}', 
    //            TRUE,
    //            FALSE,
    //            NOW(),
    //            '{userId.Replace("'", "''")}',
    //            NOW(),
    //            '{userId.Replace("'", "''")}'
    //        )
    //        RETURNING Id;";

    //        int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

    //        // Fetch inserted record
    //        string selectQuery = $@"
    //                  SELECT ws.*,wfp.Name AS WorkflowPolicyName, u.EmployeeCode, u.EmployeeName,
    //                  des.Name as Designation, dt.Name AS DocumentType, r.Name AS UserRole
    //                  FROM WorkflowStepDefinitions ws
    //                       LEFT JOIN WorkflowPolicies wfp
    //                       ON ws.WorkflowPolicyId = wfp.Id
    //                 LEFT JOIN DocumentTypes dt
    //                 ON ws.DocumentTypeCode = dt.Code
    //                       LEFT JOIN Users u
    //                       ON ws.UserId = u.Id  
    //                         LEFT JOIN UserRoles ur
    //                         ON u.Id = ur.UserId
    //                             LEFT JOIN Roles r
    //                             ON ur.RoleId = r.Id
    //                             LEFT JOIN Designations des ON u.DesignationCode = des.Code 
    //                        AND ws.IsActive = True
    //                        AND ws.IsDeleted = False
    //        WHERE ws.Id = {newId}";

    //        DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

    //        if (dt == null || dt.Rows.Count == 0)
    //            throw new Exception("Failed to fetch created division");

    //        DataRow row = dt.Rows[0];

    //        return new WorkflowStepReadDto
    //        {
    //            Id = row.Field<int>("Id"),
    //            CompanyId = row.Field<int>("CompanyId"),
    //            Company = row.Field<string>("Company"),
    //            WorkflowPolicyId = row.Field<int>("WorkflowPolicyId"),
    //            Sequence = row.Field<int>("StepOrder"),
    //            RoleId = row.Field<int>("RoleId"),
    //            UserId = row.Field<int>("UserId"),
    //            ApprovalLevel = row.Field<int>("ApprovalLevel"),

    //            CanEdit = row.Table.Columns.Contains("CanEdit") && row.Field<bool?>("CanEdit") == true,
    //            IsParallelApproval = row.Table.Columns.Contains("IsParallelApproval") && row.Field<bool?>("IsParallelApproval") == true,
    //            RequireCrossFunctionalHead = row.Table.Columns.Contains("RequireCrossFunctionalHead") && row.Field<bool?>("RequireCrossFunctionalHead") == true,

    //            IsDeleted = row.Field<bool>("IsDeleted"),
    //            IsActive = row.Field<bool>("IsActive"),
    //            CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
    //            CreatedBy = row.Field<string>("CreatedBy"),
    //            LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
    //            LastModifiedBy = row.Field<string>("LastModifiedBy")
    //        };
    //    }
    //    catch
    //    {
    //        throw;
    //    }
    //}


    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM WorkflowStepDefinitions
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("WorkflowStepDefinitions not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE WorkflowStepDefinitions
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<WorkflowStepReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE ws.IsDeleted = False 
                  AND ws.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(ws.Name) LIKE '%{search}%'
                    OR UPPER(ws.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "ID" => "ws.ID",
                "ISACTIVE" => "ws.IsActive",
                _ => "ws.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        Vw_WorkflowSteps ws
 
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM WorkflowStepDefinitions ws
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<WorkflowStepReadDto>
                {
                    Items = new List<WorkflowStepReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new WorkflowStepReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    WorkflowPolicyVersionId = row.Table.Columns.Contains("WorkflowPolicyId") ? row.Field<int>("WorkflowPolicyId") : 0,
                    StepOrder = row.Table.Columns.Contains("StepOrder") ? row.Field<int>("StepOrder") : 0,
                    RoleId = row.Field<int?>("RoleId") ?? 0,
                    UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
                    ApprovalLevel = row.Table.Columns.Contains("ApprovalLevel") ? row.Field<int>("ApprovalLevel") : 0,

                    EmployeeCode = row.Field<string>("EmployeeCode"),
                    EmployeeName = row.Field<string>("EmployeeName"),

                    Designation = row.Field<string>("Designation"),
                    UserRole = row.Field<string>("UserRole"),


                    CanEdit = row.Table.Columns.Contains("CanEdit") && row.Field<bool?>("CanEdit") == true,
                    IsParallelApproval = row.Table.Columns.Contains("IsParallelApproval") && row.Field<bool?>("IsParallelApproval") == true,
                    RequireCrossFunctionalHead = row.Table.Columns.Contains("RequireCrossFunctionalHead") && row.Field<bool?>("RequireCrossFunctionalHead") == true,


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

            return new PaginationResult<WorkflowStepReadDto>
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

    public async Task<List<WorkflowStepReadDto>> GetByDocumentTypeCodeAsync(GetStepDefinitionFilterDto input)
    {
        try
        {
            string query = $@"
                SELECT ws.*
                FROM Vw_WorkflowStepDefinitions ws
                JOIN WorkflowPolicies wp
                    ON wp.Id = ws.WorkflowPolicyId
                    AND wp.CompanyId = ws.CompanyId
                WHERE wp.CompanyId = @CompanyId
                AND wp.EntityType = @EntityType
                AND wp.DocumentTypeCode = @DocumentTypeCode
                -- Allow filtering by specific policy name or ID
                AND (@DivisionCode = '' OR wp.DivisionCode = @DivisionCode OR (wp.DivisionCode IS NULL AND @DivisionCode IS NULL))
                AND (@DepartmentCode = '' OR wp.DepartmentCode = @DepartmentCode OR (wp.DepartmentCode IS NULL AND @DepartmentCode IS NULL))
                AND (@SubDepartmentCode = '' OR wp.SubDepartmentCode = @SubDepartmentCode OR (wp.SubDepartmentCode IS NULL AND @SubDepartmentCode IS NULL))
                AND (@BusinessDomainCode = '' OR wp.BusinessDomainCode = @BusinessDomainCode OR (wp.BusinessDomainCode IS NULL AND @BusinessDomainCode IS NULL))
                AND ws.IsActive = TRUE
                AND ws.IsDeleted = FALSE
                ORDER BY ws.StepOrder;";

            var results = await _common.QueryAsync<dynamic>(query, input);

            var dtos = new List<WorkflowStepReadDto>();
            foreach (var row in results)
            {
                // Cast row to IDictionary<string, object> to access properties safely
                var rowDict = row as IDictionary<string, object>;

                dtos.Add(new WorkflowStepReadDto
                {
                    Id = GetValue<int>(rowDict, "companyid"),

                    CompanyId = GetValue<int>(rowDict, "companyid"),
                    Company = GetValue<string>(rowDict, "company"),

                    WorkflowPolicyVersionId = GetValue<int>(rowDict, "workflowpolicyversionid"),
                    StepOrder = GetValue<int>(rowDict, "steporder"),
                    RoleId = GetValue<int>(rowDict, "roleid"),
                    UserId = GetValue<int>(rowDict, "userid"),
                    ApprovalLevel = GetValue<int>(rowDict, "approvallevel"),

                    EmployeeCode = GetValue<string>(rowDict, "employeecode"),
                    EmployeeName = GetValue<string>(rowDict, "employeename"),

                    Designation = GetValue<string>(rowDict, "designation"),
                    UserRole = GetValue<string>(rowDict, "userrole"),


                    CanEdit = GetValue<bool>(rowDict, "canedit"),
                    IsParallelApproval = GetValue<bool>(rowDict, "isparallelapproval"),
                    RequireCrossFunctionalHead = GetValue<bool>(rowDict, "requirecrossfunctionalhead"),

                    IsActive = GetValue<bool>(rowDict, "isactive"),
                    IsDeleted = GetValue<bool>(rowDict, "isdeleted"),
                    CreatedAt = GetValue<string>(rowDict, "createdat"),
                    CreatedBy = GetValue<string>(rowDict, "createdby"),
                    LastModifiedAt = GetValue<string>(rowDict, "lastmodifiedat"),
                    LastModifiedBy = GetValue<string>(rowDict, "lastmodifiedby"),

                });
            }

            return dtos;

            //DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            //DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            //                                          // ✅ SAFETY CHECKS
            //if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            //{

            //}

            //var divisions = divisionsTable.AsEnumerable()
            //    .Select(row => new WorkflowStepReadDto
            //    {
            //        Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

            //        CompanyId = row.Field<int>("CompanyId"),
            //        Company = row.Field<string>("Company"),

            //        WorkflowPolicyVersionId = row.Table.Columns.Contains("WorkflowPolicyId") ? row.Field<int>("WorkflowPolicyId") : 0,
            //        Sequence = row.Table.Columns.Contains("StepOrder") ? row.Field<int>("StepOrder") : 0,
            //        RoleId = row.Field<int?>("RoleId") ?? 0,
            //        UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
            //        ApprovalLevel = row.Table.Columns.Contains("ApprovalLevel") ? row.Field<int>("ApprovalLevel") : 0,

            //        EmployeeCode = row.Field<string>("EmployeeCode"),
            //        EmployeeName = row.Field<string>("EmployeeName"),

            //        Designation = row.Field<string>("Designation"),
            //        UserRole = row.Field<string>("UserRole"),


            //        CanEdit = row.Table.Columns.Contains("CanEdit") && row.Field<bool?>("CanEdit") == true,
            //        IsParallelApproval = row.Table.Columns.Contains("IsParallelApproval") && row.Field<bool?>("IsParallelApproval") == true,
            //        RequireCrossFunctionalHead = row.Table.Columns.Contains("RequireCrossFunctionalHead") && row.Field<bool?>("RequireCrossFunctionalHead") == true,

            //        IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
            //        IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
            //        CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
            //                    ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
            //        CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
            //        LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
            //                         ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
            //        LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
            //    })
            //    .ToList();

            //return divisions;
        }
        catch (Exception)
        {
            throw;
        }
    }

    // Helper method to safely get values from the dynamic row
    private static T GetValue<T>(IDictionary<string, object> row, string columnName)
    {
        if (row.ContainsKey(columnName) && row[columnName] != DBNull.Value)
        {
            try
            {
                var value = row[columnName];

                // Handle type conversions
                if (typeof(T) == typeof(int?) || typeof(T) == typeof(int))
                {
                    if (value is int intValue)
                        return (T)(object)intValue;
                    if (value is long longValue)
                        return (T)(object)(int)longValue;
                    if (value is decimal decimalValue)
                        return (T)(object)(int)decimalValue;
                }

                if (typeof(T) == typeof(string) && value != null)
                    return (T)(object)value.ToString();

                return (T)value;
            }
            catch
            {
                return default(T);
            }
        }
        return default(T);
    }

    public async Task<IEnumerable<PendingRequestDto>> GetPendingApprovalsAsync(
    long companyId,
    long userId)
    {


        var sql = @"
            SELECT
                dr.Id AS RequestId,
                dr.RequestNumber,
                dr.DocumentName,
                dr.DocumentRequestTypeCode AS RequestType,
                dr.CreatedBy AS SubmittedBy,
                dr.CreatedAt,
                we.Id AS WorkflowExecutionId,
                wes.Id AS StepId
            FROM WorkflowExecutionSteps wes

            INNER JOIN WorkflowExecutions we
                ON we.Id = wes.WorkflowExecutionId
                AND we.CompanyId = wes.CompanyId

            INNER JOIN DocumentRequests dr
                ON dr.Id = we.EntityId
                AND we.EntityType = 'Request'

            WHERE wes.CompanyId = @CompanyId
            AND we.Status = 'Running'
            AND wes.Decision IS NULL

            AND (
                wes.AssignedUserId = @UserId

                OR

                wes.AssignedRoleId IN
                (
                    SELECT RoleId
                    FROM UserRoles
                    WHERE CompanyId = @CompanyId
                    AND UserId = @UserId
                    AND IsActive = TRUE
                )
            )

            ORDER BY dr.CreatedAt;
            ";

        return await _common.QueryAsync<PendingRequestDto>(
            sql,
            new { CompanyId = companyId, UserId = userId }
        );


    }

    public async Task<List<WorkflowStepReadDto>> GetObservationBy(string code, int policyId)
    {
        try
        {
            string query = $@"
                  Select * from Vw_WorkflowStepDefinitions ws 
                WHERE ws.DocumentTypeCode = '{code}' AND ws.WorkflowPolicyId = {policyId}
                  AND ws.IsActive = True
                  AND ws.IsDeleted = False";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {

            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new WorkflowStepReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    WorkflowPolicyVersionId = row.Table.Columns.Contains("WorkflowPolicyId") ? row.Field<int>("WorkflowPolicyId") : 0,
                    StepOrder = row.Table.Columns.Contains("StepOrder") ? row.Field<int>("StepOrder") : 0,
                    RoleId = row.Field<int?>("RoleId") ?? 0,
                    UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
                    ApprovalLevel = row.Table.Columns.Contains("ApprovalLevel") ? row.Field<int>("ApprovalLevel") : 0,

                    EmployeeCode = row.Field<string>("EmployeeCode"),
                    EmployeeName = row.Field<string>("EmployeeName"),

                    Designation = row.Field<string>("Designation"),
                    UserRole = row.Field<string>("UserRole"),


                    CanEdit = row.Table.Columns.Contains("CanEdit") && row.Field<bool?>("CanEdit") == true,
                    IsParallelApproval = row.Table.Columns.Contains("IsParallelApproval") && row.Field<bool?>("IsParallelApproval") == true,
                    RequireCrossFunctionalHead = row.Table.Columns.Contains("RequireCrossFunctionalHead") && row.Field<bool?>("RequireCrossFunctionalHead") == true,

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

            return divisions;
        }
        catch (Exception)
        {
            throw;
        }
    }



    public async Task<WorkflowStepReadDto> UpdateAsync(WorkflowStepUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM WorkflowStepDefinitions
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("WorkflowStepDefinitions not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE WorkflowStepDefinitions
            SET 
                WorkflowPolicyId = '{input.WorkflowPolicyId}', 
                Sequence = '{input.Sequence}',
                RoleId = '{input.RoleId}',
                UserId = '{input.UserId}',
                ApprovalLevel = '{input.ApprovalLevel}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@" 
            SELECT ws.*,wfp.Name AS WorkflowPolicyName, u.EmployeeCode, u.EmployeeName,
                  des.Name as Designation, dt.Name AS DocumentType, r.Name AS UserRole,c.Name AS Company
                  FROM WorkflowStepDefinitions ws
                       LEFT JOIN WorkflowPolicies wfp
                       ON ws.WorkflowPolicyId = wfp.Id
		               LEFT JOIN DocumentTypes dt
		               ON ws.DocumentTypeCode = dt.Code
		               LEFT JOIN Companies c
		               ON ws.CompanyId = c.id
                       LEFT JOIN Users u
                       ON ws.UserId = u.Id  
                         LEFT JOIN UserRoles ur
                         ON u.Id = ur.UserId
                             LEFT JOIN Roles r
                             ON ur.RoleId = r.Id
                             LEFT JOIN Designations des ON u.DesignationCode = des.Code 
            WHERE ws.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new WorkflowStepReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                WorkflowPolicyVersionId = row.Table.Columns.Contains("WorkflowPolicyId") ? row.Field<int>("WorkflowPolicyId") : 0,
                StepOrder = row.Table.Columns.Contains("StepOrder") ? row.Field<int>("StepOrder") : 0,
                RoleId = row.Field<int?>("RoleId") ?? 0,
                UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
                ApprovalLevel = row.Table.Columns.Contains("ApprovalLevel") ? row.Field<int>("ApprovalLevel") : 0,

                EmployeeCode = row.Field<string>("EmployeeCode"),
                EmployeeName = row.Field<string>("EmployeeName"),

                Designation = row.Field<string>("Designation"),
                UserRole = row.Field<string>("UserRole"),


                CanEdit = row.Table.Columns.Contains("CanEdit") && row.Field<bool?>("CanEdit") == true,
                IsParallelApproval = row.Table.Columns.Contains("IsParallelApproval") && row.Field<bool?>("IsParallelApproval") == true,
                RequireCrossFunctionalHead = row.Table.Columns.Contains("RequireCrossFunctionalHead") && row.Field<bool?>("RequireCrossFunctionalHead") == true,


                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
            };
        }
        catch
        {
            throw;
        }
    }

    //public async Task<List<WorkflowStepReadDto>> CreateWorkflowStepsByFilterAsync(WorkFlowStepsFilterDto filters)
    //{
    //    try
    //    {
    //        //-----------------------------------------
    //        // 1️⃣ Fetch User
    //        //-----------------------------------------
    //        var user = await GetUsersByFiltersAsync(filters);

    //        //-----------------------------------------
    //        // 2️⃣ Resolve WorkflowPolicyId dynamically
    //        //    (TAB + DocType + Cabinet Scope)
    //        //-----------------------------------------

    //        var policyId = await _dapperService.ExecuteScalarAsync<int?>(@"
    //            SELECT Id
    //            FROM WorkflowPolicies
    //            WHERE CompanyId = @CompanyId
    //            AND EntityType = @EntityType
    //            AND DocumentTypeCode = @DocumentTypeCode
    //            AND COALESCE(DivisionCode,'') = COALESCE(@DivisionCode,'')
    //            AND COALESCE(DepartmentCode,'') = COALESCE(@DepartmentCode,'')
    //            AND COALESCE(SubDepartmentCode,'') = COALESCE(@SubDepartmentCode,'')
    //            AND COALESCE(BusinessDomainCode,'') = COALESCE(@BusinessDomainCode,'')
    //            AND IsDeleted = FALSE;",
    //        new
    //        {
    //            filters.CompanyId,
    //            filters.EntityType,
    //            filters.DocumentTypeCode,
    //            filters.DivisionCode,
    //            filters.DepartmentCode,
    //            filters.SubDepartmentCode,
    //            filters.BusinessDomainCode
    //        });

    //        //-----------------------------------------
    //        // 3️⃣ If NO policy exists → CREATE ONE
    //        //-----------------------------------------

    //        if (policyId == null)
    //        {
    //            policyId = await _dapperService.ExecuteScalarAsync<int>(@"
    //                INSERT INTO WorkflowPolicies
    //                (
    //                    CompanyId,
    //                    Name,
    //                    EntityType,
    //                    DivisionCode,
    //                    DepartmentCode,
    //                    SubDepartmentCode,
    //                    BusinessDomainCode,
    //                    DocumentTypeCode,
    //                    CreatedAt,
    //                    CreatedBy,
    //                    LastModifiedAt,
    //                    LastModifiedBy

    //                )
    //                VALUES
    //                (
    //                    @CompanyId,
    //                    'Auto Generated Policy',
    //                    @EntityType,
    //                    @DivisionCode,
    //                    @DepartmentCode,
    //                    @SubDepartmentCode,
    //                    @BusinessDomainCode,
    //                    @DocumentTypeCode,
    //                    NOW(),
    //                    'system',
    //                    NOW(),
    //                    'system'
    //                )
    //                RETURNING Id;",
    //            new
    //            {
    //                filters.CompanyId,
    //                filters.EntityType,
    //                filters.DivisionCode,
    //                filters.DepartmentCode,
    //                filters.SubDepartmentCode,
    //                filters.BusinessDomainCode,
    //                filters.DocumentTypeCode
    //            });
    //        }

    //        //-----------------------------------------
    //        // 4️⃣ Resolve ACTIVE Version
    //        //-----------------------------------------

    //        var versionId = await _dapperService.ExecuteScalarAsync<int?>(@"
    //            SELECT Id
    //            FROM WorkflowPolicyVersions
    //            WHERE CompanyId = @CompanyId
    //            AND WorkflowPolicyId = @PolicyId
    //            AND IsActive = TRUE
    //            LIMIT 1;",
    //        new
    //        {
    //            filters.CompanyId,
    //            PolicyId = policyId
    //        });

    //        //-----------------------------------------
    //        // 5️⃣ If NO version exists → CREATE ONE
    //        //-----------------------------------------

    //        if (versionId == null)
    //        {
    //            versionId = await _dapperService.ExecuteScalarAsync<int>(@"
    //                INSERT INTO WorkflowPolicyVersions
    //                (
    //                    CompanyId,
    //                    WorkflowPolicyId,
    //                    VersionNumber,
    //                    IsActive,
    //                    CreatedAt,
    //                    CreatedBy
    //                )
    //                VALUES
    //                (
    //                    @CompanyId,
    //                    @PolicyId,
    //                    (
    //                        SELECT COALESCE(MAX(VersionNumber),0) + 1
    //                        FROM WorkflowPolicyVersions
    //                        WHERE WorkflowPolicyId = @PolicyId
    //                    ),
    //                    TRUE,
    //                    NOW(),
    //                    'system'
    //                )
    //                RETURNING Id;",
    //            new
    //            {
    //                filters.CompanyId,
    //                PolicyId = policyId
    //            });
    //        }

    //        //-----------------------------------------
    //        // 6️⃣ Get Next StepOrder
    //        //-----------------------------------------

    //        var nextOrder = await _dapperService.ExecuteScalarAsync<int>(@"
    //            SELECT COALESCE(MAX(StepOrder),0) + 1
    //            FROM WorkflowStepDefinitions
    //            WHERE WorkflowPolicyVersionId = @VersionId;",
    //                new { VersionId = versionId });

    //        //-----------------------------------------
    //        // 7️⃣ Prevent duplicate user ONLY in SAME STEP
    //        //-----------------------------------------

    //        var exists = await _dapperService.ExecuteScalarAsync<int>(@"
    //            SELECT COUNT(*)
    //            FROM WorkflowStepDefinitions
    //            WHERE WorkflowPolicyVersionId = @VersionId
    //            AND StepOrder = @StepOrder
    //            AND UserId = @UserId
    //            AND IsDeleted = FALSE;",
    //        new
    //        {
    //            VersionId = versionId,
    //            StepOrder = nextOrder,
    //            UserId = user.Id
    //        });

    //        if (exists > 0)
    //            throw new CustomException("User already exists in this step.", 409);

    //        //-----------------------------------------
    //        // 8️⃣ Insert Step
    //        //-----------------------------------------

    //        await _dapperService.ExecuteAsync(@"
    //            INSERT INTO WorkflowStepDefinitions
    //            (
    //                CompanyId,
    //                WorkflowPolicyVersionId,
    //                StepOrder,
    //                StepGroup,
    //                StepType,
    //                RoleId,
    //                UserId,
    //                RequiresAllApprovals,
    //                IsActive,
    //                IsDeleted,
    //                CreatedAt,
    //                CreatedBy,
    //                LastModifiedAt,
    //                LastModifiedBy
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @VersionId,
    //                @StepOrder,
    //                1,
    //                @StepType,
    //                @RoleId,
    //                @UserId,
    //                @RequiresAllApprovals,
    //                TRUE,
    //                FALSE,
    //                NOW(),
    //                'system',
    //                NOW(),
    //                'system'
    //            );",
    //        new
    //        {
    //            filters.CompanyId,
    //            VersionId = versionId,
    //            StepOrder = nextOrder,
    //            filters.StepType,
    //            user.RoleId,
    //            UserId = user.Id,
    //            RequiresAllApprovals = filters.IsParallelApproval
    //        });

    //        //-----------------------------------------
    //        // 9️⃣ Return Updated Steps
    //        //-----------------------------------------

    //        var steps = await _dapperService.QueryAsync<WorkflowStepReadDto>(@"
    //        SELECT *
    //        FROM Vw_WorkflowStepDefinitions
    //        WHERE WorkflowPolicyVersionId = @VersionId
    //        AND IsDeleted = FALSE
    //        ORDER BY StepOrder;",
    //        new { VersionId = versionId });

    //        return steps.ToList();
    //    }
    //    catch
    //    {
    //        throw;
    //    }
    //}

    public async Task<List<WorkflowStepReadDto>> CreateWorkflowStepsByFilterAsync(WorkFlowStepsFilterDto filters)
    {
        try
        {

            var user = await GetUsersByFiltersAsync(filters);

            //-----------------------------------------
            // 1️⃣ Resolve WorkflowPolicyId
            //-----------------------------------------

            var policyId = await _dapperService.ExecuteScalarAsync<int?>(@"
                SELECT Id
                FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId
                AND EntityType = @EntityType
                AND DocumentTypeCode = @DocumentTypeCode
                AND COALESCE(DivisionCode,'') = COALESCE(@DivisionCode,'')
                AND COALESCE(DepartmentCode,'') = COALESCE(@DepartmentCode,'')
                AND COALESCE(SubDepartmentCode,'') = COALESCE(@SubDepartmentCode,'')
                AND COALESCE(BusinessDomainCode,'') = COALESCE(@BusinessDomainCode,'')
                AND IsDeleted = FALSE;",
            new
            {
                filters.CompanyId,
                filters.EntityType,
                filters.DocumentTypeCode,
                filters.DivisionCode,
                filters.DepartmentCode,
                filters.SubDepartmentCode,
                filters.BusinessDomainCode
            });

            //-----------------------------------------
            // 2️⃣ Create Policy IF NOT Exists
            //-----------------------------------------

            if (policyId == null)
            {
                policyId = await _dapperService.ExecuteScalarAsync<int>(@"
                    INSERT INTO WorkflowPolicies
                    (
                        CompanyId,
                        Name,
                        EntityType,
                        DivisionCode,
                        DepartmentCode,
                        SubDepartmentCode,
                        BusinessDomainCode,
                        DocumentTypeCode,
                        IsActive,
                        IsDeleted,
                        CreatedAt,
                        CreatedBy,
                        LastModifiedAt, 
                        LastModifiedBy
                    )
                    VALUES
                    (
                        @CompanyId,
                        'Configured Policy',
                        @EntityType,
                        @DivisionCode,
                        @DepartmentCode,
                        @SubDepartmentCode,
                        @BusinessDomainCode,
                        @DocumentTypeCode,
                        TRUE,
                        FALSE,
                        NOW(), 
                        'system', 
                        NOW(), 
                        'system'
                    )
                    RETURNING Id;",
                filters);
            }

            //-----------------------------------------
            // 3️⃣ Resolve ACTIVE Version
            //-----------------------------------------

            var versionId = await _dapperService.ExecuteScalarAsync<int?>(@"
                SELECT Id
                FROM WorkflowPolicyVersions
                WHERE CompanyId = @CompanyId
                AND WorkflowPolicyId = @PolicyId
                AND IsActive = TRUE
                LIMIT 1;",
            new { filters.CompanyId, PolicyId = policyId });

            //-----------------------------------------
            // 4️⃣ Create Version IF NOT Exists
            //-----------------------------------------

            if (versionId == null)
            {
                versionId = await _dapperService.ExecuteScalarAsync<int>(@"
                    INSERT INTO WorkflowPolicyVersions
                    (
                        CompanyId,
                        WorkflowPolicyId,
                        VersionNumber,
                        IsActive,
                        CreatedAt,
                        CreatedBy
                    )
                    VALUES
                    (
                        @CompanyId,
                        @PolicyId,
                        1,
                        TRUE,
                        NOW(),
                        'system'
                    )
                    RETURNING Id;",
                new { filters.CompanyId, PolicyId = policyId });
            }

            //-----------------------------------------
            // 5️⃣ Get Next StepOrder
            //-----------------------------------------

            var nextOrder = await _dapperService.ExecuteScalarAsync<int>(@"
                SELECT COALESCE(MAX(StepOrder),0) + 1
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId;",
                new { VersionId = versionId });

            //-----------------------------------------
            // 6️⃣ Insert Step
            //-----------------------------------------

            await _dapperService.ExecuteAsync(@"
                INSERT INTO WorkflowStepDefinitions
                (
                    CompanyId, 
                    WorkflowPolicyVersionId, 
                    StepOrder, 
                    StepGroup, 
                    StepType, 
                    RoleId, 
                    UserId, 
                    RequiresAllApprovals, 
                    IsActive, 
                    IsDeleted, 
                    CreatedAt, 
                    CreatedBy, 
                    LastModifiedAt, 
                    LastModifiedBy
                )
                VALUES
                (
                    @CompanyId, @VersionId, @StepOrder, 1, @StepType, @RoleId, @UserId, @RequiresAllApprovals, TRUE, FALSE, NOW(), 'system', NOW(), 'system'
                );",
            new
            {
                filters.CompanyId,
                VersionId = versionId,
                StepOrder = nextOrder,
                filters.StepType,
                user.RoleId,
                UserId = user.Id,
                RequiresAllApprovals = filters.IsParallelApproval
            });

            //-----------------------------------------
            // 7️⃣ Return Updated Steps
            //-----------------------------------------

            var steps = await _dapperService.QueryAsync<WorkflowStepReadDto>(@"
                SELECT *
                FROM Vw_WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId
                AND IsDeleted = FALSE
                ORDER BY StepOrder;",
                new { VersionId = versionId });

            return steps.ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    public async Task<int> ValidateWorkflowIsReadyAsync(
            int companyId,
            string entityType,
            string documentTypeCode,
            string divisionCode,
            string departmentCode,
            string subDepartmentCode,
            string businessDomainCode)
    {
        //-----------------------------------------
        // 1️⃣ Find Policy
        //-----------------------------------------

        var policyId = await _dapperService.ExecuteScalarAsync<int?>(@"
        SELECT Id
        FROM WorkflowPolicies
        WHERE CompanyId = @CompanyId
        AND EntityType = @EntityType
        AND DocumentTypeCode = @DocType
        AND COALESCE(DivisionCode,'') = COALESCE(@DivisionCode,'')
        AND COALESCE(DepartmentCode,'') = COALESCE(@DepartmentCode,'')
        AND COALESCE(SubDepartmentCode,'') = COALESCE(@SubDepartmentCode,'')
        AND COALESCE(BusinessDomainCode,'') = COALESCE(@BusinessDomainCode,'')
        AND IsActive = TRUE
        AND IsDeleted = FALSE;",
        new
        {
            CompanyId = companyId,
            EntityType = entityType,
            DocType = documentTypeCode,
            DivisionCode = divisionCode,
            DepartmentCode = departmentCode,
            SubDepartmentCode = subDepartmentCode,
            BusinessDomainCode = businessDomainCode
        });

        if (policyId == null)
            throw new Exception("Workflow policy not configured for this Cabinet Scope.");

        //-----------------------------------------
        // 2️⃣ Find Active Version
        //-----------------------------------------

        var versionId = await _dapperService.ExecuteScalarAsync<int?>(@"
        SELECT Id
        FROM WorkflowPolicyVersions
        WHERE CompanyId = @CompanyId
        AND WorkflowPolicyId = @PolicyId
        AND IsActive = TRUE
        LIMIT 1;",
        new { CompanyId = companyId, PolicyId = policyId });

        if (versionId == null)
            throw new Exception("Workflow policy version not configured.");

        //-----------------------------------------
        // 3️⃣ Check Steps Exist
        //-----------------------------------------

        var steps = await _dapperService.ExecuteScalarAsync<int>(@"
        SELECT COUNT(*)
        FROM WorkflowStepDefinitions
        WHERE WorkflowPolicyVersionId = @VersionId
        AND IsDeleted = FALSE;",
        new { VersionId = versionId });

        if (steps == 0)
            throw new Exception("Workflow steps not configured.");

        return versionId.Value;
    }

    public async Task<List<WorkflowStepReadDto>> CreateWorkflowStepsByFilterAsync_old(WorkFlowStepsFilterDto filters)
    {

        try
        {
            //-----------------------------------------
            // STEP 1 — FETCH USERS
            //-----------------------------------------

            var user = await GetUsersByFiltersAsync(filters);


            int nextVersion = await _dapperService.ExecuteScalarAsync<int>(
                @"
                SELECT COALESCE(MAX(VersionNumber),0) + 1
                FROM WorkflowPolicyVersions
                WHERE CompanyId = @CompanyId
                AND WorkflowPolicyId = @PolicyId;
                ",
                new
                {
                    filters.CompanyId,
                    PolicyId = filters.WorkflowPolicyId
                });

            // ✅ 2. Create Version = 1
            var versionId = @"
                    INSERT INTO WorkflowPolicyVersions
                    (CompanyId, WorkflowPolicyId, VersionNumber, IsActive, CreatedAt, CreatedBy)
                    VALUES
                    (@CompanyId, @PolicyId, @VersionNumber, TRUE, NOW(), @User)
                    RETURNING Id;
                    ";

            int insertedWorkflowPolicyVersions = await _dapperService.ExecuteScalarAsync<int>(
             versionId,
             new
             {
                 filters.CompanyId,
                 PolicyId = filters.WorkflowPolicyId,
                 VersionNumber = nextVersion,
                 User = user.Id
             });


            var existingUsersSql = @"
                SELECT UserId
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @WorkflowPolicyId
                AND UserId IN (@UserIds);
                ";

            var existingUserIds = (await _dapperService.QueryAsync<long>(
                existingUsersSql,
                new
                {
                    filters.WorkflowPolicyId,
                    UserIds = user.Id
                }))
                .ToHashSet();

            if (existingUserIds.Count() > 0)
            {
                throw new CustomException("WorkflowStepDefinitions already exists", 409);
            }

            //-----------------------------------------
            // STEP 2 — GET MAX SEQUENCE (LOCK ROWS)
            //-----------------------------------------

            var seqSql = @"
                    SELECT COALESCE(MAX(ws.StepOrder),0)
                    FROM WorkflowStepDefinitions ws
                          LEFT JOIN WorkflowPolicyVersions wfp
                          ON ws.WorkflowPolicyVersionId = wfp.Id 
                    WHERE wfp.WorkflowPolicyId = @WorkflowPolicyId;
                    ";

            int maxSequence = await _dapperService.ExecuteScalarAsync<int>(
                seqSql,
                new { filters.WorkflowPolicyId });


            //-----------------------------------------
            // STEP 3 — INSERT WORKFLOW STEPS
            //-----------------------------------------

            int nextSequence = maxSequence + 1;

            var stepOrder = nextSequence++;
            var UserId = user.Id;
            var RequiresAllApprovals = filters.IsParallelApproval;
            var now = DateTime.UtcNow; // or DateTime.Now depending on your DB setup
            var currentUser = "system"; // Get this from your user context/session
            // ✅ 3. Insert Steps
            var stepSql = @"
            INSERT INTO WorkflowStepDefinitions
            (CompanyId,WorkflowPolicyVersionId, StepOrder, StepGroup, StepType, RoleId, UserId, RequiresAllApprovals,CreatedAt,CreatedBy,LastModifiedAt,LastModifiedBy)
            VALUES
            (@CompanyId, @VersionId, @StepOrder, @StepGroup, @StepType, @RoleId, @UserId, @RequiresAllApprovals, @CreatedAt, @CreatedBy,@LastModifiedAt,@LastModifiedBy);
            ";
            var insertedSteps = await _dapperService.QueryAsync<WorkflowStepReadDto>(
               stepSql,
               new
               {
                   filters.CompanyId,
                   VersionId = insertedWorkflowPolicyVersions,
                   stepOrder,   //step.StepOrder,
                   StepGroup = 1, //step.StepGroup == 0 ? 1 : step.StepGroup,
                   filters.StepType,
                   user.RoleId,
                   UserId,
                   RequiresAllApprovals, //step.RequiresAllApprovals,

                   // Add these audit fields
                   CreatedAt = now,
                   CreatedBy = currentUser,
                   LastModifiedAt = now,
                   LastModifiedBy = currentUser
               });

            //-----------------------------------------
            // STEP 4 — MAP USER INFO FOR UI
            //-----------------------------------------
            string query = $@"
                         Select * from Vw_WorkflowStepDefinitions ws
                    WHERE ws.IsActive = True AND ws.WorkflowPolicyId ={filters.WorkflowPolicyId}
                    AND ws.IsDeleted = False";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new WorkflowStepReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    WorkflowPolicyVersionId = row.Table.Columns.Contains("WorkflowPolicyId") ? row.Field<int>("WorkflowPolicyId") : 0,
                    StepOrder = row.Table.Columns.Contains("StepOrder") ? row.Field<int>("StepOrder") : 0,
                    RoleId = row.Field<int?>("RoleId") ?? 0,
                    UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
                    ApprovalLevel = row.Table.Columns.Contains("ApprovalLevel") ? row.Field<int>("ApprovalLevel") : 0,

                    EmployeeCode = row.Field<string>("EmployeeCode"),
                    EmployeeName = row.Field<string>("EmployeeName"),

                    Designation = row.Field<string>("Designation"),
                    UserRole = row.Field<string>("UserRole"),

                    CanEdit = row.Table.Columns.Contains("CanEdit") && row.Field<bool?>("CanEdit") == true,
                    IsParallelApproval = row.Table.Columns.Contains("IsParallelApproval") && row.Field<bool?>("IsParallelApproval") == true,
                    RequireCrossFunctionalHead = row.Table.Columns.Contains("RequireCrossFunctionalHead") && row.Field<bool?>("RequireCrossFunctionalHead") == true,

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

            return divisions;

        }
        catch
        {
            //await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<UserReadDto> GetUsersByFiltersAsync(WorkFlowStepsFilterDto filters)
    {
        try
        {
            var parameters = new DynamicParameters();
            var whereConditions = new List<string>();

            // Base query
            string baseQuery = @"
            SELECT u.*, 
                   div.Name as Division, 
                   dep.Name as Department,
                   sdep.Name as SubDepartment, 
                   c.Name as Company, 
                   bd.Name as BusinessDomain, 
                   des.Name as Designation,
	               r.Name AS UserRole
            FROM Users u
            LEFT JOIN Divisions div ON u.DivisionCode = div.Code
            LEFT JOIN Departments dep ON u.DepartmentCode = dep.Code
            LEFT JOIN SubDepartments sdep ON u.SubDepartmentCode = sdep.Code
            LEFT JOIN Designations des ON u.DesignationCode = des.Code
            LEFT JOIN Companies c ON u.CompanyId = c.Id
            LEFT JOIN BusinessDomains bd ON u.BusinessDomainCode = bd.Code
            LEFT JOIN UserRoles ur
            ON u.Id = ur.UserId
	            LEFT JOIN Roles r
	            ON ur.RoleId = r.Id
            WHERE u.IsActive = TRUE AND u.IsDeleted = FALSE";

            // Add conditions based on provided filters
            if (!string.IsNullOrEmpty(filters.DivisionCode))
            {
                whereConditions.Add("u.DivisionCode = @DivisionCode");
                parameters.Add("@DivisionCode", filters.DivisionCode);
            }

            if (!string.IsNullOrEmpty(filters.DepartmentCode))
            {
                whereConditions.Add("u.DepartmentCode = @DepartmentCode");
                parameters.Add("@DepartmentCode", filters.DepartmentCode);
            }

            if (!string.IsNullOrEmpty(filters.SubDepartmentCode))
            {
                whereConditions.Add("u.SubDepartmentCode = @SubDepartmentCode");
                parameters.Add("@SubDepartmentCode", filters.SubDepartmentCode);
            }

            if (!string.IsNullOrEmpty(filters.BusinessDomainCode))
            {
                whereConditions.Add("u.BusinessDomainCode = @BusinessDomainCode");
                parameters.Add("@BusinessDomainCode", filters.BusinessDomainCode);
            }

            // Multiple Designations
            if (filters.DesignationCodes != null && filters.DesignationCodes.Any())
            {
                var designationList = filters.DesignationCodes.ToList();
                var designationParams = new List<string>();

                for (int i = 0; i < designationList.Count; i++)
                {
                    var paramName = $"@DesignationCode{i}";
                    designationParams.Add(paramName);
                    parameters.Add(paramName, designationList[i]);
                }

                whereConditions.Add($"u.DesignationCode IN ({string.Join(",", designationParams)})");
            }

            // Multiple Roles
            if (filters.Roles != null && filters.Roles.Any())
            {
                var roleList = filters.Roles.ToList();
                var roleParams = new List<string>();

                for (int i = 0; i < roleList.Count; i++)
                {
                    var paramName = $"@Role{i}";
                    roleParams.Add(paramName);
                    parameters.Add(paramName, roleList[i]);
                }

                whereConditions.Add($"ur.RoleId IN ({string.Join(",", roleParams)})");
            }


            // Multiple Employee Codes
            if (filters.EmployeeCodes != null && filters.EmployeeCodes.Any())
            {
                var employeeCodeList = filters.EmployeeCodes.ToList();
                var employeeCodeParams = new List<string>();

                for (int i = 0; i < employeeCodeList.Count; i++)
                {
                    var paramName = $"@EmployeeCode{i}";
                    employeeCodeParams.Add(paramName);
                    parameters.Add(paramName, employeeCodeList[i]);
                }

                whereConditions.Add($"u.EmployeeCode IN ({string.Join(",", employeeCodeParams)})");
            }

            // Combine all conditions
            if (whereConditions.Count > 0)
            {
                baseQuery += " AND " + string.Join(" AND ", whereConditions);
            }

            // Add ORDER BY for consistent results
            baseQuery += " ORDER BY u.EmployeeName";

            var resultList = (await _dapperService.QuerySingleAsync<UserReadDto>(baseQuery, parameters));


            if (resultList == null)
                throw new CustomException("No users found matching the criteria", 404);

            var divisions = new UserReadDto
            {
                Id = resultList.Id,
                CompanyId = resultList.CompanyId,
                Company = resultList.Company,
                EmployeeCode = resultList.EmployeeCode,
                EmployeeName = resultList.EmployeeName,
                Email = resultList.Email,

                Division = resultList.Division,
                DivisionCode = resultList.DivisionCode,

                Department = resultList.Department,
                DepartmentCode = resultList.DepartmentCode,

                SubDepartment = resultList.SubDepartment,
                SubDepartmentCode = resultList.SubDepartmentCode,

                BusinessDomain = resultList.BusinessDomain,
                BusinessDomainCode = resultList.BusinessDomainCode,

                Designation = resultList.Designation,
                DesignationCode = resultList.DesignationCode,

                ReportingTo = resultList.ReportingTo,
                Grade = resultList.Grade,

            };

            return divisions;



        }
        catch (Exception)
        {
            throw;
        }
    }

}
