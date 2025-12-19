using System.Net;

namespace HCMS_Api.Common;


public static class HttpResponseCode
{
    public static int GetHttpStatusCode(int errorCode)
    {
        var result = errorCode switch
        {
            400 => (int)HttpResponseStatus.BadRequest,
            401 => (int)HttpResponseStatus.Unauthorized,
            403 => (int)HttpResponseStatus.Forbidden,
            404 => (int)HttpResponseStatus.NotFound,
            405 => (int)HttpResponseStatus.MethodNotAllowed,
            409 => (int)HttpResponseStatus.Conflict,
            410 => (int)HttpResponseStatus.Gone,
            412 => (int)HttpResponseStatus.PreconditionFailed,
            415 => (int)HttpResponseStatus.UnsupportedMediaType,
            422 => (int)HttpResponseStatus.UnprocessableEntity,
            429 => (int)HttpResponseStatus.TooManyRequests,
            500 => (int)HttpResponseStatus.InternalServerError,
            501 => (int)HttpResponseStatus.NotImplemented,
            502 => (int)HttpResponseStatus.BadGateway,
            503 => (int)HttpResponseStatus.ServiceUnavailable,
            504 => (int)HttpResponseStatus.GatewayTimeout,
            200 => (int)HttpResponseStatus.Success,
            _ => (int)HttpResponseStatus.BadRequest
        };
        return result;
        //switch (errorCode)
        //{
        //    case 4001: // Example error code and corresponding status code
        //        return (int)HttpResponseStatus.BadRequest;
        //    // Add more cases as needed for other error codes
        //    default:
        //        return (int)HttpResponseStatus.BadRequest; // Default to BadRequest if no matching code found
        //}
    }
}

public static class HttpResponseCatchReturn
{
    public static HttpApiResponse<T> ReturnException<T>(CustomException ex, T data)
    {
        var statusCode = HttpResponseCode.GetHttpStatusCode(ex.ErrorCode);
        return new HttpApiResponse<T>()
        {
            Success = false,
            Data = data,
            Message = ex.Message,
            Code = ex.ErrorCode // Set the error code from the CustomException
        };
    }

    public static HttpApiResponse<T> ReturnException<T>(Exception ex, T data)
    {
        HttpStatusCode statusCode;

        // Determine the status code based on the type of exception
        if (ex is CustomException customException)
        {
            statusCode = (HttpStatusCode)HttpResponseCode.GetHttpStatusCode(customException.ErrorCode);
        }
        else
        {
            statusCode = HttpStatusCode.InternalServerError; // Default status code for other exceptions
        }

        return new HttpApiResponse<T>()
        {
            Success = false,
            Data = data,
            Message = ex.Message,
            Code = (int)statusCode
        };
    }

}


public enum HttpResponseStatus
{
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    MethodNotAllowed = 405,
    Conflict = 409,
    Gone = 410,
    PreconditionFailed = 412,
    UnsupportedMediaType = 415,
    UnprocessableEntity = 422,
    TooManyRequests = 429,
    InternalServerError = 500,
    NotImplemented = 501,
    BadGateway = 502,
    ServiceUnavailable = 503,
    GatewayTimeout = 504,
    Success = 200,
}