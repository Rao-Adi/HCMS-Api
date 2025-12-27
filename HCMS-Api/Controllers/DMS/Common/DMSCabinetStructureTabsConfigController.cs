using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HCMS_Api.Controllers.DMS.Common;

public class DMSCabinetStructureTabsConfigController : Controller
{

    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly CabinetStructureTabsConfigComponent _divisionComponent;

    public DMSCabinetStructureTabsConfigController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     CabinetStructureTabsConfigComponent divisionComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _divisionComponent = divisionComponent;
    }

    [HttpPost("get-all-cabinet-tabs")]
    public async Task<IActionResult> GetAllCabinetStructureTabsConfigs(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<CabinetStructureTabsConfigReadDto>>()
            {
                Success = true,
                Data = await _divisionComponent.GetAllAsync(input),
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


    [HttpGet("get-all-cabinet-tabs-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _divisionComponent.GetAllSelectList();
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


    [HttpGet("get-cabinet-tab-by-id/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<CabinetStructureTabsConfigReadDto>()
            {
                Success = true,
                Data = await _divisionComponent.GetByIDAsync(id),
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

    [HttpPost("create-cabinet-tab")]
    public async Task<IActionResult> Create([FromBody] CabinetStructureTabsConfigCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            //var empIdStr = _utilities.GetEmployeeId(HttpContext, userId);
            return Ok(new HttpApiResponse<CabinetStructureTabsConfigReadDto>()
            {
                Success = true,
                Data = await _divisionComponent.CreateAsync(input),
                Message = "CabinetStructureTabsConfig created successfully.",
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

    [HttpPut("update-cabinet-tab")]
    public async Task<IActionResult> Update([FromBody] CabinetStructureTabsConfigUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<CabinetStructureTabsConfigReadDto>()
            {
                Success = true,
                Data = await _divisionComponent.UpdateAsync(input),
                Message = "CabinetStructureTabsConfig updated successfully.",
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

    [HttpDelete("delete-cabinet/{id}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        try
        {
            var existingRecord = await _divisionComponent.GetByIDAsync(id);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "CabinetStructureTabsConfig not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _divisionComponent.DeleteAsync(id),
                Message = "CabinetStructureTabsConfig deleted successfully.",
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
