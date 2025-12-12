using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.HR;
using HCMS_Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Ocsp;
using System;
using System.ComponentModel.Design;
using System.Data;
using System.Web.Http;
using static Azure.Core.HttpHeader;
using static System.Net.Mime.MediaTypeNames;

using System.Linq;
using System.Collections.Generic;

namespace HCMS_Api.Components.HCMS.HR
{
    public class PerformanceJournalPolicyComponent
    {
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly HCMS_Api.Common.Common _common;
        private readonly IDapperDataService _dapperService;

        public PerformanceJournalPolicyComponent(IConfiguration configuration, Utilities utilities
            , DataServices dataservice, HCMS_Api.Common.Common common
            , IDapperDataService dapper)
        {
            _configuration = configuration;
            _utilities = utilities;
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
            _common = common;
            _dapperService = dapper;
        }

        public async Task<IReadOnlyList<FiscalYearDto>> GetClosedFiscalYears(string CompanyID)
        {
            string sql = @"SELECT  FYId
                , DATENAME(M, StartDate) + ' ' + CAST( Year(StartDate) AS VARCHAR) + ' - ' + DATENAME(M, EndDate) + ' ' + CAST( Year(EndDate) AS VARCHAR) FiscalYear 
                FROM tblFiscalYearSetup WHERE CompanyId = @CompanyId AND Active = 1 ORDER BY FYId DESC ";

            var args = new
            {
                CompanyId = CompanyID,
            };

            var rows = (await _dapperService.QueryAsync<FiscalYearDto>(sql, args)).ToList();
            rows.Insert(0, new FiscalYearDto { FYId = -1, FiscalYear = "--Select--" });
            return rows;
        }

        public async Task<IReadOnlyList<GradeDto>> GetGrades(string companyId)
        {
            const string sql = @"
                                SELECT 
                                    CAST(Code AS int)       AS GradeID,
                                    Name                     AS GradeName
                                FROM tblSetupsDetail WITH (NOLOCK)
                                WHERE CompanyId = @CompanyId 
                                  AND SmsId = 19            -- Grades
                                ORDER BY CAST(Code AS int);";

            var rows = (await _dapperService.QueryAsync<GradeDto>(sql, new { CompanyId = companyId })).ToList();
            return rows;
        }

        public async Task<IReadOnlyList<DivisionDto>> GetDivisions(string companyId)
        {
            const string sql = @"
                                SELECT 
                                    CAST(Code AS int)       AS DivisionID,
                                    Name                     AS DivisionName
                                FROM tblSetupsDetail WITH (NOLOCK)
                                WHERE CompanyId = @CompanyId 
                                  AND SmsId = 70            -- Divisions
                                ORDER BY CAST(Code AS int);";

            var rows = (await _dapperService.QueryAsync<DivisionDto>(sql, new { CompanyId = companyId })).ToList();
            return rows;
        }

        public async Task<int> SavePerformancePolicyAsync(PmSettingsDto dto, string userName)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            // Build TVP (MUST match dbo.PM_CheckInRuleCsv_TVP)
            var rulesTvp = new DataTable();
            rulesTvp.Columns.Add("TempRowId", typeof(int));
            rulesTvp.Columns.Add("FYId", typeof(int));
            rulesTvp.Columns.Add("FrequencyDays", typeof(int));
            rulesTvp.Columns.Add("VisibilityCompetencies", typeof(bool));
            rulesTvp.Columns.Add("GradeIds", typeof(string));     // CSV e.g. "1,2,3"
            rulesTvp.Columns.Add("DivisionIds", typeof(string));  // CSV e.g. "4,5"

            int idx = 0;
            foreach (var r in (dto.Rules ?? Enumerable.Empty<CheckInRuleDto>()))
            {
                int temp = r.TempRowId > 0 ? r.TempRowId : ++idx;

                string gradeCsv = string.Join(",", (r.GradeIds ?? new List<int>()).Distinct().OrderBy(x => x));
                string divCsv = string.Join(",", (r.DivisionIds ?? new List<int>()).Distinct().OrderBy(x => x));
                gradeCsv = string.IsNullOrWhiteSpace(gradeCsv) ? null : gradeCsv;
                divCsv = string.IsNullOrWhiteSpace(divCsv) ? null : divCsv;

                rulesTvp.Rows.Add(temp, r.FYId, r.FrequencyDays, r.VisibilityCompetencies, gradeCsv, divCsv);
            }

            var p = new DynamicParameters();

            p.Add("@SettingsId", dto.SettingsId, DbType.Int32, ParameterDirection.InputOutput);
            p.Add("@CompanyId", dto.CompanyId);
            p.Add("@PGId", dto.PGId);
            p.Add("@EffectiveFrom", dto.EffectiveFrom);
            p.Add("@ReminderDaysAfterDue", dto.ReminderDaysAfterDue);

