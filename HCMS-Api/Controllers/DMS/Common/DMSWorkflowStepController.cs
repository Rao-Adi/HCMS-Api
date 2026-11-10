﻿using HCMS_Api.Common;
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
public class DMSWorkflowStepController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly WorkflowStepComponent _workflowStepComponent;
    private readonly WorkflowReconcileComponent _workflowReconcileComponent;

    public DMSWorkflowStepController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     WorkflowStepComponent workflowStepComponent,
     WorkflowReconcileComponent workflowReconcileComponent)
    {
        _workflowReconcileComponent = workflowReconcileComponent;
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _workflowStepComponent = workflowStepComponent;
    }

    [HttpPost("get-all-workflow-step")]
    public async Task<IActionResult> GetAllWorkflowSetups(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<WorkflowStepReadDto>>()
            {
                Success = true,
                Data = await _workflowStepComponent.GetAllAsync(input),
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



    [HttpPost("get-workflow-step-by-document-code")]
    public async Task<IActionResult> GetWorkflowStepById(GetStepDefinitionFilterDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<List<WorkflowStepDefiniationReadDto>>()
            {
                Success = true,
                Data = await _workflowStepComponent.GetByDocumentTypeCodeAsync(input),
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

    [HttpPost("get-workflow-policy-by-document-code")]
    public async Task<IActionResult> GetWorkflowPolicyByDocumentCode(GetStepDefinitionFilterDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<List<WorkflowStepDefiniationReadDto>>()
            {
                Success = true,
                Data = await _workflowStepComponent.GetWorkflowPolicyDocumentTypeCodeAsync(input),
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

    [HttpGet("get-pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<PendingRequestDto>>()
            {
                Success = true,
                Data = await _workflowStepComponent.GetPendingApprovalsAsync(),
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
     
    [HttpPost("create-workflow-step")]
    public async Task<IActionResult> Create([FromBody] WorkFlowStepsFilterDto filters)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _workflowStepComponent.CreateWorkflowStepsByFilterAsync(filters);
            // Workflows already running on this policy follow its new list.
            await _workflowReconcileComponent.ReconcilePolicyAsync(created?.FirstOrDefault()?.WorkflowPolicyId ?? filters.WorkflowPolicyId);
            return Ok(new HttpApiResponse<List<WorkflowStepDefiniationReadDto>>()
            {
                Success = true,
                Data = created,
                Message = "Workflow Step created successfully.",
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

    [HttpPut("update-workflow-step")]
    public async Task<IActionResult> Update([FromBody] WorkflowStepUpdateDto input)
    {
        try
        {
            var policyOfStep = await _workflowReconcileComponent.GetPolicyIdOfStepAsync(Convert.ToString(input.Id) ?? "");
            var updated = await _workflowStepComponent.UpdateAsync(input);
            await _workflowReconcileComponent.ReconcilePolicyAsync(policyOfStep);
            return Ok(new HttpApiResponse<WorkflowStepReadDto>()
            {
                Success = true,
                Data = updated,
                Message = "Workflow Step updated successfully.",
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

    [HttpDelete("delete-workflow-step/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        { 
            var policyOfStep = await _workflowReconcileComponent.GetPolicyIdOfStepAsync(code);
            var deleted = await _workflowStepComponent.DeleteAsync(code);
            await _workflowReconcileComponent.ReconcilePolicyAsync(policyOfStep);
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = deleted,
                Message = "Workflow Step deleted successfully.",
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

    [HttpPost("update-approval-sequence")]
    public async Task<IActionResult> UpdateApprovalSequence([FromBody] UpdateApprovalSequenceDto input)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var sequence = await _workflowStepComponent.UpdateApprovalSequenceAsync(input);
            // Workflows already running on this policy follow its new list / order.
            await _workflowReconcileComponent.ReconcilePolicyAsync(input.WorkflowPolicyId);
            return Ok(new HttpApiResponse<List<WorkflowStepDefiniationReadDto>>()
            {
                Success = true,
                Data = sequence,
                Message = "Approval sequence updated successfully.",
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
