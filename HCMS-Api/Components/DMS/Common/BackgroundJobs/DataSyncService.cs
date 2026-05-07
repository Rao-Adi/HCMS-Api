using Dapper;
using HCMS_Api.Components.DMS.Common.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Internal;
using NpgsqlTypes;
using System.Data.SqlClient;

namespace HCMS_Api.Components.DMS.Common.BackgroundJobs;

public class DataSyncService : BackgroundService
{
    private readonly string _sqlConn;
    private readonly string _pgConn;
    private readonly ILogger<DataSyncService> _logger;

    public DataSyncService(IConfiguration configuration, ILogger<DataSyncService> logger)
    {
        // Ensure connection strings are non-null to satisfy nullable analysis and fail fast if missing.
        _sqlConn = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _pgConn = configuration.GetConnectionString("DMSConnectionString") ?? throw new InvalidOperationException("Connection string 'DMSConnectionString' is not configured.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate time: This runs immediately on start, then every 12 hours
            _logger.LogInformation("Scheduled Data Sync Started at: {time}", DateTimeOffset.Now);

            try
            {
                await RunFullSync();
                _logger.LogInformation("Data Sync Completed Successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during recurring data sync.");
            }

            // Wait for 12 hours
            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }

    private async Task RunFullSync()
    {
        try
        {

            using var sqlSource = new SqlConnection(_sqlConn);
            using var pgTarget = new NpgsqlConnection(_pgConn);
            await pgTarget.OpenAsync();

            // 1. tblSetupsdetail
            var setups = await sqlSource.QueryAsync<tblSetupsdetail>("SELECT * FROM tblSetupsdetail");
            await SyncSetupsDetail(pgTarget, setups);

            // 2. tblDeptstrMaster
            //var masters = await sqlSource.QueryAsync<tblDeptstrMaster>("SELECT * FROM tblDeptstrMaster");
            //await SyncDeptstrMaster(pgTarget, masters);
            string sql = @"SELECT 
                            dptMId, MdptId, CompanyId, Budgeted, Approved, divId, 
                            UserId, ApplicationID, FormId, UserEmpId, UserEmpName, 
                            UserEmpCode, EntTerminal, EntTerminalIP, EntOperation, EntDate 
                        FROM tblDeptstrMaster";

            var masters = await sqlSource.QueryAsync<tblDeptstrMaster>(sql);
            await SyncDeptstrMaster(pgTarget, masters);


            // 3. tblDeptstrDetail
            var details = await sqlSource.QueryAsync<tblDeptstrDetail>(@"SELECT dptDId,dptMId,dptId,Budgeted,Approved,companyId,UserId,ApplicationID,FormId
                                            ,UserEmpId,UserEmpName,UserEmpCode,EntTerminal,EntTerminalIP,EntOperation,EntDate FROM tblDeptstrDetail");
            await BulkImportDeptDetails(pgTarget, details);

            // 4. TblEmpJobProfile
            var jobs = await sqlSource.QueryAsync<TblEmpJobProfile>("SELECT * FROM TblEmpJobProfile");
            await SyncJobProfile(pgTarget, jobs);

            // 5. tblEmployee
            var employees = await sqlSource.QueryAsync<tblEmployee>("SELECT * FROM tblEmployee");
            await SyncEmployees(pgTarget, employees);
        }
        catch (Exception ex)
        {
            // Preserve original stack trace    
            throw ex;
        }
    }

    private async Task SyncSetupsDetail(NpgsqlConnection conn, IEnumerable<tblSetupsdetail> data)
    {
        int rowIndex = 0;
        await Truncate(conn, "tblSetupsdetail");

        using var writer = await conn.BeginBinaryImportAsync(@"
        COPY tblSetupsdetail (
            sdlid, code, smsid, name, lastupdate, 
            companyid, userempid, entoperation, entdate, userempcode, 
            entterminalip, entterminal, userempname, formid, userid, 
            applicationid, chngtimestamp, inactive
        ) FROM STDIN (FORMAT BINARY)");

        foreach (var i in data)
        {
            rowIndex++;
            try
            {
                await writer.StartRowAsync();

                // sdlid (1)
                await writer.WriteAsync(i.sdlid, NpgsqlDbType.Integer);

                // Code (000001) - Sanitized for 0x00
                await writer.WriteAsync(Sanitize(i.Code) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);

                // smsid (1)
                await writer.WriteAsync(i.smsid, NpgsqlDbType.Integer);

                // Name (Permanent)
                await writer.WriteAsync(Sanitize(i.Name) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);

                await writer.WriteAsync(i.LastUpdate, NpgsqlDbType.Timestamp);


                // CompanyId (1)
                await writer.WriteAsync(i.CompanyId, NpgsqlDbType.Integer);

                // The following columns in your data are NULL
                await writer.WriteAsync(i.UserEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(Sanitize(i.EntOperation) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);

                await writer.WriteAsync(i.EntDate, NpgsqlDbType.Timestamp);


                await writer.WriteAsync(Sanitize(i.UserEmpCode) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.EntTerminalIP) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.EntTerminal) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.UserEmpName) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.FormId) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.UserId) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.ApplicationId) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);

                // ChngTimeStamp (0x00000000003DF486)
                if (i.ChngTimeStamp != null && i.ChngTimeStamp.Length == 8)
                {
                    byte[] bytes = (byte[])i.ChngTimeStamp.Clone();
                    if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    long versionNumber = BitConverter.ToInt64(bytes, 0);
                    await writer.WriteAsync(versionNumber, NpgsqlDbType.Bigint);
                }
                else
                {
                    await writer.WriteAsync(DBNull.Value, NpgsqlDbType.Bigint);
                }

                // Inactive (NULL in your data)
                await writer.WriteAsync(i.Inactive, NpgsqlDbType.Boolean);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Row {Row} failed.", rowIndex);
                throw;
            }
        }
        await writer.CompleteAsync();
    }

    private string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Replace("\0", string.Empty).Trim();
    }


    private async Task SyncDeptstrMaster(NpgsqlConnection conn, IEnumerable<tblDeptstrMaster> data)
    {
        await Truncate(conn, "tblDeptstrMaster");

        using var writer = await conn.BeginBinaryImportAsync(@"
        COPY tblDeptstrMaster (
            dptmid, mdptid, companyid, budgeted, approved, 
            divid, userid, applicationid, formid, userempid, 
            userempname, userempcode, entterminal, entterminalip, entoperation, 
            entdate
        ) FROM STDIN (FORMAT BINARY)");

        foreach (var i in data)
        {
            try
            {
                await writer.StartRowAsync();

                await writer.WriteAsync(i.dptMId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.MdptId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.CompanyId ?? (object)DBNull.Value, NpgsqlDbType.Integer);

                // Cast to float for Postgres REAL type
                await writer.WriteAsync(i.Budgeted.HasValue ? (float)i.Budgeted.Value : (object)DBNull.Value, NpgsqlDbType.Real);
                await writer.WriteAsync(i.Approved.HasValue ? (float)i.Approved.Value : (object)DBNull.Value, NpgsqlDbType.Real);

                await writer.WriteAsync(i.divId ?? (object)DBNull.Value, NpgsqlDbType.Integer);

                // Strings
                await writer.WriteAsync(Sanitize(i.UserId) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.ApplicationID) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.FormId) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.UserEmpId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                await writer.WriteAsync(Sanitize(i.UserEmpName) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.UserEmpCode) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.EntTerminal) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.EntTerminalIP) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                await writer.WriteAsync(Sanitize(i.EntOperation) ?? (object)DBNull.Value, NpgsqlDbType.Varchar);

                // Date
                if (i.EntDate.HasValue)
                    await writer.WriteAsync(i.EntDate.Value, NpgsqlDbType.Timestamp);
                else
                    await writer.WriteAsync(DBNull.Value, NpgsqlDbType.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed at dptMId: {Id}", i.dptMId);
                throw;
            }
        }
        await writer.CompleteAsync();
    }

    private async Task SyncJobProfile(NpgsqlConnection conn, IEnumerable<TblEmpJobProfile> data)
    {
        try
        {
            await Truncate(conn, "TblEmpJobProfile");
            using var writer = await conn.BeginBinaryImportAsync(@"COPY TblEmpJobProfile (JobProfileId, EmpId, JobCode, JobCDate, JobTitle, MdptId, dptId, JobId, baseId, typId, ReportingRelationship, KeyRelationshipInt, KeyRelationshipExt, AcademicQualifications, WorkExperience, JobSummary, KeyPerformanceIndicators, Responsibilities, Accountabilities, EOWorkCondition, EmpDate, MgrEmpId, MgrDate, HrEmpId, HRDate, CompanyId, AppDoc, DivId, DirectRptTo, DCompanyId, DGradeId, DDsgId, DDivId, DMdptId, DDptId, DBaseId, InDirectRptTo, IndCompanyId, IndGradeId, IndDsgId, IndDivId, IndMdptId, IndDptId, IndBaseId, PAgeRange, PrefGender, EYears, DsgId, RExpYears, chkqual, chkcert, chktrain, isDescripancyInCompetency, isDescripancyInKPI, JdId, Active, PageRangeTo, EYearsTo, RExpYearsTo, IncPackage, OtherBenifit, SalaryRemarks, visaStatus, SalaryAmountFrom, SalaryAmountTo, SalaryCurrencyId, PrefReligion, AssignedDate, UserId, ApplicationID, FormId, UserEmpId, UserEmpName, UserEmpCode, EntTerminal, EntTerminalIP, EntOperation, EntDate, EmoIntlProfiling, RoleId, DRoleId, IndRoleId, Received, JDChangeVersionNo, AcknowledgeOn, AcknowledgeByUserID, AcknowledgeTerminal, AcknowledgeByEmpID, changeResponsibilityDetail) FROM STDIN (FORMAT BINARY)");
            foreach (var i in data)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(i.JobProfileId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.JobCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.JobCDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.JobTitle, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MdptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.JobId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.BaseId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.TypId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ReportingRelationship, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.KeyRelationshipInt, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.KeyRelationshipExt, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.AcademicQualifications, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.WorkExperience, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.JobSummary, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.KeyPerformanceIndicators, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Responsibilities, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Accountabilities, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EOWorkCondition, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EmpDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.MgrEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.MgrDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.HrEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.HRDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.CompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.AppDoc, NpgsqlDbType.Bytea);
                await writer.WriteAsync(i.DivId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DirectRptTo, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.DCompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DGradeId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DDsgId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DDivId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DMdptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DDptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DBaseId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.InDirectRptTo, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IndCompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndGradeId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndDsgId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndDivId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndMdptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndDptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndBaseId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.PAgeRange, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PrefGender, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EYears, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.DsgId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RExpYears, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Chkqual, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.Chkcert, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.Chktrain, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsDescripancyInCompetency, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsDescripancyInKPI, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.JdId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Active, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.PageRangeTo, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EYearsTo, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RExpYearsTo, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IncPackage, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.OtherBenifit, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SalaryRemarks, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.VisaStatus, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SalaryAmountFrom, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.SalaryAmountTo, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.SalaryCurrencyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.PrefReligion, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.AssignedDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.UserId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ApplicationID, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.FormId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.UserEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.UserEmpName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.UserEmpCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EntTerminal, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EntTerminalIP, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EntOperation, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EntDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.EmoIntlProfiling, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RoleId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DRoleId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IndRoleId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Received, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.JDChangeVersionNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.AcknowledgeOn, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.AcknowledgeByUserID, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.AcknowledgeTerminal, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.AcknowledgeByEmpID, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ChangeResponsibilityDetail, NpgsqlDbType.Varchar);
            }
            await writer.CompleteAsync();
        }
        catch
        {
            // Preserve original stack trace
            throw;
        }
    }

    private async Task SyncEmployees(NpgsqlConnection conn, IEnumerable<tblEmployee> data)
    {
        try
        {
            await Truncate(conn, "tblEmployee");
            // Column list for COPY command based on your InsertEmployeeAsync method
            string copySql = @"COPY tblEmployee (EmpId, EmpCode, CompanyId, EmpRefNo, FirstName, MidName, LastName, TitleID, Initial, Fname, gndId, NIC, NICnew, NTN, DateofBirth, Address, ctyId, Phone, PhoneCntCode, PhoneCtyCode, PhoneNumber, Phone2, Phone2CntCode, Phone2CtyCode, Phone2Number, Mobile, MobileCode, MobileNumber, Email, rlgId, mrtId, bldId, nationalityid, qlfId, EmgPerson, EmgPhone, EmgAddress, MdptId, dptId, dsgId, cmpid, brnId, typId, DateJoin, DateConfirm, CDueDate, ExtDate, JobDes, Active, JobId, NotePad, ReportTo, Dotted, Medical, GShift, Gross, PDate, LastIncDate, Height, Weight, EyeColor, Glass, MedicalHistory, Disabilities, DisReason, PGId, RelName, RelWorking, RelationshipId, RelPositionId, SectId, AreaId, MTId, DivId, UId, PWD, VDesigId, VDesigId2, BaseId, RegionId, MTeamId, TeamId, ExpYr, EditBy, ConfirmBy, Hold, IsDirty, DispID, Identification, SDWId, DomId, InsurerId, GrpInsrNum, GrpInsrAmt, SumInsured, AssetStatus, LReason, Shift, AutoPresent, PGIds, CountryId, OfferDate, AppId, VaccinationHistory, ContractExpireDate, ProratedLeave, PassportNo, FatherNIC, Dgid, Dutyid, PayrollStatus, SLICAmount, TotalSLICAmount, OPDID, Email2, EmgMobile, MdptIdCurrent, MdptIdOld, dptIdCurrent, dptIdOld, dsgIdCurrent, dsgIdOld, JobIdCurrent, PGIdCurrent, ReportToCurrent, CNICExpiryDate, InternExpiryDate, HiringChecklistProcess, OldEmpId, LeavingDate, InterCompanyTransfer, SourceCompanyId, TargetCompanyId, OriginalCompanyId, SourceEmpId, TargetEmpId, OriginalEmpId, InterCompanyInduction, lastWorkingDate, ReportingDate, BaseCntId, DispCntId, ReportToCompany, relId, DottedReportToCompany, PassportExpiryDate, UserId, FormId, UserEmpId, UserEmpName, EntTerminal, EntTerminalIP, UserEmpCode, VisaNumer, VisaExpiryDate, VisaNumber, nextincDate, Matchedwithblacklist, processmanualmap, duplicatedRecordId, duplicatedMapEmpId, IdCardRemarks, FamilyCardNo, IqamaNo, IqamaExpiryHijri, IqamaExpiryGregorian, CurrSpnsName, SpnsTransferable, SpnsType, SpnsCountry, SpnsCity, SpnsContactDetails, SpnsNatureOfBusiness, IqamaProfession, SpnsExpiryHijri, SpnsExpiryGregorian, FirstNameArabic, MidNameArabic, LastNameArabic, TitleIDArabic, ValidDrivingLicenseKSA, HstOfPersecution, HstOfPenalties, PendingCases, SpnsCategory, NoOfSpnsChangedOfVisa, EmpCategoryId, AutoPresentFromDate, AutoPresentToDate, TransactionSource, SalaryChangedStatus, ReviewTransactionId, HrSeries, IsLeaveAllocated, IsEmployeeSalary, IsHiringChecklistFinalized, IsEmployeeProfileExtended, IsEmployeeExpenseEntitlement, IsEmployeeJD, IsApprovalForPayroll, IsUserId, PreferredCulture, DomCntId, EntDate, ApplicationId, RetirementDate, SalaryReviewChangedStatus, TimeStamp, JobIdOld, PGIdOld, ReportToOld, FlexiShift, FlexiType, RequiredHours, EditByUser, secondment, PhotoPath, CompanyShortName, ACids, DrivingLicenseNo, DrivingLicenseExpiryDate, isSpouseEmployed, isAnyOtherIncomeSource, isAnyPhysicalDisability, OtherIncomeSourceDetails, PhysicalDisabilityDetails, ResidentialStatusId, FatherHusbandPhone, FatherHusbandOccupation, isOwnConveyance, ConveyanceType, ConveyanceMake, ConveyanceModel, ConveyanceYear, ConveyanceRegisterationNo, PhoneExtension, SittingLocation) FROM STDIN (FORMAT BINARY)";

            using var writer = await conn.BeginBinaryImportAsync(copySql);
            foreach (var i in data)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(i.EmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EmpCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.CompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EmpRefNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.FirstName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MidName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.LastName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.TitleID, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Initial, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Fname, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.gndId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.NIC, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.NICnew, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.NTN, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.DateofBirth, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.Address, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ctyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Phone, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PhoneCntCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PhoneCtyCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PhoneNumber, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Phone2, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Phone2CntCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Phone2CtyCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Phone2Number, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Mobile, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MobileCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MobileNumber, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Email, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.rlgId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.mrtId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.bldId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.nationalityid, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.qlfId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EmgPerson, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EmgPhone, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EmgAddress, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MdptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.dptId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.dsgId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.cmpid, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.brnId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.typId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DateJoin, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.DateConfirm, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.CDueDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.ExtDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.JobDes, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Active, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.JobId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.NotePad, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ReportTo, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Dotted, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Medical, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.GShift, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.Gross, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.PDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.LastIncDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.Height, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Weight, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EyeColor, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Glass, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MedicalHistory, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Disabilities, NpgsqlDbType.Smallint);
                await writer.WriteAsync(i.DisReason, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PGId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RelName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.RelWorking, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.RelationshipId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RelPositionId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SectId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.AreaId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.MTId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DivId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.UId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PWD, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.VDesigId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.VDesigId2, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.BaseId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RegionId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.MTeamId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.TeamId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ExpYr, NpgsqlDbType.Real);
                await writer.WriteAsync(i.EditBy, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ConfirmBy, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Hold, NpgsqlDbType.Smallint);
                await writer.WriteAsync(i.IsDirty, NpgsqlDbType.Smallint);
                await writer.WriteAsync(i.DispID, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Identification, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SDWId, NpgsqlDbType.Smallint);
                await writer.WriteAsync(i.DomId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.InsurerId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.GrpInsrNum, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.GrpInsrAmt, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.SumInsured, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.AssetStatus, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.LReason, NpgsqlDbType.Varchar);
                if (string.IsNullOrEmpty(i.Shift))
                {
                    await writer.WriteNullAsync();
                }
                else
                {
                    await writer.WriteAsync(i.Shift.Substring(0, 1), NpgsqlDbType.Char);
                }
                await writer.WriteAsync(i.AutoPresent, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.PGIds, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.CountryId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.OfferDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.AppId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.VaccinationHistory, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ContractExpireDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.ProratedLeave, NpgsqlDbType.Smallint);
                await writer.WriteAsync(i.PassportNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.FatherNIC, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.Dgid, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Dutyid, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.PayrollStatus, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SLICAmount, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.TotalSLICAmount, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.OPDID, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.Email2, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EmgMobile, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MdptIdCurrent, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.MdptIdOld, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.dptIdCurrent, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.dptIdOld, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.dsgIdCurrent, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.dsgIdOld, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.JobIdCurrent, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.PGIdCurrent, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ReportToCurrent, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.CNICExpiryDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.InternExpiryDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.HiringChecklistProcess, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.OldEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.LeavingDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.InterCompanyTransfer, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.SourceCompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.TargetCompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.OriginalCompanyId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SourceEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.TargetEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.OriginalEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.InterCompanyInduction, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.lastWorkingDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.ReportingDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.BaseCntId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DispCntId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ReportToCompany, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.relId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.DottedReportToCompany, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.PassportExpiryDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.UserId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.FormId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.UserEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.UserEmpName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EntTerminal, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.EntTerminalIP, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.UserEmpCode, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.VisaNumer, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.VisaExpiryDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.VisaNumber, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.nextincDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.Matchedwithblacklist, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.processmanualmap, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.duplicatedRecordId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.duplicatedMapEmpId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.IdCardRemarks, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.FamilyCardNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.IqamaNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.IqamaExpiryHijri, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.IqamaExpiryGregorian, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.CurrSpnsName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SpnsTransferable, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SpnsType, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SpnsCountry, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SpnsCity, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SpnsContactDetails, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SpnsNatureOfBusiness, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.IqamaProfession, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.SpnsExpiryHijri, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.SpnsExpiryGregorian, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.FirstNameArabic, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.MidNameArabic, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.LastNameArabic, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.TitleIDArabic, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ValidDrivingLicenseKSA, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.HstOfPersecution, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.HstOfPenalties, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PendingCases, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SpnsCategory, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.NoOfSpnsChangedOfVisa, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EmpCategoryId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.AutoPresentFromDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.AutoPresentToDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.TransactionSource, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SalaryChangedStatus, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ReviewTransactionId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.HrSeries, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.IsLeaveAllocated, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsEmployeeSalary, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsHiringChecklistFinalized, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsEmployeeProfileExtended, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsEmployeeExpenseEntitlement, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsEmployeeJD, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsApprovalForPayroll, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.IsUserId, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.PreferredCulture, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.DomCntId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.EntDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.ApplicationId, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.RetirementDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.SalaryReviewChangedStatus, NpgsqlDbType.Integer);
                await writer.WriteAsync(DateTime.Now, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.JobIdOld, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.PGIdOld, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ReportToOld, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.FlexiShift, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.FlexiType, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.RequiredHours, NpgsqlDbType.Numeric);
                await writer.WriteAsync(i.EditByUser, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.secondment, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.PhotoPath, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.CompanyShortName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ACids, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.DrivingLicenseNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.DrivingLicenseExpiryDate, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(i.isSpouseEmployed, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.isAnyOtherIncomeSource, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.isAnyPhysicalDisability, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.OtherIncomeSourceDetails, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PhysicalDisabilityDetails, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ResidentialStatusId, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.FatherHusbandPhone, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.FatherHusbandOccupation, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.isOwnConveyance, NpgsqlDbType.Boolean);
                await writer.WriteAsync(i.ConveyanceType, NpgsqlDbType.Integer);
                await writer.WriteAsync(i.ConveyanceMake, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ConveyanceModel, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ConveyanceYear, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.ConveyanceRegisterationNo, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.PhoneExtension, NpgsqlDbType.Varchar);
                await writer.WriteAsync(i.SittingLocation, NpgsqlDbType.Varchar);
            }
            await writer.CompleteAsync();
        }
        catch
        {
            // Preserve original stack trace
            throw;
        }
    }

    private async Task BulkImportDeptDetails(NpgsqlConnection conn, IEnumerable<tblDeptstrDetail> data)
    {
        int rowNumber = 0;

        try
        {
            await ExecuteCommand(conn, "TRUNCATE TABLE tblDeptstrDetail CASCADE");

            using var writer = await conn.BeginBinaryImportAsync(@"
        COPY tblDeptstrDetail (
            dptdid,
            dptmid,
            dptid,
            budgeted,
            approved,
            companyid,
            userid,
            applicationid,
            formid,
            userempid,
            userempname,
            userempcode,
            entterminal,
            entterminalip,
            entoperation,
            entdate
        ) FROM STDIN (FORMAT BINARY)");

            foreach (var item in data)
            {
                rowNumber++;

                await writer.StartRowAsync();

                await writer.WriteAsync(item.dptDId, NpgsqlDbType.Integer);
                await writer.WriteAsync(item.dptMId, NpgsqlDbType.Integer);
                await writer.WriteAsync(item.dptId, NpgsqlDbType.Integer);

                // real = float4
                await writer.WriteAsync(Convert.ToSingle(item.Budgeted), NpgsqlDbType.Real);
                await writer.WriteAsync(Convert.ToSingle(item.Approved), NpgsqlDbType.Real);

                await writer.WriteAsync(item.companyId, NpgsqlDbType.Integer);

                await writer.WriteAsync(item.UserId ?? "", NpgsqlDbType.Varchar);
                await writer.WriteAsync(item.ApplicationID ?? "", NpgsqlDbType.Varchar);
                await writer.WriteAsync(item.FormId ?? "", NpgsqlDbType.Varchar);

                await writer.WriteAsync(item.UserEmpId, NpgsqlDbType.Integer);

                await writer.WriteAsync(item.UserEmpName ?? "", NpgsqlDbType.Varchar);
                await writer.WriteAsync(item.UserEmpCode ?? "", NpgsqlDbType.Varchar);
                await writer.WriteAsync(item.EntTerminal ?? "", NpgsqlDbType.Varchar);
                await writer.WriteAsync(item.EntTerminalIP ?? "", NpgsqlDbType.Varchar);
                await writer.WriteAsync(item.EntOperation ?? "", NpgsqlDbType.Varchar);

                if (item.EntDate.HasValue)
                    await writer.WriteAsync(item.EntDate.Value, NpgsqlDbType.Timestamp);
                else
                    await writer.WriteNullAsync();

                Console.WriteLine($"Row {rowNumber} imported successfully.");
            }

            await writer.CompleteAsync();

            Console.WriteLine("Bulk import completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred at row {rowNumber}");
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    private async Task Truncate(NpgsqlConnection conn, string table)
    {
        using var cmd = new NpgsqlCommand($"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteCommand(NpgsqlConnection conn, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task WriteFlexibleDateAsync(NpgsqlBinaryImporter writer, object? value, NpgsqlDbType dbType = NpgsqlDbType.Timestamp)
    {
        // Write a typed value to the binary importer. When writing NULL, pass an explicit dbType so the COPY parser
        // knows which column type is expected and avoids "no NpgsqlDbType" errors.
        if (value == null || value == DBNull.Value)
        {
            await writer.WriteAsync(DBNull.Value, dbType);
            return;
        }

        if (value is DateTime dt)
        {
            await writer.WriteAsync(dt, dbType);
            return;
        }

        if (value is string str)
        {
            if (DateTime.TryParse(str, out var parsedDt))
            {
                await writer.WriteAsync(parsedDt, dbType);
                return;
            }

            // If string cannot be parsed to DateTime, write as text with Varchar type when caller expects Timestamp
            // (fallback). Use text to avoid attempting to write NpgsqlDbType enum values.
            await writer.WriteAsync(str, NpgsqlDbType.Varchar);
            return;
        }

        if (value is TimeSpan ts)
        {
            // If caller specified Timestamp but we have a TimeSpan, write as Interval explicitly
            await writer.WriteAsync(ts, NpgsqlDbType.Interval);
            return;
        }

        // Final fallback: write the value with the requested dbType if possible
        await writer.WriteAsync(value, dbType);
    }

    // Add near the other helpers in the class
    private async Task WriteTypedAsync(NpgsqlBinaryImporter writer, object? value, NpgsqlDbType dbType, int rowIndex, string columnName, ILogger logger)
    {
        try
        {
            if (value == null || value == DBNull.Value)
            {
                await writer.WriteAsync(DBNull.Value, dbType);
                return;
            }

            // Common safe conversions
            if (dbType == NpgsqlDbType.Timestamp)
            {
                if (value is DateTime dt) { await writer.WriteAsync(dt, dbType); return; }
                if (value is string s && DateTime.TryParse(s, out var parsed)) { await writer.WriteAsync(parsed, dbType); return; }
            }
            if (dbType == NpgsqlDbType.Interval && value is TimeSpan ts)
            {
                await writer.WriteAsync(ts, dbType);
                return;
            }

            // Fallback: attempt to write directly with explicit dbType
            await writer.WriteAsync(value, dbType);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Binary COPY write failed at row {Row} column {Column} (expected {DbType}) — value type={ValueType} value={Value}",
                rowIndex, columnName, dbType, value?.GetType().FullName ?? "null", value);
            throw;
        }
    }


    // Central helper that always uses an explicit dbType for NULLs and logs context on failure.
    private async Task WriteColumnAsync(NpgsqlBinaryImporter writer, object? value, NpgsqlDbType dbType, int rowIndex, string columnName)
    {
        try
        {
            if (value == null || value == DBNull.Value || (value is string s1 && string.IsNullOrWhiteSpace(s1)))
            {
                await writer.WriteAsync(DBNull.Value, dbType);
                return;
            }

            // 1. Handle Timestamps
            if (dbType == NpgsqlDbType.Timestamp)
            {
                if (value is DateTime dt) { await writer.WriteAsync(dt, dbType); return; }
                if (value is string s && DateTime.TryParse(s, out var parsed))
                {
                    await writer.WriteAsync(parsed, dbType);
                    return;
                }
                // If parsing fails, the logic used to just "stop" here. 
                // Now we fall through to the generic write below or throw an error.
            }

            // 2. Handle Char(1) Truncation (Prevents the 22001 error)
            if (dbType == NpgsqlDbType.Char && value is string sChar && sChar.Length > 1)
            {
                await writer.WriteAsync(sChar.Substring(0, 1), dbType);
                return;
            }

            // 3. Handle Intervals
            if (dbType == NpgsqlDbType.Interval && value is TimeSpan ts)
            {
                await writer.WriteAsync(ts, dbType);
                return;
            }

            // 4. Handle Bytea
            if (dbType == NpgsqlDbType.Bytea && value is not byte[])
            {
                // If it's a string (common in some ORMs), you might need to convert it
                // For now, let's keep your safety check but ensure it doesn't just hang
                throw new InvalidCastException($"Column '{columnName}' expected byte[], got {value.GetType().FullName}");
            }

            // 5. FINAL FALLBACK: Always write something to keep the stream aligned
            await writer.WriteAsync(value, dbType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Row {Row} Col {Column}: Write failed. Type: {DbType}, Value: {Value}",
                rowIndex, columnName, dbType, value);
            throw; // Rethrow to stop the import; otherwise, the rest of the file will be corrupt
        }
    }

}