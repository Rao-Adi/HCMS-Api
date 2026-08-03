﻿﻿﻿using HCMS_Api.Common;
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
public class DMSTransferWorkflowPolicyController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly TransferWorkflowPolicyComponent _transferWorkflowPolicyComponent;

    public DMSTransferWorkflowPolicyController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     TransferWorkflowPolicyComponent transferWorkflowPolicyComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _transferWorkflowPolicyComponent = transferWorkflowPolicyComponent;
    }

    [HttpPost("get-all-transfer-workflow-policy")]
    public async Task<IActionResult> GetAllTransferWorkflowPolicies(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<TransferWorkflowPolicyReadDto>>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.GetAllAsync(input),
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



    [HttpGet("get-transfer-workflow-policy-by-code/{code}")]
    public async Task<IActionResult> GetTransferWorkflowPolicyById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<TransferWorkflowPolicyReadDto>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.GetByTransferWorkflowPolicyCodeAsync(code),
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


    [HttpPost("create-transfer-workflow-policy")]
    public async Task<IActionResult> Create([FromBody] TransferWorkflowPolicyCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<TransferWorkflowPolicyReadDto>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.CreateAsync(input),
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

    [HttpPut("update-transfer-workflow-policy")]
    public async Task<IActionResult> Update([FromBody] TransferWorkflowPolicyUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<TransferWorkflowPolicyReadDto>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.UpdateAsync(input),
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

    [HttpDelete("delete-transfer-workflow-policy/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _transferWorkflowPolicyComponent.GetByTransferWorkflowPolicyCodeAsync(code);
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
                Data = await _transferWorkflowPolicyComponent.DeleteAsync(code),
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

    [HttpPost("get-my-responsibility-transfers-approvals")]
    public async Task<IActionResult> GetMyResponsibilityTransfersApprovals([FromBody] GetMyResponsibilityTransfersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.GetMyResponsibilityTransfersApprovalsAsync(input),
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

    [HttpGet("get-my-responsibility-transfers-approvals-count")]
    public async Task<IActionResult> GetMyResponsibilityTransfersApprovalsCount()
    {
        try
        {
            return Ok(new HttpApiResponse<ResponsibilityTransferApprovalCountsDto>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.GetMyResponsibilityTransfersApprovalsCountAsync(),
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

    [HttpGet("get-my-submitted-responsibility-transfers-count")]
    public async Task<IActionResult> GetMySubmittedResponsibilityTransfersCount()
    {
        try
        {
            return Ok(new HttpApiResponse<ResponsibilityTransferApprovalCountsDto>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.GetMySubmittedResponsibilityTransfersCountAsync(),
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

    [HttpPost("get-my-submitted-responsibility-transfers")]
    public async Task<IActionResult> GetMySubmittedResponsibilityTransfers([FromBody] GetMyResponsibilityTransfersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.GetMySubmittedResponsibilityTransfersAsync(input),
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

    [HttpPost("take-action")]
    public async Task<IActionResult> TakeActionAsync([FromBody] ResponsibilityTransferActionDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _transferWorkflowPolicyComponent.TakeActionAsync(input),
                Message = "Action submitted successfully.",
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
