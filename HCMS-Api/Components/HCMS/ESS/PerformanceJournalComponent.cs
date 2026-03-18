using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.DataAccess; 
using HCMS_Api.Controllers.HCMS.ESS; 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; 
using System.Data;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;

namespace HCMS_Api.Components.HCMS.ESS;
 
public class PerformanceJournalComponent 
{
    private readonly ILogger<UtilitiesController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly Utilities _utilities;
    private readonly DataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDapperDataService _dapperService;

    public PerformanceJournalComponent(
        IWebHostEnvironment env,
        Utilities utilities
        , ILogger<UtilitiesController> logger
        , DataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDapperDataService dapper)
    {
        _logger = logger;
        _env = env;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _dapperService = dapper;

        string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
        _dataservice.BeginProcess(connectionString);
    }

    public async Task<PJEmployeeInfo?> GetEmployeeInfoDataAsync(string empId, string companyId, string culture)
    {
        if (string.IsNullOrWhiteSpace(empId)) return null;

        const string sql = @"
                SELECT 
                    a.EmpCode AS EmployeeCode,
                    ISNULL(a.Name,'') AS EmployeeName,
                    FORMAT(E.DateofBirth, 'dd/MMM/yyyy') AS DateofBirth,
                    a.DivisionName AS Division,
                    ISNULL(a.MainDepartment,'') AS Department,
                    ISNULL(a.Department,'') AS SubDepartment,
                    a.Location AS Location,
                    a.Region as Region,
                    a.JobGroup AS Grade,
                    a.PayrollGroup AS PayrollGroup,
                    ISNULL(a.Designation,'') AS Designation,
                    a.EmployeeStatus AS EmployeeStatus,
                    EC.Name AS EmployeeCategory,
                    a.EmployeeType AS EmployeeType,

                    (SELECT TOP (1)
						CAST(CAST(dbo.fnGetDecryptData(CAST(EmpId AS varchar) + CAST(CompanyId AS varchar), BasicSalary) AS decimal(18,2)) AS int)
					FROM TblEmpSalarySetup
					WHERE EmpId = @EmpId
					  AND CompanyId = @CompanyId) AS BasicSalary,

                    dbo.fn_General_GetEmployeeName(E.ReportTo, @CompanyId, 1, 1, 1, @Culture) AS DirectReportingTo,
                    dbo.fn_General_GetEmployeeName(E.Dotted,   @CompanyId, 1, 1, 1, @Culture) AS IndirectReportingTo,
                    FORMAT(a.DateConfirm, 'dd/MMM/yyyy') AS DateofConfirmation,

                    (SELECT TOP (1)
						CAST(CAST(dbo.fnGetDecryptData(CAST(EmpId AS varchar) + CAST(CompanyId AS varchar), GrossSalary) AS decimal(18,2)) AS int)
					FROM TblEmpSalarySetup
					WHERE EmpId = @EmpId
					  AND CompanyId = @CompanyId) AS GrossSalary,

                    FORMAT(a.DateJoin, 'dd/MMM/yyyy') AS DateofJoining
	
                FROM fn_Lookup_EmployeeProfile(@Culture, (SELECT CompanyId FROM TblEmployee WHERE EmpId = @EmpId)) AS a
                INNER JOIN TblEmployee E                  ON E.EmpId = a.EmpId
                LEFT  JOIN tblempjobprofile ejp           ON ejp.EmpId = a.EmpId
                LEFT  JOIN Tblsetupsdetail sd             ON sd.sdlid = ejp.RoleId
                LEFT  JOIN vwtblSetupsDetail I            ON E.IqamaProfession = I.sdlid AND I.Culture = @Culture
                LEFT  JOIN vwtblSetupsDetail S            ON E.SpnsType        = S.sdlid AND S.Culture = @Culture
                LEFT  JOIN tblCountry Cnt                 ON E.SpnsCountry     = Cnt.cntid
                LEFT  JOIN tblCity Cty                    ON E.SpnsCity        = Cty.ctyid
                LEFT  JOIN vwtblSetupsDetail EC           ON EC.sdlid          = E.empCategoryid AND EC.Culture = @Culture
                LEFT  JOIN EXTERNAL_HCMS_FILES_TBLEMPLOYEE_IMAGECV AS f ON f.EmpId = a.EmpId
                WHERE a.EmpId = @EmpId;
            ";

        try
        {
            var info = (await _dapperService.QueryAsync<PJEmployeeInfo>(sql, new { EmpId = empId, CompanyId = companyId, Culture = culture }))?.FirstOrDefault();

            return info; // PJEmployeeInfo? returned; null if no row
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<dynamic?> InitiatePerformanceJournalAsync(
        int APID, int selectedEmpId, string formId, string companyId, string culture, int loginEmpId)
    {

        var user = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());

        try
        {
            var sql = "EXEC dbo.SP_PM_PerformanceJournal_Initiate " +
                      "@Param_APID, @Param_SelectedEmpId, @Param_UserId, @Param_UserEmpCode, @Param_UserEmpName, " +
                      "@Param_EntTerminalIP, @Param_EntTerminal, @Param_FormId, @Param_CompanyId, @Param_LoginCulture, @Param_LoginEmpID";

            var result = await _dapperService.QuerySingleAsync<dynamic>(
                sql,
                new
                {
                    Param_APID = APID,
                    Param_SelectedEmpId = selectedEmpId,
                    Param_UserId = user.UserEmpId,
                    Param_UserEmpCode = user.UserEmpCode,
                    Param_UserEmpName = user.UserEmpName,
                    Param_EntTerminalIP = _utilities.GetTerminalIP(), 
                    Param_EntTerminal = _utilities.GetTerminalId(), 
                    Param_FormId = formId,
                    Param_CompanyId = companyId,
                    Param_LoginCulture = culture,
                    Param_LoginEmpID = loginEmpId
                });

            return result;
        }
        catch
        {
            return null;
        }
    }

    public async Task<dynamic?> GetRIDAsync(int selectedEmpId, int APID)
    {
        try
        {
            const string sql = @" 
                                        SELECT TOP (1) RID FROM tblPMPerformanceReview
                                        WHERE EmpId = @SelectedEmpId AND APID  = @APID AND UPPER(LTRIM(RTRIM(Frequency))) = 'YEARLY'
                                        ORDER BY RID DESC;
                                    ";

            var result = await _dapperService.QuerySingleAsync<dynamic>(
                sql,
                new
                {
                    SelectedEmpId = selectedEmpId,
                    APID = APID
                });

            return result; // dynamic row with .RID
        }
        catch
        {
            return null;
        }
    }

    public async Task<dynamic?> GetSelectedEmpAsync(int selectedEmpId)
    {
        try
        {
            const string sql = @"DECLARE @EmpCompanyId   INT,
                                    @EmpDsgId       INT,
                                    @EmpCategoryId  INT,
                                    @EmpTypId       INT,
                                    @EmpFID         INT,
                                    @EmpCompanyAPID INT,
                                    @EmpDivId       INT,
                                    @EmpDeptId      INT,
                                    @EmpSubDeptId   INT;

                            -- From employee
                            SELECT @EmpCompanyId = e.companyid,
                                   @EmpDsgId = e.dsgid,
                                   @EmpCategoryId = e.empcategoryid,
                                   @EmpTypId = e.typid,
                                   @EmpDivId = e.divid,
                                   @EmpDeptId = e.mdptid,
                                   @EmpSubDeptId = e.dptid
                            FROM   dbo.tblemployee AS e
                            WHERE  e.empid = @Param_SelectedEmpId;

                            -- Format (FID) for company/designation/category/type
                            SELECT @EmpFID = d.fid
                            FROM   dbo.tblpmdesignationwiseformat AS d
                            WHERE  d.companyid = @EmpCompanyId
                                   AND d.dsgid = @EmpDsgId
                                   AND d.catid = @EmpCategoryId
                                   AND d.typid = @EmpTypId;

                            -- Active appraisal period for company
                            SELECT @EmpCompanyAPID = a.apid
                            FROM   dbo.tblappraisalperiod AS a
                            WHERE  a.companyid = @EmpCompanyId
                                   AND a.active = 1;

                            -- Final single row
                            SELECT @EmpCompanyId   AS EmpCompanyId,
                                   @EmpDsgId       AS EmpDsgId,
                                   @EmpCategoryId  AS EmpCategoryId,
                                   @EmpTypId       AS EmpTypId,
                                   @EmpFID         AS EmpFID,
                                   @EmpCompanyAPID AS EmpCompanyAPID,
                                   @EmpDivId       AS EmpDivId,
                                   @EmpDeptId      AS EmpDeptId,
                                   @EmpSubDeptId   AS EmpSubDeptId;";

            var result = await _dapperService.QuerySingleAsync<dynamic>(
                sql,
                new { Param_SelectedEmpId = selectedEmpId });

            return result; // always 1 row (NULLs if not found upstream)
        }
        catch
        {
            return null;
        }
    }

    public async Task<PoliciesResult?> GetPoliciesAsync(int empCompanyId, int empCompanyAPID, int empFID)
    {
        try
        {
            var result = await _dapperService.QueryMultipleAsync(
                @"EXEC dbo.sp_Performance_GetPolicies 
                        @CompanyId=@CompanyId, 
                        @APID=@APID, 
                        @FID=@FID;",
                new { CompanyId = empCompanyId, APID = empCompanyAPID, FID = empFID },
                async grid =>
                {
                    var t1 = (await grid.ReadAsync()).ToList();
                    var t2 = (await grid.ReadAsync()).ToList();
                    var t3 = (await grid.ReadAsync()).ToList();
                    var t4 = (await grid.ReadAsync()).ToList();
                    var t5 = (await grid.ReadAsync()).ToList();
                    return new PoliciesResult
                    {
                        Table1 = t1,
                        Table2 = t2,
                        Table3 = t3,
                        Table4 = t4,
                        Table5 = t5
                    };
                });

            return result;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetCompetenciesAsync(int APID, string selectedEmpId, string companyId, int RID, int viewerEmpId)
    {
        try
        {
            const string sql =
                "EXEC dbo.sp_PerformanceJournal_GetCompetenciesGrid " +
                "@APId, @EmpId, @CompanyId, @OrderClause, @Rid, @query, @OrderByColumn, @OrderByDir, @ViewerEmpId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    APId = APID,
                    EmpId = selectedEmpId,              // subject
                    CompanyId = companyId,
                    OrderClause = "",
                    Rid = RID,
                    query = (string)null,
                    OrderByColumn = "Id",
                    OrderByDir = "ASC",
                    ViewerEmpId = viewerEmpId           // <-- NEW
                });

            return rows;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetTargetRatingsAsync(int APID, string selectedEmpId, string companyId, int CompID)
    {
        try
        {
            const string sql =
                "EXEC dbo.sp_GetCompetencyRating @APId, @EmpId, @CompanyId, @comp3Id";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    APId = APID,
                    EmpId = selectedEmpId,
                    CompanyId = companyId,
                    comp3Id = CompID
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> UpdateCompetencyAsync(
        string Operation, int selectedEmpId, string companyId, int APID, int CompID, int RID, DataTable dt)
    {
        try
        {
            var user = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());

            var userID = user.UserID;
            var userEmpId = user.UserEmpId;
            var userEmpCode = user.UserEmpCode;
            var userEmpName = user.UserEmpName;
            var nowLocal = _utilities.GetScalarData("select dbo.fn_general_getlocaldatetimeCompanyWise(" + companyId + ")");
            var applicationID = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            var formID = "PerformanceJournal";
            var terminal = _utilities.GetTerminalId();
            var terminalIP = _utilities.GetTerminalIP();

            // Prepare XML
            var ds = new DataSet("ds");         // rename to what your SP expects if needed (e.g., "NewDataSet")
            dt.TableName = "Competencies";      // keep this aligned with SP shredding
            ds.Tables.Add(dt);
            string xml = ds.GetXml();

            const string sql = @"
                    EXEC dbo.SP_PM_PerformanceJournal_UpsertRatingWeight_XML 
                            @EmpId         = @EmpId,
                            @CompanyId     = @CompanyId,
                            @APId          = @APId,
                            @CompId        = @CompId,
                            @RId           = @RId,
                            @UserID        = @UserID,
                            @UserEmpId     = @UserEmpId,
                            @UserEmpCode   = @UserEmpCode,
                            @UserEmpName   = @UserEmpName,
                            @DateTime      = @DateTime,
                            @Operation     = @Operation,
                            @ApplicationID = @ApplicationID,
                            @FormID        = @FormID,
                            @Terminal      = @Terminal,
                            @TerminalIP    = @TerminalIP,
                            @Xml           = @Xml,
                            @Message       = @Message OUTPUT;";   // ✅ correct OUTPUT binding

            var dp = new DynamicParameters();
            dp.Add("@EmpId", selectedEmpId);
            dp.Add("@CompanyId", Convert.ToInt32(companyId));
            dp.Add("@APId", APID);
            dp.Add("@CompId", CompID);
            dp.Add("@RId", RID);
            dp.Add("@UserID", userID);
            dp.Add("@UserEmpId", userEmpId);
            dp.Add("@UserEmpCode", userEmpCode);
            dp.Add("@UserEmpName", userEmpName);
            dp.Add("@DateTime", nowLocal);
            dp.Add("@Operation", Operation);
            dp.Add("@ApplicationID", applicationID);
            dp.Add("@FormID", formID);
            dp.Add("@Terminal", terminal);
            dp.Add("@TerminalIP", terminalIP);
            dp.Add("@Xml", xml);
            dp.Add("@Message", dbType: DbType.String, size: 300, direction: ParameterDirection.Output); // ✅ match SP

            await _dapperService.ExecuteAsync(sql, dp);

            return dp.Get<string>("@Message");
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<IEnumerable<UnreadRow>> GetUnreadCountsAsync(int empId, IEnumerable<int> ids)
    {
        // Build TVP
        var tvp = new DataTable();
        tvp.Columns.Add("Id", typeof(int));
        foreach (var id in ids?.Distinct() ?? Enumerable.Empty<int>())
            tvp.Rows.Add(id);

        var dp = new DynamicParameters();
        dp.Add("@EmpId", empId);
        dp.Add("@CompetencyIds", tvp.AsTableValuedParameter("dbo.IntList")); // Dapper helper

        // If your Dapper wrapper doesn’t support AsTableValuedParameter,
        // use: dp.Add("@CompetencyIds", tvp, dbType: DbType.Object, direction: ParameterDirection.Input);
        // and inside _dapperService set SqlDbType.Structured + TypeName = "dbo.IntList".

        var rows = await _dapperService.QueryAsync<UnreadRow>(
            "dbo.sp_PM_CC_GetUnreadCounts",
            dp);

        return rows ?? Enumerable.Empty<UnreadRow>();
    }

    public async Task<IEnumerable<dynamic>?> GetRolesAsync(string selectedEmpId, int APID, string companyId)
    {
        try
        {
            const string sql = @" EXEC dbo.Get_PJ_CRoles
                                        @EmpId     = @EmpId,
                                        @APId      = @APId,
                                        @CompanyId = @CompanyId;";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    EmpId = selectedEmpId,   // keep as string if your SP accepts EmpCode; convert to int if needed
                    APId = APID,
                    CompanyId = companyId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetCompetenciesForAddAsync(int RID, string companyId)
    {
        try
        {
            // Safer than NOT IN (handles NULLs correctly)
            const string sql = @"
                                    SELECT 
                                        qssd.subsecdetailId,
                                        qssd.subsecId,
                                        qssd.secId,
                                        qssd.Code,
                                        qssd.Name,
                                        qssd.Rating,
                                        qssd.CompanyId,
                                        qssd.Description
                                    FROM dbo.tblQuizSubSectionDetail AS qssd
                                    WHERE 
                                        qssd.Active = 1
                                        AND qssd.CompanyId = @CompanyId
                                        AND NOT EXISTS (
                                            SELECT 1
                                            FROM dbo.tblPMPerformanceReviewCC AS prc
                                            WHERE prc.RID = @RID
                                              AND prc.SubSecDetailId = qssd.SubSecDetailId
                                        )
                                    ORDER BY qssd.Name;";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    RID = RID,
                    CompanyId = Convert.ToInt32(companyId) // if your column is INT
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetCompetencyCategorySubCategoryAsync(int competencyId)
    {
        try
        {
            const string sql = @"
                                    SELECT Category,
                                            SubCategory,
                                            CategoryId,
                                            SubCategoryId
                                    FROM dbo.vw_CompetencyCatSubCat
                                    WHERE Id = @CompetencyId
                                    ORDER BY Category, SubCategory;";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    CompetencyId = competencyId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetTargetRatingsAsync(int APID, int SelectedEmpId, int CompetencyId, string companyId)
    {
        try
        {
            // Parameterize properly; param names in SQL must match the anonymous object's properties
            const string sql = @"
                                    EXEC dbo.sp_GetCompetencyRating
                                        @APId     = @APId,
                                        @EmpId    = @EmpId,
                                        @CompanyId= @CompanyId,
                                        @comp3Id  = @Comp3Id;";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    APId = APID,
                    EmpId = SelectedEmpId,
                    CompanyId = companyId,          // if DB column is INT: Convert.ToInt32(companyId)
                    Comp3Id = CompetencyId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> AddCompetencyAysnc(
        int empCompanyId, int selectedEmpId, int empCompanyAPID, int rid, int empFID, DataTable dt)
    {
        try
        {

            var user = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());

            var UserID = user.UserID;
            var UserEmpId = user.UserEmpId;
            var UserEmpCode = user.UserEmpCode;
            var UserEmpName = user.UserEmpName;
            var DateTime = _utilities.GetScalarData("select dbo.fn_general_getlocaldatetimeCompanyWise(" + empCompanyId + ")");
            var Operation = "INSERT";
            var ApplicationID = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            var FormID = "PerformanceJournal";
            var Terminal = _utilities.GetTerminalId();
            var TerminalIP = _utilities.GetTerminalIP();

            // Pack into DataSet -> XML (element-based; easy to shred in SQL)
            var ds = new DataSet("ds");
            dt.TableName = "Competency";   // matches the helper above
            ds.Tables.Add(dt);
            string xml = ds.GetXml();

            const string sql = @"
                    EXEC dbo.SP_PM_PerformanceJournal_AddCompetency
                        @CompanyId   = @CompanyId,
                        @EmpId       = @EmpId,
                        @APID        = @APID,
                        @RID         = @RID,
                        @FID         = @FID,
                        @Xml         = @Xml,
                        @Message     = @Message OUTPUT,
                        @UserID      = @UserID,
                        @UserEmpId   = @UserEmpId,
                        @UserEmpCode = @UserEmpCode,
                        @UserEmpName = @UserEmpName,
                        @DateTime    = @DateTime,
                        @Operation   = @Operation,
                        @ApplicationID = @ApplicationID,
                        @FormID      = @FormID,
                        @Terminal    = @Terminal,
                        @TerminalIP  = @TerminalIP;
                    ";

            var dp = new DynamicParameters();
            dp.Add("@CompanyId", empCompanyId);
            dp.Add("@EmpId", selectedEmpId);
            dp.Add("@APID", empCompanyAPID);
            dp.Add("@RID", rid);
            dp.Add("@FID", empFID);
            dp.Add("@UserID", UserID);
            dp.Add("@UserEmpId", UserEmpId);
            dp.Add("@UserEmpCode", UserEmpCode);
            dp.Add("@UserEmpName", UserEmpName);
            dp.Add("@DateTime", DateTime);
            dp.Add("@Operation", Operation);
            dp.Add("@ApplicationID", ApplicationID);
            dp.Add("@FormID", FormID);
            dp.Add("@Terminal", Terminal);
            dp.Add("@TerminalIP", TerminalIP);
            dp.Add("@Xml", xml);
            dp.Add("@Message", dbType: DbType.String, size: 300, direction: ParameterDirection.Output);

            // We don't rely on rows count; we rely on @Message set by the SP
            await _dapperService.ExecuteAsync(sql, dp);

            return dp.Get<string>("@Message") ?? "OK";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<int> MarkThreadReadAsync(int empId, int competencyId)
    {
        const string sql = "dbo.sp_PM_CC_MarkThreadRead";
        var dp = new DynamicParameters();
        dp.Add("@EmpId", empId);
        dp.Add("@CompetencyId", competencyId);
        // ExecuteAsync returns rows affected (0/1) depending on your proc; OK to ignore
        return await _dapperService.ExecuteAsync(sql, dp);
    }

    public async Task<IEnumerable<dynamic>> GetCompetenciesChatCommentsAsync(
        int Id, int RId, int CompID, int APId, int FID, int EmpId,
        int SubId, int SubSecId, int SubSecDetailId, int JDId, bool active, string clientIP)
    {
        var companyId = _utilities.GetCompanyId(clientIP);

        var param = new
        {
            Id,
            RId,
            CompId = CompID,
            APId,
            FID,
            EmpId,
            SubId,
            SubSecId,
            SubSecDetailId,
            JDId,
            Active = active
        };

        // 1) Get comments (first result)
        const string sqlComments = @"
        SELECT
          cc.CCId,
          cc.Id, cc.RId, cc.APId, cc.EmpId, cc.FID, cc.CompId, cc.JDId,
          cc.SubId, cc.SubName, cc.SubSecId, cc.SubSecName, cc.SubSecDetailId,
          cc.Competency, cc.RatingId, cc.Weightage, cc.ActualWeightage,
          cc.Type, cc.Source, cc.TargetRatingID, cc.ReviewRatingID, cc.ReviewFrequency,
          cc.PercentScore, cc.WeightedScore, cc.IncludeInNextPeriod,
          cc.Operation, cc.Active, cc.Locked, cc.CreateOn, cc.CreateBy, cc.InitialTerminal,
          cc.UpdateOn, cc.UpdateBy, cc.LastTerminal, cc.CompanyId, cc.IsCoreCompetency,
          cc.Comments, cc.PerformanceStatusId, cc.PJCommentBy, cc.PJCommentDateTime, cc.PJReflectionRequired,
          cc.AttachmentFileName, cc.AttachmentStoredName, cc.AttachmentRelativePath, cc.AttachmentMimeType, cc.AttachmentSizeBytes
        FROM dbo.tblPMPerformanceJournalCompetencyChatComments cc
        WHERE cc.Id = @Id AND cc.RId = @RId AND cc.CompId = @CompId
          AND cc.APId = @APId AND cc.FID = @FID AND cc.EmpId = @EmpId
          AND cc.SubId = @SubId AND cc.SubSecId = @SubSecId AND cc.SubSecDetailId = @SubSecDetailId
          AND cc.JDId = @JDId
        ORDER BY cc.PJCommentDateTime ASC;";

        var comments = (await _dapperService.QueryAsync<dynamic>(sqlComments, param)).ToList();

        if (comments.Count == 0)
            return comments;

        // 2) Query attachments for those CCIds (second round-trip; no Dapper Multi required)
        var ccIds = comments.Select(c => (int)c.CCId).Distinct().ToList();

        // Build a TVP to avoid massive IN clauses
        var idsTvp = new DataTable();
        idsTvp.Columns.Add("Id", typeof(int));
        foreach (var id in ccIds) idsTvp.Rows.Add(id);

        var attParams = new Dapper.DynamicParameters();
        attParams.Add("Ids", idsTvp.AsTableValuedParameter("dbo.IntListUDT")); // <-- create once if you don't have it yet
        const string sqlAtt = @"
        SELECT
          a.CCId,
          a.FileName      AS AttachmentFileName,
          a.StoredName    AS AttachmentStoredName,
          a.RelativePath  AS AttachmentRelativePath,
          a.MimeType      AS AttachmentMimeType,
          a.SizeBytes     AS AttachmentSizeBytes
        FROM dbo.PJ_CompetencyCommentAttachment a
        INNER JOIN @Ids i ON a.CCId = i.Id;";

        // If you don't have IntListUDT yet, fall back to string join and IN (...) safely (parameterize) — or create IntListUDT once.
        IEnumerable<dynamic> atts;
        try
        {
            atts = await _dapperService.QueryAsync<dynamic>(sqlAtt, attParams);
        }
        catch
        {
            // Fallback without TVP (safe small list)
            var inClause = string.Join(",", ccIds);
            var sqlFallback = $@"
            SELECT
              a.CCId,
              a.FileName      AS AttachmentFileName,
              a.StoredName    AS AttachmentStoredName,
              a.RelativePath  AS AttachmentRelativePath,
              a.MimeType      AS AttachmentMimeType,
              a.SizeBytes     AS AttachmentSizeBytes
            FROM dbo.PJ_CompetencyCommentAttachment a
            WHERE a.CCId IN ({inClause});";
            atts = await _dapperService.QueryAsync<dynamic>(sqlFallback, null);
        }

        var lookup = atts.GroupBy(a => (int)a.CCId)
                         .ToDictionary(g => g.Key, g => g
                            .Select(a => new {
                                fileName = (string)a.AttachmentFileName,
                                storedName = (string)a.AttachmentStoredName,
                                relativePath = (string)a.AttachmentRelativePath,
                                mimeType = (string)a.AttachmentMimeType,
                                sizeBytes = (long?)a.AttachmentSizeBytes
                            }).ToList());

        // 3) Attach arrays to each comment (non-breaking — FE already checks both array & legacy single)
        var enriched = comments.Select(c =>
        {
            var ccid = (int)c.CCId;
            var obj = (IDictionary<string, object>)new System.Dynamic.ExpandoObject();
            foreach (var prop in ((IDictionary<string, object>)c))
                obj[prop.Key] = prop.Value;

            if (lookup.TryGetValue(ccid, out var arr))
                obj["Attachments"] = arr;
            else
                obj["Attachments"] = new List<object>();

            return (dynamic)obj;
        }).ToList();

        return enriched;
    }

    public async Task<object> UpsertCompetenciesChatCommentsAsync(SaveChatCommentDto dto, string clientIP)
    {
        var companyId = _utilities.GetCompanyId(clientIP);
        var user = _utilities.GetCurrentUserMap(clientIP);
        var empName = user.UserEmpName;
        var terminalIP = _utilities.GetTerminalIP();
        var loginEmpId = _utilities.GetEmpid(clientIP);

        if (dto.Operation == "UPDATE" || dto.Operation == "DELETE")
            dto.PJCommentBy = loginEmpId;

        decimal? percent = dto.PercentScore.HasValue ? Math.Round(dto.PercentScore.Value, 0) : (decimal?)null;
        decimal? weighted = dto.WeightedScore.HasValue ? Math.Round(dto.WeightedScore.Value, 0) : (decimal?)null;

        // ---------- NEW: compute removed physical files (for UPDATE/INSERT) ----------
        var existing = new List<(string StoredName, string RelativePath)>();
        if ((dto.Operation == "UPDATE" || dto.Operation == "INSERT") && dto.CCId.HasValue && dto.CCId.Value > 0)
        {
            const string q = @"SELECT StoredName, RelativePath FROM dbo.PJ_CompetencyCommentAttachment WHERE CCId=@CCId";
            existing = (await _dapperService.QueryAsync<dynamic>(q, new { CCId = dto.CCId.Value }))
                       .Select(r => ((string)r.StoredName, (string)r.RelativePath)).ToList();
        }

        var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dto.Attachments != null && dto.Attachments.Count > 0)
            foreach (var a in dto.Attachments)
                if (!string.IsNullOrWhiteSpace(a.AttachmentStoredName))
                    desired.Add(a.AttachmentStoredName);

        if (!string.IsNullOrWhiteSpace(dto.AttachmentStoredName))
            desired.Add(dto.AttachmentStoredName);

        var localCompanyTime = DateTime.UtcNow;
        try
        {
            var scalar = _utilities.GetScalarData(
                $"select dbo.fn_general_getlocaldatetimeCompanyWise({companyId})");
            if (scalar != null && DateTime.TryParse(Convert.ToString(scalar), out var parsed))
            {
                localCompanyTime = parsed;
            }
        }
        catch
        {
            localCompanyTime = DateTime.UtcNow;
        }

        // If the client explicitly requested removal of all
        if (dto.RemoveAttachment == true) desired.Clear();

        // ---------------------------------------------------------------------------

        var dyn = new Dapper.DynamicParameters();
        dyn.Add("Operation", dto.Operation);
        dyn.Add("CompanyId", companyId);
        dyn.Add("CCId", dto.CCId);

        dyn.Add("Id", dto.Id);
        dyn.Add("RId", dto.RId);
        dyn.Add("APId", dto.APId);
        dyn.Add("EmpId", dto.EmpId);
        dyn.Add("FID", dto.FID);
        dyn.Add("CompId", dto.CompId);
        dyn.Add("JDId", dto.JDId);
        dyn.Add("SubId", dto.SubId);
        dyn.Add("SubName", dto.SubName);
        dyn.Add("SubSecId", dto.SubSecId);
        dyn.Add("SubSecName", dto.SubSecName);
        dyn.Add("SubSecDetailId", dto.SubSecDetailId);
        dyn.Add("Competency", dto.Competency);

        dyn.Add("RatingId", dto.RatingId);
        dyn.Add("Weightage", dto.Weightage);
        dyn.Add("ActualWeightage", dto.ActualWeightage);
        dyn.Add("Type", dto.Type);
        dyn.Add("Source", dto.Source);
        dyn.Add("TargetRatingID", dto.TargetRatingID);
        dyn.Add("ReviewRatingID", dto.ReviewRatingID);
        dyn.Add("ReviewFrequency", dto.ReviewFrequency);
        dyn.Add("PercentScore", percent);
        dyn.Add("WeightedScore", weighted);
        dyn.Add("IncludeInNextPeriod", dto.IncludeInNextPeriod);
        dyn.Add("IsCoreCompetency", dto.IsCoreCompetency);

        dyn.Add("Comments", dto.Comments);
        dyn.Add("PerformanceStatusId", dto.PerformanceStatusId);
        dyn.Add("PJCommentBy", dto.PJCommentBy);
        dyn.Add("PJCommentDateTime", localCompanyTime);
        dyn.Add("PJReflectionRequired", dto.PJReflectionRequired);

        dyn.Add("CreateBy", empName);
        dyn.Add("InitialTerminal", terminalIP);
        dyn.Add("UpdateBy", empName);
        dyn.Add("LastTerminal", terminalIP);

        dyn.Add("AttachmentFileName", dto.AttachmentFileName);
        dyn.Add("AttachmentStoredName", dto.AttachmentStoredName);
        dyn.Add("AttachmentRelativePath", dto.AttachmentRelativePath);
        dyn.Add("AttachmentMimeType", dto.AttachmentMimeType);
        dyn.Add("AttachmentSizeBytes", dto.AttachmentSizeBytes);
        dyn.Add("RemoveAttachment", dto.RemoveAttachment);

        var tvp = new DataTable();
        tvp.Columns.Add("AttachmentFileName", typeof(string));
        tvp.Columns.Add("AttachmentStoredName", typeof(string));
        tvp.Columns.Add("AttachmentRelativePath", typeof(string));
        tvp.Columns.Add("AttachmentMimeType", typeof(string));
        tvp.Columns.Add("AttachmentSizeBytes", typeof(long));

        if (dto.Attachments != null)
        {
            foreach (var a in dto.Attachments)
                tvp.Rows.Add(a.AttachmentFileName, a.AttachmentStoredName, a.AttachmentRelativePath, a.AttachmentMimeType, a.AttachmentSizeBytes);
        }

        dyn.Add("Attachments", tvp.AsTableValuedParameter("dbo.AttachmentMetaUDT")); 

        const string sql = @"
                EXEC dbo.SP_PM_PJ_CompetenciesChatComments_Upsert
                     @Operation=@Operation,
                     @CompanyId=@CompanyId,
                     @CCId=@CCId,
                     @Id=@Id,
                     @RId=@RId,
                     @APId=@APId,
                     @EmpId=@EmpId,
                     @FID=@FID,
                     @CompId=@CompId,
                     @JDId=@JDId,
                     @SubId=@SubId,
                     @SubName=@SubName,
                     @SubSecId=@SubSecId,
                     @SubSecName=@SubSecName,
                     @SubSecDetailId=@SubSecDetailId,
                     @Competency=@Competency,
                     @RatingId=@RatingId,
                     @Weightage=@Weightage,
                     @ActualWeightage=@ActualWeightage,
                     @Type=@Type,
                     @Source=@Source,
                     @TargetRatingID=@TargetRatingID,
                     @ReviewRatingID=@ReviewRatingID,
                     @ReviewFrequency=@ReviewFrequency,
                     @PercentScore=@PercentScore,
                     @WeightedScore=@WeightedScore,
                     @IncludeInNextPeriod=@IncludeInNextPeriod,
                     @IsCoreCompetency=@IsCoreCompetency,
                     @Comments=@Comments,
                     @PerformanceStatusId=@PerformanceStatusId,
                     @PJCommentBy=@PJCommentBy,
                     @PJCommentDateTime=@PJCommentDateTime,
                     @PJReflectionRequired=@PJReflectionRequired,
                     @CreateBy=@CreateBy,
                     @InitialTerminal=@InitialTerminal,
                     @UpdateBy=@UpdateBy,
                     @LastTerminal=@LastTerminal,
                     @AttachmentFileName=@AttachmentFileName,
                     @AttachmentStoredName=@AttachmentStoredName,
                     @AttachmentRelativePath=@AttachmentRelativePath,
                     @AttachmentMimeType=@AttachmentMimeType,
                     @AttachmentSizeBytes=@AttachmentSizeBytes,
                     @RemoveAttachment=@RemoveAttachment,
                     @Attachments=@Attachments";

        var result = await _dapperService.QuerySingleAsync<dynamic>(sql, dyn);
        if (result == null) result = new { CCId = 0, Operation = dto.Operation, Affected = 0 };

        // ---------- NEW: delete removed physical files if DB update succeeded ----------
        try
        {
            bool ok = (dto.Operation == "DELETE") ? ((int)result.Affected > 0)
                     : ((string)result.Operation == dto.Operation && ((int)result.CCId) > 0);

            if (ok && (dto.Operation == "UPDATE" || dto.Operation == "INSERT"))
            {
                // Desired set after update: 'desired'
                // Removed = existing - desired
                var removed = existing.Where(e => !desired.Contains(e.StoredName)).ToList();
                if (removed.Count > 0)
                {
                    var webRoot = _env.WebRootPath;
                    if (string.IsNullOrWhiteSpace(webRoot))
                    {
                        webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                        Directory.CreateDirectory(webRoot);
                    }

                    foreach (var r in removed)
                    {
                        // Use the saved relative path if available
                        var fullPath = !string.IsNullOrWhiteSpace(r.RelativePath)
                            ? Path.Combine(webRoot, r.RelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))
                            : Path.Combine(webRoot, "uploads", "pj", companyId.ToString(), r.StoredName);

                        try
                        {
                            if (System.IO.File.Exists(fullPath))
                                System.IO.File.Delete(fullPath);
                        }
                        catch (Exception exDel)
                        {
                            _logger.LogWarning(exDel, "Couldn't delete removed attachment file at {fullPath}", fullPath);
                        }
                    }
                }
            }
        }
        catch (Exception ex2)
        {
            _logger.LogWarning(ex2, "Attachment file cleanup failed (will ignore).");
        }
        // ---------------------------------------------------------------------------

        return result;
    }



    public async Task<IEnumerable<dynamic>?> GetCompanyObjectivesAsync(bool Include, int APID, int EmpId)
    {
        try
        {
            // Explicitly cast Include as 1 or 0
            const string sql = @"exec GetCompanyObjectives @Included = @Include, @APId = @APID, @EmpId = @EmpId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    Include = Include ? 1 : 0,  // Ensure boolean is converted to 1 or 0
                    APID = APID,
                    EmpId = EmpId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetDivisionObjectivesAsync(bool Include, int EmpId, int compObj, int div, int APID)
    {
        try
        {
            // Explicitly cast Include as 1 or 0
            const string sql = @"exec GetDivisionObjectives @Included = @Include, @APId = @APID, @CompObjId = @CompObjId, @DivId = @DivId, @EmpId = @EmpId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    Include = Include ? 1 : 0,  // Ensure boolean is converted to 1 or 0
                    APID = APID,
                    EmpId = EmpId,
                    CompObjId = compObj,  // Corrected: Ensure parameter name matches SQL
                    DivId = div           // Corrected: Ensure parameter name matches SQL
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetDepartmentObjectivesAsync(bool Include, int EmpId, int divObj, int compObj, int dept, int APID)
    {
        try
        {
            // Explicitly cast Include as 1 or 0
            const string sql = @"exec GetDeptObjectives @Included = @Included, @APId = @APId, @CompObjId = @CompObjId, @DivObjId = @DivObjId, @DeptId = @DeptId, @EmpId = @EmpId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    Included = Include ? 1 : 0,  // Ensure boolean is converted to 1 or 0
                    APID = APID,
                    CompObjId = compObj,
                    DivObjId = divObj,
                    DeptId = dept,
                    EmpId = EmpId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetSubDepartmentObjectivesAsync(bool Include, int EmpId, int deptObj, int divObj, int compObj, int subdept, int APID)
    {
        try
        {
            // Explicitly cast Include as 1 or 0
            const string sql = @"exec GetSubDeptObjectives @Included = @Included, @APId = @APId, @CompObjId = @CompObjId, @DivObjId = @DivObjId, @DeptObjId = @DeptObjId, @SubDeptId = @SubDeptId, @EmpId = @EmpId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    Included = Include ? 1 : 0,  // Ensure boolean is converted to 1 or 0
                    APID = APID,
                    CompObjId = compObj,
                    DivObjId = divObj,
                    DeptObjId = deptObj,
                    SubDeptId = subdept,
                    EmpId = EmpId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> AddObjectiveAsync(
        int rid,
        int apid,
        int empId,
        int fid,
        string formId,
        string companyId,
        AddObjectives dto,
        string measuresTargetsXml,
        string strategiesBudgetsXml)
    {
        try
        {
            var user = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
            var userId = user.UserID;
            var terminalId = _utilities.GetTerminalId();

            // Safe company-local time (works even if companyId isn't numeric)
            // TRY_CONVERT returns NULL if it can't convert; the function will just get NULL.
            var localNow = _utilities.GetScalarData("select dbo.fn_general_getlocaldatetimeCompanyWise(" + companyId + ")");

            var p = new DynamicParameters();

            p.Add("@RId", rid);
            p.Add("@APId", apid);
            p.Add("@EmpId", empId);
            p.Add("@FID", fid);

            // If your domain really wants a fixed label, keep it; otherwise prefer dto.RelevantType
            p.Add("@ObjectiveType", "PerformanceJournal");

            p.Add("@DepartmentObjectiveId", dto.DepartmentObjectiveID);
            p.Add("@SubDepartmentObjectiveId", dto.SubDepartmentObjectiveID);

            p.Add("@EmployeeObjective", dto.ObjectiveDescription);

            // nvarchar(50) in SP -> send strings
            p.Add("@Weightage", dto.Weightage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            p.Add("@Target", dto.Target);
            p.Add("@Achievement", ""); // as per your current setup
            p.Add("@UnitofMeasurement", 0); // was 0 before; keep string
            p.Add("@TargetType", "");                   // leave blank if not tracked
            p.Add("@TargetTrackingPeriod", "");

            p.Add("@AchievementPercentage", 0); // or "0" if your SP expects a number-in-string
            p.Add("@Source", formId );
            p.Add("@Active", "1");
            p.Add("@Locked", "0");

            p.Add("@Userid", userId);
            p.Add("@TerminalId", terminalId);

            // SP expects nvarchar(50); pass the string companyId directly
            p.Add("@EmpCompId", companyId);

            p.Add("@Comments", "");

            p.Add("@CompanyObjectiveId", dto.CompanyObjectiveID);
            p.Add("@DivisionObjectiveId", dto.DivisionObjectiveID);

            p.Add("@MeasuresTargets", measuresTargetsXml ?? "<MeasuresTargets />", DbType.Xml);
            p.Add("@StrategiesBudgets", strategiesBudgetsXml ?? "<StrategiesBudgets />", DbType.Xml);

            p.Add("@CompanyObjectiveText", dto.CompanyObjective);
            p.Add("@DivisionObjectiveText", dto.DivisionObjective);
            p.Add("@DepartmentObjectiveText", dto.DepartmentObjective);
            p.Add("@SubDepartmentObjectiveText", dto.SubDepartmentObjective);

            p.Add("@scaleId", 0);
            p.Add("@RatingDescId", null);
            p.Add("@SelectedPerformanceCOID", dto.Id);
            p.Add("@PerformantStatusId", 0);


            p.Add("@Specific", dto.Specific);

            // Nvarchar(max) fields
            p.Add("@Measurable", dto.Measurable);
            p.Add("@AttainableResourcesRequired", dto.AttainableResourcesRequired);
            p.Add("@RelevantTypeID", dto.RelevantTypeID);
            p.Add("@RelevantType", dto.RelevantType);

            // Datetime
            p.Add("@Timeframe", dto.TimeFrame == default ? (DateTime?)null : dto.TimeFrame, DbType.DateTime2);

            // TargetRating (add property in DTO if not present)
            p.Add("@Timelines", dto.Timelines); ;

            const string sql = @"
                                    EXEC SP_PerformanceJournal_InsertIntblPMPerformanceReviewCO
                                          @RId=@RId,
                                          @APId=@APId,
                                          @EmpId=@EmpId,
                                          @FID=@FID,
                                          @ObjectiveType=@ObjectiveType,
                                          @DepartmentObjectiveId=@DepartmentObjectiveId,
                                          @SubDepartmentObjectiveId=@SubDepartmentObjectiveId,
                                          @EmployeeObjective=@EmployeeObjective,
                                          @Weightage=@Weightage,
                                          @Target=@Target,
                                          @Achievement=@Achievement,
                                          @UnitofMeasurement=@UnitofMeasurement,
                                          @TargetType=@TargetType,
                                          @TargetTrackingPeriod=@TargetTrackingPeriod,
                                          @AchievementPercentage=@AchievementPercentage,
                                          @Source=@Source,
                                          @Active=@Active,
                                          @Locked=@Locked,
                                          @Userid=@Userid,
                                          @TerminalId=@TerminalId,
                                          @EmpCompId=@EmpCompId,
                                          @Comments=@Comments,
                                          @CompanyObjectiveId=@CompanyObjectiveId,
                                          @DivisionObjectiveId=@DivisionObjectiveId,
                                          @MeasuresTargets=@MeasuresTargets,
                                          @StrategiesBudgets=@StrategiesBudgets,
                                          @CompanyObjectiveText=@CompanyObjectiveText,
                                          @DivisionObjectiveText=@DivisionObjectiveText,
                                          @DepartmentObjectiveText=@DepartmentObjectiveText,
                                          @SubDepartmentObjectiveText=@SubDepartmentObjectiveText,
                                          @scaleId=@scaleId,
                                          @RatingDescId=@RatingDescId,
                                          @SelectedPerformanceCOID=@SelectedPerformanceCOID,
                                          @PerformantStatusId=@PerformantStatusId,
                                          @Specific=@Specific,
                                          @Measurable=@Measurable,
                                          @AttainableResourcesRequired=@AttainableResourcesRequired,
                                          @RelevantTypeID=@RelevantTypeID,
                                          @RelevantType=@RelevantType,
                                          @Timeframe=@Timeframe,
                                          @Timelines=@Timelines;";

            var status = await _dapperService.ExecuteScalarAsync<string>(sql, p);

            return string.IsNullOrWhiteSpace(status) ? "OK" : status;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<IEnumerable<dynamic>?> GetObjectivesAsync(int RID, int EmpId, int CompanyId)
    {
        try
        {

            const string sql = @"exec sp_PerformanceJournal_GetObjectivesGrid @APId = @APId,
                    @EmpId = @EmpId,
                    @CompanyId = @CompanyId,
                    @OrderClause = '',
                    @GetObjectivesOnly = 1, 
                    @PerformanceCOID = Null";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    APId = RID,
                    EmpId = EmpId,
                    CompanyId = CompanyId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> DeleteObjectivesAsync(int Id)
    {
        try
        {

            const string sql = @"exec deletePerformanceObjectiveCO @Id = @Id";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    Id = Id
                });

            return rows; 
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<dynamic>?> GetPerformanceStatusesAsync(int companyId)
    {
        const string sql = @"
                                select Id, Name, ColorCode
                                from tblPerformanceStatus
                                where CompanyId = @CompanyId    
                                order by Name;";

        // Let exceptions bubble up; Dapper returns an empty sequence if no rows
        var rows = await _dapperService.QueryAsync<dynamic>(sql, new { CompanyId = companyId });
        return rows.AsList(); // materialize to avoid multiple enumeration surprises
    }

    public async Task<IReadOnlyList<dynamic>?> GetRatingsORAsync(int APId, int EmpId, string culture, int companyId)
    {
        const string sql = @"Select * from dbo.fn_TblCompetencyRating(@APId, 'OR', @EmpId, @culture, @companyId) Order by RatingValue";

        // Let exceptions bubble up; Dapper returns an empty sequence if no rows
        var rows = await _dapperService.QueryAsync<dynamic>(
            sql, 
            new {
                APId = APId,
                EmpId = EmpId,
                culture = culture,
                companyId = companyId
            });
        return rows.AsList(); // materialize to avoid multiple enumeration surprises
    }

    public async Task<object> SetObjectiveChatCommentsAsync(JObject payload, string clientIP)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(clientIP)) throw new ArgumentNullException(nameof(clientIP));

        var companyIdText = _utilities.GetCompanyId(clientIP);
        if (string.IsNullOrWhiteSpace(companyIdText) || !int.TryParse(companyIdText, out var companyId))
            throw new InvalidOperationException("Unable to resolve a numeric CompanyId.");

        var user = _utilities.GetCurrentUserMap(clientIP);
        var empName = user?.UserEmpName ?? "SYSTEM";
        var terminalIP = _utilities.GetTerminalIP() ?? string.Empty;
        var loginEmpId = _utilities.GetEmpid(clientIP);

        var ocId = payload.Value<int?>("OCId");
        var rawOp = payload.Value<string>("Operation");
        var operation = string.IsNullOrWhiteSpace(rawOp)
            ? (ocId.HasValue && ocId.Value > 0 ? "UPDATE" : "INSERT")
            : rawOp.Trim().ToUpperInvariant();

        if (operation != "INSERT" && operation != "UPDATE" && operation != "DELETE")
            throw new InvalidOperationException($"Unsupported operation '{operation}'.");

        if ((operation == "UPDATE" || operation == "DELETE") && (!ocId.HasValue || ocId.Value <= 0))
            throw new InvalidOperationException("OCId is required for update or delete operations.");

        var includeNext = payload.Value<bool?>("IncludeInNextPeriod") ?? false;
        var active = payload.Value<bool?>("Active") ?? true;
        var locked = payload.Value<bool?>("Locked") ?? false;
        var completed = payload.Value<bool?>("PJCompleted") ?? false;

        var attachmentsToken = payload["Attachments"];
        var removeAttachment = payload.Value<bool?>("RemoveAttachment") ?? false;
        var attachmentsTable = BuildAttachmentTable(attachmentsToken);

        var localCompanyTime = DateTime.UtcNow;
        try
        {
            var scalar = _utilities.GetScalarData(
                $"select dbo.fn_general_getlocaldatetimeCompanyWise({companyId})");
            if (scalar != null && DateTime.TryParse(Convert.ToString(scalar), out var parsed))
            {
                localCompanyTime = parsed;
            }
        }
        catch
        {
            localCompanyTime = DateTime.UtcNow;
        }

        var isUpdate = operation == "UPDATE";

        var parameters = new DynamicParameters();
        parameters.Add("Operation", operation);
        parameters.Add("CompanyId", companyId);
        parameters.Add("OCId", ocId);
        parameters.Add("Id", payload.Value<int?>("Id"));
        parameters.Add("RId", payload.Value<int?>("RId"));
        parameters.Add("APId", payload.Value<int?>("APId"));
        parameters.Add("EmpId", payload.Value<int?>("EmpId"));
        parameters.Add("FID", payload.Value<int?>("FID"));
        parameters.Add("ObjectiveType", payload.Value<string>("ObjectiveType"));
        parameters.Add("EmployeeObjective", payload.Value<string>("EmployeeObjective"));
        parameters.Add("CompanyObjectiveId", payload.Value<int?>("CompanyObjectiveId"));
        parameters.Add("DivisionObjectiveId", payload.Value<int?>("DivisionObjectiveId"));
        parameters.Add("DepartmentObjectiveId", payload.Value<int?>("DepartmentObjectiveId"));
        parameters.Add("SubDepartmentObjectiveId", payload.Value<int?>("SubDepartmentObjectiveId"));
        parameters.Add("Source", payload.Value<string>("Source") ?? "Performance Journal");
        parameters.Add("Weightage", payload.Value<int?>("Weightage"));
        parameters.Add("Target", payload.Value<string>("Target"));
        parameters.Add("Achievement", payload.Value<string>("Achievement"));
        parameters.Add("UnitofMeasurement", payload.Value<int?>("UnitofMeasurement"));
        parameters.Add("TargetType", payload.Value<string>("TargetType"));
        parameters.Add("TargetTrackingPeriod", payload.Value<string>("TargetTrackingPeriod"));
        parameters.Add("AchievementPercentage", payload.Value<decimal?>("AchievementPercentage"));
        parameters.Add("IncludeInNextPeriod", includeNext);
        parameters.Add("scaleId", payload.Value<int?>("scaleId"));
        parameters.Add("Points", payload.Value<decimal?>("Points"));
        parameters.Add("Specific", payload.Value<string>("Specific"));
        parameters.Add("Measurable", payload.Value<string>("Measurable"));
        parameters.Add("AttainableResourcesRequired", payload.Value<string>("AttainableResourcesRequired"));
        parameters.Add("RelevantTypeID", payload.Value<int?>("RelevantTypeID"));
        parameters.Add("RelevantType", payload.Value<string>("RelevantType"));
        parameters.Add("Timeframe", payload.Value<DateTime?>("Timeframe"));
        parameters.Add("Timelines", payload.Value<string>("Timelines"));
        parameters.Add("Active", active);
        parameters.Add("Locked", locked);
        parameters.Add("CreateBy", empName);
        parameters.Add("InitialTerminal", terminalIP);
        parameters.Add("UpdateBy", isUpdate ? empName : null);
        parameters.Add("LastTerminal", isUpdate ? terminalIP : null);
        parameters.Add("PJCommentBy", payload.Value<int?>("PJCommentBy") ?? loginEmpId);
        parameters.Add("PJCommentDateTime", localCompanyTime);
        parameters.Add("Comments", payload.Value<string>("Comments"));
        parameters.Add("PJReflectionRequired", payload.Value<string>("PJReflectionRequired"));
        parameters.Add("PerformanceStatusId", payload.Value<int?>("PerformanceStatusId"));
        parameters.Add("PJCompleted", completed);
        parameters.Add("RatingDescId", payload.Value<int?>("RatingDescId"));
        parameters.Add("AttachmentFileName", payload.Value<string>("AttachmentFileName"));
        parameters.Add("AttachmentStoredName", payload.Value<string>("AttachmentStoredName"));
        parameters.Add("AttachmentRelativePath", payload.Value<string>("AttachmentRelativePath"));
        parameters.Add("AttachmentMimeType", payload.Value<string>("AttachmentMimeType"));
        parameters.Add("AttachmentSizeBytes", payload.Value<long?>("AttachmentSizeBytes"));
        parameters.Add("RemoveAttachment", removeAttachment);
        parameters.Add("Attachments", attachmentsTable.AsTableValuedParameter("dbo.AttachmentMetaUDT"));

        const string sql = @"
EXEC dbo.SP_PM_PJ_ObjectiveChatComments_Upsert
     @Operation = @Operation,
     @CompanyId = @CompanyId,
     @OCId = @OCId,
     @Id = @Id,
     @RId = @RId,
     @APId = @APId,
     @EmpId = @EmpId,
     @FID = @FID,
     @ObjectiveType = @ObjectiveType,
     @EmployeeObjective = @EmployeeObjective,
     @CompanyObjectiveId = @CompanyObjectiveId,
     @DivisionObjectiveId = @DivisionObjectiveId,
     @DepartmentObjectiveId = @DepartmentObjectiveId,
     @SubDepartmentObjectiveId = @SubDepartmentObjectiveId,
     @Source = @Source,
     @Weightage = @Weightage,
     @Target = @Target,
     @Achievement = @Achievement,
     @UnitofMeasurement = @UnitofMeasurement,
     @TargetType = @TargetType,
     @TargetTrackingPeriod = @TargetTrackingPeriod,
     @AchievementPercentage = @AchievementPercentage,
     @IncludeInNextPeriod = @IncludeInNextPeriod,
     @scaleId = @scaleId,
     @Points = @Points,
     @Specific = @Specific,
     @Measurable = @Measurable,
     @AttainableResourcesRequired = @AttainableResourcesRequired,
     @RelevantTypeID = @RelevantTypeID,
     @RelevantType = @RelevantType,
     @Timeframe = @Timeframe,
     @Timelines = @Timelines,
     @Active = @Active,
     @Locked = @Locked,
     @CreateBy = @CreateBy,
     @InitialTerminal = @InitialTerminal,
     @UpdateBy = @UpdateBy,
     @LastTerminal = @LastTerminal,
     @PJCommentBy = @PJCommentBy,
     @PJCommentDateTime = @PJCommentDateTime,
     @Comments = @Comments,
     @PJReflectionRequired = @PJReflectionRequired,
     @PerformanceStatusId = @PerformanceStatusId,
     @PJCompleted = @PJCompleted,
     @RatingDescId = @RatingDescId,
     @AttachmentFileName = @AttachmentFileName,
     @AttachmentStoredName = @AttachmentStoredName,
     @AttachmentRelativePath = @AttachmentRelativePath,
     @AttachmentMimeType = @AttachmentMimeType,
     @AttachmentSizeBytes = @AttachmentSizeBytes,
     @RemoveAttachment = @RemoveAttachment,
     @Attachments = @Attachments";

        var rawResult = await _dapperService.QuerySingleAsync<dynamic>(sql, parameters);
        var result = rawResult as IDictionary<string, object>;
        if (result != null && result.TryGetValue("AttachmentsJson", out var json) && json != null)
        {
            result["Attachments"] = JsonConvert.DeserializeObject<List<AttachmentMetaDto>>(json.ToString())
                                   ?? new List<AttachmentMetaDto>();
            result.Remove("AttachmentsJson");
        }

        if (result == null)
        {
            return new { OCId = ocId ?? 0, Operation = operation, Affected = 0 };
        }

        return result;
    }

    public async Task<object> GetObjectiveChatCommentsAsync(ObjectiveChatCommentsQuery dto, string clientIp)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(clientIp)) throw new ArgumentNullException(nameof(clientIp));

        var companyIdText = _utilities.GetCompanyId(clientIp);
        if (string.IsNullOrWhiteSpace(companyIdText) || !int.TryParse(companyIdText, out var companyIdFromSession))
            throw new InvalidOperationException("Unable to resolve CompanyId from session.");

        if (dto.CompanyId <= 0) dto.CompanyId = companyIdFromSession;

        var parameters = new DynamicParameters();
        parameters.Add("Id", dto.Id);
        parameters.Add("RId", dto.RId);
        parameters.Add("APId", dto.APId);
        parameters.Add("EmpId", dto.EmpId);
        parameters.Add("FID", dto.FID);
        parameters.Add("CompanyId", dto.CompanyId);

        const string sql = @"
EXEC dbo.SP_PM_PJ_ObjectiveChatComments_Get
     @Id = @Id,
     @RId = @RId,
     @APId = @APId,
     @EmpId = @EmpId,
     @FID = @FID,
     @CompanyId = @CompanyId";

        var rawRows = await _dapperService.QueryAsync<dynamic>(sql, parameters);
        var items = new List<object>();

        if (rawRows != null)
        {
            foreach (var raw in rawRows)
            {
                var row = raw as IDictionary<string, object>;
                if (row == null) continue;

                if (row.TryGetValue("AttachmentsJson", out var json) && json != null)
                {
                    row["Attachments"] = JsonConvert.DeserializeObject<List<AttachmentMetaDto>>(json.ToString())
                                       ?? new List<AttachmentMetaDto>();
                    row.Remove("AttachmentsJson");
                }
                else
                {
                    row["Attachments"] = new List<AttachmentMetaDto>();
                }

                items.Add(row);
            }
        }

        return new { items };

    }

    //private static DataTable BuildAttachmentTable(JToken attachmentsToken)
    //{
    //    var table = new DataTable();
    //    table.Columns.Add("AttachmentFileName", typeof(string));
    //    table.Columns.Add("AttachmentStoredName", typeof(string));
    //    table.Columns.Add("AttachmentRelativePath", typeof(string));
    //    table.Columns.Add("AttachmentMimeType", typeof(string));
    //    table.Columns.Add("AttachmentSizeBytes", typeof(long));

    //    if (attachmentsToken is JArray array)
    //    {
    //        foreach (var item in array)
    //        {
    //            if (item == null) continue;

    //            var storedName = item.Value<string>("AttachmentStoredName")
    //                          ?? item.Value<string>("storedName")
    //                          ?? item.Value<string>("StoredName");

    //            if (string.IsNullOrWhiteSpace(storedName))
    //                continue;

    //            table.Rows.Add(
    //                item.Value<string>("AttachmentFileName") ?? item.Value<string>("fileName"),
    //                storedName,
    //                item.Value<string>("AttachmentRelativePath") ?? item.Value<string>("relativePath"),
    //                item.Value<string>("AttachmentMimeType") ?? item.Value<string>("mimeType"),
    //                item.Value<long?>("AttachmentSizeBytes") ?? item.Value<long?>("sizeBytes") ?? 0L);
    //        }
    //    }

    //    return table;
    //}

    public async Task<IEnumerable<ObjectiveUnreadRow>> GetObjectiveUnreadCountsAsync(int empId, IEnumerable<int> objectiveIds)
    {
        var tvp = new DataTable();
        tvp.Columns.Add("Id", typeof(int));
        foreach (var id in objectiveIds?.Distinct() ?? Enumerable.Empty<int>())
            tvp.Rows.Add(id);

        var dp = new DynamicParameters();
        dp.Add("@EmpId", empId);
        dp.Add("@ObjectiveIds", tvp.AsTableValuedParameter("dbo.IntList"));

        var rows = await _dapperService.QueryAsync<ObjectiveUnreadRow>(
            "dbo.sp_PM_Objective_GetUnreadCounts",
            dp);

        return rows ?? Enumerable.Empty<ObjectiveUnreadRow>();
    }

    public async Task<int> MarkObjectiveThreadReadAsync(int empId, int objectiveId)
    {
        var dp = new DynamicParameters();
        dp.Add("@EmpId", empId);
        dp.Add("@ObjectiveId", objectiveId);

        return await _dapperService.ExecuteAsync(
            "dbo.sp_PM_Objective_MarkThreadRead",
            dp);
    }

    public async Task<IEnumerable<dynamic>?> GetResponsibilitiesAsync(int EmpId, int CompanyId)
    {
        try
        {

            const string sql = @"select top 1 Responsibilities, JobProfileId from tblEmpJobProfile where EmpId = @EmpId and CompanyId = @CompanyId order by JobProfileId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    EmpId = EmpId,
                    CompanyId = CompanyId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<object> UpdateResponsibilityAsync(int empId, int companyId, int jobProfileId, string responsibilities, string changeResponsibilityDetail, string clientIP)
    {
        if (string.IsNullOrWhiteSpace(clientIP))
            throw new ArgumentNullException(nameof(clientIP));

        // Get current logged-in user ID for audit trail
        var user = _utilities.GetCurrentUserMap(clientIP);
        var loginEmpId = user?.UserEmpId ?? 0;

        if (loginEmpId <= 0)
            throw new InvalidOperationException("Unable to determine logged-in user employee ID");

        try
        {
            const string sql = @"
                    UPDATE tblEmpJobProfile
                    SET Responsibilities = @Responsibilities, changeResponsibilityDetail = @changeResponsibilityDetail
                    WHERE EmpId = @EmpId
                      AND CompanyId = @CompanyId
                      AND JobProfileId = @JobProfileId";

            var rowsAffected = await _dapperService.ExecuteAsync(
                sql,
                new
                {
                    Responsibilities = responsibilities,
                    EmpId = empId,
                    CompanyId = companyId,
                    JobProfileId = jobProfileId,
                    changeResponsibilityDetail = changeResponsibilityDetail
                });

            return new
            {
                success = rowsAffected > 0,
                rowsAffected = rowsAffected,
                message = rowsAffected > 0 ? "Responsibility updated successfully" : "No records were updated"
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                rowsAffected = 0,
                message = $"Error updating responsibility: {ex.Message}"
            };
        }
    }

    public async Task<IEnumerable<dynamic>?> GetResponsibilitiesChangeHistoryAsync(int EmpId, int CompanyId)
    {
        try
        {

            const string sql = @"SELECT * 
                                        FROM dbo.fn_GetEmpJobProfileAuditTrailOfResponsibilities(@EmpId, @CompanyId)
                                        ORDER BY EntDate DESC, SNO";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    EmpId = EmpId,
                    CompanyId = CompanyId
                });

            return rows; // may be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<object> SetResponsibilityChatCommentsAsync(JObject payload, string clientIP)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(clientIP)) throw new ArgumentNullException(nameof(clientIP));

        var companyIdText = _utilities.GetCompanyId(clientIP);
        if (string.IsNullOrWhiteSpace(companyIdText) || !int.TryParse(companyIdText, out var companyId))
            throw new InvalidOperationException("Unable to resolve a numeric CompanyId.");

        // Get user information
        var user = _utilities.GetCurrentUserMap(clientIP);
        var UserID = user?.UserID ?? "SYSTEM";
        var UserEmpId = user?.UserEmpId ?? 0;
        var UserEmpCode = user?.UserEmpCode ?? string.Empty;
        var UserEmpName = user?.UserEmpName ?? "SYSTEM";
        var Terminal = _utilities.GetTerminalId() ?? Environment.MachineName;
        var TerminalIP = _utilities.GetTerminalIP() ?? string.Empty;
        var ApplicationID = _configuration.GetSection("CorsSettings:ApplicationId")?.Value ?? "ESSv4.5";

        // Get company-wise local datetime
        DateTime DateTime = System.DateTime.UtcNow;
        try
        {
            var scalar = _utilities.GetScalarData($"select dbo.fn_general_getlocaldatetimeCompanyWise({companyId})");
            if (scalar != null && System.DateTime.TryParse(Convert.ToString(scalar), out var parsed))
            {
                DateTime = parsed;
            }
        }
        catch
        {
            DateTime = System.DateTime.UtcNow;
        }

        var id = payload.Value<int?>("Id");
        var rawOp = payload.Value<string>("Operation");
        var operation = string.IsNullOrWhiteSpace(rawOp)
            ? (id.HasValue && id.Value > 0 ? "UPDATE" : "INSERT")
            : rawOp.Trim().ToUpperInvariant();

        if (operation != "INSERT" && operation != "UPDATE" && operation != "DELETE")
            throw new InvalidOperationException($"Unsupported operation '{operation}'.");

        if ((operation == "UPDATE" || operation == "DELETE") && (!id.HasValue || id.Value <= 0))
            throw new InvalidOperationException("Id is required for update or delete operations.");

        var active = payload.Value<bool?>("Active") ?? true;
        var attachmentsToken = payload["Attachments"];
        var removeAttachment = payload.Value<bool?>("RemoveAttachment") ?? false;
        var attachmentsTable = BuildAttachmentTable(attachmentsToken);

        var parameters = new DynamicParameters();
        parameters.Add("Operation", operation);
        parameters.Add("Id", id, DbType.Int32, ParameterDirection.InputOutput);
        parameters.Add("EmpId", payload.Value<int?>("EmpId"));
        parameters.Add("JobProfileId", payload.Value<int?>("JobProfileId"));
        parameters.Add("CompanyId", companyId);
        parameters.Add("CommentById", payload.Value<int?>("CommentById") ?? UserEmpId);
        parameters.Add("CommentByName", payload.Value<string>("CommentByName") ?? UserEmpName);
        parameters.Add("Comment", payload.Value<string>("Comment"));
        parameters.Add("Timestamp", DateTime);
        parameters.Add("ApplicationID", ApplicationID);
        parameters.Add("Active", active);

        // User information (SP will handle whether to use for Ent* or Upd* fields)
        parameters.Add("UserID", UserID);
        parameters.Add("UserEmpId", UserEmpId);
        parameters.Add("UserEmpCode", UserEmpCode);
        parameters.Add("UserEmpName", UserEmpName);
        parameters.Add("Date", DateTime);
        parameters.Add("Terminal", Terminal);
        parameters.Add("TerminalIP", TerminalIP);

        parameters.Add("RemoveAttachment", removeAttachment);
        parameters.Add("Attachments", attachmentsTable.AsTableValuedParameter("dbo.AttachmentMetaUDT"));

        const string sql = @"
                                  EXEC dbo.SP_PM_PJ_ResponsibilitiesChatComments_Upsert
                                       @Operation = @Operation,
                                       @Id = @Id OUTPUT,
                                       @EmpId = @EmpId,
                                       @JobProfileId = @JobProfileId,
                                       @CompanyId = @CompanyId,
                                       @CommentById = @CommentById,
                                       @CommentByName = @CommentByName,
                                       @Comment = @Comment,
                                       @Timestamp = @Timestamp,
                                       @ApplicationID = @ApplicationID,
                                       @Active = @Active,
                                       @UserID = @UserID,
                                       @UserEmpId = @UserEmpId,
                                       @UserEmpCode = @UserEmpCode,
                                       @UserEmpName = @UserEmpName,
                                       @Date = @Date,
                                       @Terminal = @Terminal,
                                       @TerminalIP = @TerminalIP,
                                       @RemoveAttachment = @RemoveAttachment,
                                       @Attachments = @Attachments";

        var rawResult = await _dapperService.QuerySingleAsync<dynamic>(sql, parameters);
        var result = rawResult as IDictionary<string, object>;

        if (result != null && result.TryGetValue("AttachmentsJson", out var json) && json != null)
        {
            result["Attachments"] = JsonConvert.DeserializeObject<List<AttachmentMetaDto>>(json.ToString())
                                   ?? new List<AttachmentMetaDto>();
            result.Remove("AttachmentsJson");
        }

        if (result == null)
        {
            return new { Id = id ?? 0, Operation = operation, Affected = 0 };
        }

        return result;
    }

    public async Task<object> GetResponsibilityChatCommentsAsync(ResponsibilityChatCommentsQuery dto, string clientIp)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(clientIp)) throw new ArgumentNullException(nameof(clientIp));

        var companyIdText = _utilities.GetCompanyId(clientIp);
        if (string.IsNullOrWhiteSpace(companyIdText) || !int.TryParse(companyIdText, out var companyIdFromSession))
            throw new InvalidOperationException("Unable to resolve CompanyId from session.");

        if (dto.CompanyId <= 0) dto.CompanyId = companyIdFromSession;

        var parameters = new DynamicParameters();
        parameters.Add("EmpId", dto.EmpId);
        parameters.Add("JobProfileId", dto.JobProfileId);
        parameters.Add("CompanyId", dto.CompanyId);

        const string sql = @"EXEC dbo.SP_PM_PJ_ResponsibilitiesChatComments_Get
                    @EmpId = @EmpId,
                    @JobProfileId = @JobProfileId,
                    @CompanyId = @CompanyId";

        return await _dapperService.QueryMultipleAsync(sql, parameters, async (multi) =>
        {
            // Read both result sets FIRST - before any processing
            var comments = (await multi.ReadAsync<dynamic>()).AsList();
            var attachments = (await multi.ReadAsync<dynamic>()).AsList();

            // Group attachments by comment Id
            var attachmentsByCommentId = new Dictionary<int, List<object>>();

            foreach (var attachment in attachments)
            {
                var attachmentDict = attachment as IDictionary<string, object>;
                if (attachmentDict == null) continue;

                // The "Id" field in attachments table represents the COMMENT ID
                var commentId = Convert.ToInt32(attachmentDict["Id"]);

                if (!attachmentsByCommentId.ContainsKey(commentId))
                {
                    attachmentsByCommentId[commentId] = new List<object>();
                }

                var attachmentObj = new Dictionary<string, object>
                {
                    ["AttachmentId"] = attachmentDict["AttachmentId"],
                    ["AttachmentFileName"] = attachmentDict["AttachmentFileName"],
                    ["AttachmentStoredName"] = attachmentDict["AttachmentStoredName"],
                    ["AttachmentRelativePath"] = attachmentDict["AttachmentRelativePath"],
                    ["AttachmentMimeType"] = attachmentDict["AttachmentMimeType"],
                    ["AttachmentSizeBytes"] = attachmentDict["AttachmentSizeBytes"],
                    ["CreatedOn"] = attachmentDict["CreatedOn"],
                    ["CreatedBy"] = attachmentDict["CreatedBy"]
                };

                attachmentsByCommentId[commentId].Add(attachmentObj);
            }

            // Attach the attachments to their respective comments
            var items = new List<object>();

            foreach (var comment in comments)
            {
                var commentDict = comment as IDictionary<string, object>;
                if (commentDict == null) continue;

                var commentId = Convert.ToInt32(commentDict["Id"]);

                commentDict["Attachments"] = attachmentsByCommentId.ContainsKey(commentId)
                    ? attachmentsByCommentId[commentId]
                    : new List<object>();

                items.Add(commentDict);
            }

            return (object)new { items };
        });
    }

    private static DataTable BuildAttachmentTable(JToken attachmentsToken)
    {
        var table = new DataTable();
        table.Columns.Add("AttachmentFileName", typeof(string));
        table.Columns.Add("AttachmentStoredName", typeof(string));
        table.Columns.Add("AttachmentRelativePath", typeof(string));
        table.Columns.Add("AttachmentMimeType", typeof(string));
        table.Columns.Add("AttachmentSizeBytes", typeof(long));

        if (attachmentsToken is JArray array)
        {
            foreach (var item in array)
            {
                if (item == null) continue;

                var storedName = item.Value<string>("AttachmentStoredName")
                            ?? item.Value<string>("attachmentStoredName")
                            ?? item.Value<string>("storedName")
                            ?? item.Value<string>("StoredName");

                if (string.IsNullOrWhiteSpace(storedName))
                    continue;

                table.Rows.Add(
                    item.Value<string>("AttachmentFileName")
                        ?? item.Value<string>("attachmentFileName")
                        ?? item.Value<string>("fileName"),
                    storedName,
                    item.Value<string>("AttachmentRelativePath")
                        ?? item.Value<string>("attachmentRelativePath")
                        ?? item.Value<string>("relativePath"),
                    item.Value<string>("AttachmentMimeType")
                        ?? item.Value<string>("attachmentMimeType")
                        ?? item.Value<string>("mimeType"),
                    item.Value<long?>("AttachmentSizeBytes")
                        ?? item.Value<long?>("attachmentSizeBytes")
                        ?? item.Value<long?>("sizeBytes")
                        ?? 0L);
            }
        }

        return table;
    }

    public async Task<object> GetResponsibilityUnreadCountAsync(JObject payload, string clientIP)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrWhiteSpace(clientIP)) throw new ArgumentNullException(nameof(clientIP));

        // Extract parameters from payload
        var empId = payload.Value<int?>("empId");
        var jobProfileId = payload.Value<int?>("jobProfileId");
        var companyId = payload.Value<int?>("companyId");

        if (!empId.HasValue || empId.Value <= 0)
            throw new ArgumentException("empId is required and must be greater than 0");

        if (!jobProfileId.HasValue || jobProfileId.Value <= 0)
            throw new ArgumentException("jobProfileId is required and must be greater than 0");

        if (!companyId.HasValue || companyId.Value <= 0)
            throw new ArgumentException("companyId is required and must be greater than 0");

        // Get current logged-in user ID
        var user = _utilities.GetCurrentUserMap(clientIP);
        var loginEmpId = user?.UserEmpId ?? 0;

        if (loginEmpId <= 0)
            throw new InvalidOperationException("Unable to determine logged-in user employee ID");

        // Build parameters for stored procedure
        var parameters = new DynamicParameters();
        parameters.Add("EmpId", empId.Value);
        parameters.Add("JobProfileId", jobProfileId.Value);
        parameters.Add("CompanyId", companyId.Value);
        parameters.Add("LoginEmpId", loginEmpId);

        const string sql = @"EXEC dbo.SP_PM_PJ_ResponsibilityChatComments_UnreadCount
                            @EmpId = @EmpId,
                            @JobProfileId = @JobProfileId,
                            @CompanyId = @CompanyId,
                            @LoginEmpId = @LoginEmpId";

        // Execute stored procedure
        var unreadCount = await _dapperService.QueryAsync<int>(sql, parameters);

        return new { result = unreadCount };
    }

    public async Task<int> MarkResponsibilityThreadReadAsync(int empId, int jobProfileId, int companyId, string clientIP)
    {
        if (string.IsNullOrWhiteSpace(clientIP))
            throw new ArgumentNullException(nameof(clientIP));

        // Get current logged-in user ID
        var user = _utilities.GetCurrentUserMap(clientIP);
        var loginEmpId = user?.UserEmpId ?? 0;

        if (loginEmpId <= 0)
            throw new InvalidOperationException("Unable to determine logged-in user employee ID");

        var dp = new DynamicParameters();
        dp.Add("@EmpId", empId);
        dp.Add("@JobProfileId", jobProfileId);
        dp.Add("@CompanyId", companyId);
        dp.Add("@LoginEmpId", loginEmpId);

        return await _dapperService.ExecuteAsync(
            "dbo.SP_PM_PJ_ResponsibilityChatComments_MarkRead",
            dp);
    }

    public async Task<IEnumerable<dynamic>?> GetAllCompaniesAsync()
    {
        try
        {
            const string sql = "SELECT CCODE, CompanyName FROM vCompanyManagement ORDER BY CCODE";

            var rows = await _dapperService.QueryAsync<dynamic>(sql);

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetDivisionsAsync(int EmpId, int CompanyId, string Culture)
    {
        try
        {
            var Param_DoNotDisplayInactiveSetups = 1;

            const string sql = "EXEC emp_GetDivision_With_Culture " +
                "@CompanyId = @CompanyId, " +
                "@Culture  = @Culture, " +
                "@Param_DoNotDisplayInactiveSetups = @Param_DoNotDisplayInactiveSetups";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    CompanyId = CompanyId,
                    Culture = Culture,
                    Param_DoNotDisplayInactiveSetups = Param_DoNotDisplayInactiveSetups
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetDepartmentsAsync(int DivId, int CompanyId, string Culture)
    {
        try
        {
            var Param_DoNotDisplayInactiveSetups = 1;

            const string sql = "EXEC emp_GetMainDepartment_With_Culture " +
                "@DivId = @DivId, @CompanyId = @CompanyId, @Culture = @Culture, " +
                "@Param_DoNotDisplayInactiveSetups = @Param_DoNotDisplayInactiveSetups";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    DivId = DivId,
                    CompanyId = CompanyId,
                    Culture = Culture,
                    Param_DoNotDisplayInactiveSetups = Param_DoNotDisplayInactiveSetups
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetSubDepartmentsAsync(int DivId, int DeptId, int CompanyId, string Culture)
    {
        try
        {
            var Param_DoNotDisplayInactiveSetups = 1;

            const string sql = @"Exec emp_GetSubDepartment_With_Culture @DivId = @DivId
                , @MdptId = @MdptId
                , @CompanyId = @CompanyId
                , @Culture  = @Culture
                , @Param_DoNotDisplayInactiveSetups = @Param_DoNotDisplayInactiveSetups";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    MdptId = DeptId,
                    DivId = DivId,
                    CompanyId = CompanyId,
                    Culture = Culture,
                    Param_DoNotDisplayInactiveSetups = Param_DoNotDisplayInactiveSetups
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetSetupsAsync(int smsid, int CompanyId)
    {
        try
        {

            const string sql = @"SELECT sdlid, Code, Name FROM tblSetupsDetail where smsid = @smsid and CompanyId = @CompanyId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    smsid = smsid,
                    CompanyId = CompanyId
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetRolesFromSetupsByGradeAsync(int smsid, int CompanyId, int Grade)
    {
        try
        {

            const string sql = @"SELECT sdlid, Code, Name FROM tblSetupsDetail where smsid = @smsid and CompanyId = @CompanyId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    smsid = smsid,
                    CompanyId = CompanyId
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetCountriesAsync(int CompanyId)
    {
        try
        {

            const string sql = @"Select cntid, Name, CompanyId from tblCountry where CompanyId = @CompanyId order by Name";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    CompanyId = CompanyId
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetCitiesAsync(int cntid, int CompanyId)
    {
        try
        {

            const string sql = @"Select ctyid, cntid, Code, Name, CompanyId from tblCity where cntid = @cntid and CompanyId = @CompanyId order by Name";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    cntid = cntid,
                    CompanyId = CompanyId
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<dynamic>?> GetEmployeeCurrentPositionInformationAsync(int EmpId, int CompanyId)
    {
        try
        {

            const string sql = @"SELECT 
                    ISNULL(RoleSD.smsid, DefaultRole.name) AS RoleId,
                    ISNULL(RoleSD.name, DefaultRole.name) AS Role,

                    EMP.JobId,
	                JobSD.Name as Grade,
    
                    EMP.DivId, 
                    DivSD.name AS Division,
    
                    EMP.MdptId, 
                    MdptSD.name AS Department,
    
                    EMP.dptId, 
                    DptSD.name AS SubDepartment,
    
                    EMP.dsgId, 
                    DsgSD.name AS Designation,
    
                    EMP.RegionId, 
                    RegionSD.name AS Region,
    
                    EMP.brnId, 
                    BrnSD.name AS Location,
    
                    EMP.BaseCntId, 
                    CNT.name AS CurrentStationCountry,
    
                    EMP.BaseId, 
                    CTY.name AS CurrentStationCity,
    
                    EMP.ReportTo, 
                    RTRIM(ISNULL(RT.FirstName, '') + ' ' + ISNULL(RT.LastName, '')) AS ReportToName,
    
                    EMP.ReportToCompany,
                    RTC.CompanyName AS ReportToCompanyName,

                    RoleSD.Responsibilities

                FROM tblEmployee EMP 

                -- Role from Job Profile
                OUTER APPLY (
                    SELECT TOP 1 SD.smsid, SD.name, Responsibilities 
                    FROM tblEmpJobProfile EJP 
                    INNER JOIN tblSetupsDetail SD ON SD.sdlid = EJP.RoleId 
                    WHERE EJP.EmpId = EMP.EmpId 
                    ORDER BY EJP.JobProfileId
                ) RoleSD

                -- Default Role
                LEFT JOIN tblSetupsDetail DefaultRole 
                    ON DefaultRole.CompanyId = @CompanyId 
                    AND DefaultRole.smsid = 189 
                    AND TRY_CAST(DefaultRole.Code AS INT) = 0

                -- Lookup joins
                LEFT JOIN tblSetupsDetail DivSD ON DivSD.sdlid = EMP.DivId
                LEFT JOIN tblSetupsDetail MdptSD ON MdptSD.sdlid = EMP.MdptId
                LEFT JOIN tblSetupsDetail DptSD ON DptSD.sdlid = EMP.dptId
                LEFT JOIN tblSetupsDetail DsgSD ON DsgSD.sdlid = EMP.dsgId
                LEFT JOIN tblSetupsDetail RegionSD ON RegionSD.sdlid = EMP.RegionId
                LEFT JOIN tblSetupsDetail BrnSD ON BrnSD.sdlid = EMP.brnId
                LEFT JOIN tblSetupsDetail JobSD ON JobSD.sdlid = EMP.JobId
                LEFT JOIN tblCountry CNT ON CNT.cntid = EMP.BaseCntId
                LEFT JOIN tblCity CTY ON CTY.ctyid = EMP.BaseId
                LEFT JOIN tblEmployee RT ON RT.EmpId = EMP.ReportTo
                LEFT JOIN EXTERNAL_SECURITY_COMPANYMANAGEMENT RTC ON RTC.CCODE = EMP.ReportToCompany

                WHERE EMP.EmpId = @EmpId";

            var rows = await _dapperService.QueryAsync<dynamic>(
                sql,
                new
                {
                    EmpId = EmpId,
                    CompanyId = CompanyId
                });

            return rows; // can be empty
        }
        catch
        {
            return null;
        }
    }



}

public class PJEmployeeInfo
{
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public string? DateofBirth { get; set; }
    public string? Division { get; set; }
    public string? Department { get; set; }
    public string? SubDepartment { get; set; }
    public string? Location { get; set; }
    public string? Region { get; set; }
    public string? Grade { get; set; }
    public string? PayrollGroup { get; set; }
    public string? Designation { get; set; }
    public string? EmployeeStatus { get; set; }
    public string? EmployeeCategory { get; set; }
    public string? EmployeeType { get; set; }
    public string? BasicSalary { get; set; }
    public string? DirectReportingTo { get; set; }
    public string? IndirectReportingTo { get; set; }
    public string? DateofConfirmation { get; set; }
    public string? GrossSalary { get; set; }
    public string? DateofJoining { get; set; }
}

public class Competencies
{
    public int Id { get; set; }
    public int RId { get; set; }
    public int CompID { get; set; }
    public int APId { get; set; }
    public int FId { get; set; }
    public int EmpId { get; set; }
    public int SubId { get; set; }
    public string? SubName { get; set; }
    public int SubSecId { get; set; }
    public string? SubSecName { get; set; }
    public int SubSecDetailId { get; set; }
    public string? Competency { get; set; }   // renamed to avoid clash with class name
    public int? RatingId { get; set; }
    public string? Comments { get; set; }
    public bool IsCoreCompetency { get; set; }
    public bool IncludeInNextPeriod { get; set; }
    public int JDId { get; set; }
    public string? JobDescription { get; set; }
    public string? FourthLevelDescription { get; set; }
    public string? Behavior { get; set; }
    public decimal Weightage { get; set; }
    public decimal WeightageSum { get; set; }
    public decimal? ActualWeightage { get; set; }
    public string? Type { get; set; }
    public string? Source { get; set; }
    public bool Active { get; set; }
    public bool Locked { get; set; }
    public DateTime CreateOn { get; set; }
    public string? CreateBy { get; set; }
    public DateTime? UpdateOn { get; set; }
    public string? UpdateBy { get; set; }
    public string? LastTerminal { get; set; }
    public int CompanyId { get; set; }
    public int TargetRatingID { get; set; }
    public int ReviewRatingID { get; set; }
    public string? ReviewRating { get; set; }
    public string? TargetRating { get; set; }
    public int MaxRating { get; set; }
    public int? CompetenctRating { get; set; }
    public decimal Score1 { get; set; }
    public decimal Score { get; set; }
    public int? AchievedRating { get; set; }
    public decimal TargetRatingValue { get; set; }
    public int? SelfRating { get; set; }
    public int? PreviousRating { get; set; }
    public string? ReviewFrequency { get; set; }
    public decimal PercentScore { get; set; }
    public decimal WeightedScore { get; set; }
    public int? PerformanceStatusId { get; set; }
}

public sealed class PoliciesResult
{
    public IReadOnlyList<dynamic> Table1 { get; init; } = Array.Empty<dynamic>();
    public IReadOnlyList<dynamic> Table2 { get; init; } = Array.Empty<dynamic>();
    public IReadOnlyList<dynamic> Table3 { get; init; } = Array.Empty<dynamic>();
    public IReadOnlyList<dynamic> Table4 { get; init; } = Array.Empty<dynamic>();
    public IReadOnlyList<dynamic> Table5 { get; init; } = Array.Empty<dynamic>();
}

public class AddCompetency
{
    public string? Competency { get; set; }
    public bool IncludeInNextPeriod { get; set; }
    public bool IsCoreCompetency { get; set; }
    public int JDId { get; set; }
    public string? JobCode { get; set; }
    public string? JobDescription { get; set; }
    public int JobProfileId { get; set; }
    public string? LearningNeeds { get; set; }
    public int SubId { get; set; }
    public string? SubName { get; set; }
    public int SubSecDetailId { get; set; }
    public int SubSecId { get; set; }
    public string? SubSecName { get; set; }
    public string? TargetRating { get; set; }
    public int TargetRatingID { get; set; }
    public int Weightage { get; set; }
    public string? RatingRationalRemarks { get; set; }
}

public sealed class SaveChatCommentDto
{
    public string Operation { get; set; } = "INSERT"; // INSERT | UPDATE | DELETE
    public int? CCId { get; set; }
    public List<AttachmentMetaDto> Attachments { get; set; } = new();
    public int? Id { get; set; }
    public int? RId { get; set; }
    public int? APId { get; set; }
    public int? EmpId { get; set; }
    public int? FID { get; set; }
    public int? CompId { get; set; }
    public int? JDId { get; set; }
    public int? SubId { get; set; }
    public string? SubName { get; set; }
    public int? SubSecId { get; set; }
    public string? SubSecName { get; set; }
    public int? SubSecDetailId { get; set; }
    public string? Competency { get; set; }
    public int? RatingId { get; set; }
    public int? Weightage { get; set; }
    public int? ActualWeightage { get; set; }
    public string? Type { get; set; }
    public string? Source { get; set; }
    public int? TargetRatingID { get; set; }
    public int? ReviewRatingID { get; set; }
    public string? ReviewFrequency { get; set; }
    public decimal? PercentScore { get; set; }
    public decimal? WeightedScore { get; set; }
    public bool? IncludeInNextPeriod { get; set; }
    public bool? IsCoreCompetency { get; set; }
    public string? Comments { get; set; }            
    public int PJCommentBy { get; set; }                
    public DateTime? PJCommentDateTime { get; set; }
    public string? PJReflectionRequired { get; set; }   
    public int? PerformanceStatusId { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentStoredName { get; set; }
    public string? AttachmentRelativePath { get; set; }
    public string? AttachmentMimeType { get; set; }
    public long? AttachmentSizeBytes { get; set; }
    public bool? RemoveAttachment { get; set; }
}

public sealed class MarkReadDto
{
    public int EmpId { get; set; }
    public int CompetencyId { get; set; }   // thread id == tblPMPerformanceReviewCC.Id
}

public sealed class UnreadReq
{
    public int EmpId { get; set; }
    public int[] Ids { get; set; } = Array.Empty<int>(); // competency/thread ids
}

public sealed class UnreadRow
{
    public int Id { get; set; }
    public int Unread { get; set; }
}

public class AddObjectives
{
    public int Id { get; set; }
    public int RId { get; set; }
    public int APId { get; set; }
    public int EmpId { get; set; }
    public int FID { get; set; }
    public string? ObjectiveDescription { get; set; }
    public string? Specific { get; set; }
    public string? Measurable { get; set; }
    public string? AttainableResourcesRequired { get; set; }

    public int RelevantTypeID { get; set; }
    public string? RelevantType { get; set; }

    public DateTime TimeFrame { get; set; }

    public string? Target { get; set; }
    public string? Timelines { get; set; }
    public decimal Weightage { get; set; }

    public bool Include { get; set; }

    public int CompanyObjectiveID { get; set; }
    public string? CompanyObjective { get; set; }

    public int DivisionObjectiveID { get; set; }
    public string? DivisionObjective { get; set; }

    public int DepartmentObjectiveID { get; set; }
    public string? DepartmentObjective { get; set; }

    public int SubDepartmentObjectiveID { get; set; }
    public string? SubDepartmentObjective { get; set; }
}

public sealed class ObjectiveChatCommentDto
{
    public string? Operation { get; set; }
    public int? OCId { get; set; }
    public int? Id { get; set; }
    public int? RId { get; set; }
    public int? APId { get; set; }
    public int? EmpId { get; set; }
    public int? FID { get; set; }
    public string? ObjectiveType { get; set; }
    public string? EmployeeObjective { get; set; }
    public int? CompanyObjectiveId { get; set; }
    public int? DivisionObjectiveId { get; set; }
    public int? DepartmentObjectiveId { get; set; }
    public int? SubDepartmentObjectiveId { get; set; }
    public string? Source { get; set; }
    public int? Weightage { get; set; }
    public string? Target { get; set; }
    public string? Achievement { get; set; }
    public int? UnitofMeasurement { get; set; }
    public string? TargetType { get; set; }
    public string? TargetTrackingPeriod { get; set; }
    public decimal? AchievementPercentage { get; set; }
    public bool? IncludeInNextPeriod { get; set; }
    public int? scaleId { get; set; }
    public decimal? Points { get; set; }
    public string? Specific { get; set; }
    public string? Measurable { get; set; }
    public string? AttainableResourcesRequired { get; set; }
    public int? RelevantTypeID { get; set; }
    public string? RelevantType { get; set; }
    public DateTime? Timeframe { get; set; }
    public string? Timelines { get; set; }
    public bool? Active { get; set; }
    public bool? Locked { get; set; }
    public int? PJCommentBy { get; set; }
    public DateTime? PJCommentDateTime { get; set; }
    public string? Comments { get; set; }
    public string? PJReflectionRequired { get; set; }
    public int? PerformanceStatusId { get; set; }
    public bool? PJCompleted { get; set; }
    public int? RatingDescId { get; set; }

    public List<AttachmentMetaDto>? Attachments { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentStoredName { get; set; }
    public string? AttachmentRelativePath { get; set; }
    public string? AttachmentMimeType { get; set; }
    public long? AttachmentSizeBytes { get; set; }
    public bool? RemoveAttachment { get; set; }
}

public sealed class ObjectiveChatCommentsQuery
{
    public int Id { get; set; }
    public int RId { get; set; }
    public int APId { get; set; }
    public int EmpId { get; set; }
    public int FID { get; set; }
    public int CompanyId { get; set; }
}

public sealed class ObjectiveUnreadRequest
{
    public int EmpId { get; set; }
    public IEnumerable<int> ObjectiveIds { get; set; } = Array.Empty<int>();
}

public sealed class ObjectiveMarkReadRequest
{
    public int EmpId { get; set; }
    public int ObjectiveId { get; set; }
}

public sealed class ObjectiveUnreadRow
{
    public int Id { get; set; }
    public int Unread { get; set; }
}

public class ResponsibilityChatCommentsQuery
{
    public int EmpId { get; set; }
    public int JobProfileId { get; set; }
    public int CompanyId { get; set; }
}

public sealed class AttachmentMetaDto
{
    public int? AttachmentId { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentStoredName { get; set; }
    public string? AttachmentRelativePath { get; set; }
    public string? AttachmentMimeType { get; set; }
    public long? AttachmentSizeBytes { get; set; }
}
