using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSResponsibilityTransferController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ResponsibilityTransferComponent _responsibilityTransferComponent;

    public DMSResponsibilityTransferController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger,
     ResponsibilityTransferComponent responsibilityTransferComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _responsibilityTransferComponent = responsibilityTransferComponent;
    }

    [HttpPost("get-all-responsibility-transfer")]
    public async Task<IActionResult> GetAllResponsibilityTransfer(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<ResponsibilityTransfer>>()
            {
                Success = true,
                Data = await _responsibilityTransferComponent.GetAllAsync(input),
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


    [HttpGet("get-responsibility-transfer-by-code/{code}")]
    public async Task<IActionResult> GetResponsibilityTransferById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<ResponsibilityTransfer>()
            {
                Success = true,
                Data = await _responsibilityTransferComponent.GetByCodeAsync(code),
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

    [HttpPost("create-responsibility-transfer")]
    public async Task<IActionResult> Create([FromBody] ResponsibilityTransfer input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<ResponsibilityTransfer>()
            {
                Success = true,
                Data = await _responsibilityTransferComponent.CreateAsync(input),
                Message = "DocumentType created successfully.",
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

    [HttpPut("update-responsibility-transfer")]
    public async Task<IActionResult> Update([FromBody] ResponsibilityTransfer input)
    {
        try
        {
            return Ok(new HttpApiResponse<ResponsibilityTransfer>()
            {
                Success = true,
                Data = await _responsibilityTransferComponent.UpdateAsync(input),
                Message = "DocumentType updated successfully.",
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

    [HttpDelete("delete-responsibility-transfer/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _responsibilityTransferComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "DocumentType not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _responsibilityTransferComponent.DeleteAsync(code),
                Message = "DocumentType deleted successfully.",
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
