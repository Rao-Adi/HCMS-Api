using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.HR;
using HCMS_Api.Controllers.HCMS.ESS;
using HCMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Data;
using static Azure.Core.HttpHeader;

namespace HCMS_Api.Controllers.HCMS.HR
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PerformanceJournalPolicyController : ControllerBase
    {
        private readonly ILogger<UtilitiesController> _logger;
        private readonly PerformanceJournalPolicyComponent _PerformanceJournalPolicyComponent;
        private readonly Utilities _utilities;
        private readonly HCMS_Api.Common.Common _common;

        public PerformanceJournalPolicyController(ILogger<UtilitiesController> logger, PerformanceJournalPolicyComponent PerformanceJournalPolicyComponent
            , ClientContextService clientContextService
            , Utilities utilities, HCMS_Api.Common.Common common)
        {
            _logger = logger;
            _PerformanceJournalPolicyComponent = PerformanceJournalPolicyComponent;
            _utilities = utilities;
            _common = common;
        }

        [HttpGet("GetFormInitData")]
        public async Task<IActionResult> GetFormInitData()
        {
            try
            {
                JwtArray loginUserData = await _common.GetJwtUser();
                string companyId = loginUserData.UserCompanyID;

                var fiscalyears = await _PerformanceJournalPolicyComponent.GetClosedFiscalYears(companyId);
                var grades = await _PerformanceJournalPolicyComponent.GetGrades(companyId);
                var divisions = await _PerformanceJournalPolicyComponent.GetDivisions(companyId);

                return Ok(new
                {
                    FiscalYears = fiscalyears,
                    Grades = grades,
                    Divisions = divisions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
        }

        [HttpGet("Load")]
        public async Task<IActionResult> Load()
        {
            try
            {
                JwtArray loginUser = await _common.GetJwtUser();
                int companyId = int.Parse(loginUser.UserCompanyID);

                // Latest for company; no PG/Settings dependency
                var result = await _PerformanceJournalPolicyComponent.GetPerformancePolicyAsync(companyId, null, null);
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Load PerformanceJournalPolicy failed");
                return StatusCode(500, new { message = "Failed to load." });
            }
        }

        public sealed class SavePayload
        {
            public PmSettingsDto Settings { get; set; } = new();
        }

        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] SavePayload payload)
        {
            try
            {
                JwtArray loginUser = await _common.GetJwtUser();
                payload.Settings.CompanyId = int.Parse(loginUser.UserCompanyID);

                var settingsId = await _PerformanceJournalPolicyComponent.SavePerformancePolicyAsync(payload.Settings, loginUser.UserEmpName);
                return Ok(new { SettingsId = settingsId, Message = "Saved" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Save PerformanceJournalPolicy failed");
                return StatusCode(500, new { message = "Save failed." });
            }
        }



    }
}
