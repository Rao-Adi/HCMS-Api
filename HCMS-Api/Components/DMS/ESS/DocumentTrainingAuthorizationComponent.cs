using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentTrainingAuthorizationComponent
{
  
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;

    public DocumentTrainingAuthorizationComponent(
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

    public async Task<DocumentTrainingAuthorizationReadDto> CreateAsync(DocumentTrainingAuthorizationCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // A policy can't be bound to a DocumentType that's invalid or deleted -- without this,
            // CreateAsync would happily insert a row that every read API then hides (they all
            // exclude rows whose DocumentType is deleted), so the user gets no feedback at all
            // about why their new policy never shows up anywhere.
            string docTypeCheckQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTypes
            WHERE Code = '{input.DocumentTypeCode?.Replace("'", "''")}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int docTypeExists = Convert.ToInt32(_common.ExecuteScalarQuery(docTypeCheckQuery));

            if (docTypeExists == 0)
                throw new CustomException("Selected Document Type is invalid or has been deleted.", 400);

            // Check duplicate by DocumentTypeCode
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTrainingAuthorizations
            WHERE DocumentTypeCode = '{input.DocumentTypeCode?.Replace("'", "''")}' AND CompanyId = {CompanyId}
              AND IsActive = TRUE AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Authorization Policy already exists for this Document Type", 409);

            // Format parameters to handle nulls properly
            string userIdVal = string.IsNullOrWhiteSpace(input.AuthorizingUserId) ? "NULL" : $"'{input.AuthorizingUserId.Replace("'", "''")}'";

            // Insert
            string insertQuery = $@"
            INSERT INTO DocumentTrainingAuthorizations
            (   CompanyId,
                DocumentTypeCode,
                AuthorizationRequired,
                AuthorizingUserId,
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
                '{input.DocumentTypeCode?.Replace("'", "''")}',
                {(input.AuthorizationRequired ? "TRUE" : "FALSE")},
                {userIdVal},
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
                FROM DocumentTrainingAuthorizations
                WHERE Id = {id} AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Authorization Policy not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentTrainingAuthorizations
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

    public async Task<PaginationResult<DocumentTrainingAuthorizationReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // dt.Id IS NULL tolerates a DocumentTypeCode that never resolved to a real row at all
            // (a different, pre-existing data problem) -- only a DocumentType that resolves AND is
            // actually deleted should hide the policy row that references it.
            var whereClause = @"
                WHERE a.IsDeleted = False AND a.CompanyId = " + CompanyId + @"
                  AND a.IsActive = " + (input.IsActive ? "True" : "False") + @"
                  AND (dt.Id IS NULL OR dt.IsDeleted = False)";

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
                 SELECT a.*, c.Id AS CompanyId, c.Name AS Company, dt.Name AS DocumentType, 
                LTRIM(RTRIM(COALESCE(e1.firstname, '') || ' ' || COALESCE(e1.midname, '') || ' ' || COALESCE(e1.lastname, ''))) AS AuthorizingUser,
                 -- 🔹 Audit Fields
                  COALESCE(e.EmployeeName, a.CreatedBy::text) AS CreatedByName,
 
                  COALESCE(m.EmployeeName, a.LastModifiedBy::text) AS LastModifiedByName
                FROM DocumentTrainingAuthorizations a 
                LEFT JOIN DocumentTypes dt ON a.DocumentTypeCode = dt.Code  
				LEFT JOIN tblEmployee e1 ON LTRIM(a.AuthorizingUserId::text, '0') = LTRIM(e1.empcode::text, '0') AND e1.CompanyId = a.CompanyId
                LEFT JOIN Companies c
                        ON a.CompanyId = c.Id
                    -- 🔹 Created By Employee
                 LEFT JOIN Vw_EmployeeNames e
                     ON e.CleanEmpCode = LTRIM(a.CreatedBy::text, '0')

                 -- 🔹 Last Modified By Employee
                 LEFT JOIN Vw_EmployeeNames m 
                     ON m.CleanEmpCode = LTRIM(a.LastModifiedBy::text, '0')
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                SELECT COUNT(1)
                FROM DocumentTrainingAuthorizations a
                LEFT JOIN DocumentTypes dt ON a.DocumentTypeCode = dt.Code
                {whereClause};
            ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable policiesTable = ds.Tables[0];
            DataTable countTable = ds.Tables[1];

            if (policiesTable == null || policiesTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentTrainingAuthorizationReadDto>
                {
                    Items = new List<DocumentTrainingAuthorizationReadDto>(),
                    TotalCount = 0
                };
            }

            var policies = policiesTable.AsEnumerable()
                .Select(row => new DocumentTrainingAuthorizationReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                    AuthorizationRequired = row.Table.Columns.Contains("AuthorizationRequired") && row.Field<bool?>("AuthorizationRequired") == true, 
                    AuthorizingUserId = row.Table.Columns.Contains("AuthorizingUserId") ? row.Field<string>("AuthorizingUserId") : string.Empty, 
                    AuthorizingUser = row.Table.Columns.Contains("AuthorizingUser") ? row.Field<string>("AuthorizingUser") : string.Empty,
                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    CreatedByName = row.Table.Columns.Contains("CreatedByName") ? row.Field<string>("CreatedByName") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                    LastModifiedByName = row.Table.Columns.Contains("LastModifiedByName") ? row.Field<string>("LastModifiedByName") : string.Empty,
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<DocumentTrainingAuthorizationReadDto>
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

    public async Task<DocumentTrainingAuthorizationReadDto> GetByIdAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT a.*, c.Id AS CompanyId, c.Name AS Company, dt.Name AS DocumentType, 
                LTRIM(RTRIM(COALESCE(e1.firstname, '') || ' ' || COALESCE(e1.midname, '') || ' ' || COALESCE(e1.lastname, ''))) AS AuthorizingUser,
                 -- 🔹 Audit Fields
                  COALESCE(e.EmployeeName, a.CreatedBy::text) AS CreatedByName,
 
                  COALESCE(m.EmployeeName, a.LastModifiedBy::text) AS LastModifiedByName
                FROM DocumentTrainingAuthorizations a 
                LEFT JOIN DocumentTypes dt ON a.DocumentTypeCode = dt.Code  
				LEFT JOIN tblEmployee e1 ON LTRIM(a.AuthorizingUserId::text, '0') = LTRIM(e1.empcode::text, '0') AND e1.CompanyId = a.CompanyId
                LEFT JOIN Companies c
                        ON a.CompanyId = c.Id
                    -- 🔹 Created By Employee
                 LEFT JOIN Vw_EmployeeNames e
                     ON e.CleanEmpCode = LTRIM(a.CreatedBy::text, '0')

                 -- 🔹 Last Modified By Employee
                 LEFT JOIN Vw_EmployeeNames m 
                     ON m.CleanEmpCode = LTRIM(a.LastModifiedBy::text, '0')
                WHERE a.Id = {id} AND a.CompanyId = {CompanyId}
                  AND a.IsActive = True
                  AND a.IsDeleted = False
                  AND (dt.Id IS NULL OR dt.IsDeleted = False)";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Authorization Policy not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentTrainingAuthorizationReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                DocumentType = row.Field<string>("DocumentType"),
                AuthorizationRequired = row.Field<bool>("AuthorizationRequired"), 
                AuthorizingUserId = row.Field<string>("AuthorizingUserId"), 
                AuthorizingUser = row.Field<string>("AuthorizingUser"),
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

    public async Task<DocumentTrainingAuthorizationReadDto> UpdateAsync(DocumentTrainingAuthorizationUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <= 0)
                throw new CustomException("Invalid Id.", 404);

            // Check existence
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTrainingAuthorizations
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Authorization Policy not found", 404);

            // Same reasoning as CreateAsync -- don't allow (re)binding to a deleted DocumentType.
            string docTypeCheckQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTypes
            WHERE Code = '{input.DocumentTypeCode?.Replace("'", "''")}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int docTypeExists = Convert.ToInt32(_common.ExecuteScalarQuery(docTypeCheckQuery));

            if (docTypeExists == 0)
                throw new CustomException("Selected Document Type is invalid or has been deleted.", 400);

            // Format parameters to handle nulls properly
            string userIdVal = string.IsNullOrWhiteSpace(input.AuthorizingUserId) ? "NULL" : $"'{input.AuthorizingUserId.Replace("'", "''")}'";

            // Update
            string updateQuery = $@"
            UPDATE DocumentTrainingAuthorizations
            SET
                DocumentTypeCode = '{input.DocumentTypeCode?.Replace("'", "''")}',
                AuthorizationRequired = {(input.AuthorizationRequired ? "TRUE" : "FALSE")},
                AuthorizingUserId = {userIdVal},
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
