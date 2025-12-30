using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

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
            if (input.Id != Guid.Empty)
                throw new CustomException("DocumentRequests code is required.", 200);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentRequests
            WHERE (Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentRequests already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DocumentRequests
            (
                RequestNumber,
                RequestType,
                DocumentId,
                DocumentTypeCode,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
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
                '{input.RequestNumber}',
                '{input.RequestType}',
                '{input.DocumentId}',
                '{input.DocumentTypeCode}',
                '{input.DivisionCode}', 
                '{input.DepartmentCode}', 
                '{input.SubDepartmentCode}', 
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
            SELECT  Id,
                    RequestNumber,
                    RequestType,
                    DocumentId,
                    DocumentTypeCode,
                    DivisionCode,
                    DepartmentCode,
                    SubDepartmentCode,
                    DocumentName, 
                    Justification, 
                    Status,  
                    CurrentStep,  
                    IsActive
            FROM Documents
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<Guid>("Id"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
                IsActive = row.Field<bool>("IsActive")
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
                FROM Documents
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Documents not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Documents
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
                WHERE dep.IsDeleted = False 
                  AND dep.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(dep.Name) LIKE '%{search}%'
                    OR UPPER(dep.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "dep.Name",
                "CODE" => "dep.Id",
                "ISACTIVE" => "dep.IsActive",
                _ => "dep.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT *
                        FROM Documents dep
                        LEFT JOIN Documents div
						ON dep.DocumentId = div.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Documents dep
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
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
                    RequestType = row.Table.Columns.Contains("RequestType") ? row.Field<int>("RequestType") : 0,
                    DocumentId = row.Table.Columns.Contains("DocumentId") ? row.Field<Guid>("DocumentId") : Guid.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("DivisionCode") : string.Empty,
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
            FROM Documents
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
                SELECT  Id,
                        RequestNumber,
                        RequestType,
                        DocumentId,
                        DocumentTypeCode,
                        DivisionCode,
                        DepartmentCode,
                        SubDepartmentCode,
                        DocumentName, 
                        Justification, 
                        Status,  
                        IsActive
                FROM Documents
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<Guid>("Id"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
                IsActive = row.Field<bool>("IsActive")
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
                SELECT  Id,
                        RequestNumber,
                        RequestType,
                        DocumentId,
                        DocumentTypeCode,
                        DivisionCode,
                        DepartmentCode,
                        SubDepartmentCode,
                        DocumentName, 
                        Justification, 
                        Status,  
                        IsActive
                FROM Documents
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<Guid>("Id"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
                IsActive = row.Field<bool>("IsActive")
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
            if (input.Id != Guid.Empty)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Documents
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Documents not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Documents
            SET 
                RequestNumber = '{input.RequestNumber}',
                RequestType = '{input.RequestType}',
                DocumentTypeCode = '{input.DocumentTypeCode}',
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}',
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
            SELECT Id, IsActive
            FROM Documents
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Field<Guid>("Id"),
                RequestNumber = row.Field<string>("RequestNumber"),
                RequestType = row.Field<int>("RequestType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                DocumentName = row.Field<string>("DocumentName"),
                Justification = row.Field<string>("Justification"),
                Status = row.Field<int>("Status"),
                CurrentStep = row.Field<int>("CurrentStep"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }

}
