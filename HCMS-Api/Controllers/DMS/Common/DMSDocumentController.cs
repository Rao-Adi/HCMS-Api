using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
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
            if (string.IsNullOrEmpty(request.DocumentURL))
            {
                return NotFound(new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "This request does not have a physical draft file to download.",
                    Code = 404
                });
            }

            var relativePath = request.DocumentURL.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
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

            string fileName = $"Document_{DateTime.Now:yyyyMMddHHmmss}.csv";
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
