using HCMS_Api.Common;
using Microsoft.AspNetCore.Mvc;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using System.Data;
using System.Globalization;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicalReimbursementController : Controller
    {
        private readonly Utilities _utilities;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly ClientContextService _clientContextService;
        private readonly MedicalReimbursementComponent _medicalReimbursementComponent;

        public MedicalReimbursementController(
            Utilities utilities
        , IConfiguration configuration
        , ILogger<UtilitiesController> logger
        , ClientContextService clientContextService
        , MedicalReimbursementComponent medicalReimbursementComponent)
        {
            _logger = logger;
            _utilities = utilities;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _medicalReimbursementComponent = medicalReimbursementComponent;
        }
        [HttpGet("GetDependentsDetail/{empId}")]


        public async Task<IActionResult> GetDependentsDetail(int empId)
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

                var DependentsDetail = await _medicalReimbursementComponent.GetEmpDependentsInfoAsync(strLoginEmpId, strLoginCompanyId);
                DataTable dependents = _configuration.ConvertToDataTable<EmpDependentsInfoSetup>(DependentsDetail);

                return Ok(new
                {
                    dependents
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetFiscalYear/{companyid}")]
        public IActionResult GetFiscalYear(string companyid)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("ConnectionString");
                var strLoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP()).ToString();
                var strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());

                DataTable fiscalYear = _medicalReimbursementComponent.GetFiscalYears(strLoginEmpId, strLoginCompanyId, culture);
                List<FiscalYearModel> FiscalYearModelList = new List<FiscalYearModel>();
                if (fiscalYear.Rows.Count > 0)
                {
                    for (int i = 0; i < fiscalYear.Rows.Count; i++)
                    {
                        FiscalYearModel ObjFiscalYear = new FiscalYearModel
                        {
                            ID = Convert.ToInt32(fiscalYear.Rows[i]["ID"]),
                            Name = Convert.ToString(fiscalYear.Rows[i]["FiscalYear"])
                        };
                        FiscalYearModelList.Add(ObjFiscalYear);
                    }
                }
                return Ok(FiscalYearModelList);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }


        [HttpGet("GetEmployeeMedical/{EmpId}/{claimStatus}/{companyId}/{fiscalYearid:int}")]

        public IActionResult GetEmployeeMedical(string EmpId, string claimStatus, string companyId, int fiscalYearid)

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

                var EmpMedicalList = _medicalReimbursementComponent.GetEmpMedicalInfo(EmpId, claimStatus, strLoginCompanyId, fiscalYearid);

                return Ok(new
                {
                    EmpMedicalInfoData = EmpMedicalList
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }
        [HttpGet("GetEmployeeMedicalData/{EmpId}/{RecordId:int}")]
        public async Task<IActionResult> GetEmployeeMedicalData(string EmpId, int RecordId)
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
                var GetEmpMedical = await _medicalReimbursementComponent.GetEmpMedical(EmpId, RecordId);
                return Ok(new
                {
                    EmpMedical = GetEmpMedical
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetMasterMedicalInformation/{empId}/{DpdId:int}/{fiscalYearId:int}")]
        public async Task<IActionResult> GetMasterMedicalInformation(int empId, int DpdId, int fiscalYearId)
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

                DataTable MasterMedicalInformation = await _medicalReimbursementComponent.GetMasterMedicalInformationAsync(empId, DpdId, fiscalYearId);
                return Ok(new
                {
                    masterMedicalInformation = MasterMedicalInformation
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }


        [HttpGet("GetEmployeeNetAmount/{empId}/{companyId}/{fiscalYearId:int}/{claimStatus}")]
        public async Task<IActionResult> GetEmployeeNetAmount(string empId, string companyId, int fiscalYearId, string claimStatus)
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

                DataTable EmployeeNetAmount = await _medicalReimbursementComponent.GetEmployeeNetAmountAsync(empId, companyId, fiscalYearId, claimStatus);
                return Ok(new { employeeNetAmount = EmployeeNetAmount });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetEmployeeMedicalType/{EmpId:int}/{FyId}/{depdId}")]
        public IActionResult GetEmployeeMedicalType(int EmpId, string FyId, string depdId)
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

                var empMedicalType = _medicalReimbursementComponent.GetEmpMedicalType(EmpId, FyId, depdId);

                return Ok(new
                {
                    EmpMedicalType = empMedicalType
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }



        [HttpGet("GetHospitals")]
        public IActionResult GetHospitals()
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

                var Hospitals = _medicalReimbursementComponent.GetHospital();

                return Ok(new
                {
                    Hospital = Hospitals
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetCurrency")]
        public IActionResult GetCurrency()
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

                var GetCurrency = _medicalReimbursementComponent.GetCurrency();

                return Ok(new
                {
                    Currency = GetCurrency
                });
            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetDocument/{recordId:int}")]
        public async Task<IActionResult> GetDocument(int recordId)
        {
            try
            {

                var document = await _medicalReimbursementComponent.GetDocumentByRecordId(recordId);

                if (document == null || document.DocumentBody == null)
                {
                    return NotFound("The requested document could not be found.");
                }
                return File(document.DocumentBody, GetMimeType(document.Extention ?? string.Empty), $"MedicalDocument_{recordId}{document.Extention}");


            }

            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }

        }

        private string GetMimeType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }


        [HttpGet("GetConversionCount/{currencyId:int}")]
        public async Task<IActionResult> GetConversionCount(int currencyId)
        {
            try
            {
                var conversionCount = await _medicalReimbursementComponent.GetConversionCountAsync(currencyId);
                return Ok(new { count = conversionCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while getting conversion count for CurrencyId: {currencyId}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetSalaryRecordId/{empId:int}/{medicalId:int}/{fyId:int}")]
        public async Task<IActionResult> GetSalaryRecordId(int empId, int medicalId, int fyId)
        {
            try
            {
                var salaryRecordId = await _medicalReimbursementComponent.GetEmpMedicalIdInSalaryTableAsync(empId, medicalId, fyId);
                return Ok(salaryRecordId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

       
        [HttpPost("InsertEmpMedical")]
        public async Task<IActionResult> InsertEmpMedical([FromBody] MedicalTypeSetup payload)
        {
            APIResponse response = new APIResponse();
            if (payload == null)
                return Ok(new APIResponse { IsValid = false, Message = "No data received." });

            try
            {
                var clientIp = _clientContextService.GetClientIP();
                var prefix = _utilities.GetPrefix(clientIp);
                var userId = _utilities.GetUserid(prefix);
                var empIdStr = _utilities.GetEmployeeId(HttpContext, userId);
                var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                if (!int.TryParse(empIdStr, out var empId))
                {
                    _logger.LogWarning($"Validation Failed (Controller): Could not parse EmpId for UserId={userId}. Received='{empIdStr}'");
                    return Ok(new APIResponse { IsValid = false, Message = "Could not identify employee." });
                }
                payload.EMPID = empId;
                if (!payload.MEDTYPEID.HasValue || payload.MEDTYPEID.Value <= 0) 
                {
                    _logger.LogWarning($"Validation Failed (Controller): Missing or invalid MEDTYPEID. EmpId={empId}");
                    return Ok(new APIResponse { IsValid = false, Message = "Medical Entitlement Category is required." });
                }
                int medTypeId = payload.MEDTYPEID.Value;
                if (!payload.FiscalYearID.HasValue || payload.FiscalYearID.Value <= 0) 
                {
                    _logger.LogWarning($"Validation Failed (Controller): Missing or invalid FiscalYearID. EmpId={empId}");
                    return Ok(new APIResponse { IsValid = false, Message = "Fiscal Year is required." });
                }
                int fyId = payload.FiscalYearID.Value;
                int salaryRecordId = await _medicalReimbursementComponent.GetEmpMedicalIdInSalaryTableAsync(empId, medTypeId, fyId);
                if (salaryRecordId <= 0)
                {
                    return Ok(new APIResponse { IsValid = false, Message = "Medical entitlement configuration not found..." });
                }
                var mDateString = payload.MDATE;
                DateTime claimDate;
                string expectedDateFormat = "yyyy-MM-dd";
                if (string.IsNullOrWhiteSpace(mDateString) ||
                    !DateTime.TryParseExact(mDateString, expectedDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out claimDate))
                {
                    return Ok(new APIResponse {/*...*/});
                }

                string serviceDateValidationMessage = await _medicalReimbursementComponent.CheckEmployeeServiceDateAsync(empId, salaryRecordId, claimDate);

                if (!payload.CURRENCYID.HasValue)
                {
                    return Ok(new APIResponse { IsValid = false, Message = "Currency is required." });
                }
                int tranCurrencyId = payload.CURRENCYID.Value;
                int dependentId;
                if (string.IsNullOrWhiteSpace(payload.DPDID) || !int.TryParse(payload.DPDID, out dependentId))
                {
                    if (payload.DPDID == "0" || string.IsNullOrWhiteSpace(payload.DPDID))
                    {
                        dependentId = 0;
                    }
                    else
                    {
                        _logger.LogWarning($"Validation Failed (Controller): Invalid DPDID. EmpId={empId}, Received='{payload.DPDID}'");
                        return Ok(new APIResponse { IsValid = false, Message = "Invalid Dependent ID." });
                    }
                }
                double claimAmount = 0.0; 
                if (!payload.AMOUNT.HasValue || payload.AMOUNT.Value <= 0)
                {
                    if (!payload.TOTALCLAIM.HasValue || payload.TOTALCLAIM.Value <= 0)
                    {
                        return Ok(new APIResponse { IsValid = false, Message = "Claim Amount (TOTALCLAIM) must be greater than zero." });
                    }
                    claimAmount = (double)payload.TOTALCLAIM.Value;
                }
                else
                {
                    claimAmount = (double)payload.AMOUNT.Value;
                }
                int remId = payload.MEDID ?? 0;
                var IsValid = await _medicalReimbursementComponent.CheckAmountValidationAsync(
                     empId,
                     medTypeId,
                     tranCurrencyId,
                     dependentId,
                     claimAmount,
                     claimDate,
                     remId,
                     fyId);
                
                if (dependentId != 0) 
                {
                    (bool isAgeValid, string ageErrorMessage) = await _medicalReimbursementComponent.checkChildrenAge(empId, salaryRecordId, claimDate, dependentId, companyId);
                    if (!isAgeValid)
                    {
                        _logger.LogWarning($"Validation Failed (Controller): Child Age Check. EmpId={empId}, DpdId={dependentId}, Message: {ageErrorMessage}");
                        return Ok(new APIResponse { IsValid = false, Message = "Medical Reimbursement Claim' cannot be entered as the selected child is not covered due to being over age." });
                    }

                    bool isAlive = await _medicalReimbursementComponent.IsDependentAliveAsync(empId, dependentId);
                    if (!isAlive)
                    {
                        _logger.LogWarning($"Validation Failed (Controller): Dependent Alive Check. EmpId={empId}, DpdId={dependentId}");
                        return Ok(new APIResponse { IsValid = false, Message = "Cannot continue! The selected 'Dependant' is expired." }); 
                    }
                }
                
                decimal conversionRate = 0;
                bool isBaseCurrency = await _medicalReimbursementComponent.IsBaseCurrencyAsync(tranCurrencyId);

                if (isBaseCurrency)
                {
                    conversionRate = 1;
                }
                else
                {
                    conversionRate = await _medicalReimbursementComponent.GetLastConversionRateAsync(tranCurrencyId);

                    if (conversionRate == 0)
                    {
                        _logger.LogWarning($"Validation Failed (Controller): No exchange rate defined for non-base currency CurrencyId={tranCurrencyId}.");
                        return Ok(new APIResponse { IsValid = false, Message = "Cannot continue! The exchange rate..." });
                    }
                }

                payload.CONVERSIONRATE = conversionRate;

                var result = await _medicalReimbursementComponent.InsertEmpMedicalAsync(payload);

                return Ok(new
                {
                    isValid = result.IsValid,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new { isValid = false, message = ex.Message });
            }
        }
        
    }
}