using HCMS_Api.Common;
using Microsoft.AspNetCore.Mvc;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using System.Data;
using HCMS_Api.Controllers.HCMS.Common;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeePersonalInformationController : Controller
    {
        private readonly Utilities _utilities;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly ClientContextService _clientContextService;
        private readonly EmployeePersonalInformationComponent _employeePersonalInformationComponent;

        public EmployeePersonalInformationController(
    Utilities utilities
    , IConfiguration configuration
    , ILogger<UtilitiesController> logger
    , ClientContextService clientContextService
    , EmployeePersonalInformationComponent employeePersonalInformationComponent)
        {
            _logger = logger;
            _utilities = utilities;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _employeePersonalInformationComponent = employeePersonalInformationComponent;
        }
        [HttpGet("GetEmployeePersonalInfo/{empId}")]
        public async Task<IActionResult> GetEmployeePersonalInfo(string empId)
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
                

                DataTable employeeProfile = _employeePersonalInformationComponent.GetEmployeeInfoData(empId, strLoginCompanyId, culture);
                DataTable fillEmployeeInfo = _employeePersonalInformationComponent.GetEmployeeData(empId, strLoginCompanyId, culture);
                DataTable employeeEmergencyContact = _employeePersonalInformationComponent.GetEmergencyContact(empId, strLoginCompanyId, culture);
                DataTable employeecertificates = await _employeePersonalInformationComponent.GetEmployeeCertificatesAsync(empId, strLoginCompanyId, culture);
                DataTable employeeAcademicQualification = await _employeePersonalInformationComponent.GetEmployeeAcademicQualificationAsync(empId, strLoginCompanyId, culture);
                DataTable employeeDependents = await _employeePersonalInformationComponent.GetEmployeeDependentsAsync(empId, strLoginCompanyId, culture);

                return Ok(new
                {
                    EmployeePersonalProfile = employeeProfile,
                    FillEmployeeProfile = fillEmployeeInfo,
                    EmployeeEmergencyContact = employeeEmergencyContact,
                    EmployeeCertificates = employeecertificates,
                    EmployeeAcademicQualification = employeeAcademicQualification,
                    EmployeeDependents = employeeDependents
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
    }
}
