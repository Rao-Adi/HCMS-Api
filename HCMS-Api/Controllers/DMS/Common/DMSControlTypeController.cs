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
public class DMSControlTypeController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly ControlTypeComponent _controlTypeComponent;

    public DMSControlTypeController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     ControlTypeComponent divisionComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _controlTypeComponent = divisionComponent;
    }

    [HttpPost("get-all-control-types")]
    public async Task<IActionResult> GetAllControlTypes(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<ControlTypeReadDto>>()
            {
                Success = true,
                Data = await _controlTypeComponent.GetAllAsync(input),
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


    [HttpGet("get-all-control-type-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _controlTypeComponent.GetAllSelectList();
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


    [HttpGet("get-control-type-by-id/{id}")]
    public async Task<IActionResult> GetControlTypeById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<ControlTypeReadDto>()
            {
                Success = true,
                Data = await _controlTypeComponent.GetByIdAsync(id),
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

    [HttpGet("get-control-type-count")]
    public async Task<IActionResult> GetDocumentTypeCount()
    {
        try
        {
            return Ok(new HttpApiResponse<int>()
            {
                Success = true,
                Data = await _controlTypeComponent.GetCount(),
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

    [HttpPost("create-control-type")]
    public async Task<IActionResult> Create([FromBody] ControlTypeCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<ControlTypeReadDto>()
            {
                Success = true,
                Data = await _controlTypeComponent.CreateAsync(input),
                Message = "ControlType created successfully.",
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

    [HttpPut("update-control-type")]
    public async Task<IActionResult> Update([FromBody] ControlTypeUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<ControlTypeReadDto>()
            {
                Success = true,
                Data = await _controlTypeComponent.UpdateAsync(input),
                Message = "ControlType updated successfully.",
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

    [HttpDelete("delete-control-type/{id}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        try
        {
            
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _controlTypeComponent.DeleteAsync(id),
                Message = "ControlType deleted successfully.",
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

