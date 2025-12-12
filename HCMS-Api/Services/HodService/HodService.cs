using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Data.SqlClient;
using static HCMS_Api.Controllers.HodSetupController;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace HCMS_Api.Services.HodService
{
    public class HodService : IHodService
    {

        private readonly Common.Common _common;


        public HodService(Common.Common common)
        {
            _common = common;
        }



        public async Task<DataTable> GetEmployeeAuthorityHODAsync(string OrderBy, string _Culture, string _CompanyId, string OldEmpId)
        {
            string query = " Select * from ( " +
                      " Select HodId, DivId, MDeptId, DeptId,CompanyId,EmpID, " +
                      " (Select Name from tblsetupsdetail where sdlid = DivId) Division, " +
                      " (Select Name from tblsetupsdetail where sdlid = MDeptId) MainDepartment, " +
                      " (Select Name from tblsetupsdetail where sdlid = DeptId) Department, " +
                      " (Select CompanyId from tblemployee where EmpId = tbl.EmpId) CCode, " +
                      " (Select (Select CompanyName as CompanyDescription From EXTERNAL_SECURITY_COMPANYMANAGEMENT where CCode = CompanyId) from tblemployee where EmpId = tbl.EmpId) CompanyDescription, " +
                      " (Select EmpCode from fn_Lookup_EmployeeProfile('" + _Culture + "', -1) where EmpId = tbl.EmpId) EmpCode, " +
                      " (Select (case when fn.CompanyId = " + _CompanyId + " then NameOnly else NameWithShortCompany End) Name from fn_Lookup_EmployeeProfile('" + _Culture + "', -1) fn where EmpId = tbl.EmpId) Name " +
                      //" ROW_NUMBER() OVER (ORDER BY " + OrderBy + ") " + " AS ResultSetRowNumber " +
                      " from HCMS..tblHODSetup tbl where tbl.CompanyId = " + _CompanyId + " and EmpId = " + OldEmpId + ")A"
                      + @" ORDER BY 
    CASE 
        WHEN Department IS NULL OR Department = 'N/A' THEN 0
        WHEN MainDepartment IS NULL OR MainDepartment = 'N/A' THEN 1
        ELSE 2
    END, 
    Division ASC";
            return await _common.ExecuteSqlQuery(query);
        }

        public async Task<DataTable> GetHodNonAtiveAsync()
        {

            string query = @"SELECT emp.EmpId As 'Employee ID', emp.Name As 'Employee Name',LTrim(RTrim(CompanyName)) AS Company ,
                            Div.Name AS Division, Mdept.Name AS Department, dept.Name AS 'Sub Department'
                            FROM fn_Lookup_EmployeeProfile('en-GB', '-1') AS emp
                            JOIN tblHodSetup AS hod ON emp.EmpId = hod.EmpId
                            LEFT JOIN tblSetupsDetail AS Mdept ON hod.MDeptId = Mdept.sdlid
                            LEFT JOIN tblSetupsDetail AS dept ON hod.DeptId = dept.sdlid
                            LEFT JOIN tblSetupsDetail AS Div ON hod.DivId = Div.sdlid
                            LEFT JOIN vCompanyManagement AS company ON emp.CompanyId = company.CCODE
                            WHERE emp.Active != 1";

            return await _common.ExecuteSqlQuery(query);

        }

        public bool UpdateHoDAll([FromBody] List<HoDData> data, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            try
            {

                string CompanyId = SelectedCompany;

                //string query1 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                //             " Select hodId,EmpId,'" + NewEmpId + "','HoD',CompanyId,GetDate(),'" + _UserId + "','" + _FormId + "',GetDate(),'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "' from tblHodSetup where CompanyId = " + CompanyId + " and EmpId = " + OldEmpId + "";

                //string query2 = " update tblHodSetup set EmpId = " + NewEmpId + ", ApplicationId = 'HCMS', FormId = '" + _FormId + "',UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                //                " UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntOperation='Update',EntDate=GetDate() where EmpId = " + OldEmpId + "";

                foreach (var item in data)
                {
                    int OldEmpId = Convert.ToInt32(_common.ExecuteScalarQuery("select EmpId from tblHodSetup where HodId = " + item.HodId + " "));

                    string query1 = " update tblHodSetup set EmpId = " + item.EmpID + ", ApplicationId = 'HCMS', FormId = '" + _FormId + "',UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                    " UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntOperation='Update',EntDate=GetDate()   where HodId = " + item.HodId + "";
                    string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                    " VALUES ('" + item.HodId + "','" + OldEmpId + "','" + item.EmpID + "','HoD','" + CompanyId + "',GetDate(),'" + _UserId + "','" + _FormId + "',GetDate(),'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                    _common.ExecuteNonQuery(query1);
                    _common.ExecuteNonQuery(query2);
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception();
            }

        }


    }
}
