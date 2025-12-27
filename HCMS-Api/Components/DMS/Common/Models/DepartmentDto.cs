using System.Reflection.Metadata;
using static HCMS_Api.Controllers.HCMS.Common.SecurityController;

namespace HCMS_Api.Components.DMS.Common.Models.Departments;

public class DepartmentDto
{
}


public class DepartmentCreateDto
{
    public string Code { get; set; }
    public string DivisionCode { get; set; }

    public string Name { get; set; }
}

public class DepartmentUpdateDto
{
    public string Code { get; set; }
    public string Name { get; set; }

    public string DivisionCode { get; set; }

    public bool IsActive { get; set; }
}

public class DepartmentReadDto
{
    public string Code { get; set; }
    public string Name { get; set; }

    public string Division { get; set; }
    public string DivisionCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; }
}



//--------------------------------------

public abstract class BaseEntity
{
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; } = null!;
}


public class Division : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<Department> Departments { get; set; } = new HashSet<Department>();
    public ICollection<User> Users { get; set; } = new HashSet<User>();
}

public class Department : BaseEntity
{
    public int Id { get; set; }

    public string DivisionCode { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public Division Division { get; set; } = null!;
    public ICollection<SubDepartment> SubDepartments { get; set; } = new HashSet<SubDepartment>();
}

public class SubDepartment : BaseEntity
{
    public int Id { get; set; }

    public string DepartmentCode { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public Department Department { get; set; } = null!;
}


public class User : BaseEntity
{
    public Guid Id { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public Division? Division { get; set; }
    public Department? Department { get; set; }
    public SubDepartment? SubDepartment { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
}

public class Role : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public Guid CreatedById { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public Guid LastModifiedById { get; set; }
    public User LastModifiedByUser { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
}


public class UserRole : BaseEntity
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}


public class DocumentType : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public ICollection<Document> Documents { get; set; } = new HashSet<Document>();
}

public class Document : BaseEntity
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = null!;

    public string DocumentTypeCode { get; set; } = null!;
    public string DivisionCode { get; set; } = null!;
    public string DepartmentCode { get; set; } = null!;
    public string SubDepartmentCode { get; set; } = null!;

    public string DocumentName { get; set; } = null!;
    public int Status { get; set; }

    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateOnly? NextReviewDate { get; set; }

    public DocumentType DocumentType { get; set; } = null!;
    public Division Division { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public SubDepartment SubDepartment { get; set; } = null!;

    public ICollection<DocumentVersion> Versions { get; set; } = new HashSet<DocumentVersion>();
}

public class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }

    public string Version { get; set; } = null!;
    public int VersionType { get; set; }
    public string Content { get; set; } = null!;
    public string? ChangeDescription { get; set; }
    public int Status { get; set; }

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public Document Document { get; set; } = null!;
}


public class WorkflowPolicy : BaseEntity
{
    public Guid Id { get; set; }
    public int PolicyType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? DocumentTypeCode { get; set; }

    public int? SharingType { get; set; }

    public ICollection<WorkflowStep> Steps { get; set; } = new HashSet<WorkflowStep>();
}

public class WorkflowStep : BaseEntity
{
    public Guid Id { get; set; }
    public Guid WorkflowPolicyId { get; set; }
    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public Guid? ApproverUserId { get; set; }
    public int? ApprovalLevel { get; set; }

    public WorkflowPolicy WorkflowPolicy { get; set; } = null!;
    public Role? ApproverRole { get; set; }
    public User? ApproverUser { get; set; }
}
