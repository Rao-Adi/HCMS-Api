namespace HCMS_Api.Common;

public class CustomException : Exception
{
    public int ErrorCode { get; }

    public CustomException(string message, int errorCode = 404) : base(message)
    {
        ErrorCode = errorCode;
    }
}
