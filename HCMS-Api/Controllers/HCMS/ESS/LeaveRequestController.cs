using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveRequestController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly LeaveComponent _leaveComponent;
        private readonly EmployeeInformation _employeeInformation;

        public LeaveRequestController(IConfiguration configuration, Utilities utilities
            , ClientContextService clientContextService, LeaveComponent leaveComponent
            , EmployeeInformation employeeInformation)
        {
            _configuration = configuration;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _leaveComponent = leaveComponent;
            _employeeInformation = employeeInformation;
        }

        [HttpGet("GetLeaveType/{empId}")]
        public IActionResult GetLeaveType(string empId)
        {            
            var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

            var empAttendance = _leaveComponent.LoadEmployeeData(empId, strLoginEmpId, strLoginCompanyId, culture);

            return empAttendance.Count > 0 ? Ok(empAttendance) : NoContent();
        }

        [HttpGet("CheckLFARow/{empId}")]
        public IActionResult CheckLFARow(string empId)
        {            
            var isEmployeeLFAEntitled = _leaveComponent.IsEmployeeLFAEntitled(empId);
            string hf_IsLFARowToShow;

            if (isEmployeeLFAEntitled)
            {
                var getEmployeeAvailableLFA = _leaveComponent.GetEmployeeAvailableLFA(empId);
                hf_IsLFARowToShow = getEmployeeAvailableLFA > 0 ? "1" : "0";
            }
            else
            {
                hf_IsLFARowToShow = "0";
            }

            hf_IsLFARowToShow = $"{hf_IsLFARowToShow},{isEmployeeLFAEntitled}";
            return Ok(hf_IsLFARowToShow);
        }

        [HttpGet("EnableDisableLFA/{datefrom}/{dateto}/{empid}/{isLFAEntitled}/{days}/{leavetype}/{description}")]
        public IActionResult EnableDisableLFA(string datefrom, string dateto, string empid, bool isLFAEntitled, string days, string leavetype, string description)
        {             
            var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

            var lstReportToEmployees = _leaveComponent.CountLeaveDays(leavetype, datefrom, dateto, empid, isLFAEntitled, days, description, strLoginEmpId, strLoginCompanyId, culture);
            return Ok(lstReportToEmployees);
        }

        [HttpGet("GetEmployeeLeaveAvailType/{empId}")]
        public IActionResult GetEmployeeLeaveAvailType(string empId)
        {            
            var objlvType = _utilities.ExecuteSQLFunction($"dbo.fn_Leave_GetEmployeeLeaveAvailType({empId},NULL)");
            return Ok(objlvType);
        }

        [HttpGet("isLeaveRequestForSubordinates")]
        public IActionResult isLeaveRequestForSubordinates()
        {
            var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            

            var reqForSubordinate = _utilities.isLeaveRequestForSubordinate(_utilities.GetCompanyId(clientIP), _utilities.GetAppCurrentUICulture(clientIP));
            return reqForSubordinate ? Ok(reqForSubordinate) : NoContent();
        }

        [HttpGet("GetUserLevels/{empId}")]
        public IActionResult GetUserLevels(string empId)
        {            
            var userLevels = _employeeInformation.GetUserLevels(empId);
            return userLevels.Count > 0 ? Ok(userLevels) : NoContent();
        }

        [HttpGet("GetSubordinates/{empId}/{level}")]
        public IActionResult GetSubordinates(string empId, string level)
        {            
            var empSubordinates = _employeeInformation.GetSubordinates(empId, level);
            return empSubordinates.Count > 0 ? Ok(empSubordinates) : NoContent();
        }

        [HttpGet("GetLeaveBalance/{empId}/{sortExpression}/{sortDirection}")]
        public IActionResult GetLeaveBalance(string empId, string sortExpression, string sortDirection)
        {       
            var empSubordinates = _employeeInformation.GetLeaveInfoFDHD(empId, sortExpression, sortDirection);
            return empSubordinates.Count > 0 ? Ok(empSubordinates) : NoContent();
        }

        [HttpGet("GetWFTrack_Leave/{empId}")]
        public IActionResult GetWFTrack_Leave(string empId)
        {             
            var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

            var wfAuth = _leaveComponent.BindWorkflowAuthoritiesGrid(empId, strLoginEmpId, strLoginCompanyId, culture);
            return wfAuth.Count > 0 ? Ok(wfAuth) : NoContent();
        }

        [HttpGet("blnGradeWiseLeaveAvailTypeDefined/{empId}")]
        public IActionResult blnGradeWiseLeaveAvailTypeDefined(string empId)
        {            
            var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            
            var blnGradeWiseLeaveAvailTypeDefined = _leaveComponent.blnGradeWiseLeaveAvailTypeDefined(empId, strLoginEmpId, strLoginCompanyId, culture);
            return Ok(blnGradeWiseLeaveAvailTypeDefined);
        }

        [HttpGet("lvType/{empId}")]
        public IActionResult lvType(string empId)
        {
            var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

            var lvType = _leaveComponent.lvType(empId, strLoginEmpId, strLoginCompanyId, culture);
            return lvType.Count() > 0 ? Ok(lvType) : NoContent();
        }

        [HttpGet("getLeaveDays/{dateFrom}/{dateTo}/{leaveDays}/{empId}")]
        public IActionResult getLeaveDays(string dateFrom, string dateTo, string leaveDays, string empId)
        {
            var leaveDaysResult = _leaveComponent.getLeaveDays(dateFrom, dateTo, leaveDays, empId);
            return leaveDaysResult.Count() > 0 ? Ok(leaveDaysResult) : NoContent();
        }

        [HttpGet("getLeaveAvailType_EmployeeWise/{empId}")]
        public IActionResult getLeaveAvailType_EmployeeWise(string empId)
        {         
            var strLeaveAvailType = _leaveComponent.getLeaveAvailType_EmployeeWise(empId);
            return strLeaveAvailType.Count() > 0 ? Ok(strLeaveAvailType) : NoContent();
        }

        [HttpGet("getLeaveDaysInDHMFormat/{dateFrom}/{dateTo}/{leaveDays}/{empId}")]
        public IActionResult getLeaveDaysInDHMFormat(string dateFrom, string dateTo, string leaveDays, string empId)
        {
            var getLeaveDaysInDHMFormat = _leaveComponent.getLeaveDaysInDHMFormat(dateFrom, dateTo, leaveDays, empId);
            return getLeaveDaysInDHMFormat.Count() > 0 ? Ok(getLeaveDaysInDHMFormat) : NoContent();
        }

        [HttpPost("Leave_SaveRecord")]
        public IActionResult Leave_SaveRecord([FromBody] TblEmpLeave leaveSaveRecord)
        {            
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

            if (leaveSaveRecord.EmpId == "Self")
            {
                leaveSaveRecord.EmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            }
            else
            {
                DataSet dsSubordinate = _utilities.GetSubordinates(leaveSaveRecord.EmpId, companyId, culture);
                var isEmployeeInDs = (from row in dsSubordinate.Tables[0].AsEnumerable()
                                      where row.Field<string>("EmpId") == leaveSaveRecord.EmpId
                                      select row.Field<string>("EmpId")).Distinct().Count();
                if (isEmployeeInDs != 0)
                {
                    return NoContent();
                }
            }

            if (leaveSaveRecord.CompanyID == -1)
            {
                leaveSaveRecord.CompanyID = Convert.ToInt32(companyId);
            }

            if (leaveSaveRecord.loginculture == "en-gb")
            {
                leaveSaveRecord.loginculture = culture;
            }

            var empCompanyId = _utilities.GetEmployeeCompanyId(leaveSaveRecord.EmpId);
            var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
            var leaveSave = _leaveComponent.SaveLeaveRequest(leaveSaveRecord, empCompanyId, strLoginEmpId, companyId, culture);
            return leaveSave != null ? Ok(leaveSave) : NoContent();
        }

        [HttpGet("GetEmployeeStatus")]
        public IActionResult GetEmployeeStatus()
        {
            var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            
            var culture = _utilities.GetAppCurrentUICulture(clientIP);

            var dtEmpStatus = _utilities.GetEmployeeStatus(culture);
            return dtEmpStatus.Rows.Count > 0 ? Ok(dtEmpStatus) : NoContent();
        }

        [HttpPost("GetLeaveRequestFDHD")]
        public IActionResult GetLeaveRequestFDHD([FromBody] TblEmpGetLeaveRequest tblGetLeaveRequest)
        {
            var leaveReq = _leaveComponent.GetLeaveRequestFDHD(tblGetLeaveRequest);
            return leaveReq.Count > 0 ? Ok(leaveReq) : NoContent();
        }

        [HttpPost("GetLeaveStatus")]
        public IActionResult GetLeaveStatus([FromBody] TblEmpGetLeaveRequest tblGetLeaveRequest)
        {           
            var leaveReq = _leaveComponent.GetLeaveRequest(tblGetLeaveRequest);
            return leaveReq.Count > 0 ? Ok(leaveReq) : NoContent();
        }

        [HttpPost("ForwardToNextWithApprove")]
        public IActionResult ForwardToNextWithApprove([FromBody] TblForwardToNextWithApprove objParam)
        {
            var forwardToNextWithApprove = _leaveComponent.ForwardToNextWithApprove(objParam);
            return forwardToNextWithApprove.Count() > 0 ? Ok(forwardToNextWithApprove) : NoContent();
        }

        [HttpGet("GetSysCurrentdate")]
        public IActionResult GetSysCurrentdate()
        {
            var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            
            var strLoginCompanyId = _utilities.GetCompanyId(clientIP);

            var getSysCurrentdate = _utilities.GetSysCurrentdate();
            return getSysCurrentdate != null ? Ok(getSysCurrentdate) : NoContent();
        }
    }
}
