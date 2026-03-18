using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Controllers.DMS.Common;
 

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]

public class DMSUserAccesslevelController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly UserAccessLevelComponent _userAccessLevelComponent;

    public DMSUserAccesslevelController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     UserAccessLevelComponent userAccessLevelComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _userAccessLevelComponent = userAccessLevelComponent;
    }

    [HttpPost("get-all-user-access-levels")]
    public async Task<IActionResult> GetAllUsers(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<UserAccessLevelReadDto>>()
            {
                Success = true,
                Data = await _userAccessLevelComponent.GetAllAsync(input),
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



    [HttpGet("get-user-access-level-by-id/{id}")]
    public async Task<IActionResult> GetUserAccessLevelById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<UserAccessLevelReadDto>()
            {
                Success = true,
                Data = await _userAccessLevelComponent.GetByCodeAsync(id),
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


     
    [HttpPost("create-user-access-level")]
    public async Task<IActionResult> Create([FromBody] UserAccessLevelCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<UserAccessLevelReadDto>()
            {
                Success = true,
                Data = await _userAccessLevelComponent.CreateAsync(input),
                Message = "UserAccessLevel created successfully.",
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

    [HttpPut("update-user-access-level")]
    public async Task<IActionResult> Update([FromBody] UserAccessLevelUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<UserAccessLevelReadDto>()
            {
                Success = true,
                Data = await _userAccessLevelComponent.UpdateAsync(input),
                Message = "UserAccessLevel updated successfully.",
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

    [HttpDelete("delete-user-access-level/{id}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _userAccessLevelComponent.DeleteAsync(id),
                Message = "UserAccessLevel deleted successfully.",
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

