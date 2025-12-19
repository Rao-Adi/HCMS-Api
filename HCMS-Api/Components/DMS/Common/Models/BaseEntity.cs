using System.ComponentModel.DataAnnotations;

namespace HCMS_Api.Components.DMS.Common.Models;

public class BaseEntity
{
    [Key]
    public int Id { get; set; }

    public Boolean IsDeleted { get; set; }

    public Boolean IsActive { get; set; }


    [DisplayFormat(DataFormatString = "{0:MM/dd/YYYY hh:mm:ss tt}")]
    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; }


    [DisplayFormat(DataFormatString = "{0:MM/dd/YYYY hh:mm:ss tt}")]
    public DateTime LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; }

    public BaseEntity()
    {
        this.CreatedAt = DateTime.Now;
        //this.UpdatedOn = DateTime.Now;
    }
}

public interface ICodeNameEntity
{
    [MaxLength(10)]
    string Code { get; set; }

    [MaxLength(350)]
    string Name { get; set; }
}