namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class EmployeeDataResponse
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeRole { get; set; }
    }

    public class ERPLogin
    {
        public string FormUri { get; set; }
        public string CompID { get; set; }
        public string password { get; set; }
        public string CompName { get; set; }
        public string USERIDCODE { get; set; }
        public string USERID { get; set; }
        public string CompetitorAllowed { get; set; }
        public string UserCulture { get; set; }

        public string App { get; set; }
    }
    public class others
    {
        public string UserSelectedCulture { get; set; }
    }

    public class DASMobileLogin
    {
        public string UserId { get; set; }
        public string Password { get; set; }
        public string UserName { get; set; }
        public int EmployeeId { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string Name { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationId { get; set; }
        public string Designation { get; set; }
        public string City { get; set; }
        public DateTime LastCLosingMonth { get; set; }
        public string ErrorMessage { get; set; }
        public string FCMToken { get; set; }
        public List<FormAttributes> formAttributes { get; set; }
    }
    public class FormAttributes
    {
        public string UserId { get; set; }
        public string Ccode { get; set; }
        public string FormId { get; set; }
        public string Module { get; set; }
        public string Description { get; set; }
        public bool AddMode { get; set; }
        public bool EditMode { get; set; }
        public bool DeleteMode { get; set; }
        public bool ViewMode { get; set; }
        public string Exception_Message { get; set; }

    }
    public class SessionHelper
    {
        public string UserId { get; set; }
    }

    public class WorkFlowTrack_Result
    {
        public Nullable<int> EmpId { get; set; }
        public string EmpCode { get; set; }
        public string EmpName { get; set; }
        public string Designation { get; set; }
        public string Department { get; set; }
        public string SubDepartment { get; set; }
        public string ApproverLevel { get; set; }
        public Nullable<bool> isDefaultApprover { get; set; }
        public Nullable<bool> ApproverStatus { get; set; }
        public Nullable<bool> isTemporaryApprover { get; set; }
        public Nullable<int> ActualApproverEmpId { get; set; }
        public string Remarks { get; set; }
        public string SeqNo { get; set; }
    }

    public sealed class IdNameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ProcessDateDto
    {
        public string DateId { get; set; } = string.Empty;
        public string DateName { get; set; } = string.Empty;
    }
}
