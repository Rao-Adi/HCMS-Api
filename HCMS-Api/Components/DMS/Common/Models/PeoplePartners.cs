using HCMS_Api.Common.Misc;
using System.Collections.Generic;

namespace HCMS_Api.Components.DMS.Common.Models;

public class PeoplePartners
{
}

public class EmployeeCreateDto
{ 
    public int? CompanyId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string MobileNumber { get; set; }
    public int? dptId { get; set; }
    public int? dsgId { get; set; }
    public DateTime? DateofBirth { get; set; }
    public DateTime? DateJoin { get; set; }
    public string ReportTo { get; set; }
}

public class EmployeeFilterDto : TableFiltersDto
{
    public int? ReportingTo { get; set; } // e.g. reportingto >= n
    public int? DesignationId { get; set; } // Maps to e.dsgId
    public int? RoleId { get; set; } // Maps to TblEmpJobProfile.RoleId
    public bool? IsHeadOfDivision { get; set; }
    public bool? IsHeadOfDepartment { get; set; }
    public bool? IsHeadOfSubDepartment { get; set; }
}


public class tblEmployee
{
    public int EmpId { get; set; }
    public string EmpCode { get; set; }
    public int? CompanyId { get; set; }
    public string EmpRefNo { get; set; }
    public string FirstName { get; set; }
    public string MidName { get; set; }
    public string LastName { get; set; }
    public int? TitleID { get; set; }
    public string Initial { get; set; }
    public string Fname { get; set; }
    public int? gndId { get; set; }
    public string NIC { get; set; }
    public string NICnew { get; set; }
    public string NTN { get; set; }
    public DateTime? DateofBirth { get; set; }
    public string Address { get; set; }
    public int? ctyId { get; set; }
    public string Phone { get; set; }
    public string PhoneCntCode { get; set; }
    public string PhoneCtyCode { get; set; }
    public string PhoneNumber { get; set; }
    public string Phone2 { get; set; }
    public string Phone2CntCode { get; set; }
    public string Phone2CtyCode { get; set; }
    public string Phone2Number { get; set; }
    public string Mobile { get; set; }
    public string MobileCode { get; set; }
    public string MobileNumber { get; set; }
    public string Email { get; set; }
    public int? rlgId { get; set; }
    public int? mrtId { get; set; }
    public int? bldId { get; set; }
    public int? nationalityid { get; set; }
    public int? qlfId { get; set; }
    public string EmgPerson { get; set; }
    public string EmgPhone { get; set; }
    public string EmgAddress { get; set; }
    public int? MdptId { get; set; }
    public int dptId { get; set; }
    public int? dsgId { get; set; }
    public int? cmpid { get; set; }
    public int? brnId { get; set; }
    public int? typId { get; set; }
    public DateTime? DateJoin { get; set; }
    public DateTime? DateConfirm { get; set; }
    public DateTime? CDueDate { get; set; }
    public DateTime? ExtDate { get; set; }
    public string JobDes { get; set; }
    public int? Active { get; set; }
    public string JobId { get; set; }
    public string NotePad { get; set; }
    public int? ReportTo { get; set; }
    public int? Dotted { get; set; }

