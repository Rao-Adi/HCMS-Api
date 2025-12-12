using Microsoft.Extensions.Localization;

namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class Constants
    {
        private readonly IStringLocalizer<Constants> _localizer;
        public Constants(IStringLocalizer<Constants> localizer)
        {
            _localizer = localizer;
        }
        #region Alerts

        public string MRPIname => GetString("MRPIname");

        public string MPRDateofI => GetString("MPRDateofI");
        public string MPRDesig => GetString("MPRDesig");
        public string MPRAssessType => GetString("MPRAssessType");
        public string MPRCode => GetString("MPRCode");
        public string MPRAppName => GetString("MPRAppName");

        //Approved Manpower Requests(Open)
        public string CreatedBy => GetString("CreatedBy");
        public string DateOfRequest => GetString("DateOfRequest");

        // Hiring Checklist (In-Process)(for Applicant)
        public string AppNo => GetString("AppNo");
        public string AppName => GetString("AppName");
        public string Des => GetString("Designation");
        public string JoiningDate => GetString("JoiningDate");

        //Interview Evaluations (Pending)
        public string IntName => GetString("IntName");
        public string DateOfInt => GetString("DateOfInt");
        public string AssType => GetString("AssType");

        // MPR not Published Yet
        public string JobTitle => GetString("JobTitle");

        // List of Applicants On Hold after Interview
        public string ApplicantNo => GetString("ApplicantNo");
        public string StatusDate => GetString("StatusDate");
        public string EmployeeCode => GetString("EmployeeCode");
        public string EmployeeName => GetString("EmployeeName");
        public string DOB => GetString("DOB");

        // Hiring Checklist (Finalized)(for Applicant) in last x days
        public string FinalizedDate => GetString("FinalizedDate");

        // Interviews Scheduled in next x days
        public string InterviewNo => GetString("InterviewNo");
        public string IntTime => GetString("IntTime");
        public string Venue => GetString("Venue");

        // Approved Manpower Requests [To Be Close]
        public string ClosingDate => GetString("ClosingDate");

        // Avg. Satisfaction Level (Appraisal Year to Date)
        public string MonthYear => GetString("MonthYear");
        public string AvgRating => GetString("AvgRating");
        public string Dept => GetString("Dept");

        //Employee Certificates expiring in next x days
        public string Certificate => GetString("Certificate");
        public string DateAchieved => GetString("DateAchieved");
        public string ExpiryDate => GetString("ExpiryDate");

        // Employee confirmations due in next x days
        public string ConfDueDate => GetString("ConfDueDate");
        public string ConfRecommendation => GetString("ConfRecommendation");
        public string RecExtendDate => GetString("RecExtendDate");
        public string DateofConfExtended => GetString("DateofConfExtended");

        // Employees reaching retirement age in next x calendar months
        public string DORetirement => GetString("DORetirement");

        // Employees With Pending Approval for Payroll
        public string DOJoining => GetString("DOJoining");

        // Unassigned Tickets
        public string TicketNo => GetString("TicketNo");
        public string Employee => GetString("Employee");
        public string InitiatedOn => GetString("InitiatedOn");
        public string TicketType => GetString("TicketType");

        // Medical Cards expiring in next x days
        public string MedicalCardNo => GetString("MedicalCardNo");

        // Misc. Alerts expiring in next x days
        public string AlertType => GetString("AlertType");
        public string Remarks => GetString("Remarks");

        // Tickets Requiring Response (Open)
        public string InitiatedFrom => GetString("InitiatedFrom");
        public string AssignedTo => GetString("AssignedTo");
        public string LastResponseOn => GetString("LastResponseOn");

        //GPS Tracking Fee (due in next x days)

        public string AssetNo => GetString("AssetNo");

        // public string AssetNo = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("AlertsHeadings", "AssetNo")).ToString();
        public string AssetName => GetString("AssetName");
        public string Vendor => GetString("Vendor");
        public string Fees => GetString("Fees");
        public string TrackingFeeDueDate => GetString("TrackingFeeDueDate");

        //Insurance Renewal (due in next x days)
        public string Insurer => GetString("Insurer");

        //Leasing Installments (due in next x days)
        public string Leaser => GetString("Leaser");
        public string RenewalDate => GetString("RenewalDate");

        //List of Expired Assets [Not Disposed] (Assigned / Un-Assigned)
        public string AssetCat => GetString("AssetCat");
        public string AcqDate => GetString("AcqDate");

        //List of Un-Assigned Assets
        public string Expired => GetString("Expired");

        //Vehicle Taxes (expiring in next x days)
        public string TaxExpiry => GetString("TaxExpiry");

        //Leave Days Approved against Holidays/Off Days (falling in last x days)
        public string LeaveDate => GetString("LeaveDate");
        public string LeaveCode => GetString("LeaveCode");
        public string DayStatus => GetString("DayStatus");

        //Absents (3 or more) (current calendar month)
        public string NoOfLeaves => GetString("NoOfLeaves");

        //Early-Out (more than x days in current calendar month)
        public string Name => GetString("Name");
        public string EarlyOutDays => GetString("EarlyOutDays");

        //Employees with X or more absents in last consecutive days
        public string Location => GetString("Location");
        public string ConsecutiveAbsentDays => GetString("ConsecutiveAbsentDays");

        //Late-In (more than x days in current quarter)
        public string LateInDays => GetString("LateInDays");

        //OT Budget Revisions (Approved / Not approved)
        public string FiscalYear => GetString("FiscalYear");
        public string RequestedBy => GetString("RequestedBy");
        public string SubDept => GetString("SubDept");
        public string Status => GetString("Status");
        public string FinalizedOn => GetString("FinalizedOn");

        //Overtime Request (Approved / Not approved)
        public string RequestedFor => GetString("RequestedFor");
        public string DateFrom => GetString("DateFrom");
        public string DateTo => GetString("DateTo");
        public string OTHoursPerDay => GetString("OTHoursPerDay");
        public string ReqStatus => GetString("ReqStatus");
        public string StatusFinalizedOn => GetString("StatusFinalizedOn");

        //Roster Requests (pending)
        public string RosterDate => GetString("RosterDate");
        public string ProfileShift => GetString("ProfileShift");
        public string ReqShift => GetString("ReqShift");
        public string CurrentShift => GetString("CurrentShift");

        //Variable Allowance (Approved / Not Approved)
        public string VariableAllowance => GetString("VariableAllowance");
        public string AmountClaimed => GetString("AmountClaimed");
        public string Currency => GetString("Currency");
        public string Period => GetString("Period");

        //Employee Scheduled on Training Today
        public string Course => GetString("Course");
        public string StartDate => GetString("StartDate");
        public string EndDate => GetString("EndDate");
        public string InstName => GetString("InstName");
        public string TrainingID => GetString("TrainingID");


        //Employees to be planned for Orientation Courses
        public string TrainingCat => GetString("TrainingCat");
        public string TrainingSub => GetString("TrainingSub");

        //Recurring Courses (To Be Planned)
        public string RecurDate => GetString("RecurDate");
        public string NoOfEmpReqTraining => GetString("NoOfEmpReqTraining");
        public string EmpPlanned => GetString("EmpPlanned");
        public string EmpToBePlanned => GetString("EmpToBePlanned");
        public string NoOfDaysToRecur => GetString("NoOfDaysToRecur");

        //Training Effectiveness Feedback (Pending)
        public string Id => GetString("Id");
        public string CourseId => GetString("CourseId");
        public string ManagerName => GetString("ManagerName");
        public string TrainingDateFrom => GetString("TrainingDateFrom");
        public string TrainingDateTo => GetString("TrainingDateTo");
        public string CourseName => GetString("CourseName");

        //Employees Due For Probation Evaluation
        public string EmpCode => GetString("EmpCode");
        public string JobTenure => GetString("JobTenure");

        //List of Employees Recommended to be Separated via Probation Evaluation Finalization Sheet(Pending Action)
        public string DateOfConfDue => GetString("DateOfConfDue");
        public string DateOfProbationExtended => GetString("DateOfProbationExtended");
        public string SepRecWEF => GetString("SepRecWEF");

        //Employees (Clearance Status as Pending)
        public string LastWorkingDate => GetString("LastWorkingDate");

        //Employees Finalized for Inter Company Transfers
        public string TransfFrom => GetString("TransfFrom");
        public string TransfTo => GetString("TransfTo");
        public string TransfDate => GetString("TransfDate");

        //Resigned Employees
        public string ResignDate => GetString("ResignDate");

        //Root Cause Analysis (Pending)
        public string Resignee => GetString("Resignee");
        public string RootCauseToBeFilledBy => GetString("RootCauseToBeFilledBy");

        //Separated Employees (defined as ‘Reporting To’)
        public string Direct_IndirectRepTo => GetString("Direct_IndirectRepTo");

        //Applicant Test Scheduled in next x days
        public string DateOfTest => GetString("DateOfTest");
        public string TestTime => GetString("TestTime");
        public string Admin => GetString("Admin");
        public string TestName => GetString("TestName");

        //Approved / Not Approved Budgets
        public string Division => GetString("Division");
        public string Team => GetString("Team");

        //Employee future intra-Company requests
        public string TransferEffectiveFrom => GetString("TransferEffectiveFrom");
        public string TransferEffectiveTo => GetString("TransferEffectiveTo");

        public string strYes => GetString("strYes");
        public string strNo => GetString("strNo");
        public string strNotAlocated => GetString("strNotAlocated");


        #endregion


        #region Errorpage Errors

        // Errorpage Errors
        public const String err100 = "err100";
        public const String err101 = "err101";
        public const String err200 = "err200";
        public const String err201 = "err201";
        public const String err202 = "err202";
        public const String err203 = "err203";
        public const String err204 = "err204";
        public const String err205 = "err205";
        public const String err206 = "err206";
        public const String err300 = "err300";
        public const String err301 = "err301";
        public const String err302 = "err302";
        public const String err303 = "err303";
        public const String err304 = "err304";
        public const String err305 = "err305";
        public const String err400 = "err400";
        public const String err401 = "err401";
        public const String err402 = "err402";
        public const String err403 = "err403";
        public const String err404 = "err404";
        public const String err405 = "err405";
        public const String err406 = "err406";
        public const String err407 = "err407";
        public const String err408 = "err408";
        public const String err409 = "err409";
        public const String err410 = "err410";
        public const String err411 = "err411";
        public const String err412 = "err412";
        public const String err413 = "err413";
        public const String err414 = "err414";
        public const String err415 = "err415";
        public const String err500 = "err500";
        public const String err501 = "err501";
        public const String err502 = "err502";
        public const String err503 = "err503";
        public const String err504 = "err504";
        public const String err505 = "err505";


        // Errorpage Messages
        public const String err100Msg = "Error 100 : Please contact system administrator.";
        public const String err101Msg = "Error 101 : Please contact system administrator.";
        public const String err200Msg = "Error 200 : Please contact system administrator.";
        public const String err201Msg = "Error 201 : Please contact system administrator.";
        public const String err202Msg = "Error 202 : Please contact system administrator.";
        public const String err203Msg = "Error 203 : Please contact system administrator.";
        public const String err204Msg = "Error 204 : Please contact system administrator.";
        public const String err205Msg = "Error 205 : Please contact system administrator.";
        public const String err206Msg = "Error 206 : Please contact system administrator.";
        public const String err300Msg = "Error 300 : Please contact system administrator.";
        public const String err301Msg = "Error 301 : Please contact system administrator.";
        public const String err302Msg = "Error 302 : Please contact system administrator.";
        public const String err303Msg = "Error 303 : Please contact system administrator.";
        public const String err304Msg = "Error 304 : Please contact system administrator.";
        public const String err305Msg = "Error 305 : Please contact system administrator.";
        public const String err400Msg = "Error 400 : Please contact system administrator.";
        public const String err401Msg = "Error 401 : Please contact system administrator.";
        public const String err402Msg = "Error 402 : Please contact system administrator.";
        public const String err403Msg = "Error 403 : Please contact system administrator.";
        public const String err404Msg = "Error 404 : Please contact system administrator.";
        public const String err405Msg = "Error 405 : Please contact system administrator.";
        public const String err406Msg = "Error 406 : Please contact system administrator.";
        public const String err407Msg = "Error 407 : Please contact system administrator.";
        public const String err408Msg = "Error 408 : Please contact system administrator.";
        public const String err409Msg = "Error 409 : Please contact system administrator.";
        public const String err410Msg = "Error 410 : Please contact system administrator.";
        public const String err411Msg = "Error 411 : Please contact system administrator.";
        public const String err412Msg = "Error 412 : Please contact system administrator.";
        public const String err413Msg = "Error 413 : Please contact system administrator.";
        public const String err414Msg = "Error 414 : Please contact system administrator.";
        public const String err415Msg = "Error 415 : Please contact system administrator.";
        public const String err500Msg = "Error 500 : Please contact system administrator.";
        public const String err501Msg = "Error 501 : Please contact system administrator.";
        public const String err502Msg = "Error 502 : Please contact system administrator.";
        public const String err503Msg = "Error 503 : Please contact system administrator.";
        public const String err504Msg = "Error 504 : Please contact system administrator.";
        public const String err505Msg = "Error 505 : Please contact system administrator.";

        #endregion

        public const String MainPage = "MainPage";

        #region Recruitment Manager
        public const String APPLICANTPROFILEBASIC = "ApplicantProfileBasic";
        public const String AdvertisementInfo = "AdvertisementInfo";
        public const String AdWiseAppDetail = "AdWiseAppDetail";
        public const String JobWiseAppList = "JobWiseAppList";
        public const String APPLICANTPROFILEEXTENDED = "ApplicantProfileExtended";
        public const String INTERVIEWQUESTION = "InterviewQuestion";
        public const String MPR = "MPR";
        public const String MPRStatus = "MPRStatus";
        public const String RECRUITMENTSETUP = "RecruitmentSetup";
        public const String SKILLSSETUP = "SkillsSetup";
        public const String TEMPLATECREATION = "TemplateCreation";
        public const String TESTQUESTIONS = "TestQuestions";
        public const String TESTINSTRUCTIONSETUP = "TestInstructionSetup";
        public const String TestWiseInstructions = "TestWiseInstructions";
        public const String COMPETENCYSKILLSSETUP = "CompetencySkillsSetup";
        public const String WORKFLOWSETUP = "WorkFlowSetup";
        public const String TESTCREATION = "TestCreation";
        public const String TESTMANAGER = "TestManager";
        public const String MAPPINGCVS = "MappingCVs";
        public const String TRASHBANK = "TrashBank";
        public const String OFFERINGNAPPOINTMENTLETTER = "OfferingnAppointmentLetter";
        public const String EmailSetup = "EmailSetup";
        public const String ApplicantAnswerSheet = "ApplicantAnswerSheet";
        public const String RecruitmentPositions = "RecruitmentPositions";
        public const String ResumeBank = "ResumeBank";
        public const String ApplicantSalarySetup = "ApplicantSalarySetup";
        public const String Offerletter = "ApplicantOfferLetter";
        public const String ApplicantStatusConfig = "ApplicantStatusConfig";

        public const String ApplicantQuestionSetup = "ApplicantQuestionSetup";
        public const String InterviewManager = "InterviewManager";
        public const String InterviewEvaluation = "InterviewEvaluation";
        public const String TestPercentRanges = "TestPercentRanges";
        public const String TestStatusReport = "TestStatusReport";
        public const String TransportArrangement = "TransportArrangement";
        public const String POTENTIALDUPLICATEAPPLICANT = "POTENTIALDUPLICATEAPPLICANT";
        public const String InterviewSetup = "InterviewSetup";
        public const String AppSalarySetup = "AppSalarySetup";
        public const String AssesmentCriteria = "AssesmentCriteriaSetup";
        public const String AssesmentCriteriaDimension = "AssesmentDimensionsSetup";
        public const String AssesmentRatingsetup = "AssesmentRatingsSetup";

        public const String ExternalInterviewSetups = "ExternalInterviewersSetup";
        public const String CCFieldsSetup = "CCFieldsSetup";
        //Created by Ahsan Ahmed
        public const string CompulsoryDataOnCC = "CompulsoryDataOnCC";
        //-----------------------
        // Reports
        public const String DuplicateResume = "DuplicateResume";
        public const String ApplicantHistory = "ApplicantHistory";
        public const String ApplicantStatus = "ApplicantStatus";
        public const String rptRecruitmentProcess = "rptRecruitmentProcess";
        public const String rptTestSchedule = "rptTestSchedule";
        public const String rptPriorityWiseClass = "rptPriorityWiseClass";
        public const String rptStatusOfReqDept = "rptStatusOfReqDept";
        public const String rptStatusOfReqDivision = "rptStatusOfReqDivision";
        public const String CandidateList = "CandidateList";
        public const String InterviewSchedule = "InterviewSchedule";
        public const String TestSchedule = "TestSchedule";
        public const String InterviewScheduleRpt = "InterviewScheduleRpt";
        public const String ActionPlanForInduction = "ActionPlanForInduction";
        public const String MPRJobStatusSummary = "MPRJobStatusSummary";
        public const string ISCatSetup = "ISCatSetup";
        public const string ISSubCatSetup = "ISSubCatSetup";
        public const string ISDesigner = "ISDesigner";
        public const string ISRptDesigner = "ISRptDesigner";
        public const string ISDesignerForm = "ISDesignerForm";

        public const string ISDReport1 = "RptSingleInterview";
        public const string ISDReport2 = "RptMultipleInterview";
        public const string ISDReport3 = "RptInterviewResult";

        public const string TopApplicantReport = "RptTopApplicant";
        public const string RptInterviewResultsAssessmentBased = "RptInterviewResultsAssessmentBased";
        public const string RptInterviewEvaluation = "RptInterviewEvaluation";

        public const string PHCatSetup = "PHCatSetup";
        public const string PHSubCatSetup = "PHSubCatSetup";
        public const string PHEvalDesigner = "PHEvalDesigner";
        public const string PHEvalForm = "PHEvalForm";
        public const string PHEvalRptDesigner = "PHEvalRptDesigner";

        public const string TestDetailReport = "TestDetail";
        public const string TestDetailDateWiseReport = "TestDetailDateWise";
        public const string Compulsorypage = "CompulsoryDataOnCC";
        public const string CompentencyRating = "CompetencyRatingSetup";
        public const string RequirementLevel = "RequirementLevelSetup";
        public const string FrequencyLevel = "FrequencyLevelSetup";
        public const string CostEstimation_General = "CostEstimation_General";
        public const string CostEstimation_Employee = "CostEstimation_Employee";
        public const string TestSummaryReport = "TestSummaryReport";
        public const string TestCentreSectionwiseReport = "rptTestCentreSectionwiseSummary";
        public const string DeleteCCResume = "DeleteCCResume";
        public const string RecommendationFeedbacksetup = "RecommendationFeedbacksetup";
        public const string AHiring = "ApplicantHiring";
        //public const String InterviewScheduleRpt = "InterviewScheduleRpt";
        public const string rptApplicantDetail = "rptApplicantDetail";
        public const String RptAccidentInfo = "RptAccidentWiseInformation";
        public const string PMEmployeesAppraisalProcessStatus = "PMEmployeesAppraisalProcessStatus";
        public const string PMAppraisalEvaluationEmployeewise = "PMAppraisalEvaluationEmployeewise";
        public const string PMProbationEvaluationEmployeewise = "PMProbationEvaluationEmployeewise";
        public const string PMBalanceScoreCardEmployeewise = "PMBalanceScoreCardEmployeewise";
        public const string PMAppraisalResultsSummary = "PMAppraisalResultsSummary";
        public const string PMReviewResultsSummary = "PMReviewResultsSummary";
        public const string PMAppraisalResultsAnalysis = "PMAppraisalResultsAnalysis";
        public const string DepartmentSubDepartmentObjectives = "DepartmentSubDepartmentObjectives";


        #endregion

        #region Employee Manager

        //Report Form Constants 
        //-----------------------------------------Abbas Reports Constants---------------------
        public const String AreaCityCountryMapping = "AreaCityAndCountryMapping";
        public const String ChangeReportingTo = "ChangeReportingTo";
        public const String RptEmpINSameArea = "RptEmpINSameArea";
        public const String RptEmpBDayInfo = "RptEmpBDayInfo";
        public const String RptEmpCard = "RptEmpCard";
        public const String EmployeeCardReport = "EmployeeCardReport";
        public const String EmployeeLeaveYear = "EmployeeLeaveYear";
        public const String EmpSeparationForm = "EmpSeparationForm";
        public const String RptEmpCardDetail = "RptEmpCardDetail";
        public const String RptEmpCmpInfoDept = "RptEmpCmpInfoDept";
        public const String RptEmpCmpInfoJob = "RptEmpCmpInfojob";
        public const String RptEmpCmpInfoLoc = "RptEmpCmpInfoLocation";
        public const String RptEmpConfirmDue = "RptEmpConfirmDue";
        public const String rptEmpDeptWiseInfo = "RptEmpCmpInfoLocation";
        public const String RptEmpDirtyList = "RptEmpDirtyList";
        public const String RptEmpSameFatherName = "RptEmpSameFatherName";
        public const String RptEmpJoiningInfo = "RptEmpJoiningInfo";
        public const String RptEmpList = "RptEmpList";
        public const String RptEmployeeBloodGroupInfo = "RptEmployeeBloodGroupInfo";
        public const String RptEmpReligionInfo = "RptEmpReligionInfo";
        public const String RptEmpMT = "RptEmpMT";
        public const String rptEmpQlfWiseInfo = "rptEmpQlfWiseInfo";
        public const String RptEmpResignStatus = "RptEmpResignStatus";
        public const String RptEmpResignStatusBack = "RptEmpResignStatusBack";
        public const String RptEmpSalaryDetail = "RptEmpSalaryDetail";
        public const String RptEmpSalaryDetailDed = "RptEmpSalaryDetailDed";
        public const String RptEmpSalaryInfo = "RptEmpSalaryInfo";
        //public const String RptEmpSalaryInfoNew = "RptEmpSalaryInfoNew";
        public const String RptEmployeeSalaryList = "RptEmployeeSalaryList";
        public const String RptEmpSalarySetup = "RptEmpSalarySetup";
        public const String rptSalaryandEntitlementsVariation = "rptSalaryandEntitlementsVariation";
        // public const String RptEmpProfile = "RptEmployeeProfile";
        public const String AirTicketAllocationProcess = "AirTicketAllocationProcess";

        public const String RptEmpSect = "RptEmpSect";
        public const String RptSetupInformation = "RptSetupInformation";
        public const String RptWorkFlowInfo = "WorkFlowDefined";
        public const String RptEmpNormalRetire = "RptEmpNormalRetire";
        public const String rptEmpSalarySetupBankMode = "rptEmpSalarySetupBankMode";
        public const String EmpWithIncompleteData = "EmpWithIncompleteData";
        public const String rptEmpSelectedInfo = "rptEmpSelectedInfo";
        public const String RptEmpSelectedInfoNew = "RptEmpSelectedInfoNew";
        public const String RptEx_GratiaOT_Hours = "RptEx_GratiaOT_Hours";
        public const String WorkFlowAnalysis = "WorkFlowAnalysis";
        public const String ExpenseAnalysis = "ExpenseAnalysis";
        public const String rptEmpSelectedInfoExcel = "RptEmpSelectedInfoExcel";
        public const String RptEmpMedicalReimbursementMemo = "RptEmpMedicalReimbursementMemo";
        public const String RptEmpMedicalReimbursementInfo = "RptEmpMedicalReimbursementInfo";
        public const String RptEmpMedicalReimbursementYearly = "RptEmpMedicalReimbursementYearly";
        public const String AdminLogin = "AdminLogin";
        public const String BankDetail = "BankDetail";
        public const String CityAreaMapping = "CityAreaMapping";
        public const String OutStationMapping = "OutStationMapping";
        public const String rptEmployeeDocumentHistory = "rptEmployeeDocumentHistory";
        public const String RptEmpBlackListed = "RptEmpBlackListed";
        //public const String KPIObjectives = "KPIObjectives";
        //public const String KPIMeasures = "KPIMeasures";
        //public const String KPISetup = "KPISetup";
        //public const String KPIGraphSetup = "KPIGraphSetup";
        public const String RptSR = "RptSalaryRange";
        public const String RptHRHeadCount = "RptHRHeadCount";
        public const String rptEmpRelationMeasure = "rptEmpRelationMeasure";
        public const String RptTurnOverRate = "RptTurnOverRate";
        public const String RptGrievanceResolution = "RptGrievanceResolution";
        public const String rptJobDesFactor = "rptJobDesFactor";
        public const String RptEmpRecognitionRate = "RptEmpRecognitionRate";
        public const String TimeToFill = "TimeToFill";
        public const String TimeToStart = "TimeToStart";
        public const String RptEmpNoSalarySetup = "RptEmpNoSalarySetup";
        public const String RptEmpNotApproved = "RptEmpNotApproved";
        public const String rptEmpEntitlementDetails = "rptEmpEntitlementDetails";
        public const String rptEmpAuditTrail_Personnel = "rptEmpAuditTrail_Personnel";
        public const String rptEmpAuditTrail_Attendance = "rptEmpAuditTrail_Attendance";
        public const String rptEmpAuditTrail_Recruitment = "rptEmpAuditTrail_Recruitment";
        public const String rptEmpAuditTrail_Assets = "rptEmpAuditTrail_Assets";
        public const String rptEmpAuditTrail_Leave = "rptEmpAuditTrail_Leave";
        public const String rptEmpAuditTrail_Expense = "rptEmpAuditTrail_Expense";
        public const String rptEmpAuditTrail_Training = "rptEmpAuditTrail_Training";
        public const String rptEmpAuditTrail_Learning = "rptEmpAuditTrail_Learning";
        public const String rptEmpAuditTrail_Performance = "rptEmpAuditTrail_Performance";
        public const String rptEmpAuditTrail_Succession = "rptEmpAuditTrail_Succession";
        public const String rptEmpAuditTrail_Appraisal360 = "rptEmpAuditTrail_Appraisal360";
        public const String rptEmpAuditTrail_Survey = "rptEmpAuditTrail_Survey";
        public const String rptEmpAuditTrail_Task = "rptEmpAuditTrail_Task";
        public const String rptEmpAuditTrail_Collaboration = "rptEmpAuditTrail_Collaboration";
        public const String rptEmpAuditTrail_Separation = "rptEmpAuditTrail_Separation";
        public const String rptEmpAuditTrail_Metrics = "rptEmpAuditTrail_Metrics";
        public const String RptMiscAlerts = "RptMiscAlerts";
        public const String rptEmpSalaryReview = "rptEmpSalaryReview";
        public const String rptEmployeeCost = "rptEmployeeCost";
        public const String rptConfSalReview = "rptConfSalReview";
        public const String EmailContentCreation = "EmailContentCreation";
        public const String EFC_EmployeeTicket = "EFC_EmployeeTicket";
        public const String EFC_MessageType = "EFC_MessageType";
        public const String PriorityLevel = "PriorityLevel";
        public const String PersonalProfile = "PersonalProfile";
        public const string FormWisePayrollGroupRights = "FormWisePayrollGroupRights";
        public const string DashboardItemsPayrollGroupRights = "DashboardItemsPayrollGroupRights";
        public const String NonSalAllEmpwise = "NonSalAllEmpwise";
        public const String WorkflowRequestOwnershipTransfer = "WorkflowRequestOwnershipTransfer";
        public const String EFC_OwnershipAssignAndTransfer = "EFC_OwnershipAssignAndTransfer";
        //-----------------------------------------Jawwad Reports Constants--------------------

        public const String RptEmpDocList = "RptEmpDocList";
        public const String RptEmpAssetInformation = "RptEmpAssetInformation";
        public const String RptTransferInfo = "RptTransferInfo";
        public const String RptDependentInfoDepWise = "RptDependentInfoDepWise";
        public const String rptEmpCardPrinting = "rptEmpCardPrinting";
        public const String rptEmpProf = "rptEmpProf";
        public const String rptEmpInterCompTransfer = "rptEmpInterCompTransfer";
        public const String RptEmpDisplacement = "RptEmpDisplacement";
        public const String RptEmpIncLetter = "RptEmpIncLetter";
        public const String RptEmpMedicalHistory = "RptEmpMedicalHistory";
        public const String RptCmpMedicalSummary = "RptCmpMedicalSummary";
        public const String RptDptMedicalSummary = "RptDptMedicalSummary";
        public const String RptEducationWiseGraphView = "RptEducationWiseGraphView";
        public const String RptGenderWiseGraphView = "RptGenderWiseGraphView";
        public const String RptMSWiseGraphView = "RptMSWiseGraphView";
        public const String RptEmpAgeGraphView = "RptEmpAgeGraphView";
        public const String RptDptWiseGraphView = "RptDptWiseGraphView";
        public const String RptServiceDurationGView = "RptServiceDurationGView";
        public const String RptEmergencyLoanAF = "RptEmergencyLoanAF";
        public const String RptStaffVehicleFinanceAF = "RptStaffVehicleFinanceAF";
        public const String RptStaffHousingFinanceAF = "RptStaffHousingFinanceAF";
        public const String RptStaffHousingFinanceForm = "RptStaffHousingFinanceForm";
        public const String RptStaffEmergencyFinanceAF = "RptStaffEmergencyFinanceAF";
        public const String RptStaffEmergencyFinanceAppForm = "RptStaffEmergencyFinanceAppForm";
        public const String rptEmployeeCount = "rptEmployeecountReport";

        public const String RptTransferReport = "RptEmployeeTransfer";
        public const String RptGLIReport = "RptGroupInsuranceSummary";

        //Constant for hiring checklist
        public const String rptHiringChecklist = "rptHiringChecklist";

        public const String RptHIReport = "RptHealthInsuranceSummary";

        public const String EmpMedicalCardInfo = "EmpMedicalCardInfo";
        //Setup Form Constants
        public const String EarningDeductionStoppage = "DeductionSetup";
        public const String BirthDayGreetings = "BirthDayGreetings";
        public const String CountryCitySetup = "CountryCitySetup";
        public const String DeptStrength = "DeptStrength";
        public const String EmployeeCategory = "MappingCategoryType";
        public const String WorkFlowSetup = "WorkFlowSetup";
        public const String WorkFlowSetupwp = "WorkFlowSetup-wp";
        public const String Earnings = "Earnings";
        public const String Deductions = "Deductions";
        public const String EmployeeManagerSetup = "EmployeeManagerSetup";
        public const String EmpNewsAndEvents = "EmpNewsAndEvents";
        public const String CompanyHighlights = "CompanyHighlights";
        public const String frmEmploeeSetup = "frmEmploeeSetup";
        public const String GeneralSetup = "GeneralSetup";
        public const String HRGeneralSetup = "HRGeneralSetup";
        public const String TeamSetup = "TeamSetup";
        public const String HRPolicy = "HRPolicy";
        public const String HRAllowancePolicy = "HRAllowancePolicy";
        public const String VendorInfo = "VendorInfo";
        public const String EmployeeProfileBasic = "EmployeeProfileBasic";
        public const String EMPLOYEEPROFIELEXTENDED = "EmployeeProfileBasic"; // EmployeeProfileExtended
        public const String FormWrites = "Not Permission View form";
        public const String UnauthorizedUser = "Unauthorized user";
        public const String Entitlement = "Entitlement";
        public const String SalaryBand = "SalaryBand";
        public const String EntitlementKSA = "EntitlementKSA";
        public const String TicketIssuance = "TicketIssuance";
        public const String EmployeesalarySetup = "EmployeesalarySetup";
        public const String MappingCategoryType = "MappingCategoryType";
        public const String SalaryUpload = "SalaryUpload";
        public const string StallerStopperSetup = "StallerStopperSetup";
        public const string SurveyQuestionStallerStopper = "SurveyQuestionStallerStopper";

        public const String EmployeesalarySetupOld = "EmployeesalarySetupOld";
        public const String ExchangeRateHistory = "ExchangeRateHistory";
        public const String PayRollGroupSetup = "PayRollGroupSetup";
        public const String EmpResponsibilitiesSetup = "EmpResponsibilitiesSetup";
        public const String EmpWiseBonusSetup = "EmpWiseBonusSetup";
        public const String OperatorCategorySetup = "OperatorCategorySetup";
        public const String EmployeeIdentificationCriteria = "EmployeeIdentificationCriteria";
        public const String SecurityForm = "SecurityForm";
        public const string FieldLevelSecurity = "FieldLevelSecurity";
        public const String Nitaqat = "NitaqatSetup";
        public const String SurveyGeneralSetup = "SurveyGeneralSetup";
        public const String MetricsGeneralSetup = "MetricsGeneralSetup";
        //Added by Mohammad Sadiq
        public const String EmployeeCalculationCriteria = "EmployeeCalculationCriteria";
        public const String EmployeeCostEstimationCriteriaSC = "EmployeeCostEstimationCriteriaSC";
        // saad junaid
        public const String TicketIssuanceDetails = "TicketIssuanceDetails";
        // saad junaid
        //Transaction From Constants
        //Added by Mohammad Sadiq
        public const String EmployeeBasicInfo = "EmployeeBasicInfo";
        public const String TemplateManager = "TemplateManager";
        public const string DocumentHistory = "DocumentHistory";
        public const String DocumentMapper = "DocumentMapper";
        public const String CompanyDocument = "CompanyDocument";
        public const String EmpDataConfirmation = "EmpDataConfirmation";
        public const String EmpDocumentCreation = "EmpDocumentCreation";
        public const String BulkIssuanceofletters = "BulkIssuanceofletters";
        public const String IssuanceOfLetterForApplicant = "IssuanceOfLetterForApplicant";
        public const String DocumentLocking = "DocumentLocking";
        public const String EmployeeDocument = "EmployeeDocument";
        public const String EmployeeDocumentImages = "EmployeeDocumentImages";
        public const String EmployeeSalaryReview = "EmployeeSalaryReview";
        public const String EmployeeSalaryPromotionDemotion = "EmployeeSalaryPromotionDemotion";
        public const String EmployeeMedicalReimbursement = "EmployeeMedicalReimbursement";
        public const String SalaryReview = "SalaryReview";

        public const String EmployeeMedicalProcess = "EmployeeMedicalProcess";
        public const String MedicalReimbursementRequest = "MedicalReimbursementRequest";
        public const String RptMedicalEntitlementBalanceDetails = "RptMedicalEntitlementBalanceDetails";
        public const String EmployeeTeams = "EmployeeTeams";
        public const String EmpTransfer = "EmpTransfer";
        public const string IntraCompanyTransferCreation = "IntraCompanyTransferCreation";
        public const string IntraCompanyRequestStatus = "IntraCompanyRequestStatus";
        public const string IntraCompanyTransferEmpInfo = "IntraCompanyTransferCreation";
        public const String ScanDocument = "ScanDocument";
        public const String EmployeeQueries = "EmployeeQueries";
        public const String AuditTrail = "AuditTrail";
        public const String NewEmployeeApprovalForPayroll = "NewEmployeeApprovalForPayroll";
        public const String frmEmployeeBasicInformation = "frmEmployeeBasicInformation";
        public const String ConfirmEmployees = "ConfirmEmployees";
        public const String DataInfo = "DataInfo";
        public const String RptEmpResponsibility = "RptEmpResponsibility";
        public const String EmployeeSecondment = "EmployeeSecondment";
        public const String RptEmployeeSecondment = "RptEmployeeSecondment";
        public const String MedicalTypeSetup = "MedicalTypeSetup";
        public const String HODSetup = "HODSetup";
        public const String HiringChecklistSetup = "HiringChecklistSetup";
        public const String HiringChecklistGradeMap = "HiringChecklistGradeMap";
        public const String HiringChecklist = "HiringChecklist";
        public const String DuplicationcriteriaEmployeeProfileBasic = "DuplicationcriteriaEmployeeProfileBasic";
        public const String PersonalProfileApprovalHR = "PersonalProfileApprovalHR";
        public const String ScanDocumentApplicant = "ScanDocumentApplicant";
        public const String rptSalaryBandPolicy = "rptSalaryBandPolicy";
        public const String TimeSheetGeneralSetup = "TimeSheetGeneralSetup";
        public const String ExitInterviewAnalysis = "ExitInterviewAnalysis";

        public const String rptMPR = "rptMPR";

        public const String EFTEMoevenpick = "EFTEMoevenpick";

        // Assessment

        public const string CapBestFit = "PercentileForBestFitDesignations";
        public const string BehBestFit = "BestFitCriterria";
        public const string RespondentPolicyLeader = "RespondentPolicy_Leader";
        #endregion

        #region Module Name use for Images, Cv, Videos
        public const string M_Recruitment = "recruitment";
        public const string F_ApplicantProfileBasic = "applicantprofilebasic";


        #endregion

        #region Asset Management
        //setups
        public const String Asset = "Asset";
        public const String AssetManagerSetup = "AssetManagerSetup";
        public const String MaintenanceSlab = "MaintenanceSlab";
        public const String VehicleActivityLog = "VehicleActivityLog";
        public const string AssetCategoryAndBrandMapping = "AssetCategoryAndBrandMapping";
        public const string AssetMakeModel = "CompanyModelBrandMapping";
        // Transaction Form
        public const string TransferAssets = "TransferAssets";
        public const string AssetDisposal = "AssetDisposal";
        public const string AssetAuditChecklistSetup = "AssetAuditChecklistSetup";
        public const String AssetActivityLog = "frmAssetActivityLog";
        public const String AssetMaintenanceBalance = "AssetMaintenanceBalance";
        public const String AssetMaintenanceBalanceMonthEndProcess = "AssetMaintenanceBalanceMonthEndProcess";
        //Reports
        public const String RptAllocatedAsset = "RptAllocatedAsset";
        public const String RptAssetEntitledButnot = "RptAssetEntitledButnot";
        public const String RptAssetExpiry = "RptAssetExpiry";
        public const String RptAssetListCatWise = "RptAssetListCatWise";
        public const String RptAssetListEmpWise = "RptAssetListEmpWise";
        public const String RptUnallocatedAsset = "RptUnallocatedAsset";
        public const String RptVehicleActivityLog = "RptVehicleActivityLog";
        public const String rptMaintenanceSlab = "rptMaintenanceSlab";
        public const String AssetAllocationDeallocation = "AssetAllocationDeallocation";
        public const String RptAssetListPayrollGroupWise = "RptAssetListPayrollGroupWise";
        public const String AssetAuditChecklist = "AssetAuditChecklist";
        public const string rptVehicleDetail = "rptVehicleDetail";
        public const string rptAssetMaintenanceAmountLog = "rptAssetMaintenanceAmountLog";
        public const string rptAssetAllocateDeallocateHistory = "rptAssetAllocateDeallocateHistory";
        public const string RptAssetDisposal = "RptAssetDisposal";
        public const string rptassetlist = "rptassetlist";

        #endregion

        #region Appraisal
        public const String AppraisalSetup = "AppraisalSetup";
        //public const String AppraisalPeriod = "AppraisalPeriod";
        public const String AppraisalFormat = "AppraisalFormat";
        public const String AppraisalReview = "AppraisalReview";
        public const String AppraisalReviewPolicy = "AppraisalReviewPolicy";
        public const String UserInformation = "UserInformation";
        public const String AppraisalControlPanel = "AppraisalControlPanel";
        public const String CriteriaforAnnualIncrement = "CriteriaforAnnualIncrement";
        public const String IncrementPolicy = "IncrementPolicy";

        public const String BasisforIncrement = "BasisforIncrement";
        public const String AppraisalSheetType = "AppraisalSheetType";
        public const String WeightFactorSetup = "WeightFactorSetup";
        public const String EmployeeIncrement = "EmployeeIncrement";
        public const String PBCatSetup = "PBCatSetup";
        public const String PBSubCatSetup = "PBSubCatSetup";
        public const String PBEvalDesigner = "PBEvalDesigner";
        public const String PBEvalRptDesigner = "PBEvalRptDesigner";
        public const String AppraisalGrade = "AppraisalGrade";
        public const String PerformanceBonusBudget = "PerformanceBonusBudget";
        public const String BonusPromotionIncrement = "BonusPromotionIncrement";
        public const String BonusPerformanceRequirementPoint = "BonusPerformanceRequirementPoint";
        public const String AppraisalCalculationPeriod = "AppraisalCalculationPeriod";

        public const String PAPIDesigner = "PAPIDesigner";
        public const String PAPODesigner = "PAPODesigner";
        public const String PACRatingSetup = "PACRatingSetup";
        //public const String PACCatSetup = "PACCatSetup";    public const String PACSubCatSetup = "PACSubCatSetup";
        public const String PACDesigner = "PACDesigner";
        public const String PAGRatingSetup = "PAGRatingSetup";
        public const String PAGCatSetup = "PAGCatSetup";
        public const String PAGSubCatSetup = "PAGSubCatSetup";
        public const String PAGDesigner = "PAGDesigner";
        public const String PAGDesignerFieldMapping = "PAGDesignerFieldMapping";
        public const String CorporateObjective = "CorporateObjective";
        //public const String KPICoroprateObjective = "KPICoroprateObjective";

        public const String PASetupProcess = "PASetupProcess";
        public const String AppraisalFormatDesignationWise = "AppraisalFormatDesignationWise";
        public const string PerformanceAppraisalStatus = "PerformanceAppraisalStatus";
        public const string ProbationPerformanceStatus = "ProbationPerformanceStatus";
        public const string PerformanceAppraisalReviewStatus = "PerformanceAppraisalReviewStatus";
        public const string ProbationEvaluationFormatStatus = "ProbationEvaluationFormatStatus";
        public const string ProbationEvaluation = "ProbationEvaluation";

        public const String MonthlyPSM = "MonthlyPSM";
        public const String rptMonthlyPSM = "rptMonthlyPSM";
        public const String RptComparisonSummary = "RptComparisonSummary";
        public const String AppraisalDetailEmpWiseReport = "AppraisalDetailEmpWiseReport";
        public const String AppraisalFormatHistoryReport = "AppraisalFormatHistoryReport";
        public const String RptTScorer = "RptTopScorer";
        public const String RptTScorerAll = "RptTopScorerAll";
        public const String RptPD = "RptPerformanceDifference";
        public const String RptPDAll = "RptPerformanceDifferenceAll";
        public const String RptConstAchiever = "RptConsistentAchiever";
        public const String PMAppraisalOperation = "PMAppraisalOperation";
        public const String PMProbationOperation = "PMProbationOperation";
        public const String PMReviewOperation = "PMReviewOperation";
        #endregion

        #region HR Planning
        //Setups
        public const String RatingScale = "CompetencyRatingSetup";

        #endregion



        #region EmployeeFacilitationCenter
        public const string EFC_Security = "EFC_Security";
        public const string rptEmployeeSatisfactionAnalysis = "rptEmployeeSatisfactionAnalysis";
        #endregion

        public const string EmployeeInformationConfigration = "EmployeeInformationConfigration";

        #region Perfomance

        //Old
        public const String PerformanceAppraisalProcess = "PerformanceAppraisalProcess";
        //Old
        public const string CorporateGoalsObjectives = "CorporateGoalsObjectives";
        public const string BulkSalaryIncrement = "BulkSalaryIncrement";
        public const string PMSetup = "PMSetup";
        public const string PMPeriod = "PMPeriod";
        public const string PMRatingScale = "PMRatingScale";
        public const string PMGeneralPolicies = "PMGeneralPolicies";
        public const string PMCompanyObjectives = "PMCompanyObjectives";
        public const string PMDepartmentObjectives = "PMDepartmentObjectives";
        public const string PMSubDepartmentObjectives = "PMSubDepartmentObjectives";
        public const string PMAppraisalEvaluationFinalizationSheet = "PMAppraisalEvaluationFinalizationSheet";
        public const string PMProbationEvaluationFinalizationSheet = "PMProbationEvaluationFinalizationSheet";
        public const string PMAppraisalEvaluationIncrementPolicy = "PMAppraisalEvaluationIncrementPolicy";
        public const string PMProbationEvaluationIncrementPolicy = "PMProbationEvaluationIncrementPolicy";
        public const string PMDesignationWiseFormat = "PMDesignationWiseFormat";
        public const string PMFormatDesigner = "PMFormatDesigner";
        public const string PMProbationFormatDesigner = "PMProbationFormatDesigner";
        public const string AppraisalEvalWorkflowStatus = "AppraisalEvalWorkflowStatus";
        public const string ProbationEvalWorkflowStatus = "ProbationEvalWorkflowStatus";
        public const string PerformanceEvalWorkflowStatus = "PerformanceEvalWorkflowStatus";
        public const string rptPMAppraisalEvaluationEmployeewise = "PMAppraisalEvaluationEmployeewise";
        public const string rptPMProbationEvaluationEmployeewise = "PMProbationEvaluationEmployeewise";
        public const string rptPMBalanceScoreCardEmployeewise = "PMBalanceScoreCardEmployeewise";
        public const string rptPMAppraisalResultsSummary = "PMAppraisalResultsSummary";
        public const string rptPMReviewResultsSummary = "PMReviewResultsSummary";
        public const string rptPMAppraisalResultsAnalysis = "PMAppraisalResultsAnalysis";
        public const string AppraisalEvaluationSelfSubOrdinateStatus = "AppraisalEvaluationSelfSubOrdinateStatus";
        public const string WFRequestAlreadyForwarded = "You cannot edit/delete this Request because it is already forwarded.";
        public const String msgRefrentialIntegrity = "You cannot delete this record because it is used in transaction.";
        public const string Intimation_Forward_Approval_Appraisal = "Edit/Delete/Save operations cannot be performed because the Request for 'Appraisal Evaluation' has already been forwarded/Approved";
        public const string Intimation_Forward_Approval_Review = "Edit/Delete/Save operations cannot be performed because the Request for 'Performance Review' has already been forwarded/Approved";
        public const string Intimation_Forward_Approval = "Edit/Delete/Save operations cannot be performed because the Request for 'Probation Evaluation' has already been forwarded/Approved";
        public const string NineBoxPerformance = "NineBoxPerformance";
        public const string NineBoxPerformanceReport = "Rpt9BoxPerformance_new";
        public const string ObjectiveRatingScale = "ObjectiveRatingScale";
        public const string MPRsWithOutVacancies = "MPRsWithoutVaccancies";
        public const String WhatHowAnalysis = "WhatHowAnalysis_HR";
        public const String WhatHowAnalysisConfig = "WhatHowAnalysisConfig";
        public const string WhatHowAnalysisEmpDetail = "WhatHowAnalysisEmployeeDetail_HR";
        public const string rptGraphicalStrategyMap = "rptGraphicalStrategyMap";
        public const String LeadershipPotential = "LeadershipPotential";
        public const string PMProbationDesignationWiseFormat = "PMProbationDesignationWiseFormat";

        public const string ObjectivesPerspectiveandSubPerspective = "ObjectivesPerspectiveandSubPerspective";

        #endregion

        #region Appraisal 360

        public const String AppraiserPolicy = "AppraisalQuestion360";
        public const String BehavioralRespondentPolicy = "BehavioralRespondentPolicy";
        public const String EmotionalRespondentPolicy = "EmotionalRespondentPolicy";
        public const String ObjectiveDetail360 = "ObjectiveDetail360";
        public const String RespondentPolicy360 = "RespondentPolicySurvey360";
        public const String AppraisalQuestion360 = "AppraisalQuestion360";
        public const String QuestionCompetencyAssociation = "QuestionCompetencyAssociation";
        public const String AppraisalPublishing = "AppraisalPublishing";
        public const String SurveyPublishing360 = "SurveyPublishing360";
        public const String AppraisalInstruction = "AppraisalInstruction";
        public const String ResultProcessingConditions = "ResultProcessingConditions";
        public const String ResultViewingPolicy = "ResultViewingPolicy";
        public const String StrengthWeaknessDefinition = "StrengthWeaknessDefinition";
        public const String TNIPolicy = "TNIPolicy";
        public const String IntroductionText = "IntroductionText";
        public const String rptCompetencyPortfolioEmpWise = "rptCompetencyPortfolioEmpWise";
        public const String TNIFinalization = "TNIFinalization";
        public const String AssessmentSchedule = "AssessmentSchedule";
        public const String EmotionalAssessmentSchedule = "EmotionalAssessmentSchedule";
        public const String IQAssessment = "IQAssessment";
        public const String TestSchedulePyschometric = "TestSchedulePyschometric";

        public const String AssessmentSchedule_Behavioral_Applicant = "AssessmentSchedule_Applicant";
        public const String EngagementSurveyPublishing = "EngagementSurveyPublishing";
        public const String AssessmentSchedule_EmotionalApplicant = "EQAssessmentSchedule_Applicant";
        public const String AssessmentSchedule_IQ_Applicants = "AssessmentScheduleApplicants";
        public const String DeriveBestFit = "DeriveBestFit";
        #endregion

        #region CreateDivisionDepSubDeptGoal

        public const String CreateDivisionDeptGoal = "CreateDivisionDeptSubDeptGoal";
        #endregion
        #region Attendancehee
        public const String AttendanceProcess = "AttendanceProcess";
        public const String AutoAttendanceProcess = "AutoAttendanceProcess";
        public const String ImportAttendanceProcess = "ImportAttendanceProcess";
        public const String HolidaySetup = "HolidaySetup";
        public const String RosterShift = "RosterShift";
        public const String ChangeShift = "ChangeShift";
        public const String ShiftSetup = "ShiftSetup";
        public const String ChangeShifts = "ChangeShifts";
        public const String OvertimeCategory = "OvertimeCategory";
        public const String RosterPlan = "RosterPlan";
        public const String OverTimeApproval = "OverTimeApproval";
        public const String OverTimeApprovalBulk = "OverTimeApprovalBulk";
        public const String OvertimeStatus = "OvertimeStatus";
        public const String AttendanceRegister = "AttendanceRegisterBulk";
        public const String ExgratiaApproval = "ExgratiaApproval";
        public const String ExgratiaApprovalBulk = "ExgratiaApprovalBulk";
        public const String MonthlyEmpAttndnc = "MonthlyEmpAttndnc";
        public const String EmpAttndnc = "EmpAttndnc";
        public const String AttendanceWaiveOff = "AttendanceWaiveOff";
        //public const String AttndMnthEndProcessForcedMarking = "RptMarkAttendance";
        public const String AttndMnthEndProcessForcedMarking = "AttndMnthEndProcessForcedMarking";
        public const String ExportAttendanceSummaryProcess = "ExportAttendanceSummaryProcess";
        public const String ExportAttendanceSummaryProcess_DW = "ExportAttendanceSummaryProcess_DW";
        public const String UploadAttendance = "UploadAttendance";
        public const String UploadAttendance_New = "UploadAttendance_New";
        public const String AttndRegister = "AttndRegister";
        public const String OffDays = "OffDays";
        public const String ShortAbsenteesInfo = "ShortAbsenteesInfo";
        public const String ShortHours = "ShortHours";

        //public const String WorkFlowSetup = "WorkFlowSetup";
        public const String TaxSlabs = "TaxSlabs";
        public const String IncomeTaxOpening = "IncomeTaxOpening";
        public const String TaxEditing = "TaxEditing";
        public const String AttendanceSummary = "AttendanceSummary";
        public const String AttndFileConfigSetup = "AttndFileConfigSetup";
        public const string CopyRosterPlan = "CopyRosterPlan";
        public const string UploadRosterPlan = "UploadRosterPlan";
        public const string AttAdjustment = "AttAdjFinalApproval";
        public const String AuthorizedDeparture = "AuthorizedDeparture";
        public const String ExtraOTHours = "ExtraOTHours";
        public const String ExtraOTDays = "ExtraOTDays";
        public const String SehriIftariTimingSetup = "SehriIftariTimingSetup";
        public const String OTRequestStatus = "OTRequestStatus";
        public const String ShortPresenceReport = "ShortPresenceReport";
        public const String MultipleTimeInTimeOut = "MultipleTimeInTimeOut";
        public const string NightAllowanceApproval = "NightAllowanceApproval";
        public const string NightAllowanceApprovalBulk = "NightAllowanceApprovalBulk";
        public const String AttendanceAdjustmentRequestStatus = "AttendanceAdjustmentRequestStatus";
        public const String TimeAdjustmentStatus = "TimeAdjustmentStatus";
        public const String OvertimeStatusHR = "OvertimeStatusHR";
        public const String Attendance = "AttendanceInfo";
        public const string OTBudget = "OTBudget";
        public const string OTBudgetStatus = "OTBudgetStatus";
        public const string OTBudgetRevRequest = "OTBudgetRevRequest";
        public const String OTBudgetRevisionStatus = "OTBudgetRevisionStatus";
        public const String OpeningOTBudget = "OpeningOTBudget";
        public const String ChangeandCopyEmployeeProfileShift = "ChangeandCopyEmployeeProfileShift";
        public const String OTEntry = "OTEntry";
        public const String OTBudgetAuthorization = "OTBudgetAuthorization";
        public const String RPRequestStatus = "RPRequestStatus";
        public const String rptShiftSetupNew = "rptShiftSetupNew";
        public const String RPExtraDaysOtHours = "ExtraDaysExtraOTHoursReport";
        public const String PercentileForBestFitDesignations = "PercentileForBestFitDesignations";

        //-------------------Report
        #region Attendance Reports
        public const String HabitAbsntRpt = "HabitAbsntRpt";
        public const String RosterPlanReport = "RosterPlanReport";
        public const String AttendanceReport = "AttndncRpt";
        public const String AttendanceReportNew = "AttndncRpt_New";
        public const String AttendanceSReport = "AttndncStatusRpt";
        public const String AttendanceStatus = "AttendanceStatus";
        public const String AttendanceDailyReport = "DailyAttendance";

        public const String EmpShiftInfoReport = "EmployeeShiftInfo";
        public const String HabitualLateReport = "HabitLateComers";
        public const String ShiftTimingSetup = "rptShiftTimingSetup";
        public const String DailyAttendanceSheet = "rptDailyAttendanceSheet";
        public const String IndividualOverTimeSheet = "rptIndividualOverTimeSheet";
        public const String RptAttAdjustment = "RptAttAdjustment";
        public const String AttnRegisterUnderSection41 = "AttnRegisterUnderSection41";
        public const String IftariSehriAllowance = "IftariSehriAllowance";
        public const String AbsenteesReport = "AbsenteesReport";
        public const String AttndOnHolidaysForPayment = "AttndOnHolidaysForPayment";
        public const String LetterOfCharge = "LetterOfCharge";
        public const String ExGratiaReport = "ExGratiaReport";
        public const String AttnRegister_ByTimeOfWorkedHrs = "AttnRegister_ByTimeOfWorkedHrs";
        public const string RptNightAllowance = "RptNightAllowance";
        public const string RptAttendancesheet = "RptAttendanceSheet";
        public const string AttendanceSheetReport = "AttendanceSheetReport";
        public const string AvgAttSummary = "AvgAttSummary";
        public const string PeriodicalReport = "PeriodicalConsolidateReport";
        public const String rptMonthlyOTStayAllw = "rptMonthlyOTStayAllw";
        public const String rptOvertimeBudgetSheet = "rptOvertimeBudgetSheet";
        public const String RptAttendancePerformance = "RptAttendancePerformance";
        public const string RptAttendancesheet1 = "RptAttendanceSheet1";
        public const String RptAttendanceMachineReport = "RptAttendanceMachineReport";
        // adding constants sabahat siddiqui
        public const String rptRosterPlanNew = "rptRosterPlanNew";
        public const String EmployeesCurrentlyInOffice = "RptEmployeesCurrentlyInOffice";
        public const String RptAttendanceAnalysisStatitical = "RptAttendanceAnalysisStatitical";
        public const string AttendanceAnalysis = "AttendanceAnalysis";


        #endregion
        #endregion

        #region Organization

        //Report Forms Constants
        public const String ManPowerDetail = "ManPowerDetail";
        public const String ReportToInformation = "RptoInformation";
        public const String PayRollGroup = "PayRollGroup";


        //Setups
        public const String GroupSetup = "GroupSetup";
        public const String FiscalYearSetup = "FiscalYearSetup";
        public const String TaxYearSetup = "TaxYearSetup";
        public const bool TwoFiscalYearsCanBeOpen = true;
        public const String ProvisionSetup = "ProvisionSetup";
        public const String CostHeadSetup = "CostHeadSetup";
        //public const String KPISetupforHRPlanning = "KPISetup";
        //public const String KPIGroupSetupforHRPlanning = "KPIGroupSetup";

        //Transation
        public const String EmpReqBudget = "EmpReqBudget";
        public const String EmpJobDescription = "EmpJobDescription";
        public const String JobAnalysis = "JobAnalysis";
        public const String JobDescription = "JobDescription";
        public const String ManPower = "ManPower";
        public const String OrgChart = "OrgChart";
        public const String ControlBudgeting = "ControlBudgeting";
        public const String ManPowerBudgetRevision = "ManPowerBudgetRevision";
        public const String OpeningBudget = "OpeningBudget";
        public const String ControlBudgetingStatus = "ControlBudgetingStatus";
        public const String BudgetRevisionStatus = "BudgetRevisionStatus";
        public const String BudgetExpense = "BudgetExpense";
        public const String BudgetRevenue = "BudgetRevenue";
        public const String PLProjectionInfo = "PLProjectionInfo";
        //public const String TopSheet = "TopSheet";
        //public const String TopSheetHistory = "TopSheetHistory";
        public const string JDMassAllocation = "JDMassAllocation";
        public const string MPBudgetVsActual = "MPBudgetVsActual";

        public const String frmOrganizationalChart = "OrganizationalChart"; //Added by Adnan on 31-Jan-12

        public const String rptListOfEmpGradeWise = "rptListOfEmpGradeWise";
        public const String rptWorkingForBudgetEXGRATIA = "rptWorkingForBudgetEXGRATIA";
        public const String rptWorkingForBudgetEXGRATIASummary = "rptWorkingForBudgetEXGRATIASummary";
        public const String rptWorkingForBudgetManningDeptWise = "rptWorkingForBudgetManningDeptWise";
        public const String rptWorkingForBudgetOvertime = "rptWorkingForBudgetOvertime";
        public const String rptWorkingForBudgetMachineOprHrs = "rptWorkingForBudgetMachineOprHrs";

        public const String rptMOHWorkingforBudget = "rptMOHWorkingforBudget";
        public const String rptBudgetManning = "rptBudgetManning";
        public const String rptBudgetManningHeadDetail = "rptBudgetManningDetailHeadcount";
        public const String rptBudgetManningSummaryManagement = "rptWorkingBudgetSummaryofManning";
        public const String rptProvisionGratuity = "rptProvisionGratuity";
        public const String rptProvisionBonus = "rptProvisionBonus";
        public const String rptProvisionLeaves = "rptProvisionLeaves";
        public const String rptProvisionsehriIftari = "rptProvisionsehriIftari";
        public const String rptProvisionOthers = "rptProvisionOthers";
        public const String EmpReqBudgetReport = "EmpReqBudgetReport";
        public const String rptAttrition = "rptAttrition";
        public const String rptAttritionManPower = "rptAttritionManPower";
        public const String rptAttritionReportingBased = "rptAttritionReportingBased";
        public const String SeparationListEmpExperienceBased = "SeparationListEmpExperienceBased";
        public const String SeparationListEmpPerformanceBased = "SeparationListEmpPerformanceBased";
        public const String rptManPowerBudgetvsActual = "rptManPowerBudgetvsActual";
        public const String rptOverallDeptPerSingleApp = "rptOverallDeptPerSingleApp";
        public const String RptDiffbetweenJDEmpJD = "RptDiffbetweenJDEmpJD";
        public const String RptJD = "RptJD";
        public const String RptEmpJD = "RptEmpJD";
        public const String ManpowerBudgetAuthorization = "ManpowerBudgetAuthorization";
        public const String EmployeeSessiRegistrationLocation = "EmployeeSessiRegistrationLocation";
        //public const String EmployeeSalaryBand = "EmployeeSalaryBand";
        public const String EmployeeSalaryBand = "SalaryBand";
        public const String rptCompetencyInventory = "rptCompetencyInventory";

        #endregion

        #region Leaves
        //Report Form Constants
        public const String LeaveReports = "LeaveReports";
        public const String LeaveAvailed = "RptLeaveAvailed";
        public const String RptLeaveLedger = "RptLeaveLedger";

        public const String RptEmpWiseLeaveBalance = "RptEmpWiseLeaveBalance";
        public const String RptEmpWiseLeaveBalance_Payroll = "RptEmpWiseLeaveBalance_Payroll";
        public const String AnnualLeaveCalendar = "AnnualLeaveCalendar";
        public const String LeaveSetup = "LeaveSetup";
        public const String LeaveBalanceReport = "LeaveBalanceReport";
        public const String RptLeaveBalance = "RptLeaveBalance";
        //Setup Form Constants
        public const String AttendanceLeaveCode = "AttendanceLeaveCode";
        public const String EmpLeavesOpBal = "EmpLeavesOpBal";
        public const String LeavePolicy = "LeavePolicy";
        public const String LeavePolicyDetail = "LeavePolicyDetail";
        public const String SandwichPolicy = "SandwichPolicy";
        public const String LeavePriority = "LeavePriority";
        public const String LeavePolicyLayOff = "LeavePolicyLayOff";
        public const string MonthlyHalfDayQuota = "MonthlyHalfDayQuota";
        public const string Emailecipients = "Emailecipients";
        public const String CompanyLeaveSetup = "CompanyLeaveSetup";
        public const String GradeWiseCompensatoryLeavesSetup = "GradeWiseCompensatoryLeavesSetup";
        public const String LeaveEntitlementExceptionpolicy = "LeaveEntitlementExceptionpolicy";
        public const String PopServerPolicy = "PopServerPolicy";
        //Transaction Form Constants

        public const String EmpLeavesEntry = "EmpLeavesEntry";
        public const String LeavesEndProcess = "LeavesEndProcess";
        public const String LeaveEncashmentProcess = "LeaveEncashmentProcess";
        public const String EmployeeLeaveEncashment = "EmployeeLeaveEncashment";
        public const String LeaveStatus = "LeaveStatus";
        public const String RptLeaveRoster = "RptLeaveRoster";
        public const String LeavesProcessSchedule = "LeavesProcessSchedule";
        public const String LFARequestProcess = "LFARequestProcess";

        public const String RptIndividualLeaveChart = "RptIndividualLeaveChart";
        public const String RptLeaveStatusReport = "RptLeaveStatusReport";
        public const String RptEmpAnnualLeave = "RptEmpAnnualLeave";
        public const String RptEmpLeaveSummary = "RptEmpLeaveSummary";
        public const String RptEmpLeaveEncashment = "RptEmpLeaveEncashment";
        public const String RptCPLReport = "rptCPLReport";
        public const string Leave = "Leave";
        #endregion

        #region Loan
        public const string LoanStatus = "LoanStatus";
        #endregion

        #region Separation Manager

        //Reports Forms Constants
        public const String ClearenceCertificate = "ClearenceCertificate";
        public const String EmpExitInfo = "EmpExitInfo";
        public const String ExitInterview = "ExitInterview2";
        public const String MemoForAcctDept = "MemoForAcctDept";
        public const String Memorandum = "Memorandum";
        public const String ResigAccptLetter = "ResigAccptLetter";
        public const String TurnOver = "TurnOver";
        public const String TurnOverAnalysis = "TurnOverAnalysis";
        public const String TurnOverAnalysisDeptWise = "TurnOverAnalysisDeptWise";
        public const String TurnOverDesigWise = "TurnOverDesigWise";
        public const String TurnOverSepReasWise = "TurnOverSepReasWise";
        public const String RptStatusWiseResign = "RptStatusWiseResign";
        public const String HandingOverSummaryReport = "HandingOverSummaryReport";

        public const String ExitInterviewDesigner = "ExitInterviewDesigner";


        // UnitsManagement Form Constant

        public const String UnitManagement = "UnitsManagement";

        //Setup Forms Constants
        public const String EmployeePGId = "EmployeePGId";
        public const String ExitQuestion = "ExitQuestion";
        public const String SeparationSetup = "SeparationSetup";
        public const String HandingOverChecklistSetup = "HandingOverChecklistSetup";
        public const String MappingofSeparationTypeReasons = "MappingofSeparationTypeReasons";
        //Transaction Forms Constants
        public const String SeparationManager = "SeparationManager";
        public const String EmpSpecChecklstSetup = "EmpSepecificChecklistSetup";
        public const String SeparationStatus = "SeparationStatus";
        public const String Exit_Interview = "Exit_Interview";
        public const String IntraDepartmentPolicy = "IntraDepartmentPolicy";
        public const String InterDepartmentPolicy = "InterDepartmentPolicy";


        //Designer
        public const string EICatSetup = "EISCatSetup";
        public const string EISubCatSetup = "EISSubCatSetup";
        public const string EIOptionSetup = "EISOptionTypeSetup";
        public const string EISDesigner = "EISDesigner";
        public const string EISRptDesigner = "EISRptDesigner";

        public const string StatusTypeDetail = "StatusTypeDetail";
        public const string ClearanceSetup = "ClearanceSetup";
        public const string ConcernedDepartmentSetup = "ConcernedDepartmentSetup";
        public const string ClearanceDepartmentSetup = "ClearanceDepartmentSetup";
        public const string ClearanceWorkflowSetup = "ClearanceWorkflowSetup";
        public const string AssociateClearanceForm = "AssociateClearanceForm";
        //Reports
        public const string AssociateClearanceDetail = "AssociateClearanceDetail";
        public const string rptAssociateClearance = "rptAssociateClearance";
        public const string rptEmployeesPendingClearance = "rptEmployeesPendingClearance";
        public const string rptFullFinalProcessedWithoutClearance = "rptFullFinalProcessedWithoutClearance";
        public const string RptSepRootAnalysis = "RptSepRootAnalysis";

        public const String ResignedandSeperatedEmployeevariousProcess = "ResignedandSeparatedEmployeesbeingInvolvedinVariousProcesses";
        //public const string FullFinalProcessedWithoutClearance = "FullFinalProcessedWithoutClearance";
        //public const string EmployeesPendingClearance = "EmployeesPendingClearance";
        public const String grfTurnoverAnalysis = "grfTurnoverAnalysis";

        //Reports
        #endregion

        #region Training

        //Setups
        public const String TrainingSetup = "TrainingSetup";
        public const String TrainingInstitutes = "TrainingInstitutes";
        public const String TrainingBudget = "TrainingBudget";
        public const String TrainingBondPolicy = "TrainingBondPolicy";
        public const String TrainerSetup = "TrainerSetup";
        public const String TrainingSubjectWiseCategory = "TrainingSubjectWiseCategory";
        public const String TrainingPolicyOrientation_RecurringSubjects = "TrainingPolicyOrientation_RecurringSubjects";
        public const String TrainingNeedsIdentificationCriteria = "TrainingNeedsIdentificationCriteria";
        public const String TrainingSubjectWiseCompetency = "TrainingSubjectWiseCompetency";
        public const String TrainingDevelopmentPlan = "TrainingDevelopmentPlan";
        public const String PersonalDevelopmentPlan = "PersonalDevelopmentPlan";
        public const String DeptDesgAssociationWithTrainingSubject = "DeptDesgAssociationWithTrainingSubject";
        public const String CurrencyMappingWithBaseCompanyCurrency = "CurrencyMappingWithBaseCompanyCurrency";


        public const String TrainerCoachSetup = "TrainerCoachSetup";
        public const String TrainingPlanner = "TrainingPlan";
        public const String TrainingCoaching = "TrainingMentorCoach";
        public const String IdentifiedTrainingNeeds = "IdentifiedTrainingNeeds";
        public const String TrainingDetail = "TrainingDetail";
        public const String TrAttendanceSheet = "TrAttendanceSheet";

        public const String TrAttendanceSheetnew = "TrAttendanceSheetnew";

        public const String TrainingNominationConfirmation = "TrainingNominationConfirmation";
        public const String InsertNewTrainingNeeds = "InsertNewTrainingNeeds";

        public const string TFCatSetup = "TrEvalCatSetup";
        public const string TFSubCatSetup = "TrEvalSubCatSetup";
        public const string TFOptionSetup = "TFOptionTypeSetup";

        public const string TRSubCatSetup = "TrTrainerSection";
        public const string TROptionSetup = "TrTrainerOption";
        public const string TRDesigner = "TrTrainerDesigner";

        public const string TFDesigner = "TrEvalDesigner";
        //public const string PHEvalForm = "PHEvalForm";
        public const string TFRptDesigner = "TrEvalRptDesigner";
        public const string TECatSetup = "TrCatSetup";
        public const string TESubCatSetup = "TrSubCatSetup";
        public const string TEOptionSetup = "TEOptionTypeSetup";

        public const string TEDesigner = "TrFeedbackDesigner";
        public const string TERptDesigner = "TrFeedbackRptDesigner";
        public const string TrainingEvalEffectivenessFeedback = "TrainingEvalEffectivenessFeedback";
        // added by Mohammad  Sadiq
        public const string RptTrainingNeedIdentified = "RptTrainingNeedIdentified";
        public const string RptTrainingInformation = "RptTrainingInformation";
        // added by Arif Anjum
        public const string TrainerFeedbackReport = "TrainerFeedbackReport";
        // added by Mohammad  Sadiq
        public const string RptTrBudget = "RptTrDepartmentBudget";
        public const string TrainingStatus = "TrainingRequestStatus";
        public const string TrainingPlanRequestStatus_HR = "TrainingPlanRequestStatus_HR";
        public const string RptEmpBond = "RptEmployeeBond";
        public const string RptCalendar = "RptTrainingCalendar";
        public const string rptEmployeeNTBPlanned = "rptEmployeeNTBPlanned";
        public const string NonPlannedRecurringCoursesEmployees = "NonPlannedRecurringCoursesEmployees";
        public const string TrainingDetails = "TrainingDetail";
        public const string TrainingEffectivenessAnalysisGraph = "TrainingEffectivenessAnalysisGraph";


        // Added by Zain hashmi
        public const string TrainingEvalutationAnalysisGraph = "TrainingEvaluationAnalysis";
        public const string Point_Setup = "PointsSetup";
        //;
        public const string EmpNotAttendAnyTraining = "EmpNotAttendAnyTraining";
        public const string RptTrainingBudget = "RptTrainingBudget";

        #endregion

        #region Succession Planning

        //Report Form Constants
        public const String SuccessionPlanning = "rptSuccessionPlanning";
        public const String rptBenchMarkComparisonSheet = "rptBenchMarkComparisonSheet";
        public const String rptBenchMarkComparisonSheetDesignationWise = "rptBenchMarkComparisonSheetDesignationWise";
        // Succession Planning

        public const String Age = "Age";
        public const String AppraisalGradingWiseRating = "AppraisalGradingWiseRating ";
        public const String AttendancePercentage = "AttendancePercentage";
        public const String Competencies = "Competencies";
        public const String DegreeInstitute = "DegreeInstitute";
        public const String Designation = "Designation";
        public const String ExperienceWiseRating = "ExperienceWiseRating";
        public const String ExpIntPresentDept = "ExpIntPresentDept";
        public const String QlfWiseRating = "QlfWiseRating";
        public const String SuccessionSetup = "SuccessionSetup";
        public const String SuccessionBenchmark = "SuccessionBenchmark";
        public const String TrainingCategoriesWiseRating = "TrainingCategoriesWiseRating";
        public const String SuccStatusWorkSheet = "SuccStatusWorkSheet";
        public const String SuccStatusWorkSheetOrg = "SuccStatusWorkSheetOrg";

        #endregion

        #region Payroll
        public const String NewPayrollMonth = "NewPayrollMonth";
        public const String FacilityRateSetup = "FacilityRateSetup";
        public const String SalaryHold = "SalaryHold";
        public const String ReleasePayslip = "ReleasePayslip";
        #endregion

        public const string OperatorCategoeySetup = "OperatorCategoeySetup";
        public const String BoxPerformance_new = "9BoxPerformance_new";
        #region Survey
        public const string SurveySetup = "SurveySetup";
        public const string SurveyQuestionsSetup = "SurveyQuestionsSetup";
        public const string SurveyCategorySetup = "SurveyCategorySetup";
        public const string SurveyRatingSetup = "SurveyRatingSetup";
        public const string SurveyQuestions = "SurveyQuestions";
        public const string SurveyFeedbackHR = "SurveyFeedbackHR";
        public const string SurveyReport = "SurveyReport";
        public const string SurveyFeedBack = "SurveyFeedBack";
        public const string AttemptedSurveyInfo = "AttemptedSurveyInfo";
        public const string SurveyWiseFBPercentage = "SurveyWiseFBPercentage";
        public const string SurveyAndQuesWiseFBPercentage = "SurveyAndQuesWiseFBPercentage";
        public const string SurveyAveragePeriodWise = "SurveyAveragePeriodWise";
        public const string EmployeeWiseSurveyPercentage = "EmployeeWiseSurveyPercentage";
        #endregion

        #region Tools
        public const String DBoard = "DBoard";
        public const String EmpLoanInfo = "EmpLoanInfo";
        public const string HRPersonalizedemailalerts = "HRPersonalizedEmailAlerts";
        public const string ImportProfileBasicGeneralSetup = "ImportBasicProfileSetupGeneral";
        public const string TimesheetAnalysis = "TimeSheetAnalysis";
        public const string TrainingVideos = "TrainingVideos";
        #endregion

        public const String StandardCustomizelabel = "CustomizeLabel";
        public const String CustomizeFormDesigner = "CustomizeFormDesigner";

        public const String ErrorPagePath = "~/ErrPage.aspx";
        public const string rptAttendanceSummary = "rptAttendanceSummary";


        #region Messages
        public const String qsSession_SessionExpiry = "Your session has been expired please re-login";
        public const String qsAccess_Denied = "You don't have enough permissions to view this form";
        public const String qsLogout_true = "You have been logged out of the application";
        public const String qsAccess_UnKnownString = "Unknown login attempt";
        public const String qsAccess_NoString = "Use the ERP Login screen to use the application";
        public const String qsAccess_PermissionsNotSet = "Failed to write permissions";
        public const String qsAccess_PGIdNotSet = "Payroll Groups not assigned to the user";
        public const String qsUserMapping_UnSuccessful = "User mapping not correct, assign proper Employee Code and PGIDs";

        public const String msgCannotInsert = "You do not have permission to create record(s) on this form.";
        public const String msgCannotEdit = "You do not have permission to edit record(s) on this form.";
        public const String msgCannotDelete = "You do not have permission to delete record(s) on this form.";
        public const String msgCannotProcess = "You do not have permission to execute process on this form.";
        public const String msgCannotView = "You do not have permission to view.";
        public const String msgCannotViewReport = "You do not have permissions to view this Report.";
        public const String msgUseInTransaction = "Cannot continue! This record is used in a transaction(s).";

        public const String msgInsertedSuccessfully = "Record created successfully!";
        public const String msgUpdatedSuccessfully = "Record updated successfully!";
        public const String msgDeletedSuccessfully = "Record deleted successfully!";

        public const string msgMultiTerminalInsertion = "Cannot continue! Another user has simultaneously updated this record from another terminal. Please refresh the page to continue.";

        public const String msgProcessSuccessfully = "Process run successfully!";

        public const String msgSaved = msgInsertedSuccessfully;
        public const String msgUpdated = msgUpdatedSuccessfully;
        public const String msgDeleted = msgDeletedSuccessfully;

        public const String msgReEmployed = "has been re-employed";

        public const String msgProcessNotSuccessfully = "Process not run successfully";
        public const String msgDeletedRefrence = "This record cannot be Deleted.It is in use...";
        public const String msgCompetencyReferenced = "The selected item cannot be deleted as it is used in further transactions.";
        public const String msgCompetencyAlreadyExist = "Code or Description already exist.";
        public const String msgPeriodNotEditable = "Changes cannot be made in this Period. Either Period is not 'Editable' or 'Editable Date' has been passed. - Please mark it editable and enter editable date in the screen Appraisal Period";
        public const String msgMaxLength500 = " Value exceed the limit of 500 character.";
        public const String msgMaxLength250 = " Value exceed the limit of 250 character.";
        public const String msgDsgStrength = "Strength for this designation is not defined";

        public const String msgExceedStrength1 = "Manpower Budget is not created for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength2 = "Manpower Budget is not created for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength3 = "Manpower Budget is not created for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength4 = "Manpower Budget is not created for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength5 = "Manpower Budget is not created for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength6 = "Manpower Budget is not created for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";




        public const String msgExceedStrength1_1 = "Please first create Manpower Budget for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength2_2 = "Please first create Manpower Budget for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength3_3 = "Please first create Manpower Budget for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength4_4 = "Please first create Manpower Budget for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength5_5 = "Please first create Manpower Budget for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength6_6 = "Please first create Manpower Budget for the selected ‘Designation’, against the selected ‘Division’, ‘Department’, ‘Sub-Department’, ‘Location’ and ‘Team’";
        public const String msgExceedStrength = "Record cannot be saved because the actual head count will go beyond the approved head count defined in ‘Manpower Budget’.";

        //public const String msgExceedStrength1 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgManPowerBugetNotCreated1").ToString());
        //public const String msgExceedStrength2 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgManPowerBugetNotCreated11").ToString());
        //public const String msgExceedStrength3 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgManPowerBugetNotCreated12").ToString());
        //public const String msgExceedStrength4 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgManPowerBugetNotCreated13").ToString());
        //public const String msgExceedStrength5 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgManPowerBugetNotCreated14")).ToString();
        //public const String msgExceedStrength6 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgManPowerBugetNotCreated15").ToString());

        //public const  String msgExceedStrength1_1 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgFirstCreateManPowerBuget")).ToString();
        //public const String msgExceedStrength2_2 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgFirstCreateManPowerBuget1")).ToString();
        //public const String msgExceedStrength3_3 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgFirstCreateManPowerBuget2")).ToString();
        //public const String msgExceedStrength4_4 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgFirstCreateManPowerBuget3")).ToString();
        //public const String msgExceedStrength5_5 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgFirstCreateManPowerBuget4")).ToString();
        //public const String msgExceedStrength6_6 = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgFirstCreateManPowerBuget5")).ToString();



        // public const String msgExceedStrength = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "msgRecordCannotbeSaved")).ToString();


        public const string msgExceedStrenghtConfirmation = "If the record is saved, actual head count will go beyond the approved head count defined in ‘Manpower Budget’.";

        public const String msgExceedSalaryStrength = "Cannot exceed salary more than approved in the budget";
        public const String msgExceedDsgStrength = "Cannot exceed Strength more than approved in the budget";
        public const String msgHeSheInSeparation = "Employee cannot avail Leave(s), as he/she has resigned.";
        public const String msgEmployeeInSeparation = "You cannot avail Leave(s), as you have resigned and according to policy resigned employees cannot avail leave.";
        public const String msgmultiTerminalDeleted = "Record has been deleted from other terminal.";


        public const String msgDsgSalary = "Approved salary is not defined in budget.";
        public const String msgExceedSalary = "Cannot exceed gross salary more than approved in the budget";

        public const String msgExceedInc = "Cannot exceed increment more than approved in the budget";
        public const String msgDsgInc = "Approved increment is not defined in budget.";

        public const String fiscalYearNotDef = "Please define fiscal year in which WEF lie.";

        public const String InvalidDescription = "ampersand ( & ) and apostrophe ( ' ) are not allowed in description. Kindly remove these characters from description.";
        public const String msgSearchRecordNotFound = "Record not found.";
        //Employee cannot avail Leave(s), as he/she is resigned.
        //You cannot avail Leave(s), as you have resigned and according to policy resigned employees cannot avail leave.
        //public const String msgEmployeeInSeparation = "This Employee can not avail Leave, as his/her entry exists in Separation Manager";
        #endregion

        #region Color Codes
        public const String clrCancelledLeaveEntryRowColor = "#E0E0E0";
        public const String clrExportedLeaveEntryRowColor = "#FAFCA7";
        #endregion

        #region WorkFlow Type Names

        public const string WF_TYPENAME_OTBUDGET = "Overtime Budget";
        public const string WF_TYPENAME_OTBUDGETREVISION = "Overtime Budget Revision";

        #endregion

        #region ExpenseManager

        public const String VariableAllowanceSetup = "VariableAllowanceSetup";
        public const String DisbursementPeriod = "DisbursementPeriod";
        public const String VariablefixedEntitlementGradewise = "VariablefixedEntitlementGradewise";
        public const String WorkflowConfiguration = "WorkflowConfiguration";
        public const String ImmediateAdvancePaymentPolicy = "ImmediateAdvancePaymentPolicy";
        public const String VariableFixedAllowanceEmployeeWise = "VariableFixedAllowanceEmployeeWise";
        public const String VariableFixedAllowanceStatus = "VariableFixedAllowanceStatus";
        public const String VariableFixedAllowanceProcess = "VariableFixedAllowanceProcess";
        public const String VariableFixedAllowanceProcessClosing = "VariableFixedAllowanceProcessClosing";
        public const String RptEmpVariableFixedAllowanceDetail = "RptEmpVariableFixedAllowanceDetail";
        public const String RptEmpVariableFixedAllowanceSummary = "RptEmpVariableFixedAllowanceSummary";
        public const String VariableFixedAllowanceTransaction = "VariableFixedAllowanceTransaction";
        public const String ExpenseProcessforPayment = "ExpenseProcessforPayment";
        public const String ExpenseEntry = "VariableFixedAllowanceTransaction";
        public const String RptTeamWiseAllowanceSummary = "RptTeamWiseAllowanceSummary";
        public const String RptBankAdvice = "RptBankAdvice";
        public const String ExpensePayment = "ExpensePayment";
        public const String OneTimeExpenseImport = "OneTimeExpenseImport";
        public const String PaymentSettlement = "PaymentSettlement";
        public const String rptExpenseBalance = "rptExpenseBalance";
        public const String rptExpenseAnalysis = "rptExpenseAnalysis";
        public const String rptExpensePaymentAdvice = "rptExpensePaymentAdvice";
        //adding costants sabahat siddiqui
        public const String CriteriaforMileageReimbursementPolicy = "CriteriaforMileageReimbursementPolicy";
        public const String MileageReimbursementPolicyStandard = "MileageReimbursementPolicyStandard";
        public const String MileageReimbursementPolicyEmployeeSpecific = "MileageReimbursementPolicyEmployeeSpecific";
        public const String MileageCalculationCriteria = "MileageCalculationCriteria";
        #endregion

        #region Task Management

        //SETUPS
        public const String TaskManagerSetup = "TaskManagerSetup";
        public const String IntimationPolicy_Company = "IntimationPolicy_Company";
        public const String IntimationPolicy_Employee = "IntimationPolicy_Employee";

        #endregion

        #region Psychometric Testing - MBTI

        //SETUPS
        public const String MBTI_Setup = "MBTI_Setup";

        public const String TestSections_MBTI = "TestSections";
        public const String Outcome_MBTI = "Outcome";
        public const String BestfitDesignations_MBTI = "BestfitDesignations";
        public const String JobProfileMatch = "JobProfileMatch";

        public const String PersonalityTypeMeaning_MBTI = "PersonalityTypeMeaning";
        public const String Questionnaire_MBTI = "Questionnaire";
        public const String TestSchedule_Applicants_MBTI = "TestSchedule_Applicants";
        public const String TestSchedule_Employee_MBTI = "TestSchedule_Employee";
        //REPORTS
        public const String MBTI_Result_Applicants = "MBTI_Result_Applicants";
        public const String MBTI_Result_Employees = "MBTI_Result_Employees";
        public const String MBTI_Report_BestFitDesignation = "MBTI_Report_BestFitDesignation";

        #endregion

        #region Master Setups ...

        public const String CONST_DIVISION_SMSID = "70";
        public const String CONST_MAINDEPARTMENT_SMSID = "84";
        public const String CONST_DEPARTMENT_SMSID = "24";
        public const String CONST_DESIGNATION_SMSID = "3";
        public const String CONST_REGION_SMSID = "79";
        public const String CONST_LOCATION_SMSID = "63";
        public const String CONST_CATEGORY_SMSID = "210";
        public const String CONST_EMPLOYEETYPE_SMSID = "25";
        public const String CONST_GRADEID = "19";

        #endregion
        #region CoreConfig
        public const string CoreConfiguration = "CoreConfiguration";
        public const string TargetAndBenchmarkSetting = "TargetAndBenchmarkSetting";
        public const string ReportingResults = "ReportingResults";
        public const string ReleaseMetrics = "ReleaseMetrics";
        public const string TargetandBenchmarkSetting = "TargetandBenchmarkSetting";
        public const string MetricsAnalytics = "MetricsAnalytics";
        public const string MandatoryFieldsPolicy = "MandatoryFieldsPolicy";

        public const string GeneralPolicy = "GeneralPolicy";
        public const string CostPerHire = "CostPerHire";
        public const string PeriodWiseExternalEntitiesValue = "PeriodWiseExternalEntitiesValue";


        #endregion
        #region Collaboration
        public const string ThreadsManagement = "ThreadsManagement";
        public const string ThreadsPolicy = "ThreadsPolicy";
        public const string ThreadPostApproval = "ThreadPostApproval";
        public const string Posts = "Posts";
        public const string PostDetails = "PostsDetails";
        #endregion

        #region Recruitment
        public const string TestQuestions = "TestQuestions";
        public const string MandatoryFeildsforMPR = "MandatoryFeildsforMPR";

        public const string rptApplicantList = "rptApplicantList";

        #endregion

        #region Timesheet

        public const string Customer = "Customer";

        public const string Timesheet_PerHourCostSetup = "Timesheet_PerHourCostSetup";
        public const string rptTimesheetGraph = "rptTimesheetGraph";

        #endregion

        #region Learning

        public const string CourseManagement = "CourseManagementPrototype";


        #endregion

        #region Security

        //public const string LocalRedisCredential = "Softronic.redis.cache.windows.net:6380,password=aSOWHs3p6vqpMV50rpE6R9ZXmMBAGEfKAI/j4HXApyY=,ssl=True,abortConnect=False";//Dbsrv redis connection string --> IConnectionMultiplexer redisConn = ConnectionMultiplexer.Connect("10.20.0.5,ssl=False,abortConnect=False");
        //public const string GlobalRedisCredential = "Softronic.redis.cache.windows.net:6380,password=aSOWHs3p6vqpMV50rpE6R9ZXmMBAGEfKAI/j4HXApyY=,ssl=True,abortConnect=False";//"Softronic.redis.cache.windows.net:6380,password=aSOWHs3p6vqpMV50rpE6R9ZXmMBAGEfKAI/j4HXApyY=,ssl=True,abortConnect=False";
        //public const string GlobalRedisHost = "Softronic.redis.cache.windows.net";
        //public const int GlobalRedisPort = 6380;

        //public const string LocalRedisCredential = "SoftronicRedis.redis.cache.windows.net:6380,password=wQgkjM1rNhMGK/wkE6B9Usky5V2WZE74DGk9ie4hUkA=,ssl=True,abortConnect=False,ConnectTimeout=30000";//"10.20.0.5,ssl=False,abortConnect=False";
        //public const string GlobalRedisCredential = "SoftronicRedis.redis.cache.windows.net:6380,password=wQgkjM1rNhMGK/wkE6B9Usky5V2WZE74DGk9ie4hUkA=,ssl=True,abortConnect=False,ConnectTimeout=30000";// "10.20.0.5, ssl=False,abortConnect=False";
        //public const string GlobalRedisHost = "SoftronicRedis.redis.cache.windows.net";
        //public const int GlobalRedisPort = 6379;

        //public static string LocalRedisCredential = ConfigurationManager.ConnectionStrings["RedisConnectioString"].ToString();
        //public static string GlobalRedisCredential = ConfigurationManager.ConnectionStrings["RedisConnectioString"].ToString();
        //public static string GlobalRedisHost = System.Configuration.ConfigurationManager.AppSettings["APIPathV5"].ToString();//"SoftronicRedis.redis.cache.windows.net";
        //public static int GlobalRedisPort = 6379;    


        //public const string LocalRedisCredential = "10.20.0.5,ssl=False,abortConnect=False,ConnectTimeout=30000";
        //public const string GlobalRedisCredential = "10.20.0.5, ssl=False,abortConnect=False,ConnectTimeout=30000";
        //public const string GlobalRedisHost = "10.20.0.5";
        //public const int GlobalRedisPort = 6379;
        #endregion Security

        #region 360 Survey 


        public const String OverallSetup = "Setup360Survey";

        #endregion 360 Survey


        public const String About = "About";

        #region PFA 
        //Setups
        public const string RegionManagementSetup = "regionmanagementregionsandsmallocation";
        public const string DistrictManagementSetup = "districtmanagementterritorydsmallocation";

        public const string DistrictManagementSetupReplica = "districtmanagementterritorydsmallocationreplica";

        public const string IncentivePolicy = "IncentivePolicy";

        public const string IncentivePolicyManagementSetups = "IncentivePolicyManagementSetups";
        public const String ZakatExemptionCertificateSetups = "ZakatExemptionCertificateSetups";
        public const String AccountHeadsMappingSetups = "AccountHeadsMappingSetups";

        //Transactions
        public const string AccuralProcessTransaction = "AccuralProcessTransaction";
        public const string InvestmentDealsTransaction = "InvestmentDealsTransaction";
        public const string InvestmentRedemptionTransaction = "InvestmentRedemptionTransaction";
        public const string PaymentReceiptTransaction = "PaymentReceiptTransaction";
        public const string InterestIncomeThroughBanksTransaction = "InterestIncomeThroughBanksTransaction";
        public const string LoanTransaction = "LoanTransaction";
        public const string Profitcontributionprocess = "Profitcontributionprocess";

        //Reports
        public const string rptInvestmentDeals = "rptInvestmentDeals";
        public const string RptProvidentFundDetails = "RptProvidentFundDetails";
        public const string RptMonthlyAccural = "RptMonthlyAccural";
        public const string RptFinalSettlement = "RptFinalSettlement";
        public const string RptTotalIncomeReceived = "RptTotalIncomeReceived";
        public const string RptEmployeeLedger = "RptEmployeeLedger";

        public const string FinalSettlement = "FinalSettlement";
        public const string PaymentReceiptFnF = "PaymentReceiptFnF";


        #endregion PFA

        private string GetString(string key)
        {
            return _localizer[key];
        }

        public const string DateFormat = "MMM-yyyy";

        #region Email
        public const string Param_LoginCulture = "en-GB";
        public const string Param_LoginApplication = "PharmaCRMv2";
        public const string Param_Module = "DAS";
        #endregion

        #region FormIDs
        public const string LeaveRequest = "LeaveRequest";
        #endregion
    }
}