            p.Add("@ObjectiveChangeCutoffMonth", dto.ObjectiveChangeCutoffMonth);
            p.Add("@FurtherApprovalRequired", dto.FurtherApprovalRequired);
            p.Add("@AllowHoDChangeBeyondCutoff", dto.AllowHoDChangeBeyondCutoff);
            p.Add("@DeptOrDivisionGoalsMandatory", dto.DeptOrDivisionGoalsMandatory);

            p.Add("@Comp_NoWeightageChange", dto.Comp_NoWeightageChange);
            p.Add("@Comp_NoAddOrRemove", dto.Comp_NoAddOrRemove);
            p.Add("@Comp_NoTargetRatingChange", dto.Comp_NoTargetRatingChange);
            p.Add("@Comp_NoCoreCompetencyMarking", dto.Comp_NoCoreCompetencyMarking);
            p.Add("@Comp_NoAddEditDeleteImport", dto.Comp_NoAddEditDeleteImport);

            p.Add("@EmpInfo_EmployeeCode", dto.EmpInfo_EmployeeCode);
            p.Add("@EmpInfo_Division", dto.EmpInfo_Division);
            p.Add("@EmpInfo_Location", dto.EmpInfo_Location);
            p.Add("@EmpInfo_PayrollGroup", dto.EmpInfo_PayrollGroup);
            p.Add("@EmpInfo_EmployeeCategory", dto.EmpInfo_EmployeeCategory);
            p.Add("@EmpInfo_DirectReportingTo", dto.EmpInfo_DirectReportingTo);
            p.Add("@EmpInfo_GrossSalary", dto.EmpInfo_GrossSalary);

            p.Add("@EmpInfo_EmployeeName", dto.EmpInfo_EmployeeName);
            p.Add("@EmpInfo_Department", dto.EmpInfo_Department);
            p.Add("@EmpInfo_Region", dto.EmpInfo_Region);
            p.Add("@EmpInfo_Designation", dto.EmpInfo_Designation);
            p.Add("@EmpInfo_EmployeeType", dto.EmpInfo_EmployeeType);
            p.Add("@EmpInfo_IndirectReportingTo", dto.EmpInfo_IndirectReportingTo);
            p.Add("@EmpInfo_DateOfJoining", dto.EmpInfo_DateOfJoining);

            p.Add("@EmpInfo_DateOfBirth", dto.EmpInfo_DateOfBirth);
            p.Add("@EmpInfo_SubDepartment", dto.EmpInfo_SubDepartment);
            p.Add("@EmpInfo_Grade", dto.EmpInfo_Grade);
            p.Add("@EmpInfo_EmployeeStatus", dto.EmpInfo_EmployeeStatus);
            p.Add("@EmpInfo_BasicSalary", dto.EmpInfo_BasicSalary);
            p.Add("@EmpInfo_DateOfConfirmation", dto.EmpInfo_DateOfConfirmation);

            p.Add("@Other_AttendanceInformation", dto.Other_AttendanceInformation);
            p.Add("@Other_LeaveDetails", dto.Other_LeaveDetails);
            p.Add("@Other_Qualifications", dto.Other_Qualifications);
            p.Add("@Other_CertificationDetails", dto.Other_CertificationDetails);
            p.Add("@Other_DisciplinaryActions", dto.Other_DisciplinaryActions);
            p.Add("@Other_Awards", dto.Other_Awards);
            p.Add("@Other_JobDescription", dto.Other_JobDescription);

            p.Add("@Trend_Enabled", dto.Trend_Enabled);
            p.Add("@Trend_YearRange", dto.Trend_YearRange);
            p.Add("@IncProm_Enabled", dto.IncProm_Enabled);
            p.Add("@IncProm_YearRange", dto.IncProm_YearRange);
            p.Add("@RoundOverallScore", dto.RoundOverallScore);

            p.Add("@OAE_CompanyGoals", dto.OAE_CompanyGoals);
            p.Add("@OAE_DivisionGoals", dto.OAE_DivisionGoals);
            p.Add("@OAE_DepartmentGoals", dto.OAE_DepartmentGoals);
            p.Add("@OAE_SubDepartmentGoals", dto.OAE_SubDepartmentGoals);

            p.Add("@CreatedOrModifiedBy", userName);
            p.Add("@Rules", rulesTvp.AsTableValuedParameter("dbo.PM_CheckInRuleCsv_TVP"));

