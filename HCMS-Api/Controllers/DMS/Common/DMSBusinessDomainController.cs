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
public class DMSBusinessDomainController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly BusinessDomainComponent _businessDomainComponent;

    public DMSBusinessDomainController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     BusinessDomainComponent businessDomainComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _businessDomainComponent = businessDomainComponent;
    }

    [HttpPost("get-all-business-domain")]
    public async Task<IActionResult> GetAllBusinessDomains(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<BusinessDomainReadDto>>()
            {
                Success = true,
                Data = await _businessDomainComponent.GetAllAsync(input),
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


    [HttpGet("get-all-business-domain-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _businessDomainComponent.GetAllSelectList();
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


    [HttpGet("get-business-domain-by-code/{code}")]
    public async Task<IActionResult> GetBusinessDomainById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<BusinessDomainReadDto>()
            {
                Success = true,
                Data = await _businessDomainComponent.GetByCodeAsync(code),
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


    [HttpPost("create-business-domain")]
    public async Task<IActionResult> Create([FromBody] BusinessDomainCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        { 
            return Ok(new HttpApiResponse<BusinessDomainReadDto>()
            {
                Success = true,
                Data = await _businessDomainComponent.CreateAsync(input),
                Message = "Business Domain created successfully.",
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

    [HttpPut("update-business-domain")]
    public async Task<IActionResult> Update([FromBody] BusinessDomainUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<BusinessDomainReadDto>()
            {
                Success = true,
                Data = await _businessDomainComponent.UpdateAsync(input),
                Message = "Business Domain updated successfully.",
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

    [HttpDelete("delete-business-domain/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _businessDomainComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Business Domain not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _businessDomainComponent.DeleteAsync(code),
                Message = "Business Domain deleted successfully.",
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
