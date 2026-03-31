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

            // FSD UC-16 Validation: Remarks are mandatory.
            if (string.IsNullOrWhiteSpace(input.Remarks))
            {
                throw new CustomException("Remarks field is mandatory.", 400);
            }

            // UC-16 Business Rule: Cannot transfer responsibilities to self.
            if (!string.IsNullOrWhiteSpace(input.EmployeeFrom) && input.EmployeeFrom.Equals(input.EmployeeTo, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("Cannot transfer responsibilities to self.", 400);
            }

            // FSD UC-16 Post-condition: Route to Division Head for approval.
            var empDetails = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT DivisionCode 
                FROM Users 
                WHERE EmployeeCode = @EmpCode AND IsDeleted = FALSE", new { EmpCode = input.EmployeeFrom });
            
            if (empDetails == null || string.IsNullOrWhiteSpace(empDetails.divisioncode))
            {
                throw new CustomException("Cannot determine the division for the 'Employee From'.", 400);
            }

            // Route to Division Head via TransferWorkflowPolicies
            var policy = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT ApprovalUserId 
                FROM TransferWorkflowPolicies 
                WHERE DivisionCode = @DivCode AND IsActive = TRUE AND IsDeleted = FALSE", new { DivCode = empDetails?.divisioncode });

            int approverId = policy?.approvaluserid ?? 0;
            if (approverId == 0)
            {
                // UC-18 Default Routing: Automatically route to default generic Division Head role
                var defaultDivHead = await _common.QueryFirstOrDefaultAsync<int?>(@"
                    SELECT u.Id 
                    FROM Users u
                    JOIN UserRoles ur ON u.Id = ur.UserId
                    JOIN Roles r ON ur.RoleId = r.Id
                    WHERE u.DivisionCode = @DivCode AND r.Name = 'Division Head' AND u.IsDeleted = FALSE AND u.IsActive = TRUE LIMIT 1", 
                    new { DivCode = empDetails?.divisioncode });

                approverId = defaultDivHead ?? 0;

                if (approverId == 0)
                    throw new CustomException("Approval routing policy not found, and no default Division Head could be identified.", 400);
            }

            // Attachment handling
            string? documentUrl = null;
            if (input.Attachment != null && input.Attachment.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "responsibility-transfers");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileExtension = Path.GetExtension(input.Attachment.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExtension}"; // Unique filename
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await input.Attachment.CopyToAsync(stream);
                }
                documentUrl = $"/uploads/responsibility-transfers/{fileName}";
            }

            // FSD UC-16 Extension: Handle Permanent Transfer
            DateTime? effectiveDateTo = input.EffectiveDateTo;
            if (input.PermanentTransfer)
            {
                effectiveDateTo = null;
            }

            // Parameterized INSERT query to prevent SQL Injection
            string insertQuery = @"
            INSERT INTO ResponsibilityTransfers
            (   CompanyId, EmployeeFrom, EmployeeTo, ReasonForTransfer, EffectiveDateFrom, 
                EffectiveDateTo, PermanentTransfer, Attachment, Remarks, Status, 
                ApproverId, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, 
                LastModifiedBy
            )
            VALUES
            (   @CompanyId, @EmployeeFrom, @EmployeeTo, @ReasonForTransfer, @EffectiveDateFrom, 
                @EffectiveDateTo, @PermanentTransfer, @Attachment, @Remarks, 1, 
                @ApproverId, TRUE, FALSE, NOW(), @UserId, 
                NOW(), @UserId
            )
            RETURNING Id;";

            var insertParams = new
            {
                input.CompanyId,
                input.EmployeeFrom,
                input.EmployeeTo,
                input.ReasonForTransfer,
                input.EffectiveDateFrom,
                EffectiveDateTo = effectiveDateTo,
                input.PermanentTransfer,
                Attachment = documentUrl,
                input.Remarks,
                ApproverId = approverId,
                UserId = userId
            };

            int newId = await _common.ExecuteScalarAsync<int>(insertQuery, insertParams);

            // Parameterized SELECT query
            string selectQuery = @"
            SELECT rt.*, c.Name AS Company, uf.EmployeeName AS EmployeeFromName, ut.EmployeeName AS EmployeeToName
            FROM ResponsibilityTransfers rt
            LEFT JOIN Companies c
            ON rt.CompanyId = c.Id
            LEFT JOIN Users uf ON rt.EmployeeFrom = uf.EmployeeCode
            LEFT JOIN Users ut ON rt.EmployeeTo = ut.EmployeeCode
            WHERE rt.Id = @Id";

            var newRecord = await _common.QueryFirstOrDefaultAsync<dynamic>(selectQuery, new { Id = newId });

            if (newRecord == null)
                throw new Exception("Failed to fetch created responsibility transfer request.");

            // Map dynamic object to DTO
            return new ResponsibilityTransferReadDto
            {
                Id = newRecord.id,
                CompanyId = newRecord.companyid,
                Company = newRecord.company,
                EmployeeFrom = newRecord.employeefrom,
                EmployeeTo = newRecord.employeeto,
                EmployeeFromName = newRecord.employeefromname,
                EmployeeToName = newRecord.employeetoname,
                ReasonForTransfer = newRecord.reasonfortransfer,
                EffectiveDateFrom = newRecord.effectivedatefrom,
                EffectiveDateTo = newRecord.effectivedateto ?? null, // Handle nullable DateOnly
                PermanentTransfer = newRecord.permanenttransfer ?? false, // Handle nullable bool
                Attachment = newRecord.attachment,
                Remarks = newRecord.remarks,
                Status = newRecord.status,
                ApproverId = newRecord.approverid,
                Observation = newRecord.observation,
                ActionDate = newRecord.actiondate,
                IsDeleted = newRecord.isdeleted,
                IsActive = newRecord.isactive,
                CreatedAt = newRecord.createdat.ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = newRecord.createdby,
                LastModifiedAt = newRecord.lastmodifiedat.ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = newRecord.lastmodifiedby
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


    public async Task<PaginationResult<ResponsibilityTransferReadDto>> GetAllAsync(GetResponsibilityTransferByStatusDto input)
    {
        try
        {
            var whereClause = @"
                WHERE rt.IsDeleted = False 
                  AND rt.IsActive = " + (input.IsActive ? "True" : "False");

            // Add status filter
            whereClause += $" AND rt.Status = {input.StatusId}";

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(rt.EmployeeFrom) LIKE '%{search}%'
                    OR UPPER(rt.EmployeeTo) LIKE '%{search}%'
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
                         SELECT rt.*, c.Id AS CompanyId, c.Name AS Company, uf.EmployeeName AS EmployeeFromName, ut.EmployeeName AS EmployeeToName
                            FROM ResponsibilityTransfers rt
                            LEFT JOIN Companies c
                            ON rt.CompanyId = c.Id
                            LEFT JOIN Users uf ON rt.EmployeeFrom = uf.EmployeeCode
                            LEFT JOIN Users ut ON rt.EmployeeTo = ut.EmployeeCode
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
                    EmployeeFromName = row.Table.Columns.Contains("EmployeeFromName") ? row.Field<string>("EmployeeFromName") : string.Empty,
                    EmployeeToName = row.Table.Columns.Contains("EmployeeToName") ? row.Field<string>("EmployeeToName") : string.Empty,
                    ReasonForTransfer = row.Table.Columns.Contains("ReasonForTransfer") ? row.Field<string>("ReasonForTransfer") : string.Empty,
                    EffectiveDateFrom = (row.Table.Columns.Contains("EffectiveDateFrom") && !row.IsNull("EffectiveDateFrom")) // Assuming DB column is DATE
                                     ? row.Field<DateOnly>("EffectiveDateFrom").ToDateTime(TimeOnly.MinValue) // Convert DateOnly to DateTime
                                     : DateTime.MinValue, // Default to min value, or handle as nullable DateTime?
                    EffectiveDateTo = (row.Table.Columns.Contains("EffectiveDateTo") && !row.IsNull("EffectiveDateTo")) // Assuming DB column is DATE
                                     ? row.Field<DateOnly>("EffectiveDateTo").ToDateTime(TimeOnly.MinValue) // Convert DateOnly to DateTime
                                     : DateTime.Now,
                    PermanentTransfer = row.Table.Columns.Contains("PermanentTransfer") ? row.Field<bool>("PermanentTransfer") : false,
                    Attachment = row.Table.Columns.Contains("Attachment") ? row.Field<string>("Attachment") : string.Empty,
                    Remarks = row.Table.Columns.Contains("Remarks") ? row.Field<string>("Remarks") : string.Empty, 
                    Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                    ApproverId = row.Table.Columns.Contains("ApproverId") ? row.Field<int>("ApproverId") : 0,
                    Observation = row.Table.Columns.Contains("Observation") ? row.Field<string>("Observation") : string.Empty,
                    ActionDate = row.Table.Columns.Contains("ActionDate") && !row.IsNull("ActionDate") ? row.Field<string>("ActionDate") : null,
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
                EffectiveDateFrom = (row.Table.Columns.Contains("EffectiveDateFrom") && !row.IsNull("EffectiveDateFrom")) // Assuming DB column is DATE
                                     ? row.Field<DateOnly>("EffectiveDateFrom").ToDateTime(TimeOnly.MinValue) // Convert DateOnly to DateTime
                                     : DateTime.MinValue,
                EffectiveDateTo = (row.Table.Columns.Contains("EffectiveDateTo") && !row.IsNull("EffectiveDateTo")) // Assuming DB column is DATE
                                     ? row.Field<DateOnly>("EffectiveDateTo").ToDateTime(TimeOnly.MinValue) // Convert DateOnly to DateTime
                                     : DateTime.Now,
                PermanentTransfer = row.Field<bool>("PermanentTransfer"),
                Attachment = row.Field<string>("Attachment"),
                Remarks = row.Field<string>("Remarks"),
                Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                ApproverId = row.Table.Columns.Contains("ApproverId") ? row.Field<int>("ApproverId") : 0,
                Observation = row.Table.Columns.Contains("Observation") ? row.Field<string>("Observation") : string.Empty,
                ActionDate = row.Table.Columns.Contains("ActionDate") && !row.IsNull("ActionDate") ? row.Field<string>("ActionDate") : null,
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
                EmployeeTo = @EmployeeTo,
                ReasonForTransfer = @ReasonForTransfer,
                EffectiveDateFrom = @EffectiveDateFrom,
                EffectiveDateTo = @EffectiveDateTo,
                PermanentTransfer = @PermanentTransfer,
                Attachment = @Attachment,
                Remarks = @Remarks, 
                IsActive = @IsActive,
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = @Id";

            var updateParams = new
            {
                input.EmployeeFrom, input.EmployeeTo, input.ReasonForTransfer, input.EffectiveDateFrom,
                input.EffectiveDateTo, input.PermanentTransfer, input.Attachment, input.Remarks,
                IsActive = input.IsActive, Id = input.Id
            };

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
                EffectiveDateFrom = (row.Table.Columns.Contains("EffectiveDateFrom") && !row.IsNull("EffectiveDateFrom")) // Assuming DB column is DATE
                                     ? row.Field<DateOnly>("EffectiveDateFrom").ToDateTime(TimeOnly.MinValue) // Convert DateOnly to DateTime
                                     : DateTime.MinValue,
                EffectiveDateTo = (row.Table.Columns.Contains("EffectiveDateTo") && !row.IsNull("EffectiveDateTo")) // Assuming DB column is DATE
                                     ? row.Field<DateOnly>("EffectiveDateTo").ToDateTime(TimeOnly.MinValue) // Convert DateOnly to DateTime
                                     : DateTime.Now,
                PermanentTransfer = row.Field<bool>("PermanentTransfer"),
                Attachment = row.Field<string>("Attachment"),
                Remarks = row.Field<string>("Remarks"),
                Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                ApproverId = row.Table.Columns.Contains("ApproverId") ? row.Field<int>("ApproverId") : 0,
                Observation = row.Table.Columns.Contains("Observation") ? row.Field<string>("Observation") : string.Empty,
                ActionDate = row.Table.Columns.Contains("ActionDate") && !row.IsNull("ActionDate") ? row.Field<string>("ActionDate") : null,
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

    public async Task<PaginationResult<dynamic>> GetMyApprovalsAsync(GetTransferApprovalsDto input)
    {
        try
        {
            var whereClause = @"
                WHERE rt.IsDeleted = FALSE 
                  AND rt.ApproverId = @ApproverId 
                  AND rt.Status = @Status";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(rt.EmployeeFrom) LIKE '%{search}%'
                    OR UPPER(rt.EmployeeTo) LIKE '%{search}%'
                    OR UPPER(rt.ReasonForTransfer) LIKE '%{search}%'
                )";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "EMPLOYEEFROM" => "rt.EmployeeFrom",
                "ACTIONDATE" => "rt.ActionDate",
                "CREATEDAT" => "rt.CreatedAt",
                _ => "rt.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT rt.*, c.Name AS Company, uf.EmployeeName AS EmployeeFromName, ut.EmployeeName AS EmployeeToName
                FROM ResponsibilityTransfers rt
                LEFT JOIN Companies c ON rt.CompanyId = c.Id
                LEFT JOIN Users uf ON rt.EmployeeFrom = uf.EmployeeCode
                LEFT JOIN Users ut ON rt.EmployeeTo = ut.EmployeeCode
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1) 
                FROM ResponsibilityTransfers rt
                {whereClause};";

            var queryParams = new { ApproverId = int.Parse(input.UserId), Status = input.Status };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> TakeActionAsync(ResponsibilityTransferActionDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(input.Observation))
                throw new CustomException("Observation is required to submit action.", 400);

            int newStatus = input.Action.ToUpper() switch
            {
                "APPROVE" => 2,
                "REJECT" => 3,
                "REVERT" => 4,
                _ => throw new CustomException("Invalid action specified.", 400)
            };

            var transfer = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT EmployeeFrom, EmployeeTo, Status FROM ResponsibilityTransfers WHERE Id = @Id FOR UPDATE;", 
                new { Id = input.TransferId }, tx);

            if (transfer == null) throw new CustomException("Transfer request not found.", 404);
            if (transfer.status != 1) throw new CustomException("This request has already been processed.", 400);

            await _common.ExecuteAsync(@"
                UPDATE ResponsibilityTransfers
                SET Status = @Status,
                    Observation = @Observation,
                    ActionDate = NOW(),
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @UserId
                WHERE Id = @Id;", 
                new { Status = newStatus, input.Observation, input.UserId, Id = input.TransferId }, tx);

            // UC-17: Workflow Transfer Logic
            if (newStatus == 2)
            {
                var empFromId = await _common.ExecuteScalarAsync<int>("SELECT Id FROM Users WHERE EmployeeCode = @Code", new { Code = transfer.employeefrom }, tx);
                var empToId = await _common.ExecuteScalarAsync<int>("SELECT Id FROM Users WHERE EmployeeCode = @Code", new { Code = transfer.employeeto }, tx);

                await _common.ExecuteAsync(@"
                    UPDATE WorkflowExecutionSteps
                    SET AssignedUserId = @EmpToId
                    WHERE AssignedUserId = @EmpFromId
                    AND Decision IS NULL
                    AND IsActive = TRUE;",
                    new { EmpToId = empToId, EmpFromId = empFromId }, tx);
            }

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

public class ResponsibilityTransferActionDto
{
    public int TransferId { get; set; }
    public string Action { get; set; } // "APPROVE", "REJECT", "REVERT"
    public string Observation { get; set; }
    public string UserId { get; set; }
}

public class GetTransferApprovalsDto : TableFiltersDto
{
    public int Status { get; set; } // 1=Pending, 2=Approved, 3=Rejected
    public string? UserId { get; set; }
}
