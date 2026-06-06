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
    public async Task<IActionResult> SubmitDocument(SubmitDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.SubmitDocumentAsync(input),
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


    [HttpPost("approve-document")]
    public async Task<IActionResult> ApproveDocument(ActionOnDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.ApproveDocumentAsync(input),
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


    [HttpPost("reject-document")]
    public async Task<IActionResult> RejectDocument(ActionOnDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.RejectDocumentAsync(input),
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


    [HttpPost("send-back-for-rework")]
    public async Task<IActionResult> SendBackForRework(ActionOnDocument input)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentComponent.SendBackForReworkAsync(input),
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

            var relativePath = request.DocumentURL.TrimStart('/');
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
    public async Task<IActionResult> GetDocumentsPendingApprovalAsync(TableFiltersDto input)
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
            return Ok(new HttpApiResponse<List<string>>()
            {
                Success = true,
                Data = await _documentComponent.BulkImportDocumentMetadataAsync(csvFile),
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
