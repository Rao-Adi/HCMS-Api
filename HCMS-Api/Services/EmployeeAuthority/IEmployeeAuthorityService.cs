using Microsoft.AspNetCore.Mvc;
using System.Data;
using static HCMS_Api.Controllers.EmployeeAuthorityController;

namespace HCMS_Api.Services.EmployeeAuthority
{
    public interface IEmployeeAuthorityService
    {

        Task<DataTable> GetEmployeeTxtCode(string EmpCode, string _culture, string GetCompanyId, string status);
        Task<Dictionary<string, object>> GetResignEmployee(string EmpId, string _culture, string GetCompanyId);
        Task<Dictionary<string, object>> GetResignEmployee2(string EmpId, string _culture, string GetCompanyId);
        Task<DataTable> GetInterviewDesigner(string OrderBy, string _Culture, string _CompanyId, string OldEmpId);
        bool UpdateInterviewDesignerAll([FromBody] List<InterviewDesignerData> data, string OldEmpId, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP);
        Task<DataTable> GetDirectInDirectReport(string OldEmpId, string SelectedCompany);
        Task<string> EffectiveDateValidate(string txtTEFDate, string SelectedCompany);

        Task<bool> UpdateReportingAll([FromBody] List<ReportingData> data, string OldEmpId, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP);
        Task<Dictionary<string, object>> GetHiringChecklist(string OldEmpId, string SelectedCompany);
        bool UpdateHiringCheckList([FromBody] HiringCheckList data, string OldEmpId, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP);
        Task<Dictionary<string, object>> GetExitClearance(string OldEmpId, string SelectedCompany);
        Task<bool> UpdateExitClearance([FromBody] ExitClearance data);


        Task<Dictionary<string, object>> GetWorkFlows(string OldEmpId, string SelectedCompany);

        Task<bool> UpdateWorkFlows([FromBody] WorkFlows? data, string OldEmpId, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP);

        Task<Dictionary<string, object>> GetWorkFlowsTrack(string CompanyId, string LoginEmpId, string ParentRecordId, string WorkflowID, string _Culture);




    }
}
