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
public class DMSDocumentReviewPolicyController : Controller
{
    private readonly DocumentReviewPolicyComponent _component;
    private readonly ILogger<UtilitiesController> _logger;

    public DMSDocumentReviewPolicyController(DocumentReviewPolicyComponent component,
        ILogger<UtilitiesController> logger)
    {
        _component = component;
        _logger = logger;
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] DocumentReviewPolicyCreateDto input)
    {  
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _component.CreateAsync(input),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] DocumentReviewPolicyUpdateDto input)
    { 
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _component.UpdateAsync(input),
                Message = "Success",
                Code = 200
            });
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
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _component.DeleteAsync(id),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpPost("get-all-document-review-policies")]
    public async Task<IActionResult> GetAll([FromBody] TableFiltersDto input)
    { 
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _component.GetAllAsync(input),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpGet("get-document-review-policy-by-id")]
    public async Task<IActionResult> GetById(int id)
    { 
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _component.GetByIdAsync(id),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    [HttpGet("get-document-review-policy-by-documenttypecode")]
    public async Task<IActionResult> GetByDocumentTypeCode(string documentTypeCode)
    { 
        try
        {
            return Ok(new HttpApiResponse<dynamic>()
            {
                Success = true,
                Data = await _component.GetByDocumentTypeAsync(documentTypeCode),
                Message = "Success",
                Code = 200
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }
}
