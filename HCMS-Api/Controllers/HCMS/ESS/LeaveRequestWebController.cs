using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveRequestWebController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly LeaveComponent _leaveComponent;
        private readonly DataServices _dataservice;
        private readonly EmployeeInformation _employeeInformation;

        public LeaveRequestWebController(IConfiguration configuration, Utilities utilities
            ,ClientContextService clientContextService, LeaveComponent leaveComponent
            , DataServices dataservice, EmployeeInformation employeeInformation)
        {
            _configuration = configuration;
            _utilities = utilities;            
            _clientContextService = clientContextService;
            _leaveComponent = leaveComponent;
            _dataservice = dataservice;
            _employeeInformation = employeeInformation;
        }

        [HttpPost]
        public IActionResult LookupLoadSubOrdinates([FromBody] object value, [FromQuery] string Level)
        {
            try
            {
                var serializeObject = JsonConvert.DeserializeObject<clsLeaveLookUp>(value.ToString());
                string WhereClause = "1=1 ";
                string OrderByClause = "";
                var obj = new LookupControlLegacy(_configuration, _dataservice);

                string Self = _utilities.GetMasterLabelByCode("Self", _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()));
                Self = Self == "" ? "Self" : Self;

                var list = obj.GetLookupQuery(WhereClause + OrderByClause, $"FN_ESS_GetSubOrdinatesList({(string.IsNullOrWhiteSpace(Level) ? "0" : Level)},{_utilities.GetEmpid(_clientContextService.GetClientIP())},'-1','{Self}', '{_utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP())}', '1,2')", "EmpId", "EmpCode", "NameWithShortCompany", "DivisionName", "MainDepartment", "Department", "Designation", "JobGroup");
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message);
            }
        }

        [HttpPost]
        public IActionResult LookupSearchSubOrdinates([FromBody] object value, [FromQuery] string Level)
        {
            try
            {                
                var serializeObject = JsonConvert.DeserializeObject<clsLeaveLookUp>(value.ToString());
                string WhereClause = "1=1";
                var obj = new LookupControlLegacy(_configuration, _dataservice);

                string Self = _utilities.GetMasterLabelByCode("Self", _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()));
                Self = Self == "" ? "Self" : Self;

                var list = obj.GetSearchQuery(
                    serializeObject.search.Replace("'", "''"),
                    serializeObject.fieldName,
                    WhereClause,
                    $"FN_ESS_GetSubOrdinatesList({(string.IsNullOrWhiteSpace(Level) ? "0" : Level)},{_utilities.GetEmpid(_clientContextService.GetClientIP())},'-1','{Self}', '{_utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP())}', '1,2')",
                    "EmpId", "EmpCode", "NameWithShortCompany", "DivisionName", "MainDepartment", "Department", "Designation", "JobGroup");
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message);
            }
        }

        [HttpGet("GetLeaveTypeDropdown/{EmpId}")]
        public IActionResult GetLeaveTypeDropdown(string Empid)
        {
            var EmpAttendance = _leaveComponent.LoadEmpLeaveData(Empid);
            return EmpAttendance.Count > 0 ? StatusCode(201, EmpAttendance) : NoContent();
        }

        [HttpPost("CountLeaveDays")]
        public IActionResult CountLeaveDays(clsLeaveCountParam obj)
        {                                    
            var leaveDetail = _leaveComponent.CountLeaveDaysWeb(
                obj.leavetype, obj.datefrom, obj.dateto, obj.empid, obj.isLFAEntitled, obj.days, obj.description,
                _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString(), _utilities.GetCompanyId(_clientContextService.GetClientIP()), _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()));
            return leaveDetail != null ? StatusCode(201, leaveDetail) : NoContent();
        }

        [HttpGet]
        public IActionResult isLeaveRequestForSubordinatesWeb()
        {            
            var reqForSubordinate = _utilities.isLeaveRequestForSubordinate(_utilities.GetCompanyId(_clientContextService.GetClientIP()), _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()));
            return reqForSubordinate ? StatusCode(201, reqForSubordinate) : NoContent();
        }

        [HttpGet("GetUserLevelsLeaveRequest/{EmpId}")]
        public IActionResult GetUserLevelsLeaveRequest(string EmpId)
        {
            var userLevels = _employeeInformation.GetUserLevels(EmpId);
            return userLevels.Count > 0 ? StatusCode(201, userLevels) : NoContent();
        }

        [HttpGet("GetSubordinatesLeaveRequest/{EmpId}/{Level}")]
        public IActionResult GetSubordinatesLeaveRequest(string EmpId, string Level)
        {
            var empSubordinates = _employeeInformation.GetSubordinates(EmpId, Level);
            return empSubordinates.Count > 0 ? StatusCode(201, empSubordinates) : NoContent();
        }

        [HttpGet("GetLeaveBalanceWeb/{EmpId}/{SortExpression}/{SortDirection}")]
        public IActionResult GetLeaveBalanceWeb(string EmpId, string SortExpression, string SortDirection)
        {
            var empSubordinates = _employeeInformation.GetLeaveInfoFDHD(EmpId, SortExpression, SortDirection);
            return empSubordinates.Count > 0 ? StatusCode(201, empSubordinates) : NoContent();
        }

        [HttpGet("GetWFTrack_LeaveWeb/{EmpId}")]
        public IActionResult GetWFTrack_LeaveWeb(string EmpId)
        {
            var dsdata = _leaveComponent.GetLeaveWorkflowTrack(EmpId, _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString(), _utilities.GetCompanyId(_clientContextService.GetClientIP()), _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()));
            return Ok(dsdata);
        }

        [HttpGet("IsGradeWiseLeaveAvailTypeDefined/{EmpId}")]
        public IActionResult IsGradeWiseLeaveAvailTypeDefined(string EmpId)
        {
            bool Res = _leaveComponent.blnGradeWiseLeaveAvailTypeDefined(EmpId);

            return Ok(new { Result = Res });
        }

        [HttpGet("GetLeaveAvailType/{EmpId}")]
        public IActionResult GetLeaveAvailType(string EmpId)
        {
            string lvType = _leaveComponent.lvType(EmpId);
            return Ok(new { Result = lvType });
        }

        [HttpGet("IsEmployeeAvailableLFA/{EmpId}")]
        public IActionResult IsEmployeeAvailableLFA(string Empid)
        {                        
            bool hf_IsLFARowToShow = false;
            if (_leaveComponent.IsEmployeeLFAEntitled(Empid))
            {
                hf_IsLFARowToShow = _leaveComponent.GetEmployeeAvailableLFA(Empid) > 0;
            }
            return Ok( new { Result = hf_IsLFARowToShow });
        }

        [HttpGet("GetLeaveBalancePolicy/{EmpId}")]
        public IActionResult GetLeaveBalancePolicy(string EmpId)
        {            
            string CompanyID = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            
            var obj = new clsLeaveBalancePolicy
            {
                IsMaintainPrevYrsLvHistory = _utilities.MaintainPreviousYearLeaveBalances(EmpId, CompanyID),
                IsAvailLeavesFromNextLeaveYearAllowed = _leaveComponent.isAvailLeavesFromNextLeaveYearAllowed(EmpId),
                IsAnyLeaveTypeCarryForward = _leaveComponent.isAnyLeaveTypeCarryForward(EmpId),
                IsPenaltyApplicable = _leaveComponent.CheckIsPenaltyApplicable(EmpId)
            };

            return Ok(obj);
        }

        [HttpPost("ValidateControls")]
        public IActionResult ValidateControls([FromBody] clsLeaveCountParam obj)
        {             
            var vmsg = new ValidateUserInput();

            if (obj.leavetype == "000")
                vmsg.leavetype = "Pleaseentermandatoryfields";

            if (string.IsNullOrEmpty(obj.description))
                vmsg.remarks = "Pleaseentermandatoryfields";

            if (string.IsNullOrEmpty(obj.days))
                vmsg.DaysMsg = "Pleaseentermandatoryfields";

            bool dateFromError = false;

            if (!string.IsNullOrWhiteSpace(obj.datefrom) && !_utilities.CheckDate(obj.datefrom))
            {
                vmsg.datefrom = "InvalidDate";
                dateFromError = true;
            }

            if (!string.IsNullOrWhiteSpace(obj.dateto) && !_utilities.CheckDate(obj.dateto))
            {
                vmsg.dateto = "InvalidDate";
                dateFromError = true;
            }

            if (dateFromError)
                return StatusCode(201, vmsg);

            if (!string.IsNullOrWhiteSpace(obj.datefrom) && !string.IsNullOrWhiteSpace(obj.dateto))
            {
                if (!DateTime.TryParse(obj.datefrom, out DateTime dateFrom) ||
                    !DateTime.TryParse(obj.dateto, out DateTime dateTo))
                {
                    vmsg.datefrom = "DateFromLessThanDateTo";
                    return StatusCode(201, vmsg);
                }

                if (dateFrom > dateTo)
                {
                    vmsg.dateto = "DateFromLessThanDateTo";
                    return StatusCode(201, vmsg);
                }

                double dtDiff = (dateTo - dateFrom).TotalDays + 1;
                double dblExcludingDays = 0.0;

                string strFromDate = dateFrom.ToString("yyyy/MM/dd");
                string strToDate = dateTo.ToString("yyyy/MM/dd");

                try
                {
                    dblExcludingDays = Convert.ToDouble(_utilities.GetScalarData(
                        $"top 1 dbo.fn_GetExcludingLeaveDays ({_utilities.GetEmployeeCompanyId(obj.empid)}, '{obj.leavetype}', {obj.empid}, '{strFromDate}', '{strToDate}', 1)",
                        "tblemployee", "1=1"));
                    dtDiff -= dblExcludingDays;
                }
                catch { }

                object objlvType = _utilities.ExecuteSQLFunction($"dbo.fn_Leave_GetEmployeeLeaveAvailType({obj.empid},NULL)");

                if (objlvType?.ToString() == "FDHD")
                {
                    vmsg.FDHDDays = dtDiff.ToString();
                    vmsg.LeaveDays = "FDHD";
                    if (obj.days == "0.5" && vmsg.FDHDDays == "1")
                        vmsg.FDHDDays = obj.days;
                }
                else
                {
                    if (dtDiff > 99999)
                    {
                        vmsg.result = "LeaveRequestmorethanarenotallowed";
                        return StatusCode(201, vmsg);
                    }
                    else
                    {
                        vmsg.DDHMDays = obj.days;
                        vmsg.LeaveDays = "DDHM";
                    }
                }

                try
                {
                    string strSandwichIncurring = Convert.ToString(_utilities.GetScalarData(
                        $"top 1 dbo.fn_GetExcludingLeaveDays ({_utilities.GetEmployeeCompanyId(obj.empid)}, '{obj.leavetype}', {obj.empid}, '{strFromDate}', '{strToDate}', 3)",
                        "tblemployee", "1=1"));

                    if (strSandwichIncurring.Trim().ToUpper() == "YES")
                    {
                        string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                        string colName = culture == "en-GB" ? "MessageEng" : "MessageArb";

                        DataServices dataservice = new DataServices(_configuration);
                        DataSet ds = new DataSet();
                        string msg = dataservice.GetDataWithClause(colName, "tblValidationMessages",
                            $"FormId = '{Constants.LeaveRequest}' and ApplicationCode = 'HRIS' and ValidationCode = '0056'", ref ds);

                        string valmsg = ds.Tables[0].Rows[0][0].ToString().Replace("<br />", " ");
                        vmsg.sandwitch = valmsg;
                        return StatusCode(201, vmsg);
                    }
                }
                catch { }
            }

            return StatusCode(201, vmsg);
        }

        [HttpPost("ValidateLeaveRequestWeb")]
        public IActionResult ValidateLeaveRequestWeb([FromBody] LeaveRequest empLeaveValidate)
        {            
            var valMsg = _leaveComponent.ValidateLeaveRequestWeb(
                empLeaveValidate,
                _utilities.GetEmployeeCompanyId(empLeaveValidate.EmpId),
                _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString(),
                _utilities.GetCompanyId(_clientContextService.GetClientIP()),
                _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP())
            );

            return valMsg != null
                ? StatusCode(201, valMsg)
                : NoContent();
        }

        [HttpPost("SaveLeaveRequest")]
        public IActionResult SaveLeaveRequest([FromBody] LeaveRequest leaveSaveRecord)
        {            
            var leaveSave = _leaveComponent.InsertLeave_Web(leaveSaveRecord);

            if (!string.IsNullOrWhiteSpace(leaveSave))
            {
                return Ok(new { Result = leaveSave });
            }
            else  
            {
                return Ok(new { Result = "Error Occured" });
            }
        }



    }

    
}
