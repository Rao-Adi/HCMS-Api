namespace HCMS_Api.Common;
public class PaginationResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; } = 0;
}
