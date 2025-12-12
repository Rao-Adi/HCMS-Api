#nullable disable
namespace HCMS_Api.Models
{
    public class JwtSettings
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public int ExpireMonths { get; set; }
    }

    public class JwtArray
    {
        public string FormId { get; set; }
        public string UserID { get; set; }
        public string UserEmpID { get; set; }
        public string UserEmpName { get; set; }
        public string UserEmpCode { get; set; }
        public string UserCompanyID { get; set; }
        public string EntTerminal { get; set; }
        public string EntTerminalIP { get; set; }
        public string LoginCulture { get; set; }
    }
}
