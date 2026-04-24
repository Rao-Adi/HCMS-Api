using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("AuditLogs")]
public class AuditLog : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string EmployeeCode { get; set; }

    [MaxLength(100)]
    public string Action { get; set; } = null!;

    [MaxLength(50)]
    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }

    [MaxLength(45)]
    public string? IPAddress { get; set; }
}

public class AuditLogReadDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string EmployeeCode { get; set; }
     
    public string Action { get; set; } = null!;
     
    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }
     
    public string? IPAddress { get; set; }
}

public class AuditLogCreateDto
{   
    public string EmployeeCode { get; set; }
     

    public string Action { get; set; } = null!;
     
    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }
     
    public string? IPAddress { get; set; }
}

public class AuditLogUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string EmployeeCode { get; set; }
     
    public string Action { get; set; } = null!;
     
    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }
     
    public string? IPAddress { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

