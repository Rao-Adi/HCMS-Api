using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Controllers.HCMS.ESS;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSDocumentTrainingAuthorizationController : Controller
{

    private readonly DocumentTrainingAuthorizationComponent _component;
    private readonly ILogger<UtilitiesController> _logger;

    public DMSDocumentTrainingAuthorizationController(DocumentTrainingAuthorizationComponent component,
        ILogger<UtilitiesController> logger)
    {
        _component = component;
        _logger = logger;
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] DocumentTrainingAuthorizationCreateDto input)
    { 
        try
        {
            return Ok(new HttpApiResponse<DocumentTrainingAuthorizationReadDto>()
            {
                Success = true,
                Data = await _component.CreateAsync(input),
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

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] DocumentTrainingAuthorizationUpdateDto input)
    { 
        try
        {
            return Ok(new HttpApiResponse<DocumentTrainingAuthorizationReadDto>()
            {
                Success = true,
                Data = await _component.UpdateAsync(input),
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

    [HttpDelete("Delete")]
    public async Task<IActionResult> Delete(int id)
    { 
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _component.DeleteAsync(id),
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

    [HttpPost("get-all-document-training-authorizations")]
    public async Task<IActionResult> GetAll([FromBody] TableFiltersDto input)
    { 
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<DocumentTrainingAuthorizationReadDto>>()
            {
                Success = true,
                Data = await _component.GetAllAsync(input),
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

    [HttpGet("get-by-id")]
    public async Task<IActionResult> GetById(int id)
    { 
        try
        {
            return Ok(new HttpApiResponse<DocumentTrainingAuthorizationReadDto>()
            {
                Success = true,
                Data = await _component.GetByIdAsync(id),
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
}
