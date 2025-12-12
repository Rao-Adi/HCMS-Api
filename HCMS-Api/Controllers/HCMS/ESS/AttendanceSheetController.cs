using Microsoft.AspNetCore.Mvc;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using System.Data;
using HCMS_Api.Components.HCMS.Common.Models;
using System.Text;
using System.Net;
using Newtonsoft.Json;
namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceSheetController : Controller
    {
        
        private readonly Utilities _utilities;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly ClientContextService _clientContextService;
        private readonly AttendanceSheetComponent _attendanceSheetComponent;

        public AttendanceSheetController(
         Utilities utilities
       , IConfiguration configuration
       , ILogger<UtilitiesController> logger
       , ClientContextService clientContextService
       , AttendanceSheetComponent attendanceSheetComponent)
        {
            _logger = logger;
            _utilities = utilities;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _attendanceSheetComponent = attendanceSheetComponent;
        }
        [HttpGet("GetUsers/{companyid}")]
        public async Task<IActionResult> GetUsers(string companyid)
        {
            try
            {
                var userList = await _attendanceSheetComponent.GetUsersAsync(companyid);
                var clientIp = _clientContextService.GetClientIP();
                var objUserInformation = _utilities.GetCurrentUserMap(clientIp);

                var selfUser = new UserInfo
                {
                    UserEmpName = "Self",
                    UserID = objUserInformation.UserEmpName
                };

                userList.Insert(0, selfUser);
                return Ok(userList);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
        [HttpGet("GetCompanies")]
        public async Task<dynamic> GetCompanies()
        {
            try
            {
                string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                string Culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

                var companylist = await _attendanceSheetComponent.Getcompanies(CompanyId);

                return new { Companylist = companylist };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching data.", error = ex.Message });
            }
            }
            [HttpGet("GetAttendanceParameters/{UserID}/{ShareUserId}")]
        public async Task<IActionResult> GetAttendanceParameters(
        string UserID,
        string ShareUserId)
        {
            try
            {
                var clientIp = _clientContextService.GetClientIP();
                var objUserInfo = _utilities.GetCurrentUserMap(clientIp);
                string selfValue = objUserInfo.UserEmpName; // Yeh hai 'Self'
                var result = await _attendanceSheetComponent.GetAttnParametersAsync(
                    UserID, selfValue, ShareUserId
                );
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching data.", error = ex.Message });
            }
        }
        [HttpPost("DeleteTemplate/{id}")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Invalid ID provided." });
            }

            try
            {
                string resultMessage = await _attendanceSheetComponent.DeletetblAttnSheetParameterAsync(id);

                if (resultMessage == "Record Deleted successfully")
                {
                    return Ok(new { message = resultMessage });
                }
                else
                {
                    return BadRequest(new { message = resultMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteTemplate for ID {Id}", id);
                return StatusCode(500, new { message = "An internal server error occurred while deleting the record." });
            }
        }
        [HttpGet("GetPayrollStartEndDates")]
        public async Task<IActionResult> GetPayrollStartEndDates()
        {
            try
            {
                var dates = await _attendanceSheetComponent.GetPayrollStartEndDatesAsync();

                return Ok(dates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payroll start/end dates.");
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }
        [HttpGet("AttendanceStatus/{companyId}")]
        public async Task<IActionResult> GetAttStatus(string companyId)
        {
            if (string.IsNullOrEmpty(companyId))
            {
                return BadRequest(new { message = "Company ID is required." });
            }

            try
            {
                var attendanceStatus = await _attendanceSheetComponent.GetAttStatusAsync(companyId);
                return Ok(new
                {
                    AttendanceStatus = attendanceStatus
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred fetching attendance statuses." });
            }
        }
        [HttpGet("RosterShifts/{companyId}/{isShiftDay}")] 
        public async Task<IActionResult> GetRosterShifts(string companyId, string isShiftDay)
        {
            if (string.IsNullOrEmpty(companyId))
            {
                return BadRequest(new { message = "Company ID is required." });
            }

            try
            {
                var data = await _attendanceSheetComponent.GetRosterShiftForAttSheetAgaintsShiftTypeAsync(companyId, isShiftDay);
                return Ok(data);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching roster shift data." });
            }
        }
        [HttpGet("WeekDays")]
        public async Task<IActionResult> GetWeekDaysStatus()
        {
            try
            {
                var data = await _attendanceSheetComponent.NewGetDaysStatusAsync();
                return Ok(data);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching week days status." });
            }
        }
        [HttpGet("AttendanceSource")]
        public async Task<IActionResult> GetAttendanceSource()
        {
            try
            {
                var data = await _attendanceSheetComponent.GetAttendanceModeAsync();
                return Ok(data);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching attendance source." });
            }
        }
        [HttpPost("GenerateReport")]
        public IActionResult GenerateReport([FromBody] ReportRequestDto request)
        {

            string attendanceStatusSql = _attendanceSheetComponent.GetAttendanceStatusHighlightersSql(request.AttendanceFilters!);

            StringBuilder workedHoursSb = _attendanceSheetComponent.GetWorkedHoursHighlightersSql(request.WorkedHoursFilters!);
            string workedHoursSql = workedHoursSb.ToString();
            StringBuilder actualTimeInOutSb = _attendanceSheetComponent.GetActualTimeInOutHighlightersSql(request.ActualTimeInOutFilters!);
            string actualTimeInOutSql = actualTimeInOutSb.ToString();

            return Ok(new { AttendanceStatusSql = attendanceStatusSql, WorkedHoursSql = workedHoursSql, ActualTimeInOutSql = actualTimeInOutSql });
        }
        


    }
}
public class IDNameDataModel
{
    public string? id { get; set; }
    public string? name { get; set; }
}
