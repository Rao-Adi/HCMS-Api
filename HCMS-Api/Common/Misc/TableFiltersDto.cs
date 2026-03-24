namespace HCMS_Api.Common.Misc;

public class TableFiltersDto
{
    public string? SearchText { get; set; }

    public string? SortBy { get; set; }

    public string? SortColumn { get; set; }

    public bool IsActive { get; set; } = true;

    public int PageNumber { get; set; }

    public int PageSize { get; set; }
}
