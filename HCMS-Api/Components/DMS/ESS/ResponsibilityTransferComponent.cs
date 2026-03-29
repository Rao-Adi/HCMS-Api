using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class ResponsibilityTransferComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public ResponsibilityTransferComponent(
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


    public async Task<ResponsibilityTransferReadDto> CreateAsync(ResponsibilityTransferCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);


            //// Check duplicate by Id OR Name
            //string checkQuery = $@"
            //SELECT COUNT(1)
            //FROM ResponsibilityTransfers
            //WHERE EmployeeFrom = '{input.EmployeeFrom}' 
            //  AND IsDeleted = FALSE";

            //int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            //if (exists > 0)
            //    throw new CustomException("ResponsibilityTransfers already exists", 200);

            if (input.Attachment == null || input.Attachment.Length == 0)
                throw new CustomException("Document file is required", 400);

            // 2️⃣ Prepare upload path
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");

            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            // 3️⃣ Create unique filename
            var fileExtension = Path.GetExtension(input.Attachment.FileName);
            var fileName = $"{input.Attachment.FileName}.{fileExtension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            // 4️⃣ Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await input.Attachment.CopyToAsync(stream);
            }

            // 5️⃣ Generate URL (adjust domain if needed)
            var documentUrl = $"/uploads/documents/{fileName}";

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO ResponsibilityTransfers
            (   CompanyId,
                EmployeeFrom,
                EmployeeTo,
                ReasonForTransfer,
                EffectiveDateFrom,
                EffectiveDateTo,
                PermanentTransfer,
                Attachment,
                Remarks,
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
                '{input.EmployeeFrom}',
                '{input.EmployeeTo}',
                '{input.ReasonForTransfer}',
                '{input.EffectiveDateFrom}',
                '{input.EffectiveDateTo}', 
                '{input.PermanentTransfer}', 
                '{documentUrl}', 
                '{input.Remarks}', 
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
            SELECT rt.*, c.Id AS CompanyId, c.Name AS Company
            FROM ResponsibilityTransfers rt
            LEFT JOIN Companies c
            ON rt.CompanyId = c.Id
            WHERE rt.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeFrom = row.Field<string>("EmployeeFrom"),
                EmployeeTo = row.Field<string>("EmployeeTo"),
                ReasonForTransfer = row.Field<string>("ReasonForTransfer"),
                EffectiveDateFrom = row.Field<DateOnly>("EffectiveDateFrom")
                           .ToDateTime(TimeOnly.MinValue),

                EffectiveDateTo = row.Field<DateOnly>("EffectiveDateTo")
                         .ToDateTime(TimeOnly.MinValue),
                PermanentTransfer = row.Field<bool>("PermanentTransfer"),
                Attachment = row.Field<string>("Attachment"),
                Remarks = row.Field<string>("Remarks"), 
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
                FROM ResponsibilityTransfers
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE ResponsibilityTransfers
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<ResponsibilityTransferReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE rt.IsDeleted = False 
                  AND rt.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(rt.Name) LIKE '%{search}%'
                    OR UPPER(rt.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "EMPLOYEEFROM" => "rt.EmployeeFrom",
                "EMPLOYEETO" => "rt.EmployeeTo",
                "REASONFORTRANSFER" => "rt.ReasonForTransfer",
                "EFFECTIVEDATEFROM" => "rt.EffectiveDateFrom",
                "EFFECTIVEDATETO" => "rt.EffectiveDateTo",
                "REMARKS" => "rt.Remarks", 
                "ISACTIVE" => "rt.IsActive",
                _ => "rt.EMPLOYEEFROM"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                         SELECT rt.*, c.Id AS CompanyId, c.Name AS Company
                            FROM ResponsibilityTransfers rt
                            LEFT JOIN Companies c
                            ON rt.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM ResponsibilityTransfers rt
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<ResponsibilityTransferReadDto>
                {
                    Items = new List<ResponsibilityTransferReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new ResponsibilityTransferReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    EmployeeFrom = row.Table.Columns.Contains("EmployeeFrom") ? row.Field<string>("EmployeeFrom") : string.Empty,
                    EmployeeTo = row.Table.Columns.Contains("EmployeeTo") ? row.Field<string>("EmployeeTo") : string.Empty,
                    ReasonForTransfer = row.Table.Columns.Contains("ReasonForTransfer") ? row.Field<string>("ReasonForTransfer") : string.Empty,
                    EffectiveDateFrom = row.Table.Columns.Contains("EffectiveDateFrom") ? row.Field<DateTime>("EffectiveDateFrom") : DateTime.Now,
                    EffectiveDateTo = row.Table.Columns.Contains("EffectiveDateTo") ? row.Field<DateTime>("EffectiveDateTo") : DateTime.Now,
                    PermanentTransfer = row.Table.Columns.Contains("PermanentTransfer") ? row.Field<bool>("PermanentTransfer") : false,
                    Attachment = row.Table.Columns.Contains("Attachment") ? row.Field<string>("Attachment") : string.Empty,
                    Remarks = row.Table.Columns.Contains("Remarks") ? row.Field<string>("Remarks") : string.Empty, 
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

            return new PaginationResult<ResponsibilityTransferReadDto>
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

    public async Task<ResponsibilityTransferReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                 SELECT rt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM ResponsibilityTransfers rt
                    LEFT JOIN Companies c
                    ON rt.CompanyId = c.Id
                WHERE rt.Id = {code}
                  AND rt.IsActive = True
                  AND rt.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeFrom = row.Field<string>("EmployeeFrom"),
                EmployeeTo = row.Field<string>("EmployeeTo"),
                ReasonForTransfer = row.Field<string>("ReasonForTransfer"),
                EffectiveDateFrom = row.Field<DateTime>("EffectiveDateFrom"),
                EffectiveDateTo = row.Field<DateTime>("EffectiveDateTo"),
                PermanentTransfer = row.Field<bool>("PermanentTransfer"),
                Attachment = row.Field<string>("Attachment"),
                Remarks = row.Field<string>("Remarks"),
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

  
    public async Task<ResponsibilityTransferReadDto> UpdateAsync(ResponsibilityTransferUpdateDto input)
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
            FROM ResponsibilityTransfers
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE ResponsibilityTransfers
            SET 
                EmployeeFrom = '{input.EmployeeFrom}',
                EmployeeTo = '{input.EmployeeTo}',
                ReasonForTransfer = '{input.ReasonForTransfer}',
                EffectiveDateFrom = '{input.EffectiveDateFrom}',
                EffectiveDateTo = '{input.EffectiveDateTo}',
                PermanentTransfer = '{input.PermanentTransfer}',
                Attachment = '{input.Attachment}',
                Remarks = '{input.Remarks}', 
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                    SELECT rt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM ResponsibilityTransfers rt
                    LEFT JOIN Companies c
                    ON rt.CompanyId = c.Id
            WHERE rt.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeFrom = row.Field<string>("EmployeeFrom"),
                EmployeeTo = row.Field<string>("EmployeeTo"),
                ReasonForTransfer = row.Field<string>("ReasonForTransfer"),
                EffectiveDateFrom = row.Field<DateTime>("EffectiveDateFrom"),
                EffectiveDateTo = row.Field<DateTime>("EffectiveDateTo"),
                PermanentTransfer = row.Field<bool>("PermanentTransfer"),
                Attachment = row.Field<string>("Attachment"),
                Remarks = row.Field<string>("Remarks"),
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
