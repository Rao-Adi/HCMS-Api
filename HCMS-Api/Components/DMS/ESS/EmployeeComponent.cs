using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using Npgsql;
using System.Data;
using static HCMS_Api.Controllers.HCMS.Common.SecurityController;

namespace HCMS_Api.Components.DMS.ESS;

public class EmployeeComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public EmployeeComponent(
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
        //string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        //_dataservice.BeginProcess(connectionString);

    }

    public async Task<int> InsertDepartmentMasterAsync(tblSetupsdetail input)
    {
        string sql = @"
                INSERT INTO tblSetupsdetail (
                    sdlid,
                    Code,
                    smsid,
                    Name,
                    LastUpdate,
                    CompanyId,
                    UserEmpId,
                    EntOperation,
                    EntDate,
                    UserEmpCode,
                    EntTerminalIP,
                    EntTerminal,
                    UserEmpName,
                    FormId,
                    UserId,
                    ApplicationId,
                    ChngTimeStamp,
                    Inactive
                )
                VALUES (
                    @sdlid,
                    @Code,
                    @smsid,
                    @Name,
                    @LastUpdate,
                    @CompanyId,
                    @UserEmpId,
                    @EntOperation,
                    @EntDate,
                    @UserEmpCode,
                    @EntTerminalIP,
                    @EntTerminal,
                    @UserEmpName,
                    @FormId,
                    @UserId,
                    @ApplicationId,
                    @ChngTimeStamp,
                    @Inactive
                )
                RETURNING sdlid;
            ";

        var parameters = new Dictionary<string, object>
        {
            { "@sdlid",           input.sdlid },
            { "@Code",            input.Code            ?? (object)DBNull.Value },
            { "@smsid",           input.smsid },
            { "@Name",            input.Name            ?? (object)DBNull.Value },
            { "@LastUpdate",      input.LastUpdate },
            { "@CompanyId",       input.CompanyId},
            { "@UserEmpId",       input.UserEmpId},
            { "@EntOperation",    input.EntOperation    ?? (object)DBNull.Value },
            { "@EntDate",         input.EntDate},
            { "@UserEmpCode",     input.UserEmpCode     ?? (object)DBNull.Value },
            { "@EntTerminalIP",   input.EntTerminalIP   ?? (object)DBNull.Value },
            { "@EntTerminal",     input.EntTerminal     ?? (object)DBNull.Value },
            { "@UserEmpName",     input.UserEmpName     ?? (object)DBNull.Value },
            { "@FormId",          input.FormId          ?? (object)DBNull.Value },
            { "@UserId",          "userId" },
            { "@ApplicationId",   input.ApplicationId   ?? (object)DBNull.Value },
            { "@ChngTimeStamp",   input.ChngTimeStamp },
            { "@Inactive",        input.Inactive }
        };

        int newId = Convert.ToInt32(_common.ExecuteScalarQuery(sql, parameters));

        return newId;
        //const string sql = @"
        //    INSERT INTO tblSetupsdetail (
        //        sdlid, Code, smsid, Name, LastUpdate, CompanyId, UserEmpId, 
        //        EntOperation, EntDate, UserEmpCode, EntTerminalIP, EntTerminal, 
        //        UserEmpName, FormId, UserId, ApplicationId, ChngTimeStamp, Inactive
        //    ) VALUES (
        //        @sdlid, @Code, @smsid, @Name, @LastUpdate, @CompanyId, @UserEmpId,
        //        @EntOperation, @EntDate, @UserEmpCode, @EntTerminalIP, @EntTerminal,
        //        @UserEmpName, @FormId, @UserId, @ApplicationId, @ChngTimeStamp, @Inactive
        //    );
        //    SELECT LASTVAL();"; // For PostgreSQL - returns the last inserted ID


        //using (var connection = new Npgsql.NpgsqlConnection(_connectionString))
        //{
        //    using (var command = new Npgsql.NpgsqlCommand(sql, connection))
        //    {
        //        // Add parameters
        //        command.Parameters.AddWithValue("@sdlid", department.sdlid);
        //        command.Parameters.AddWithValue("@Code", (object)department.Code ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@smsid", department.smsid);
        //        command.Parameters.AddWithValue("@Name", (object)department.Name ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@LastUpdate", department.LastUpdate);
        //        command.Parameters.AddWithValue("@CompanyId", (object)department.CompanyId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpId", (object)department.UserEmpId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntOperation", (object)department.EntOperation ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntDate", (object)department.EntDate ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpCode", (object)department.UserEmpCode ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntTerminalIP", (object)department.EntTerminalIP ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntTerminal", (object)department.EntTerminal ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpName", (object)department.UserEmpName ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@FormId", (object)department.FormId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserId", (object)department.UserId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@ApplicationId", (object)department.ApplicationId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@ChngTimeStamp", department.ChngTimeStamp);
        //        command.Parameters.AddWithValue("@Inactive", (object)department.Inactive ?? DBNull.Value);

        //        await connection.OpenAsync();
        //        var result = await command.ExecuteScalarAsync();
        //        return Convert.ToInt32(result);
        //    }
        //}
    }


    public int InsertDepartmentBudget(tblDeptstrMaster input)
    {
        string sql = @"
            INSERT INTO tblDeptstrMaster (
                dptMId, MdptId, CompanyId, Budgeted, Approved, divId, UserId, ApplicationID,
                FormId, UserEmpId, UserEmpName, UserEmpCode, EntTerminal, EntTerminalIP, EntOperation, EntDate
            )
            VALUES (
                @dptMId, @MdptId, @CompanyId, @Budgeted, @Approved, @divId, @UserId, @ApplicationID,
                @FormId, @UserEmpId, @UserEmpName, @UserEmpCode, @EntTerminal, @EntTerminalIP, @EntOperation, @EntDate
            )
            RETURNING dptMId;
        ";

        var parameters = new Dictionary<string, object>
            {
                { "@dptMId",        input.dptMId },
                { "@MdptId",        input.MdptId},
                { "@CompanyId",     input.CompanyId },
                { "@Budgeted",      input.Budgeted},
                { "@Approved",      input.Approved},
                { "@divId",         input.divId},
                { "@UserId",        input.UserId        ?? (object)DBNull.Value },
                { "@ApplicationID", input.ApplicationID ?? (object)DBNull.Value },
                { "@FormId",        input.FormId        ?? (object)DBNull.Value },
                { "@UserEmpId",     input.UserEmpId },
                { "@UserEmpName",   input.UserEmpName   ?? (object)DBNull.Value },
                { "@UserEmpCode",   input.UserEmpCode   ?? (object)DBNull.Value },
                { "@EntTerminal",   input.EntTerminal   ?? (object)DBNull.Value },
                { "@EntTerminalIP", input.EntTerminalIP ?? (object)DBNull.Value },
                { "@EntOperation",  input.EntOperation  ?? (object)DBNull.Value },
                { "@EntDate",       input.EntDate}
            };

        int newId = Convert.ToInt32(_common.ExecuteScalarQuery(sql, parameters));

        return newId;

        //const string sql = @"
        //    INSERT INTO tblDeptstrMaster (
        //        dptMId, MdptId, CompanyId, Budgeted, Approved, divId, UserId,
        //        ApplicationID, FormId, UserEmpId, UserEmpName, UserEmpCode,
        //        EntTerminal, EntTerminalIP, EntOperation, EntDate
        //    ) VALUES (
        //        @dptMId, @MdptId, @CompanyId, @Budgeted, @Approved, @divId, @UserId,
        //        @ApplicationID, @FormId, @UserEmpId, @UserEmpName, @UserEmpCode,
        //        @EntTerminal, @EntTerminalIP, @EntOperation, @EntDate
        //    );
        //    SELECT SCOPE_IDENTITY();";

        //using (var connection = new SqlConnection(_connectionString))
        //{
        //    using (var command = new SqlCommand(sql, connection))
        //    {
        //        command.Parameters.AddWithValue("@dptMId", budget.dptMId);
        //        command.Parameters.AddWithValue("@MdptId", (object)budget.MdptId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@CompanyId", (object)budget.CompanyId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@Budgeted", (object)budget.Budgeted ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@Approved", (object)budget.Approved ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@divId", (object)budget.divId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserId", (object)budget.UserId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@ApplicationID", (object)budget.ApplicationID ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@FormId", (object)budget.FormId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpId", (object)budget.UserEmpId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpName", (object)budget.UserEmpName ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpCode", (object)budget.UserEmpCode ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntTerminal", (object)budget.EntTerminal ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntTerminalIP", (object)budget.EntTerminalIP ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntOperation", (object)budget.EntOperation ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntDate", (object)budget.EntDate ?? DBNull.Value);

        //        connection.Open();
        //        return Convert.ToInt32(command.ExecuteScalar());
        //    }
        //}
    }

    public int InsertDepartmentDetail(tblDeptstrDetail input)
    {
        string sql = @"INSERT INTO tblDeptstrDetail (dptDId, dptMId, dptId, Budgeted, Approved, companyId, UserId, ApplicationID, FormId, UserEmpId, UserEmpName, UserEmpCode, EntTerminal, EntTerminalIP, EntOperation, EntDate)
                   VALUES (@dptDId, @dptMId, @dptId, @Budgeted, @Approved, @companyId, @UserId, @ApplicationID, @FormId, @UserEmpId, @UserEmpName, @UserEmpCode, @EntTerminal, @EntTerminalIP, @EntOperation, @EntDate) RETURNING dptDId;";

        var parameters = new Dictionary<string, object>
        {
            { "@dptDId",        input.dptDId },
            { "@dptMId",        input.dptMId },
            { "@dptId",         input.dptId  },
            { "@Budgeted",      input.Budgeted },
            { "@Approved",      input.Approved  },
            { "@companyId",     input.companyId  },
            { "@UserId",        input.UserId        ?? (object)DBNull.Value },
            { "@ApplicationID", input.ApplicationID ?? (object)DBNull.Value },
            { "@FormId",        input.FormId        ?? (object)DBNull.Value },
            { "@UserEmpId",     input.UserEmpId },
            { "@UserEmpName",   input.UserEmpName   ?? (object)DBNull.Value },
            { "@UserEmpCode",   input.UserEmpCode   ?? (object)DBNull.Value },
            { "@EntTerminal",   input.EntTerminal   ?? (object)DBNull.Value },
            { "@EntTerminalIP", input.EntTerminalIP ?? (object)DBNull.Value },
            { "@EntOperation",  input.EntOperation  ?? (object)DBNull.Value },
            { "@EntDate",       input.EntDate  }
        };

        int newId = Convert.ToInt32(_common.ExecuteScalarQuery(sql, parameters));

        return newId;

        //const string sql = @"
        //    INSERT INTO tblDeptstrDetail (
        //        dptDId, dptMId, dptId, Budgeted, Approved, companyId, UserId,
        //        ApplicationID, FormId, UserEmpId, UserEmpName, UserEmpCode,
        //        EntTerminal, EntTerminalIP, EntOperation, EntDate
        //    ) VALUES (
        //        @dptDId, @dptMId, @dptId, @Budgeted, @Approved, @companyId, @UserId,
        //        @ApplicationID, @FormId, @UserEmpId, @UserEmpName, @UserEmpCode,
        //        @EntTerminal, @EntTerminalIP, @EntOperation, @EntDate
        //    );
        //    SELECT SCOPE_IDENTITY();";

        //using (var connection = new SqlConnection(_connectionString))
        //{
        //    using (var command = new SqlCommand(sql, connection))
        //    {
        //        command.Parameters.AddWithValue("@dptDId", detail.dptDId);
        //        command.Parameters.AddWithValue("@dptMId", (object)detail.dptMId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@dptId", (object)detail.dptId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@Budgeted", (object)detail.Budgeted ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@Approved", (object)detail.Approved ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@companyId", (object)detail.companyId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserId", (object)detail.UserId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@ApplicationID", (object)detail.ApplicationID ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@FormId", (object)detail.FormId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpId", (object)detail.UserEmpId ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpName", (object)detail.UserEmpName ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@UserEmpCode", (object)detail.UserEmpCode ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntTerminal", (object)detail.EntTerminal ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntTerminalIP", (object)detail.EntTerminalIP ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntOperation", (object)detail.EntOperation ?? DBNull.Value);
        //        command.Parameters.AddWithValue("@EntDate", (object)detail.EntDate ?? DBNull.Value);

        //        connection.Open();
        //        return Convert.ToInt32(command.ExecuteScalar());
        //    }
        //}
    }


    public int InsertJobProfile(TblEmpJobProfile input)
    {
        string sql = @"INSERT INTO TblEmpJobProfile (
        JobProfileId, EmpId, JobCode, JobCDate, JobTitle, MdptId, dptId, JobId, baseId, typId, ReportingRelationship, KeyRelationshipInt,
        KeyRelationshipExt, AcademicQualifications, WorkExperience, JobSummary, KeyPerformanceIndicators, Responsibilities, Accountabilities, EOWorkCondition,
        EmpDate, MgrEmpId, MgrDate, HrEmpId, HRDate, CompanyId, AppDoc, DivId, DirectRptTo, DCompanyId, DGradeId, DDsgId, DDivId, DMdptId, DDptId, DBaseId,
        InDirectRptTo, IndCompanyId, IndGradeId, IndDsgId, IndDivId, IndMdptId, IndDptId, IndBaseId, PAgeRange, PrefGender, EYears, DsgId, RExpYears,
        chkqual, chkcert, chktrain, isDescripancyInCompetency, isDescripancyInKPI, JdId, Active, PageRangeTo, EYearsTo, RExpYearsTo, IncPackage, OtherBenifit,
        SalaryRemarks, visaStatus, SalaryAmountFrom, SalaryAmountTo, SalaryCurrencyId, PrefReligion, AssignedDate, UserId, ApplicationID, FormId, UserEmpId,
        UserEmpName, UserEmpCode, EntTerminal, EntTerminalIP, EntOperation, EntDate, EmoIntlProfiling, RoleId, DRoleId, IndRoleId, Received, JDChangeVersionNo,
        AcknowledgeOn, AcknowledgeByUserID, AcknowledgeTerminal, AcknowledgeByEmpID, changeResponsibilityDetail)
        VALUES (
        @JobProfileId, @EmpId, @JobCode, @JobCDate, @JobTitle, @MdptId, @dptId, @JobId, @baseId, @typId, @ReportingRelationship, @KeyRelationshipInt,
        @KeyRelationshipExt, @AcademicQualifications, @WorkExperience, @JobSummary, @KeyPerformanceIndicators, @Responsibilities, @Accountabilities, @EOWorkCondition,
        @EmpDate, @MgrEmpId, @MgrDate, @HrEmpId, @HRDate, @CompanyId, @AppDoc, @DivId, @DirectRptTo, @DCompanyId, @DGradeId, @DDsgId, @DDivId, @DMdptId, @DDptId, @DBaseId,
        @InDirectRptTo, @IndCompanyId, @IndGradeId, @IndDsgId, @IndDivId, @IndMdptId, @IndDptId, @IndBaseId, @PAgeRange, @PrefGender, @EYears, @DsgId, @RExpYears,
        @chkqual, @chkcert, @chktrain, @isDescripancyInCompetency, @isDescripancyInKPI, @JdId, @Active, @PageRangeTo, @EYearsTo, @RExpYearsTo, @IncPackage, @OtherBenifit,
        @SalaryRemarks, @visaStatus, @SalaryAmountFrom, @SalaryAmountTo, @SalaryCurrencyId, @PrefReligion, @AssignedDate, @UserId, @ApplicationID, @FormId, @UserEmpId,
        @UserEmpName, @UserEmpCode, @EntTerminal, @EntTerminalIP, @EntOperation, @EntDate, @EmoIntlProfiling, @RoleId, @DRoleId, @IndRoleId, @Received, @JDChangeVersionNo,
        @AcknowledgeOn, @AcknowledgeByUserID, @AcknowledgeTerminal, @AcknowledgeByEmpID, @changeResponsibilityDetail)
        RETURNING JobProfileId;";


        var parameters = new Dictionary<string, object>
        {
            { "@JobProfileId", input.JobProfileId },
            { "@EmpId", input.EmpId },
            { "@JobCode", input.JobCode },
            { "@JobCDate", input.JobCDate },
            { "@JobTitle", input.JobTitle ?? (object)DBNull.Value },
            { "@MdptId", input.MdptId },
            { "@dptId", input.DptId },
            { "@JobId", input.JobId },
            { "@baseId", input.BaseId },
            { "@typId", input.TypId },
            { "@ReportingRelationship", input.ReportingRelationship ?? (object)DBNull.Value },
            { "@KeyRelationshipInt", input.KeyRelationshipInt ?? (object)DBNull.Value },
            { "@KeyRelationshipExt", input.KeyRelationshipExt ?? (object)DBNull.Value },
            { "@AcademicQualifications", input.AcademicQualifications ?? (object)DBNull.Value },
            { "@WorkExperience", input.WorkExperience ?? (object)DBNull.Value },
            { "@JobSummary", input.JobSummary ?? (object)DBNull.Value },
            { "@KeyPerformanceIndicators", input.KeyPerformanceIndicators ?? (object)DBNull.Value },
            { "@Responsibilities", input.Responsibilities ?? (object)DBNull.Value },
            { "@Accountabilities", input.Accountabilities ?? (object)DBNull.Value },
            { "@EOWorkCondition", input.EOWorkCondition ?? (object)DBNull.Value },
            { "@EmpDate", input.EmpDate ?? (object)DBNull.Value },
            { "@MgrEmpId", input.MgrEmpId ?? (object)DBNull.Value },
            { "@MgrDate", input.MgrDate ?? (object)DBNull.Value },
            { "@HrEmpId", input.HrEmpId ?? (object)DBNull.Value },
            { "@HRDate", input.HRDate ?? (object)DBNull.Value },
            { "@CompanyId", input.CompanyId },
            { "@AppDoc", input.AppDoc ?? (object)DBNull.Value },
            { "@DivId", input.DivId ?? (object)DBNull.Value },
            { "@DirectRptTo", input.DirectRptTo ?? (object)DBNull.Value },
            { "@DCompanyId", input.DCompanyId ?? (object)DBNull.Value },
            { "@DGradeId", input.DGradeId ?? (object)DBNull.Value },
            { "@DDsgId", input.DDsgId ?? (object)DBNull.Value },
            { "@DDivId", input.DDivId ?? (object)DBNull.Value },
            { "@DMdptId", input.DMdptId ?? (object)DBNull.Value },
            { "@DDptId", input.DDptId ?? (object)DBNull.Value },
            { "@DBaseId", input.DBaseId ?? (object)DBNull.Value },
            { "@InDirectRptTo", input.InDirectRptTo ?? (object)DBNull.Value },
            { "@IndCompanyId", input.IndCompanyId ?? (object)DBNull.Value },
            { "@IndGradeId", input.IndGradeId ?? (object)DBNull.Value },
            { "@IndDsgId", input.IndDsgId ?? (object)DBNull.Value },
            { "@IndDivId", input.IndDivId ?? (object)DBNull.Value },
            { "@IndMdptId", input.IndMdptId ?? (object)DBNull.Value },
            { "@IndDptId", input.IndDptId ?? (object)DBNull.Value },
            { "@IndBaseId", input.IndBaseId ?? (object)DBNull.Value },
            { "@PAgeRange", input.PAgeRange ?? (object)DBNull.Value },
            { "@PrefGender", input.PrefGender ?? (object)DBNull.Value },
            { "@EYears", input.EYears ?? (object)DBNull.Value },
            { "@DsgId", input.DsgId ?? (object)DBNull.Value },
            { "@RExpYears", input.RExpYears ?? (object)DBNull.Value },
            { "@chkqual", input.Chkqual ?? (object)DBNull.Value },
            { "@chkcert", input.Chkcert ?? (object)DBNull.Value },
            { "@chktrain", input.Chktrain ?? (object)DBNull.Value },
            { "@isDescripancyInCompetency", input.IsDescripancyInCompetency ?? (object)DBNull.Value },
            { "@isDescripancyInKPI", input.IsDescripancyInKPI ?? (object)DBNull.Value },
            { "@JdId", input.JdId ?? (object)DBNull.Value },
            { "@Active", input.Active ?? (object)DBNull.Value },
            { "@PageRangeTo", input.PageRangeTo ?? (object)DBNull.Value },
            { "@EYearsTo", input.EYearsTo ?? (object)DBNull.Value },
            { "@RExpYearsTo", input.RExpYearsTo ?? (object)DBNull.Value },
            { "@IncPackage", input.IncPackage ?? (object)DBNull.Value },
            { "@OtherBenifit", input.OtherBenifit ?? (object)DBNull.Value },
            { "@SalaryRemarks", input.SalaryRemarks ?? (object)DBNull.Value },
            { "@visaStatus", input.VisaStatus ?? (object)DBNull.Value },
            { "@SalaryAmountFrom", input.SalaryAmountFrom ?? (object)DBNull.Value },
            { "@SalaryAmountTo", input.SalaryAmountTo ?? (object)DBNull.Value },
            { "@SalaryCurrencyId", input.SalaryCurrencyId ?? (object)DBNull.Value },
            { "@PrefReligion", input.PrefReligion ?? (object)DBNull.Value },
            { "@AssignedDate", input.AssignedDate ?? (object)DBNull.Value },
            { "@UserId", input.UserId ?? (object)DBNull.Value },
            { "@ApplicationID", input.ApplicationID ?? (object)DBNull.Value },
            { "@FormId", input.FormId ?? (object)DBNull.Value },
            { "@UserEmpId", input.UserEmpId ?? (object)DBNull.Value },
            { "@UserEmpName", input.UserEmpName ?? (object)DBNull.Value },
            { "@UserEmpCode", input.UserEmpCode ?? (object)DBNull.Value },
            { "@EntTerminal", input.EntTerminal ?? (object)DBNull.Value },
            { "@EntTerminalIP", input.EntTerminalIP ?? (object)DBNull.Value },
            { "@EntOperation", input.EntOperation ?? (object)DBNull.Value },
            { "@EntDate", input.EntDate ?? (object)DBNull.Value },
            { "@EmoIntlProfiling", input.EmoIntlProfiling ?? (object)DBNull.Value },
            { "@RoleId", input.RoleId ?? (object)DBNull.Value },
            { "@DRoleId", input.DRoleId ?? (object)DBNull.Value },
            { "@IndRoleId", input.IndRoleId ?? (object)DBNull.Value },
            { "@Received", input.Received ?? (object)DBNull.Value },
            { "@JDChangeVersionNo", input.JDChangeVersionNo ?? (object)DBNull.Value },
            { "@AcknowledgeOn", input.AcknowledgeOn ?? (object)DBNull.Value },
            { "@AcknowledgeByUserID", input.AcknowledgeByUserID ?? (object)DBNull.Value },
            { "@AcknowledgeTerminal", input.AcknowledgeTerminal ?? (object)DBNull.Value },
            { "@AcknowledgeByEmpID", input.AcknowledgeByEmpID ?? (object)DBNull.Value },
            { "@changeResponsibilityDetail", input.ChangeResponsibilityDetail ?? (object)DBNull.Value }
        };

        return Convert.ToInt32(_common.ExecuteScalarQuery(sql, parameters));


        //const string sql = @"
        //    INSERT INTO TblEmpJobProfile (
        //        JobProfileId, EmpId, JobCode, JobCDate, JobTitle, MdptId, dptId,
        //        JobId, baseId, typId, ReportingRelationship, KeyRelationshipInt,
        //        KeyRelationshipExt, AcademicQualifications, WorkExperience, JobSummary,
        //        KeyPerformanceIndicators, Responsibilities, Accountabilities, EOWorkCondition,
        //        EmpDate, MgrEmpId, MgrDate, HrEmpId, HRDate, CompanyId, AppDoc, DivId,
        //        DirectRptTo, DCompanyId, DGradeId, DDsgId, DDivId, DMdptId, DDptId, DBaseId,
        //        InDirectRptTo, IndCompanyId, IndGradeId, IndDsgId, IndDivId, IndMdptId,
        //        IndDptId, IndBaseId, PAgeRange, PrefGender, EYears, DsgId, RExpYears,
        //        chkqual, chkcert, chktrain, isDescripancyInCompetency, isDescripancyInKPI,
        //        JdId, Active, PageRangeTo, EYearsTo, RExpYearsTo, IncPackage, OtherBenifit,
        //        SalaryRemarks, visaStatus, SalaryAmountFrom, SalaryAmountTo, SalaryCurrencyId,
        //        PrefReligion, AssignedDate, UserId, ApplicationID, FormId, UserEmpId,
        //        UserEmpName, UserEmpCode, EntTerminal, EntTerminalIP, EntOperation, EntDate,
        //        EmoIntlProfiling, RoleId, DRoleId, IndRoleId, Received, JDChangeVersionNo,
        //        AcknowledgeOn, AcknowledgeByUserID, AcknowledgeTerminal, AcknowledgeByEmpID,
        //        changeResponsibilityDetail
        //    ) VALUES (
        //        @JobProfileId, @EmpId, @JobCode, @JobCDate, @JobTitle, @MdptId, @dptId,
        //        @JobId, @baseId, @typId, @ReportingRelationship, @KeyRelationshipInt,
        //        @KeyRelationshipExt, @AcademicQualifications, @WorkExperience, @JobSummary,
        //        @KeyPerformanceIndicators, @Responsibilities, @Accountabilities, @EOWorkCondition,
        //        @EmpDate, @MgrEmpId, @MgrDate, @HrEmpId, @HRDate, @CompanyId, @AppDoc, @DivId,
        //        @DirectRptTo, @DCompanyId, @DGradeId, @DDsgId, @DDivId, @DMdptId, @DDptId, @DBaseId,
        //        @InDirectRptTo, @IndCompanyId, @IndGradeId, @IndDsgId, @IndDivId, @IndMdptId,
        //        @IndDptId, @IndBaseId, @PAgeRange, @PrefGender, @EYears, @DsgId, @RExpYears,
        //        @chkqual, @chkcert, @chktrain, @isDescripancyInCompetency, @isDescripancyInKPI,
        //        @JdId, @Active, @PageRangeTo, @EYearsTo, @RExpYearsTo, @IncPackage, @OtherBenifit,
        //        @SalaryRemarks, @visaStatus, @SalaryAmountFrom, @SalaryAmountTo, @SalaryCurrencyId,
        //        @PrefReligion, @AssignedDate, @UserId, @ApplicationID, @FormId, @UserEmpId,
        //        @UserEmpName, @UserEmpCode, @EntTerminal, @EntTerminalIP, @EntOperation, @EntDate,
        //        @EmoIntlProfiling, @RoleId, @DRoleId, @IndRoleId, @Received, @JDChangeVersionNo,
        //        @AcknowledgeOn, @AcknowledgeByUserID, @AcknowledgeTerminal, @AcknowledgeByEmpID,
        //        @changeResponsibilityDetail
        //    );
        //    SELECT SCOPE_IDENTITY();";

        //using (var connection = new SqlConnection(_connectionString))
        //{
        //    using (var command = new SqlCommand(sql, connection))
        //    {
        //        // Add parameters
        //        AddJobProfileParameters(command, jobProfile);

        //        connection.Open();
        //        return Convert.ToInt32(command.ExecuteScalar());
        //    }
        //}
    }

    //private void AddJobProfileParameters(SqlCommand command, TblEmpJobProfile jobProfile)
    //{
    //    command.Parameters.AddWithValue("@JobProfileId", jobProfile.JobProfileId);
    //    command.Parameters.AddWithValue("@EmpId", jobProfile.EmpId);
    //    command.Parameters.AddWithValue("@JobCode", jobProfile.JobCode);
    //    command.Parameters.AddWithValue("@JobCDate", jobProfile.JobCDate);
    //    command.Parameters.AddWithValue("@JobTitle", (object)jobProfile.JobTitle ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MdptId", jobProfile.MdptId);
    //    command.Parameters.AddWithValue("@dptId", jobProfile.DptId);
    //    command.Parameters.AddWithValue("@JobId", jobProfile.JobId);
    //    command.Parameters.AddWithValue("@baseId", jobProfile.BaseId);
    //    command.Parameters.AddWithValue("@typId", jobProfile.TypId);
    //    command.Parameters.AddWithValue("@ReportingRelationship", (object)jobProfile.ReportingRelationship ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@KeyRelationshipInt", (object)jobProfile.KeyRelationshipInt ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@KeyRelationshipExt", (object)jobProfile.KeyRelationshipExt ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AcademicQualifications", (object)jobProfile.AcademicQualifications ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@WorkExperience", (object)jobProfile.WorkExperience ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@JobSummary", (object)jobProfile.JobSummary ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@KeyPerformanceIndicators", (object)jobProfile.KeyPerformanceIndicators ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Responsibilities", (object)jobProfile.Responsibilities ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Accountabilities", (object)jobProfile.Accountabilities ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EOWorkCondition", (object)jobProfile.EOWorkCondition ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmpDate", (object)jobProfile.EmpDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MgrEmpId", (object)jobProfile.MgrEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MgrDate", (object)jobProfile.MgrDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@HrEmpId", (object)jobProfile.HrEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@HRDate", (object)jobProfile.HRDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@CompanyId", jobProfile.CompanyId);
    //    command.Parameters.AddWithValue("@AppDoc", (object)jobProfile.AppDoc ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DivId", (object)jobProfile.DivId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DirectRptTo", (object)jobProfile.DirectRptTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DCompanyId", (object)jobProfile.DCompanyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DGradeId", (object)jobProfile.DGradeId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DDsgId", (object)jobProfile.DDsgId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DDivId", (object)jobProfile.DDivId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DMdptId", (object)jobProfile.DMdptId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DDptId", (object)jobProfile.DDptId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DBaseId", (object)jobProfile.DBaseId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@InDirectRptTo", (object)jobProfile.InDirectRptTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndCompanyId", (object)jobProfile.IndCompanyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndGradeId", (object)jobProfile.IndGradeId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndDsgId", (object)jobProfile.IndDsgId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndDivId", (object)jobProfile.IndDivId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndMdptId", (object)jobProfile.IndMdptId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndDptId", (object)jobProfile.IndDptId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndBaseId", (object)jobProfile.IndBaseId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PAgeRange", (object)jobProfile.PAgeRange ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PrefGender", (object)jobProfile.PrefGender ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EYears", (object)jobProfile.EYears ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DsgId", (object)jobProfile.DsgId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RExpYears", (object)jobProfile.RExpYears ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@chkqual", (object)jobProfile.Chkqual ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@chkcert", (object)jobProfile.Chkcert ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@chktrain", (object)jobProfile.Chktrain ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@isDescripancyInCompetency", (object)jobProfile.IsDescripancyInCompetency ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@isDescripancyInKPI", (object)jobProfile.IsDescripancyInKPI ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@JdId", (object)jobProfile.JdId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Active", (object)jobProfile.Active ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PageRangeTo", (object)jobProfile.PageRangeTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EYearsTo", (object)jobProfile.EYearsTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RExpYearsTo", (object)jobProfile.RExpYearsTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IncPackage", (object)jobProfile.IncPackage ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OtherBenifit", (object)jobProfile.OtherBenifit ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SalaryRemarks", (object)jobProfile.SalaryRemarks ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@visaStatus", (object)jobProfile.VisaStatus ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SalaryAmountFrom", (object)jobProfile.SalaryAmountFrom ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SalaryAmountTo", (object)jobProfile.SalaryAmountTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SalaryCurrencyId", (object)jobProfile.SalaryCurrencyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PrefReligion", (object)jobProfile.PrefReligion ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AssignedDate", (object)jobProfile.AssignedDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserId", (object)jobProfile.UserId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ApplicationID", (object)jobProfile.ApplicationID ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FormId", (object)jobProfile.FormId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserEmpId", (object)jobProfile.UserEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserEmpName", (object)jobProfile.UserEmpName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserEmpCode", (object)jobProfile.UserEmpCode ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntTerminal", (object)jobProfile.EntTerminal ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntTerminalIP", (object)jobProfile.EntTerminalIP ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntOperation", (object)jobProfile.EntOperation ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntDate", (object)jobProfile.EntDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmoIntlProfiling", (object)jobProfile.EmoIntlProfiling ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RoleId", (object)jobProfile.RoleId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DRoleId", (object)jobProfile.DRoleId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IndRoleId", (object)jobProfile.IndRoleId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Received", (object)jobProfile.Received ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@JDChangeVersionNo", (object)jobProfile.JDChangeVersionNo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AcknowledgeOn", (object)jobProfile.AcknowledgeOn ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AcknowledgeByUserID", (object)jobProfile.AcknowledgeByUserID ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AcknowledgeTerminal", (object)jobProfile.AcknowledgeTerminal ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AcknowledgeByEmpID", (object)jobProfile.AcknowledgeByEmpID ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@changeResponsibilityDetail", (object)jobProfile.ChangeResponsibilityDetail ?? DBNull.Value);
    //}

    public async Task<int> InsertEmployeeAsync(tblEmployee input)
    {
        string sql = @"INSERT INTO tblEmployee (
        EmpId, EmpCode, CompanyId, EmpRefNo, FirstName, MidName, LastName, TitleID, Initial, Fname, gndId, NIC, NICnew, NTN, DateofBirth,
        Address, ctyId, Phone, PhoneCntCode, PhoneCtyCode, PhoneNumber, Phone2, Phone2CntCode, Phone2CtyCode, Phone2Number, Mobile,
        MobileCode, MobileNumber, Email, rlgId, mrtId, bldId, nationalityid, qlfId, EmgPerson, EmgPhone, EmgAddress, MdptId, dptId, dsgId,
        cmpid, brnId, typId, DateJoin, DateConfirm, CDueDate, ExtDate, JobDes, Active, JobId, NotePad, ReportTo, Dotted, Medical, GShift,
        Gross, PDate, LastIncDate, Height, Weight, EyeColor, Glass, MedicalHistory, Disabilities, DisReason, PGId, RelName, RelWorking,
        RelationshipId, RelPositionId, SectId, AreaId, MTId, DivId, UId, PWD, VDesigId, VDesigId2, BaseId, RegionId, MTeamId, TeamId, ExpYr,
        EditBy, ConfirmBy, Hold, IsDirty, DispID, Identification, SDWId, DomId, InsurerId, GrpInsrNum, GrpInsrAmt, SumInsured, AssetStatus,
        LReason, Shift, AutoPresent, PGIds, CountryId, OfferDate, AppId, VaccinationHistory, ContractExpireDate, ProratedLeave, PassportNo,
        FatherNIC, Dgid, Dutyid, PayrollStatus, SLICAmount, TotalSLICAmount, OPDID, Email2, EmgMobile, MdptIdCurrent, MdptIdOld, dptIdCurrent,
        dptIdOld, dsgIdCurrent, dsgIdOld, JobIdCurrent, PGIdCurrent, ReportToCurrent, CNICExpiryDate, InternExpiryDate, HiringChecklistProcess,
        OldEmpId, LeavingDate, InterCompanyTransfer, SourceCompanyId, TargetCompanyId, OriginalCompanyId, SourceEmpId, TargetEmpId,
        OriginalEmpId, InterCompanyInduction, lastWorkingDate, ReportingDate, BaseCntId, DispCntId, ReportToCompany, relId,
        DottedReportToCompany, PassportExpiryDate, UserId, FormId, UserEmpId, UserEmpName, EntTerminal, EntTerminalIP, UserEmpCode,
        VisaNumer, VisaExpiryDate, VisaNumber, nextincDate, Matchedwithblacklist, processmanualmap, duplicatedRecordId,
        duplicatedMapEmpId, IdCardRemarks, FamilyCardNo, IqamaNo, IqamaExpiryHijri, IqamaExpiryGregorian, CurrSpnsName,
        SpnsTransferable, SpnsType, SpnsCountry, SpnsCity, SpnsContactDetails, SpnsNatureOfBusiness, IqamaProfession,
        SpnsExpiryHijri, SpnsExpiryGregorian, FirstNameArabic, MidNameArabic, LastNameArabic, TitleIDArabic,
        ValidDrivingLicenseKSA, HstOfPersecution, HstOfPenalties, PendingCases, SpnsCategory, NoOfSpnsChangedOfVisa,
        EmpCategoryId, AutoPresentFromDate, AutoPresentToDate, TransactionSource, SalaryChangedStatus, ReviewTransactionId,
        HrSeries, IsLeaveAllocated, IsEmployeeSalary, IsHiringChecklistFinalized, IsEmployeeProfileExtended,
        IsEmployeeExpenseEntitlement, IsEmployeeJD, IsApprovalForPayroll, IsUserId, PreferredCulture, DomCntId,
        EntDate, ApplicationId, RetirementDate, SalaryReviewChangedStatus, TimeStamp, JobIdOld, PGIdOld, ReportToOld,
        FlexiShift, FlexiType, RequiredHours, EditByUser, secondment, PhotoPath, CompanyShortName, ACids,
        DrivingLicenseNo, DrivingLicenseExpiryDate, isSpouseEmployed, isAnyOtherIncomeSource, isAnyPhysicalDisability,
        OtherIncomeSourceDetails, PhysicalDisabilityDetails, ResidentialStatusId, FatherHusbandPhone,
        FatherHusbandOccupation, isOwnConveyance, ConveyanceType, ConveyanceMake, ConveyanceModel,
        ConveyanceYear, ConveyanceRegisterationNo, PhoneExtension, SittingLocation)
        VALUES (
        @EmpId, @EmpCode, @CompanyId, @EmpRefNo, @FirstName, @MidName, @LastName, @TitleID, @Initial, @Fname, @gndId, @NIC, @NICnew, @NTN, @DateofBirth,
        @Address, @ctyId, @Phone, @PhoneCntCode, @PhoneCtyCode, @PhoneNumber, @Phone2, @Phone2CntCode, @Phone2CtyCode, @Phone2Number, @Mobile,
        @MobileCode, @MobileNumber, @Email, @rlgId, @mrtId, @bldId, @nationalityid, @qlfId, @EmgPerson, @EmgPhone, @EmgAddress, @MdptId, @dptId, @dsgId,
        @cmpid, @brnId, @typId, @DateJoin, @DateConfirm, @CDueDate, @ExtDate, @JobDes, @Active, @JobId, @NotePad, @ReportTo, @Dotted, @Medical, @GShift,
        @Gross, @PDate, @LastIncDate, @Height, @Weight, @EyeColor, @Glass, @MedicalHistory, @Disabilities, @DisReason, @PGId, @RelName, @RelWorking,
        @RelationshipId, @RelPositionId, @SectId, @AreaId, @MTId, @DivId, @UId, @PWD, @VDesigId, @VDesigId2, @BaseId, @RegionId, @MTeamId, @TeamId, @ExpYr,
        @EditBy, @ConfirmBy, @Hold, @IsDirty, @DispID, @Identification, @SDWId, @DomId, @InsurerId, @GrpInsrNum, @GrpInsrAmt, @SumInsured, @AssetStatus,
        @LReason, @Shift, @AutoPresent, @PGIds, @CountryId, @OfferDate, @AppId, @VaccinationHistory, @ContractExpireDate, @ProratedLeave, @PassportNo,
        @FatherNIC, @Dgid, @Dutyid, @PayrollStatus, @SLICAmount, @TotalSLICAmount, @OPDID, @Email2, @EmgMobile, @MdptIdCurrent, @MdptIdOld, @dptIdCurrent,
        @dptIdOld, @dsgIdCurrent, @dsgIdOld, @JobIdCurrent, @PGIdCurrent, @ReportToCurrent, @CNICExpiryDate, @InternExpiryDate, @HiringChecklistProcess,
        @OldEmpId, @LeavingDate, @InterCompanyTransfer, @SourceCompanyId, @TargetCompanyId, @OriginalCompanyId, @SourceEmpId, @TargetEmpId,
        @OriginalEmpId, @InterCompanyInduction, @lastWorkingDate, @ReportingDate, @BaseCntId, @DispCntId, @ReportToCompany, @relId,
        @DottedReportToCompany, @PassportExpiryDate, @UserId, @FormId, @UserEmpId, @UserEmpName, @EntTerminal, @EntTerminalIP, @UserEmpCode,
        @VisaNumer, @VisaExpiryDate, @VisaNumber, @nextincDate, @Matchedwithblacklist, @processmanualmap, @duplicatedRecordId,
        @duplicatedMapEmpId, @IdCardRemarks, @FamilyCardNo, @IqamaNo, @IqamaExpiryHijri, @IqamaExpiryGregorian, @CurrSpnsName,
        @SpnsTransferable, @SpnsType, @SpnsCountry, @SpnsCity, @SpnsContactDetails, @SpnsNatureOfBusiness, @IqamaProfession,
        @SpnsExpiryHijri, @SpnsExpiryGregorian, @FirstNameArabic, @MidNameArabic, @LastNameArabic, @TitleIDArabic,
        @ValidDrivingLicenseKSA, @HstOfPersecution, @HstOfPenalties, @PendingCases, @SpnsCategory, @NoOfSpnsChangedOfVisa,
        @EmpCategoryId, @AutoPresentFromDate, @AutoPresentToDate, @TransactionSource, @SalaryChangedStatus, @ReviewTransactionId,
        @HrSeries, @IsLeaveAllocated, @IsEmployeeSalary, @IsHiringChecklistFinalized, @IsEmployeeProfileExtended,
        @IsEmployeeExpenseEntitlement, @IsEmployeeJD, @IsApprovalForPayroll, @IsUserId, @PreferredCulture, @DomCntId,
        @EntDate, @ApplicationId, @RetirementDate, @SalaryReviewChangedStatus, @TimeStamp, @JobIdOld, @PGIdOld, @ReportToOld,
        @FlexiShift, @FlexiType, @RequiredHours, @EditByUser, @secondment, @PhotoPath, @CompanyShortName, @ACids,
        @DrivingLicenseNo, @DrivingLicenseExpiryDate, @isSpouseEmployed, @isAnyOtherIncomeSource, @isAnyPhysicalDisability,
        @OtherIncomeSourceDetails, @PhysicalDisabilityDetails, @ResidentialStatusId, @FatherHusbandPhone,
        @FatherHusbandOccupation, @isOwnConveyance, @ConveyanceType, @ConveyanceMake, @ConveyanceModel,
        @ConveyanceYear, @ConveyanceRegisterationNo, @PhoneExtension, @SittingLocation)
        RETURNING EmpId;";


        var parameters = new Dictionary<string, object>
        {
            { "@EmpId", input.EmpId },
            { "@EmpCode", input.EmpCode ?? (object)DBNull.Value },
            { "@CompanyId", input.CompanyId ?? (object)DBNull.Value },
            { "@EmpRefNo", input.EmpRefNo ?? (object)DBNull.Value },
            { "@FirstName", input.FirstName ?? (object)DBNull.Value },
            { "@MidName", input.MidName ?? (object)DBNull.Value },
            { "@LastName", input.LastName ?? (object)DBNull.Value },
            { "@TitleID", input.TitleID ?? (object)DBNull.Value },
            { "@Initial", input.Initial ?? (object)DBNull.Value },
            { "@Fname", input.Fname ?? (object)DBNull.Value },
            { "@gndId", input.gndId ?? (object)DBNull.Value },
            { "@NIC", input.NIC ?? (object)DBNull.Value },
            { "@NICnew", input.NICnew ?? (object)DBNull.Value },
            { "@NTN", input.NTN ?? (object)DBNull.Value },
            { "@DateofBirth", input.DateofBirth ?? (object)DBNull.Value },
            { "@Address", input.Address ?? (object)DBNull.Value },
            { "@ctyId", input.ctyId ?? (object)DBNull.Value },
            { "@Phone", input.Phone ?? (object)DBNull.Value },
            { "@PhoneCntCode", input.PhoneCntCode ?? (object)DBNull.Value },
            { "@PhoneCtyCode", input.PhoneCtyCode ?? (object)DBNull.Value },
            { "@PhoneNumber", input.PhoneNumber ?? (object)DBNull.Value },
            { "@Phone2", input.Phone2 ?? (object)DBNull.Value },
            { "@Phone2CntCode", input.Phone2CntCode ?? (object)DBNull.Value },
            { "@Phone2CtyCode", input.Phone2CtyCode ?? (object)DBNull.Value },
            { "@Phone2Number", input.Phone2Number ?? (object)DBNull.Value },
            { "@Mobile", input.Mobile ?? (object)DBNull.Value },
            { "@MobileCode", input.MobileCode ?? (object)DBNull.Value },
            { "@MobileNumber", input.MobileNumber ?? (object)DBNull.Value },
            { "@Email", input.Email ?? (object)DBNull.Value },
            { "@rlgId", input.rlgId ?? (object)DBNull.Value },
            { "@mrtId", input.mrtId ?? (object)DBNull.Value },
            { "@bldId", input.bldId ?? (object)DBNull.Value },
            { "@nationalityid", input.nationalityid ?? (object)DBNull.Value },
            { "@qlfId", input.qlfId ?? (object)DBNull.Value },
            { "@EmgPerson", input.EmgPerson ?? (object)DBNull.Value },
            { "@EmgPhone", input.EmgPhone ?? (object)DBNull.Value },
            { "@EmgAddress", input.EmgAddress ?? (object)DBNull.Value },
            { "@MdptId", input.MdptId ?? (object)DBNull.Value },
            { "@dptId", input.dptId },
            { "@dsgId", input.dsgId ?? (object)DBNull.Value },
            { "@cmpid", input.cmpid ?? (object)DBNull.Value },
            { "@brnId", input.brnId ?? (object)DBNull.Value },
            { "@typId", input.typId ?? (object)DBNull.Value },
            { "@DateJoin", input.DateJoin ?? (object)DBNull.Value },
            { "@DateConfirm", input.DateConfirm ?? (object)DBNull.Value },
            { "@CDueDate", input.CDueDate ?? (object)DBNull.Value },
            { "@ExtDate", input.ExtDate ?? (object)DBNull.Value },
            { "@JobDes", input.JobDes ?? (object)DBNull.Value },
            { "@Active", input.Active ?? (object)DBNull.Value },
            { "@JobId", input.JobId ?? (object)DBNull.Value },
            { "@NotePad", input.NotePad ?? (object)DBNull.Value },
            { "@ReportTo", input.ReportTo ?? (object)DBNull.Value },
            { "@Dotted", input.Dotted ?? (object)DBNull.Value },
            { "@Medical", input.Medical  },
            { "@GShift", input.GShift   },
            { "@Gross", input.Gross },
            { "@PDate", input.PDate ?? (object)DBNull.Value },
            { "@LastIncDate", input.LastIncDate ?? (object)DBNull.Value },
            { "@Height", input.Height ?? (object)DBNull.Value },
            { "@Weight", input.Weight ?? (object)DBNull.Value },
            { "@EyeColor", input.EyeColor ?? (object)DBNull.Value },
            { "@Glass", input.Glass ?? (object)DBNull.Value },
            { "@MedicalHistory", input.MedicalHistory ?? (object)DBNull.Value },
            { "@Disabilities", input.Disabilities ?? (object)DBNull.Value },
            { "@DisReason", input.DisReason ?? (object)DBNull.Value },
            { "@PGId", input.PGId ?? (object)DBNull.Value },
            { "@RelName", input.RelName ?? (object)DBNull.Value },
            { "@RelWorking", input.RelWorking ?? (object)DBNull.Value },
            { "@RelationshipId", input.RelationshipId ?? (object)DBNull.Value },
            { "@RelPositionId", input.RelPositionId ?? (object)DBNull.Value },
            { "@SectId", input.SectId ?? (object)DBNull.Value },
            { "@AreaId", input.AreaId ?? (object)DBNull.Value },
            { "@MTId", input.MTId ?? (object)DBNull.Value },
            { "@DivId", input.DivId ?? (object)DBNull.Value },
            { "@UId", input.UId ?? (object)DBNull.Value },
            { "@PWD", input.PWD ?? (object)DBNull.Value },
            { "@VDesigId", input.VDesigId ?? (object)DBNull.Value },
            { "@VDesigId2", input.VDesigId2 ?? (object)DBNull.Value },
            { "@BaseId", input.BaseId ?? (object)DBNull.Value },
            { "@RegionId", input.RegionId ?? (object)DBNull.Value },
            { "@MTeamId", input.MTeamId ?? (object)DBNull.Value },
            { "@TeamId", input.TeamId ?? (object)DBNull.Value },
            { "@ExpYr", input.ExpYr ?? (object)DBNull.Value },
            { "@EditBy", input.EditBy ?? (object)DBNull.Value },
            { "@ConfirmBy", input.ConfirmBy ?? (object)DBNull.Value },
            { "@Hold", input.Hold ?? (object)DBNull.Value },
            { "@IsDirty", input.IsDirty ?? (object)DBNull.Value },
            { "@DispID", input.DispID ?? (object)DBNull.Value },
            { "@Identification", input.Identification ?? (object)DBNull.Value },
            { "@SDWId", input.SDWId ?? (object)DBNull.Value },
            { "@DomId", input.DomId ?? (object)DBNull.Value },
            { "@InsurerId", input.InsurerId ?? (object)DBNull.Value },
            { "@GrpInsrNum", input.GrpInsrNum ?? (object)DBNull.Value },
            { "@GrpInsrAmt", input.GrpInsrAmt ?? (object)DBNull.Value },
            { "@SumInsured", input.SumInsured ?? (object)DBNull.Value },
            { "@AssetStatus", input.AssetStatus ?? (object)DBNull.Value },
            { "@LReason", input.LReason ?? (object)DBNull.Value },
            { "@Shift", input.Shift ?? (object)DBNull.Value },
            { "@AutoPresent", input.AutoPresent  },
            { "@PGIds", input.PGIds ?? (object)DBNull.Value },
            { "@CountryId", input.CountryId ?? (object)DBNull.Value },
            { "@OfferDate", input.OfferDate ?? (object)DBNull.Value },
            { "@AppId", input.AppId ?? (object)DBNull.Value },
            { "@VaccinationHistory", input.VaccinationHistory ?? (object)DBNull.Value },
            { "@ContractExpireDate", input.ContractExpireDate ?? (object)DBNull.Value },
            { "@ProratedLeave", input.ProratedLeave ?? (object)DBNull.Value },
            { "@PassportNo", input.PassportNo ?? (object)DBNull.Value },
            { "@FatherNIC", input.FatherNIC ?? (object)DBNull.Value },
            { "@Dgid", input.Dgid ?? (object)DBNull.Value },
            { "@Dutyid", input.Dutyid ?? (object)DBNull.Value },
            { "@PayrollStatus", input.PayrollStatus ?? (object)DBNull.Value },
            { "@SLICAmount", input.SLICAmount ?? (object)DBNull.Value },
            { "@TotalSLICAmount", input.TotalSLICAmount ?? (object)DBNull.Value },
            { "@OPDID", input.OPDID ?? (object)DBNull.Value },
            { "@Email2", input.Email2 ?? (object)DBNull.Value },
            { "@EmgMobile", input.EmgMobile ?? (object)DBNull.Value },
            { "@MdptIdCurrent", input.MdptIdCurrent ?? (object)DBNull.Value },
            { "@MdptIdOld", input.MdptIdOld ?? (object)DBNull.Value },
            { "@dptIdCurrent", input.dptIdCurrent ?? (object)DBNull.Value },
            { "@dptIdOld", input.dptIdOld ?? (object)DBNull.Value },
            { "@dsgIdCurrent", input.dsgIdCurrent ?? (object)DBNull.Value },
            { "@dsgIdOld", input.dsgIdOld ?? (object)DBNull.Value },
            { "@JobIdCurrent", input.JobIdCurrent ?? (object)DBNull.Value },
            { "@PGIdCurrent", input.PGIdCurrent ?? (object)DBNull.Value },
            { "@ReportToCurrent", input.ReportToCurrent ?? (object)DBNull.Value },
            { "@CNICExpiryDate", input.CNICExpiryDate ?? (object)DBNull.Value },
            { "@InternExpiryDate", input.InternExpiryDate ?? (object)DBNull.Value },
            { "@HiringChecklistProcess", input.HiringChecklistProcess  },
            { "@OldEmpId", input.OldEmpId ?? (object)DBNull.Value },
            { "@LeavingDate", input.LeavingDate ?? (object)DBNull.Value },
            { "@InterCompanyTransfer", input.InterCompanyTransfer },
            { "@SourceCompanyId", input.SourceCompanyId ?? (object)DBNull.Value },
            { "@TargetCompanyId", input.TargetCompanyId ?? (object)DBNull.Value },
            { "@OriginalCompanyId", input.OriginalCompanyId ?? (object)DBNull.Value },
            { "@SourceEmpId", input.SourceEmpId ?? (object)DBNull.Value },
            { "@TargetEmpId", input.TargetEmpId ?? (object)DBNull.Value },
            { "@OriginalEmpId", input.OriginalEmpId ?? (object)DBNull.Value },
            { "@InterCompanyInduction", input.InterCompanyInduction  },
            { "@lastWorkingDate", input.lastWorkingDate ?? (object)DBNull.Value },
            { "@ReportingDate", input.ReportingDate ?? (object)DBNull.Value },
            { "@BaseCntId", input.BaseCntId ?? (object)DBNull.Value },
            { "@DispCntId", input.DispCntId ?? (object)DBNull.Value },
            { "@ReportToCompany", input.ReportToCompany ?? (object)DBNull.Value },
            { "@relId", input.relId ?? (object)DBNull.Value },
            { "@DottedReportToCompany", input.DottedReportToCompany ?? (object)DBNull.Value },
            { "@PassportExpiryDate", input.PassportExpiryDate ?? (object)DBNull.Value },
            { "@UserId", input.UserId ?? (object)DBNull.Value },
            { "@FormId", input.FormId ?? (object)DBNull.Value },
            { "@UserEmpId", input.UserEmpId ?? (object)DBNull.Value },
            { "@UserEmpName", input.UserEmpName ?? (object)DBNull.Value },
            { "@EntTerminal", input.EntTerminal ?? (object)DBNull.Value },
            { "@EntTerminalIP", input.EntTerminalIP ?? (object)DBNull.Value },
            { "@UserEmpCode", input.UserEmpCode ?? (object)DBNull.Value },
            { "@VisaNumer", input.VisaNumer ?? (object)DBNull.Value },
            { "@VisaExpiryDate", input.VisaExpiryDate ?? (object)DBNull.Value },
            { "@VisaNumber", input.VisaNumber ?? (object)DBNull.Value },
            { "@nextincDate", input.nextincDate ?? (object)DBNull.Value },
            { "@Matchedwithblacklist", input.Matchedwithblacklist ?? (object)DBNull.Value },
            { "@processmanualmap", input.processmanualmap ?? (object)DBNull.Value },
            { "@duplicatedRecordId", input.duplicatedRecordId ?? (object)DBNull.Value },
            { "@duplicatedMapEmpId", input.duplicatedMapEmpId ?? (object)DBNull.Value },
            { "@IdCardRemarks", input.IdCardRemarks ?? (object)DBNull.Value },
            { "@FamilyCardNo", input.FamilyCardNo ?? (object)DBNull.Value },
            { "@IqamaNo", input.IqamaNo ?? (object)DBNull.Value },
            { "@IqamaExpiryHijri", input.IqamaExpiryHijri ?? (object)DBNull.Value },
            { "@IqamaExpiryGregorian", input.IqamaExpiryGregorian ?? (object)DBNull.Value },
            { "@CurrSpnsName", input.CurrSpnsName ?? (object)DBNull.Value },
            { "@SpnsTransferable", input.SpnsTransferable ?? (object)DBNull.Value },
            { "@SpnsType", input.SpnsType ?? (object)DBNull.Value },
            { "@SpnsCountry", input.SpnsCountry ?? (object)DBNull.Value },
            { "@SpnsCity", input.SpnsCity ?? (object)DBNull.Value },
            { "@SpnsContactDetails", input.SpnsContactDetails ?? (object)DBNull.Value },
            { "@SpnsNatureOfBusiness", input.SpnsNatureOfBusiness ?? (object)DBNull.Value },
            { "@IqamaProfession", input.IqamaProfession ?? (object)DBNull.Value },
            { "@SpnsExpiryHijri", input.SpnsExpiryHijri ?? (object)DBNull.Value },
            { "@SpnsExpiryGregorian", input.SpnsExpiryGregorian ?? (object)DBNull.Value },
            { "@FirstNameArabic", input.FirstNameArabic ?? (object)DBNull.Value },
            { "@MidNameArabic", input.MidNameArabic ?? (object)DBNull.Value },
            { "@LastNameArabic", input.LastNameArabic ?? (object)DBNull.Value },
            { "@TitleIDArabic", input.TitleIDArabic ?? (object)DBNull.Value },

            { "@ValidDrivingLicenseKSA", input.ValidDrivingLicenseKSA ?? (object)DBNull.Value },
            { "@HstOfPersecution", input.HstOfPersecution ?? (object)DBNull.Value },
            { "@HstOfPenalties", input.HstOfPenalties ?? (object)DBNull.Value },
            { "@PendingCases", input.PendingCases ?? (object)DBNull.Value },
            { "@SpnsCategory", input.SpnsCategory ?? (object)DBNull.Value },
            { "@NoOfSpnsChangedOfVisa", input.NoOfSpnsChangedOfVisa  },
            { "@EmpCategoryId", input.EmpCategoryId  },
            { "@AutoPresentFromDate", input.AutoPresentFromDate   },
            { "@AutoPresentToDate", input.AutoPresentToDate   },
            { "@TransactionSource", input.TransactionSource ?? (object)DBNull.Value },
            { "@SalaryChangedStatus", input.SalaryChangedStatus   },
            { "@ReviewTransactionId", input.ReviewTransactionId   },
            { "@HrSeries", input.HrSeries ?? (object)DBNull.Value },
            { "@IsLeaveAllocated", input.IsLeaveAllocated },
            { "@IsEmployeeSalary", input.IsEmployeeSalary   },
            { "@IsHiringChecklistFinalized", input.IsHiringChecklistFinalized },
            { "@IsEmployeeProfileExtended", input.IsEmployeeProfileExtended   },
            { "@IsEmployeeExpenseEntitlement", input.IsEmployeeExpenseEntitlement  },
            { "@IsEmployeeJD", input.IsEmployeeJD  },
            { "@IsApprovalForPayroll", input.IsApprovalForPayroll },
            { "@IsUserId", input.IsUserId  },
            { "@PreferredCulture", input.PreferredCulture ?? (object)DBNull.Value },
            { "@DomCntId", input.DomCntId  },
            { "@EntDate", input.EntDate  },
            { "@ApplicationId", input.ApplicationId ?? (object)DBNull.Value },
            { "@RetirementDate", input.RetirementDate   },
            { "@SalaryReviewChangedStatus", input.SalaryReviewChangedStatus },
            { "@TimeStamp", input.TimeStamp },
            { "@JobIdOld", input.JobIdOld   },
            { "@PGIdOld", input.PGIdOld  },
            { "@ReportToOld", input.ReportToOld   },
            { "@FlexiShift", input.FlexiShift },
            { "@FlexiType", input.FlexiType   },
            { "@RequiredHours", input.RequiredHours  },
            { "@EditByUser", input.EditByUser ?? (object)DBNull.Value },
            { "@secondment", input.secondment },
            { "@PhotoPath", input.PhotoPath ?? (object)DBNull.Value },
            { "@CompanyShortName", input.CompanyShortName ?? (object)DBNull.Value },
            { "@ACids", input.ACids ?? (object)DBNull.Value },
            { "@DrivingLicenseNo", input.DrivingLicenseNo ?? (object)DBNull.Value },
            { "@DrivingLicenseExpiryDate", input.DrivingLicenseExpiryDate  },
            { "@isSpouseEmployed", input.isSpouseEmployed  },
            { "@isAnyOtherIncomeSource", input.isAnyOtherIncomeSource  },
            { "@isAnyPhysicalDisability", input.isAnyPhysicalDisability  },
            { "@OtherIncomeSourceDetails", input.OtherIncomeSourceDetails ?? (object)DBNull.Value },
            { "@PhysicalDisabilityDetails", input.PhysicalDisabilityDetails ?? (object)DBNull.Value },
            { "@ResidentialStatusId", input.ResidentialStatusId },
            { "@FatherHusbandPhone", input.FatherHusbandPhone ?? (object)DBNull.Value },
            { "@FatherHusbandOccupation", input.FatherHusbandOccupation ?? (object)DBNull.Value },
            { "@isOwnConveyance", input.isOwnConveyance },
            { "@ConveyanceType", input.ConveyanceType  },
            { "@ConveyanceMake", input.ConveyanceMake ?? (object)DBNull.Value },
            { "@ConveyanceModel", input.ConveyanceModel ?? (object)DBNull.Value },
            { "@ConveyanceYear", input.ConveyanceYear ?? (object)DBNull.Value },
            { "@ConveyanceRegisterationNo", input.ConveyanceRegisterationNo ?? (object)DBNull.Value },
            { "@PhoneExtension", input.PhoneExtension ?? (object)DBNull.Value },
            { "@SittingLocation", input.SittingLocation ?? (object)DBNull.Value }
        };

        return Convert.ToInt32(_common.ExecuteScalarQuery(sql, parameters));
    }


    //public async Task<int> InsertEmployeeAsync(tblEmployee employee)
    //{
    //    const string sql = @"
    //        INSERT INTO tblEmployee (
    //            EmpId, EmpCode, CompanyId, EmpRefNo, FirstName, MidName, LastName,
    //            TitleID, Initial, Fname, gndId, NIC, NICnew, NTN, DateofBirth,
    //            Address, ctyId, Phone, PhoneCntCode, PhoneCtyCode, PhoneNumber,
    //            Phone2, Phone2CntCode, Phone2CtyCode, Phone2Number, Mobile,
    //            MobileCode, MobileNumber, Email, rlgId, mrtId, bldId, nationalityid,
    //            qlfId, EmgPerson, EmgPhone, EmgAddress, MdptId, dptId, dsgId,
    //            cmpid, brnId, typId, DateJoin, DateConfirm, CDueDate, ExtDate,
    //            JobDes, Active, JobId, NotePad, ReportTo, Dotted, Medical, GShift,
    //            Gross, PDate, LastIncDate, Height, Weight, EyeColor, Glass,
    //            MedicalHistory, Disabilities, DisReason, PGId, RelName, RelWorking,
    //            RelationshipId, RelPositionId, SectId, AreaId, MTId, DivId, UId,
    //            PWD, VDesigId, VDesigId2, BaseId, RegionId, MTeamId, TeamId, ExpYr,
    //            EditBy, ConfirmBy, Hold, IsDirty, DispID, Identification, SDWId,
    //            DomId, InsurerId, GrpInsrNum, GrpInsrAmt, SumInsured, AssetStatus,
    //            LReason, Shift, AutoPresent, PGIds, CountryId, OfferDate, AppId,
    //            VaccinationHistory, ContractExpireDate, ProratedLeave, PassportNo,
    //            FatherNIC, Dgid, Dutyid, PayrollStatus, SLICAmount, TotalSLICAmount,
    //            OPDID, Email2, EmgMobile, MdptIdCurrent, MdptIdOld, dptIdCurrent,
    //            dptIdOld, dsgIdCurrent, dsgIdOld, JobIdCurrent, PGIdCurrent,
    //            ReportToCurrent, CNICExpiryDate, InternExpiryDate, HiringChecklistProcess,
    //            OldEmpId, LeavingDate, InterCompanyTransfer, SourceCompanyId,
    //            TargetCompanyId, OriginalCompanyId, SourceEmpId, TargetEmpId,
    //            OriginalEmpId, InterCompanyInduction, lastWorkingDate, ReportingDate,
    //            BaseCntId, DispCntId, ReportToCompany, relId, DottedReportToCompany,
    //            PassportExpiryDate, UserId, FormId, UserEmpId, UserEmpName, EntTerminal,
    //            EntTerminalIP, UserEmpCode, VisaNumer, VisaExpiryDate, VisaNumber,
    //            nextincDate, Matchedwithblacklist, processmanualmap, duplicatedRecordId,
    //            duplicatedMapEmpId, IdCardRemarks, FamilyCardNo, IqamaNo,
    //            IqamaExpiryHijri, IqamaExpiryGregorian, CurrSpnsName, SpnsTransferable,
    //            SpnsType, SpnsCountry, SpnsCity, SpnsContactDetails, SpnsNatureOfBusiness,
    //            IqamaProfession, SpnsExpiryHijri, SpnsExpiryGregorian, FirstNameArabic,
    //            MidNameArabic, LastNameArabic, TitleIDArabic, ValidDrivingLicenseKSA,
    //            HstOfPersecution, HstOfPenalties, PendingCases, SpnsCategory,
    //            NoOfSpnsChangedOfVisa, EmpCategoryId, AutoPresentFromDate,
    //            AutoPresentToDate, TransactionSource, SalaryChangedStatus,
    //            ReviewTransactionId, HrSeries, IsLeaveAllocated, IsEmployeeSalary,
    //            IsHiringChecklistFinalized, IsEmployeeProfileExtended,
    //            IsEmployeeExpenseEntitlement, IsEmployeeJD, IsApprovalForPayroll,
    //            IsUserId, PreferredCulture, DomCntId, EntDate, ApplicationId,
    //            RetirementDate, SalaryReviewChangedStatus, TimeStamp, JobIdOld,
    //            PGIdOld, ReportToOld, FlexiShift, FlexiType, RequiredHours,
    //            EditByUser, secondment, PhotoPath, CompanyShortName, ACids,
    //            DrivingLicenseNo, DrivingLicenseExpiryDate, isSpouseEmployed,
    //            isAnyOtherIncomeSource, isAnyPhysicalDisability, OtherIncomeSourceDetails,
    //            PhysicalDisabilityDetails, ResidentialStatusId, FatherHusbandPhone,
    //            FatherHusbandOccupation, isOwnConveyance, ConveyanceType,
    //            ConveyanceMake, ConveyanceModel, ConveyanceYear,
    //            ConveyanceRegisterationNo, PhoneExtension, SittingLocation
    //        ) VALUES (
    //            @EmpId, @EmpCode, @CompanyId, @EmpRefNo, @FirstName, @MidName, @LastName,
    //            @TitleID, @Initial, @Fname, @gndId, @NIC, @NICnew, @NTN, @DateofBirth,
    //            @Address, @ctyId, @Phone, @PhoneCntCode, @PhoneCtyCode, @PhoneNumber,
    //            @Phone2, @Phone2CntCode, @Phone2CtyCode, @Phone2Number, @Mobile,
    //            @MobileCode, @MobileNumber, @Email, @rlgId, @mrtId, @bldId, @nationalityid,
    //            @qlfId, @EmgPerson, @EmgPhone, @EmgAddress, @MdptId, @dptId, @dsgId,
    //            @cmpid, @brnId, @typId, @DateJoin, @DateConfirm, @CDueDate, @ExtDate,
    //            @JobDes, @Active, @JobId, @NotePad, @ReportTo, @Dotted, @Medical, @GShift,
    //            @Gross, @PDate, @LastIncDate, @Height, @Weight, @EyeColor, @Glass,
    //            @MedicalHistory, @Disabilities, @DisReason, @PGId, @RelName, @RelWorking,
    //            @RelationshipId, @RelPositionId, @SectId, @AreaId, @MTId, @DivId, @UId,
    //            @PWD, @VDesigId, @VDesigId2, @BaseId, @RegionId, @MTeamId, @TeamId, @ExpYr,
    //            @EditBy, @ConfirmBy, @Hold, @IsDirty, @DispID, @Identification, @SDWId,
    //            @DomId, @InsurerId, @GrpInsrNum, @GrpInsrAmt, @SumInsured, @AssetStatus,
    //            @LReason, @Shift, @AutoPresent, @PGIds, @CountryId, @OfferDate, @AppId,
    //            @VaccinationHistory, @ContractExpireDate, @ProratedLeave, @PassportNo,
    //            @FatherNIC, @Dgid, @Dutyid, @PayrollStatus, @SLICAmount, @TotalSLICAmount,
    //            @OPDID, @Email2, @EmgMobile, @MdptIdCurrent, @MdptIdOld, @dptIdCurrent,
    //            @dptIdOld, @dsgIdCurrent, @dsgIdOld, @JobIdCurrent, @PGIdCurrent,
    //            @ReportToCurrent, @CNICExpiryDate, @InternExpiryDate, @HiringChecklistProcess,
    //            @OldEmpId, @LeavingDate, @InterCompanyTransfer, @SourceCompanyId,
    //            @TargetCompanyId, @OriginalCompanyId, @SourceEmpId, @TargetEmpId,
    //            @OriginalEmpId, @InterCompanyInduction, @lastWorkingDate, @ReportingDate,
    //            @BaseCntId, @DispCntId, @ReportToCompany, @relId, @DottedReportToCompany,
    //            @PassportExpiryDate, @UserId, @FormId, @UserEmpId, @UserEmpName, @EntTerminal,
    //            @EntTerminalIP, @UserEmpCode, @VisaNumer, @VisaExpiryDate, @VisaNumber,
    //            @nextincDate, @Matchedwithblacklist, @processmanualmap, @duplicatedRecordId,
    //            @duplicatedMapEmpId, @IdCardRemarks, @FamilyCardNo, @IqamaNo,
    //            @IqamaExpiryHijri, @IqamaExpiryGregorian, @CurrSpnsName, @SpnsTransferable,
    //            @SpnsType, @SpnsCountry, @SpnsCity, @SpnsContactDetails, @SpnsNatureOfBusiness,
    //            @IqamaProfession, @SpnsExpiryHijri, @SpnsExpiryGregorian, @FirstNameArabic,
    //            @MidNameArabic, @LastNameArabic, @TitleIDArabic, @ValidDrivingLicenseKSA,
    //            @HstOfPersecution, @HstOfPenalties, @PendingCases, @SpnsCategory,
    //            @NoOfSpnsChangedOfVisa, @EmpCategoryId, @AutoPresentFromDate,
    //            @AutoPresentToDate, @TransactionSource, @SalaryChangedStatus,
    //            @ReviewTransactionId, @HrSeries, @IsLeaveAllocated, @IsEmployeeSalary,
    //            @IsHiringChecklistFinalized, @IsEmployeeProfileExtended,
    //            @IsEmployeeExpenseEntitlement, @IsEmployeeJD, @IsApprovalForPayroll,
    //            @IsUserId, @PreferredCulture, @DomCntId, @EntDate, @ApplicationId,
    //            @RetirementDate, @SalaryReviewChangedStatus, @TimeStamp, @JobIdOld,
    //            @PGIdOld, @ReportToOld, @FlexiShift, @FlexiType, @RequiredHours,
    //            @EditByUser, @secondment, @PhotoPath, @CompanyShortName, @ACids,
    //            @DrivingLicenseNo, @DrivingLicenseExpiryDate, @isSpouseEmployed,
    //            @isAnyOtherIncomeSource, @isAnyPhysicalDisability, @OtherIncomeSourceDetails,
    //            @PhysicalDisabilityDetails, @ResidentialStatusId, @FatherHusbandPhone,
    //            @FatherHusbandOccupation, @isOwnConveyance, @ConveyanceType,
    //            @ConveyanceMake, @ConveyanceModel, @ConveyanceYear,
    //            @ConveyanceRegisterationNo, @PhoneExtension, @SittingLocation
    //        );
    //        SELECT LASTVAL();"; // For PostgreSQL - use SCOPE_IDENTITY() for SQL Server

    //    using (var connection = new NpgsqlConnection(_connectionString))
    //    {
    //        using (var command = new NpgsqlCommand(sql, connection))
    //        {
    //            // Add all parameters
    //            AddEmployeeParameters(command, employee);

    //            await connection.OpenAsync();
    //            var result = await command.ExecuteScalarAsync();
    //            return Convert.ToInt32(result);
    //        }
    //    }
    //}

    //private void AddEmployeeParameters(NpgsqlCommand command, tblEmployee employee)
    //{
    //    // Basic Information (1-20)
    //    command.Parameters.AddWithValue("@EmpId", employee.EmpId);
    //    command.Parameters.AddWithValue("@EmpCode", employee.EmpCode ?? (object)DBNull.Value);
    //    command.Parameters.AddWithValue("@CompanyId", (object)employee.CompanyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmpRefNo", (object)employee.EmpRefNo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
    //    command.Parameters.AddWithValue("@MidName", (object)employee.MidName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@LastName", (object)employee.LastName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TitleID", (object)employee.TitleID ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Initial", (object)employee.Initial ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Fname", (object)employee.Fname ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@gndId", (object)employee.gndId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@NIC", (object)employee.NIC ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@NICnew", (object)employee.NICnew ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@NTN", (object)employee.NTN ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DateofBirth", (object)employee.DateofBirth ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Address", (object)employee.Address ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ctyId", (object)employee.ctyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Phone", (object)employee.Phone ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PhoneCntCode", (object)employee.PhoneCntCode ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PhoneCtyCode", (object)employee.PhoneCtyCode ?? DBNull.Value);

    //    // Phone Numbers (21-40)
    //    command.Parameters.AddWithValue("@PhoneNumber", (object)employee.PhoneNumber ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Phone2", (object)employee.Phone2 ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Phone2CntCode", (object)employee.Phone2CntCode ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Phone2CtyCode", (object)employee.Phone2CtyCode ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Phone2Number", (object)employee.Phone2Number ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Mobile", (object)employee.Mobile ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MobileCode", (object)employee.MobileCode ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MobileNumber", (object)employee.MobileNumber ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Email", (object)employee.Email ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@rlgId", (object)employee.rlgId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@mrtId", (object)employee.mrtId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@bldId", (object)employee.bldId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@nationalityid", (object)employee.nationalityid ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@qlfId", (object)employee.qlfId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmgPerson", (object)employee.EmgPerson ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmgPhone", (object)employee.EmgPhone ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmgAddress", (object)employee.EmgAddress ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MdptId", (object)employee.MdptId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@dptId", employee.dptId);
    //    command.Parameters.AddWithValue("@dsgId", (object)employee.dsgId ?? DBNull.Value);

    //    // Company/Department IDs (41-60)
    //    command.Parameters.AddWithValue("@cmpid", (object)employee.cmpid ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@brnId", (object)employee.brnId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@typId", (object)employee.typId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DateJoin", (object)employee.DateJoin ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DateConfirm", (object)employee.DateConfirm ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@CDueDate", (object)employee.CDueDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ExtDate", (object)employee.ExtDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@JobDes", (object)employee.JobDes ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Active", (object)employee.Active ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@JobId", (object)employee.JobId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@NotePad", (object)employee.NotePad ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ReportTo", (object)employee.ReportTo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Dotted", (object)employee.Dotted ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Medical", (object)employee.Medical ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@GShift", (object)employee.GShift ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Gross", employee.Gross);
    //    command.Parameters.AddWithValue("@PDate", (object)employee.PDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@LastIncDate", (object)employee.LastIncDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Height", (object)employee.Height ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Weight", (object)employee.Weight ?? DBNull.Value);

    //    // Physical Attributes (61-80)
    //    command.Parameters.AddWithValue("@EyeColor", (object)employee.EyeColor ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Glass", (object)employee.Glass ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MedicalHistory", (object)employee.MedicalHistory ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Disabilities", (object)employee.Disabilities ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DisReason", (object)employee.DisReason ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PGId", (object)employee.PGId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RelName", (object)employee.RelName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RelWorking", (object)employee.RelWorking ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RelationshipId", (object)employee.RelationshipId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RelPositionId", (object)employee.RelPositionId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SectId", (object)employee.SectId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AreaId", (object)employee.AreaId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MTId", (object)employee.MTId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DivId", (object)employee.DivId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UId", (object)employee.UId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PWD", (object)employee.PWD ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@VDesigId", (object)employee.VDesigId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@VDesigId2", (object)employee.VDesigId2 ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@BaseId", (object)employee.BaseId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RegionId", (object)employee.RegionId ?? DBNull.Value);

    //    // Team/Group IDs (81-100)
    //    command.Parameters.AddWithValue("@MTeamId", (object)employee.MTeamId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TeamId", (object)employee.TeamId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ExpYr", (object)employee.ExpYr ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EditBy", (object)employee.EditBy ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ConfirmBy", (object)employee.ConfirmBy ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Hold", (object)employee.Hold ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsDirty", (object)employee.IsDirty ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DispID", (object)employee.DispID ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Identification", (object)employee.Identification ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SDWId", (object)employee.SDWId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DomId", (object)employee.DomId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@InsurerId", (object)employee.InsurerId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@GrpInsrNum", (object)employee.GrpInsrNum ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@GrpInsrAmt", (object)employee.GrpInsrAmt ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SumInsured", (object)employee.SumInsured ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AssetStatus", (object)employee.AssetStatus ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@LReason", (object)employee.LReason ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Shift", (object)employee.Shift ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AutoPresent", (object)employee.AutoPresent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PGIds", (object)employee.PGIds ?? DBNull.Value);

    //    // More IDs and Dates (101-120)
    //    command.Parameters.AddWithValue("@CountryId", (object)employee.CountryId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OfferDate", (object)employee.OfferDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AppId", (object)employee.AppId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@VaccinationHistory", (object)employee.VaccinationHistory ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ContractExpireDate", (object)employee.ContractExpireDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ProratedLeave", (object)employee.ProratedLeave ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PassportNo", (object)employee.PassportNo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FatherNIC", (object)employee.FatherNIC ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Dgid", (object)employee.Dgid ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Dutyid", (object)employee.Dutyid ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PayrollStatus", (object)employee.PayrollStatus ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SLICAmount", (object)employee.SLICAmount ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TotalSLICAmount", (object)employee.TotalSLICAmount ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OPDID", (object)employee.OPDID ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Email2", (object)employee.Email2 ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmgMobile", (object)employee.EmgMobile ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MdptIdCurrent", (object)employee.MdptIdCurrent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MdptIdOld", (object)employee.MdptIdOld ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@dptIdCurrent", (object)employee.dptIdCurrent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@dptIdOld", (object)employee.dptIdOld ?? DBNull.Value);

    //    // Current/Old IDs (121-140)
    //    command.Parameters.AddWithValue("@dsgIdCurrent", (object)employee.dsgIdCurrent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@dsgIdOld", (object)employee.dsgIdOld ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@JobIdCurrent", (object)employee.JobIdCurrent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PGIdCurrent", (object)employee.PGIdCurrent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ReportToCurrent", (object)employee.ReportToCurrent ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@CNICExpiryDate", (object)employee.CNICExpiryDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@InternExpiryDate", (object)employee.InternExpiryDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@HiringChecklistProcess", (object)employee.HiringChecklistProcess ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OldEmpId", (object)employee.OldEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@LeavingDate", (object)employee.LeavingDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@InterCompanyTransfer", (object)employee.InterCompanyTransfer ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SourceCompanyId", (object)employee.SourceCompanyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TargetCompanyId", (object)employee.TargetCompanyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OriginalCompanyId", (object)employee.OriginalCompanyId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SourceEmpId", (object)employee.SourceEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TargetEmpId", (object)employee.TargetEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OriginalEmpId", (object)employee.OriginalEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@InterCompanyInduction", (object)employee.InterCompanyInduction ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@lastWorkingDate", (object)employee.lastWorkingDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ReportingDate", (object)employee.ReportingDate ?? DBNull.Value);

    //    // More IDs (141-160)
    //    command.Parameters.AddWithValue("@BaseCntId", (object)employee.BaseCntId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DispCntId", (object)employee.DispCntId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ReportToCompany", (object)employee.ReportToCompany ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@relId", (object)employee.relId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DottedReportToCompany", (object)employee.DottedReportToCompany ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PassportExpiryDate", (object)employee.PassportExpiryDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserId", (object)employee.UserId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FormId", (object)employee.FormId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserEmpId", (object)employee.UserEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserEmpName", (object)employee.UserEmpName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntTerminal", (object)employee.EntTerminal ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntTerminalIP", (object)employee.EntTerminalIP ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@UserEmpCode", (object)employee.UserEmpCode ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@VisaNumer", (object)employee.VisaNumer ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@VisaExpiryDate", (object)employee.VisaExpiryDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@VisaNumber", (object)employee.VisaNumber ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@nextincDate", (object)employee.nextincDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@Matchedwithblacklist", (object)employee.Matchedwithblacklist ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@processmanualmap", (object)employee.processmanualmap ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@duplicatedRecordId", (object)employee.duplicatedRecordId ?? DBNull.Value);

    //    // Duplicate Records (161-180)
    //    command.Parameters.AddWithValue("@duplicatedMapEmpId", (object)employee.duplicatedMapEmpId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IdCardRemarks", (object)employee.IdCardRemarks ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FamilyCardNo", (object)employee.FamilyCardNo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IqamaNo", (object)employee.IqamaNo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IqamaExpiryHijri", (object)employee.IqamaExpiryHijri ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IqamaExpiryGregorian", (object)employee.IqamaExpiryGregorian ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@CurrSpnsName", (object)employee.CurrSpnsName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsTransferable", (object)employee.SpnsTransferable ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsType", (object)employee.SpnsType ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsCountry", (object)employee.SpnsCountry ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsCity", (object)employee.SpnsCity ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsContactDetails", (object)employee.SpnsContactDetails ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsNatureOfBusiness", (object)employee.SpnsNatureOfBusiness ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IqamaProfession", (object)employee.IqamaProfession ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsExpiryHijri", (object)employee.SpnsExpiryHijri ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsExpiryGregorian", (object)employee.SpnsExpiryGregorian ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FirstNameArabic", (object)employee.FirstNameArabic ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@MidNameArabic", (object)employee.MidNameArabic ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@LastNameArabic", (object)employee.LastNameArabic ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TitleIDArabic", (object)employee.TitleIDArabic ?? DBNull.Value);

    //    // Saudi Specific (181-200)
    //    command.Parameters.AddWithValue("@ValidDrivingLicenseKSA", (object)employee.ValidDrivingLicenseKSA ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@HstOfPersecution", (object)employee.HstOfPersecution ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@HstOfPenalties", (object)employee.HstOfPenalties ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PendingCases", (object)employee.PendingCases ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SpnsCategory", (object)employee.SpnsCategory ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@NoOfSpnsChangedOfVisa", (object)employee.NoOfSpnsChangedOfVisa ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EmpCategoryId", (object)employee.EmpCategoryId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AutoPresentFromDate", (object)employee.AutoPresentFromDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@AutoPresentToDate", (object)employee.AutoPresentToDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TransactionSource", (object)employee.TransactionSource ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SalaryChangedStatus", (object)employee.SalaryChangedStatus ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ReviewTransactionId", (object)employee.ReviewTransactionId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@HrSeries", (object)employee.HrSeries ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsLeaveAllocated", (object)employee.IsLeaveAllocated ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsEmployeeSalary", (object)employee.IsEmployeeSalary ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsHiringChecklistFinalized", (object)employee.IsHiringChecklistFinalized ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsEmployeeProfileExtended", (object)employee.IsEmployeeProfileExtended ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsEmployeeExpenseEntitlement", (object)employee.IsEmployeeExpenseEntitlement ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsEmployeeJD", (object)employee.IsEmployeeJD ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@IsApprovalForPayroll", (object)employee.IsApprovalForPayroll ?? DBNull.Value);

    //    // Final Batch (201-220)
    //    command.Parameters.AddWithValue("@IsUserId", (object)employee.IsUserId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PreferredCulture", (object)employee.PreferredCulture ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DomCntId", (object)employee.DomCntId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EntDate", (object)employee.EntDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ApplicationId", (object)employee.ApplicationId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RetirementDate", (object)employee.RetirementDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SalaryReviewChangedStatus", (object)employee.SalaryReviewChangedStatus ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@TimeStamp", employee.TimeStamp);
    //    command.Parameters.AddWithValue("@JobIdOld", (object)employee.JobIdOld ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PGIdOld", (object)employee.PGIdOld ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ReportToOld", (object)employee.ReportToOld ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FlexiShift", (object)employee.FlexiShift ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FlexiType", (object)employee.FlexiType ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@RequiredHours", (object)employee.RequiredHours ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@EditByUser", (object)employee.EditByUser ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@secondment", (object)employee.secondment ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PhotoPath", (object)employee.PhotoPath ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@CompanyShortName", (object)employee.CompanyShortName ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ACids", (object)employee.ACids ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@DrivingLicenseNo", (object)employee.DrivingLicenseNo ?? DBNull.Value);

    //    // Final Fields (221-235)
    //    command.Parameters.AddWithValue("@DrivingLicenseExpiryDate", (object)employee.DrivingLicenseExpiryDate ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@isSpouseEmployed", (object)employee.isSpouseEmployed ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@isAnyOtherIncomeSource", (object)employee.isAnyOtherIncomeSource ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@isAnyPhysicalDisability", (object)employee.isAnyPhysicalDisability ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@OtherIncomeSourceDetails", (object)employee.OtherIncomeSourceDetails ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PhysicalDisabilityDetails", (object)employee.PhysicalDisabilityDetails ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ResidentialStatusId", (object)employee.ResidentialStatusId ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FatherHusbandPhone", (object)employee.FatherHusbandPhone ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@FatherHusbandOccupation", (object)employee.FatherHusbandOccupation ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@isOwnConveyance", (object)employee.isOwnConveyance ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ConveyanceType", (object)employee.ConveyanceType ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ConveyanceMake", (object)employee.ConveyanceMake ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ConveyanceModel", (object)employee.ConveyanceModel ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ConveyanceYear", (object)employee.ConveyanceYear ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@ConveyanceRegisterationNo", (object)employee.ConveyanceRegisterationNo ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@PhoneExtension", (object)employee.PhoneExtension ?? DBNull.Value);
    //    command.Parameters.AddWithValue("@SittingLocation", (object)employee.SittingLocation ?? DBNull.Value);
    //}


}