using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Controllers.DMS.Common;

/// <summary>Role &lt;-&gt; DMS Cabinet mapping (Setups configurations &gt; Users &gt; Role Cabinet Mapping).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSRoleCabinetMappingController : Controller
{
    private readonly ILogger<UtilitiesController> _logger;
    private readonly RoleCabinetMappingComponent _component;

    public DMSRoleCabinetMappingController(
        ILogger<UtilitiesController> logger,
        RoleCabinetMappingComponent component)
    {
        _logger = logger;
        _component = component;
    }

    [HttpGet("get-all-role-cabinet-mappings")]
    public Task<IActionResult> GetAll() =>
        RunAsync(() => _component.GetAllAsync(), "Success");

    [HttpGet("get-role-cabinet-mapping-by-id/{id}")]
    public Task<IActionResult> GetById(int id) =>
        RunAsync(() => _component.GetByIdAsync(id), "Success");

    [HttpPost("create-role-cabinet-mapping")]
    public Task<IActionResult> Create([FromBody] RoleCabinetMappingCreateDto input) =>
        RunAsync(() => _component.CreateAsync(input), "Role mapped to cabinet successfully.");

    [HttpPut("update-role-cabinet-mapping")]
    public Task<IActionResult> Update([FromBody] RoleCabinetMappingUpdateDto input) =>
        RunAsync(() => _component.UpdateAsync(input), "Role cabinet mapping updated successfully.");

    [HttpDelete("delete-role-cabinet-mapping/{id}")]
    public Task<IActionResult> Delete(int id) =>
        RunAsync(() => _component.DeleteAsync(id), "Role cabinet mapping deleted successfully.");

    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action, string message)
    {
        try
        {
            return Ok(new HttpApiResponse<T>()
            {
                Success = true,
                Data = await action(),
                Message = message,
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
