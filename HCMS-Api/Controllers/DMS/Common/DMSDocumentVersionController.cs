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
public class DMSDocumentVersionController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly DocumentVersionComponent _documentTypeComponent;

    public DMSDocumentVersionController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     DocumentVersionComponent divisionComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _documentTypeComponent = divisionComponent;
    }

    [HttpPost("get-all-document-versions")]
    public async Task<IActionResult> GetAllDocumentVersions(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentVersion>>()
            {
                Success = true,
                Data = await _documentTypeComponent.GetAllAsync(input),
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


    [HttpGet("get-all-document-version-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _documentTypeComponent.GetAllSelectList();
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


    [HttpGet("get-document-version-by-code/{code}")]
    public async Task<IActionResult> GetDocumentVersionById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentVersion>()
            {
                Success = true,
                Data = await _documentTypeComponent.GetByCodeAsync(code),
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

    [HttpPost("create-document-version")]
    public async Task<IActionResult> Create([FromBody] DocumentVersion input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        { 
            return Ok(new HttpApiResponse<DocumentVersion>()
            {
                Success = true,
                Data = await _documentTypeComponent.CreateAsync(input),
                Message = "Document Version created successfully.",
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

    [HttpPut("update-document-version")]
    public async Task<IActionResult> Update([FromBody] DocumentVersion input)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentVersion>()
            {
                Success = true,
                Data = await _documentTypeComponent.UpdateAsync(input),
                Message = "Document Version updated successfully.",
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

    [HttpDelete("delete-document-version/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _documentTypeComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Document Version not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentTypeComponent.DeleteAsync(code),
                Message = "Document Version deleted successfully.",
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
