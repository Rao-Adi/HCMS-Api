using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Security;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Configuration;
using System.Data;
using System.Net;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class UtilitiesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly UserPermission _userPermission;
        private readonly EmployeeInformation _employeeInformation;

        
        public UtilitiesController(IConfiguration configuration, ILogger<UtilitiesController> logger
            , Utilities utilities, ClientContextService clientContextService, UserPermission userPermission
            , EmployeeInformation employeeInformation)
        {
            _configuration = configuration;
            _logger = logger;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _userPermission = userPermission;
            _employeeInformation = employeeInformation;
        }

        [HttpGet("GetEmployeePicParameters/{empId}/{companyId}")]
        public IActionResult GetEmployeePicParameters(string empId, string companyId)
        {
            if (string.IsNullOrEmpty(companyId))
            {
                var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
                
                companyId = _utilities.GetCompanyId(clientIP);
            }

            string imagePath = _utilities.GetEmployeePicPath(empId, companyId);
            return Ok(new { imagePath });
        }

        [HttpGet("GetUserLoginDetails")]
        public IActionResult GetUserLoginDetails()
        {            
            try
            {
                DataTable dt = new DataTable();                
                var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
                 
                string _prefix = _utilities.GetPrefix(clientIP);

                var LoginDetails = _utilities.GetKeyInRedis(_prefix + "OtherData", _configuration);

                if (LoginDetails != null && LoginDetails.Length > 0)
                {
                    dt = (DataTable)JsonConvert.DeserializeObject(LoginDetails, (typeof(DataTable)));

                    if (dt != null && dt.Rows.Count > 0)
                    {                        
                        var result = ConvertDataTableToList(dt);
                        return Ok(result);

                    }
                    else
                    {
                        return Ok(dt);
                    }                        
                }
                else
                {
                    return Ok(dt);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(204);
            }
        }

        public List<Dictionary<string, object>> ConvertDataTableToList(DataTable dt)
        {
            return dt.AsEnumerable()
                     .Select(row => dt.Columns.Cast<DataColumn>()
                         .ToDictionary(col => col.ColumnName, col => row[col]))
                     .ToList();
        }


        [HttpGet("GetLogin")]
        public IActionResult GetLogin()
        {
            var _prefix = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();            
            _prefix = _utilities.GetPrefix(_prefix);

            if (!String.IsNullOrEmpty(_prefix))
            {
                var loginDetails = _utilities.GetKeyInRedis(_prefix + "OtherData", _configuration);

                // Log the original loginDetails before deserialization
                _logger.LogInformation($"Original LoginDetails before deserialization: {loginDetails}");

                if (!String.IsNullOrEmpty(loginDetails))
                {
                    try
                    {
                        // Deserialize directly into a list of loginDetailsModel
                        List<loginDetailsModel> dataList = JsonConvert.DeserializeObject<List<loginDetailsModel>>(loginDetails)
                            ?? new List<loginDetailsModel>();

                        // Initialize null properties with default values
                        foreach (var item in dataList)
                        {
                            item.UICulture ??= "";
                            item.Culture ??= "";
                            // ... (repeat for other nullable properties)
                        }

                        // Log the deserialized data list
                        if (dataList != null)
                        {
                            _logger.LogInformation($"Deserialized DataList: {JsonConvert.SerializeObject(dataList)}");
                            return Ok(dataList);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception details
                        _logger.LogError($"Error during deserialization: {ex}");
                    }
                }
            }

            // Return an empty response if there are issues
            return Ok(new List<loginDetailsModel>());
        }





        [HttpGet("IsTrainingVideoExists/{FormId}")]
        public ActionResult<bool> IsTrainingVideoExists(string FormId)
        {
            try
            {
                bool TrainingVideoExists = false;
                int TotalCount = 0;

                if (!string.IsNullOrEmpty(FormId) && FormId != "null")
                {
                    TotalCount = _utilities.IsTrainingVideoExists(_configuration, FormId);
                    if (TotalCount > 0)
                        TrainingVideoExists = true;
                }

                return Ok(TrainingVideoExists);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("CanInsertParameters/{FormId}/{AppCode}")]
        public IActionResult CanInsertParameters(string FormId, string AppCode)
        {
            string _res = "";            
            var _headertoken = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            _res = _userPermission.CanInsert(FormId, _headertoken, AppCode).ToString();

            if (!String.IsNullOrEmpty(_utilities.CheckSessionTimeOut(_headertoken)))
            {
                _res = "SessionTimeOut";
            }

            return Ok(new { Result = _res });
        }


        [HttpGet("CanDeleteParameters/{FormId}/{AppCode}")]
        public IActionResult CanDeleteParameters(string FormId, string AppCode)
        {
            string _res = "";            
            var _headertoken = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            _res = _userPermission.CanDelete(FormId, _headertoken, AppCode).ToString();

            if (!String.IsNullOrEmpty(_utilities.CheckSessionTimeOut(_headertoken)))
            {
                _res = "SessionTimeOut";
            }

            return Ok(new { Result = _res });
        }

        [HttpGet("CanEditParameters/{FormId}/{AppCode}")]
        public IActionResult CanEditParameters(string FormId, string AppCode)
        {
            string _res = "";           
            var _headertoken = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            _res = _userPermission.CanEdit(FormId, _headertoken, AppCode).ToString();

            if (!String.IsNullOrEmpty(_utilities.CheckSessionTimeOut(_headertoken)))
            {
                _res = "SessionTimeOut";
            }

            return Ok(new { Result = _res });
        }
        [HttpGet("CanViewParameters/{FormId}/{AppCode}")]
        public IActionResult CanViewParameters(string FormId, string AppCode)
        {
            string _res = "";
            var _headertoken = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            _res = _userPermission.CanView(FormId, _headertoken, AppCode).ToString();

            if (!String.IsNullOrEmpty(_utilities.CheckSessionTimeOut(_headertoken)))
            {
                _res = "SessionTimeOut";
            }

            return Ok(new { Result = _res});
        }

        [HttpGet("GetUserLevelsDataParameters/{empId}/{active}")]
        public IActionResult GetUserLevelsDataParameters(string empId, string active)
        {
            var clientIP = _clientContextService.GetClientIP();


            DataTable dt = _utilities.GetUserLevelsData(empId, active, _utilities.GetAppCurrentUICulture(clientIP));

            if (dt != null && dt.Rows.Count > 0)
            {
                return Ok(dt);
            }

            return NoContent();
        }

        [HttpGet("GetSubordinates/{empId}/{level}")]
        public IActionResult GetSubordinates(string empId, string level)
        {
            var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();            
            var empSubordinates = _employeeInformation.GetSubordinates(empId, level);
            return empSubordinates.Count > 0 ? Ok(empSubordinates) : NoContent();
        }

        [HttpGet("GetAppraisalPeriods")]
        public IActionResult GetAppraisalPeriods()
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


            DataTable dt = _utilities.GetAppraisalPeriodsData(strLoginCompanyId);

            if (dt != null && dt.Rows.Count > 0)
            {
                return Ok(dt);
            }

            return NoContent();
        }

        [HttpGet("GetSelectedEmployeePeriods/{empId}")]
        public IActionResult GetSelectedEmployeePeriods(int empId)
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


            DataTable dt = _utilities.GetSelectedEmployeePeriodsData(empId);

            if (dt != null && dt.Rows.Count > 0)
            {
                return Ok(dt);
            }

            return NoContent();
        }

        [HttpGet("GetEmployeeImage/{empId}")]
        public async Task<IActionResult> GetEmployeeImage(int empId)
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

                var imageBytes = await _utilities.GetEmployeeImageAsync(empId, strLoginCompanyId);

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    // Return default image if not found
                    return Ok(null);
                }

                return File(imageBytes, "image/png");

            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetEmpCodeParameters/{empId}")]
        public IActionResult GetEmpCodeParameters(string empId)
        {
            string returnval = _utilities.GetEmpCode(empId);
            if (string.IsNullOrEmpty(returnval))
            {
                return NoContent();
            }
            return Ok(new { EmpCode = returnval });
        }

        [HttpGet("GetEmployeeNameParameters/{EmpId}")]
        public IActionResult GetEmployeeNameParameters(string EmpId)
        {
            var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
            

            string returnval = _utilities.GetEmployeeName(EmpId, _utilities.GetCompanyId(clientIP), _utilities.GetAppCurrentUICulture(clientIP));

            if (string.IsNullOrEmpty(returnval))
            {
                return NoContent();
            }
            return Ok(new { EmpName = returnval });
        }

        [HttpGet("IsInternationalDeployment")]
        public ActionResult<bool> IsInternationalDeployment()
        {
            try
            {
                var raw = _configuration["CorsSettings:InternationalDeployment"];

                var isInternational =
                    string.Equals(raw?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);

                return Ok(isInternational);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read 'InternationalDeployment' from configuration.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("GetEmployeeCurrencyId/{empId}/{companyId}")]
        public async Task<IActionResult> GetEmployeeCurrencyId(int empId, string companyId)
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

                var currencyId = await _utilities.GetEmployeeCurrencyIdAsync(strLoginEmpId, strLoginCompanyId);
                return Ok(new { currencyId });



            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
        [HttpGet("GetCurrencyCode/{currencyId}/{culture}/{companyId}")]
        public async Task<IActionResult> GetCurrencyCode(int currencyId, string culture, string companyId)
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
                var strLoginculture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

                var currencyCode = await _utilities.GetCurrencyCodeAsync(currencyId, strLoginculture, strLoginCompanyId);
                return Ok(new { currencyCode });



            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
        [HttpGet("GetSysCurrentdate")]
        public async Task<IActionResult> GetSysCurrentdate()
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (string.IsNullOrEmpty(clientIP))
                {
                    _logger.LogError("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var sysCurrentDate = await _utilities.GetSysCurrentdateAsync();
                return Ok(new { sysCurrentDate });



            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
        [HttpGet("GetBaseCurrency")]
        public  IActionResult GetBaseCurrency()
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (string.IsNullOrEmpty(clientIP))
                {
                    _logger.LogError("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }
                
                var GetBasecurrency =  _utilities.GetBaseCurrency();
                return Ok(new { GetBasecurrency });



            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetExchangeRate/{currencyId}")]
        public IActionResult GetExchangeRate(int currencyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (string.IsNullOrEmpty(clientIP))
                {
                    _logger.LogError("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var GetExchangerate = _utilities.GetExchangeRate(currencyId);
                return Ok(new { GetExchangerate });



            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
        [HttpGet("isMultiCompanyEnabled")]
        public string isMultiCompanyEnabled()
        {
            bool isMultiCompanyEnabled;
            isMultiCompanyEnabled = _utilities.isMultiCompanyEnabled();
            if (isMultiCompanyEnabled)
                return "1";
            else
                return "0";
        }
        [HttpGet("GetCompanies/{userId}/{formId}")]
       
        public async Task<IActionResult> GetCompanies( string userId, string formId)
        {
           
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(formId))
                return BadRequest(new { message = "userId and formId are required." });

            var tableCompanies = await _utilities.GetCompanyWRTUserAsync(userId, formId);

           
            return Ok(new { Companies = tableCompanies });
        }
        private class loginDetailsModel
        {
            public string? UICulture { get; set; }
            public string? Culture { get; set; }
            public string? UserIdCode { get; set; }
            public string? UserId { get; set; }
            public string? userid { get; set; }
            public string? FileName { get; set; }
            public string? FilePath { get; set; }
            public string? CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public string? CompanyName_UserWise { get; set; }
            public string? BaseCompanyId { get; set; }
            public string EmpId { get; set; }
            public string? PGid { get; set; }
            public string? PGIDS { get; set; }
            public string? EmpName { get; set; }
            public string? Logo { get; set; }
            public string? UserRole { get; set; }
        }
    }
}
