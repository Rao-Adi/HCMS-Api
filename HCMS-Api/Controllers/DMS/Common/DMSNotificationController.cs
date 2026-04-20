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
public class DMSNotificationController : Controller
{
    private readonly Utilities _utilities;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UtilitiesController> _logger;
    private readonly ClientContextService _clientContextService;
    private readonly NotificationComponent _notificationComponent;

    public DMSNotificationController(
     Utilities utilities
   , IConfiguration configuration
   , ILogger<UtilitiesController> logger
   , ClientContextService clientContextService,
     NotificationComponent notificationComponent)
    {
        _logger = logger;
        _utilities = utilities;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _notificationComponent = notificationComponent;
    }



    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll([FromBody] TableFiltersDto filter)
    {
        try
        {
            var result = await _notificationComponent.GetAllAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications");
            return StatusCode(500, new { message = "An error occurred fetching notifications." });
        }
    }

    [HttpPut("mark-as-read/{notificationId}/{empId}")]
    public async Task<IActionResult> MarkAsRead(int notificationId, int empId)
    {
        try
        {
            bool isSuccess = await _notificationComponent.MarkAsReadAsync(notificationId, empId);
            return Ok(new { success = isSuccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error marking notification {notificationId} as read");
            return StatusCode(500, new { message = "An error occurred marking notification as read." });
        }
    }

    [HttpPut("mark-all-as-read/{empId}")]
    public async Task<IActionResult> MarkAllAsRead(int empId)
    {
        try
        {

            bool isSuccess = await _notificationComponent.MarkAllAsReadAsync(empId);
            return Ok(new { success = isSuccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return StatusCode(500, new { message = "An error occurred marking all notifications as read." });
        }
    }


    [HttpPost("SendTestNotification")]
    public async Task<IActionResult> SendTestNotification([FromBody] TestNotificationDto request)
    {
        await _notificationComponent.SendTestNotification(request.Title, request.Message, request.Type);
        return Ok(new { success = true, message = "Test notification sent successfully" });
    }


    [HttpPost("get-all-notification")]
    public async Task<IActionResult> GetAllNotifications(TableFiltersDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<PaginationResult<NotificationReadDto>>()
            {
                Success = true,
                Data = await _notificationComponent.GetAllAsync(input),
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


    [HttpGet("get-notification-by-code/{code}/{isRead}")]
    public async Task<IActionResult> GetNotificationById(int code, bool isRead)
    {
        try
        {
            return Ok(new HttpApiResponse<List<NotificationReadDto>>()
            {
                Success = true,
                Data = await _notificationComponent.GetByIdAsync(code, isRead),
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

    [HttpPost("create-notification")]
    public async Task<IActionResult> Create([FromBody] NotificationCreateDto input)
    {
        if (!ModelState.IsValid)
        {
            // Return validation errors
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(new HttpApiResponse<NotificationReadDto>()
            {
                Success = true,
                Data = await _notificationComponent.CreateAsync(input),
                Message = "DocumentType created successfully.",
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

    [HttpPut("update-notification")]
    public async Task<IActionResult> Update([FromBody] NotificationUpdateDto input)
    {
        try
        {
            return Ok(new HttpApiResponse<NotificationReadDto>()
            {
                Success = true,
                Data = await _notificationComponent.UpdateAsync(input),
                Message = "DocumentType updated successfully.",
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

    [HttpDelete("delete-notification/{code}")]
    public async Task<IActionResult> DeleteAsync(int code)
    {
        try
        {

            return Ok(new HttpApiResponse<bool>()
            {
                Success = true,
                Data = await _notificationComponent.DeleteAsync(code),
                Message = "DocumentType deleted successfully.",
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

public class TestNotificationDto
{
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
}