using System.ComponentModel.Design;
using System.Data;
using System.Data.SqlClient;
using HCMS_Api.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace HCMS_Api.Services.LookupService
{
    public class LookupService : ILookupService
    {

        private readonly Common.Common _common;

        public LookupService(Common.Common common)
        {

            _common = common;
        }

        public async Task<DataTable> GetEmployeeLookupAsync(int companyId)
        {
            string query = @"SELECT * FROM dbo.fn_Lookup_EmployeeProfile('en-GB', 1) WHERE active IN (1, 2) AND companyId = '" + companyId + "'";
            return await _common.ExecuteSqlQuery(query);
        }

        public async Task<DataTable> GetEmployeeHODLookupAsync(int companyId)
        {
            string query = @"SELECT * 
                                FROM fn_Lookup_EmployeeProfile('en-GB', '-1') Emp
                           WHERE 1 = 1 
                                AND (Emp.EmpId IN (SELECT EmpId FROM tblHodSetup WHERE CompanyId = 1) 
                                OR Emp.EmpId IN (SELECT d.EmpId 
                                FROM HCMS_JobPortal..tblDesignerISMaster m 
                                INNER JOIN HCMS_JobPortal..tblInterviewSetupDetail d 
                                ON d.MasterId = m.Id 
                            WHERE m.CompanyId = " + companyId + "))";
            return await _common.ExecuteSqlQuery(query);
        }

        public async Task<DataTable> GetCompanyLookupAsync()
        {

            string query = @"Select Cast(CCode as int) as CCode, CompanyName as CompanyDescription From EXTERNAL_SECURITY_COMPANYMANAGEMENT";


            return await _common.ExecuteSqlQuery(query);

        }

        public async Task<DataTable> lookUpDefinedAuthority(int companyId, string _Culture, string status)
        {

            string query = @"SELECT 
                                Emp.EmpId AS [Employee Id],
                                Emp.EmpCode AS [Employee Code],
                                    CASE 
                                        WHEN '" + companyId + @"' = CompanyId THEN Emp.NameOnly 
                                    ELSE Emp.NameWithShortCompany 
                                    END AS [Employee Name],
                                DivisionName AS [Division Name],
                                Emp.MainDepartment AS [Department],
                                Emp.Department AS [Sub-Department],
                                Emp.PayrollGroup AS [Payroll Group],
                                Emp.Designation AS [Designation],
                                JobGroup AS [Grade],
                                EmployeeCategory AS [Employee Category],
                                EmployeeType AS [Employee Type],
                                Location AS [Location],
                                Region AS [Region],
                                EmployeeStatus AS [Employee Status],
Emp.CompanyId
                            FROM 
                                fn_Lookup_EmployeeProfile('" + _Culture + @"', '-1') Emp
                                WHERE 
                                    1 = 1";
            if (status != "-1")
                query += @"AND Emp.Active IN (" + status + ")";

            query += @"
                        AND (
                            Emp.EmpId IN (
                                SELECT EmpId 
                                    FROM tblHodSetup 
                                WHERE CompanyId = " + companyId + @"
                                )
                        OR Emp.EmpId IN (
                            SELECT d.EmpId 
                                FROM HCMS_JobPortal..tblDesignerISMaster m
                                INNER JOIN HCMS_JobPortal..tblInterviewSetupDetail d ON d.MasterId = m.Id
                            WHERE m.CompanyId = " + companyId + @$"
                            )
OR Emp.EmpId IN (
                            SELECT ReportTo 
                                FROM tblEmployee where CompanyId={companyId} and Active = 1 UNION
                                SELECT Dotted FROM  tblEmployee where CompanyId={companyId} and Active = 1
                            )
OR Emp.EmpId IN (
                            SELECT ResponsiblePersonnel 
                                FROM tblHiringChecklistSetup where CompanyId={companyId}
                            )
							
OR Emp.EmpId IN (
                            SELECT ResponsiblePersonnel 
                                FROM tblHiringCheckListGradeMap where CompanyId={companyId}
                            )
							
OR Emp.EmpId IN (
                            SELECT ResponsiblePersonnel 
                                FROM tblHiringChecklistDetail   where CompanyId={companyId}
                            )
OR Emp.EmpId IN (
                            SELECT EmployeeId 
                                FROM tblWorkFlowSetupDefaultAuthority   where CompanyId={companyId}
                            )
OR Emp.EmpId IN (
                            SELECT EmpId 
                                FROM vWorkFlowSetup  where CompanyId={companyId} 
                            )
OR Emp.EmpId IN (
                            SELECT Authority_EmpId 
                                FROM tblWorkFlowStatus   where AuthorityStatus = 'P'
                            )
OR Emp.EmpId IN (
                            SELECT EmpId 
                                FROM tblClearanceSetup where CompanyId={companyId}
                            )
OR Emp.EmpId IN (
                            SELECT EmpId 
                                FROM tblConcernedDepartmentSetup where CompanyId={companyId}
                            )

OR Emp.EmpId IN (
                            SELECT master.ApproverID
FROM tblAssoctateClearance master
LEFT JOIN tblAssoctateClearanceDetail detail
    ON master.id = detail.id
WHERE detail.id IS NULL and master.CompanyId={companyId}
                            )
                       )";



            return await _common.ExecuteSqlQuery(query);
        }

        public async Task<DataTable> lookUpSelectedAuthority(string Company, string _Culture, string EmpDefined)
        {
            string query = @"SELECT 
                                Emp.EmpId AS [Employee Id],
                                Emp.EmpCode AS [Employee Code],
                                Emp.NameOnly AS [Employee Name],
                                DivisionName AS [Division Name],
                                Emp.MainDepartment AS [Department],
                                Emp.Department AS [Sub-Department],
                                Emp.PayrollGroup AS [Payroll Group],
                                Emp.Designation AS [Designation],
                                JobGroup AS [Grade],
                                EmployeeCategory AS [Employee Category],
                                EmployeeType AS [Employee Type],
                                Location AS [Location],
                                Region AS [Region],
                                EmployeeStatus AS [Employee Status],
Emp.CompanyId
                            FROM 
                                fn_Lookup_EmployeeProfile('" + _Culture + "', '" + Company + @"') Emp
                            WHERE 
                                1 = 1
                                AND EmpId <> " + EmpDefined + @" AND Emp.Active IN(1,2)
                            ORDER BY 
                                CAST(Emp.EmpCode AS INT);";

            return await _common.ExecuteSqlQuery(query);

        }

        public async Task<DataTable> lookUpEmpGrid(string Company, string _Culture)
        {
            string query = @"SELECT
    Emp.EmpId AS [Employee Id],
    Emp.EmpCode AS [Employee Code],
    Emp.NameOnly AS [Employee Name],
    DivisionName AS [Division Name],
    Emp.MainDepartment AS [Department],
    Emp.Department AS [Sub-Department],
    Emp.PayrollGroup AS [Payroll Group],
    Emp.Designation AS [Designation],
    JobGroup AS [Grade],
    EmployeeCategory AS [Employee Category],
    EmployeeType AS [Employee Type],
    Location AS [Location],
    Region AS [Region],
    EmployeeStatus AS [Employee Status]
FROM
    fn_Lookup_EmployeeProfile('" + _Culture + "', '" + Company + @"') Emp
WHERE
    1 = 1 AND Emp.Active IN(1,2) AND
    CompanyId = " + Company + "";


            return await _common.ExecuteSqlQuery(query);

        }




        public async Task<DataTable> GetEmployeeStatusAsync()
        {

            DataTable dtEmpStatus = new DataTable();
            DataColumnCollection columns1 = dtEmpStatus.Columns;
            if (!columns1.Contains("ID") && !columns1.Contains("Name"))
            {
                dtEmpStatus.Columns.Add("ID");
                dtEmpStatus.Columns.Add("Name");
            }
            //dtEmpStatus = Utilities.GetEmployeeStatus();
            DataRow dr1 = dtEmpStatus.NewRow(); //Create New Row
            dr1["ID"] = "-1";
            dr1["Name"] = "All"; // Set Column Value
            dtEmpStatus.Rows.InsertAt(dr1, 0);

            DataRow dr2 = dtEmpStatus.NewRow(); //Create New Row
            dr2["ID"] = "1,2";
            dr2["Name"] = "Active Employees (All)"; // Set Column Value
            dtEmpStatus.Rows.InsertAt(dr2, 1);

            DataRow dr3 = dtEmpStatus.NewRow(); //Create New Row
            dr3["ID"] = "2";
            dr3["Name"] = "Active Employees (Resigned but not Separated)"; // Set Column Value
            dtEmpStatus.Rows.InsertAt(dr3, 2);

            DataRow dr4 = dtEmpStatus.NewRow(); //Create New Row
            dr4["ID"] = "0";
            dr4["Name"] = "Separated Employees"; // Set Column Value
            dtEmpStatus.Rows.InsertAt(dr4, 3);

            return dtEmpStatus;
        }

        public string GetResignEmployeeHoD(string Company, string EmpId, string _culture)
        {
            string query = @"Exec Sp_GetResignedEmployee @Param_LoginEmpId = " + EmpId + ",@Param_LoginCulture = '" + _culture + "',@Param_ComapanyId = '" + Company + "'";

            return _common.ExecuteScalarQuery(query);
        }
    }
}
