using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Hosting;           // IWebHostEnvironment
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;     // (optional, for static files)
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Ocsp;
using System.Data;
using System.IO;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class PerformanceJournalController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly Utilities _utilities;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly ClientContextService _clientContextService;
        private readonly PerformanceJournalComponent _performanceJournalComponent;

        public PerformanceJournalController(
            IWebHostEnvironment env,
            Utilities utilities
            , IConfiguration configuration
            , ILogger<UtilitiesController> logger
            , ClientContextService clientContextService
            , PerformanceJournalComponent performanceJournalComponent)
        {
            _env = env;
            _logger = logger;
            _utilities = utilities;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _performanceJournalComponent = performanceJournalComponent;
        }

        [HttpGet("GetEmployeeInfo/{empId}")]
        public async Task<IActionResult> GetEmployeeInfo(string empId)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return BadRequest(new { message = "empId is required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var loginEmpId = _utilities.GetEmpid(clientIP);
                var companyId = _utilities.GetCompanyId(clientIP);
                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                var employeeInfo = await _performanceJournalComponent.GetEmployeeInfoDataAsync(empId, companyId, culture);

                if (employeeInfo is null)
                    return NotFound(new { message = "Employee not found." });

                return Ok(new { EmployeeInfo = employeeInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEmployeeInfo failed for {EmpId}", empId);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("InitiatePerformanceJournal/{APID}/{selectedEmpId}/{formId}")]
        public async Task<IActionResult> InitiatePerformanceJournal(int APID, int selectedEmpId, string formId)
        {
            if (string.IsNullOrWhiteSpace(selectedEmpId.ToString()))
                return BadRequest(new { message = "selectedEmpId is required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var loginEmpId = _utilities.GetEmpid(clientIP);
                var companyId = _utilities.GetCompanyId(clientIP);
                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                var result = await _performanceJournalComponent.InitiatePerformanceJournalAsync(
                    APID, selectedEmpId, formId, companyId, culture, loginEmpId);

                // Expecting: new { Status = "...", Message = "..." } or null
                if (result is null)
                    return StatusCode(500, new { message = "No response returned from the stored procedure." });

                var status = (string)(result.Status ?? string.Empty);
                var message = (string)(result.Message ?? string.Empty);

                if (status.Equals("Success", StringComparison.OrdinalIgnoreCase))
                    return Ok(new { Status = status, Message = message }); // success case

                // Not success: bubble up the message
                return BadRequest(new { Status = status, Message = string.IsNullOrWhiteSpace(message) ? "Operation failed." : message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "InitiatePerformanceJournal failed for APID={APID}, selectedEmpId={selectedEmpId}, formId={formId}",
                    APID, selectedEmpId, formId);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetRID/{selectedEmpId:int}/{APID:int}")]
        public async Task<IActionResult> GetRID(int selectedEmpId, int APID)
        {
            if (selectedEmpId <= 0)
                return BadRequest(new { message = "selectedEmpId is required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var ridRow = await _performanceJournalComponent.GetRIDAsync(selectedEmpId, APID);
                if (ridRow is null)
                    return NotFound(new { message = "RID not found (no yearly review matching the given EmpId/APID)." });

                // Extract the RID column from the dynamic row
                int ridValue = Convert.ToInt32(ridRow.RID);

                return Ok(new { RID = ridValue });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRID failed for selectedEmpId={selectedEmpId}, APID={APID}", selectedEmpId, APID);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetSelectedEmp/{selectedEmpId}")]
        public async Task<IActionResult> GetSelectedEmp(int selectedEmpId)
        {
            if (selectedEmpId <= 0)
                return BadRequest(new { message = "selectedEmpId is required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var result = await _performanceJournalComponent.GetSelectedEmpAsync(selectedEmpId);

                return Ok(new { result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSelectedEmp failed for selectedEmpId={selectedEmpId}", selectedEmpId);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetPolicies/{EmpCompanyId:int}/{EmpCompanyAPID:int}/{EmpFID:int}")]
        public async Task<IActionResult> GetPolicies(int EmpCompanyId, int EmpCompanyAPID, int EmpFID)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var res = await _performanceJournalComponent.GetPoliciesAsync(EmpCompanyId, EmpCompanyAPID, EmpFID);

                if (res is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                // 404 only if ALL tables are empty
                if (!res.Table1.Any() && !res.Table2.Any() && !res.Table3.Any() && !res.Table4.Any() && !res.Table5.Any())
                    return NotFound(new { message = "Policies not found." });

                return Ok(new { result = res });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPolicies failed for CompanyId={CompanyId}, APID={APID}, FID={FID}",
                    EmpCompanyId, EmpCompanyAPID, EmpFID);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetCompetencies/{APID:int}/{selectedEmpId}/{RID:int}")]
        public async Task<IActionResult> GetCompetencies(int APID, string selectedEmpId, int RID)
        {
            if (string.IsNullOrWhiteSpace(selectedEmpId))
                return BadRequest(new { message = "empId is required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var companyId = _utilities.GetCompanyId(clientIP);
                var viewerEmpId = _utilities.GetEmpid(clientIP);   // <-- logged-in user

                var rows = await _performanceJournalComponent
                    .GetCompetenciesAsync(APID, selectedEmpId, companyId, RID, viewerEmpId); // <-- pass viewer

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                var list = rows.ToList();
                if (!list.Any())
                    return NotFound(new { message = "Competencies not found." });

                return Ok(new { result = list });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCompetencies failed for APID={APID}, EmpId={EmpId}, RID={RID}", APID, selectedEmpId, RID);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetTargetRatings/{APID:int}/{selectedEmpId}/{CompID:int}")]
        public async Task<IActionResult> GetTargetRatings(int APID, string selectedEmpId, int CompID)
        {
            if (string.IsNullOrWhiteSpace(selectedEmpId))
                return BadRequest(new { message = "empId is required." });
            if (CompID <= 0)
                return BadRequest(new { message = "CompID must be a positive integer." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var companyId = _utilities.GetCompanyId(clientIP);

                var rows = await _performanceJournalComponent.GetTargetRatingsAsync(
                    APID, selectedEmpId, companyId, CompID);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Target Ratings not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GetTargetRatings failed for APID={APID}, EmpId={EmpId}, CompID={CompID}",
                    APID, selectedEmpId, CompID);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("UpdateCompetency/{Operation}/{selectedEmpId:int}/{APID:int}/{CompID:int}/{RID:int}")]
        public async Task<IActionResult> UpdateCompetency(
            string Operation, int selectedEmpId, int APID, int CompID, int RID, [FromBody] Competencies obj)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var companyId = _utilities.GetCompanyId(clientIP);
                var dt = BuildCompetenciesDataTable(obj);

                if (dt == null || dt.Rows.Count == 0)
                    return BadRequest(new { message = "No competency rows to process." });

                var status = await _performanceJournalComponent.UpdateCompetencyAsync(
                    Operation, selectedEmpId, companyId, APID, CompID, RID, dt);

                return Ok(new { Result = string.IsNullOrWhiteSpace(status) ? "Error Occured" : status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateCompetency failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        private static DataTable BuildCompetenciesDataTable(Competencies c)
        {
            // 1) Define schema (all columns from your model)
            var dt = new DataTable("Competencies");

            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("RId", typeof(int));
            dt.Columns.Add("CompID", typeof(int));
            dt.Columns.Add("APId", typeof(int));
            dt.Columns.Add("FId", typeof(int));
            dt.Columns.Add("EmpId", typeof(int));
            dt.Columns.Add("SubId", typeof(int));
            dt.Columns.Add("SubName", typeof(string));
            dt.Columns.Add("SubSecId", typeof(int));
            dt.Columns.Add("SubSecName", typeof(string));
            dt.Columns.Add("SubSecDetailId", typeof(int));
            dt.Columns.Add("Competency", typeof(string));
            dt.Columns.Add("RatingId", typeof(int));              // nullable -> allow DBNull.Value when filling
            dt.Columns.Add("Comments", typeof(string));
            dt.Columns.Add("IsCoreCompetency", typeof(int));      // bool -> int (1/0) for XML/SQL friendliness
            dt.Columns.Add("IncludeInNextPeriod", typeof(int));   // bool -> int
            dt.Columns.Add("JDId", typeof(int));
            dt.Columns.Add("JobDescription", typeof(string));
            dt.Columns.Add("FourthLevelDescription", typeof(string));
            dt.Columns.Add("Behavior", typeof(string));
            dt.Columns.Add("Weightage", typeof(decimal));
            dt.Columns.Add("WeightageSum", typeof(decimal));
            dt.Columns.Add("ActualWeightage", typeof(decimal));   // nullable -> allow DBNull on fill
            dt.Columns.Add("Type", typeof(string));
            dt.Columns.Add("Source", typeof(string));
            dt.Columns.Add("Active", typeof(int));                // bool -> int
            dt.Columns.Add("Locked", typeof(int));                // bool -> int
            dt.Columns.Add("CreateOn", typeof(string));           // use string ISO datetime for safe XML
            dt.Columns.Add("CreateBy", typeof(string));
            dt.Columns.Add("UpdateOn", typeof(string));           // nullable datetime -> string
            dt.Columns.Add("UpdateBy", typeof(string));
            dt.Columns.Add("LastTerminal", typeof(string));
            dt.Columns.Add("CompanyId", typeof(int));
            dt.Columns.Add("TargetRatingID", typeof(int));
            dt.Columns.Add("ReviewRatingID", typeof(int));
            dt.Columns.Add("ReviewRating", typeof(string));
            dt.Columns.Add("TargetRating", typeof(string));
            dt.Columns.Add("MaxRating", typeof(int));
            dt.Columns.Add("CompetenctRating", typeof(int));      // nullable -> allow DBNull
            dt.Columns.Add("Score1", typeof(decimal));
            dt.Columns.Add("Score", typeof(decimal));
            dt.Columns.Add("AchievedRating", typeof(int));        // nullable -> allow DBNull
            dt.Columns.Add("TargetRatingValue", typeof(decimal));
            dt.Columns.Add("SelfRating", typeof(int));            // nullable -> allow DBNull
            dt.Columns.Add("PreviousRating", typeof(int));        // nullable -> allow DBNull
            dt.Columns.Add("ReviewFrequency", typeof(string));
            dt.Columns.Add("PercentScore", typeof(decimal));
            dt.Columns.Add("WeightedScore", typeof(decimal));
            dt.Columns.Add("PerformanceStatusId", typeof(int));   // nullable -> allow DBNull

            // 2) Add one row (map every property; swap nulls to DBNull)
            var r = dt.NewRow();

            r["Id"] = c.Id;
            r["RId"] = c.RId;
            r["CompID"] = c.CompID;
            r["APId"] = c.APId;
            r["FId"] = c.FId;
            r["EmpId"] = c.EmpId;
            r["SubId"] = c.SubId;
            r["SubName"] = (object?)c.SubName ?? DBNull.Value;
            r["SubSecId"] = c.SubSecId;
            r["SubSecName"] = (object?)c.SubSecName ?? DBNull.Value;
            r["SubSecDetailId"] = c.SubSecDetailId;
            r["Competency"] = (object?)c.Competency ?? DBNull.Value;
            r["RatingId"] = (object?)c.RatingId ?? DBNull.Value;
            r["Comments"] = (object?)c.Comments ?? DBNull.Value;
            r["IsCoreCompetency"] = c.IsCoreCompetency ? 1 : 0;
            r["IncludeInNextPeriod"] = c.IncludeInNextPeriod ? 1 : 0;
            r["JDId"] = c.JDId;
            r["JobDescription"] = (object?)c.JobDescription ?? DBNull.Value;
            r["FourthLevelDescription"] = (object?)c.FourthLevelDescription ?? DBNull.Value;
            r["Behavior"] = (object?)c.Behavior ?? DBNull.Value;
            r["Weightage"] = c.Weightage;
            r["WeightageSum"] = c.WeightageSum;
            r["ActualWeightage"] = (object?)c.ActualWeightage ?? DBNull.Value;
            r["Type"] = (object?)c.Type ?? DBNull.Value;
            r["Source"] = (object?)c.Source ?? DBNull.Value;
            r["Active"] = c.Active ? 1 : 0;
            r["Locked"] = c.Locked ? 1 : 0;
            r["CreateOn"] = c.CreateOn == default ? DBNull.Value : (object)c.CreateOn.ToString("yyyy-MM-dd HH:mm:ss");
            r["CreateBy"] = (object?)c.CreateBy ?? DBNull.Value;
            r["UpdateOn"] = c.UpdateOn.HasValue ? (object)c.UpdateOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value;
            r["UpdateBy"] = (object?)c.UpdateBy ?? DBNull.Value;
            r["LastTerminal"] = (object?)c.LastTerminal ?? DBNull.Value;
            r["CompanyId"] = c.CompanyId;
            r["TargetRatingID"] = c.TargetRatingID;
            r["ReviewRatingID"] = c.ReviewRatingID;
            r["ReviewRating"] = (object?)c.ReviewRating ?? DBNull.Value;
            r["TargetRating"] = (object?)c.TargetRating ?? DBNull.Value;
            r["MaxRating"] = c.MaxRating;
            r["CompetenctRating"] = (object?)c.CompetenctRating ?? DBNull.Value;
            r["Score1"] = c.Score1;
            r["Score"] = c.Score;
            r["AchievedRating"] = (object?)c.AchievedRating ?? DBNull.Value;
            r["TargetRatingValue"] = c.TargetRatingValue;
            r["SelfRating"] = (object?)c.SelfRating ?? DBNull.Value;
            r["PreviousRating"] = (object?)c.PreviousRating ?? DBNull.Value;
            r["ReviewFrequency"] = (object?)c.ReviewFrequency ?? DBNull.Value;
            r["PercentScore"] = c.PercentScore;
            r["WeightedScore"] = c.WeightedScore;
            r["PerformanceStatusId"] = (object?)c.PerformanceStatusId ?? DBNull.Value;

            dt.Rows.Add(r);
            return dt;
        }

        [HttpPost("UnreadCounts")]
        public async Task<IActionResult> UnreadCounts([FromBody] UnreadReq req)
        {
            if (req == null || req.EmpId <= 0)
                return BadRequest(new { message = "EmpId is required." });

            var rows = await _performanceJournalComponent.GetUnreadCountsAsync(req.EmpId, req.Ids ?? Array.Empty<int>());
            return Ok(new { result = rows });
        }

        [HttpGet("GetRoles/{selectedEmpId}/{APID:int}")]
        public async Task<IActionResult> GetRoles(string selectedEmpId, int APID)
        {
            if (string.IsNullOrWhiteSpace(selectedEmpId))
                return BadRequest(new { message = "empId is required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var companyId = _utilities.GetCompanyId(clientIP);

                var rows = await _performanceJournalComponent.GetRolesAsync(selectedEmpId, APID, companyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Roles not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GetRoles failed for APID={APID}, EmpId={EmpId}",
                    APID, selectedEmpId);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetCompetenciesForAdd/{RID:int}")]
        public async Task<IActionResult> GetCompetenciesForAdd(int RID)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var companyId = _utilities.GetCompanyId(clientIP);

                var rows = await _performanceJournalComponent.GetCompetenciesForAddAsync(RID, companyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Competencies not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCompetenciesForAdd failed for RID={RID}", RID);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetCompetencyCategorySubCategory/{competencyId:int}")]
        public async Task<IActionResult> GetCompetencyCategorySubCategory(int competencyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                // If your view needs it, you can read companyId here
                // var companyId = _utilities.GetCompanyId(clientIP);

                var rows = await _performanceJournalComponent.GetCompetencyCategorySubCategoryAsync(competencyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Competency category/subcategory not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCompetencyCategorySubCategory failed for CompetencyId={CompetencyId}", competencyId);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetTargetRatings/{APID:int}/{SelectedEmpId:int}/{CompetencyId:int}")]
        public async Task<IActionResult> GetTargetRatings(int APID, int SelectedEmpId, int CompetencyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var companyId = _utilities.GetCompanyId(clientIP);

                var rows = await _performanceJournalComponent
                    .GetTargetRatingsAsync(APID, SelectedEmpId, CompetencyId, companyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Target ratings not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GetTargetRatings failed for APID={APID}, SelectedEmpId={SelectedEmpId}, CompetencyId={CompetencyId}",
                    APID, SelectedEmpId, CompetencyId);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("AddCompetency/{EmpCompanyId:int}/{SelectedEmpId:int}/{EmpCompanyAPID:int}/{RID:int}/{EmpFID:int}")]
        public async Task<IActionResult> AddCompetency(
            int EmpCompanyId, int SelectedEmpId, int EmpCompanyAPID, int RID, int EmpFID, [FromBody] AddCompetency obj)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                // Manual mapping: object -> DataTable (single row)
                var dt = BuildAddCompetencyDataTable(obj);

                var status = await _performanceJournalComponent.AddCompetencyAysnc(
                    EmpCompanyId, SelectedEmpId, EmpCompanyAPID, RID, EmpFID, dt);

                return Ok(new { Result = string.IsNullOrWhiteSpace(status) ? "Error Occured" : status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddCompetency failed for CompanyId={CompanyId}, EmpId={EmpId}, APID={APID}, RID={RID}, FID={FID}",
                    EmpCompanyId, SelectedEmpId, EmpCompanyAPID, RID, EmpFID);
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        private static DataTable BuildAddCompetencyDataTable(AddCompetency c)
        {
            var dt = new DataTable("Competency"); // table node name in XML

            dt.Columns.Add("Competency", typeof(string));
            dt.Columns.Add("IncludeInNextPeriod", typeof(bool));
            dt.Columns.Add("IsCoreCompetency", typeof(bool));
            dt.Columns.Add("JDId", typeof(int));
            dt.Columns.Add("JobCode", typeof(string));
            dt.Columns.Add("JobDescription", typeof(string));
            dt.Columns.Add("JobProfileId", typeof(int));
            dt.Columns.Add("LearningNeeds", typeof(string));
            dt.Columns.Add("SubId", typeof(int));
            dt.Columns.Add("SubName", typeof(string));
            dt.Columns.Add("SubSecDetailId", typeof(int));
            dt.Columns.Add("SubSecId", typeof(int));
            dt.Columns.Add("SubSecName", typeof(string));
            dt.Columns.Add("TargetRating", typeof(string));
            dt.Columns.Add("TargetRatingID", typeof(int));
            dt.Columns.Add("Weightage", typeof(int));
            dt.Columns.Add("RatingRationalRemarks", typeof(string));

            var r = dt.NewRow();
            r["Competency"] = (object?)c.Competency ?? DBNull.Value;
            r["IncludeInNextPeriod"] = c.IncludeInNextPeriod;
            r["IsCoreCompetency"] = c.IsCoreCompetency;
            r["JDId"] = c.JDId;
            r["JobCode"] = (object?)c.JobCode ?? DBNull.Value;
            r["JobDescription"] = (object?)c.JobDescription ?? DBNull.Value;
            r["JobProfileId"] = c.JobProfileId;
            r["LearningNeeds"] = (object?)c.LearningNeeds ?? DBNull.Value;
            r["SubId"] = c.SubId;
            r["SubName"] = (object?)c.SubName ?? DBNull.Value;
            r["SubSecDetailId"] = c.SubSecDetailId;
            r["SubSecId"] = c.SubSecId;
            r["SubSecName"] = (object?)c.SubSecName ?? DBNull.Value;
            r["TargetRating"] = (object?)c.TargetRating ?? DBNull.Value;
            r["TargetRatingID"] = c.TargetRatingID;
            r["Weightage"] = c.Weightage;
            r["RatingRationalRemarks"] = (object?)c.RatingRationalRemarks ?? DBNull.Value;

            dt.Rows.Add(r);
            return dt;
        }

        [HttpPost("MarkThreadRead")]
        public async Task<IActionResult> MarkThreadRead([FromBody] MarkReadDto dto)
        {
            if (dto == null || dto.EmpId <= 0 || dto.CompetencyId <= 0)
                return BadRequest(new { message = "EmpId and CompetencyId are required." });

            // Optional: trust client IP as you do elsewhere (not required for this op)
            await _performanceJournalComponent.MarkThreadReadAsync(dto.EmpId, dto.CompetencyId);
            return Ok(new { ok = true });
        }

        [HttpGet("CompetenciesChatComments/{Id:int}/{RId:int}/{CompID:int}/{APId:int}/{FID:int}/{EmpId:int}/{SubId:int}/{SubSecId:int}/{SubSecDetailId:int}/{JDId:int}/{active:bool}")]
        public async Task<IActionResult> GetCompetenciesChatComments(
            int Id, int RId, int CompID, int APId, int FID, int EmpId,
            int SubId, int SubSecId, int SubSecDetailId, int JDId, bool active)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var result = await _performanceJournalComponent
                    .GetCompetenciesChatCommentsAsync(Id, RId, CompID, APId, FID, EmpId,
                                                      SubId, SubSecId, SubSecDetailId, JDId, active, clientIP);

                return Ok(new { result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCompetenciesChatComments failed. Route values => Id:{Id}, RId:{RId}, CompID:{CompID}, APId:{APId}, FID:{FID}, EmpId:{EmpId}, SubId:{SubId}, SubSecId:{SubSecId}, SubSecDetailId:{SubSecDetailId}, JDId:{JDId}, active:{active}",
                    Id, RId, CompID, APId, FID, EmpId, SubId, SubSecId, SubSecDetailId, JDId, active);

                return StatusCode(500, new { message = "An error occurred loading comments. Check server logs for details." });
            }
        }

        [HttpPost("CompetenciesChatComments")]
        public async Task<IActionResult> CompetenciesChatComments([FromBody] SaveChatCommentDto dto)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                if (dto == null || string.IsNullOrWhiteSpace(dto.Operation))
                    return BadRequest(new { message = "Invalid payload: Operation is required." });

                if ((dto.Attachments?.Count ?? 0) > 0)
                {
                    var m = dto.Attachments[0];
                    dto.AttachmentFileName = m.AttachmentFileName;
                    dto.AttachmentStoredName = m.AttachmentStoredName;
                    dto.AttachmentRelativePath = m.AttachmentRelativePath;
                    dto.AttachmentMimeType = m.AttachmentMimeType;
                    dto.AttachmentSizeBytes = m.AttachmentSizeBytes;
                }

                var result = await _performanceJournalComponent
                    .UpsertCompetenciesChatCommentsAsync(dto, clientIP);

                return Ok(result); // { CCId, Operation, Affected }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompetenciesChatComments (upsert) failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("CompetenciesChatComments/Upload")]
        [RequestSizeLimit(25_000_000)] // 25 MB
        public async Task<IActionResult> UploadAttachment([FromForm] IFormFile file)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid." });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file uploaded." });

                var companyId = _utilities.GetCompanyId(clientIP);
                var webRoot = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                    Directory.CreateDirectory(webRoot);
                }

                var uploadsRoot = Path.Combine(webRoot, "uploads", "pj", companyId.ToString());
                Directory.CreateDirectory(uploadsRoot);

                var originalName = Path.GetFileName(file.FileName);
                var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(originalName)}";
                var storedPath = Path.Combine(uploadsRoot, storedName);

                using (var stream = System.IO.File.Create(storedPath))
                    await file.CopyToAsync(stream);

                var relPath = $"/uploads/pj/{companyId}/{storedName}";
                _logger.LogInformation("Attachment saved at {path}", storedPath);

                return Ok(new
                {
                    attachmentFileName = originalName,
                    attachmentStoredName = storedName,
                    attachmentRelativePath = relPath,
                    attachmentMimeType = file.ContentType,
                    attachmentSizeBytes = file.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadAttachment failed!");
                return StatusCode(500, new { message = "Upload failed." });
            }
        }



        [HttpGet("GetCompanyObjectives/{Include}/{APID}/{EmpId}")]
        public async Task<IActionResult> GetCompanyObjectives(bool Include, int APID, int EmpId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetCompanyObjectivesAsync(Include, APID, EmpId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Company Objectives not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCompanyObjectives failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetDivisionObjectives/{Include}/{EmpId}/{compObj}/{div}/{APID}")]
        public async Task<IActionResult> GetDivisionObjectives(bool Include, int EmpId, int compObj, int div, int APID)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetDivisionObjectivesAsync(Include, EmpId, compObj, div, APID);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Division Objectives not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDivisionObjectives failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetDepartmentObjectives/{Include}/{EmpId}/{divObj}/{compObj}/{dept}/{APID}")]
        public async Task<IActionResult> GetDepartmentObjectives(bool Include, int EmpId, int divObj, int compObj, int dept, int APID)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetDepartmentObjectivesAsync(Include, EmpId, divObj, compObj, dept, APID);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Department Objectives not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDepartmentObjectives failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetSubDepartmentObjectives/{Include}/{EmpId}/{deptObj}/{divObj}/{compObj}/{subdept}/{APID}")]
        public async Task<IActionResult> GetSubDepartmentObjectives(bool Include, int EmpId, int deptObj, int divObj, int compObj, int subdept, int APID)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetSubDepartmentObjectivesAsync(Include, EmpId, deptObj, divObj, compObj, subdept, APID);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Sub-Department Objectives not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSubDepartmentObjectives failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("AddObjective/{RID:int}/{APID:int}/{EmpId:int}/{FID:int}/{FormID}")]
        public async Task<IActionResult> AddObjective(int RID, int APID, int EmpId, int FID, string FormID, [FromBody] AddObjectives obj)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var loginEmpId = _utilities.GetEmpid(clientIP);
                var companyId = _utilities.GetCompanyId(clientIP);
                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                // If you have collections to send down as XML, build them here.
                // For demo purposes we create empty XMLs (the SP requires xml params).
                string measuresTargetsXml = "<MeasuresTargets />";   // TODO: build from your real data
                string strategiesBudgetsXml = "<StrategiesBudgets />"; // TODO: build from your real data

                var status = await _performanceJournalComponent.AddObjectiveAsync(
                    rid: RID,
                    apid: APID,
                    empId: EmpId,
                    fid: FID,
                    formId: FormID,
                    companyId: companyId,
                    dto: obj,
                    measuresTargetsXml: measuresTargetsXml,
                    strategiesBudgetsXml: strategiesBudgetsXml
                );

                return Ok(new { Result = string.IsNullOrWhiteSpace(status) ? "Error Occured" : status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddObjective failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        //[HttpGet("GetObjectives/{RID}/{EmpId}/{CompanyId}")]
        //public async Task<IActionResult> GetObjectives(int RID, int EmpId, int CompanyId)
        //{
        //    try
        //    {
        //        var clientIP = _clientContextService.GetClientIP();
        //        if (string.IsNullOrWhiteSpace(clientIP))
        //        {
        //            _logger.LogWarning("Client IP is missing or invalid in the request header.");
        //            return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
        //        }

        //        var rows = await _performanceJournalComponent.GetObjectivesAsync(RID, EmpId, CompanyId);

        //        if (rows is null)
        //            return StatusCode(500, new { message = "Stored procedure failed or returned null." });

        //        if (!rows.Any())
        //            return NotFound(new { message = "Objectives not found." });

        //        return Ok(new { result = rows });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "GetObjectives failed!");
        //        return StatusCode(500, new { message = "An error occurred while processing the request." });
        //    }
        //}

        [HttpGet("GetObjectives/{RID:int}/{EmpId:int}/{CompanyId:int}")]
        public async Task<IActionResult> GetObjectives(int RID, int EmpId, int CompanyId, [FromServices] IWebHostEnvironment env)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "Client IP is missing or invalid in the request header.");
                }

                var result = await _performanceJournalComponent.GetObjectivesAsync(RID, EmpId, CompanyId);

                // If your component might return IEnumerable<T>, materialize to avoid deferred execution issues.
                var rows = result switch
                {
                    null => null,
                    IEnumerable<object> seq => seq.Cast<object>().ToList()
                    // Remove the fallback arm: _ => new List<object> { result }
                };

                if (rows is null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Data source returned null",
                        detail: "Stored procedure failed or returned null.");
                }

                // ✅ Always 200 OK, even when rows.Count == 0
                return Ok(new { count = rows.Count, result = rows });
            }
            catch (Exception ex)
            {
                var traceId = HttpContext?.TraceIdentifier;
                _logger.LogError(ex, "GetObjectives failed! TraceId={TraceId}", traceId);

                var detail = env.IsDevelopment() ? ex.ToString() : "An unhandled error occurred.";
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "GetObjectives failed",
                    detail: $"{detail} (traceId: {traceId})");
            }
        }

        [HttpGet("DeleteObjectives/{Id}")]
        public async Task<IActionResult> DeleteObjectives(int Id)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.DeleteObjectivesAsync(Id);

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteObjectives failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetPerformanceStatuses/{companyId:int}")]
        public async Task<IActionResult> GetPerformanceStatuses(int companyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetPerformanceStatusesAsync(companyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPerformanceStatuses failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetRatingsOR/{APId:int}/{EmpId:int}/{companyId:int}")]
        public async Task<IActionResult> GetRatingsOR(int APId, int EmpId, int companyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                var rows = await _performanceJournalComponent.GetRatingsORAsync(APId, EmpId, culture, companyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Stored procedure failed or returned null." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRatingsORAsync failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("SetObjectiveChatComments")]
        public async Task<IActionResult> SetObjectiveChatComments([FromBody] JObject obj)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var result = await _performanceJournalComponent.SetObjectiveChatCommentsAsync(obj, clientIP);
                return Ok(result); // { OCId, Operation, Affected, Attachments }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetObjectiveChatComments failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetObjectiveChatComments")]
        public async Task<IActionResult> GetObjectiveChatComments(
            [FromQuery] int id,
            [FromQuery] int rId,
            [FromQuery] int apId,
            [FromQuery] int empId,
            [FromQuery] int fId,
            [FromQuery] int companyId)
        {
            try
            {
                var clientIp = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIp))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var payload = await _performanceJournalComponent.GetObjectiveChatCommentsAsync(
                    new ObjectiveChatCommentsQuery
                    {
                        Id = id,
                        RId = rId,
                        APId = apId,
                        EmpId = empId,
                        FID = fId,
                        CompanyId = companyId
                    },
                    clientIp);

                return Ok(payload); // { items = [...] }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetObjectiveChatComments failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("ObjectiveChatComments/Upload")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> UploadObjectiveCommentAttachment(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var clientIP = _clientContextService.GetClientIP();
            if (string.IsNullOrWhiteSpace(clientIP))
                return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

            var companyIdText = _utilities.GetCompanyId(clientIP);
            if (string.IsNullOrWhiteSpace(companyIdText) || !int.TryParse(companyIdText, out var companyId))
                return BadRequest(new { message = "Unable to resolve CompanyId from request." });

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var target = Path.Combine(webRoot, "uploads", "pj", companyId.ToString());
            Directory.CreateDirectory(target);

            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var relativePath = Path.Combine("uploads", "pj", companyId.ToString(), storedName).Replace('\\', '/');
            var fullPath = Path.Combine(target, storedName);

            await using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            return Ok(new
            {
                attachmentFileName = file.FileName,
                attachmentStoredName = storedName,
                attachmentRelativePath = "/" + relativePath,
                attachmentMimeType = file.ContentType,
                attachmentSizeBytes = file.Length
            });
        }

        [HttpPost("ObjectiveUnreadCounts")]
        public async Task<IActionResult> ObjectiveUnreadCounts([FromBody] ObjectiveUnreadRequest req)
        {
            if (req == null || req.EmpId <= 0)
                return BadRequest(new { message = "EmpId is required." });

            var rows = await _performanceJournalComponent.GetObjectiveUnreadCountsAsync(
                req.EmpId,
                req.ObjectiveIds ?? Array.Empty<int>());

            return Ok(new { result = rows });
        }

        [HttpPost("MarkObjectiveThreadRead")]
        public async Task<IActionResult> MarkObjectiveThreadRead([FromBody] ObjectiveMarkReadRequest dto)
        {
            if (dto == null || dto.EmpId <= 0 || dto.ObjectiveId <= 0)
                return BadRequest(new { message = "EmpId and ObjectiveId are required." });

            await _performanceJournalComponent.MarkObjectiveThreadReadAsync(dto.EmpId, dto.ObjectiveId);
            return Ok(new { ok = true });
        }

        [HttpGet("GetResponsibilities/{EmpId:int}/{CompanyId:int}")]
        public async Task<IActionResult> GetResponsibilities(int EmpId, int CompanyId, [FromServices] IWebHostEnvironment env)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "Client IP is missing or invalid in the request header.");
                }

                var result = await _performanceJournalComponent.GetResponsibilitiesAsync(EmpId, CompanyId);

                // If your component might return IEnumerable<T>, materialize to avoid deferred execution issues.
                var rows = result switch
                {
                    null => null,
                    IEnumerable<object> seq => seq.Cast<object>().ToList()
                    // Remove the fallback arm: _ => new List<object> { result }
                };

                if (rows is null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Data source returned null",
                        detail: "Stored procedure failed or returned null.");
                }

                // ✅ Always 200 OK, even when rows.Count == 0
                return Ok(new { count = rows.Count, result = rows });
            }
            catch (Exception ex)
            {
                var traceId = HttpContext?.TraceIdentifier;
                _logger.LogError(ex, "GetResponsibilities failed! TraceId={TraceId}", traceId);

                var detail = env.IsDevelopment() ? ex.ToString() : "An unhandled error occurred.";
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "GetResponsibilities failed",
                    detail: $"{detail} (traceId: {traceId})");
            }
        }

        /// <summary>
        /// POST: api/PerformanceJournal/UpdateResponsibility
        /// Update responsibility description for an employee's job profile
        /// </summary>
        [HttpPost("UpdateResponsibility")]
        public async Task<IActionResult> UpdateResponsibility([FromBody] UpdateResponsibilityRequest request)
        {
            if (request == null || request.EmpId <= 0 || request.CompanyId <= 0 || request.JobProfileId <= 0)
                return BadRequest(new { message = "EmpId, CompanyId, and JobProfileId are required." });

            if (string.IsNullOrWhiteSpace(request.Responsibilities))
                return BadRequest(new { message = "Responsibilities cannot be empty." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var result = await _performanceJournalComponent.UpdateResponsibilityAsync(
                    request.EmpId,
                    request.CompanyId,
                    request.JobProfileId,
                    request.Responsibilities,
                    request.changeResponsibilityDetail,
                    clientIP);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateResponsibility failed!");
                return StatusCode(500, new { message = "An error occurred while updating the responsibility." });
            }
        }

        [HttpGet("GetResponsibilitiesChangeHistory/{EmpId:int}/{CompanyId:int}")]
        public async Task<IActionResult> GetResponsibilitiesChangeHistory(int EmpId, int CompanyId, [FromServices] IWebHostEnvironment env)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "Client IP is missing or invalid in the request header.");
                }

                var result = await _performanceJournalComponent.GetResponsibilitiesChangeHistoryAsync(EmpId, CompanyId);

                // If your component might return IEnumerable<T>, materialize to avoid deferred execution issues.
                var rows = result switch
                {
                    null => null,
                    IEnumerable<object> seq => seq.Cast<object>().ToList()
                    // Remove the fallback arm: _ => new List<object> { result }
                };

                if (rows is null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Data source returned null",
                        detail: "Stored procedure failed or returned null.");
                }

                // ✅ Always 200 OK, even when rows.Count == 0
                return Ok(new { count = rows.Count, result = rows });
            }
            catch (Exception ex)
            {
                var traceId = HttpContext?.TraceIdentifier;
                _logger.LogError(ex, "GetResponsibilitiesChangeHistory failed! TraceId={TraceId}", traceId);

                var detail = env.IsDevelopment() ? ex.ToString() : "An unhandled error occurred.";
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "GetResponsibilitiesChangeHistory failed",
                    detail: $"{detail} (traceId: {traceId})");
            }
        }

        [HttpPost("SetResponsibilityChatComments")]
        public async Task<IActionResult> SetResponsibilityChatComments([FromBody] JObject obj)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var result = await _performanceJournalComponent.SetResponsibilityChatCommentsAsync(obj, clientIP);
                return Ok(result); // { Id, Operation, Affected, Attachments }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetResponsibilityChatComments failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetResponsibilityChatComments")]
        public async Task<IActionResult> GetResponsibilityChatComments(
            [FromQuery] int empId,
            [FromQuery] int jobProfileId,
            [FromQuery] int companyId)
        {
            try
            {
                var clientIp = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIp))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var payload = await _performanceJournalComponent.GetResponsibilityChatCommentsAsync(
                    new ResponsibilityChatCommentsQuery
                    {
                        EmpId = empId,
                        JobProfileId = jobProfileId,
                        CompanyId = companyId
                    },
                    clientIp);

                return Ok(payload); // { items = [...] }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetResponsibilityChatComments failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("ResponsibilityChatComments/Upload")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> UploadResponsibilityCommentAttachment(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var clientIP = _clientContextService.GetClientIP();
            if (string.IsNullOrWhiteSpace(clientIP))
                return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

            var companyIdText = _utilities.GetCompanyId(clientIP);
            if (string.IsNullOrWhiteSpace(companyIdText) || !int.TryParse(companyIdText, out var companyId))
                return BadRequest(new { message = "Unable to resolve CompanyId from request." });

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var target = Path.Combine(webRoot, "uploads", "pj", "responsibilities", companyId.ToString());
            Directory.CreateDirectory(target);

            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var relativePath = Path.Combine("uploads", "pj", "responsibilities", companyId.ToString(), storedName).Replace('\\', '/');
            var fullPath = Path.Combine(target, storedName);

            await using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            return Ok(new
            {
                attachmentFileName = file.FileName,
                attachmentStoredName = storedName,
                attachmentRelativePath = "/" + relativePath,
                attachmentMimeType = file.ContentType,
                attachmentSizeBytes = file.Length
            });
        }

        /// <summary>
        /// POST: api/PerformanceJournal/ResponsibilityUnreadCount
        /// Get unread count for a specific responsibility
        /// </summary>
        [HttpPost("ResponsibilityUnreadCount")]
        public async Task<IActionResult> GetResponsibilityUnreadCount([FromBody] JObject payload)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                var result = await _performanceJournalComponent.GetResponsibilityUnreadCountAsync(payload, clientIP);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetResponsibilityUnreadCount failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        /// <summary>
        /// POST: api/PerformanceJournal/MarkResponsibilityThreadRead
        /// Mark responsibility chat comments as read for the current user
        /// </summary>
        [HttpPost("MarkResponsibilityThreadRead")]
        public async Task<IActionResult> MarkResponsibilityThreadRead([FromBody] ResponsibilityMarkReadRequest dto)
        {
            if (dto == null || dto.EmpId <= 0 || dto.JobProfileId <= 0 || dto.CompanyId <= 0)
                return BadRequest(new { message = "EmpId, JobProfileId, and CompanyId are required." });

            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });

                await _performanceJournalComponent.MarkResponsibilityThreadReadAsync(dto.EmpId, dto.JobProfileId, dto.CompanyId, clientIP);
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarkResponsibilityThreadRead failed!");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetAllCompanies")]
        public async Task<IActionResult> GetAllCompanies()
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetAllCompaniesAsync();

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Companies not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllCompanies failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetDivisions/{EmpId}/{CompanyId}")]
        public async Task<IActionResult> GetDivisions(int EmpId, int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                var rows = await _performanceJournalComponent.GetDivisionsAsync(EmpId, CompanyId, culture);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Divisions not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDivisions failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetDepartments/{DivId}/{CompanyId}")]
        public async Task<IActionResult> GetDepartments(int DivId, int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                var rows = await _performanceJournalComponent.GetDepartmentsAsync(DivId, CompanyId, culture);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Departments not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDepartments failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetSubDepartments/{DivId}/{DeptId}/{CompanyId}")]
        public async Task<IActionResult> GetSubDepartments(int DivId, int DeptId, int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var culture = _utilities.GetAppCurrentUICulture(clientIP);

                var rows = await _performanceJournalComponent.GetSubDepartmentsAsync(DivId, DeptId, CompanyId, culture);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Sub-Departments not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSubDepartments failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetSetups/{smsid}/{CompanyId}")]
        public async Task<IActionResult> GetSetups(int smsid, int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetSetupsAsync(smsid, CompanyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Data not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSetups failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetRolesFromSetupsByGrade/{smsid}/{CompanyId}/{Grade}")]
        public async Task<IActionResult> GetRolesFromSetupsByGrade(int smsid, int CompanyId, int Grade)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetRolesFromSetupsByGradeAsync(smsid, CompanyId, Grade);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Data not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSetups failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetCountries/{CompanyId}")]
        public async Task<IActionResult> GetCountries(int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetCountriesAsync(CompanyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Countries not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCountries failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetCities/{cntid}/{CompanyId}")]
        public async Task<IActionResult> GetCities(int cntid, int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetCitiesAsync(cntid, CompanyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Cities not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCities failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("GetEmployeeCurrentPositionInformation/{SelectedEmpId}/{CompanyId}")]
        public async Task<IActionResult> GetEmployeeCurrentPositionInformation(int SelectedEmpId, int CompanyId)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    _logger.LogWarning("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }

                var rows = await _performanceJournalComponent.GetEmployeeCurrentPositionInformationAsync(SelectedEmpId, CompanyId);

                if (rows is null)
                    return StatusCode(500, new { message = "Query failed or returned null." });

                if (!rows.Any())
                    return NotFound(new { message = "Employee Current Position Information not found." });

                return Ok(new { result = rows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEmployeeCurrentPositionInformation failed");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }



    }
}

public class ResponsibilityMarkReadRequest
{
    public int EmpId { get; set; }
    public int JobProfileId { get; set; }
    public int CompanyId { get; set; }
}

public class UpdateResponsibilityRequest
{
    public int EmpId { get; set; }
    public int CompanyId { get; set; }
    public int JobProfileId { get; set; }
    public string? Responsibilities { get; set; }
    public string? changeResponsibilityDetail { get; set; }
}
