using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.Payroll;
using HCMS_Api.Controllers.HCMS.ESS;
using HCMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using static Azure.Core.HttpHeader;


namespace HCMS_Api.Controllers.HCMS.Payroll
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PFSlipViaEmailController : ControllerBase
    {
        private readonly ILogger<UtilitiesController> _logger;
        private readonly PFSlipViaEmailComponent _pfslipEmailComponent;
        private readonly Utilities _utilities;
        private readonly HCMS_Api.Common.Common _common;

        public PFSlipViaEmailController(ILogger<UtilitiesController> logger, PFSlipViaEmailComponent pfslipEmailComponent
            , ClientContextService clientContextService
            , Utilities utilities, HCMS_Api.Common.Common common) 
        {
            _logger = logger;
            _pfslipEmailComponent = pfslipEmailComponent;
            _utilities = utilities;
            _common = common;
        }

        [HttpGet("GetFormInitData")]
        public async Task<IActionResult> GetFormInitData()
        {
            try
            {
                JwtArray LoginUserData = await _common.GetJwtUser();
                string CompanyID = LoginUserData.UserCompanyID;

                var activePMonth = await _pfslipEmailComponent.GetActivePMonth(CompanyID);
                var fiscalyears = await _pfslipEmailComponent.GetClosedFiscalYears(CompanyID);
                var pGroups = await _pfslipEmailComponent.GetPayrollGroups(CompanyID, "HCMSPayroll", LoginUserData.UserEmpID, LoginUserData.FormId);
                var processDates = await _pfslipEmailComponent.GetProcessDateList(CompanyID);

                return Ok(new
                {
                    ActivePMonth = activePMonth,
                    FiscalYears = fiscalyears,
                    PayrollGoups = pGroups,
                    ProcessDates = processDates
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }            
        }

        [HttpGet("GetPFEmployees")]
        public async Task<IActionResult> GetPFEmployees([FromQuery] string pgid, [FromQuery] string fyid, [FromQuery] string PdateId)
        {
            try
            {
                JwtArray LoginUserData = await _common.GetJwtUser();
                string CompanyID = LoginUserData.UserCompanyID;               
                var PFEmployees = await _pfslipEmailComponent.GetPFEmployees(CompanyID, fyid, pgid, LoginUserData.UserEmpID, PdateId);

                return Ok(new
                {
                    PFEmployees = PFEmployees,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("QueuePFEmployees")]
        public async Task<IActionResult> QueuePFEmployees([FromBody] QueuePFEmployeesRequestDTo req)
        {
            try
            {
                if (req == null) return BadRequest("Request body is required.");

                if (req.EmpIds == null || req.EmpIds.Count == 0)
                    return BadRequest("Provide at least one EmpId.");
                if ( req.FyId <= 0)
                    return BadRequest("Valid FyId are required.");

                JwtArray LoginUserData = await _common.GetJwtUser();


                if (!int.TryParse(LoginUserData?.UserCompanyID, out var companyId))
                    return Unauthorized("Invalid UserCompanyID in token.");

                // Insert batch (no TVP; one insert per EmpId, only if employee exists)
                var rids = await _pfslipEmailComponent.InsertReleaseEmailPFSlipBatchAsync(
                    companyId: companyId,
                    pgId: req.PgId,
                    fyId: req.FyId,
                    empIds: req.EmpIds,
                    loginUserData: LoginUserData,
                    applicationId: "HCMSPayroll"                    
                );

                var requested = req.EmpIds.Distinct().Count();
                var inserted = rids.Count;

                var message = inserted > 0
                    ? (inserted == requested
                        ? "Emails queued successfully."
                        : $"Queued {inserted} of {requested} employee(s); some were skipped.")
                    : "No emails queued.";

                return Ok(new
                {
                    Message = message,
                    Requested = requested,
                    Inserted = inserted,
                    PgId = req.PgId,
                    FyId = req.FyId,
                    Rids = rids
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
