using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSEmployeeDraftObservationController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly EmployeeDraftObservationComponent _observationComponent;

    public DMSEmployeeDraftObservationController(
        Utilities utilities,
        IConfiguration configuration,
        ILogger<UtilitiesController> logger,
        ClientContextService clientContextService,
        EmployeeDraftObservationComponent observationComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _observationComponent = observationComponent;
    }

    [HttpGet("get-by-employeecode/{employeeCode}")]
    public async Task<IActionResult> GetByEmployeeCode(string employeeCode)
    {
        try
        {
            var data = await _observationComponent.GetByEmployeeCodeAsync(employeeCode);
            return Ok(new HttpApiResponse<EmployeeDraftObservation?>()
            {
                Success = true,
                Data = data,
                Message = "Success",
                Code = 200
            });
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
            return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpPost("save-observation")]
    public async Task<IActionResult> SaveObservation([FromBody] EmployeeDraftObservationDto input)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var data = await _observationComponent.SaveObservationAsync(input);
            return Ok(new HttpApiResponse<EmployeeDraftObservation>()
            {
                Success = true,
                Data = data,
                Message = "Observation saved successfully.",
                Code = 200
            });
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
            return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }
}