            const string sql = @"
EXEC dbo.usp_PM_Settings_Save
     @SettingsId=@SettingsId OUTPUT,
     @CompanyId=@CompanyId,
     @PGId=@PGId,
     @EffectiveFrom=@EffectiveFrom,
     @ReminderDaysAfterDue=@ReminderDaysAfterDue,
     @ObjectiveChangeCutoffMonth=@ObjectiveChangeCutoffMonth,
     @FurtherApprovalRequired=@FurtherApprovalRequired,
     @AllowHoDChangeBeyondCutoff=@AllowHoDChangeBeyondCutoff,
     @DeptOrDivisionGoalsMandatory=@DeptOrDivisionGoalsMandatory,
     @Comp_NoWeightageChange=@Comp_NoWeightageChange,
     @Comp_NoAddOrRemove=@Comp_NoAddOrRemove,
     @Comp_NoTargetRatingChange=@Comp_NoTargetRatingChange,
     @Comp_NoCoreCompetencyMarking=@Comp_NoCoreCompetencyMarking,
     @Comp_NoAddEditDeleteImport=@Comp_NoAddEditDeleteImport,
     @EmpInfo_EmployeeCode=@EmpInfo_EmployeeCode,
     @EmpInfo_Division=@EmpInfo_Division,
     @EmpInfo_Location=@EmpInfo_Location,
     @EmpInfo_PayrollGroup=@EmpInfo_PayrollGroup,
     @EmpInfo_EmployeeCategory=@EmpInfo_EmployeeCategory,
     @EmpInfo_DirectReportingTo=@EmpInfo_DirectReportingTo,
     @EmpInfo_GrossSalary=@EmpInfo_GrossSalary,
     @EmpInfo_EmployeeName=@EmpInfo_EmployeeName,
     @EmpInfo_Department=@EmpInfo_Department,
     @EmpInfo_Region=@EmpInfo_Region,
     @EmpInfo_Designation=@EmpInfo_Designation,
     @EmpInfo_EmployeeType=@EmpInfo_EmployeeType,
     @EmpInfo_IndirectReportingTo=@EmpInfo_IndirectReportingTo,
     @EmpInfo_DateOfJoining=@EmpInfo_DateOfJoining,
     @EmpInfo_DateOfBirth=@EmpInfo_DateOfBirth,
     @EmpInfo_SubDepartment=@EmpInfo_SubDepartment,
     @EmpInfo_Grade=@EmpInfo_Grade,
     @EmpInfo_EmployeeStatus=@EmpInfo_EmployeeStatus,
     @EmpInfo_BasicSalary=@EmpInfo_BasicSalary,
     @EmpInfo_DateOfConfirmation=@EmpInfo_DateOfConfirmation,
     @Other_AttendanceInformation=@Other_AttendanceInformation,
     @Other_LeaveDetails=@Other_LeaveDetails,
     @Other_Qualifications=@Other_Qualifications,
     @Other_CertificationDetails=@Other_CertificationDetails,
     @Other_DisciplinaryActions=@Other_DisciplinaryActions,
     @Other_Awards=@Other_Awards,
     @Other_JobDescription=@Other_JobDescription,
     @Trend_Enabled=@Trend_Enabled,
     @Trend_YearRange=@Trend_YearRange,
     @IncProm_Enabled=@IncProm_Enabled,
     @IncProm_YearRange=@IncProm_YearRange,
     @RoundOverallScore=@RoundOverallScore,
     @OAE_CompanyGoals=@OAE_CompanyGoals,
     @OAE_DivisionGoals=@OAE_DivisionGoals,
     @OAE_DepartmentGoals=@OAE_DepartmentGoals,
     @OAE_SubDepartmentGoals=@OAE_SubDepartmentGoals,
     @CreatedOrModifiedBy=@CreatedOrModifiedBy,
     @Rules=@Rules;
";

            await _dapperService.ExecuteAsync(sql, p);
            return p.Get<int?>("@SettingsId") ?? 0;
        }

        // ======== LOAD ========

