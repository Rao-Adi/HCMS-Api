using System.ComponentModel.DataAnnotations;

namespace HCMS_Api.Components.DMS.Common.Models;

public class Company
{
    [Key]
    public Int64 Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;
    public string? SubscriptionPlan { get; set; } = null!;
    public int? StorageQuotaGB { get; set; } = null!;
    public DateTime CreatedAt { get; set; }


    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}

public class CompanyReadDto : AuditableEntity
{
    [Key]
    public Int64 Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public string? Status { get; set; } = null!;
    public string? SubscriptionPlan { get; set; } = null!;
    public int? StorageQuotaGB { get; set; } = null!;
    public string CreatedAt { get; set; }

}


public class CompanyCreateDto
{
    public Int64 Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;
    public string? Status { get; set; } = null!;
    public string? SubscriptionPlan { get; set; } = null!;
    public int? StorageQuotaGB { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}


public class CompanyUpdateDto
{
    public Int64 Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public string? Status { get; set; } = null!;
    public string? SubscriptionPlan { get; set; } = null!;
    public int? StorageQuotaGB { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
