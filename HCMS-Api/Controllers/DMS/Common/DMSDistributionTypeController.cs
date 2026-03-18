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
public class DMSDistributionTypeController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly DistributionTypeComponent _roleComponent;

    public DMSDistributionTypeController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     DistributionTypeComponent roleComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _roleComponent = roleComponent;
    }

    [HttpPost("get-all-distribution-type")]
    public async Task<IActionResult> GetAllDistributionTypes(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DistributionTypeReadDto>>()
            {
                Success = true,
                Data = await _roleComponent.GetAllAsync(input),
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



    [HttpGet("get-distribution-type-by-code/{code}")]
    public async Task<IActionResult> GetDistributionTypeById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<DistributionTypeReadDto>()
            {
                Success = true,
                Data = await _roleComponent.GetByCodeAsync(id),
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

    [HttpGet("get-all-distribution-type-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _roleComponent.GetAllSelectList();
            return Ok(new HttpApiResponse<IList<SelectList2Dto>>()
            {
                Success = true,
                Data = selectList.ToList(),
                Message = "Success",
                Code = 200
            });
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
            return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new string[0]));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new string[0]);
            return StatusCode(response.Code, response);
        }
    }

    [HttpPost("create-distribution-type")]
    public async Task<IActionResult> Create([FromBody] DistributionTypeCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<DistributionTypeReadDto>()
            {
                Success = true,
                Data = await _roleComponent.CreateAsync(input),
                Message = "DistributionType created successfully.",
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

    [HttpPut("update-distribution-type")]
    public async Task<IActionResult> Update([FromBody] DistributionTypeUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<DistributionTypeReadDto>()
            {
                Success = true,
                Data = await _roleComponent.UpdateAsync(input),
                Message = "DistributionType updated successfully.",
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

    [HttpDelete("delete-distribution-type/{id}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        try
        { 
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _roleComponent.DeleteAsync(id),
                Message = "DistributionType deleted successfully.",
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
