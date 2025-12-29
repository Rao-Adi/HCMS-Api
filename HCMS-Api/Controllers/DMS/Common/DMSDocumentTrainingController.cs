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
public class DMSDocumentTrainingController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly DocumentTrainingComponent _documentTrainingComponent;

    public DMSDocumentTrainingController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     DocumentTrainingComponent documentTrainingComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _documentTrainingComponent = documentTrainingComponent;
    }

    [HttpPost("get-all-document-training")]
    public async Task<IActionResult> GetAllDocumentTraining(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentTraining>>()
            {
                Success = true,
                Data = await _documentTrainingComponent.GetAllAsync(input),
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


    [HttpGet("get-all-document-training-list")]
    public async Task<IActionResult> GetAllSelectList()
    {
        try
        {
            var selectList = await _documentTrainingComponent.GetAllSelectList();
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


    [HttpGet("get-document-training-by-code/{code}")]
    public async Task<IActionResult> GetDocumentTrainingById(string code)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentTraining>()
            {
                Success = true,
                Data = await _documentTrainingComponent.GetByCodeAsync(code),
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


    [HttpPost("create-document-training")]
    public async Task<IActionResult> Create([FromBody] DocumentTraining input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        { 
            return Ok(new HttpApiResponse<DocumentTraining>()
            {
                Success = true,
                Data = await _documentTrainingComponent.CreateAsync(input),
                Message = "Document Training created successfully.",
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

    [HttpPut("update-document-training")]
    public async Task<IActionResult> Update([FromBody] DocumentTraining input)
    {
        try
        {
            return Ok(new HttpApiResponse<DocumentTraining>()
            {
                Success = true,
                Data = await _documentTrainingComponent.UpdateAsync(input),
                Message = "Document Training updated successfully.",
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

    [HttpDelete("delete-document-training/{code}")]
    public async Task<IActionResult> DeleteAsync(string code)
    {
        try
        {
            var existingRecord = await _documentTrainingComponent.GetByCodeAsync(code);
            if (existingRecord is null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new HttpApiResponse<object>()
                {
                    Success = false,
                    Data = new { },
                    Message = "Document Training not found",
                    Code = 404
                });
            }

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _documentTrainingComponent.DeleteAsync(code),
                Message = "Document Training deleted successfully.",
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
