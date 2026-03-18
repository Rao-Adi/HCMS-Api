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
public class DMSWorkflowPolicyController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly WorkflowPolicyComponent _workflowPolicyComponent;

    public DMSWorkflowPolicyController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     WorkflowPolicyComponent workflowPolicyComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _workflowPolicyComponent = workflowPolicyComponent;
    }

    [HttpPost("get-all-workflow-policy")]
    public async Task<IActionResult> GetAllTransferWorkflowPolicies(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<WorkflowPolicyReadDto>>()
            {
                Success = true,
                Data = await _workflowPolicyComponent.GetAllAsync(input),
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



    [HttpGet("get-workflow-policy-by-code/{code}")]
    public async Task<IActionResult> GetWorkflowPolicyById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<WorkflowPolicyReadDto>()
            {
                Success = true,
                Data = await _workflowPolicyComponent.GetByCodeAsync(code),
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


    [HttpPost("create-workflow-policy")]
    public async Task<IActionResult> Create([FromBody] WorkflowPolicyCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<WorkflowPolicyReadDto>()
            {
                Success = true,
                Data = await _workflowPolicyComponent.CreateAsync(input),
                Message = "Audit Log created successfully.",
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

    [HttpPut("update-workflow-policy")]
    public async Task<IActionResult> Update([FromBody] WorkflowPolicyUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<WorkflowPolicyReadDto>()
            {
                Success = true,
                Data = await _workflowPolicyComponent.UpdateAsync(input),
                Message = "Audit Log updated successfully.",
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

    [HttpDelete("delete-workflow-policy/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _workflowPolicyComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Audit Log not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _workflowPolicyComponent.DeleteAsync(code),
                Message = "Audit Log deleted successfully.",
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
