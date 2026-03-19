using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Net;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSTemplateController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly TemplateComponent _templateComponent;

    public DMSTemplateController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     TemplateComponent templateComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _templateComponent = templateComponent;
    }

    [HttpPost("get-all-template")]
    public async Task<IActionResult> GetAllTemplates(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<TemplateReadDto>>()
            {
                Success = true,
                Data = await _templateComponent.GetAllAsync(input),
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



    [HttpGet("get-template-by-document-type/{code}")]
    public async Task<IActionResult> GetTemplateById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<TemplateReadDto>()
            {
                Success = true,
                Data = await _templateComponent.GetByCodeAsync(code),
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


    [HttpGet("download-template/{code}")] 
    public async Task<IActionResult> DownloadTemplate(string code)
    {
        try
        {
            var template = await _templateComponent.GetByCodeAsync(code);
            if (string.IsNullOrEmpty(template.TemplateFileUrl))
            {
                return NotFound(new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "This template does not have a physical file to download.",
                    Code = 404
                });
            }

            var relativePath = template.TemplateFileUrl.TrimStart('/');
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



    [HttpPost("create-template")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] TemplateCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<TemplateReadDto>()
            {
                Success = true,
                Data = await _templateComponent.CreateAsync(input),
                Message = "Template created successfully.",
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

    [HttpPut("update-template")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update([FromForm] TemplateUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<TemplateReadDto>()
            {
                Success = true,
                Data = await _templateComponent.UpdateAsync(input),
                Message = "Template updated successfully.",
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

    [HttpDelete("delete-template/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _templateComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Template not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _templateComponent.DeleteAsync(code),
                Message = "Template deleted successfully.",
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
