﻿﻿﻿﻿﻿﻿﻿using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentTrainingComponent
{

    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DocumentTrainingComponent(
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


    public async Task<DocumentTrainingReadDto> CreateAsync(DocumentTrainingCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <0)
                throw new CustomException("DocumentTraining code is required.", 400);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTraining
            WHERE Id = '{input.Id}'  AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentTraining already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DocumentTraining
            (   CompanyId,
                DocumentId,
                TrainingMode,
                TrainingProofURL,
                AssessmentScore,
                ValidationStatus,
                ReadyForAuthorization,
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
                '{input.DocumentId}',
                '{input.TrainingMode}',
                '{input.TrainingProofURL}',
                '{input.AssessmentScore}', 
                '{input.ValidationStatus}', 
                {(input.ReadyForAuthorization ? "TRUE" : "FALSE")}, 
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
            SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
            FROM DocumentTraining dt
            LEFT JOIN Companies c
            ON dt.CompanyId = c.Id
            WHERE dt.Id = {newId} AND dt.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentTrainingReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentId = row.Field<int>("DocumentId"),
                TrainingMode = row.Field<int>("TrainingMode"),
                TrainingProofURL = row.Field<string>("TrainingProofURL"),
                AssessmentScore = row.Field<decimal>("AssessmentScore"),
                ValidationStatus = row.Field<int>("ValidationStatus"),
                ReadyForAuthorization = row.Field<bool>("ReadyForAuthorization"),
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
                FROM DocumentTraining
                WHERE Id = {id} AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentTraining not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentTraining
                SET IsDeleted = True, LastModifiedAt = NOW(), LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Id = {id} AND CompanyId = {CompanyId}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentTrainingReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE dt.IsDeleted = False AND dt.CompanyId = " + CompanyId + @" AND dt.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(dt.Name) LIKE '%{search}%'
                    OR UPPER(dt.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "ID" => "dt.Id",
                "CODE" => "dt.Id",
                "ISACTIVE" => "dt.IsActive",
                "CREATEDAT" => "dt.CreatedAt",
                "CREATEDBY" => "dt.CreatedBy",
                "LASTMODIFIEDAT" => "dt.LastModifiedAt",
                "LASTMODIFIEDBY" => "dt.LastModifiedBy",
                _ => "dt.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                        FROM DocumentTraining dt
                        LEFT JOIN Companies c
                        ON dt.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentTraining dt
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentTrainingReadDto>
                {
                    Items = new List<DocumentTrainingReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentTrainingReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentId = row.Table.Columns.Contains("DocumentId") ? row.Field<int>("DocumentId") : 0,
                    TrainingMode = row.Table.Columns.Contains("TrainingMode") ? row.Field<int>("TrainingMode") : 0,
                    TrainingProofURL = row.Table.Columns.Contains("TrainingProofURL") ? row.Field<string>("TrainingProofURL") : string.Empty,
                    AssessmentScore = row.Table.Columns.Contains("AssessmentScore") ? row.Field<decimal>("AssessmentScore") : 0,
                    ValidationStatus = row.Table.Columns.Contains("ValidationStatus") ? row.Field<int>("ValidationStatus") : 0,
                    ReadyForAuthorization = row.Table.Columns.Contains("ReadyForAuthorization") && row.Field<bool?>("ReadyForAuthorization") == true,
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

            return new PaginationResult<DocumentTrainingReadDto>
            {
                Items = divisions,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }


    public async Task<IQueryable<SelectListDto>> GetAllSelectList()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = @"
            SELECT Id, Name
            FROM DocumentTraining
            WHERE IsActive = True
              AND IsDeleted = False AND CompanyId = " + CompanyId + @"
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


    public async Task<DocumentTrainingReadDto> GetByCodeAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                        FROM DocumentTraining dt
                        LEFT JOIN Companies c
                        ON dt.CompanyId = c.Id
                WHERE dt.Id = {id} AND dt.CompanyId = {CompanyId}
                  AND dt.IsActive = True
                  AND dt.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentTraining not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentTrainingReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentId = row.Field<int>("DocumentId"),
                TrainingMode = row.Field<int>("TrainingMode"),
                TrainingProofURL = row.Field<string>("TrainingProofURL"),
                AssessmentScore = row.Field<decimal>("AssessmentScore"),
                ValidationStatus = row.Field<int>("ValidationStatus"),
                ReadyForAuthorization = row.Field<bool>("ReadyForAuthorization"),
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

 

    public async Task<DocumentTrainingReadDto> UpdateAsync(DocumentTrainingUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <0)
                throw new CustomException("Invalid division code.", 404);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTraining
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentTraining not found", 404);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentTraining
            SET 
                DocumentId = '{input.DocumentId}',
                TrainingMode = '{input.TrainingMode}',
                TrainingProofURL = '{input.TrainingProofURL}',
                AssessmentScore = '{input.AssessmentScore}',
                ValidationStatus = '{input.ValidationStatus}',
                ReadyForAuthorization = '{input.ReadyForAuthorization}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = '{input.Id}' AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                    SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM DocumentTraining dt
                    LEFT JOIN Companies c
                    ON dt.CompanyId = c.Id
            WHERE dt.Id = '{input.Id}' AND dt.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentTrainingReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentId = row.Field<int>("DocumentId"),
                TrainingMode = row.Field<int>("TrainingMode"),
                TrainingProofURL = row.Field<string>("TrainingProofURL"),
                AssessmentScore = row.Field<decimal>("AssessmentScore"),
                ValidationStatus = row.Field<int>("ValidationStatus"),
                ReadyForAuthorization = row.Field<bool>("ReadyForAuthorization"),
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

    public async Task<TrainingAssessmentResultDto> GetTrainingAssessmentDetailsAsync(int documentId, int trainingMode)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId); 

            string query = @"
                SELECT 
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName,
                    dut.EmployeeCode AS EmployeeCode,
                    dut.TrainingStatus,
                    dut.AssessmentScore,
                    dut.TrainingProofUrl,
                    dut.TrainingMode,
	                    CASE 
                            WHEN dut.TrainingMode = 1 THEN 'Classroom' 
                            ELSE 'Online' 
                        END AS TrainingModeName,
                    r.name AS RoleName,
                    desig.name AS Designation,
                    div.name AS Division,
                    dep.name AS Department,
                    subd.name AS SubDepartment
                FROM DocumentUserTraining dut
                
                LEFT JOIN tblEmployee e 
                    ON e.empCode = LPAD(dut.EmployeeCode::text, 9, '0')  AND e.CompanyId = @CompanyId
                    
                LEFT JOIN public.tblempjobprofile ejp 
                    ON ejp.empid = e.empid 
                    AND COALESCE(ejp.Active, TRUE) = TRUE  AND ejp.CompanyId = @CompanyId
                    
                LEFT JOIN public.tblsetupsdetail r 
                    ON r.sdlid = ejp.roleid  AND r.CompanyId = @CompanyId
                    
                LEFT JOIN public.tblsetupsdetail desig 
                    ON desig.sdlid = ejp.dsgid  AND desig.CompanyId = @CompanyId
                    
                LEFT JOIN public.tblsetupsdetail div 
                    ON div.sdlid = e.divid AND div.CompanyId = @CompanyId
                    
                LEFT JOIN public.tblsetupsdetail dep 
                    ON dep.sdlid = e.mdptid AND dep.CompanyId = @CompanyId
                    
                LEFT JOIN public.tblsetupsdetail subd 
                    ON subd.sdlid = e.dptid AND subd.CompanyId = @CompanyId
                    
                WHERE dut.DocumentId = @DocumentId 
                  AND (@TrainingMode = 0 OR dut.TrainingMode = @TrainingMode)
                  AND dut.CompanyId = @CompanyId
                  AND dut.IsDeleted = FALSE";

            var userScores = (await _common.QueryAsync<TrainingUserScoreDto>(query, new { DocumentId = documentId, CompanyId = CompanyId , TrainingMode = trainingMode })).ToList();

            var totalAssigned = userScores.Count;
            var totalCompleted = userScores.Count(x => x.TrainingStatus == 1); // Assuming 1 = Completed
            var avgScore = totalCompleted > 0 ? userScores.Where(x => x.TrainingStatus == 1).Average(x => x.AssessmentScore) : 0;
            var participation = totalAssigned > 0 ? ((decimal)totalCompleted / totalAssigned) * 100 : 0;

            return new TrainingAssessmentResultDto
            {
                TotalAssigned = totalAssigned,
                TotalCompleted = totalCompleted,
                AverageScore = Math.Round(avgScore, 2),
                ParticipationPercentage = Math.Round(participation, 2),
                UserScores = userScores
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> AcknowledgeAndSendForAuthorizationAsync(int documentId)
    {
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        var clientIp = _clientContextService.GetClientIP();
        int CompanyId = int.Parse(_CompanyId);
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        await using var tx = await _common.BeginTransactionAsync();
        try
        { 

            // 1. Mark Document Training as Acknowledged / Ready
            string updateQuery = @"
                UPDATE DocumentTraining
                SET 
                    ReadyForAuthorization = TRUE,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @UserId
                WHERE DocumentId = @DocumentId 
                  AND CompanyId = @CompanyId
                  AND IsDeleted = FALSE";
            
            await _common.ExecuteAsync(updateQuery, new { DocumentId = documentId, CompanyId = CompanyId, UserId = empCode }, tx);

            // 2. Log Action and transition state to AUTHORIZATION_PENDING (ID=7)
            string stateQuery = @"
                INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, ChangedAt, Comments)
                SELECT @CompanyId, @DocumentId, 
                       (SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC LIMIT 1),
                       (SELECT Id FROM DocumentStates WHERE Code = 'AUTHORIZATION_PENDING'),
                       @UserId, NOW(), 'Training Acknowledged, Sent for Authorization'";
            
            await _common.ExecuteAsync(stateQuery, new { DocumentId = documentId, CompanyId = CompanyId, UserId = empCode }, tx);

            // TODO: Add notification logic here to inform the final authorizer(s) that a document is ready for their action.

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

public class TrainingAssessmentResultDto
{
    public decimal AverageScore { get; set; }
    public decimal ParticipationPercentage { get; set; }
    public int TotalAssigned { get; set; }
    public int TotalCompleted { get; set; }
    public List<TrainingUserScoreDto> UserScores { get; set; } = new();
}

public class TrainingUserScoreDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public int TrainingStatus { get; set; }
    public decimal AssessmentScore { get; set; }
    public string TrainingProofUrl { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string SubDepartment { get; set; } = string.Empty;
}
