using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentApprovalComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DocumentApprovalComponent(
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


    public async Task<DocumentApprovalReadDto> CreateAsync(DocumentApprovalCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("DocumentApproval code is required.", 400);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentApprovals
            WHERE Id = '{input.Id}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentApproval already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DocumentApprovals
            (   CompanyId,
                DocumentVersionId,
                WorkflowStepId,
                ApproverUserId,
                Status,
                Observation,
                ActionDate,
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
                '{input.DocumentVersionId}',
                '{input.WorkflowStepId}',
                '{input.ApproverUserId}',
                '{input.Status}',
                '{input.Observation}',
                '{input.ActionDate}',
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
                        SELECT da.*, c.Id AS CompanyId, c.Name AS Company,div.*
                        FROM DocumentApprovals da 
                        LEFT JOIN Companies c
                        ON da.CompanyId = c.Id
            WHERE da.Id = {newId} da.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentApprovalReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentVersionId = row.Field<int>("DocumentVersionId"),
                WorkflowStepId = row.Field<int>("WorkflowStepId"),
                ApproverUserId = row.Field<int>("ApproverUserId"),
                Status = row.Field<int>("Status"),
                Observation = row.Field<string>("Observation"),
                ActionDate = row.Field<DateTime>("ActionDate").ToString("yyyy-MM-dd HH:mm:ss"),
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
                FROM DocumentApprovals
                WHERE Id = {code} AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentApproval not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentApprovals
                SET IsDeleted = True,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentApprovalReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE da.IsDeleted = False AND da.CompanyId = " + CompanyId + @" AND da.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(da.Name) LIKE '%{search}%'
                    OR UPPER(da.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "da.Name",
                "CODE" => "da.Id",
                "CREATEDAT" => "da.CreatedAt",
                "CREATEDBY" => "da.CreatedBy",
                "LASTMODIFIEDAT" => "da.LastModifiedAt",
                "LASTMODIFIEDBY" => "da.LastModifiedBy",
                "ISACTIVE" => "da.IsActive",
                _ => "da.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT da.*, c.Id AS CompanyId, c.Name AS Company,div.*
                        FROM DocumentApprovals da 
                        LEFT JOIN Companies c
                        ON da.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentApprovals da
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentApprovalReadDto>
                {
                    Items = new List<DocumentApprovalReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentApprovalReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentVersionId = row.Table.Columns.Contains("DocumentVersionId") ? row.Field<int>("DocumentVersionId") : 0,
                    WorkflowStepId = row.Table.Columns.Contains("WorkflowStepId") ? row.Field<int>("WorkflowStepId") : 0,
                    ApproverUserId = row.Table.Columns.Contains("ApproverUserId") ? row.Field<int>("ApproverUserId") : 0,
                    Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                    Observation = row.Table.Columns.Contains("Observation") ? row.Field<string>("Observation") : string.Empty,
                    ActionDate = row.Table.Columns.Contains("ActionDate") ? row.Field<DateTime>("ActionDate").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
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

            return new PaginationResult<DocumentApprovalReadDto>
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
            SELECT Id, Name
            FROM DocumentApprovals
            WHERE IsActive = True
              AND IsDeleted = False AND CompanyId = {CompanyId}
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


    public async Task<DocumentApprovalReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                        SELECT da.*, c.Id AS CompanyId, c.Name AS Company,div.*
                        FROM DocumentApprovals da 
                        LEFT JOIN Companies c
                        ON da.CompanyId = c.Id
                WHERE da.Id = {code} AND da.CompanyId = {CompanyId}
                  AND da.IsActive = True
                  AND da.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentApproval not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentApprovalReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentVersionId = row.Field<int>("DocumentVersionId"),
                WorkflowStepId = row.Field<int>("WorkflowStepId"),
                ApproverUserId = row.Field<int>("ApproverUserId"),
                Status = row.Field<int>("Status"),
                Observation = row.Field<string>("Observation"),
                ActionDate = row.Field<DateTime>("ActionDate").ToString("yyyy-MM-dd HH:mm:ss"),
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


    public async Task<DocumentApprovalReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                        SELECT da.*, c.Id AS CompanyId, c.Name AS Company,div.*
                        FROM DocumentApprovals da 
                        LEFT JOIN Companies c
                        ON da.CompanyId = c.Id
                WHERE da.Division = {dCode} da.CompanyId = {CompanyId}
                  AND da.IsActive = True
                  AND da.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentApproval not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentApprovalReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentVersionId = row.Field<int>("DocumentVersionId"),
                WorkflowStepId = row.Field<int>("WorkflowStepId"),
                ApproverUserId = row.Field<int>("ApproverUserId"),
                Status = row.Field<int>("Status"),
                Observation = row.Field<string>("Observation"),
                ActionDate = row.Field<DateTime>("ActionDate").ToString("yyyy-MM-dd HH:mm:ss"),
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


    public async Task<DocumentApprovalReadDto> UpdateAsync(DocumentApprovalUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentApprovals
            WHERE Id = '{input.Id}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentApproval not found", 404);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentApprovals
            SET 
                DocumentVersionId = '{input.DocumentVersionId}',
                WorkflowStepId = '{input.WorkflowStepId}',
                ApproverUserId = '{input.ApproverUserId}',
                Status = '{input.Status}',
                Observation = '{input.Observation}',
                ActionDate = '{input.ActionDate}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = '{input.Id}' AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                        SELECT da.*, c.Id AS CompanyId, c.Name AS Company,div.*
                        FROM DocumentApprovals da 
                        LEFT JOIN Companies c
                        ON da.CompanyId = c.Id
            WHERE da.Id = '{input.Id}' da.CompanyId ={CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentApprovalReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentVersionId = row.Field<int>("DocumentVersionId"),
                WorkflowStepId = row.Field<int>("WorkflowStepId"),
                ApproverUserId = row.Field<int>("ApproverUserId"),
                Status = row.Field<int>("Status"),
                Observation = row.Field<string>("Observation"),
                ActionDate = row.Field<DateTime>("ActionDate").ToString("yyyy-MM-dd HH:mm:ss"),
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
