using System.ComponentModel.DataAnnotations;

namespace HCMS_Api.Components.DMS.Common.Models;

public class Company
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; } = null!;

    // Navigation
    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}

public class CompanyReadDto : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;
     
     
}


public class CompanyCreateDto
{ 
    public int Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;
     
}


public class CompanyUpdateDto
{ 
    public int Id { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
     
     
}
