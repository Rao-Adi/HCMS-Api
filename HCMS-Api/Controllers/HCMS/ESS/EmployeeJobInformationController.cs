using HCMS_Api.Common;
using Microsoft.AspNetCore.Mvc;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using System.Data;
using HCMS_Api.Components.HCMS.Common.Models;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeJobInformationController : ControllerBase
    {
        private readonly Utilities _utilities;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly ClientContextService _clientContextService;
        private readonly EmployeeJobInformationComponent _employeeJobInformationComponent;

        public EmployeeJobInformationController(
            Utilities utilities
            , IConfiguration configuration
            , ILogger<UtilitiesController> logger
            , ClientContextService clientContextService
            , EmployeeJobInformationComponent employeeJobInformationComponent)
        {
            _logger = logger;
            _utilities = utilities;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _employeeJobInformationComponent = employeeJobInformationComponent;
        }

        [HttpGet("GetEmployeeInfo/{empId}")]
        public async Task<IActionResult> GetEmployeeInfo(string empId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (string.IsNullOrEmpty(clientIP))
                {
                    _logger.LogError("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
                var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

                DataTable employeeProfile = _employeeJobInformationComponent.GetEmployeeInfoData(empId, strLoginCompanyId, culture);
                DataTable employeeLeaves = await _employeeJobInformationComponent.GetEmployeeLeavesInfoDataAsync(empId, strLoginCompanyId, culture);
                DataTable employeeHolidays = await _employeeJobInformationComponent.GetEmployeeHolidaysInfoDataAsync(empId, strLoginCompanyId, culture);
                DataTable subjects = await _employeeJobInformationComponent.GetSubjectsDataAsync(empId, strLoginCompanyId, culture);
                DataTable documents = await _employeeJobInformationComponent.GetDocumentsDataAsync(empId, strLoginCompanyId, culture, "0", "null", "null");
                DataTable miscAlerts = await _employeeJobInformationComponent.GetMiscAlertsDataAsync(empId, strLoginCompanyId, culture);
                DataTable awardsGrid = await _employeeJobInformationComponent.GetAwardsGridDataAsync(empId, strLoginCompanyId, culture);
                DataTable disciplinaryActions = await _employeeJobInformationComponent.GetDisciplinaryActionsDataAsync(empId, strLoginCompanyId, culture);
                DataTable completedTrainings = await _employeeJobInformationComponent.GetCompletedTrainingsDataAsync(empId, strLoginCompanyId, culture);
                DataTable assetsDetail = await _employeeJobInformationComponent.GetAssetsDetailDataAsync(empId, strLoginCompanyId, culture);
                DataTable trainingNominationsConfirmations = await _employeeJobInformationComponent.GetTrainingNominationsConfirmationsDataAsync(empId, strLoginCompanyId, culture);
                DataTable jobDescription = await _employeeJobInformationComponent.GetJobDescriptionDataAsync(empId, strLoginCompanyId, culture);
                DataTable activeLoans = await _employeeJobInformationComponent.GetActiveLoansDataAsync(empId, strLoginCompanyId, culture);

                return Ok(new
                {
                    EmployeeProfile = employeeProfile,
                    EmployeeLeaves = employeeLeaves,
                    EmployeeHolidays = employeeHolidays,
                    Subjects = subjects,
                    Documents = documents,
                    MiscAlerts = miscAlerts,
                    AwardsGrid = awardsGrid,
                    DisciplinaryActions = disciplinaryActions,
                    CompletedTrainings = completedTrainings,
                    AssetsDetail = assetsDetail,
                    TrainingNominationsConfirmations = trainingNominationsConfirmations,
                    JobDescription = jobDescription,
                    ActiveLoans = activeLoans
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetFilteredDocument/{empId}/{selectedSubject}/{DateFromSubject}/{DateToSubject}")]
        public async Task<IActionResult> GetFilteredDocument(string empId, string selectedSubject, string DateFromSubject, string DateToSubject)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (string.IsNullOrEmpty(clientIP))
                {
                    _logger.LogError("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
                var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

                DataTable documents = await _employeeJobInformationComponent.GetDocumentsDataAsync(empId, strLoginCompanyId, culture, selectedSubject, DateFromSubject, DateToSubject);

                return Ok(new
                {
                    Documents = documents
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("UpdateEmpDocRecStatus")]
        public async Task<IActionResult> UpdateEmpDocRecStatus([FromBody] DocumentsGrid obj)
        {
            try
            {
                int EmpDocId = Convert.ToInt32(obj?.EmpDocId);
                bool Received = Convert.ToBoolean(obj?.Received);
                var Status = await _employeeJobInformationComponent.UpdateEmpDocRecStatusAsync(EmpDocId, Received);

                if (!string.IsNullOrWhiteSpace(Status))
                {
                    return Ok(new { Result = Status });
                }
                else
                {
                    return Ok(new { Result = "Error Occured" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("UpdateLoan")]
        public async Task<IActionResult> UpdateLoan([FromBody] ActiveLoans obj)
        {
            try
            {
                int LoanMstId = Convert.ToInt32(obj?.LoanMstId);
                bool Received = Convert.ToBoolean(obj?.ReceivedFromPortal);
                var Status = await _employeeJobInformationComponent.UpdateLoanAsync(LoanMstId, Received);

                if (!string.IsNullOrWhiteSpace(Status))
                {
                    return Ok(new { Result = Status });
                }
                else
                {
                    return Ok(new { Result = "Error Occured" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

    }
}
