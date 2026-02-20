using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSDocumentRequestController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly DocumentRequestComponent _documentRequestComponent;

    public DMSDocumentRequestController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     DocumentRequestComponent distributionListComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _documentRequestComponent = distributionListComponent;
    }

    [HttpPost("get-all-document-request")]
    public async Task<IActionResult> GetAllDocumentRequest(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentRequestReadDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetAllAsync(input),
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

    [HttpPost("get-my-pending-request")]
    public async Task<IActionResult> GetMyInboxRequests(GetPendingRequestDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<DocumentRequestReadDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetMyInboxRequestsAsync(input),
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


    [HttpPost("get-my-request-pending-approval")]
    public async Task<IActionResult> GetMyRequestsPendingApproval(MyRequestFilterDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<MyRequestPendingDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetMyRequestsPendingApprovalAsync(input),
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


    [HttpGet("get-request-details")]
    public async Task<IActionResult> GetRequestDetails(int companyId, int requestId, string? initiator)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentRequestDetailsDto>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetRequestDetailsAsync(companyId, requestId, initiator),
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


    //[HttpGet("get-my-draft-request")]
    //public async Task<IActionResult> GetMyDraftRequests(int companyId, string userId)
    //{
    //    try
    //    {
    //        return Ok(new HttpApiResponse<List<DocumentRequestReadDto>>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.GetDraftRequestsAsync(companyId, userId),
    //            Message = "Success",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}

    [HttpGet("get-all-document-request-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _documentRequestComponent.GetAllSelectList();
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


    [HttpGet("get-document-request-by-code/{code}")]
    public async Task<IActionResult> GetDocumentRequestById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentRequestReadDto>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetByCodeAsync(code),
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


    [HttpPost("draft-document-request")]
    public async Task<IActionResult> DraftDocumentRequest([FromBody] DraftDocumentRequestDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<long>()
            {
                Success = true,
                Data = await _documentRequestComponent.DraftDocumentRequestAsync(input),
                Message = "Document Request created successfully.",
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

    [HttpPost("update-draft-document-request")]
    public async Task<IActionResult> UpdateDraftDocumentRequest([FromBody] UpdateDraftRequestDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<long>()
            {
                Success = true,
                Data = await _documentRequestComponent.UpdateDraftDocumentRequestAsync(input),
                Message = "Document Request created successfully.",
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

    [HttpPost("take-workflow-action")]
    public async Task<IActionResult> TakeWorkflowAction([FromBody] ApproveRejectWorkflowStepDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentRequestComponent.TakeWorkflowActionAsync(input),
                Message = "Action taken successfully.",
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



    //[HttpPost("approve-document-request")]
    //public async Task<IActionResult> ApproveDraftDocumentRequest([FromBody] ApproveRejectWorkflowStepDto input)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        // Return validation errors
    //        return BadRequest(ModelState);
    //    }

    //    try
    //    {
    //        return Ok(new HttpApiResponse<bool>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.ApproveWorkflowStepAsync(input),
    //            Message = "Document Request created successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}



    //[HttpPost("reject-document-request")]
    //public async Task<IActionResult> RejectDraftDocumentRequest([FromBody] ApproveRejectWorkflowStepDto input)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        // Return validation errors
    //        return BadRequest(ModelState);
    //    }

    //    try
    //    {
    //        return Ok(new HttpApiResponse<bool>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.RejectWorkflowStepAsync(input),
    //            Message = "Document Request created successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}





    [HttpPost("submit-document-request")]
    public async Task<IActionResult> SubmitDocumentRequest(SubmitDocumentRequestDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentRequestComponent.SubmitDocumentRequestAsync(input),
                Message = "Document Request created successfully.",
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


    //[HttpPost("create-document-request")]
    //public async Task<IActionResult> Create([FromBody] CreateDocumentRequest2Dto input)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        // Return validation errors
    //        return BadRequest(ModelState);
    //    }

    //    try
    //    {
    //        return Ok(new HttpApiResponse<long>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.CreateDocumentRequestAsync(input),
    //            Message = "Document Request created successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}

    [HttpPut("update-document-request")]
    public async Task<IActionResult> Update([FromBody] DocumentRequestUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentRequestReadDto>()
            {
                Success = true,
                Data = await _documentRequestComponent.UpdateAsync(input),
                Message = "Document Request updated successfully.",
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

    [HttpDelete("delete-document-request/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _documentRequestComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Document Request not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentRequestComponent.DeleteAsync(code),
                Message = "Document Request deleted successfully.",
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


    //[HttpPost("submit-document-request")]
    //public async Task<IActionResult> SubmitDocumentRequest([FromBody] DocumentRequestCreateDto input)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        // Return validation errors
    //        return BadRequest(ModelState);
    //    }

    //    try
    //    {
    //        return Ok(new HttpApiResponse<long>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.SubmitDocumentRequestAsync(input),
    //            Message = "Document Request created successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}


    //[HttpPost]
    //public async Task<IActionResult> Create(CreateDocumentRequestDto dto)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        // Return validation errors
    //        return BadRequest(ModelState);
    //    }

    //    try
    //    {
    //        return Ok(new HttpApiResponse<long>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.CreateRequestAsync(dto),
    //            Message = "Document Request created successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }


    //}


    //[HttpPost("request/{id}/submit")]
    //public async Task<IActionResult> Submit(long id, long requestId)
    //{
    //    try
    //    {
    //        await _documentRequestComponent.SubmitRequestAsync(id, requestId);
    //        return Ok(new HttpApiResponse<string[]>()
    //        {
    //            Success = true,
    //            Data = null,
    //            Message = "Document Request Submitted  successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}

    //[HttpPost("approve")]
    //public async Task<IActionResult> Approve(ApprovalDto dto)
    //{ 
    //    try
    //    {
    //        await _documentRequestComponent.ApproveAsync(dto.WorkflowExecutionId, dto.Observation);
    //        return Ok(new HttpApiResponse<string[]>()
    //        {
    //            Success = true,
    //            Data = null,
    //            Message = "Document Request Approved successfully.",
    //            Code = 200
    //        });
    //    }
    //    catch (CustomException ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
    //        return StatusCode((int)statusCode, HttpResponseCatchReturn.ReturnException(ex, new object { }));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, ex.Message);
    //        var response = HttpResponseCatchReturn.ReturnException(ex, new { });
    //        return StatusCode(response.Code, response);
    //    }
    //}
}