    public bool Medical { get; set; }
    public bool GShift { get; set; }
    public decimal Gross { get; set; }
    public DateTime? PDate { get; set; }
    public DateTime? LastIncDate { get; set; }
    public string Height { get; set; }
    public string Weight { get; set; }
    public string EyeColor { get; set; }
    public string Glass { get; set; }
    public string MedicalHistory { get; set; }
    public short? Disabilities { get; set; }
    public string DisReason { get; set; }
    public int? PGId { get; set; }
    public string RelName { get; set; }
    public string RelWorking { get; set; }
    public int? RelationshipId { get; set; }
    public int? RelPositionId { get; set; }
    public int? SectId { get; set; }
    public int? AreaId { get; set; }
    public int? MTId { get; set; }
    public int? DivId { get; set; }
    public string UId { get; set; }
    public string PWD { get; set; }
    public int? VDesigId { get; set; }
    public int? VDesigId2 { get; set; }
    public int? BaseId { get; set; }
    public int? RegionId { get; set; }
    public int? MTeamId { get; set; }
    public int? TeamId { get; set; }
    public float? ExpYr { get; set; }
    public int? EditBy { get; set; }
    public int? ConfirmBy { get; set; }
    public short? Hold { get; set; }
    public short? IsDirty { get; set; }
    public int? DispID { get; set; }
    public string Identification { get; set; }
    public short? SDWId { get; set; }
    public int? DomId { get; set; }
    public int? InsurerId { get; set; }
    public string GrpInsrNum { get; set; }
    public decimal? GrpInsrAmt { get; set; }
    public decimal? SumInsured { get; set; }
    public int? AssetStatus { get; set; }
    public string LReason { get; set; }
    public string? Shift { get; set; }
    public bool AutoPresent { get; set; }
    public string PGIds { get; set; }
    public int? CountryId { get; set; }
    public DateTime? OfferDate { get; set; }
    public int? AppId { get; set; }
    public string VaccinationHistory { get; set; }
    public DateTime? ContractExpireDate { get; set; }
    public short? ProratedLeave { get; set; }
    public string PassportNo { get; set; }
    public string FatherNIC { get; set; }
    public int? Dgid { get; set; }
    public int? Dutyid { get; set; }
    public int? PayrollStatus { get; set; }
    public decimal? SLICAmount { get; set; }
    public decimal? TotalSLICAmount { get; set; }
    public int? OPDID { get; set; }
    public string Email2 { get; set; }
    public string EmgMobile { get; set; }
    public int? MdptIdCurrent { get; set; }
    public int? MdptIdOld { get; set; }
    public int? dptIdCurrent { get; set; }
    public int? dptIdOld { get; set; }
    public int? dsgIdCurrent { get; set; }
    public int? dsgIdOld { get; set; }
    public int? JobIdCurrent { get; set; }
    public int? PGIdCurrent { get; set; }
    public int? ReportToCurrent { get; set; }
    public DateTime? CNICExpiryDate { get; set; }
    public DateTime? InternExpiryDate { get; set; }
    public bool HiringChecklistProcess { get; set; }
    public int? OldEmpId { get; set; }
    public DateTime? LeavingDate { get; set; }
    public bool InterCompanyTransfer { get; set; }
    public int? SourceCompanyId { get; set; }
    public int? TargetCompanyId { get; set; }
    public int? OriginalCompanyId { get; set; }
    public int? SourceEmpId { get; set; }
    public int? TargetEmpId { get; set; }
    public int? OriginalEmpId { get; set; }
    public bool InterCompanyInduction { get; set; }
    public DateTime? lastWorkingDate { get; set; }
    public DateTime? ReportingDate { get; set; }
    public int? BaseCntId { get; set; }
    public int? DispCntId { get; set; }
    public int? ReportToCompany { get; set; }
    public int? relId { get; set; }
    public int? DottedReportToCompany { get; set; }
    public DateTime? PassportExpiryDate { get; set; }
    public string UserId { get; set; }
    public string FormId { get; set; }
    public int? UserEmpId { get; set; }
    public string UserEmpName { get; set; }
    public string EntTerminal { get; set; }
    public string EntTerminalIP { get; set; }
    public string UserEmpCode { get; set; }
    public string VisaNumer { get; set; }
    public DateTime? VisaExpiryDate { get; set; }
    public string VisaNumber { get; set; }
    public DateTime? nextincDate { get; set; }
    public int? Matchedwithblacklist { get; set; }
    public int? processmanualmap { get; set; }
    public int? duplicatedRecordId { get; set; }
    public int? duplicatedMapEmpId { get; set; }
    public string IdCardRemarks { get; set; }
    public string FamilyCardNo { get; set; }
    public string IqamaNo { get; set; }
    public DateTime? IqamaExpiryHijri { get; set; }
    public DateTime? IqamaExpiryGregorian { get; set; }
    public string CurrSpnsName { get; set; }
    public int? SpnsTransferable { get; set; }
    public int? SpnsType { get; set; }
    public int? SpnsCountry { get; set; }
    public int? SpnsCity { get; set; }
    public string SpnsContactDetails { get; set; }
    public string SpnsNatureOfBusiness { get; set; }
    public int? IqamaProfession { get; set; }
    public DateTime? SpnsExpiryHijri { get; set; }
    public DateTime? SpnsExpiryGregorian { get; set; }
    public string FirstNameArabic { get; set; }
    public string MidNameArabic { get; set; }
    public string LastNameArabic { get; set; }
    public int? TitleIDArabic { get; set; }
    public int? ValidDrivingLicenseKSA { get; set; }
    public string HstOfPersecution { get; set; }
    public string HstOfPenalties { get; set; }
    public string PendingCases { get; set; }
    public int? SpnsCategory { get; set; }


