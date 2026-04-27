using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models; 
using System.Data; 

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
                FROM WorkflowStepDefinitions
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("WorkflowStepDefinitions not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE WorkflowStepDefinitions
                SET IsDeleted = True,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode}'
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

    public async Task<List<WorkflowStepDefiniationReadDto>> GetByDocumentTypeCodeAsync(GetStepDefinitionFilterDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            int CompanyId = Convert.ToInt32(_CompanyId);

            //string query = $@"
            //    SELECT ws.*
            //    FROM Vw_WorkflowStepDefinitions ws
            //    JOIN WorkflowPolicies wp
            //        ON wp.Id = ws.WorkflowPolicyId
            //        AND wp.CompanyId = ws.CompanyId
            //    WHERE wp.CompanyId = @CompanyId
            //    AND wp.EntityType = @EntityType
            //    AND wp.DocumentTypeCode = @DocumentTypeCode
            //    -- Allow filtering by specific policy name or ID
            //    AND (COALESCE(@DivisionCode, '') = '' OR wp.DivisionCode = @DivisionCode OR (wp.DivisionCode IS NULL AND @DivisionCode IS NULL))
            //    AND (COALESCE(@DepartmentCode, '') = '' OR wp.DepartmentCode = @DepartmentCode OR (wp.DepartmentCode IS NULL AND @DepartmentCode IS NULL))
            //    AND (COALESCE(@SubDepartmentCode, '') = '' OR wp.SubDepartmentCode = @SubDepartmentCode OR (wp.SubDepartmentCode IS NULL AND @SubDepartmentCode IS NULL))
            //    AND (COALESCE(@BusinessDomainCode, '') = '' OR wp.BusinessDomainCode = @BusinessDomainCode OR (wp.BusinessDomainCode IS NULL AND @BusinessDomainCode IS NULL))
            //    AND ws.IsActive = TRUE
            //    AND ws.IsDeleted = FALSE
            //    ORDER BY ws.StepOrder, ws.employeecode ASC;";
            string query = $@"
                SELECT 
                    wsd.Id AS id, 
                    wsd.CompanyId AS companyid, 
                    c.Name AS company,
                    wsd.WorkflowPolicyVersionId AS workflowpolicyversionid,
                    wp.Id AS workflowpolicyid, 
                    wp.Name AS workflowpolicyname,
                    wsd.StepOrder AS steporder, 
                    wsd.StepGroup AS stepgroup, 
                    wsd.StepType AS steptype,
                    wsd.RoleId AS roleid, 
                    wsd.DesignationId AS designationid, 
                    wsd.UserId AS userid, 
                    --wsd.ApprovalLevel AS approvallevel, 
                    wsd.RequiresAllApprovals AS requiresallapprovals,
                    wsd.RequiresAllApprovals AS isparallelapproval,
                    FALSE AS canedit,
                    FALSE AS requirecrossfunctionalhead,
                    e.empcode AS employeecode, 
                    e.firstname, e.midname, e.lastname,
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS employeename,
                    COALESCE(r_step.name, r_emp.name) AS role, 
                    COALESCE(r_step.name, r_emp.name) AS userrole, 
                    COALESCE(des_step.name, des_fallback.name) AS designation,
                    COALESCE(des_step.code, des_fallback.code) AS designationcode,
                    wp.DocumentTypeCode AS documenttypecode, 
                    dt.Name AS documenttype,
                    wsd.IsActive AS isactive, 
                    wsd.IsDeleted AS isdeleted, 
                    wsd.CreatedAt AS createdat, 
                    wsd.CreatedBy AS createdby, 
                    wsd.LastModifiedAt AS lastmodifiedat, 
                    wsd.LastModifiedBy AS lastmodifiedby
                FROM WorkflowStepDefinitions wsd
                JOIN WorkflowPolicyVersions wpv ON wpv.Id = wsd.WorkflowPolicyVersionId
                JOIN WorkflowPolicies wp ON wp.Id = wpv.WorkflowPolicyId
                LEFT JOIN Companies c ON c.Id = wsd.CompanyId
                LEFT JOIN DocumentTypes dt ON dt.Code = wp.DocumentTypeCode
                LEFT JOIN tblEmployee e ON wsd.UserId IS NOT NULL AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(wsd.UserId::text), '0')
                LEFT JOIN tblempjobprofile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE
                LEFT JOIN tblsetupsdetail r_step ON wsd.RoleId IS NOT NULL AND r_step.sdlid = wsd.RoleId
                LEFT JOIN tblsetupsdetail r_emp ON ejp.roleid IS NOT NULL AND r_emp.sdlid = ejp.roleid
                LEFT JOIN tblsetupsdetail des_step ON wsd.DesignationId IS NOT NULL AND des_step.sdlid = wsd.DesignationId
                LEFT JOIN tblsetupsdetail des_fallback ON COALESCE(ejp.dsgid, e.dsgid) IS NOT NULL AND des_fallback.sdlid = COALESCE(ejp.dsgid, e.dsgid)
                WHERE wp.CompanyId = @CompanyId
                AND wp.EntityType = @EntityType
                AND wp.DocumentTypeCode = @DocumentTypeCode
                -- Allow filtering by specific policy name or ID
                AND (COALESCE(@DivisionCode, '') = '' OR wp.DivisionCode = @DivisionCode OR (wp.DivisionCode IS NULL AND @DivisionCode IS NULL))
                AND (COALESCE(@DepartmentCode, '') = '' OR wp.DepartmentCode = @DepartmentCode OR (wp.DepartmentCode IS NULL AND @DepartmentCode IS NULL))
                AND (COALESCE(@SubDepartmentCode, '') = '' OR wp.SubDepartmentCode = @SubDepartmentCode OR (wp.SubDepartmentCode IS NULL AND @SubDepartmentCode IS NULL))
                AND (COALESCE(@BusinessDomainCode, '') = '' OR wp.BusinessDomainCode = @BusinessDomainCode OR (wp.BusinessDomainCode IS NULL AND @BusinessDomainCode IS NULL))
                AND wsd.IsActive = TRUE
                AND wsd.IsDeleted = FALSE
                ORDER BY wsd.StepOrder, e.empcode ASC;
                ";

            var queryParams = new
            {
                CompanyId = CompanyId,
                input.EntityType,
                input.DocumentTypeCode,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode
            };

            var results = await _common.QueryAsync<dynamic>(query, queryParams);

            var dtos = new List<WorkflowStepDefiniationReadDto>();
            foreach (var row in results)
            {
                // Cast row to IDictionary<string, object> to access properties safely
                var rowDict = row as IDictionary<string, object>;

                string fName = GetValue<string>(rowDict, "firstname");
                string mName = GetValue<string>(rowDict, "midname");
                string lName = GetValue<string>(rowDict, "lastname");
                string employeeName = string.Join(" ", new[] { fName, mName, lName }.Where(s => !string.IsNullOrWhiteSpace(s)));

                dtos.Add(new WorkflowStepDefiniationReadDto
                {
                    Id = GetValue<int>(rowDict, "id"),

                    CompanyId = GetValue<int>(rowDict, "companyid"),
                    Company = GetValue<string>(rowDict, "company"),

                    WorkflowPolicyId = GetValue<int>(rowDict, "workflowpolicyid"),
                    WorkflowPolicyName = GetValue<string>(rowDict, "workflowpolicyname"),

                    WorkflowPolicyVersionId = GetValue<int>(rowDict, "workflowpolicyversionid"),

                    StepOrder = GetValue<int>(rowDict, "steporder"),
                    StepGroup = GetValue<int>(rowDict, "stepgroup"),
                    StepType = GetValue<string>(rowDict, "steptype"),

                    RoleId = GetValue<int>(rowDict, "roleid"),
                    DesignationId = GetValue<int>(rowDict, "designationid"),
                    UserRole = GetValue<string>(rowDict, "role"),
                    UserId = GetValue<int>(rowDict, "userid"),
                    ApprovalLevel = GetValue<int>(rowDict, "approvallevel"),
                    RequiresAllApprovals = GetValue<bool>(rowDict, "requiresallapprovals"),

                    EmployeeCode = GetValue<string>(rowDict, "employeecode"),
                    EmployeeName = employeeName,

                    Designation = GetValue<string>(rowDict, "designation"),
                    DesignationCode = GetValue<string>(rowDict, "designationcode"),

                    DocumentType = GetValue<string>(rowDict, "documenttype"),
                    DocumentTypeCode = GetValue<string>(rowDict, "documenttypecode"),                    


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
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<WorkflowStepDefiniationReadDto>> GetWorkflowPolicyDocumentTypeCodeAsync(GetStepDefinitionFilterDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            int CompanyId = Convert.ToInt32(_CompanyId);

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
                AND (COALESCE(@DivisionCode, '') = '' OR wp.DivisionCode = @DivisionCode OR (wp.DivisionCode IS NULL AND @DivisionCode IS NULL))
                AND (COALESCE(@DepartmentCode, '') = '' OR wp.DepartmentCode = @DepartmentCode OR (wp.DepartmentCode IS NULL AND @DepartmentCode IS NULL))
                AND (COALESCE(@SubDepartmentCode, '') = '' OR wp.SubDepartmentCode = @SubDepartmentCode OR (wp.SubDepartmentCode IS NULL AND @SubDepartmentCode IS NULL))
                AND (COALESCE(@BusinessDomainCode, '') = '' OR wp.BusinessDomainCode = @BusinessDomainCode OR (wp.BusinessDomainCode IS NULL AND @BusinessDomainCode IS NULL))
                AND ws.IsActive = TRUE
                AND ws.IsDeleted = FALSE
                ORDER BY ws.StepOrder, ws.employeecode ASC;";

            var queryParams = new
            {
                CompanyId = CompanyId,
                input.EntityType,
                input.DocumentTypeCode,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode
            };

            var results = await _common.QueryAsync<dynamic>(query, queryParams);

            var dtos = new List<WorkflowStepDefiniationReadDto>();
            foreach (var row in results)
            {
                // Cast row to IDictionary<string, object> to access properties safely
                var rowDict = row as IDictionary<string, object>;

                string fName = GetValue<string>(rowDict, "firstname");
                string mName = GetValue<string>(rowDict, "midname");
                string lName = GetValue<string>(rowDict, "lastname");
                string employeeName = string.Join(" ", new[] { fName, mName, lName }.Where(s => !string.IsNullOrWhiteSpace(s)));

                dtos.Add(new WorkflowStepDefiniationReadDto
                {
                    Id = GetValue<int>(rowDict, "id"),

                    CompanyId = GetValue<int>(rowDict, "companyid"),
                    Company = GetValue<string>(rowDict, "company"),

                    WorkflowPolicyId = GetValue<int>(rowDict, "workflowpolicyid"),
                    WorkflowPolicyName = GetValue<string>(rowDict, "workflowpolicyname"),

                    WorkflowPolicyVersionId = GetValue<int>(rowDict, "workflowpolicyversionid"),

                    StepOrder = GetValue<int>(rowDict, "steporder"),
                    StepGroup = GetValue<int>(rowDict, "stepgroup"),
                    StepType = GetValue<string>(rowDict, "steptype"),

                    RoleId = GetValue<int>(rowDict, "roleid"),
                    DesignationId = GetValue<int>(rowDict, "designationid"),
                    UserRole = GetValue<string>(rowDict, "role"),
                    UserId = GetValue<int>(rowDict, "userid"),
                    ApprovalLevel = GetValue<int>(rowDict, "approvallevel"),
                    RequiresAllApprovals = GetValue<bool>(rowDict, "requiresallapprovals"),

                    EmployeeCode = GetValue<string>(rowDict, "employeecode"),
                    EmployeeName = employeeName,

                    Designation = GetValue<string>(rowDict, "designation"),
                    DesignationCode = GetValue<string>(rowDict, "designationcode"),

                    DocumentType = GetValue<string>(rowDict, "documenttype"),
                    DocumentTypeCode = GetValue<string>(rowDict, "documenttypecode"),


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
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // Helper method to safely get values from the dynamic row
    private static T GetValue<T>(IDictionary<string, object> row, string columnName)
    {
        if (row.ContainsKey(columnName) && row[columnName] != null && row[columnName] != DBNull.Value)
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

    public async Task<List<string>> GetNextStepApproversAsync(int companyId, long executionId, int stepOrder, IDbTransaction tx = null)
    {
        try
        {
            string query = @"
                SELECT DISTINCT COALESCE(wes.AssignedUserId, TRIM(e.empcode)) AS ApproverId
                FROM WorkflowExecutionSteps wes
                LEFT JOIN public.tblempjobprofile ejp 
                    ON ((wes.AssignedRoleId IS NOT NULL AND ejp.roleid = wes.AssignedRoleId)
                        OR (wes.AssignedDesignationId IS NOT NULL AND ejp.dsgid = wes.AssignedDesignationId))
                    AND COALESCE(ejp.Active, TRUE) = TRUE
                LEFT JOIN public.tblEmployee e 
                    ON e.empid = ejp.empid 
                    AND e.CompanyId = @CompanyId 
                    AND COALESCE(e.Active, 1) = 1
                WHERE wes.WorkflowExecutionId = @ExecutionId 
                  AND wes.StepOrder = @StepOrder
                  AND wes.CompanyId = @CompanyId
                ORDER BY ApproverId ASC;";

            var approvers = await _common.QueryAsync<string>(query, new { CompanyId = companyId, ExecutionId = executionId, StepOrder = stepOrder }, tx);
            return approvers.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IEnumerable<PendingRequestDto>> GetPendingApprovalsAsync()
    {

        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        var clientIp = _clientContextService.GetClientIP();
        var prefix = _utilities.GetPrefix(clientIp); 
        //var userId = _utilities.GetUserid(prefix);
        int CompanyId = int.Parse(_CompanyId);
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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
                wes.AssignedUserId = @empCode

                OR

                wes.AssignedRoleId IN
                (
                    SELECT ejp.roleid
                    FROM public.tblempjobprofile ejp
                    INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                    WHERE e.CompanyId = @CompanyId AND TRIM(e.empcode) = @empCode AND COALESCE(ejp.Active, TRUE) = TRUE
                )
                OR
                wes.AssignedDesignationId IN
                (
                    SELECT ejp.dsgid
                    FROM public.tblempjobprofile ejp
                    INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                    WHERE e.CompanyId = @CompanyId AND TRIM(e.empcode) = @empCode AND COALESCE(ejp.Active, TRUE) = TRUE
                )
            )

            ORDER BY dr.CreatedAt;
            ";

        return await _common.QueryAsync<PendingRequestDto>(
            sql,
            new { CompanyId = CompanyId, UserId = empCode }
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <= 0)
                throw new CustomException("Invalid step ID.", 400);

            int exists = await _dapperService.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WorkflowStepDefinitions WHERE Id = @Id AND IsDeleted = FALSE",
                new { Id = input.Id });

            if (exists == 0)
                throw new CustomException("Workflow Step not found", 404);

            string updateQuery = @"
                UPDATE WorkflowStepDefinitions
                SET 
                    StepOrder = @StepOrder,
                    RoleId = @RoleId,
                    DesignationId = @DesignationId,
                    UserId = @UserId,
                    ApprovalLevel = @ApprovalLevel,
                    RequiresAllApprovals = @RequiresAllApprovals,
                    IsActive = @IsActive,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @ModifiedBy
                WHERE Id = @Id";

            var updateParams = new
            {
                StepOrder = input.Sequence,
                RoleId = input.RoleId > 0 ? input.RoleId : (int?)null,
                DesignationId = input.DesignationId > 0 ? input.DesignationId : (int?)null,
                UserId = input.UserId > 0 ? input.UserId : (int?)null,
                ApprovalLevel = input.ApprovalLevel > 0 ? input.ApprovalLevel : (int?)null,
                RequiresAllApprovals = input.IsParallelApproval,
                IsActive = input.IsActive,
                ModifiedBy = empCode,
                Id = input.Id
            };

            await _dapperService.ExecuteAsync(updateQuery, updateParams);

            // Return updated record
            string selectQuery = @"
                SELECT * 
                FROM Vw_WorkflowStepDefinitions
                WHERE Id = @Id";

            var resultList = await _dapperService.QueryAsync<dynamic>(selectQuery, new { Id = input.Id });
            var rowDict = resultList.FirstOrDefault() as IDictionary<string, object>;

            if (rowDict == null)
                throw new Exception("Failed to fetch updated workflow step.");

            return new WorkflowStepReadDto
            {
                Id = GetValue<int>(rowDict, "id"),
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
                LastModifiedBy = GetValue<string>(rowDict, "lastmodifiedby")
            };
        }
        catch
        {
            throw;
        }
    } 

    public async Task<List<WorkflowStepDefiniationReadDto>> CreateWorkflowStepsByFilterAsync(WorkFlowStepsFilterDto filters)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());


            filters.CompanyId = CompanyId; // Ensure CompanyId is set in filters for downstream queries

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
                CompanyId,
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
                policyId = await _dapperService.ExecuteAsync(@"
                    INSERT INTO WorkflowPolicies
                    (
                        CompanyId, Name, EntityType, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode,
                        DocumentTypeCode, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt,  LastModifiedBy
                    )
                    VALUES
                    (
                        @CompanyId, 'Configured Policy', @EntityType, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode,
                        @DocumentTypeCode, TRUE, FALSE, NOW(), @CreatedBy,  NOW(), @CreatedBy
                    )
                    RETURNING Id;",
                new
                {
                    CompanyId,
                    EntityType = filters.EntityType,
                    DivisionCode = filters.DivisionCode,
                    DepartmentCode = filters.DepartmentCode,
                    SubDepartmentCode = filters.SubDepartmentCode,
                    BusinessDomainCode = filters.BusinessDomainCode,
                    DocumentTypeCode = filters.DocumentTypeCode,
                    CreatedBy = empCode
                });
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
            new { CompanyId, PolicyId = policyId });

            //-----------------------------------------
            // 4️⃣ Create Version IF NOT Exists
            //-----------------------------------------

            if (versionId == null)
            {
                versionId = await _dapperService.ExecuteScalarAsync<int>(@"
                    INSERT INTO WorkflowPolicyVersions
                    (
                        CompanyId, WorkflowPolicyId, VersionNumber, IsActive, CreatedAt, CreatedBy
                    )
                    VALUES
                    (
                        @CompanyId, @PolicyId, 1, TRUE, NOW(), @CreatedBy
                    )
                    RETURNING Id;",
                new { CompanyId, PolicyId = policyId, CreatedBy = empCode });
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

            bool hasRoleOrDesignation = (filters.Roles != null && filters.Roles.Any(r => r > 0)) || 
                                        (filters.DesignationCodes != null && filters.DesignationCodes.Any());

            if (hasRoleOrDesignation)
            {
                if (filters.Roles != null && filters.Roles.Any(r => r > 0))
                {
                    foreach (var roleId in filters.Roles.Where(r => r > 0))
                    {
                        var existingRoleStepCount = await _dapperService.ExecuteScalarAsync<int>(@"
                            SELECT COUNT(1) FROM WorkflowStepDefinitions 
                            WHERE WorkflowPolicyVersionId = @VersionId AND RoleId = @RoleId AND IsDeleted = FALSE;", 
                            new { VersionId = versionId, RoleId = roleId });

                        if (existingRoleStepCount > 0)
                            throw new CustomException("A step for this Role already exists in this workflow.", 409);

                        await _dapperService.ExecuteAsync(@"
                            INSERT INTO WorkflowStepDefinitions
                            (CompanyId, WorkflowPolicyVersionId, StepOrder, StepGroup, StepType, RoleId, DesignationId, UserId, RequiresAllApprovals, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                            VALUES
                            (@CompanyId, @VersionId, @StepOrder, 1, @StepType, @RoleId, NULL, NULL, @RequiresAllApprovals, TRUE, FALSE, NOW(), @CreatedBy, NOW(), @CreatedBy);",
                        new { CompanyId, VersionId = versionId, StepOrder = nextOrder, filters.StepType, RoleId = roleId, RequiresAllApprovals = filters.IsParallelApproval, CreatedBy = empCode });
                        nextOrder++;
                    }
                }

                if (filters.DesignationCodes != null && filters.DesignationCodes.Any())
                {
                    foreach (var desigCodeStr in filters.DesignationCodes)
                    {
                        if (int.TryParse(desigCodeStr, out int designationId) && designationId > 0)
                        {
                            var existingDesigStepCount = await _dapperService.ExecuteScalarAsync<int>(@"
                                SELECT COUNT(1) FROM WorkflowStepDefinitions 
                                WHERE WorkflowPolicyVersionId = @VersionId AND DesignationId = @DesignationId AND IsDeleted = FALSE;", 
                                new { VersionId = versionId, DesignationId = designationId });

                            if (existingDesigStepCount > 0)
                                throw new CustomException("A step for this Designation already exists in this workflow.", 409);

                            await _dapperService.ExecuteAsync(@"
                                INSERT INTO WorkflowStepDefinitions
                                (CompanyId, WorkflowPolicyVersionId, StepOrder, StepGroup, StepType, RoleId, DesignationId, UserId, RequiresAllApprovals, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                                VALUES
                                (@CompanyId, @VersionId, @StepOrder, 1, @StepType, NULL, @DesignationId, NULL, @RequiresAllApprovals, TRUE, FALSE, NOW(), @CreatedBy, NOW(), @CreatedBy);",
                            new { CompanyId, VersionId = versionId, StepOrder = nextOrder, filters.StepType, DesignationId = designationId, RequiresAllApprovals = filters.IsParallelApproval, CreatedBy = empCode });
                            nextOrder++;
                        }
                    }
                }
            }
            else
            {
                // Only fetch specific employees if no role/designation group assignment was selected
                var users = await GetEmployeesByAccessFiltersAsync(filters);

                foreach (var user in users)
                {
                    var existingStepCount = await _dapperService.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(1)
                        FROM WorkflowStepDefinitions
                        WHERE WorkflowPolicyVersionId = @VersionId
                        AND UserId = @UserId
                        AND IsDeleted = FALSE;",
                        new { VersionId = versionId, UserId = user.EmployeeCode });

                    if (existingStepCount > 0)
                    {
                        throw new CustomException($"Employee {user.EmployeeName} ({user.EmployeeCode}) already exists in this workflow.", 409);
                    }

                    await _dapperService.ExecuteAsync(@"
                        INSERT INTO WorkflowStepDefinitions
                        (
                            CompanyId, WorkflowPolicyVersionId, StepOrder, StepGroup, StepType, RoleId, DesignationId, UserId, 
                            RequiresAllApprovals, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt,LastModifiedBy
                        )
                        VALUES
                        (
                            @CompanyId, @VersionId, @StepOrder, 1, @StepType, NULL, NULL, @UserId, @RequiresAllApprovals, TRUE, FALSE, NOW(), @CreatedBy, NOW(), @CreatedBy
                        );",
                    new
                    {
                        CompanyId,
                        VersionId = versionId,
                        StepOrder = nextOrder,
                        filters.StepType,
                        UserId = user.EmployeeCode,
                        RequiresAllApprovals = filters.IsParallelApproval,
                        CreatedBy = empCode
                    });
                    nextOrder++;
                }
            }

            //-----------------------------------------
            // 7️⃣ Return Updated Steps
            //-----------------------------------------


            string query = $@"
                SELECT 
                    wsd.Id AS id, 
                    wsd.CompanyId AS companyid, 
                    c.Name AS company,
                    wsd.WorkflowPolicyVersionId AS workflowpolicyversionid,
                    wp.Id AS workflowpolicyid, 
                    wp.Name AS workflowpolicyname,
                    wsd.StepOrder AS steporder, 
                    wsd.StepGroup AS stepgroup, 
                    wsd.StepType AS steptype,
                    wsd.RoleId AS roleid, 
                    wsd.DesignationId AS designationid, 
                    wsd.UserId AS userid, 
                    --wsd.ApprovalLevel AS approvallevel, 
                    wsd.RequiresAllApprovals AS requiresallapprovals,
                    wsd.RequiresAllApprovals AS isparallelapproval,
                    FALSE AS canedit,
                    FALSE AS requirecrossfunctionalhead,
                    e.empcode AS employeecode, 
                    e.firstname, e.midname, e.lastname,
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS employeename,
                    COALESCE(r_step.name, r_emp.name) AS role, 
                    COALESCE(r_step.name, r_emp.name) AS userrole, 
                    COALESCE(des_step.name, des_fallback.name) AS designation,
                    COALESCE(des_step.code, des_fallback.code) AS designationcode,
                    wp.DocumentTypeCode AS documenttypecode, 
                    dt.Name AS documenttype,
                    wsd.IsActive AS isactive, 
                    wsd.IsDeleted AS isdeleted, 
                    wsd.CreatedAt AS createdat, 
                    wsd.CreatedBy AS createdby, 
                    wsd.LastModifiedAt AS lastmodifiedat, 
                    wsd.LastModifiedBy AS lastmodifiedby
                FROM WorkflowStepDefinitions wsd
                JOIN WorkflowPolicyVersions wpv ON wpv.Id = wsd.WorkflowPolicyVersionId
                JOIN WorkflowPolicies wp ON wp.Id = wpv.WorkflowPolicyId
                LEFT JOIN Companies c ON c.Id = wsd.CompanyId
                LEFT JOIN DocumentTypes dt ON dt.Code = wp.DocumentTypeCode
                LEFT JOIN tblEmployee e ON wsd.UserId IS NOT NULL AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(wsd.UserId::text), '0')
                LEFT JOIN tblempjobprofile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE
                LEFT JOIN tblsetupsdetail r_step ON wsd.RoleId IS NOT NULL AND r_step.sdlid = wsd.RoleId
                LEFT JOIN tblsetupsdetail r_emp ON ejp.roleid IS NOT NULL AND r_emp.sdlid = ejp.roleid
                LEFT JOIN tblsetupsdetail des_step ON wsd.DesignationId IS NOT NULL AND des_step.sdlid = wsd.DesignationId
                LEFT JOIN tblsetupsdetail des_fallback ON COALESCE(ejp.dsgid, e.dsgid) IS NOT NULL AND des_fallback.sdlid = COALESCE(ejp.dsgid, e.dsgid)
                WHERE wp.CompanyId = @CompanyId
                AND wp.EntityType = @EntityType
                AND wp.DocumentTypeCode = @DocumentTypeCode
                -- Allow filtering by specific policy name or ID
                AND (COALESCE(@DivisionCode, '') = '' OR wp.DivisionCode = @DivisionCode OR (wp.DivisionCode IS NULL AND @DivisionCode IS NULL))
                AND (COALESCE(@DepartmentCode, '') = '' OR wp.DepartmentCode = @DepartmentCode OR (wp.DepartmentCode IS NULL AND @DepartmentCode IS NULL))
                AND (COALESCE(@SubDepartmentCode, '') = '' OR wp.SubDepartmentCode = @SubDepartmentCode OR (wp.SubDepartmentCode IS NULL AND @SubDepartmentCode IS NULL))
                AND (COALESCE(@BusinessDomainCode, '') = '' OR wp.BusinessDomainCode = @BusinessDomainCode OR (wp.BusinessDomainCode IS NULL AND @BusinessDomainCode IS NULL))
                AND wsd.IsActive = TRUE
                AND wsd.IsDeleted = FALSE
                ORDER BY wsd.StepOrder, e.empcode ASC";

            var queryParams = new
            {
                CompanyId = CompanyId,
                filters.EntityType,
                filters.DocumentTypeCode,
                filters.DivisionCode,
                filters.DepartmentCode,
                filters.SubDepartmentCode,
                filters.BusinessDomainCode
            };

            var results = await _common.QueryAsync<dynamic>(query, queryParams); 

            var dtos = new List<WorkflowStepDefiniationReadDto>();
            foreach (var row in results)
            {
                // Cast row to IDictionary<string, object> to access properties safely
                var rowDict = row as IDictionary<string, object>;

                string fName = GetValue<string>(rowDict, "firstname");
                string mName = GetValue<string>(rowDict, "midname");
                string lName = GetValue<string>(rowDict, "lastname");
                string employeeName = string.Join(" ", new[] { fName, mName, lName }.Where(s => !string.IsNullOrWhiteSpace(s)));

                dtos.Add(new WorkflowStepDefiniationReadDto
                {
                    Id = GetValue<int>(rowDict, "id"),

                    CompanyId = GetValue<int>(rowDict, "companyid"),
                    Company = GetValue<string>(rowDict, "company"),

                    WorkflowPolicyId = GetValue<int>(rowDict, "workflowpolicyid"),
                    WorkflowPolicyName = GetValue<string>(rowDict, "workflowpolicyname"),

                    WorkflowPolicyVersionId = GetValue<int>(rowDict, "workflowpolicyversionid"),

                    StepOrder = GetValue<int>(rowDict, "steporder"),
                    StepGroup = GetValue<int>(rowDict, "stepgroup"),
                    StepType = GetValue<string>(rowDict, "steptype"),

                    RoleId = GetValue<int>(rowDict, "roleid"),
                    DesignationId = GetValue<int>(rowDict, "designationid"),
                    UserRole = GetValue<string>(rowDict, "role"),
                    UserId = GetValue<int>(rowDict, "userid"),
                    ApprovalLevel = GetValue<int>(rowDict, "approvallevel"),
                    RequiresAllApprovals = GetValue<bool>(rowDict, "requiresallapprovals"),

                    EmployeeCode = GetValue<string>(rowDict, "employeecode"),
                    EmployeeName = employeeName,

                    Designation = GetValue<string>(rowDict, "designation"),
                    DesignationCode = GetValue<string>(rowDict, "designationcode"),

                    DocumentType = GetValue<string>(rowDict, "documenttype"),
                    DocumentTypeCode = GetValue<string>(rowDict, "documenttypecode"),


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

    public async Task<IEnumerable<UserReadDto>> GetEmployeesByAccessFiltersAsync(WorkFlowStepsFilterDto filters)
    {
        try
        {
            string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int companyId = int.TryParse(companyIdStr, out int cId) ? cId : 0;

            var parameters = new DynamicParameters();
            var whereConditions = new List<string>();

            if (companyId > 0)
                whereConditions.Add("e.CompanyId = @CompanyId");
            parameters.Add("@CompanyId", companyId);

            // Production-ready query with Robust TRIM and Padding matching
            string baseQuery = @"
                SELECT DISTINCT 
                       e.empid AS Id, 
                       e.companyid AS CompanyId, 
                       TRIM(e.empcode) AS EmployeeCode, 
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName,
                       e.email AS Email,
                       ual.DivisionCode,
                       ual.DepartmentCode,
                       ual.SubDepartmentCode,
                       ual.BusinessDomainCode,
                       COALESCE(des.name, des_fallback.name) AS Designation,
                       COALESCE(des.code, des_fallback.code) AS DesignationCode,
                       ejp.roleid AS RoleId
                FROM tblEmployee e
                INNER JOIN UserAccessLevels ual 
                    ON TRIM(LEADING '0' FROM TRIM(e.empcode::text)) = TRIM(LEADING '0' FROM TRIM(ual.EmployeeCode::text))
                    AND ual.IsActive = TRUE 
                    AND ual.IsDeleted = FALSE
                LEFT JOIN public.tblempjobprofile ejp 
                    ON e.empid = ejp.empid
                LEFT JOIN public.tblsetupsdetail des 
                    ON ejp.dsgid = des.sdlid
                LEFT JOIN public.tblsetupsdetail des_fallback 
                    ON e.dsgid = des_fallback.sdlid
                WHERE COALESCE(e.Active, 1) = 1";

            // 1. Access Level Filters
            if (!string.IsNullOrEmpty(filters.DocumentTypeCode))
            {
                whereConditions.Add("ual.DocumentTypeCode = @DocCode");
                parameters.Add("@DocCode", filters.DocumentTypeCode);
            }
            if (!string.IsNullOrEmpty(filters.DivisionCode))
            {
                whereConditions.Add("ual.DivisionCode = @DivCode");
                parameters.Add("@DivCode", filters.DivisionCode);
            }
            if (!string.IsNullOrEmpty(filters.DepartmentCode))
            {
                whereConditions.Add("ual.DepartmentCode = @DeptCode");
                parameters.Add("@DeptCode", filters.DepartmentCode);
            }
            if (!string.IsNullOrEmpty(filters.SubDepartmentCode))
            {
                whereConditions.Add("ual.SubDepartmentCode = @SubDeptCode");
                parameters.Add("@SubDeptCode", filters.SubDepartmentCode);
            }
            if (!string.IsNullOrEmpty(filters.BusinessDomainCode))
            {
                whereConditions.Add("ual.BusinessDomainCode = @BusDomainCode");
                parameters.Add("@BusDomainCode", filters.BusinessDomainCode);
            }

            // 2. Designation Filter (Match against integer dsgid)
            if (filters.DesignationCodes != null && filters.DesignationCodes.Any())
            {
                var designationParams = new List<string>();
                int paramIndex = 0;

                foreach (var codeStr in filters.DesignationCodes)
                {
                    // Guard against [0] values when UI dropdown is cleared
                    if (int.TryParse(codeStr, out int dsgId) && dsgId > 0)
                    {
                        var paramName = $"@DesignationId{paramIndex}";
                        designationParams.Add(paramName);
                        parameters.Add(paramName, dsgId);
                        paramIndex++;
                    }
                }

                if (designationParams.Any())
                {
                    string paramJoin = string.Join(",", designationParams);
                    whereConditions.Add($"(e.dsgid IN ({paramJoin}) OR ejp.dsgid IN ({paramJoin}) OR ejp.ddsgid IN ({paramJoin}) OR ejp.inddsgid IN ({paramJoin}))");
                }
            }

            // 3. Role Filter (Specific to Job Profile)
            if (filters.Roles != null && filters.Roles.Any())
            {
                // Guard against [0] values when UI dropdown is cleared
                var roleList = filters.Roles.Where(r => r > 0).ToList();
                var roleParams = new List<string>();

                for (int i = 0; i < roleList.Count; i++)
                {
                    var paramName = $"@Role{i}";
                    roleParams.Add(paramName);
                    parameters.Add(paramName, roleList[i]);
                }

                if (roleParams.Any())
                {
                    whereConditions.Add($"(ejp.roleid IN ({string.Join(",", roleParams)}) OR ejp.droleid IN ({string.Join(",", roleParams)}) OR ejp.indroleid IN ({string.Join(",", roleParams)}))");
                }
            }

            // 4. Direct Employee Selection (Fallback if frontend passes explicit users)
            if (filters.EmployeeCodes != null && filters.EmployeeCodes.Any())
            {
                var employeeCodeList = filters.EmployeeCodes.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                var employeeCodeParams = new List<string>();

                for (int i = 0; i < employeeCodeList.Count; i++)
                {
                    var paramName = $"@EmployeeCode{i}";
                    employeeCodeParams.Add(paramName);
                    parameters.Add(paramName, employeeCodeList[i]);
                }

                if (employeeCodeParams.Any())
                    whereConditions.Add($"TRIM(e.EmpCode) IN ({string.Join(",", employeeCodeParams)})");
            }

            if (whereConditions.Any())
                baseQuery += " AND " + string.Join(" AND ", whereConditions);

            baseQuery += " ORDER BY EmployeeName";

            var resultList = await _dapperService.QueryAsync<UserReadDto>(baseQuery, parameters);

            if (resultList == null || !resultList.Any())
                throw new CustomException("No users found matching the criteria", 404);

            return resultList;
        }
        catch (Exception)
        {
            throw;
        }
    }
     
}
