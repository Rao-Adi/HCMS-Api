using HCMS_Api.Services;
using HCMS_Api.Services.EmployeeAuthority;
using HCMS_Api.Services.HodService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text.Json.Serialization;
using static HCMS_Api.Controllers.HodSetupController;

namespace HCMS_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeeAuthorityController : ControllerBase
    {
        private readonly IEmployeeAuthorityService _employeeAuthorityService;
        private readonly Common.Common _common;


        public EmployeeAuthorityController(IEmployeeAuthorityService employeeAuthorityService, Common.Common common)
        {
            _employeeAuthorityService = employeeAuthorityService;
            _common = common;
        }
        [HttpGet("EmpCode")]
        public async Task<IActionResult> GetEmployeeTxtCode(string EmpCode, string _culture, string GetCompanyId, string status)
        {
            DataTable table = await _employeeAuthorityService.GetEmployeeTxtCode(EmpCode, _culture, GetCompanyId, status);
            return Ok(_common.GetJsonDatatable(table));
        }
        [HttpGet("GetResignEmployee")]
        public async Task<IActionResult> GetResignEmployee(string EmpId, string _culture, string GetCompanyId)
        {
            var table = await _employeeAuthorityService.GetResignEmployee(EmpId, _culture, GetCompanyId);
            return Ok(table); ;
        }
        [HttpGet("GetResignEmployee2")]
        public async Task<IActionResult> GetResignEmployee2(string EmpId, string _culture, string GetCompanyId)
        {
            var table = await _employeeAuthorityService.GetResignEmployee2(EmpId, _culture, GetCompanyId);
            return Ok(table); ;
        }
        [HttpGet("GetInterviewDesigner")]
        public async Task<IActionResult> GetInterviewDesigner(string OrderBy, string _Culture, string _CompanyId, string OldEmpId)
        {
            DataTable table = await _employeeAuthorityService.GetInterviewDesigner(OrderBy, _Culture, _CompanyId, OldEmpId);
            return Ok(_common.GetJsonDatatable(table));
        }
        [HttpGet("GetDirectInDirectReport")]
        public async Task<IActionResult> GetDirectInDirectReport(string OldEmpId, string _CompanyId)
        {
            DataTable table = await _employeeAuthorityService.GetDirectInDirectReport(OldEmpId, _CompanyId);
            return Ok(_common.GetJsonDatatable(table));
        }
        [HttpGet("EffectiveDateValidate")]
        public async Task<IActionResult> EffectiveDateValidate(string txtTEFDate, string _CompanyId)
        {
            string data = await _employeeAuthorityService.EffectiveDateValidate(txtTEFDate, _CompanyId);
            return Ok(data);
        }
        [HttpGet("GetHiringChecklist")]
        public async Task<IActionResult> GetHiringChecklist(string OldEmpId, string _CompanyId)
        {

            var table = await _employeeAuthorityService.GetHiringChecklist(OldEmpId, _CompanyId);
            return Ok(table);
        }
        [HttpGet("GetWorkFlows")]
        public async Task<IActionResult> GetWorkFlows(string OldEmpId, string _CompanyId)
        {
            var table = await _employeeAuthorityService.GetWorkFlows(OldEmpId, _CompanyId);
            return Ok(table);
        }
        [HttpGet("GetWorkFlowsTrack")]
        public async Task<IActionResult> GetWorkFlowsTrack(string CompanyId, string LoginEmpId, string ParentRecordId, string WorkflowID, string _Culture)
        {
            var table = await _employeeAuthorityService.GetWorkFlowsTrack(CompanyId, LoginEmpId, ParentRecordId, WorkflowID, _Culture);
            return Ok(table);
        }
        [HttpGet("GetExitClearance")]
        public async Task<IActionResult> GetExitClearance(string OldEmpId, string SelectedCompany)
        {
            var table = await _employeeAuthorityService.GetExitClearance(OldEmpId, SelectedCompany);
            return Ok(table);
        }
        [HttpPost("UpdateExitClearance")]
        public async Task<ActionResult> UpdateExitClearance([FromBody] ExitClearance data)
        {
            if (data == null)
            {
                return BadRequest(new { message = "No data received or data is empty." });
            }

            // Process the received data as needed
            // Example: 
            bool table = await _employeeAuthorityService.UpdateExitClearance(data);

            return Ok(table);  // Returning the received list for testing
        }
        [HttpPost("UpdateInterviewDesignerAll")]
        public async Task<ActionResult<List<HoDData>>> UpdateInterviewDesignerAll([FromBody] List<InterviewDesignerData> data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            if (data == null || !data.Any())
            {
                return BadRequest(new { message = "No data received or data is empty." });
            }

            // Process the received data as needed
            // Example: 
            bool table = _employeeAuthorityService.UpdateInterviewDesignerAll(data, SelectedCompany, OldEmpId, _UserId, _UserEmpId, _UserEmpCode, _UserEmpName, _FormId, _EntTerminal, _EntTerminalIP);

            return Ok(data);  // Returning the received list for testing
        }
        [HttpPost("UpdateReportingAll")]
        public async Task<ActionResult<List<HoDData>>> UpdateReportingAll([FromBody] List<ReportingData> data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            if (data == null || !data.Any())
            {
                return BadRequest(new { message = "No data received or data is empty." });
            }

            // Process the received data as needed
            // Example: 
            bool table = await _employeeAuthorityService.UpdateReportingAll(data, SelectedCompany, OldEmpId, _UserId, _UserEmpId, _UserEmpCode, _UserEmpName, _FormId, _EntTerminal, _EntTerminalIP);

            return Ok(data);  // Returning the received list for testing
        }
        [HttpPost("UpdateHiringCheckList")]
        public async Task<ActionResult<List<HoDData>>> UpdateHiringCheckList([FromBody] HiringCheckList data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            if (data == null)
            {
                return BadRequest(new { message = "No data received or data is empty." });
            }

            // Process the received data as needed
            // Example: 
            bool table = _employeeAuthorityService.UpdateHiringCheckList(data, SelectedCompany, OldEmpId, _UserId, _UserEmpId, _UserEmpCode, _UserEmpName, _FormId, _EntTerminal, _EntTerminalIP);

            return Ok(data);  // Returning the received list for testing
        }

        [HttpPost("UpdateWorkFlowsAll")]
        public async Task<ActionResult<List<HoDData>>> UpdateWorkFlows([FromBody] WorkFlows? data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            if (data == null)
            {
                return BadRequest(new { message = "No data received or data is empty." });
            }

            // Process the received data as needed
            // Example: 
            bool table = await _employeeAuthorityService.UpdateWorkFlows(data, SelectedCompany, OldEmpId, _UserId, _UserEmpId, _UserEmpCode, _UserEmpName, _FormId, _EntTerminal, _EntTerminalIP);

            return Ok(data);  // Returning the received list for testing
        }
        public class ExitClearance
        {
            public List<InterDepartmentPolicy>? InterDepartmentPolicy { get; set; }
            public List<InterDepartmentPolicy>? ConcernedDepartmentSetup { get; set; }
            public List<InterDepartmentPolicy>? AssoctateClearance { get; set; }

        }
        public class InterDepartmentPolicy
        {
            public int Id { get; set; }
            public int EmpId { get; set; }
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? NewEmp { get; set; }
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? ItemCompanyId { get; set; }

        }
        public class InterviewDesignerData
        {
            public int Id { get; set; }

            public int EmpId { get; set; }
        }

        public class ReportingData
        {
            public int EmpId { get; set; }
            public int? ReportTo { get; set; }
            public int? ReportToCompany { get; set; }
            public int? Dotted { get; set; }
            public int? DottedReportToCompany { get; set; }
            public string? TDate { get; set; }
            public string? reportedchange { get; set; }


        }
        public class HiringCheckList
        {
            public List<HCList>? HiringChecklist { get; set; }
            public List<HCLGrade>? HiringCheckListGrade { get; set; }
            public List<HCLDetail>? HiringChecklistDetail { get; set; }
            public List<HCLDetail>? HiringChecklistApplicant { get; set; }

        }
        public class HCList
        {
            public int HrChkId { get; set; }
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? NewEmp { get; set; }
            public int? ResponsiblePersonnel { get; set; }
            public int? Selectedcompanyid { get; set; }
        }
        public class HCLGrade
        {
            public string? ChkId { get; set; }

            [JsonConverter(typeof(FlexibleStringConverter))]

            public string NewEmp { get; set; }
            public int? ResponsiblePersonnel { get; set; }
            public int? Selectedcompanyid { get; set; }
        }
        public class HCLDetail
        {
            public int HrDetailId { get; set; }
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? NewEmp { get; set; }
            public int? ResponsiblePersonnel { get; set; }
        }
        public class WorkFlows
        {
            public List<WorkFlowsCore>? WorkCoreSeparately { get; set; }
            public List<WorkFlowsDetail>? WorkCoreDetail { get; set; }
            public List<WorkLeave>? WorkLeave { get; set; }
            public List<WorkEmployeePersonalP>? WorkEmployeePersonalP { get; set; }
            public List<WorkIntraCompanyTransfer>? WorkIntraCompanyTransfer { get; set; }
            public List<WorkManPowerRequisit>? WorkManPowerRequisit { get; set; }
            public List<WorkTimeAdjustment>? WorkTimeAdjustment { get; set; }
            public List<WorkExpenseTransaction>? WorkExpenseTransaction { get; set; }
            public List<WorkPerformanceReview>? WorkPerformanceReview { get; set; }
            public List<WorkProbationEvaluation>? WorkProbationEvaluation { get; set; }
            public List<WorkSeparation>? WorkSeparation { get; set; }
            public List<WorkPMAppraisal>? WorkPMAppraisal { get; set; }
            public List<WorkTrainingNomination>? WorkTrainingNomination { get; set; }
            public List<WorkTrainingRecord>? WorkTrainingRecord { get; set; }
            public List<WorkRPEntry>? WorkRPEntry { get; set; }
            public List<WorkOTEntry>? WorkOTEntry { get; set; }
            public List<WorkManpowerBudget>? WorkManpowerBudget { get; set; }
            public List<WorkEmployeeLoanMaster>? WorkEmployeeLoanMaster { get; set; }
            public List<WorkAppointmentLetterWf>? WorkAppointmentLetterWf { get; set; }
            public List<WorkRecPositionApplied>? WorkRecPositionApplied { get; set; }
        }
        public class WorkFlowsCore
        {
            public int? Id { get; set; }
            public int? WFlowTypeId { get; set; }

            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? NewEmp { get; set; }
            public int? EmployeeId { get; set; }
            public int? CompanyId { get; set; }
        }
        public class WorkFlowsDetail
        {
            public int? wfId { get; set; }
            public int? EmpCompId { get; set; }

            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? NewEmp { get; set; }
            public int? FinalAuthorityEmpId { get; set; }
            public int? FinalAuthorityEmpCompId { get; set; }
        }

        public class WorkLeave : WorkAuthority
        {
            public int? EmpLeaveId { get; set; }
        }
        public class WorkEmployeePersonalP : WorkAuthority
        {
            public int? Req_Id { get; set; }
        }
        public class WorkIntraCompanyTransfer : WorkAuthority
        {
            public int? EntryId { get; set; }
        }
        public class WorkManPowerRequisit : WorkAuthority
        {
            public int? MPRId { get; set; }
        }
        public class WorkTimeAdjustment : WorkAuthority
        {
            public int? TimeAdjId { get; set; }
        }
        public class WorkExpenseTransaction : WorkAuthority
        {
            public int? VFAllowanceTransId { get; set; }
        }
        public class WorkPerformanceReview : WorkAuthority
        {
            public int? RId { get; set; }
        }
        public class WorkProbationEvaluation : WorkAuthority
        {
            public int? PId { get; set; }
        }
        public class WorkSeparation : WorkAuthority
        {
            public int? Sepid { get; set; }
        }
        public class WorkPMAppraisal : WorkAuthority
        {
            public int? AId { get; set; }
        }
        public class WorkTrainingNomination : WorkAuthority
        {
            public int? Id { get; set; }
        }

        public class WorkTrainingRecord : WorkAuthority
        {
            public int? TPID { get; set; }
        }
        public class WorkRPEntry : WorkAuthority
        {
            public int? RpEntryId { get; set; }
        }
        public class WorkOTEntry : WorkAuthority
        {
            public int? OTEntryId { get; set; }
        }
        public class WorkManpowerBudget : WorkAuthority
        {
            public int? Id { get; set; }
        }
        public class WorkEmployeeLoanMaster : WorkAuthority
        {
            public int? EmpLoanId { get; set; }
        }
        public class WorkAppointmentLetterWf : WorkAuthority
        {
            public int? tblOfferLetterWfId { get; set; }
        }
        public class WorkRecPositionApplied : WorkAuthority
        {
            public int? Id { get; set; }
        }

        public class WorkAuthority
        {
            public int? WorkflowID { get; set; }
            public int? Authority_CompanyID { get; set; }
            public int? Authority_EmpId { get; set; }
            public int? EmpCompId { get; set; }
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? NewEmp { get; set; }

        }

    }
}

