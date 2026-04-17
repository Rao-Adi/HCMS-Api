using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSDashboardController : Controller
{
    private readonly ILogger<DMSDashboardController> _logger;
    private readonly DashboardComponent _dashboardComponent;

    public DMSDashboardController(
        ILogger<DMSDashboardController> logger,
        DashboardComponent dashboardComponent)
    {
        _logger = logger;
        _dashboardComponent = dashboardComponent;
    }

    [HttpGet("get-dashboard-data/{empId}")]
    public async Task<IActionResult> GetDashboardData(int empId)
    {
        try
        {
            var data = await _dashboardComponent.GetDashboardDataAsync(empId);
            return Ok(new HttpApiResponse<DashboardDataDto>()
            {
                Success = true,
                Data = data,
                Message = "Dashboard data retrieved successfully.",
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