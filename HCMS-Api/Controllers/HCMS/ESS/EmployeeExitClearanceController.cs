using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Org.BouncyCastle.Ocsp;
using System.Data;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeExitClearanceController : ControllerBase
    {
        private readonly Utilities _utilities;
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly IDapperDataService _dapperService;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly ClientContextService _clientContextService;
        private readonly EmployeeExitClearanceComponent _employeeExitClearanceComponent;

        public EmployeeExitClearanceController(
            Utilities utilities
            , DataServices dataservice
            , IConfiguration configuration
            , IDapperDataService dapper
            , ILogger<UtilitiesController> logger
            , ClientContextService clientContextService
            , EmployeeExitClearanceComponent employeeExitClearanceComponent)
        {
            _logger = logger;
            _utilities = utilities;
            _dataservice = dataservice;
            _configuration = configuration;
            _dapperService = dapper;
            _clientContextService = clientContextService;
            _employeeExitClearanceComponent = employeeExitClearanceComponent;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        [HttpPost("GetLookupEmployees")]
        public async Task<IActionResult> GetLookupEmployees([FromBody] object value)
        {
            try
            {
                dynamic serializeObject = JsonConvert.DeserializeObject<dynamic>(value.ToString());

                string clearanceStatus = serializeObject.ClearanceStatus.ToString(); // e.g., "Approved,Pending"
                string strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
                string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

                // Split the comma-separated statuses
                var statusList = clearanceStatus.Split(',').Select(s => s.Trim()).ToList();

                // Generate parameter names dynamically: @status0, @status1, ...
                var statusParams = statusList.Select((status, index) => $"@status{index}").ToList();
                string inClause = string.Join(",", statusParams);

                // Final query with dynamic IN clause
                string sql = $@"
                    SELECT * 
                    FROM HCMS.dbo.ACD_Fn_GetDetails_ForESSLookup(@Culture, @EmpId, @SomeValue)
                    WHERE ClStatus IN ({inClause})
                ";

                // Add all parameters
                var parameters = new DynamicParameters();
                parameters.Add("@Culture", culture);
                parameters.Add("@EmpId", strLoginEmpId);
                parameters.Add("@SomeValue", -1);

                for (int i = 0; i < statusList.Count; i++)
                {
                    parameters.Add($"@status{i}", statusList[i]);
                }

                var list = await _dapperService.QueryAsync<dynamic>(sql, parameters);

                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetEmployeeInformation/{empId}")]
        public async Task<IActionResult> GetEmployeeInformation(string empId)
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

                DataTable employeeProfile = await _employeeExitClearanceComponent.GetEmployeeInformationDataAsync(empId, strLoginCompanyId, culture);
                
                return Ok(new
                {
                    EmployeeProfile = employeeProfile
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
