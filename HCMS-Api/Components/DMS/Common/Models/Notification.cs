using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(1000)]
    public string Message { get; set; } = null!;

    public int NotificationType { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
