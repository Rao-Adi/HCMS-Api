using System;

namespace HCMS_Api.Components.DMS.Common.Models;

public class DocumentReviewPolicyCreateDto
{
    public string DocumentTypeCode { get; set; } = string.Empty;
    public int ReviewPeriodYears { get; set; }
}

public class DocumentReviewPolicyUpdateDto : DocumentReviewPolicyCreateDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

public class DocumentReviewPolicyReadDto :  AuditableEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;

    public string DocumentTypeCode { get; set; } = string.Empty;
    public int ReviewPeriodYears { get; set; }
}