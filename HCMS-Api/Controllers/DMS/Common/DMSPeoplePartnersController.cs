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
public class DMSPeoplePartnersController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly PeoplePartnersComponent _peoplePartnersComponent;

    public DMSPeoplePartnersController(
        Utilities utilities,
        IConfiguration configuration,
        ILogger<UtilitiesController> logger,
        ClientContextService clientContextService,
        PeoplePartnersComponent peoplePartnersComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _peoplePartnersComponent = peoplePartnersComponent;
    }

    [HttpPost("get-all-setups-detail")]
    public async Task<IActionResult> GetAllSetupsDetail(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetAllSetupsDetailAsync(input),
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

    [HttpPost("get-all-deptstr-master")]
    public async Task<IActionResult> GetAllDeptstrMaster(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetAllDeptstrMasterAsync(input),
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

    [HttpPost("get-all-deptstr-detail")]
    public async Task<IActionResult> GetAllDeptstrDetail(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetAllDeptstrDetailAsync(input),
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

    [HttpPost("get-all-emp-job-profile")]
    public async Task<IActionResult> GetAllEmpJobProfile(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetAllEmpJobProfileAsync(input),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpPost("get-all-employees")]
    public async Task<IActionResult> GetAllEmployees(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetAllEmployeesAsync(input),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpPost("get-employees-by-role/{roleId}")]
    public async Task<IActionResult> GetEmployeesByRole(int roleId, [FromBody] TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetEmployeesByRoleIdAsync(roleId, input),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpPost("create-employee")]
    public async Task<IActionResult> CreateEmployee([FromBody] EmployeeCreateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<int>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.CreateEmployeeAsync(input),
                Message = "Employee created successfully.",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpGet("get-all-roles")]
    public async Task<IActionResult> GetRoleList()
    {
        try
        {
            return Ok(new HttpApiResponse<IQueryable<SelectList2Dto>>()
            {
                Success = true,
                Data = await _peoplePartnersComponent.GetRoleListAsync(),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpGet("get-all-employee-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _peoplePartnersComponent.GetAllEmployeeList();
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
}