        public async Task<PmSettingsGetResult> GetPerformancePolicyAsync(int companyId, int? pgId, int? settingsId)
        {
            var header = (await _dapperService.QueryAsync<PmSettingsDto>(
                @"SELECT TOP(1) *
                    FROM dbo.PM_Settings
                   WHERE CompanyId = @CompanyId
                     AND (@SettingsId IS NULL OR SettingsId = @SettingsId)
                     AND (@PGId IS NULL OR PGId = @PGId)
                ORDER BY SettingsId DESC",
                new { CompanyId = companyId, PGId = pgId, SettingsId = settingsId }
            )).FirstOrDefault();

            if (header == null)
            {
                return new PmSettingsGetResult
                {
                    Header = new PmSettingsDto(),
                    Rules = new List<RuleRowDto>(),
                    RuleGrades = new List<object>(),
                    RuleDivisions = new List<object>()
                };
            }

            var sid = header.SettingsId ?? 0;

            var rules = (await _dapperService.QueryAsync<RuleRowDto>(
                @"SELECT 
                      RuleId,
                      FYId,
                      FrequencyDays,
                      VisibilityCompetencies,
                      GradeIds    AS GradeIdsCsv,     -- alias for FE
                      DivisionIds AS DivisionIdsCsv   -- alias for FE
                  FROM dbo.PM_CheckInRule
                 WHERE SettingsId = @sid
                 ORDER BY RuleId",
                new { sid }
            )).ToList();

            return new PmSettingsGetResult
            {
                Header = header,
                Rules = rules,
                RuleGrades = new List<object>(),     // no longer used
                RuleDivisions = new List<object>()   // no longer used
            };
        }

    }

    public sealed class FiscalYearDto
    {
        public int FYId { get; set; }
        public string FiscalYear { get; set; } = string.Empty;
    }

    public sealed class GradeDto
    {
        public int GradeID { get; set; }
        public string GradeName { get; set; } = "";
    }

    public sealed class DivisionDto
    {
        public int DivisionID { get; set; }
        public string DivisionName { get; set; } = "";
    }

    public sealed class PmSettingsGetResult
    {
        public PmSettingsDto Header { get; set; } = new();
        public List<RuleRowDto> Rules { get; set; } = new();
        public List<object> RuleGrades { get; set; } = new();     // kept for FE compatibility
        public List<object> RuleDivisions { get; set; } = new();
    }

    public sealed class RuleRowDto
    {
        public int RuleId { get; set; }
        public int FYId { get; set; }
        public int FrequencyDays { get; set; }
        public bool VisibilityCompetencies { get; set; }

        // FE expects "*Csv" names (we alias from table)
        public string GradeIdsCsv { get; set; }
        public string DivisionIdsCsv { get; set; }
    }

    public sealed class PmSettingsDto
    {
        public int? SettingsId { get; set; }
        public int CompanyId { get; set; }
        public int? PGId { get; set; }
        public DateTime? EffectiveFrom { get; set; }

        public int? ReminderDaysAfterDue { get; set; }

        public byte? ObjectiveChangeCutoffMonth { get; set; }
        public bool FurtherApprovalRequired { get; set; }
        public bool AllowHoDChangeBeyondCutoff { get; set; }
        public bool DeptOrDivisionGoalsMandatory { get; set; }

        public bool Comp_NoWeightageChange { get; set; }
        public bool Comp_NoAddOrRemove { get; set; }
        public bool Comp_NoTargetRatingChange { get; set; }
        public bool Comp_NoCoreCompetencyMarking { get; set; }
        public bool Comp_NoAddEditDeleteImport { get; set; }

        public bool EmpInfo_EmployeeCode { get; set; }
        public bool EmpInfo_Division { get; set; }
        public bool EmpInfo_Location { get; set; }
        public bool EmpInfo_PayrollGroup { get; set; }
        public bool EmpInfo_EmployeeCategory { get; set; }
        public bool EmpInfo_DirectReportingTo { get; set; }
        public bool EmpInfo_GrossSalary { get; set; }
        public bool EmpInfo_EmployeeName { get; set; }
        public bool EmpInfo_Department { get; set; }
        public bool EmpInfo_Region { get; set; }
        public bool EmpInfo_Designation { get; set; }
        public bool EmpInfo_EmployeeType { get; set; }
        public bool EmpInfo_IndirectReportingTo { get; set; }
        public bool EmpInfo_DateOfJoining { get; set; }
        public bool EmpInfo_DateOfBirth { get; set; }
        public bool EmpInfo_SubDepartment { get; set; }
        public bool EmpInfo_Grade { get; set; }
        public bool EmpInfo_EmployeeStatus { get; set; }
        public bool EmpInfo_BasicSalary { get; set; }
        public bool EmpInfo_DateOfConfirmation { get; set; }

        public bool Other_AttendanceInformation { get; set; }
        public bool Other_LeaveDetails { get; set; }
        public bool Other_Qualifications { get; set; }
        public bool Other_CertificationDetails { get; set; }
        public bool Other_DisciplinaryActions { get; set; }
        public bool Other_Awards { get; set; }
        public bool Other_JobDescription { get; set; }

        public bool Trend_Enabled { get; set; }
        public byte? Trend_YearRange { get; set; }
        public bool IncProm_Enabled { get; set; }
        public byte? IncProm_YearRange { get; set; }
        public bool RoundOverallScore { get; set; }

        public bool OAE_CompanyGoals { get; set; }
        public bool OAE_DivisionGoals { get; set; }
        public bool OAE_DepartmentGoals { get; set; }
        public bool OAE_SubDepartmentGoals { get; set; }

        public List<CheckInRuleDto> Rules { get; set; } = new();
    }

    public sealed class CheckInRuleDto
    {
        public int TempRowId { get; set; }
        public int FYId { get; set; }
        public int FrequencyDays { get; set; }
        public bool VisibilityCompetencies { get; set; }

        public List<int> GradeIds { get; set; } = new();
        public List<int> DivisionIds { get; set; } = new();
    }



}
