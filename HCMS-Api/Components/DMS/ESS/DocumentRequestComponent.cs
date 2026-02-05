using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using Npgsql;
using System.Data;
using static Dapper.SqlMapper;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentRequestComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DocumentRequestComponent(
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


    public async Task<DocumentRequestReadDto> CreateAsync(DocumentRequestCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("DocumentRequests code is required.", 400);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentRequests
            WHERE Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentRequests already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DocumentRequests
            (   CompanyId,
                RequestNumber,
                RequestType,
                DocumentId,
                DocumentTypeId,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                BusinessDomainCode,
                DocumentName, 
                Justification, 
                Status,  
                CurrentStep,  
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{input.CompanyId}',
                '{input.RequestNumber}',
                '{input.RequestType}',
                '{input.DocumentId}',
                '{input.DocumentTypeId}',
                '{input.DivisionCode}', 
                '{input.DepartmentCode}', 
                '{input.SubDepartmentCode}', 
                '{input.BusinessDomainCode}', 
                '{input.DocumentName}', 
                '{input.Justification}', 
                '{input.Status}', 
                '{input.CurrentStep}', 
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
                SELECT d.*, c.Name AS Company,div.Name AS Division, dep.Name AS Department, bd.Name BusinessDomain
                FROM DocumentRequests d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
                LEFT JOIN Divisions div
                ON d.DivisionCode = div.Code
                LEFT JOIN Department dep
                ON d.DepartmentCode = dep.Code
                LEFT JOIN BusinessDomains bd
                ON d.BusinessDomainCode = bd.Code
            WHERE d.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeId = row.Field<int>("DocumentTypeId"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
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
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM DocumentRequests
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentRequests not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentRequests
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentRequestReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE d.IsDeleted = False 
                  AND d.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(d.Name) LIKE '%{search}%'
                    OR UPPER(d.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "d.Name",
                "CODE" => "d.Id",
                "ISACTIVE" => "d.IsActive",
                _ => "d.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT d.*, c.Name AS Company,div.Name AS Division, dep.Name AS Department, bd.Name BusinessDomain
                            FROM DocumentRequests d
                            LEFT JOIN Companies c
                            ON d.CompanyId = c.Id
                            LEFT JOIN Divisions div
                            ON d.DivisionCode = div.Code
                            LEFT JOIN Department dep
                            ON d.DepartmentCode = dep.Code
                            LEFT JOIN BusinessDomains bd
                            ON d.BusinessDomainCode = bd.Code
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentRequests dep
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentRequestReadDto>
                {
                    Items = new List<DocumentRequestReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentRequestReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
                    RequestType = row.Table.Columns.Contains("RequestType") ? row.Field<int>("RequestType") : 0,
                    DocumentId = row.Table.Columns.Contains("DocumentId") ? row.Field<int>("DocumentId") : 0,
                    DocumentTypeId = row.Table.Columns.Contains("DocumentTypeId") ? row.Field<int>("DocumentTypeId") : 0,

                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"),

                    Department = row.Field<string>("Department"),
                    DepartmentCode = row.Field<string>("DepartmentCode"),

                    SubDepartment = row.Field<string>("SubDepartment"),
                    SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                    BusinessDomain = row.Field<string>("BusinessDomain"),
                    BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                    DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
                    Justification = row.Table.Columns.Contains("Justification") ? row.Field<string>("Justification") : string.Empty,
                    Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                    CurrentStep = row.Table.Columns.Contains("CurrentStep") ? row.Field<int>("CurrentStep") : 0,
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

            return new PaginationResult<DocumentRequestReadDto>
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
            SELECT Id, Name
            FROM DocumentRequests
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Name";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Id"),
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


    public async Task<DocumentRequestReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT d.*, c.Name AS Company,div.Name AS Division, dep.Name AS Department, bd.Name BusinessDomain
                FROM DocumentRequests d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
                LEFT JOIN Divisions div
                ON d.DivisionCode = div.Code
                LEFT JOIN Department dep
                ON d.DepartmentCode = dep.Code
                LEFT JOIN BusinessDomains bd
                ON d.BusinessDomainCode = bd.Code
                WHERE d.Id = {code}
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentRequests not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeId = row.Field<int>("DocumentTypeId"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
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


    public async Task<DocumentRequestReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT d.*, c.Name AS Company,div.Name AS Division, dep.Name AS Department, bd.Name BusinessDomain
                FROM DocumentRequests d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
                LEFT JOIN Divisions div
                ON d.DivisionCode = div.Code
                LEFT JOIN Department dep
                ON d.DepartmentCode = dep.Code
                LEFT JOIN BusinessDomains bd
                ON d.BusinessDomainCode = bd.Code
                WHERE d.DivisionCode = {dCode}
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentRequests not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeId = row.Field<int>("DocumentTypeId"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
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


    public async Task<DocumentRequestReadDto> UpdateAsync(DocumentRequestUpdateDto input)
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
            FROM DocumentRequests
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentRequests not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentRequests
            SET 
                RequestNumber = '{input.RequestNumber}',
                RequestType = '{input.RequestType}',
                DocumentTypeId = '{input.DocumentTypeId}',
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}',
                BusinessDomainCode = '{input.BusinessDomainCode}',
                DocumentName = '{input.DocumentName}',
                Justification = '{input.Justification}',
                Status = '{input.Status}',
                CurrentStep = '{input.CurrentStep}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                SELECT d.*, c.Name AS Company,div.Name AS Division, dep.Name AS Department, bd.Name BusinessDomain
                FROM DocumentRequests d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
                LEFT JOIN Divisions div
                ON d.DivisionCode = div.Code
                LEFT JOIN Department dep
                ON d.DepartmentCode = dep.Code
                LEFT JOIN BusinessDomains bd
                ON d.BusinessDomainCode = bd.Code
            WHERE d.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeId = row.Field<int>("DocumentTypeId"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
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



    public async Task<long> SubmitDocumentRequestAsync(DocumentRequestCreateDto input)
    {
        //await using var conn = new NpgsqlConnection(_connectionString);
        //await conn.OpenAsync();

        //await using var tx = await conn.BeginTransactionAsync();
        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            //var companyId = _tenantProvider.CompanyId;
            //var userId = _currentUserProvider.UserId;
            var userId = "";
            var companyId = "";
            //------------------------------------------------
            // ✅ 1. Resolve ACTIVE Workflow Policy Version
            //------------------------------------------------

            var workflowVersionId = await _common.ExecuteScalarAsync<long?>(
            @"
                SELECT wpv.Id
                FROM WorkflowPolicyVersions wpv
                JOIN WorkflowPolicies wp 
                    ON wp.Id = wpv.WorkflowPolicyId
                WHERE wp.CompanyId = @CompanyId
                  AND wp.DocumentTypeId = @DocumentTypeId
                  AND wpv.IsActive = TRUE
                LIMIT 1;
                ",
            new
            {
                CompanyId = companyId,
                input.DocumentTypeId
            }, tx);

            if (workflowVersionId == null)
                throw new CustomException("No active workflow configured for this document type.");

            //------------------------------------------------
            // ✅ 2. Insert DocumentRequest
            //------------------------------------------------

            var requestId = await _common.ExecuteScalarAsync<long>(
            @"
                INSERT INTO DocumentRequests
                (
                    CompanyId,
                    DocumentTypeId,
                    RequestType,
                    DocumentName,
                    Justification,
                    Status,
                    CreatedBy,
                    CreatedAt
                )
                VALUES
                (
                    @CompanyId,
                    @DocumentTypeId,
                    @RequestType,
                    @DocumentName,
                    @Justification,
                    'Submitted',
                    @UserId,
                    NOW()
                )
                RETURNING Id;
                ",
            new
            {
                CompanyId = companyId,
                input.DocumentTypeId,
                input.RequestType, // Creation / Revision / Obsoletion
                input.DocumentName,
                input.Justification,
                UserId = userId
            }, tx);

            //------------------------------------------------
            // ✅ 3. Insert STATE HISTORY (CRITICAL)
            //------------------------------------------------

            await _common.ExecuteAsync(
            @"
                INSERT INTO RequestStateHistory
                (
                    CompanyId,
                    RequestId,
                    State,
                    ChangedBy
                )
                VALUES
                (
                    @CompanyId,
                    @RequestId,
                    'Submitted',
                    @UserId
                );
                ",
            new
            {
                CompanyId = companyId,
                RequestId = requestId,
                UserId = userId
            }, tx);

            //------------------------------------------------
            // ✅ 4. Start Workflow Execution
            //------------------------------------------------

            var executionId = await _common.ExecuteScalarAsync<long>(
            @"
                INSERT INTO WorkflowExecutions
                (
                    CompanyId,
                    WorkflowPolicyVersionId,
                    EntityType,
                    EntityId,
                    Status,
                    StartedBy,
                    StartedAt
                )
                VALUES
                (
                    @CompanyId,
                    @WorkflowVersionId,
                    'Request',
                    @RequestId,
                    'Running',
                    @UserId,
                    NOW()
                )
                RETURNING Id;
                ",
            new
            {
                CompanyId = companyId,
                WorkflowVersionId = workflowVersionId,
                RequestId = requestId,
                UserId = userId
            }, tx);

            //------------------------------------------------
            // ✅ 5. Insert Domain Event (Audit Power)
            //------------------------------------------------

            await _common.ExecuteAsync(
            @"
                INSERT INTO DocumentEvents
                (
                    CompanyId,
                    EventType,
                    Metadata,
                    PerformedBy
                )
                VALUES
                (
                    @CompanyId,
                    'DocumentRequestSubmitted',
                    jsonb_build_object('RequestId', @RequestId),
                    @UserId
                );
                ",
            new
            {
                CompanyId = companyId,
                RequestId = requestId,
                UserId = userId
            }, tx);

            //------------------------------------------------
            // ✅ COMMIT
            //------------------------------------------------

            await tx.CommitAsync();

            return requestId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    //public async Task<IEnumerable<WorkflowTaskDto>> GetPendingRequestStepsAsync()
    //{
    //    WorkflowExecutionSteps
    //WHERE AssignedUserId = @User
    //AND Decision IS NULL
    //}


    //public async Task ApproveWorkflowStepAsync(ApproveStepDto input)
    //{
    //    using var tx = await _common.BeginTransactionAsync();

    //    // Validate step ownership
    //    // Insert decision row
    //    // Move to next step

    //    if (finalStep)
    //    {
    //        await TransitionState(entity);
    //    }

    //    await tx.CommitAsync();
    //}

}
