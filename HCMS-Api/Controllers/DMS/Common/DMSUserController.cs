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

public class DMSUserController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly UserComponent _userComponent;

    public DMSUserController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     UserComponent userComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _userComponent = userComponent;
    }

    [HttpPost("get-all-user")]
    public async Task<IActionResult> GetAllUsers(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<UserReadDto>>()
            {
                Success = true,
                Data = await _userComponent.GetAllAsync(input),
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


    [HttpPost("get-user-with-filters")]
    public async Task<IActionResult> GetUserByFilter(UserFilterDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<UserReadDto>()
            {
                Success = true,
                Data = await _userComponent.GetUsersByFiltersAsync(input),
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




    [HttpGet("get-user-by-id/{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<UserReadDto>()
            {
                Success = true,
                Data = await _userComponent.GetByCodeAsync(id),
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



    [HttpGet("get-all-users-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _userComponent.GetAllSelectList();
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

    [HttpPost("create-user")]
    public async Task<IActionResult> Create([FromBody] UserCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<UserReadDto>()
            {
                Success = true,
                Data = await _userComponent.CreateAsync(input),
                Message = "User created successfully.",
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

    [HttpPut("update-user")]
    public async Task<IActionResult> Update([FromBody] UserUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<UserReadDto>()
            {
                Success = true,
                Data = await _userComponent.UpdateAsync(input),
                Message = "User updated successfully.",
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

    [HttpDelete("delete-user/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        { 
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _userComponent.DeleteAsync(code),
                Message = "User deleted successfully.",
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
