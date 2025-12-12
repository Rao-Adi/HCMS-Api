using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Data;
using static HCMS_Api.Controllers.HodSetupController;

namespace HCMS_Api.Services.HodService
{
    public interface IHodService
    {
        Task<DataTable> GetHodNonAtiveAsync();
        Task<DataTable> GetEmployeeAuthorityHODAsync(string OrderBy, string _Culture, string _CompanyId, string OldEmpId);

        bool UpdateHoDAll([FromBody] List<HoDData> data, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP);
    }
}
