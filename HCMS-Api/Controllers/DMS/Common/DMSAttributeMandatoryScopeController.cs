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
[Route("api/[controller]")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class DMSAttributeMandatoryScopeController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly AttributeMandatoryScopeComponent _attributeMandatoryScopeComponent;

    public DMSAttributeMandatoryScopeController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     AttributeMandatoryScopeComponent attributeMandatoryScopeComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _attributeMandatoryScopeComponent = attributeMandatoryScopeComponent;
    }

    [HttpPost("get-all-attribute-mandatory-scopes")]
    public async Task<IActionResult> GetAllAttributeMandatoryScopes(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<AttributeMandatoryScopeReadDto>>()
            {
                Success = true,
                Data = await _attributeMandatoryScopeComponent.GetAllAsync(input),
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


    [HttpGet("get-all-attribute-mandatory-scopes-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _attributeMandatoryScopeComponent.GetAllSelectList();
            return Ok(new HttpApiResponse<IList<SelectListDto>>()
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


    [HttpGet("get-attribute-mandatory-scopes-by-id/{id}")]
    public async Task<IActionResult> GetAttributeMandatoryScopeById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<AttributeMandatoryScopeReadDto>>()
            {
                Success = true,
                Data = await _attributeMandatoryScopeComponent.GetByCodeAsync(id),
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


    [HttpPost("create-attribute-mandatory-scopes")]
  
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Create([FromBody] AttributeMandatoryScopeCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<AttributeMandatoryScopeReadDto>()
            {
                Success = true,
                Data = await _attributeMandatoryScopeComponent.CreateAsync(input),
                Message = "Attribute Mandatory Scope created successfully.",
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

    [HttpPost("create")]
    [MapToApiVersion("2.0")]
    public IActionResult CreateV2(AttributeMandatoryScopeCreateDto request)
    {
        return Ok("Created using v2");
    }

    [HttpPut("update-attribute-mandatory-scopes")]
    public async Task<IActionResult> Update([FromBody] AttributeMandatoryScopeUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<AttributeMandatoryScopeReadDto>()
            {
                Success = true,
                Data = await _attributeMandatoryScopeComponent.UpdateAsync(input),
                Message = "Attribute Mandatory Scope updated successfully.",
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

    [HttpDelete("delete-attribute-mandatory-scopes/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        { 
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _attributeMandatoryScopeComponent.DeleteAsync(code),
                Message = "Attribute Mandatory Scope deleted successfully.",
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
