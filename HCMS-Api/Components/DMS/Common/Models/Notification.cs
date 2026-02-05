using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(1000)]
    public string Message { get; set; } = null!;

    public int NotificationType { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public string CreatedAt { get; set; }
}


public class NotificationReadDto
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(1000)]
    public string Message { get; set; } = null!;

    public int NotificationType { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public string CreatedAt { get; set; }
}



public class NotificationCreateDto
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(1000)]
    public string Message { get; set; } = null!;

    public int NotificationType { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public string CreatedAt { get; set; }
}



public class NotificationUpdateDto
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(1000)]
    public string Message { get; set; } = null!;

    public int NotificationType { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public string CreatedAt { get; set; }
}

