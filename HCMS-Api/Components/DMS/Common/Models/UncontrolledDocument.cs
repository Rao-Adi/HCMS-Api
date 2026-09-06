namespace HCMS_Api.Components.DMS.Common.Models;

public class UncontrolledDocumentCreateDto
{
    public string DocumentName { get; set; } = null!;
    public DateTime ReviewDate { get; set; }
    public string ReviewAuthorityEmpCode { get; set; } = null!;
    public IFormFile? DocumentFile { get; set; }
}

public class UncontrolledDocumentReviewDto
{
    public int Id { get; set; }
    public DateTime NewReviewDate { get; set; }
    public IFormFile? DocumentFile { get; set; }
}

public class UncontrolledDocumentReadDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string DocumentName { get; set; } = null!;
    public string DocumentURL { get; set; } = null!;
    public DateTime ReviewDate { get; set; }
    public string ReviewAuthorityEmpCode { get; set; } = null!;
    public string? ReviewAuthorityName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string? CreatedByName { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; } = null!;
    public string? LastModifiedByName { get; set; }
}

public class UncontrolledDocumentHistoryDto
{
    public int Id { get; set; }
    public int UncontrolledDocumentId { get; set; }
    public string? PreviousDocumentURL { get; set; }
    public string NewDocumentURL { get; set; } = null!;
    public DateTime? PreviousReviewDate { get; set; }
    public DateTime NewReviewDate { get; set; }
    public string ReviewedBy { get; set; } = null!;
    public string? ReviewedByName { get; set; }
    public DateTime ReviewedAt { get; set; }
}