    public int? NoOfSpnsChangedOfVisa { get; set; }
    public int? EmpCategoryId { get; set; }
    public DateTime? AutoPresentFromDate { get; set; }
    public DateTime? AutoPresentToDate { get; set; }
    public string TransactionSource { get; set; }
    public int? SalaryChangedStatus { get; set; }
    public int? ReviewTransactionId { get; set; }
    public string HrSeries { get; set; }
    public bool? IsLeaveAllocated { get; set; }
    public bool? IsEmployeeSalary { get; set; }
    public bool? IsHiringChecklistFinalized { get; set; }
    public bool? IsEmployeeProfileExtended { get; set; }
    public bool? IsEmployeeExpenseEntitlement { get; set; }
    public bool? IsEmployeeJD { get; set; }
    public bool? IsApprovalForPayroll { get; set; }
    public bool? IsUserId { get; set; }
    public string PreferredCulture { get; set; }
    public int? DomCntId { get; set; }
    public DateTime? EntDate { get; set; }
    public string ApplicationId { get; set; }
    public DateTime? RetirementDate { get; set; }
    public int? SalaryReviewChangedStatus { get; set; }
    public byte[]? TimeStamp { get; set; }
    public int? JobIdOld { get; set; }
    public int? PGIdOld { get; set; }
    public int? ReportToOld { get; set; }
    public bool? FlexiShift { get; set; }
    public int? FlexiType { get; set; }
    public decimal? RequiredHours { get; set; }
    public string EditByUser { get; set; }
    public bool? secondment { get; set; }
    public string PhotoPath { get; set; }
    public string CompanyShortName { get; set; }
    public string ACids { get; set; }
    public string DrivingLicenseNo { get; set; }
    public DateTime? DrivingLicenseExpiryDate { get; set; }
    public bool? isSpouseEmployed { get; set; }
    public bool? isAnyOtherIncomeSource { get; set; }
    public bool? isAnyPhysicalDisability { get; set; }
    public string OtherIncomeSourceDetails { get; set; }
    public string PhysicalDisabilityDetails { get; set; }
    public int? ResidentialStatusId { get; set; }
    public string FatherHusbandPhone { get; set; }
    public string FatherHusbandOccupation { get; set; }
    public bool? isOwnConveyance { get; set; }
    public int? ConveyanceType { get; set; }
    public string ConveyanceMake { get; set; }
    public string ConveyanceModel { get; set; }
    public string ConveyanceYear { get; set; }
    public string ConveyanceRegisterationNo { get; set; }
    public string PhoneExtension { get; set; }
    public string SittingLocation { get; set; }

}

 

public class tblSetupsdetail
{
    public int sdlid { get; set; }
    public string Code { get; set; }
    public int smsid { get; set; }
    public string Name { get; set; }

    public DateTime LastUpdate { get; set; }
    public int CompanyId { get; set; }
    public int UserEmpId { get; set; }
    public string EntOperation { get; set; }
    public DateTime EntDate { get; set; }
    public string UserEmpCode { get; set; }
    public string EntTerminalIP { get; set; }
    public string EntTerminal { get; set; }
    public string UserEmpName { get; set; }
    public string FormId { get; set; }
    public string UserId { get; set; }
    public string ApplicationId { get; set; }
    public byte[] ChngTimeStamp { get; set; }
    public bool Inactive { get; set; }
}

public class tblDeptstrMaster
{
    public int dptMId { get; set; } // Primary Key (not null)
    public int? MdptId { get; set; }
    public int? CompanyId { get; set; }
    public float? Budgeted { get; set; }
    public float? Approved { get; set; }
    public int? divId { get; set; }
    public string UserId { get; set; }
    public string ApplicationID { get; set; }
    public string FormId { get; set; }
    public int? UserEmpId { get; set; }
    public string UserEmpName { get; set; }
    public string UserEmpCode { get; set; }
    public string EntTerminal { get; set; }
    public string EntTerminalIP { get; set; }
    public string EntOperation { get; set; }
    public DateTime? EntDate { get; set; } // Use Nullable DateTime
}

