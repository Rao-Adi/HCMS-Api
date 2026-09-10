using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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

    // Mirrors the "Mon DD, YYYY" date convention already used for date columns in the
    // exported data (see DocumentRequestComponent.ExportMyInboxRequestsAsync).
    private static string BuildInboxExportFileName(string? requestStatus)
    {
        var titlePart = requestStatus?.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "Requests Approved by Me",
            "REJECTED" => "Requests Rejected by Me",
            "REVERTED" => "Requests Reverted by Me",
            "PENDING" => "Pending Requests",
            _ => "My Inbox Requests"
        };

        var datePart = DateTime.Now.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

        return $"{titlePart} - ({datePart}).xlsx";
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

            string fileName = BuildInboxExportFileName(input.RequestStatus);

            // Without this, Angular cannot read the filename off Content-Disposition
            // (see DownloadDraftDocument below, which needs the same header for the same reason).
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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


    [HttpGet("get-my-document-requests-for-approval-count")]
    public async Task<IActionResult> GetMyRequestsPendingApprovalCount()
    {
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetMyRequestsPendingApprovalCountAsync(),
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
    public async Task<IActionResult> GetDocumentObservationDetails(int requestId, string entityType,string? decision)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<DocumentRequestDetailsDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetDocumentObservationDetailsAsync(requestId, entityType, decision),
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
    public async Task<IActionResult> GetWorkflowDetail(int requestId, string entityType, string? decision)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<DocumentRequestDetailsDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetWorkflowDetailsAsync(requestId, entityType, decision),
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



    [HttpGet("get-document-revision-history")]
    public async Task<IActionResult> GetDocumentRevisionHistory(int documentId)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<RevisionHistoryItemDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetDocumentRevisionHistoryAsync(documentId),
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

    [HttpPost("get-my-total-request")]
    public async Task<IActionResult> GetMyTotalRequests(GetDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentRequestReadDto>>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetMyTotalRequestsAsync(input),
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
    [HttpGet("get-my-total-request-count")]
    public async Task<IActionResult> GetMyTotalRequestCount()
    {
        try
        {
            return Ok(new HttpApiResponse<int>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetMyTotalRequestCountAsync(),
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

    [HttpPost("export-my-total-requests")]
    public async Task<IActionResult> ExportMyTotalRequests(GetDocumentDto input)
    {
        try
        {
            var fileBytes = await _documentRequestComponent.ExportMyTotalRequestsAsync(input);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                return NotFound(new HttpApiResponse<object>
                {
                    Success = false,
                    Message = "No data available to export.",
                    Code = 404
                });
            }

            var fileName = $"My-Approved-Rejected-Requests-({DateTime.Now.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)}).xlsx";

            // Without this, Angular cannot read the filename off Content-Disposition.
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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

            string? filePath = null;
            string? extension = null;
            if (!string.IsNullOrEmpty(request.DraftFileUrl))
            {
                var relativePath = request.DraftFileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var candidatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
                if (System.IO.File.Exists(candidatePath))
                {
                    filePath = candidatePath;
                    extension = Path.GetExtension(candidatePath).ToLowerInvariant();
                }
            }

            byte[] fileBytes;
            string contentType;
            string fileName;

            // Mirrors DMSDocumentController.DownloadDraftDocument: merges the content into the
            // DocumentType's Word template (DocumentRequestComponent.MergeDocumentRequestTemplateAsync)
            // whenever there's either a docx file or saved rich-text content to work with -- not
            // gated on a physical file being present, so a request drafted purely via the rich
            // text editor (no upload at all) can still be downloaded with the approver's template
            // applied. Never lets a merge problem (no template configured yet, template file
            // missing, etc.) block the download itself -- falls back to the raw uploaded file if
            // one exists, matching the previous behavior for that case.
            bool canAttemptMerge = extension == ".doc" || extension == ".docx" || filePath == null;
            if (canAttemptMerge)
            {
                try
                {
                    Stream? contentFileStream = filePath != null
                        ? new FileStream(filePath, FileMode.Open, FileAccess.Read)
                        : null;
                    await using (contentFileStream)
                    {
                        fileBytes = await _documentRequestComponent.MergeDocumentRequestTemplateAsync(id, contentFileStream);
                    }
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    // A request drafted purely via the rich text editor (no file ever uploaded)
                    // has no filePath to name the download after -- this used to fall straight
                    // to request.RequestNumber (e.g. "DR-297.docx"), which is meaningless to the
                    // approver compared to the request's own Document Name. Prefer that instead,
                    // sanitized since it's free-text user input rather than an already-valid
                    // filesystem name the way filePath/RequestNumber are.
                    var sanitizedDocumentName = SanitizeForFileName(request.DocumentName);
                    fileName = Path.GetFileNameWithoutExtension(filePath ?? sanitizedDocumentName ?? request.RequestNumber ?? "Document") + ".docx";
                }
                catch (Exception mergeEx)
                {
                    _logger.LogWarning(mergeEx, "Template merge failed for Document Request {RequestId}; falling back to the raw uploaded file.", id);
                    if (filePath == null)
                    {
                        return NotFound(new HttpApiResponse<object>()
                        {
                            Success = false,
                            Data = new { },
                            Message = "No content available for this request to download.",
                            Code = 404
                        });
                    }
                    fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    fileName = Path.GetFileName(filePath);
                }
            }
            else if (filePath != null)
            {
                fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(filePath, out contentType))
                {
                    contentType = "application/octet-stream";
                }
                fileName = Path.GetFileName(filePath);
            }
            else
            {
                return NotFound(new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "This request does not have a physical draft file to download.",
                    Code = 404
                });
            }

            // Important for frontend reading of the File Name
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            // This same URL (requestId never changes) can legitimately return different bytes
            // and a different filename on a later call -- e.g. after a merge-vs-raw-file
            // fallback path changes, or the draft's content is edited -- so the browser must
            // never reuse a cached response for it.
            Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");

            return File(fileBytes, contentType, fileName);
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

    // Strips characters that aren't valid in a filesystem file name -- unlike filePath (already
    // a real path) or RequestNumber (a generated code), DocumentName is free-text the user typed
    // and could contain "/", ":", "?", etc. Returns null (not empty string) when nothing usable
    // is left, so callers can still fall through to their own further fallback.
    private static string? SanitizeForFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (System.Array.IndexOf(invalidChars, c) < 0)
                sb.Append(c);
        }

        var result = sb.ToString().Trim();
        return result.Length > 0 ? result : null;
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
            var success = await _documentRequestComponent.TakeWorkflowActionAsync(input);

            // Mirrors the decision switch in TakeWorkflowActionAsync -- kept here (not
            // returned from that method) so the message is one source of truth the frontend
            // just displays, instead of each tab (my-approval-request.ts /
            // my-approval-document.ts) maintaining its own action-to-text mapping that can
            // drift out of sync with what actually happened.
            string message = input.Action?.Trim().ToUpperInvariant() switch
            {
                "APPROVED" => "Document approved successfully.",
                "REJECTED" => "Document rejected successfully.",
                "REWORKED" => "Document sent for rework successfully.",
                "COMMENT" => "Comment added successfully.",
                _ => "Action taken successfully."
            };

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = success,
                Message = message,
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

     

    [HttpPost("submit-draft-document-request")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitDocumentRequest([FromForm] SubmitDocumentRequestDto input)
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

    // UC-22: Revision-only counterpart to submit-draft-document-request. Submits an existing
    // Revision draft (DocumentRequestTypeCode == "Revision") and starts its workflow.
    [HttpPost("submit-revision-document-request")]
    public async Task<IActionResult> SubmitRevisionDocumentRequest(SubmitRevisionDocumentRequestDto input)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentRequestComponent.SubmitRevisionDocumentRequestAsync(input),
                Message = "Revision Request submitted successfully.",
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

    // UC-22: Revision-only counterpart to create-and-submit-document-request. Requires
    // ParentDocumentId and validates content differs from that document before starting the workflow.
    [HttpPost("create-and-submit-revision-document-request")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateAndSubmitRevisionDocumentRequest([FromForm] DraftDocumentRequestDto input)
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
                Data = await _documentRequestComponent.CreateAndSubmitRevisionDocumentRequestAsync(input),
                Message = "Revision Request submitted successfully.",
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


    //[HttpPost("get-effective-documents-for-revision")]
    //public async Task<IActionResult> GetEffectiveDocumentsForRevision(GetDocumentDto input)
    //{
    //    try
    //    {
    //        return Ok(new HttpApiResponse<PaginationResult<EffectiveDocumentDetailsDto>>()
    //        {
    //            Success = true,
    //            Data = await _documentRequestComponent.GetEffectiveDocumentsForRevisionAsync(input),
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

    [HttpGet("get-draft-documents-count")]
    public async Task<IActionResult> GetDraftDocumentCount()
    {
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _documentRequestComponent.GetDraftDocumentCountAsync(),
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


    [HttpGet("get-request-created-by-user-list")]
    public async Task<IActionResult> GetRequestCreatedByUserList()
    {
        try
        {
            var selectList = await _documentRequestComponent.GetRequestCreatedByUserListAsync();
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
