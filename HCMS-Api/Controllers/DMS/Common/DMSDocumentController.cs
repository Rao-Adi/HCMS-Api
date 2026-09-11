using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSDocumentController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly DocumentComponent _documentComponent;

    public DMSDocumentController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     DocumentComponent documentComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _documentComponent = documentComponent;
    }

    [HttpPost("get-all-document")]
    public async Task<IActionResult> GetAllDocuments(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentReadDto>>()
            {
                Success = true,
                Data = await _documentComponent.GetAllAsync(input),
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

    [HttpGet("get-my-document-counts")]
    public async Task<IActionResult> GetMyDocumentCounts()
    {
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _documentComponent.GetMyDocumentCountsAsync(),
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

    // Every Document the current user has created, any status -- lets an Initiator see
    // everything they've created regardless of where it currently sits in the pipeline.
    [HttpPost("get-my-documents")]
    public async Task<IActionResult> GetMyDocuments(GetDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetMyDocumentsAsync(input),
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

    [HttpGet("get-my-documents-total-count")]
    public async Task<IActionResult> GetMyDocumentsTotalCount()
    {
        try
        {
            return Ok(new HttpApiResponse<int>()
            {
                Success = true,
                Data = await _documentComponent.GetMyDocumentsCountAsync(),
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

    [HttpPost("export-my-documents-list")]
    public async Task<IActionResult> ExportMyDocumentsList(GetDocumentDto input)
    {
        try
        {
            var fileBytes = await _documentComponent.ExportMyDocumentsListAsync(input);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                return NotFound(new HttpApiResponse<object>
                {
                    Success = false,
                    Message = "No data available to export.",
                    Code = 404
                });
            }

            var fileName = $"My Documents - ({DateTime.Now.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)}).xlsx";

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

    [HttpGet("get-all-document-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _documentComponent.GetAllSelectList();
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


    [HttpGet("get-document-by-code/{code}")]
    public async Task<IActionResult> GetDocumentById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentReadDto>()
            {
                Success = true,
                Data = await _documentComponent.GetByCodeAsync(code),
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


    // Prefills the Training Users table when starting a direct Revision/Obsoletion from an
    // existing document -- see DocumentComponent.GetDocumentTrainingAssignmentsByDocumentIdAsync.
    [HttpGet("get-document-training-assignments/{documentId}")]
    public async Task<IActionResult> GetDocumentTrainingAssignments(int documentId)
    {
        try
        {
            return Ok(new HttpApiResponse<List<DocumentTrainingAssignmentDto>>()
            {
                Success = true,
                Data = await _documentComponent.GetDocumentTrainingAssignmentsByDocumentIdAsync(documentId),
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


    [HttpPost("create-document")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] DocumentCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<DocumentReadDto>()
            {
                Success = true,
                Data = await _documentComponent.CreateAsync(input),
                Message = "Document created successfully.",
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


    [HttpPost("submit-document")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitDocument([FromForm] SubmitDocument input)
    {
        try
        {
            // Attributes/TrainingUsers travel as JSON-encoded strings inside the multipart form
            // (multipart is required here because DocumentFile can only travel that way, not as a
            // JSON body) -- ASP.NET Core's form binder can't bind a List<T> from a single JSON
            // string field, so we deserialize them ourselves instead of relying on model binding.
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (Request.Form.TryGetValue("attributes", out var attributesJson) && !string.IsNullOrWhiteSpace(attributesJson))
                input.Attributes = JsonSerializer.Deserialize<List<CreateDocumentAttributeValueDto>>(attributesJson!, jsonOptions) ?? new();

            if (Request.Form.TryGetValue("trainingusers", out var trainingUsersJson) && !string.IsNullOrWhiteSpace(trainingUsersJson))
                input.TrainingUsers = JsonSerializer.Deserialize<List<TraningUsers>>(trainingUsersJson!, jsonOptions) ?? new();

            if (Request.Form.TryGetValue("adhocapprovers", out var adHocApproversJson) && !string.IsNullOrWhiteSpace(adHocApproversJson))
                input.AdHocApprovers = JsonSerializer.Deserialize<List<AdHocApproverDto>>(adHocApproversJson!, jsonOptions) ?? new();

            if (Request.Form.TryGetValue("distributionlist", out var distributionListJson) && !string.IsNullOrWhiteSpace(distributionListJson))
                input.DistributionList = JsonSerializer.Deserialize<List<DistributionListCreateDto>>(distributionListJson!, jsonOptions) ?? new();

            if (Request.Form.TryGetValue("userids", out var userIdsJson) && !string.IsNullOrWhiteSpace(userIdsJson))
                input.UserIds = JsonSerializer.Deserialize<List<UserDistributionInputDto>>(userIdsJson!, jsonOptions) ?? new();

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.SubmitDocumentAsync(input),
                Message = "Document Created Successfully",
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


    [HttpPost("approve-document")]
    public async Task<IActionResult> ApproveDocument(ActionOnDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.ApproveDocumentAsync(input),
                Message = "Document Approved Successfully",
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


    [HttpPost("reject-document")]
    public async Task<IActionResult> RejectDocument(ActionOnDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.RejectDocumentAsync(input),
                Message = "Document Rejected Successfully",
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


    [HttpPost("send-back-for-rework")]
    public async Task<IActionResult> SendBackForRework(ActionOnDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.SendBackForReworkAsync(input),
                Message = "Document Reverted Successfully",
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


    [HttpPost("get-approved-request-for-document-creation")]
    public async Task<IActionResult> GetRequestsPendingFinalization(GetApprovedRequestForDocumentCreationDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetRequestsPendingFinalizationAsync(input),
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


    [HttpGet("get-draft-by-request/{requestId}")]
    public async Task<IActionResult> GetDraftDocumentByRequest(int requestId)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetDraftDocumentByRequestAsync(requestId),
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


    [HttpPost("get-document-by-status")]
    public async Task<IActionResult> GetMyInboxRequestsAsync(GetDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<AllDocumentDto>>()
            {
                Success = true,
                Data = await _documentComponent.GetDocumentByStatusAsync(input),
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


    [HttpGet("download-submitted-document-template/{id}")]
    public async Task<IActionResult> DownloadDraftDocument(int id)
    {
        try
        {
            var request = await _documentComponent.GetByIdAsync(id);

            string? filePath = null;
            string? extension = null;
            if (!string.IsNullOrEmpty(request.DocumentURL))
            {
                var relativePath = request.DocumentURL.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
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

            // The merge (DocumentComponent.MergeDocumentTemplateAsync) prefers this version's
            // saved rich-text HTML content over the uploaded file when both exist (see that
            // method's own comments), and works even with no uploaded file at all as long as HTML
            // content was saved -- so this is attempted whenever there's *either* a docx file or
            // HTML content to work with, not gated on a physical file being present. Never lets a
            // merge problem (no template configured yet, template file missing, HTML conversion
            // issue, etc.) block the download itself -- falls back to the raw uploaded file if one
            // exists, matching the previous behavior for that case.
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
                        fileBytes = await _documentComponent.MergeDocumentTemplateAsync(id, contentFileStream);
                    }
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    // A document with no uploaded file (content saved purely via the rich text
                    // editor) has no filePath to name the download after -- this used to fall
                    // straight to request.DocumentNumber (a generated code, e.g.
                    // "QA-QA-QA-SOP-018.docx"), meaningless compared to the document's own
                    // Title. Prefer that instead, sanitized since it's free-text the user typed
                    // rather than an already-valid filesystem name.
                    var sanitizedTitle = SanitizeForFileName(request.Title);
                    fileName = Path.GetFileNameWithoutExtension(filePath ?? sanitizedTitle ?? request.DocumentNumber ?? "Document") + ".docx";
                }
                catch (Exception mergeEx)
                {
                    _logger.LogWarning(mergeEx, "Template merge failed for Document {DocumentId}; falling back to the raw uploaded file.", id);
                    if (filePath == null)
                    {
                        return NotFound(new HttpApiResponse<object>()
                        {
                            Success = false,
                            Data = new { },
                            Message = "No content available for this document to download.",
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
            // This same URL (documentId never changes) can legitimately return different bytes
            // and a different filename on a later call -- e.g. after a merge-vs-raw-file
            // fallback path changes, or the document's content is edited -- so the browser must
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
    // a real path) or DocumentNumber (a generated code), Title is free-text the user typed and
    // could contain "/", ":", "?", etc. Returns null (not empty string) when nothing usable is
    // left, so callers can still fall through to their own further fallback.
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

    [HttpPost("get-pending-authorizations")]
    public async Task<IActionResult> GetPendingAuthorizationsAsync(GetPendingAuthorization input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetPendingAuthorizationsAsync(input),
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

    [HttpPost("get-pending-authorizations-counts")]
    public async Task<IActionResult> GetPendingAuthorizationCountsAsync(GetPendingAuthorization input)
    {
        try
        {
            return Ok(new HttpApiResponse<PendingAuthorizationCountsDto>()
            {
                Success = true,
                Data = await _documentComponent.GetPendingAuthorizationCountsAsync(input),
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

    [HttpPost("get-authorized-documents")]
    public async Task<IActionResult> GetAuthorizedDocumentsAsync(GetAuthorizedDocumentsDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetAuthorizedDocumentsAsync(input),
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

    [HttpPost("get-documents-pending-training")]
    public async Task<IActionResult> GetDocumentsPendingTrainingAcknowledgmentAsync(GetDocumentsPendingTrainingDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetDocumentsPendingTrainingAcknowledgmentAsync(input),
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

    [HttpPost("get-documents-pending-training-count")]
    public async Task<IActionResult> GetDocumentsPendingTrainingCountAsync(GetDocumentsPendingTrainingDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<int>()
            {
                Success = true,
                Data = await _documentComponent.GetDocumentsPendingTrainingCountAsync(input),
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

    [HttpPost("get-documents-pending-training-counts")]
    public async Task<IActionResult> GetDocumentsPendingTrainingCountsAsync(GetDocumentsPendingTrainingDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentsPendingTrainingCountsDto>()
            {
                Success = true,
                Data = await _documentComponent.GetDocumentsPendingTrainingCountsAsync(input),
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

    [HttpPost("get-documents-pending-approval")]
    public async Task<IActionResult> GetDocumentsPendingApprovalAsync(GetDocumentsPendingApprovalDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetDocumentsPendingApprovalAsync(input),
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



    [HttpPost("get-approved-effective-documents")]
    public async Task<IActionResult> GetApprovedEffectiveDocuments(GetApprovedDocumentsFilterDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<dynamic>>()
            {
                Success = true,
                Data = await _documentComponent.GetApprovedEffectiveDocumentsAsync(input),
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

    [HttpPost("authorize-document-post-training")]
    public async Task<IActionResult> AuthorizeDocumentPostTraining([FromBody] AuthorizeDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.AuthorizeDocumentPostTrainingAsync(input),
                Message = "Document successfully authorized and made effective.",
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

    [HttpPost("bulk-import-metadata")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> BulkImportMetadata([FromForm] IFormFile csvFile)
    {
        try
        {
            var file = csvFile ?? (Request.HasFormContentType ? Request.Form.Files.FirstOrDefault() : null);
            var results = await _documentComponent.BulkImportDocumentMetadataAsync(file);

            if (results != null && results.Any(r => r.Contains("Skipped") || r.StartsWith("Error")))
            {
                return BadRequest(new HttpApiResponse<List<string>>()
                {
                    Success = false,
                    Data = results,
                    Message = "Some records were skipped or failed during the bulk import process.",
                    Code = 400
                });
            }

            return Ok(new HttpApiResponse<List<string>>()
            {
                Success = true,
                Data = results,
                Message = "Bulk import metadata process completed.",
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

    [HttpPost("bulk-upload-files")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> BulkUploadFiles([FromForm] List<IFormFile> files)
    {
        try
        {
            return Ok(new HttpApiResponse<List<string>>()
            {
                Success = true,
                Data = await _documentComponent.BulkUploadDocumentFilesAsync(files),
                Message = "Bulk file upload process completed.",
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

    [HttpGet("download-bulk-upload-template")]
    public async Task<IActionResult> ExportMyInboxRequestsAsync()
    {
        try
        {
            var relativePath = "template/DMS_BulkUpload_Template.xlsx";
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

            // 1. Dynamically determine the content type based on the file extension (Removes hardcoding)
            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream"; // Default fallback if type is unknown
            }

            var fileName = Path.GetFileName(filePath);

            // 2. CRUCIAL FIX: Expose the Content-Disposition header to the frontend
            // Without this line, Angular is completely blocked from reading the filename and extension!
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



    // Mirrors DMSDocumentRequestController.BuildInboxExportFileName, just for the Documents
    // side of "My Approvals" instead of Requests.
    private static string BuildDocumentExportFileName(string? requestStatus)
    {
        var titlePart = requestStatus?.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "Documents Approved by Me",
            "REJECTED" => "Documents Rejected by Me",
            "REVERTED" => "Documents Reverted by Me",
            "PENDING" => "Pending Documents",
            _ => "My Document Approvals"
        };

        var datePart = DateTime.Now.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

        return $"{titlePart} - ({datePart}).xlsx";
    }

    [HttpPost("export-my-documents")]
    public async Task<IActionResult> ExportMyInboxRequestsAsync(GetDocumentDto input)
    {
        try
        {
            var fileBytes = await _documentComponent.ExportMyDocumentsAsync(input);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                return NotFound(new HttpApiResponse<object>
                {
                    Success = false,
                    Message = "No data available to export.",
                    Code = 404
                });
            }

            string fileName = BuildDocumentExportFileName(input.RequestStatus);

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


    [HttpPost("get-effective-documents-for-revision")]
    public async Task<IActionResult> GetEffectiveDocumentsForRevision(GetDocumentDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<EffectiveDocumentDetailsDto>>()
            {
                Success = true,
                Data = await _documentComponent.GetEffectiveDocumentsForRevisionAsync(input),
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

    //[HttpPut("update-document")]
    //public async Task<IActionResult> Update([FromForm] DocumentUpdateDto input)
    //{
    //    try
    //    {
    //        return Ok(new HttpApiResponse<DocumentReadDto>()
    //        {
    //            Success = true,
    //            Data = await _documentComponent.UpdateAsync(input),
    //            Message = "Document updated successfully.",
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

    //[HttpDelete("delete-document/{code}")]
    //public async Task<IActionResult> DeleteAsync(string code)
    //{
    //    try
    //    {
    //        var existingRecord = await _documentComponent.GetByCodeAsync(code);
    //        if (existingRecord is null)
    //        {
    //            return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
    //            {
    //                Success = false,
    //                Data = new { },
    //                Message = "Document not found",
    //                Code = 404
    //            });
    //        }

    //        return Ok(new HttpApiResponse<bool>()
    //        {
    //            Success = true,
    //            Data = await _documentComponent.DeleteAsync(code),
    //            Message = "Document deleted successfully.",
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