public class tblDeptstrDetail
{
    public int dptDId { get; set; }
    public int dptMId { get; set; }
    public int dptId { get; set; }
    public float Budgeted { get; set; }
    public float Approved { get; set; }
    public int companyId { get; set; }
    public string UserId { get; set; }
    public string ApplicationID { get; set; }
    public string FormId { get; set; }
    public int UserEmpId { get; set; }
    public string UserEmpName { get; set; }
    public string UserEmpCode { get; set; }
    public string EntTerminal { get; set; }
    public string EntTerminalIP { get; set; }
    public string EntOperation { get; set; }
    public DateTime? EntDate { get; set; }


}
public class TblEmpJobProfile
{
    public int JobProfileId { get; set; }
    public int EmpId { get; set; }
    public string JobCode { get; set; } = string.Empty;
    public DateTime JobCDate { get; set; }
    public string? JobTitle { get; set; }

    public int MdptId { get; set; }
    public int DptId { get; set; }
    public int JobId { get; set; }
    public int BaseId { get; set; }
    public int TypId { get; set; }

    public string? ReportingRelationship { get; set; }
    public string? KeyRelationshipInt { get; set; }
    public string? KeyRelationshipExt { get; set; }

    public string? AcademicQualifications { get; set; }
    public string? WorkExperience { get; set; }
    public string? JobSummary { get; set; }
    public string? KeyPerformanceIndicators { get; set; }
    public string? Responsibilities { get; set; }
    public string? Accountabilities { get; set; }
    public string? EOWorkCondition { get; set; }

    public DateTime? EmpDate { get; set; }
    public int? MgrEmpId { get; set; }
    public DateTime? MgrDate { get; set; }
    public int? HrEmpId { get; set; }
    public DateTime? HRDate { get; set; }

    public int CompanyId { get; set; }

    public byte[]? AppDoc { get; set; }

    public int? DivId { get; set; }
    public bool? DirectRptTo { get; set; }

    public int? DCompanyId { get; set; }
    public int? DGradeId { get; set; }
    public int? DDsgId { get; set; }
    public int? DDivId { get; set; }
    public int? DMdptId { get; set; }
    public int? DDptId { get; set; }
    public int? DBaseId { get; set; }

    public bool? InDirectRptTo { get; set; }

    public int? IndCompanyId { get; set; }
    public int? IndGradeId { get; set; }
    public int? IndDsgId { get; set; }
    public int? IndDivId { get; set; }
    public int? IndMdptId { get; set; }
    public int? IndDptId { get; set; }
    public int? IndBaseId { get; set; }

    public string? PAgeRange { get; set; }
    public int? PrefGender { get; set; }
    public string? EYears { get; set; }
    public int? DsgId { get; set; }
    public string? RExpYears { get; set; }

    public bool? Chkqual { get; set; }
    public bool? Chkcert { get; set; }
    public bool? Chktrain { get; set; }

    public bool? IsDescripancyInCompetency { get; set; }
    public bool? IsDescripancyInKPI { get; set; }

    public int? JdId { get; set; }
    public bool? Active { get; set; }

    public int? PageRangeTo { get; set; }
    public int? EYearsTo { get; set; }
    public int? RExpYearsTo { get; set; }

    public string? IncPackage { get; set; }

    public string? OtherBenifit { get; set; }
    public string? SalaryRemarks { get; set; }

    public int? VisaStatus { get; set; }

    public decimal? SalaryAmountFrom { get; set; }
    public decimal? SalaryAmountTo { get; set; }

    public int? SalaryCurrencyId { get; set; }
    public int? PrefReligion { get; set; }

    public DateTime? AssignedDate { get; set; }

    public string? UserId { get; set; }
    public string? ApplicationID { get; set; }
    public string? FormId { get; set; }

    public int? UserEmpId { get; set; }
    public string? UserEmpName { get; set; }
    public string? UserEmpCode { get; set; }

    public string? EntTerminal { get; set; }
    public string? EntTerminalIP { get; set; }
    public string? EntOperation { get; set; }
    public DateTime? EntDate { get; set; }

    public int? EmoIntlProfiling { get; set; }
    public int? RoleId { get; set; }
    public int? DRoleId { get; set; }
    public int? IndRoleId { get; set; }

    public bool? Received { get; set; }

    public string? JDChangeVersionNo { get; set; }

    public DateTime? AcknowledgeOn { get; set; }
    public string? AcknowledgeByUserID { get; set; }
    public string? AcknowledgeTerminal { get; set; }
    public int? AcknowledgeByEmpID { get; set; }

    public string? ChangeResponsibilityDetail { get; set; }
}
