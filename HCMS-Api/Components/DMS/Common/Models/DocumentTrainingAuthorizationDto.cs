using System;

namespace HCMS_Api.Components.DMS.Common.Models;

public class DocumentTrainingAuthorizationCreateDto
{
    public string DocumentTypeCode { get; set; } = string.Empty;
    public bool AuthorizationRequired { get; set; } 
    public string? AuthorizingUserId { get; set; }
}

public class DocumentTrainingAuthorizationUpdateDto : DocumentTrainingAuthorizationCreateDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

public class DocumentTrainingAuthorizationReadDto : DocumentTrainingAuthorizationUpdateDto
{
    public int CompanyId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; 
    public string AuthorizingUser { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string LastModifiedAt { get; set; } = string.Empty;
    public string LastModifiedBy { get; set; } = string.Empty;
}