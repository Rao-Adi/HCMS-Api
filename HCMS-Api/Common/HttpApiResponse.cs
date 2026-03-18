namespace HCMS_Api.Common;

public class HttpApiResponse<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public int Code { get; set; }
}

