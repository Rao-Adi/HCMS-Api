﻿using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.IO;
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

    [HttpPost("get-my-pending-document-request")]
    public async Task<IActionResult> GetMyInboxRequests(GetPendingRequestDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentRequestReadDto>>()
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

    [HttpPost("export-my-pending-document-request")]
    public async Task<IActionResult> ExportMyInboxRequestsAsync(GetPendingRequestDto input)
    {
        try
        {
            var fileBytes = await _documentRequestComponent.ExportMyInboxRequestsAsync(input);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                return NotFound(new HttpApiResponse<object>
                {
                    Success = false,
                    Message = "No data available to export.",
                    Code = 404
                });
            }

            string fileName = $"Pending_Requests_{DateTime.Now:yyyyMMddHHmmss}.csv";
            return File(fileBytes, "text/csv", fileName);
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

    [HttpGet("get-my-request-counts")]
    public async Task<IActionResult> GetMyRequestCounts()
    {
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetMyRequestCountsAsync(),
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



    [HttpPost("get-my-document-requests-for-approval")]
    public async Task<IActionResult> GetMyDocumentRequestsForApproval(MyRequestFilterDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<MyRequestPendingDto>>()
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


    [HttpGet("get-document-observation-details")]
    public async Task<IActionResult> GetDocumentObservationDetails(int requestId, string entityType)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<DocumentRequestDetailsDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetDocumentObservationDetailsAsync(requestId, entityType),
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



    [HttpGet("get-workflow-details")]
    public async Task<IActionResult> GetWorkflowDetail(int requestId, string entityType)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<DocumentRequestDetailsDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetWorkflowDetailsAsync(requestId, entityType),
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

    [HttpPost("get-my-draft-request")]
    public async Task<IActionResult> GetMyDraftRequests(GetDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentRequestReadDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetDraftDocumentRequestAsync(input),
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

    [HttpGet("get-all-document-request-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _documentRequestComponent.GetAllSelectList();
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


    [HttpGet("get-document-request-by-id/{id}")]
    public async Task<IActionResult> GetDocumentRequestById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentRequestReadDto>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetByIdAsync(id),
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

    [HttpGet("download-draft-document/{id}")]
    public async Task<IActionResult> DownloadDraftDocument(int id)
    {
        try
        {
            var request = await _documentRequestComponent.GetByIdAsync(id);
            if (string.IsNullOrEmpty(request.DraftFileUrl))
            {
                return NotFound(new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "This request does not have a physical draft file to download.",
                    Code = 404
                });
            }

            var relativePath = request.DraftFileUrl.TrimStart('/');
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Physical file does not exist on the server.",
                    Code = 404
                });
            }

            var memory = new MemoryStream();
            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream"; 
            }

            var fileName = Path.GetFileName(filePath);

            // Important for frontend reading of the File Name
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");

            return File(memory, contentType, fileName);
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

    [HttpPost("create-draft-document-request")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DraftDocumentRequest([FromForm] DraftDocumentRequestDto input)
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
                Data = await _documentRequestComponent.CreateDraftDocumentRequestAsync(input),
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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateDraftDocumentRequest([FromForm] UpdateDraftRequestDto input)
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





    [HttpPost("submit-draft-document-request")]
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
                Data = await _documentRequestComponent.SubmitDraftDocumentRequestAsync(input),
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

    [HttpPost("create-and-submit-document-request")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateAndSubmitDocumentRequest([FromForm] DraftDocumentRequestDto input)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<long>()
            {
                Success = true,
                Data = await _documentRequestComponent.CreateAndSubmitDocumentRequestAsync(input),
                Message = "Document Request submitted successfully.",
                Code = 201
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


    [HttpGet("get-effective-documents-details-by-id")]
    public async Task<IActionResult> GetEffectiveDocumentDetailsForRevision(int documentId)
    {
        try
        {
            return Ok(new HttpApiResponse<EffectiveDocumentDetailsDto>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetEffectiveDocumentDetailsForRevisionAsync(documentId),
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


    [HttpPost("get-effective-documents-for-revision")]
    public async Task<IActionResult> GetEffectiveDocumentsForRevision(GetDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<EffectiveDocumentDetailsDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetEffectiveDocumentsForRevisionAsync(input),
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

    //[HttpPut("update-document-request")]
    //public async Task<IActionResult> Update([FromBody] DocumentRequestUpdateDto input)
    //{
    //    try
    //    {
    //        return Ok(new HttpApiResponse<DocumentRequestReadDto>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.UpdateAsync(input),
    //            Message = "Document Request updated successfully.",
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

    //[HttpDelete("delete-document-request/{code}")]
    //public async Task<IActionResult> DeleteAsync(string code)
    //{
    //    try
    //    {
    //        return Ok(new HttpApiResponse<bool>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.DeleteAsync(code),
    //            Message = "Document Request deleted successfully.",
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
