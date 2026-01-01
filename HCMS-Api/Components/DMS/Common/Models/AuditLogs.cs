using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("AuditLogs")]
public class AuditLog : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(100)]
    public string Action { get; set; } = null!;

    [MaxLength(50)]
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }

    [MaxLength(45)]
    public string? IPAddress { get; set; }
}

public class AuditLogReadDto
{ 
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
     
    public string Action { get; set; } = null!;
     
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }
     
    public string? IPAddress { get; set; }
}

public class AuditLogCreateDto
{   
    public Guid UserId { get; set; }
     
    public string Action { get; set; } = null!;
     
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }
     
    public string? IPAddress { get; set; }
}

public class AuditLogUpdateDto
{ 
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
     
    public string Action { get; set; } = null!;
     
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }
     
    public string? IPAddress { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

