using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSUncontrolledDocumentController : Controller
{
    private readonly ILogger<DMSUncontrolledDocumentController> _logger;
    private readonly UncontrolledDocumentComponent _uncontrolledDocumentComponent;

    public DMSUncontrolledDocumentController(
        ILogger<DMSUncontrolledDocumentController> logger,
        UncontrolledDocumentComponent uncontrolledDocumentComponent)
    {
        _logger = logger;
        _uncontrolledDocumentComponent = uncontrolledDocumentComponent;
    }

    [HttpPost("create-uncontrolled-document")]
    public async Task<IActionResult> Create([FromForm] UncontrolledDocumentCreateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<UncontrolledDocumentReadDto>()
            {
                Success = true,
                Data = await _uncontrolledDocumentComponent.CreateAsync(input),
                Message = "Uncontrolled Document uploaded successfully.",
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

    [HttpPost("review-uncontrolled-document")]
    public async Task<IActionResult> Review([FromForm] UncontrolledDocumentReviewDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<UncontrolledDocumentReadDto>()
            {
                Success = true,
                Data = await _uncontrolledDocumentComponent.ReviewAsync(input),
                Message = "Uncontrolled Document reviewed successfully.",
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

    [HttpPost("get-all-uncontrolled-documents")]
    public async Task<IActionResult> GetAll(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<UncontrolledDocumentReadDto>>()
            {
                Success = true,
                Data = await _uncontrolledDocumentComponent.GetAllAsync(input),
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

    [HttpGet("get-uncontrolled-document-by-id/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<UncontrolledDocumentReadDto>()
            {
                Success = true,
                Data = await _uncontrolledDocumentComponent.GetByIdAsync(id),
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

    [HttpGet("get-uncontrolled-document-history/{id}")]
    public async Task<IActionResult> GetHistory(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<IEnumerable<UncontrolledDocumentHistoryDto>>()
            {
                Success = true,
                Data = await _uncontrolledDocumentComponent.GetHistoryAsync(id),
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

    [HttpDelete("delete-uncontrolled-document/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _uncontrolledDocumentComponent.DeleteAsync(id),
                Message = "Uncontrolled Document deleted successfully.",
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
