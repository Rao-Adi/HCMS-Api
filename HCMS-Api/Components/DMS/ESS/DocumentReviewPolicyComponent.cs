using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentReviewPolicyComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;

    public DocumentReviewPolicyComponent(
        DMSUtilities utilities,
        DMSDataServices dataservice,
        IConfiguration configuration,
        ClientContextService clientContextService,
        IDMSDapperDataService dapper,
        IHttpContextAccessor http,
        DMSCommon common)
    {
        _http = http;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common;
    }

    public async Task<DocumentReviewPolicyReadDto> CreateAsync(DocumentReviewPolicyCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check duplicate by DocumentTypeCode
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentReviewPolicies
            WHERE DocumentTypeCode = '{input.DocumentTypeCode}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentReviewPolicy already exists for this Document Type", 409);

            // Insert
            string insertQuery = $@"
            INSERT INTO DocumentReviewPolicies
            (   CompanyId,
                DocumentTypeCode,
                ReviewPeriodYears,
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
                '{input.DocumentTypeCode}',
                {input.ReviewPeriodYears},
                TRUE,
                FALSE,
                NOW(),
                '{empCode.Replace("'", "''")}',
                NOW(),
                '{empCode.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            return await GetByIdAsync(newId);
        }
        catch
        {
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
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
                FROM DocumentReviewPolicies
                WHERE Id = {id} AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentReviewPolicy not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentReviewPolicies
                SET IsDeleted = True,
                    IsActive = False,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Id = {id} AND CompanyId = {CompanyId}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<PaginationResult<DocumentReviewPolicyReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE a.IsDeleted = False AND a.CompanyId = " + CompanyId + @"
                  AND a.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(a.DocumentTypeCode) LIKE '%{search}%'
                    OR UPPER(dt.Name) LIKE '%{search}%'
                )";
            }

            // Sorting
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTTYPECODE" => "a.DocumentTypeCode",
                "DOCUMENTTYPE" => "dt.Name",
                "ID" => "a.Id",
                "ISACTIVE" => "a.IsActive",
                "CREATEDAT" => "a.CreatedAt",
                "CREATEDBY" => "a.CreatedBy",
                "LASTMODIFIEDAT" => "a.LastModifiedAt",
                "LASTMODIFIEDBY" => "a.LastModifiedBy",
                _ => "a.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                SELECT a.*, c.Id AS CompanyId, c.Name AS Company, dt.Name AS DocumentType
                FROM DocumentReviewPolicies a
                LEFT JOIN Companies c ON a.CompanyId = c.Id
                LEFT JOIN DocumentTypes dt ON a.DocumentTypeCode = dt.Code
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                SELECT COUNT(1)
                FROM DocumentReviewPolicies a
                LEFT JOIN DocumentTypes dt ON a.DocumentTypeCode = dt.Code
                {whereClause};
            ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable policiesTable = ds.Tables[0];
            DataTable countTable = ds.Tables[1];

            if (policiesTable == null || policiesTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentReviewPolicyReadDto>
                {
                    Items = new List<DocumentReviewPolicyReadDto>(),
                    TotalCount = 0
                };
            }

            var policies = policiesTable.AsEnumerable()
                .Select(row => new DocumentReviewPolicyReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                    ReviewPeriodYears = row.Table.Columns.Contains("ReviewPeriodYears") ? row.Field<int>("ReviewPeriodYears") : 0,
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

            return new PaginationResult<DocumentReviewPolicyReadDto>
            {
                Items = policies,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<DocumentReviewPolicyReadDto> GetByIdAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT a.*, c.Id AS CompanyId, c.Name AS Company, dt.Name AS DocumentType
                FROM DocumentReviewPolicies a
                LEFT JOIN Companies c ON a.CompanyId = c.Id
                LEFT JOIN DocumentTypes dt ON a.DocumentTypeCode = dt.Code
                WHERE a.Id = {id} AND a.CompanyId = {CompanyId}
                  AND a.IsActive = True
                  AND a.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentReviewPolicy not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentReviewPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                DocumentType = row.Field<string>("DocumentType"),
                ReviewPeriodYears = row.Field<int>("ReviewPeriodYears"),
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

    public async Task<DocumentReviewPolicyReadDto> UpdateAsync(DocumentReviewPolicyUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <= 0)
                throw new CustomException("Invalid Id.", 400);

            // Check existence
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentReviewPolicies
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentReviewPolicy not found", 404);

            // Update
            string updateQuery = $@"
            UPDATE DocumentReviewPolicies
            SET 
                DocumentTypeCode = '{input.DocumentTypeCode}',
                ReviewPeriodYears = {input.ReviewPeriodYears},
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            return await GetByIdAsync(input.Id);
        }
        catch
        {
            throw;
        }
    }
}