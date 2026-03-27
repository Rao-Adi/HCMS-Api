using HCMS_Api.Common;
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
        string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        _dataservice.BeginProcess(connectionString);

    }


    public async Task<DocumentTrainingReadDto> CreateAsync(DocumentTrainingCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id <0)
                throw new CustomException("DocumentTraining code is required.", 400);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTraining
            WHERE (Id = '{input.Id}' 
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
                DocumentId,
                TrainingProofURL,
                AssessmentScore,
                ValidationStatus,
                ReadyForAuthorization,
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
                '{input.DocumentId}',
                '{input.TrainingMode}',
                '{input.DocumentId}',
                '{input.TrainingProofURL}',
                '{input.AssessmentScore}', 
                '{input.ValidationStatus}', 
                '{input.ReadyForAuthorization}', 
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
            SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
            FROM DocumentTraining dt
            LEFT JOIN Companies c
            ON d.CompanyId = c.Id
            WHERE dt.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentTrainingReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
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
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM DocumentTraining
                WHERE Id = {id}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentTraining not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentTraining
                SET IsDeleted = False
                WHERE Id = {id}";

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
            var whereClause = @"
                WHERE dt.IsDeleted = False 
                  AND dt.IsActive = " + (input.IsActive ? "True" : "False");

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
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentId = row.Table.Columns.Contains("DocumentId") ? row.Field<int>("DocumentId") : 0,
                    TrainingMode = row.Table.Columns.Contains("TrainingMode") ? row.Field<int>("TrainingMode") : 0,
                    TrainingProofURL = row.Table.Columns.Contains("TrainingProofURL") ? row.Field<string>("TrainingProofURL") : string.Empty,
                    AssessmentScore = row.Table.Columns.Contains("AssessmentScore") ? row.Field<decimal>("AssessmentScore") : 0,
                    ValidationStatus = row.Table.Columns.Contains("ValidationStatus") ? row.Field<int>("ValidationStatus") : 0,
                    ReadyForAuthorization = row.Table.Columns.Contains("ReadyForAuthorization") ? row.Field<bool>("AssessmentScore") : false,
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
            FROM DocumentTraining
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


    public async Task<DocumentTrainingReadDto> GetByCodeAsync(int id)
    {
        try
        {
            string query = $@"
                SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                        FROM DocumentTraining dt
                        LEFT JOIN Companies c
                        ON d.CompanyId = c.Id
                WHERE dt.Id = {id}
                  AND dt.IsActive = True
                  AND dt.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentTraining not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentTrainingReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
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
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id <0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTraining
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentTraining not found", 200);

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
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                    SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM DocumentTraining dt
                    LEFT JOIN Companies c
                    ON d.CompanyId = c.Id
            WHERE dt.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentTrainingReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
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

    public async Task<TrainingAssessmentResultDto> GetTrainingAssessmentDetailsAsync(int documentId, int companyId)
    {
        try
        {
            string query = @"
                SELECT 
                    u.EmployeeName,
                    u.EmployeeCode,
                    dut.TrainingStatus,
                    dut.AssessmentScore,
                    dut.TrainingProofUrl
                FROM DocumentUserTraining dut
                JOIN Users u ON u.Id = dut.UserId
                WHERE dut.DocumentId = @DocumentId 
                  AND dut.CompanyId = @CompanyId
                  AND dut.IsDeleted = FALSE";

            var userScores = (await _common.QueryAsync<TrainingUserScoreDto>(query, new { DocumentId = documentId, CompanyId = companyId })).ToList();

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

    public async Task<bool> AcknowledgeAndSendForAuthorizationAsync(int documentId, int companyId, string clientIp)
    {
        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            var user = _utilities.GetCurrentUserMap(clientIp);
            var userId = user.UserID;

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
            
            await _common.ExecuteAsync(updateQuery, new { DocumentId = documentId, CompanyId = companyId, UserId = userId }, tx);

            // 2. Transition Document State to 'AuthorizationPending' queue for Authorizer
            string stateQuery = @"
                INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, ChangedAt)
                SELECT @CompanyId, @DocumentId, 
                       (SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC LIMIT 1),
                       (SELECT Id FROM DocumentStates WHERE Code = 'AuthorizationPending' LIMIT 1),
                       @UserId, NOW()";
            
            await _common.ExecuteAsync(stateQuery, new { DocumentId = documentId, CompanyId = companyId, UserId = userId }, tx);

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
}
