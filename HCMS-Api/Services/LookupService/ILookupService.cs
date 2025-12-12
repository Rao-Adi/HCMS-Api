using System.Data;

namespace HCMS_Api.Services.LookupService
{
    public interface ILookupService
    {
        Task<DataTable> GetEmployeeLookupAsync(int companyId);
        Task<DataTable> GetEmployeeHODLookupAsync(int companyId);

        Task<DataTable> GetCompanyLookupAsync();

        Task<DataTable> lookUpDefinedAuthority(int companyId, string _Culture, string status);
        Task<DataTable> lookUpSelectedAuthority(string Company, string _Culture, string EmpDefined);
        Task<DataTable> lookUpEmpGrid(string Company, string _Culture);

        Task<DataTable> GetEmployeeStatusAsync();

        string GetResignEmployeeHoD(string Company, string EmpId, string _culture);


    }
}
