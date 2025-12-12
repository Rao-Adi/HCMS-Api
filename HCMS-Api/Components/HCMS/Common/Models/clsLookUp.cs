namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class clsLookUp
    {

        public int EmpId { get; set; }
        public string EmpCode { get; set; }
        public string EmpName { get; set; }
        public string LastName { get; set; }
        public string Gender { get; set; }
        public string DOB { get; set; }
        public int CityId { get; set; }
        public int CityCode { get; set; }
        public string CityName { get; set; }
        public int Cntid { get; set; }
        public string DivisionName { get; set; }
        public string MainDepartment { get; set; }
        public string Department { get; set; }
        public string PayrollGroup { get; set; }
        public string Designation { get; set; }
        public string JobGroup { get; set; }
        public string EmployeeCategory { get; set; }
        public string EmployeeType { get; set; }
        public string Location { get; set; }
        public string Region { get; set; }
        public string EmployeeStatus { get; set; }
        public string search { get; set; }
        public string fieldName { get; set; }
        public string CompanyId { get; set; }
        public string hideColumn { get; set; }
        public ObjWhrClause whereClauseSearchBar { get; set; }
        public formFilterObj[] formFilter { get; set; }

    }
    public class formFilterObj
    {
        public string id { get; set; }
        public string itemName { get; set; }
    }
    public class ObjWhrClause
    {
        public string CompanyId { get; set; }
        public string Active { get; set; }
        public string EmpId { get; set; }
    }
}
