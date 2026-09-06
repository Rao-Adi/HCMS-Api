using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Models;

namespace HCMS_Api.Components.DMS.ESS;

public class UncontrolledDocumentComponent
{
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly DMSCommon _common;

    public UncontrolledDocumentComponent(
        DMSUtilities utilities,
        ClientContextService clientContextService,
        DMSCommon common)
    {
        _utilities = utilities;
        _clientContextService = clientContextService;
        _common = common;
    }

    // Every filename gets a timestamp prefix, not just the sanitized original name -- Review
    // uploads a new file against the same record without deleting the old one (History needs
    // both URLs to keep working), so two uploads sharing an original filename must never
    // collide on disk. Matches the {identifier}_{timestamp} pattern already used for signature
    // uploads elsewhere in this app.
    private static string SaveUploadedFile(IFormFile file, string uploadsRoot)
    {
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var safeOriginalName = Path.GetFileName(file.FileName);
        var fileName = $"{safeOriginalName}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        return $"/uploads/uncontrolled-documents/{fileName}";
    }

    private static string UploadsRoot =>
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "uncontrolled-documents");

    public async Task<UncontrolledDocumentReadDto> CreateAsync(UncontrolledDocumentCreateDto input)
    {
        int newId;

        // GetByIdAsync (below, after this block) deliberately runs on its own connection once
        // this transaction is fully done -- calling it from inside the try, after CommitAsync,
        // meant that if it threw for any reason the catch block would call tx.RollbackAsync() on
        // an already-committed transaction, which itself throws "This NpgsqlTransaction has
        // completed" and masks whatever the real error was.
        await using (var tx = await _common.BeginTransactionAsync())
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.DocumentName))
                    throw new CustomException("Document Name is required.", 400);
                if (string.IsNullOrWhiteSpace(input.ReviewAuthorityEmpCode))
                    throw new CustomException("Review Authority is required.", 400);
                if (input.DocumentFile == null || input.DocumentFile.Length == 0)
                    throw new CustomException("A document file is required.", 400);

                string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                int CompanyId = int.Parse(_CompanyId);
                var clientIp = _clientContextService.GetClientIP();
                var empId = _utilities.GetEmpid(clientIp);
                var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

                var documentUrl = SaveUploadedFile(input.DocumentFile, UploadsRoot);

                var insertQuery = @"
                    INSERT INTO UncontrolledDocuments
                        (CompanyId, DocumentName, DocumentURL, ReviewDate, ReviewAuthorityEmpCode,
                         IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                    VALUES
                        (@CompanyId, @DocumentName, @DocumentURL, @ReviewDate, @ReviewAuthorityEmpCode,
                         TRUE, FALSE, NOW(), @EmpCode, NOW(), @EmpCode)
                    RETURNING Id;";

                newId = await _common.ExecuteScalarAsync<int>(insertQuery, new
                {
                    CompanyId,
                    input.DocumentName,
                    DocumentURL = documentUrl,
                    input.ReviewDate,
                    input.ReviewAuthorityEmpCode,
                    EmpCode = empCode
                }, tx);

                await tx.CommitAsync();
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        return await GetByIdAsync(newId);
    }

    public async Task<UncontrolledDocumentReadDto> ReviewAsync(UncontrolledDocumentReviewDto input)
    {
        // Same reasoning as CreateAsync above: GetByIdAsync runs after this block, on its own
        // connection, once the transaction is fully committed or rolled back -- never inside the
        // same try/catch that would otherwise call tx.RollbackAsync() on an already-completed
        // transaction if it threw.
        await using (var tx = await _common.BeginTransactionAsync())
        {
            try
            {
                if (input.DocumentFile == null || input.DocumentFile.Length == 0)
                    throw new CustomException("A document file is required.", 400);

                string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                int CompanyId = int.Parse(_CompanyId);
                var clientIp = _clientContextService.GetClientIP();
                var empId = _utilities.GetEmpid(clientIp);
                var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

                var existing = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT DocumentURL, ReviewDate::timestamp AS ReviewDate
                    FROM UncontrolledDocuments
                    WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = FALSE",
                    new { input.Id, CompanyId }, tx);

                if (existing == null)
                    throw new CustomException("Uncontrolled Document not found.", 404);

                string previousDocumentUrl = existing.documenturl;
                DateTime previousReviewDate = existing.reviewdate;

                var newDocumentUrl = SaveUploadedFile(input.DocumentFile, UploadsRoot);

                await _common.ExecuteAsync(@"
                    INSERT INTO UncontrolledDocumentHistory
                        (CompanyId, UncontrolledDocumentId, PreviousDocumentURL, NewDocumentURL,
                         PreviousReviewDate, NewReviewDate, ReviewedBy, ReviewedAt)
                    VALUES
                        (@CompanyId, @Id, @PreviousDocumentUrl, @NewDocumentUrl,
                         @PreviousReviewDate, @NewReviewDate, @EmpCode, NOW());",
                    new
                    {
                        CompanyId,
                        input.Id,
                        PreviousDocumentUrl = previousDocumentUrl,
                        NewDocumentUrl = newDocumentUrl,
                        PreviousReviewDate = previousReviewDate,
                        input.NewReviewDate,
                        EmpCode = empCode
                    }, tx);

                await _common.ExecuteAsync(@"
                    UPDATE UncontrolledDocuments
                    SET DocumentURL = @NewDocumentUrl,
                        ReviewDate = @NewReviewDate,
                        LastModifiedAt = NOW(),
                        LastModifiedBy = @EmpCode
                    WHERE Id = @Id AND CompanyId = @CompanyId;",
                    new
                    {
                        NewDocumentUrl = newDocumentUrl,
                        input.NewReviewDate,
                        input.Id,
                        CompanyId,
                        EmpCode = empCode
                    }, tx);

                await tx.CommitAsync();
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        return await GetByIdAsync(input.Id);
    }

    public async Task<PaginationResult<UncontrolledDocumentReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // input.IsActive doubles as "not deleted" here -- the frontend always sends true
            // (there's no toggle in the UI yet), so this just excludes deleted records.
            var whereClause = @"
                WHERE ud.CompanyId = @CompanyId
                  AND ud.IsDeleted = FALSE
                  AND (@IsActive = FALSE OR ud.IsActive = TRUE)";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND UPPER(ud.DocumentName) LIKE '%{search}%'";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNAME" => "ud.DocumentName",
                "REVIEWDATE" => "ud.ReviewDate",
                "CREATEDAT" => "ud.CreatedAt",
                _ => "ud.Id"
            };
            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            var dataSql = $@"
                SELECT
                    ud.Id, ud.CompanyId, ud.DocumentName, ud.DocumentURL, ud.ReviewDate::timestamp AS ReviewDate,
                    ud.ReviewAuthorityEmpCode,
                    LTRIM(RTRIM(COALESCE(ra.firstname, '') || ' ' || COALESCE(ra.midname, '') || ' ' || COALESCE(ra.lastname, ''))) AS ReviewAuthorityName,
                    ud.IsActive, ud.CreatedAt, ud.CreatedBy,
                    LTRIM(RTRIM(COALESCE(cb.firstname, '') || ' ' || COALESCE(cb.midname, '') || ' ' || COALESCE(cb.lastname, ''))) AS CreatedByName,
                    ud.LastModifiedAt, ud.LastModifiedBy,
                    LTRIM(RTRIM(COALESCE(lm.firstname, '') || ' ' || COALESCE(lm.midname, '') || ' ' || COALESCE(lm.lastname, ''))) AS LastModifiedByName
                FROM UncontrolledDocuments ud
                LEFT JOIN tblEmployee ra ON LTRIM(RTRIM(ra.empcode::text), '0') = LTRIM(RTRIM(ud.ReviewAuthorityEmpCode), '0') AND ra.CompanyId = @CompanyId
                LEFT JOIN tblEmployee cb ON LTRIM(RTRIM(cb.empcode::text), '0') = LTRIM(RTRIM(ud.CreatedBy), '0') AND cb.CompanyId = @CompanyId
                LEFT JOIN tblEmployee lm ON LTRIM(RTRIM(lm.empcode::text), '0') = LTRIM(RTRIM(ud.LastModifiedBy), '0') AND lm.CompanyId = @CompanyId
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var countSql = $@"SELECT COUNT(1) FROM UncontrolledDocuments ud {whereClause};";

            var queryParams = new { CompanyId, IsActive = input.IsActive, Offset = offset, input.PageSize };

            var items = (await _common.QueryAsync<UncontrolledDocumentReadDto>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<UncontrolledDocumentReadDto>
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

    public async Task<UncontrolledDocumentReadDto> GetByIdAsync(int id)
    {
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int CompanyId = int.Parse(_CompanyId);

        var result = await _common.QueryFirstOrDefaultAsync<UncontrolledDocumentReadDto>(@"
            SELECT
                ud.Id, ud.CompanyId, ud.DocumentName, ud.DocumentURL, ud.ReviewDate::timestamp AS ReviewDate,
                ud.ReviewAuthorityEmpCode,
                LTRIM(RTRIM(COALESCE(ra.firstname, '') || ' ' || COALESCE(ra.midname, '') || ' ' || COALESCE(ra.lastname, ''))) AS ReviewAuthorityName,
                ud.IsActive, ud.CreatedAt, ud.CreatedBy,
                LTRIM(RTRIM(COALESCE(cb.firstname, '') || ' ' || COALESCE(cb.midname, '') || ' ' || COALESCE(cb.lastname, ''))) AS CreatedByName,
                ud.LastModifiedAt, ud.LastModifiedBy,
                LTRIM(RTRIM(COALESCE(lm.firstname, '') || ' ' || COALESCE(lm.midname, '') || ' ' || COALESCE(lm.lastname, ''))) AS LastModifiedByName
            FROM UncontrolledDocuments ud
            LEFT JOIN tblEmployee ra ON LTRIM(RTRIM(ra.empcode::text), '0') = LTRIM(RTRIM(ud.ReviewAuthorityEmpCode), '0') AND ra.CompanyId = @CompanyId
            LEFT JOIN tblEmployee cb ON LTRIM(RTRIM(cb.empcode::text), '0') = LTRIM(RTRIM(ud.CreatedBy), '0') AND cb.CompanyId = @CompanyId
            LEFT JOIN tblEmployee lm ON LTRIM(RTRIM(lm.empcode::text), '0') = LTRIM(RTRIM(ud.LastModifiedBy), '0') AND lm.CompanyId = @CompanyId
            WHERE ud.Id = @Id AND ud.CompanyId = @CompanyId AND ud.IsDeleted = FALSE",
            new { Id = id, CompanyId });

        if (result == null)
            throw new CustomException("Uncontrolled Document not found.", 404);

        return result;
    }

    public async Task<IEnumerable<UncontrolledDocumentHistoryDto>> GetHistoryAsync(int id)
    {
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int CompanyId = int.Parse(_CompanyId);

        return await _common.QueryAsync<UncontrolledDocumentHistoryDto>(@"
            SELECT
                h.Id, h.UncontrolledDocumentId, h.PreviousDocumentURL, h.NewDocumentURL,
                h.PreviousReviewDate::timestamp AS PreviousReviewDate, h.NewReviewDate::timestamp AS NewReviewDate, h.ReviewedBy,
                LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS ReviewedByName,
                h.ReviewedAt
            FROM UncontrolledDocumentHistory h
            LEFT JOIN tblEmployee e ON LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(h.ReviewedBy), '0') AND e.CompanyId = @CompanyId
            WHERE h.UncontrolledDocumentId = @Id AND h.CompanyId = @CompanyId
            ORDER BY h.ReviewedAt DESC;",
            new { Id = id, CompanyId });
    }

    public async Task<bool> DeleteAsync(int id)
    {
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int CompanyId = int.Parse(_CompanyId);
        var clientIp = _clientContextService.GetClientIP();
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        int exists = await _common.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM UncontrolledDocuments WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = FALSE",
            new { Id = id, CompanyId });

        if (exists == 0)
            throw new CustomException("Uncontrolled Document not found.", 404);

        await _common.ExecuteAsync(@"
            UPDATE UncontrolledDocuments
            SET IsDeleted = TRUE, LastModifiedAt = NOW(), LastModifiedBy = @EmpCode
            WHERE Id = @Id AND CompanyId = @CompanyId;",
            new { Id = id, CompanyId, EmpCode = empCode });

        return true;
    }
}
