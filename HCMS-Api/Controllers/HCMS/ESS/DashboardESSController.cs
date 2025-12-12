using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Security;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Configuration;
using System.Data;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardESSController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ValidateAntiForgeryTokenFilter _validateAntiForgeryTokenFilter;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly EmployeeDashboardComponent _employeeDashboardComponent;
        private readonly EmployeeInformation _employeeInformation;

        public DashboardESSController(IConfiguration configuration,
            ValidateAntiForgeryTokenFilter validateAntiForgeryTokenFilter,
            Utilities utilities, ClientContextService clientContextService,
            EmployeeDashboardComponent employeeDashboardComponent,
            EmployeeInformation employeeInformation)
        {
            _configuration = configuration;
            _validateAntiForgeryTokenFilter = validateAntiForgeryTokenFilter;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _employeeDashboardComponent = employeeDashboardComponent;
            _employeeInformation = employeeInformation;
        }

        [HttpGet("Dashboard/{EmpId}/{Count}/{Levels}/{Operation}/{ModuleId}")]
        public IActionResult Dashboard(int EmpId, int Count, int Levels, string Operation, string ModuleId)
        {
            try
            {                                
                var DashboardMain = _employeeDashboardComponent.GetColumns(EmpId, Count, Levels, Operation, ModuleId);

                if (DashboardMain != null)
                    return Ok(DashboardMain);
                else
                    return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message);
            }
        }

        [HttpGet("DashboardDashlet/{EmpId}/{Count}/{Levels}/{Operation}/{ModuleId}")]
        public IActionResult DashboardDashlet(int EmpId, int Count, int Levels, string Operation, string ModuleId)
        {
            try
            {                
                var DashboardWidgets = _employeeDashboardComponent.GetColumns_Dashlets(EmpId, Count, Levels, Operation, ModuleId);

                if (DashboardWidgets != null)
                    return StatusCode(201, DashboardWidgets); // Created
                else
                    return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("DashboardDashletSub/{EmpId}/{Count}/{Levels}/{Operation}/{ModuleId}")]
        public IActionResult DashboardDashletSub(int EmpId, int Count, int Levels, string Operation, string ModuleId)
        {
            try
            {                
                var DashboardWidgets = _employeeDashboardComponent.GetColumns_SubDashlets(EmpId, Count, Levels, Operation, ModuleId);

                if (DashboardWidgets != null)
                    return StatusCode(201, DashboardWidgets); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("GetNineBoxReportsData/{Operation}/{Level}/{EmpId}/{AppId}")]
        public IActionResult GetNineBoxReportsData(string Operation, int Level, int EmpId, int AppId)
        {
            try
            {
                var NineBox = _employeeDashboardComponent.GetNineBoxReportsData(Operation, Level, EmpId, AppId);

                if (NineBox != null)
                    return StatusCode(201, NineBox); // 201 Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // 417 Expectation Failed
            }
        }



        [HttpGet("GetAppraisalYear")]
        public IActionResult GetAppraisalYear()
        {
            try
            {
                List<AppraisalYear> AppYear = _employeeDashboardComponent.AppraisalYear();

                if (AppYear.Count > 0)
                    return StatusCode(201, AppYear); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("GetAppraisalReport/{EmpId}")]
        public IActionResult GetAppraisalReport(string EmpId)
        {
            try
            {
                List<WidgetDetail> AppYear = _employeeDashboardComponent.GetAppraisalReport(Convert.ToInt32(EmpId));

                if (AppYear.Count > 0)
                    return StatusCode(201, AppYear); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }


        [HttpGet("SetPerformanceReviewReportSession/{EmpId}")]
        public IActionResult SetPerformanceReviewReportSession(string EmpId)
        {
            try
            {
                List<WidgetDetail> performancereviewReport = _employeeDashboardComponent.GetPerformanceReviewReport(Convert.ToInt32(EmpId));

                if (performancereviewReport.Count > 0)
                    return StatusCode(201, performancereviewReport); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpPost("StrategyDesc/{BoxId}/{RRating}")]
        public IActionResult StrategyDesc(string BoxId, int RRating, [FromForm] List<clsNineBoxReportsOutput> BoxDetail)
        {
            try
            {
                var dtlist = _employeeDashboardComponent.GetRptNineBoxEmpDetail(BoxId, RRating, BoxDetail);

                if (dtlist != null && dtlist.Rows.Count > 0)
                {
                    return new ObjectResult(dtlist) { StatusCode = 200 };
                }
                else
                {
                    return NoContent(); // 204
                }
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // 417 Expectation Failed
            }
        }



        [HttpGet("GetAppraisalPerformance/{EmpId}")]
        public IActionResult GetAppraisalPerformance(int EmpId)
        {
            try
            {
                List<WidgetDetail> appraisalPerformance = _employeeDashboardComponent.GetAppraisalPerformance(EmpId);

                if (appraisalPerformance != null)
                    return StatusCode(201, appraisalPerformance); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("GetPayroll")]
        public IActionResult GetPayroll()
        {
            try
            {                
                List<WidgetDetail> payroll = _employeeDashboardComponent.GetPayroll();

                if (payroll != null)
                    return StatusCode(201, payroll); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("GetLevels/{EmpId}")]
        public IActionResult GetLevels(string EmpId)
        {
            try
            {                
                var userLevels = _employeeInformation.GetUserLevels(EmpId);

                if (userLevels.Count > 0)
                    return StatusCode(201, userLevels); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("Dashboard_GetSubordinates/{EmpId}/{Level}")]
        public IActionResult Dashboard_GetSubordinates(string EmpId, string Level)
        {
            try
            {                
                var empSubordinates = _employeeInformation.GetSubordinates(EmpId, Level);

                if (empSubordinates.Count > 0)
                    return StatusCode(201, empSubordinates); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("Employee_Image/{EmpId}/{Level}")]
        public IActionResult Employee_Image(string EmpId, string Level)
        {
            try
            {                                
                var Image = _employeeDashboardComponent.GetEmpImage(EmpId, Level);

                if (Image!= null && Image.Count > 0)
                    return StatusCode(201, Image); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("GetDashletAlerts/{EmpId}")]
        public IActionResult GetDashletAlerts(string EmpId)
        {
            try
            {                
                var GetDashletAlerts = _employeeDashboardComponent.GetDashletAlerts(EmpId);

                if (GetDashletAlerts != null && GetDashletAlerts.Tables.Count > 0)
                    return new JsonResult(GetDashletAlerts) { StatusCode = 201 };
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }



        [HttpGet("GetEmpInfo/{EmpId}/{Level}")]
        public IActionResult GetEmpInfo(string EmpId, string Level)
        {
            try
            {                
                var GetEmpInfo = _employeeDashboardComponent.GetEmpInfo(EmpId, Level);

                if (GetEmpInfo != null && GetEmpInfo.Tables.Count > 0)
                    return new JsonResult(GetEmpInfo) { StatusCode = 201 };
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }
         
        [HttpGet("GetIncompleteHiringChecklist/{EmpId}/{EmpCode}")]
        public IActionResult GetIncompleteHiringChecklist(string EmpId, string EmpCode)
        {
            try
            {        
                var GetIncompleteHiringChecklist = _employeeDashboardComponent.GetIncompleteHiringChecklist(EmpId, EmpCode);

                if (GetIncompleteHiringChecklist != null && GetIncompleteHiringChecklist.Tables.Count > 0)
                    return StatusCode(201, GetIncompleteHiringChecklist); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpPost("SetDashletAlerts")]
        public IActionResult SetDashletAlerts([FromQuery] string Mode, [FromQuery] string Module, [FromQuery] string Alerts)
        {
            try
            {
                var SetDashletAlerts = _employeeDashboardComponent.SetDashletAlerts(Mode, Module, Alerts);

                if (SetDashletAlerts != null && SetDashletAlerts.Tables.Count > 0)
                    return StatusCode(201, SetDashletAlerts); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpPost("FavoriteTabTransaction")]
        public IActionResult FavoriteTabTransaction([FromQuery] string ParentId, [FromQuery] string ItemId)
        {
            try
            {
                var FavoriteTabTransaction = _employeeDashboardComponent.FavoriteTabTransaction(ParentId, ItemId);

                if (FavoriteTabTransaction != null && FavoriteTabTransaction.Tables.Count > 0)
                    return StatusCode(201, FavoriteTabTransaction); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }

        [HttpGet("CheckDashboardRights")]
        public IActionResult CheckDashboardRights()
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                var ds = new DataSet();
                string ApplicationCode = string.Empty;
                int getCompanyId = Convert.ToInt32(_utilities.GetCompanyId(clientIP));
                var objuser = _utilities.GetCurrentUserMap(clientIP);
                ApplicationCode = _utilities.GetApplicationId();

                ds = _utilities.CheckRightsForDashboardButtonESSPORTAL(ApplicationCode, "CheckDashboardRights", getCompanyId, objuser.UserID.ToString());

                if (ds.Tables.Count > 0 && ds.Tables[0] != null)
                    return StatusCode(201, ds.Tables[0].Rows[0]["HasRights"].ToString()); // Created
                else
                    return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Expectation Failed
            }
        }


    }
}
