using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.ESS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Ocsp;
using System.Data;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppraisalEvaluationController : ControllerBase
    {
        private readonly IConfiguration _configuration;        
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly ILogger<UtilitiesController> _logger;

        public AppraisalEvaluationController(IConfiguration configuration
            , Utilities utilities
            , ClientContextService clientContextService
            , ILogger<UtilitiesController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _utilities = utilities;
            _clientContextService = clientContextService;
        }

        [HttpGet("GetAppraisalEvaluationInitialData")]
        public IActionResult GetAppraisalEvaluationInitialData()
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                
                if (string.IsNullOrEmpty(clientIP))
                {
                    _logger.LogError("Client IP is missing or invalid in the request header.");
                    return Unauthorized(new { message = "Client IP is missing or invalid in the request header." });
                }
                 
                string CompanyID = _utilities.GetCompanyId(clientIP);
                string Culture = _utilities.GetAppCurrentUICulture(clientIP);

                DataTable dtStatuses = _utilities.GetEmployeeStatus(true, Culture);


                //var dsdata = objLeaveComponent.GetLeaveWorkflowTrack("11", util.GetEmpid(clientIP).ToString(), util.GetCompanyId(clientIP), util.GetAppCurrentUICulture(clientIP));                
                return Ok(null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the request: {ex}");
                return StatusCode(500, new { message = "An error occurred while processing the request." });
            }
            
        }



    }
}
