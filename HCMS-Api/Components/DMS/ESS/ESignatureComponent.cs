using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class ESignatureComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public ESignatureComponent(
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

    // Accepts either a full data URI ("data:image/png;base64,...") or bare base64, and returns
    // the decoded bytes plus the file extension to save it under (from the data URI's MIME type
    // when present; "png" otherwise, matching what esignature.ts's SignaturePad always exports).
    private static (byte[] Bytes, string Extension) DecodeSignatureBase64(string? signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64))
            throw new CustomException("A signature image is required.", 400);

        string extension = "png";
        string base64Payload = signatureBase64;

        var commaIndex = signatureBase64.IndexOf(',');
        if (signatureBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex > 0)
        {
            var header = signatureBase64.Substring(5, commaIndex - 5); // "image/png;base64"
            base64Payload = signatureBase64.Substring(commaIndex + 1);

            var mimeType = header.Split(';')[0]; // "image/png"
            var slashIndex = mimeType.IndexOf('/');
            if (slashIndex >= 0 && slashIndex < mimeType.Length - 1)
                extension = mimeType.Substring(slashIndex + 1).ToLowerInvariant();
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Payload);
        }
        catch (FormatException)
        {
            throw new CustomException("Signature image is not valid base64 data.", 400);
        }

        if (bytes.Length == 0)
            throw new CustomException("A signature image is required.", 400);

        return (bytes, extension);
    }

    // Same uploads-root convention as the rest of DMS (wwwroot/uploads/{documents,drafts,templates}).
    private static string SaveSignatureFile(byte[] bytes, string extension, string empCode)
    {
        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "signatures");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var safeExt = string.IsNullOrWhiteSpace(extension) ? "png" : extension;
        var fileName = $"{empCode}_{DateTime.Now:yyyyMMddHHmmssfff}.{safeExt}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        File.WriteAllBytes(filePath, bytes);

        return $"/uploads/signatures/{fileName}";
    }

    // Best-effort cleanup when a signature is replaced or deleted -- an orphaned file left on
    // disk isn't worth failing the request over, so failures here are swallowed.
    private static void TryDeletePhysicalFile(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Ignore -- see comment above.
        }
    }

    private static ESignatureReadDto MapToReadDto(IDictionary<string, object> row) => new()
    {
        Id = GetValue<int>(row, "id"),
        CompanyId = GetValue<int>(row, "companyid"),
        Company = GetValue<string>(row, "company"),
        UserId = GetValue<string>(row, "userid"),
        SignatureURL = GetValue<string>(row, "signatureurl"),
        SignatureType = GetValue<int>(row, "signaturetype"),
        IsActive = GetValue<bool>(row, "isactive"),
        IsDeleted = GetValue<bool>(row, "isdeleted"),
        CreatedAt = GetValue<DateTime?>(row, "createdat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
        CreatedBy = GetValue<string>(row, "createdby"),
        LastModifiedAt = GetValue<DateTime?>(row, "lastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
        LastModifiedBy = GetValue<string>(row, "lastmodifiedby")
    };

    public async Task<ESignatureReadDto> CreateAsync(ESignatureCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // One active (non-deleted) signature per user -- MergeDocumentTemplateAsync joins
            // ESignatures by UserId with no ORDER BY/LIMIT, so more than one row for the same
            // user would make which one gets embedded non-deterministic.
            // TRIM guards against rows saved before GetEmpCodeForHCMS trimmed its result --
            // without it, a stray-whitespace UserId already in the table would never match.
            var alreadyExists = await _common.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS(
                    SELECT 1 FROM ESignatures
                    WHERE TRIM(UserId) = TRIM(@UserId) AND CompanyId = @CompanyId AND IsDeleted = FALSE
                );", new { UserId = empCode, CompanyId });

            if (alreadyExists)
                throw new CustomException("A signature already exists for this user. Use update instead.", 409);

            var (bytes, extension) = DecodeSignatureBase64(input.SignatureBase64);
            var signatureUrl = SaveSignatureFile(bytes, extension, empCode);

            int newId = await _common.ExecuteScalarAsync<int>(@"
                INSERT INTO ESignatures
                    (CompanyId, UserId, SignatureURL, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                VALUES
                    (@CompanyId, @UserId, @SignatureURL, @IsActive, FALSE, NOW(), @EmpCode, NOW(), @EmpCode)
                RETURNING Id;",
                new
                {
                    CompanyId,
                    UserId = empCode,
                    SignatureURL = signatureUrl, 
                    input.IsActive,
                    EmpCode = empCode
                });

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
            int CompanyId = int.Parse(_CompanyId);
            var empCode = _utilities.GetEmpCodeForHCMS(_utilities.GetEmpid(_clientContextService.GetClientIP()).ToString());

            var existing = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT SignatureURL FROM ESignatures
                WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = FALSE;",
                new { Id = id, CompanyId });

            if (existing == null)
                throw new CustomException("ESignatures not found", 404);

            var rows = await _common.ExecuteAsync(@"
                UPDATE ESignatures
                SET IsDeleted = TRUE,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @EmpCode
                WHERE Id = @Id AND CompanyId = @CompanyId;",
                new { Id = id, CompanyId, EmpCode = empCode });

            if (rows > 0)
            {
                string? url = ((IDictionary<string, object>)existing)["signatureurl"] as string;
                TryDeletePhysicalFile(url);
            }

            return rows > 0;
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<ESignatureReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = "WHERE es.IsDeleted = FALSE AND es.CompanyId = @CompanyId AND es.IsActive = @IsActive";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(es.UserId::text) LIKE '%{search}%'
                    OR UPPER(es.Id::text) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "USERID" => "es.UserId",
                "ID" => "es.Id",
                "ISACTIVE" => "es.IsActive",
                _ => "es.UserId"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            var dataSql = $@"
                SELECT es.*, c.Name AS Company
                FROM ESignatures es
                LEFT JOIN Companies c ON es.CompanyId = c.Id
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"SELECT COUNT(1) FROM ESignatures es {whereClause};";

            var queryParams = new { CompanyId, input.IsActive };

            var rows = await _common.QueryAsync<dynamic>(dataSql, queryParams);
            var items = rows.Select(r => MapToReadDto((IDictionary<string, object>)r)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<ESignatureReadDto>
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

    public async Task<ESignatureReadDto> GetByIdAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var row = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT es.*, c.Name AS Company
                FROM ESignatures es
                LEFT JOIN Companies c ON es.CompanyId = c.Id
                WHERE es.Id = @Id AND es.CompanyId = @CompanyId
                  AND es.IsDeleted = FALSE;",
                new { Id = id, CompanyId });

            if (row == null)
                throw new CustomException("ESignatures not found", 404);

            return MapToReadDto((IDictionary<string, object>)row);
        }
        catch (Exception)
        {
            throw;
        }
    }

    // Used to load whatever signature the current user already has saved when they land on the
    // ESignature page, so they see/can edit their existing signature instead of a blank pad.
    // Returns null (not a 404) when the user hasn't saved one yet -- that's a normal state for
    // this page, not an error.
    public async Task<ESignatureReadDto?> GetMyESignatureAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var row = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT es.*, c.Name AS Company
                FROM ESignatures es
                LEFT JOIN Companies c ON es.CompanyId = c.Id
                WHERE TRIM(es.UserId) = TRIM(@UserId) AND es.CompanyId = @CompanyId
                  AND es.IsDeleted = FALSE
                LIMIT 1;",
                new { UserId = empCode, CompanyId });

            return row == null ? null : MapToReadDto((IDictionary<string, object>)row);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<ESignatureReadDto> UpdateAsync(ESignatureUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id <= 0)
                throw new CustomException("Id is required.", 400);

            // Scoped to the caller's own UserId as well as Id -- without this, any user could
            // update anyone else's signature just by guessing an Id.
            var existing = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT SignatureURL FROM ESignatures
                WHERE Id = @Id AND CompanyId = @CompanyId AND TRIM(UserId) = TRIM(@UserId) AND IsDeleted = FALSE;",
                new { input.Id, CompanyId, UserId = empCode });

            if (existing == null)
                throw new CustomException("ESignatures not found", 404);

            string? newSignatureUrl = null;
            if (!string.IsNullOrWhiteSpace(input.SignatureBase64))
            {
                var (bytes, extension) = DecodeSignatureBase64(input.SignatureBase64);
                newSignatureUrl = SaveSignatureFile(bytes, extension, empCode);
            }

            await _common.ExecuteAsync(@"
                UPDATE ESignatures
                SET SignatureURL = COALESCE(@SignatureURL, SignatureURL),
                    IsActive = @IsActive,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @EmpCode
                WHERE Id = @Id AND CompanyId = @CompanyId AND TRIM(UserId) = TRIM(@UserId);",
                new
                {
                    input.Id,
                    CompanyId,
                    UserId = empCode,
                    SignatureURL = newSignatureUrl,
                    input.IsActive,
                    EmpCode = empCode
                });

            // Only remove the old file once the row update has committed, and only if a new
            // file actually replaced it.
            if (newSignatureUrl != null)
            {
                string? oldUrl = ((IDictionary<string, object>)existing)["signatureurl"] as string;
                TryDeletePhysicalFile(oldUrl);
            }

            return await GetByIdAsync(input.Id);
        }
        catch
        {
            throw;
        }
    }

    private static T GetValue<T>(IDictionary<string, object> row, string columnName)
    {
        if (row.ContainsKey(columnName) && row[columnName] != null && row[columnName] != DBNull.Value)
        {
            try
            {
                var value = row[columnName];

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
}