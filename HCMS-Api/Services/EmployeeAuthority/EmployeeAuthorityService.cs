
using HCMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using System.ComponentModel.Design;
using System.Data;
using System.Text.Json;
using static HCMS_Api.Controllers.EmployeeAuthorityController;
using static HCMS_Api.Controllers.HodSetupController;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace HCMS_Api.Services.EmployeeAuthority
{
    public class EmployeeAuthorityService : IEmployeeAuthorityService
    {

        private readonly Common.Common _common;
        public EmployeeAuthorityService(Common.Common common)
        {
            _common = common;//
        }
        public async Task<DataTable> GetEmployeeTxtCode(string EmpCode, string _culture, string companyId, string status)
        {
            string query;
            if (status == "2")
            {
                query = $@"SELECT 
                                    Emp.EmpId AS [Employee Id],
                                    Emp.EmpCode AS [Employee Code],
                                        CASE 
                                            WHEN '{companyId}' = CompanyId THEN Emp.NameOnly 
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
                                    fn_Lookup_EmployeeProfile('{_culture}', '-1') Emp
                                    WHERE 
            Emp.EmpCode='{EmpCode}' and Emp.CompanyId={companyId}";
            }
            else
            {
                query = @"SELECT 
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
                                fn_Lookup_EmployeeProfile('" + _culture + @"', '-1') Emp
                                WHERE 
                                    1 = 1";


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
                       ) And Emp.EmpCode = '{EmpCode}'";
            }

            return await _common.ExecuteSqlQuery(query);
        }

        public async Task<Dictionary<string, object>> GetResignEmployee(string EmpId, string _culture, string GetCompanyId)
        {
            try { 
            string result1 = "";

            result1 = Convert.ToString(_common.ExecuteScalarQuery("Exec Sp_GetResignedEmployee @Param_LoginEmpId = " + EmpId + ",@Param_LoginCulture = '" + _culture + "',@Param_ComapanyId = '" + GetCompanyId + "'"));

            var result2 = new DataTable();
            result2 = await _common.ExecuteSqlQuery(" Select DivisionName,MainDepartment,Department,Location,Region,Team,DutyGroup,DateJoin_Formatted," +
                                                               "PayrollGroup,Designation,JobGroup,EmployeeCategory,EmployeeType,EmployeeStatus," +
                                                               "(case when (select CompanyId from tblemployee where EmpId = fn.ReportTo) = fn.CompanyId then (select FirstName + (case when isnull(MidName,'') = '' then ' ' else MidName + ' '  End) + LastName from tblemployee where EmpId = fn.ReportTo) else fn.tblReportToName End)," +
                                                               "CDueDate_Formatted,EmpCode,(case when '" + GetCompanyId + "' = CompanyId then NameOnly else NameWithShortCompany end)" +
                                                               "from fn_Lookup_EmployeeProfile('" + _culture + "', '-1') fn where EmpId = " + EmpId + "");

            object objimage = _common.ExecuteScalarQuery("EmpPic", "HCMS_files.dbo.tblEmployee_imagecv", "EmpId = " + EmpId);

            string empPic = "../../Images/EmpImgD.jpg";

            if (objimage != null)
            {
                if (objimage.ToString() != "" && objimage != null && objimage != System.DBNull.Value)
                {
                    byte[] empImage = (byte[])objimage;
                    if (empImage != null && empImage.Length > 0)
                    {
                        string base64String = Convert.ToBase64String(empImage, 0, empImage.Length);
                        empPic = "data:image/png;base64," + base64String;
                    }

                }
            }
            // HoD Area
            string HodAreaCount = Convert.ToString(_common.ExecuteScalarQuery(" Select count(hodId) from tblHodSetup where EmpId = '" + EmpId + "' and CompanyId = '" + GetCompanyId + "'"));
            // HoD Area

            string InterviewDesignerAreaCount = Convert.ToString(_common.ExecuteScalarQuery(" Select Count(d.EmpId) from HCMS_JobPortal..tblDesignerISMaster m " +
                                                                                     " inner join HCMS_JobPortal..tblInterviewSetupDetail d on d.MasterId = m.Id " +
                                                                                     " where d.EmpId = '" + EmpId + "' and d.CompanyId = '" + GetCompanyId + "'"));

            string DirectInDirectReportQuery = "SELECT Count(*) FROM tblemployee WHERE (ReportTo = " + EmpId + " OR Dotted = " + EmpId + ") And CompanyId=" + GetCompanyId + " and Active = 1";
            string DirectInDirectReportCount = Convert.ToString(_common.ExecuteScalarQuery(DirectInDirectReportQuery));


            string WorkFlowQuery = @$"
SELECT Count(*) from vWorkFlowSetup WFS
LEFT JOIN 
    tblWorkFlowSetupDefaultAuthority WFSDA
	ON 
    WFS.WFlowTypeId = WFSDA.WFlowTypeId
	where WFS.Companyid = {GetCompanyId} and (WFS.EmpId = {EmpId} Or WFSDA.EmployeeId = {EmpId})";
            string WorkFlowCount = Convert.ToString(_common.ExecuteScalarQuery(WorkFlowQuery));

            string WorkFlowDetailQuery = @$"
SELECT Count(*)
FROM
    tblWorkFlow
WHERE
   CompanyId = {GetCompanyId} and FinalAuthorityType = 'Employee' and FinalAuthorityEmpId ={EmpId}";
            string WorkFlowDetailCount = Convert.ToString(_common.ExecuteScalarQuery(WorkFlowDetailQuery));


            var row = new Dictionary<string, object>
                {
                    { "Resigned", result1 },
                    { "Details", _common.GetJsonDatatable(result2) },
                    { "EmpPic", empPic },
                    { "HodAreaCount", HodAreaCount },
                    { "InterviewDesignerAreaCount", InterviewDesignerAreaCount },
                    { "DirectInDirectReportCount", DirectInDirectReportCount },
                    { "WorkFlowCount", Convert.ToInt32(WorkFlowCount)+Convert.ToInt32(WorkFlowDetailCount) }

                };

            return row;

            }
            catch (Exception ex)
            {
                var row = new Dictionary<string, object>
    {
        { "ErrorMessage", ex.Message },
        { "StackTrace", ex.StackTrace },
        { "InnerException", ex.InnerException?.Message ?? "No inner exception" },
        { "Source", ex.Source }
    };
                return row;
            }

        }

        public async Task<Dictionary<string, object>> GetResignEmployee2(string EmpId, string _culture, string GetCompanyId)
        {
            string result1 = "";
            result1 = Convert.ToString(_common.ExecuteScalarQuery("Exec Sp_GetResignedEmployee @Param_LoginEmpId = " + EmpId + ",@Param_LoginCulture = '" + _culture + "',@Param_ComapanyId = '" + GetCompanyId + "'"));

            var result2 = new DataTable();
            result2 = await _common.ExecuteSqlQuery(" Select DivisionName,MainDepartment,Department,Location,Region,Team,DutyGroup,DateJoin_Formatted," +
                                                               "PayrollGroup,Designation,JobGroup,EmployeeCategory,EmployeeType,EmployeeStatus,tblReportToName,CDueDate_Formatted,EmpCode,NameOnly " +
                                                               "from fn_Lookup_EmployeeProfile('" + _culture + "', '" + GetCompanyId + "') where EmpId = " + EmpId + "");

            object objimage = _common.ExecuteScalarQuery("EmpPic", "HCMS_files.dbo.tblEmployee_imagecv", "EmpId = " + EmpId);

            string empPic = "../../Images/EmpImgD.jpg";
            if (objimage != null && objimage.ToString() != "" && objimage != System.DBNull.Value)
            {
                byte[] empImage = (byte[])objimage;
                if (empImage != null && empImage.Length > 0)
                {
                    string base64String = Convert.ToBase64String(empImage, 0, empImage.Length);
                    empPic = "data:image/png;base64," + base64String;
                }

            }
            var row = new Dictionary<string, object>
                {
                    { "Resigned", result1 },
                    { "Details", _common.GetJsonDatatable(result2) },
                    { "EmpPic", empPic }


                };

            return row;
        }

        public async Task<DataTable> GetInterviewDesigner(string OrderBy, string _Culture, string _CompanyId, string OldEmpId)
        {

            string query = " Select * from ( " +
                           " Select d.Id,m.DesignationId,m.AssesmentTypeId,d.EmpId, " +
                           " (Select Name from vwtblsetupsdetail where Culture = 'en-GB' and sdlid = m.DesignationId) Designation, " +
                           " (Select Name from vwtblsetupsdetail where Culture = 'en-GB' and sdlid = m.AssesmentTypeId) InterviewType, " +
                           " InterviewNo, " +
                           " (Select CompanyId from tblemployee where EmpId = d.EmpId) CCode,  " +
                           " (Select(Select CompanyName as CompanyDescription From EXTERNAL_SECURITY_COMPANYMANAGEMENT where CCode = CompanyId) from tblemployee where EmpId = d.EmpId) CompanyDescription, " +
                           " (Select EmpCode from fn_Lookup_EmployeeProfile('" + _Culture + "', -1) where EmpId = d.EmpId) EmpCode, " +
                           " (Select(case when fn.CompanyId = " + _CompanyId + " then NameOnly else NameWithShortCompany End) Name from fn_Lookup_EmployeeProfile('" + _Culture + "', -1) fn where EmpId = d.EmpId) Name " +
                           //" ROW_NUMBER() OVER(ORDER BY " + OrderBy + ") " + " AS ResultSetRowNumber " +
                           " from HCMS_JobPortal..tblDesignerISMaster m " +
                           " inner join HCMS_JobPortal..tblInterviewSetupDetail d on d.MasterId = m.Id " +
                           " where d.CompanyId = " + _CompanyId + " and d.EmpId = " + OldEmpId + " " +
                           " ) A";



            return await _common.ExecuteSqlQuery(query);

        }
        public bool UpdateInterviewDesignerAll([FromBody] List<InterviewDesignerData> data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            try
            {

                string CompanyId = SelectedCompany;
                string date = _common.ExecuteScalarQuery("select dbo.fn_general_getlocaldatetimeCompanyWise(" + SelectedCompany + ")");


                foreach (var item in data)
                {
                    //int OldEmpId = Convert.ToInt32(_common.ExecuteScalarQuery("select EmpId from tblHodSetup where HodId = " + item.HodId + " "));

                    //string query1 = " update tblHodSetup set EmpId = " + item.EmpID + ", ApplicationId = 'HCMS', FormId = '" + _FormId + "',UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                    //" UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntOperation='Update',EntDate=GetDate()   where HodId = " + item.HodId + "";
                    //string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                    //                " VALUES ('" + item.HodId + "','" + OldEmpId + "','" + item.EmpID + "','HoD','" + CompanyId + "',GetDate(),'" + _UserId + "','" + _FormId + "',GetDate(),'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                    int OldEmpIds = Convert.ToInt32(_common.ExecuteScalarQuery("select EmpId from HCMS_JobPortal..tblInterviewSetupDetail where Id = " + item.Id + " "));

                    string query1 = " update HCMS_JobPortal..tblInterviewSetupDetail set EmpId = " + item.EmpId + ", " +
                         " DsgId = (select DsgId from tblemployee where EmpId = " + item.EmpId + "), " +
                         " ApplicationID = 'HCMS', EntFormId = '" + _FormId + "', UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                         " UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntOperation='Update',EntDate='" + date + "' where Id = " + item.Id + "";

                    string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                          " VALUES ('" + item.Id + "','" + OldEmpIds + "','" + item.EmpId + "','InterviewDesigner','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


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

        public async Task<bool> UpdateReportingAll([FromBody] List<ReportingData> data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            try
            {

                string CompanyId = SelectedCompany;

                string date = _common.ExecuteScalarQuery("select dbo.fn_general_getlocaldatetimeCompanyWise(" + SelectedCompany + ")");
                string TransferType = _common.ExecuteScalarQuery($"select Top 1 sdlid from tblsetupsdetail where smsid=100 and companyid={SelectedCompany} and cast(code as int)=0");

                foreach (var item in data.Where(x=>x.reportedchange == "true").ToList())
                {
                    //int OldEmpId = Convert.ToInt32(_common.ExecuteScalarQuery("select EmpId from tblHodSetup where HodId = " + item.HodId + " "));

                    //string query1 = " update tblHodSetup set EmpId = " + item.EmpID + ", ApplicationId = 'HCMS', FormId = '" + _FormId + "',UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                    //" UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntOperation='Update',EntDate=GetDate()   where HodId = " + item.HodId + "";
                    //string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                    //                " VALUES ('" + item.HodId + "','" + OldEmpId + "','" + item.EmpID + "','HoD','" + CompanyId + "',GetDate(),'" + _UserId + "','" + _FormId + "',GetDate(),'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";
                    if (item.TDate != null)
                    {
                        var tblemployee = await _common.ExecuteSqlQuerySingleRow(@$"select CompanyId,DivId,dptId,MdptId,dsgId,brnID,JobId,MTeamId,TeamId,BaseId,dispCntId,PGId,Shift,ReportTo,ReportToCompany,Dotted,DottedReportToCompany from tblEmployee where empid={item.EmpId} and CompanyId={SelectedCompany}");

                        string EmployeeTransaction = @$"Insert into tblemphistory 
(CompanyId,DivIdOld,dptIdOld,MdptIdOld,dsgIdOld,LocIdOld,JobIdOld,regIdOld,MTeamIdOld,TeamIdOld,BaseIdOld,dispCntIdOld,PGIdOld,ShiftOld,
DivIdNew,dptIdNew,MdptIdNew,dsgIdNew,LocIdNew,JobIdNew,regIdNew,MTeamIdNew,TeamIdNew,BaseIdNew,dispCntIdNew,
PGIdNew,ShiftNew,ReportToOld,DCompIdOld,DottedOld,DottedReportToCompanyOld,
EmpId,TransferType,ReportToNew,TDate,DCompIdNew,DottedNew,DottedReportToCompanyNew,
UserId,FormId,UserEmpId,UserEmpName,EntTerminal,EntTerminalIP,UserEmpCode,EntOperation,EntDate,TransDate,FutureTransfer)
VALUES('{tblemployee["CompanyId"]}','{tblemployee["DivId"]}','{tblemployee["dptId"]}','{tblemployee["MdptId"]}','{tblemployee["dsgId"]}','{tblemployee["brnID"]}','{tblemployee["JobId"]}',''
,'{tblemployee["MTeamId"]}','{tblemployee["TeamId"]}','{tblemployee["BaseId"]}','{tblemployee["dispCntId"]}','{tblemployee["PGId"]}','{tblemployee["Shift"]}'
,'{tblemployee["DivId"]}','{tblemployee["dptId"]}','{tblemployee["MdptId"]}','{tblemployee["dsgId"]}','{tblemployee["brnID"]}','{tblemployee["JobId"]}',''
,'{tblemployee["MTeamId"]}','{tblemployee["TeamId"]}','{tblemployee["BaseId"]}','{tblemployee["dispCntId"]}','{tblemployee["PGId"]}','{tblemployee["Shift"]}'
,'{tblemployee["ReportTo"]}','{tblemployee["ReportToCompany"]}','{tblemployee["Dotted"]}','{tblemployee["DottedReportToCompany"]}'
,'{item.EmpId}','{TransferType}',{(item.ReportTo.HasValue ? item.ReportTo.Value.ToString() : "NULL")},'{item.TDate}',{(item.ReportToCompany.HasValue ? item.ReportToCompany.Value.ToString() : "NULL")},
{(item.Dotted.HasValue ? item.Dotted.Value.ToString() : "NULL")},{(item.DottedReportToCompany.HasValue ? item.DottedReportToCompany.Value.ToString() : "NULL")},
'{_UserId}','{_FormId}','{_UserEmpId}','{_UserEmpName}','{_EntTerminal}','{_EntTerminalIP}','{_UserEmpCode}','Insert','{date}','{date}','0'
)";
                        _common.ExecuteNonQuery(EmployeeTransaction);
                    }

                    string query1 = @$"UPDATE tblemployee
SET 
    ReportTo ={(item.ReportTo.HasValue ? item.ReportTo.Value.ToString() : "NULL")},
    ReportToCompany ={(item.ReportToCompany.HasValue ? item.ReportToCompany.Value.ToString() : "NULL")},
    Dotted =  {(item.Dotted.HasValue ? item.Dotted.Value.ToString() : "NULL")},
UserId='{_UserId}',
    DottedReportToCompany =  {(item.DottedReportToCompany.HasValue ? item.DottedReportToCompany.Value.ToString() : "NULL")},
 ApplicationId = 'HCMS', FormId = '" + _FormId + "',UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                " UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntDate='" + date + "' " + @$"
WHERE 
    EmpId = {item.EmpId}";

                    string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                          " VALUES ('" + item.EmpId + "','" + OldEmpId + "','" + item.EmpId + "','InterviewDesigner','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


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
        public async Task<string> EffectiveDateValidate(string txtTEFDate, string SelectedCompany)
        {
            try
            {
                if (!_common.CheckDate(txtTEFDate))
                {
                    string error = "Invalid Format";
                    return error;
                }
                if (DateTime.Parse(txtTEFDate) > DateTime.Parse(_common.ExecuteScalarQuery("select dbo.fn_general_getlocaldatetimeCompanyWise(" + SelectedCompany + ")").ToString()))
                {
                    return "‘Transfer Effective From Date’ should be less than or equal to ‘Current Date'.";//Utilities.GetLocalResObjText(FormURL, "WEFDateShouldbeGreaterthanSystemDate", Utilities.GetAppCurrentUICulture());
                }
                string strDateRange = _common.ExecuteScalarQuery("SELECT  LTRIM(RTRIM(PMonth2)) + ' ' + pYear + ' (' + REPLACE(RTRIM(LTRIM(CONVERT(CHAR(11), DateFrom, 106))),' ','/') + ' - ' + REPLACE(RTRIM(LTRIM(CONVERT(CHAR(11), DateTo, 106))),' ','/') + ')' MonthName FROM tblPayrollMonth WHERE CompanyId=" + SelectedCompany + " AND Closed = 0 ");
                string ToDateOfMonth = _common.ExecuteScalarQuery(@$"select DateTo from  tblPayrollMonth where  CompanyId = {SelectedCompany} AND Closed = 0");
                if (string.IsNullOrEmpty(ToDateOfMonth))
                {
                    return "There is no payroll month opened";
                }
                bool ArrearCalculation = Convert.ToBoolean(_common.ExecuteScalarQuery(@$"select ArrearCalculation from  tblhrpolicy where  CompanyId = {SelectedCompany}"));

                if (DateTime.Parse(txtTEFDate) > DateTime.Parse(ToDateOfMonth))
                {
                    return "‘Transfer Effective From Date’ is later than dates of the Current Payroll Month i.e. " + strDateRange;//Utilities.GetLocalResObjText(FormURL, "InvalidWEF", Utilities.GetAppCurrentUICulture()) + strDateRange;
                }
                return string.Empty;
            }
            catch (Exception)
            {
                throw;
            }

        }
        public async Task<DataTable> GetDirectInDirectReport(string OldEmpId, string SelectedCompany)
        {

            string query = @$"SELECT EmpId, EmpCode,
FirstName+' ' + MidName + ' '+LastName as FirstName,
CompanyId,ReportTo,ReportToCompany,Dotted,DottedReportToCompany,
(SELECT CONVERT(VARCHAR(10), DateTo, 23) AS MonthName 
FROM tblPayrollMonth WHERE CompanyId = {SelectedCompany} AND Closed = 0) as TDate 
FROM tblemployee WHERE (ReportTo = " + OldEmpId + " OR Dotted =" + OldEmpId + @$")
And CompanyId=" + SelectedCompany + " and Active = 1";
            return await _common.ExecuteSqlQuery(query);

        }

        public async Task<Dictionary<string, object>> GetHiringChecklist(string OldEmpId, string SelectedCompany)
        {
            try
            {
                var HiringChecklist = new DataTable();
                HiringChecklist = await _common.ExecuteSqlQuery(@$"SELECT * FROM tblHiringChecklistSetup Where CompanyId = {SelectedCompany} And ResponsiblePersonnel =" + OldEmpId);


                var HiringCheckListGrade = new DataTable();
                HiringCheckListGrade = await _common.ExecuteSqlQuery(@$"SELECT DISTINCT 
    a.ChkId,
    a.ResponsiblePersonnel,
	a.CompanyId,
	a.SelectedcompanyId,
    a.GradeId,
    (SELECT HiringChecklistName FROM tblHiringChecklistSetup WHERE HrChkId = a.ChkLstId) AS HiringChecklistName,
    ISNULL((SELECT Name FROM tblSetupsDetail WHERE sdlid = a.NationalityId), '') AS Nationality,
    (
    SELECT ISNULL(
        (SELECT TOP 1 [Name] FROM tblSetupsDetail WHERE smsId = 19 AND sdlid = a.GradeId),
        '404'
    )
) AS Grade FROM tblHiringCheckListGradeMap a WHERE a.CompanyId = {SelectedCompany} And a.ResponsiblePersonnel = " + OldEmpId);

                var HiringChecklistDetail = new DataTable();
                HiringChecklistDetail = await _common.ExecuteSqlQuery(@$"SELECT 
    a.*,
    b.HiringChecklistName,
    dbo.fn_GetDateFormat_DDMMMYYYY(a.TaskPerformedDate) AS TaskPerformDate,
    (SELECT [Name] 
     FROM tblSetupsDetail 
     WHERE smsId = 19 AND sdlid = a.GradeId) AS Grade,
    (SELECT EmpCode 
     FROM tblEmployee 
     WHERE EmpId = hc.EmpId) AS EmpCode,
    ((SELECT 
  ISNULL(FirstName, '') + 
  CASE WHEN MidName IS NOT NULL AND MidName <> '' THEN ' ' + MidName ELSE '' END + 
  CASE WHEN LastName IS NOT NULL AND LastName <> '' THEN ' ' + LastName ELSE '' END AS FullName
FROM 
  tblEmployee
WHERE 
  EmpId = hc.EmpId)) AS EmpName
FROM 
    tblHiringChecklistDetail a
INNER JOIN 
    tblHiringChecklistSetup b ON b.HrChkId = a.ChecklistItemId
INNER JOIN 
    tblHiringChecklist hc ON hc.Hrid = a.HrId
WHERE a.CompanyId = {SelectedCompany} And a.ResponsiblePersonnel = {OldEmpId}  And hc.Finalize = 0 And a.Status = 0 And a.InActive=0 AND hc.EmpId IS NOT NULL");

                var HiringChecklistApplicant = new DataTable();
                HiringChecklistApplicant = await _common.ExecuteSqlQuery(@$"SELECT 
    hc.AppId,a.*,
    b.HiringChecklistName,
    dbo.fn_GetDateFormat_DDMMMYYYY(a.TaskPerformedDate) AS TaskPerformDate,
    (SELECT [Name] FROM tblSetupsDetail WHERE smsId = 19 AND sdlid = a.GradeId) AS Grade,
    (SELECT FirstName + ' ' + MiddleName + ' ' + LastName 
     FROM tblrecbasicinfo 
     WHERE Appid = hc.AppId) AS EmpName,
(SELECT EmpCode 
     FROM tblrecbasicinfo 
     WHERE Appid = hc.AppId) AS EmpCode

FROM 
    tblHiringChecklistDetail a
INNER JOIN 
    tblHiringChecklistSetup b ON b.HrChkId = a.ChecklistItemId
INNER JOIN 
    tblHiringChecklist hc ON hc.Hrid = a.HrId
WHERE 
    a.CompanyId = {SelectedCompany} And a.ResponsiblePersonnel = {OldEmpId}
     And hc.Finalize = 0 And a.Status = 0 And a.InActive=0 AND hc.AppId IS NOT NULL");

                var row = new Dictionary<string, object>
                {
                    { "HiringChecklist", _common.GetJsonDatatable(HiringChecklist) },
                    { "HiringCheckListGrade", _common.GetJsonDatatable(HiringCheckListGrade) },
                    { "HiringChecklistDetail", _common.GetJsonDatatable(HiringChecklistDetail) },
                    { "HiringChecklistApplicant", _common.GetJsonDatatable(HiringChecklistApplicant) }
                };

                return row;
            }
            catch (Exception ex)
            {
                var row = new Dictionary<string, object>
    {
        { "ErrorMessage", ex.Message },
        { "StackTrace", ex.StackTrace },
        { "InnerException", ex.InnerException?.Message ?? "No inner exception" },
        { "Source", ex.Source }
    };
                return row;
            }
        }

        public bool UpdateHiringCheckList([FromBody] HiringCheckList? data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            try
            {

                string CompanyId = SelectedCompany;

                string date = _common.ExecuteScalarQuery("select dbo.fn_general_getlocaldatetimeCompanyWise(" + SelectedCompany + ")");
                if (data != null)
                {
                    if (data?.HiringCheckListGrade != null && data?.HiringCheckListGrade?.Count != 0)
                    {

                        foreach (var item in data.HiringCheckListGrade.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {

                            string tblHiringCheckListGradeMap = @$"UPDATE tblHiringCheckListGradeMap
       SET 
           ResponsiblePersonnel ={item.NewEmp} ,
           Selectedcompanyid = {item.Selectedcompanyid},
           ApplicationCode = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           CompanyId={CompanyId} and ResponsiblePersonnel={OldEmpId} and ChkId={item.ChkId}";

                            string GradeHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                  " VALUES ('" + item.ChkId + "','" + OldEmpId + "','" + item.NewEmp + "','InterviewDesigner','" + CompanyId + "'," + date + ",'" + _UserId + "','" + _FormId + "'," + date + ",'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                            _common.ExecuteNonQuery(tblHiringCheckListGradeMap);
                            _common.ExecuteNonQuery(GradeHistory);
                        }

                    }

                    if (data?.HiringChecklistDetail != null && data?.HiringChecklistDetail?.Count != 0)
                    {

                        foreach (var item in data.HiringChecklistDetail.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {

                            string tblHiringCheckListDetail = @$"UPDATE tblHiringChecklistDetail
       SET 
           ResponsiblePersonnel ={item.NewEmp} ,
           
           ApplicationCode = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           CompanyId={CompanyId} and HrDetailId={item.HrDetailId}";

                            string HiringCheckListDetailHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                  " VALUES ('" + item.HrDetailId + "','" + OldEmpId + "','" + item.NewEmp + "','InterviewDesigner','" + CompanyId + "'," + date + ",'" + _UserId + "','" + _FormId + "'," + date + ",'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                            _common.ExecuteNonQuery(tblHiringCheckListDetail);
                            _common.ExecuteNonQuery(HiringCheckListDetailHistory);
                        }

                    }

                    if (data?.HiringChecklist != null && data?.HiringChecklist?.Count != 0)
                    {

                        foreach (var item in data.HiringChecklist.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {

                            string tblHiringCheckList = @$"UPDATE tblHiringChecklistSetup
       SET 
           ResponsiblePersonnel ={item.NewEmp} ,
           Selectedcompanyid = {item.Selectedcompanyid},
           ApplicationCode = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           CompanyId={CompanyId} and HrChkId={item.HrChkId}";

                            string HiringCheckListHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                  " VALUES ('" + item.HrChkId + "','" + OldEmpId + "','" + item.NewEmp + "','InterviewDesigner','" + CompanyId + "'," + date + ",'" + _UserId + "','" + _FormId + "'," + date + ",'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                            _common.ExecuteNonQuery(tblHiringCheckList);
                            _common.ExecuteNonQuery(HiringCheckListHistory);
                        }

                    }

                    if (data?.HiringChecklistApplicant != null && data?.HiringChecklistApplicant?.Count != 0)
                    {

                        foreach (var item in data.HiringChecklistApplicant.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {

                            string tblHiringChecklistApplicant = @$"UPDATE tblHiringChecklistDetail
       SET 
           ResponsiblePersonnel ={item.NewEmp} ,
           
           ApplicationCode = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           CompanyId={CompanyId} and HrDetailId={item.HrDetailId}";

                            string HiringChecklistApplicantHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                  " VALUES ('" + item.HrDetailId + "','" + OldEmpId + "','" + item.NewEmp + "','InterviewDesigner','" + CompanyId + "'," + date + ",'" + _UserId + "','" + _FormId + "'," + date + ",'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                            _common.ExecuteNonQuery(tblHiringChecklistApplicant);
                            _common.ExecuteNonQuery(HiringChecklistApplicantHistory);
                        }

                    }



                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception();
            }

        }

        public async Task<Dictionary<string, object>> GetExitClearance(string OldEmpId, string SelectedCompany)
        {
            try { 
            JwtArray jwtArray = await _common.GetJwtUser();
            var WorkInterDepartmentPolicy = new DataTable();
            var WorkConcernedDepartmentSetup = new DataTable();
            var WorkAssoctateClearance = new DataTable();
            var queryInterDepartmentPolicy = @$"SELECT Id,Code,Description,linkWithAssets,STId,ResponsiblePersonToProvideExitClearance AS RPerson,
(CASE ResponsiblePersonToProvideExitClearance WHEN '1' THEN 'HoD (Department)' WHEN '2' THEN 'HoD (Sub-Department)' WHEN '3' THEN 'Specific Employee' END) AS ResponsiblePersonToProvideExitClearance,
ItemCompanyId,DivId,MDptId,DptId,EmpId,Active,AssetsCategory,viewjd,
ISNULL((SELECT EmpCode+' - '+FirstName+' '+MidName+' '+LastName 
FROM TblEmployee WHERE EmpId=tblClearanceSetup.EmpId AND CompanyId=tblClearanceSetup.ItemCompanyId AND Active=1),'') EmployeeName,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SdlId=STId),'') AS StatusType,
ISNULL((SELECT CompanyName FROM vCompanyManagement WHERE CCODE=ItemCompanyId),'') AS CompanyName,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=70 AND SdlId=DivId),'') AS Division,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=84 AND SdlId=MDptId),'') AS MainDepartment,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=24 AND SdlId=DptId),'') AS Department,
CONVERT(BIGINT,TimeStamp) AS ChngTimeStamp,

    -- Fixed part for splitting and showing names
    (
        SELECT 
            STUFF((
                SELECT ', ' + LTRIM(RTRIM(sd.Name))
                FROM (
                    SELECT CAST(x.value('.', 'INT') AS INT) AS SdlId
                    FROM (
                        SELECT CAST('<i>' + REPLACE(tblClearanceSetup.AssetsCategory, ',', '</i><i>') + '</i>' AS XML) AS xmlval
                    ) AS A
                    CROSS APPLY xmlval.nodes('/i') AS Split(x)
                ) AS splitValues
                INNER JOIN tblSetupsDetail sd 
                    ON sd.SdlId = splitValues.SdlId
                WHERE sd.smsid = 23
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    ) AS AssetsCategoryNames FROM tblClearanceSetup WHERE CompanyId={SelectedCompany} and EmpId={OldEmpId}";
            var queryConcernedDepartmentSetup = @$"SELECT 
    Id,Code,Description,linkWithAssets,STId,divId, Grade,
    DptId, MDptId,DptHOD,MDptHOD,ReportTo,EmpId,
    ISNULL(
        (
            SELECT FirstName+' '+MidName+' '+LastName 
			FROM TblEmployee
            WHERE 
                EmpId = tblConcernedDepartmentSetup.EmpId 
              
        ), ''
    ) AS EmployeeName,
	ISNULL(
        (
		SELECT CompanyId
			FROM TblEmployee
            WHERE 
                EmpId = tblConcernedDepartmentSetup.EmpId 
                
           
        ), ''
    ) AS ItemCompanyId,
    (
        SELECT CompanyName 
        FROM vCompanyManagement 
        WHERE CCODE = tblConcernedDepartmentSetup.HeadCompanyId
    ) AS HeadCompany,
    ISNULL(
        (
            SELECT Name 
            FROM tblSetupsDetail 
            WHERE SdlId = STId
        ), ''
    ) AS StatusType,
    Active,
    CompanyId,
    HeadCompanyId,
    AssetsCategory,
    viewjd,
    CONVERT(BIGINT, TimeStamp) AS ChngTimeStamp,
	ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=70 AND SdlId=divId),'') AS Division,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=84 AND SdlId=MDptId),'') AS MainDepartment,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=24 AND SdlId=DptId),'') AS Department,
ISNULL((SELECT Name FROM tblSetupsDetail WHERE SmsId=19 AND SdlId=Grade),'') AS GradeName,

    -- Fixed part for splitting and showing names
    (
        SELECT 
            STUFF((
                SELECT ', ' + LTRIM(RTRIM(sd.Name))
                FROM (
                    SELECT CAST(x.value('.', 'INT') AS INT) AS SdlId
                    FROM (
                        SELECT CAST('<i>' + REPLACE(tblConcernedDepartmentSetup.AssetsCategory, ',', '</i><i>') + '</i>' AS XML) AS xmlval
                    ) AS A
                    CROSS APPLY xmlval.nodes('/i') AS Split(x)
                ) AS splitValues
                INNER JOIN tblSetupsDetail sd 
                    ON sd.SdlId = splitValues.SdlId
                WHERE sd.smsid = 23
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    ) AS AssetsCategoryNames
FROM 
    tblConcernedDepartmentSetup where EmpId={OldEmpId} and HeadCompanyId= {jwtArray.UserCompanyID}";
            var queryAssoctateClearance = @$"  SELECT 
    c.*,
    e.EmpCode,
    e.FirstName + ' ' + ISNULL(e.MidName + ' ', '') + e.LastName as fullname,
    a.AssetNo, a.RegNumber, a.AssetDesc,
    a.AssetID,
    cat.Name as AssetCategory,
   app.CompanyId as ApproverCompanyId      
FROM tblAssoctateClearance AS c
LEFT JOIN tblEmployee AS e ON e.EmpID = c.EmpID
LEFT JOIN tblAssetDetail AS a ON a.AssetID = c.AssetID
LEFT JOIN tblSetupsDetail as cat ON a.CatId = cat.sdlid AND cat.smsid = 23
LEFT JOIN tblEmployee AS app ON app.EmpId = c.ApproverID
WHERE c.CompanyID = {jwtArray.UserCompanyID}
  AND c.ApproverID = '{OldEmpId}'
  AND NOT EXISTS (
      SELECT 1 
      FROM tblAssoctateClearanceDetail AS d 
      WHERE d.Id = c.Id AND d.ClearanceStatus = 2
  )";
            WorkInterDepartmentPolicy = await _common.ExecuteSqlQuery(queryInterDepartmentPolicy);
            WorkConcernedDepartmentSetup = await _common.ExecuteSqlQuery(queryConcernedDepartmentSetup);
            WorkAssoctateClearance = await _common.ExecuteSqlQuery(queryAssoctateClearance);
            var row = new Dictionary<string, object>
                {
                    { "InterDepartmentPolicy", _common.GetJsonDatatable(WorkInterDepartmentPolicy) },
                    { "ConcernedDepartmentSetup", _common.GetJsonDatatable(WorkConcernedDepartmentSetup) },
                    { "AssoctateClearance", _common.GetJsonDatatable(WorkAssoctateClearance) },
                };
            return row;

        }
            catch (Exception ex)
            {
                var row = new Dictionary<string, object>
    {
        { "ErrorMessage", ex.Message },
        { "StackTrace", ex.StackTrace },
        { "InnerException", ex.InnerException?.Message ?? "No inner exception" },
        { "Source", ex.Source }
    };
                return row;
            }
}
        public async Task<bool> UpdateExitClearance([FromBody] ExitClearance data)
        {
            JwtArray jwtArray = await _common.GetJwtUser();

            string date = _common.ExecuteScalarQuery("select dbo.fn_general_getlocaldatetimeCompanyWise(" + jwtArray.UserCompanyID + ")");
            if (data != null)
            {
                if (data?.InterDepartmentPolicy != null && data?.InterDepartmentPolicy?.Count != 0)
                {
                    foreach (var item in data.InterDepartmentPolicy.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                    {

                        string tblInterDepartmentPolicy = @$"UPDATE tblClearanceSetup
       SET 
           EmpId ={item.NewEmp},
           ItemCompanyId = {item.ItemCompanyId},
           ApplicationCode = 'HCMS', 
           FormId = '{jwtArray.FormId}',
           UserId = '{jwtArray.UserID}',           
            UserEmpId = '{jwtArray.UserEmpID}',
           UserEmpName = '{jwtArray.UserEmpName}',
           UserEmpCode = '{jwtArray.UserEmpCode}',
           EntTerminal='{jwtArray.EntTerminal}',
           EntTerminalIP='{jwtArray.EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           Id={item.Id}";

                        string InterDepartmentPolicyHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                              " VALUES ('" + item.NewEmp + "','" + item.EmpId + "','" + item.NewEmp + "','tblClearanceSetup','" + jwtArray.UserCompanyID + "'," + date + ",'" + jwtArray.UserID + "','" + jwtArray.FormId + "'," + date + ",'Update','" + jwtArray.UserEmpID + "','" + jwtArray.UserEmpCode + "','" + jwtArray.UserEmpName + "')";

                        _common.ExecuteNonQuery(tblInterDepartmentPolicy);
                        _common.ExecuteNonQuery(InterDepartmentPolicyHistory);
                    }
                }
                if (data?.ConcernedDepartmentSetup != null && data?.ConcernedDepartmentSetup?.Count != 0)
                {
                    foreach (var item in data.ConcernedDepartmentSetup.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                    {

                        string tblInterDepartmentPolicy = @$"UPDATE tblConcernedDepartmentSetup
       SET 
           EmpId ={item.NewEmp},
           ApplicationCode = 'HCMS', 
           FormId = '{jwtArray.FormId}',
           UserId = '{jwtArray.UserID}',           
            UserEmpId = '{jwtArray.UserEmpID}',
           UserEmpName = '{jwtArray.UserEmpName}',
           UserEmpCode = '{jwtArray.UserEmpCode}',
           EntTerminal='{jwtArray.EntTerminal}',
           EntTerminalIP='{jwtArray.EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           Id={item.Id}";

                        string InterDepartmentPolicyHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                              " VALUES ('" + item.NewEmp + "','" + item.EmpId + "','" + item.NewEmp + "','tblConcernedDepartmentSetup','" + jwtArray.UserCompanyID + "'," + date + ",'" + jwtArray.UserID + "','" + jwtArray.FormId + "'," + date + ",'Update','" + jwtArray.UserEmpID + "','" + jwtArray.UserEmpCode + "','" + jwtArray.UserEmpName + "')";

                        _common.ExecuteNonQuery(tblInterDepartmentPolicy);
                        _common.ExecuteNonQuery(InterDepartmentPolicyHistory);
                    }
                }
                if (data?.AssoctateClearance != null && data?.AssoctateClearance?.Count != 0)
                {
                    foreach (var item in data.AssoctateClearance.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                    {

                        string tblAssoctateClearance = @$"UPDATE tblAssoctateClearance
       SET 
           ApproverID ={item.NewEmp},
          
           ApplicationCode = 'HCMS', 
           FormId = '{jwtArray.FormId}',
           UserId = '{jwtArray.UserID}',           
           UserEmpId = '{jwtArray.UserEmpID}',
           UserEmpName = '{jwtArray.UserEmpName}',
           UserEmpCode = '{jwtArray.UserEmpCode}',
           EntTerminal='{jwtArray.EntTerminal}',
           EntTerminalIP='{jwtArray.EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           Id={item.Id}";
                        string tblAssoctateClearanceDetail = @$"UPDATE tblAssoctateClearanceDetail
       SET 
           ApproverID ={item.NewEmp},
          
           ApplicationCode = 'HCMS', 
           FormId = '{jwtArray.FormId}',
           UserId = '{jwtArray.UserID}',           
           UserEmpId = '{jwtArray.UserEmpID}',
           UserEmpName = '{jwtArray.UserEmpName}',
           UserEmpCode = '{jwtArray.UserEmpCode}',
           EntTerminal='{jwtArray.EntTerminal}',
           EntTerminalIP='{jwtArray.EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           Id={item.Id}";



                        string AssoctateClearanceHistory = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                              " VALUES ('" + item.NewEmp + "','" + item.EmpId + "','" + item.NewEmp + "','tblAssoctateClearance','" + jwtArray.UserCompanyID + "'," + date + ",'" + jwtArray.UserID + "','" + jwtArray.FormId + "'," + date + ",'Update','" + jwtArray.UserEmpID + "','" + jwtArray.UserEmpCode + "','" + jwtArray.UserEmpName + "')";

                        _common.ExecuteNonQuery(tblAssoctateClearance);
                        _common.ExecuteNonQuery(tblAssoctateClearanceDetail);
                        _common.ExecuteNonQuery(AssoctateClearanceHistory);
                    }
                }
            }
            return true;
        }
        public async Task<Dictionary<string, object>> GetWorkFlows(string OldEmpId, string SelectedCompany)
        {

            try {
                JwtArray jwtArray = await _common.GetJwtUser();
                var WorkCoreSeparately = new DataTable();
            var WorkCoreDetail = new DataTable();
            var WorkLeave = new DataTable();
            var WorkEmployeePersonalP = new DataTable();
            var WorkIntraCompanyTransfer = new DataTable();
            var WorkManPowerRequisit = new DataTable();
            var WorkTimeAdjustment = new DataTable();
            var WorkExpenseTransaction = new DataTable();
            var WorkPerformanceReview = new DataTable();
            var WorkProbationEvaluation = new DataTable();
            var WorkSeparation = new DataTable();
            var WorkPMAppraisal = new DataTable();
            var WorkTrainingNomination = new DataTable();
            var WorkTrainingRecord = new DataTable();
            var WorkRPEntry = new DataTable();
            var WorkOTEntry = new DataTable();
            var WorkManpowerBudget = new DataTable();
            var WorkEmployeeLoanMaster = new DataTable();
            var WorkAppointmentLetterWf = new DataTable();
            var WorkRecPositionApplied = new DataTable();


            var queryseparately = @$"SELECT 
    WFSDA.id,WFS.WFlowTypeId,COALESCE(WFSDA.CompanyId, WFS.CompanyId) AS CompanyId,COALESCE(CM.CompanyName, WFS.CompanyName) AS CompanyName,
		WFSDA.DefaultAuthorityPolicyValueId,COALESCE(WFS.DefaultAuthorityPolicyName+', '+SD.Name,'Company') AS Name,WFS.DefaultApproverCompany,COALESCE(WFSDA.EmployeeId, WFS.EmpId) AS EmployeeId,E.FirstName,WFS.Name as WFlowType
FROM 
    vWorkFlowSetup WFS
LEFT JOIN 
    tblWorkFlowSetupDefaultAuthority WFSDA

	INNER JOIN vCompanyManagement AS CM
		ON CM.CCODE = WFSDA.CompanyId

		LEFT JOIN tblSetupsDetail AS SD 
		ON WFSDA.DefaultAuthorityPolicyValueId = SD.sdlid
		--AND WFSDA.DefaultAuthorityPolicyId = SD.smsid 
		AND WFSDA.DefaultAuthorityPolicyValueId = SD.sdlid
		
		LEFT JOIN tblEmployee AS E
		ON WFSDA.EmployeeId = E.EmpId	

		INNER JOIN [tblSetupsDetail] AS workname
		ON workname.smsid=85 and workname.sdlid = WFSDA.WFlowTypeId
ON 
    WFS.WFlowTypeId = WFSDA.WFlowTypeId
	 
	 where WFS.Companyid = {jwtArray.UserCompanyID} and (WFS.EmpId = {OldEmpId} Or WFSDA.EmployeeId = {OldEmpId})";

            WorkCoreSeparately = await _common.ExecuteSqlQuery(queryseparately);


            var querydetail = @$"DECLARE @sql NVARCHAR(MAX)
 
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'tblWorkFlow' AND COLUMN_NAME = 'PrimarySetupId'
)
BEGIN
    SET @sql = '
        SELECT
            wfId,
            EmpId,
            CompanyId,
            FinalAuthorityType,
            FinalAuthorityEmpId,
            FinalAuthorityEmpCompId,
            (
                SELECT Name
                FROM tblSetupsDetail
                WHERE sdlid = typeId
            ) AS typeIdName,
            (
                SELECT Name
                FROM tblSetupsDetail
                WHERE sdlid = PrimarySetupId
            ) AS PrimarySetupName
        FROM
            tblWorkFlow
        WHERE
            companyId = {jwtArray.UserCompanyID} and FinalAuthorityType = ''Employee'' and FinalAuthorityEmpId ={OldEmpId}
    '
END
ELSE
BEGIN
    SET @sql = '
        SELECT
            wfId,
            EmpId,
            CompanyId,
            FinalAuthorityType,
            FinalAuthorityEmpId,
            FinalAuthorityEmpCompId,
            (
                SELECT Name
                FROM tblSetupsDetail
                WHERE sdlid = typeId
            ) AS typeIdName,
            ''designation'' AS PrimarySetupName
        FROM
            tblWorkFlow
        WHERE
            companyId = {jwtArray.UserCompanyID} and FinalAuthorityType = ''Employee'' and FinalAuthorityEmpId ={OldEmpId}
    '
END
 
EXEC sp_executesql @sql";

            WorkCoreDetail = await _common.ExecuteSqlQuery(querydetail);



            var queryLeave = @$"SELECT 
    el.LeaveStatus,      
	emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,               
    ls.Name AS LeaveName,
	el.DateFrom,
	el.DateTo,
	el.EmpLeaveId,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID
	
FROM 
    TblEmpLeave el
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = el.EmpLeaveId
LEFT JOIN 
    tblLeaveSetup ls 
ON 
    ls.Code = el.LCode 
    AND ls.CompanyId = el.CompanyId
LEFT JOIN 
    tblEmployee emp 
ON 
    el.EmpId = emp.EmpId 
WHERE 
    wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=1)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND el.CompanyId = {jwtArray.UserCompanyID}";
            WorkLeave = await _common.ExecuteSqlQuery(queryLeave);


            var queryEmployeePersonalP = @$"SELECT 
        
	emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,               
    ep.*,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID
	
FROM 
    tblEmployeeprofile ep
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = ep.Req_Id
LEFT JOIN 
    tblEmployee emp 
ON 
    ep.InitiatedBy = emp.EmpId 
WHERE 
    wfs.Authority_EmpId = {OldEmpId} 
	AND wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=17)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND  wfs.AuthorityStatus = 'P'
    AND ep.CompanyId = {jwtArray.UserCompanyID}";
            WorkEmployeePersonalP = await _common.ExecuteSqlQuery(queryEmployeePersonalP);


            var queryIntraCompanyTransfer = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,               
    tIc.*,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID
FROM 
    tblIntraCompanyTransfer tIc
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = tIc.EntryId
LEFT JOIN 
    tblEmployee emp 
ON 
    tIc.EmpId = emp.EmpId 
WHERE 
wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=21)
    AND wfs.Authority_EmpId = {OldEmpId} 
And emp.EmpId IS NOT NULL
    AND wfs.Authority_CompanyID = {SelectedCompany}
     AND wfs.AuthorityStatus = 'P'
	
    AND tIc.CompanyId = {jwtArray.UserCompanyID}";
            WorkIntraCompanyTransfer = await _common.ExecuteSqlQuery(queryIntraCompanyTransfer);


            var queryManPowerRequisit = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    Jobman.JobTitle,
    Jobman.MPRId,
    Jobman.RequisitionStatus,
    Jobman.RequisitionDate,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId
FROM 
    HCMS_JobPortal.dbo.tblManPowerRequisitionDetail Jobman
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = Jobman.MPRId
LEFT JOIN 
    tblEmployee emp 
ON 
    Jobman.EmpId = emp.EmpId 
WHERE 
    wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=2)
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.AuthorityStatus = 'P'
And Jobman.RequisitionStatus ='P'
    AND Jobman.CompanyId = {jwtArray.UserCompanyID}";
            WorkManPowerRequisit = await _common.ExecuteSqlQuery(queryManPowerRequisit);


            var queryTimeAdjustment = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    TimeAdjustment.AdjReason,
    TimeAdjustment.TimeAdjId,
    TimeAdjustment.RequisitionStatus,
    TimeAdjustment.AdjDate,
    c.AttDate,
    wfs.ForwardingDate,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId
FROM 
    tblTimeAdjustment TimeAdjustment
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = TimeAdjustment.TimeAdjId
LEFT JOIN 
    tblEmployee emp 
ON 
    TimeAdjustment.EmpId = emp.EmpId 
LEFT JOIN 
    tblAttendance c 
ON 
    c.Id = TimeAdjustment.AttId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=14)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND TimeAdjustment.CompanyId = {jwtArray.UserCompanyID}";
            WorkTimeAdjustment = await _common.ExecuteSqlQuery(queryTimeAdjustment);


            var queryExpenseTransaction = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    expense.Balance,
    expense.VFAllowanceTransId,
    expense.RequisitionStatus,
    expense.TransactionDate,
expense.AmtClaimed,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId,
	(select [Description] as AllowanceName from TblVariableFixedAllowanceSetup where id=expense.VfAllowId) as AllowanceName
FROM 
    TblVariableFixedAllowanceTransaction expense
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = expense.VFAllowanceTransId
LEFT JOIN 
    tblEmployee emp 
ON 
    expense.EmpId = emp.EmpId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=15)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND expense.CompanyId = {jwtArray.UserCompanyID}";
            WorkExpenseTransaction = await _common.ExecuteSqlQuery(queryExpenseTransaction);


            var queryPerformanceReview = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    PerformanceReview.Frequency,
    PerformanceReview.RId,
    PerformanceReview.RequisitionStatus,
    PerformanceReview.CreateOn,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId,
	(Select cast(dbo.fn_GetDateFormat_DDMMMYYYY(FromDate) as varchar) from tblAppraisalPeriodDetail where CompanyId = PerformanceReview.CompanyId and id = PerformanceReview.RFromId) + ' - ' +
					(Select cast(dbo.fn_GetDateFormat_DDMMMYYYY(ToDate) as varchar) from tblAppraisalPeriodDetail where CompanyId = PerformanceReview.CompanyId and id = PerformanceReview.RToId)						
					as ReviewPeriod
FROM 
    tblPMPerformanceReview PerformanceReview
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = PerformanceReview.RId
LEFT JOIN 
    tblEmployee emp 
ON 
    PerformanceReview.EmpId = emp.EmpId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=11)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND PerformanceReview.CompanyId = {jwtArray.UserCompanyID}";
            WorkPerformanceReview = await _common.ExecuteSqlQuery(queryPerformanceReview);


            var queryProbationEvaluation = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    ProbationEvaluation.PId,
    ProbationEvaluation.RequisitionStatus,
    ProbationEvaluation.FromDate,
    ProbationEvaluation.ToDate,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId
FROM 
    tblPMProbationEvaluation ProbationEvaluation
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = ProbationEvaluation.PId
LEFT JOIN 
    tblEmployee emp 
ON 
    ProbationEvaluation.EmpId = emp.EmpId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=10)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND ProbationEvaluation.CompanyId = {jwtArray.UserCompanyID}";
            WorkProbationEvaluation = await _common.ExecuteSqlQuery(queryProbationEvaluation);


            var querySeparation = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    Separation.Sepid,
    Separation.STATUS,
    Separation.ResigEffectDate As LeavingDate,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId
FROM 
    tblSeparation Separation
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = Separation.Sepid
LEFT JOIN 
    tblEmployee emp 
ON 
    Separation.EmpId = emp.EmpId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=7)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND Separation.CompanyId = {jwtArray.UserCompanyID}";
            WorkSeparation = await _common.ExecuteSqlQuery(querySeparation);


            var queryPMAppraisal = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    PMAppraisal.AId,
    PMAppraisal.RequisitionStatus,
    PMAppraisal.CreateOn,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId,
	(Select TOP 1 cast(dbo.fn_GetDateFormat_DDMMMYYYY(FromDate) as varchar) from tblAppraisalPeriod where APId = PMAppraisal.APId) + ' - ' +
				(Select TOP 1 cast(dbo.fn_GetDateFormat_DDMMMYYYY(ToDate) as varchar) from tblAppraisalPeriodDetail where APId = PMAppraisal.APId)						
				as AppraisalPeriod
FROM 
    tblPMAppraisal PMAppraisal
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = PMAppraisal.AId
LEFT JOIN 
    tblEmployee emp 
ON 
    PMAppraisal.EmpId = emp.EmpId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=4)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND PMAppraisal.CompanyId = {jwtArray.UserCompanyID}";
            WorkPMAppraisal = await _common.ExecuteSqlQuery(queryPMAppraisal);


            var queryTrainingNomination = @$"SELECT 
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    TrainingNomination.Id,
    TrainingNomination.ApprovalStatus,
    TrainingNomination.FromDate,
    TrainingNomination.toDate,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID,
	Wfs.WorkflowTypeId
FROM 
    tblTrainingNomination TrainingNomination
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = TrainingNomination.Id
LEFT JOIN 
    tblEmployee emp 
ON 
    TrainingNomination.EmpId = emp.EmpId 
WHERE 
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=3)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId} 
    AND wfs.AuthorityStatus = 'P'
    AND TrainingNomination.CompanyId = {jwtArray.UserCompanyID}";
            WorkTrainingNomination = await _common.ExecuteSqlQuery(queryTrainingNomination);


            var queryTrainingRecord = @$"SELECT
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    TrainingRecord.TPID,
    TrainingRecord.TrainingPlanApprovalStatus,
    TrainingRecord.FromDate,
    TrainingRecord.toDate,
wfs.Authority_CompanyID,
wfs.Authority_EmpId,
wfs.Id as WorkflowID,
Wfs.WorkflowTypeId
FROM
    tblTrainingRecord TrainingRecord
LEFT JOIN
    tblWorkFlowStatus wfs
ON
    wfs.ParentRecordId = TrainingRecord.TPID
LEFT JOIN
    tblEmployee emp
ON
    TrainingRecord.EmpId = emp.EmpId
WHERE
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=18)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId}
    AND wfs.AuthorityStatus = 'P'
    AND TrainingRecord.CompanyId = {jwtArray.UserCompanyID}";
            WorkTrainingRecord = await _common.ExecuteSqlQuery(queryTrainingRecord);


            var queryRPEntry = @$"SELECT
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    RPEntry.RpEntryId,
    RPEntry.RequisitionStatus,
    RPEntry.EntryDate,
wfs.Authority_CompanyID,
wfs.Authority_EmpId,
wfs.Id as WorkflowID,
Wfs.WorkflowTypeId
FROM
    tblRPEntry RPEntry
LEFT JOIN
    tblWorkFlowStatus wfs
ON
    wfs.ParentRecordId = RPEntry.RpEntryId
LEFT JOIN
    tblEmployee emp
ON
    RPEntry.EmpId = emp.EmpId
WHERE
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=20)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId}
    AND wfs.AuthorityStatus = 'P'
    AND RPEntry.CompanyId = {jwtArray.UserCompanyID}";
            WorkRPEntry = await _common.ExecuteSqlQuery(queryRPEntry);


            var queryOTEntry = @$"SELECT
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    OTEntry.OTEntryId,
    OTEntry.RequisitionStatus,
    OTEntry.FromDate,
    OTEntry.ToDate,
wfs.Authority_CompanyID,
wfs.Authority_EmpId,
wfs.Id as WorkflowID,
Wfs.WorkflowTypeId
FROM
    tblOTEntry OTEntry
LEFT JOIN
    tblWorkFlowStatus wfs
ON
    wfs.ParentRecordId = OTEntry.OTEntryId
LEFT JOIN
    tblEmployee emp
ON
    OTEntry.EmpId = emp.EmpId
WHERE
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=9)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId}
    AND wfs.AuthorityStatus = 'P'
    AND OTEntry.CompanyId = {jwtArray.UserCompanyID}";
            WorkOTEntry = await _common.ExecuteSqlQuery(queryOTEntry);


            var queryManpowerBudget = @$"SELECT
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    ManpowerBudgetMaster.Id,
    ManpowerBudgetMaster.RequisitionStatus,
    ManpowerBudgetMaster.EntDate,
		(select StartDate from TblFiscalYearSetup where FYID = ManpowerBudgetMaster.FYID) RequestRaisedFiscalYearStart,
				(select EndDate from TblFiscalYearSetup where FYID = ManpowerBudgetMaster.FYID) RequestRaisedFiscalYearEnd,
wfs.Authority_CompanyID,
wfs.Authority_EmpId,
wfs.Id as WorkflowID,
Wfs.WorkflowTypeId
FROM
    TblManpowerBudgetMaster ManpowerBudgetMaster
LEFT JOIN
    tblWorkFlowStatus wfs
ON
    wfs.ParentRecordId = ManpowerBudgetMaster.Id
LEFT JOIN
    tblEmployee emp
ON
    wfs.Request_RaisedBy = emp.EmpId
WHERE
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=5)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId}
    AND wfs.AuthorityStatus = 'P'
    AND ManpowerBudgetMaster.CompanyId = {jwtArray.UserCompanyID}";
            WorkManpowerBudget = await _common.ExecuteSqlQuery(queryManpowerBudget);


            var queryEmployeeLoanMaster = @$"SELECT
emp.FirstName+ ' ' + emp.MidName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,
    EmployeeLoanMaster.EmpLoanId,
    wfs.AuthorityStatus as RequestStatus,
    EmployeeLoanMaster.ApplicationDate,
wfs.Authority_CompanyID,
wfs.Authority_EmpId,
wfs.Id as WorkflowID,
Wfs.WorkflowTypeId
FROM
    tblEmployeeLoanMaster EmployeeLoanMaster
LEFT JOIN
    tblWorkFlowStatus wfs
ON
    wfs.ParentRecordId = EmployeeLoanMaster.EmpLoanId
LEFT JOIN
    tblEmployee emp
ON
    EmployeeLoanMaster.EmpId = emp.EmpId
WHERE
 wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=22)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.Authority_EmpId = {OldEmpId}
    AND wfs.AuthorityStatus = 'P'
    AND EmployeeLoanMaster.CompanyId = {jwtArray.UserCompanyID}";
            WorkEmployeeLoanMaster = await _common.ExecuteSqlQuery(queryEmployeeLoanMaster);


                var queryAppointmentLetterWf = @$"SELECT 
	emp.FirstName+ ' ' + emp.MiddleName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,               
    appWf.*,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID
	,ISNULL(
            FORMAT(
                TRY_CAST(dbo.fngetDecryptData(
                    CAST(emp.AppId AS VARCHAR) + CAST(emp.CompanyId AS VARCHAR), 
                    s.GrossSalary
                ) AS DECIMAL(18, 2)), 
                'N2'
            ), 'N/A'
        ) AS 'Recommended Salary',
		(SELECT Top 1 Name FROM tblSetupsDetail WHERE sdlid = emp.dsgId) AS 'Recommended Designation',
		emp.Email
FROM 
    tblAppointmentLetterWf appWf
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = appWf.tblOfferLetterWfId
LEFT JOIN 
    tblRecBasicInfo emp 
ON 
    emp.AppId = appWf.ApplicantId 
LEFT JOIN 
    tblAppSalarySetup s 
ON 
    emp.AppId = s.AppId AND emp.CompanyId = s.CompanyId
WHERE 
    wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=23)
    AND wfs.Authority_CompanyID = {SelectedCompany}
    AND wfs.AuthorityStatus = 'P'
    AND wfs.Authority_EmpId = {OldEmpId}
	AND emp.CompanyID = {jwtArray.UserCompanyID}";
                WorkAppointmentLetterWf = await _common.ExecuteSqlQuery(queryAppointmentLetterWf);


                var queryRecPositionApplied = @$"SELECT 
	emp.FirstName+ ' ' + emp.MiddleName + ' ' + emp.LastName AS EmployeeFullName,
    wfs.WorkflowTypeName,  
    wfs.AuthorityStatus,               
	
    hiring.*,
	wfs.Authority_CompanyID,
	wfs.Authority_EmpId,
	wfs.Id as WorkflowID
	,ISNULL(
            FORMAT(
                TRY_CAST(dbo.fngetDecryptData(
                    CAST(emp.AppId AS VARCHAR) + CAST(emp.CompanyId AS VARCHAR), 
                    s.GrossSalary
                ) AS DECIMAL(18, 2)), 
                'N2'
            ), 'N/A'
        ) AS 'Recommended Salary',
		(SELECT Top 1 Name FROM tblSetupsDetail WHERE sdlid = emp.dsgId) AS 'Recommended Designation',
		emp.Email
FROM 
    [HCMS_JobPortal].dbo.tblRecPositionApplied hiring
LEFT JOIN 
    tblWorkFlowStatus wfs 
ON 
    wfs.ParentRecordId = hiring.Id
LEFT JOIN 
    tblRecBasicInfo emp 
ON 
    emp.AppId = hiring.AppId 
LEFT JOIN 
    tblAppSalarySetup s 
ON 
    emp.AppId = s.AppId AND emp.CompanyId = s.CompanyId
WHERE 
    wfs.WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={jwtArray.UserCompanyID} and cast(code as int)=25)
    AND wfs.Authority_CompanyID = {SelectedCompany}
   AND wfs.AuthorityStatus = 'P'
   AND wfs.Authority_EmpId = {OldEmpId}
    AND hiring.CompanyID = {jwtArray.UserCompanyID}";
                WorkRecPositionApplied = await _common.ExecuteSqlQuery(queryRecPositionApplied);


            var row = new Dictionary<string, object>
                {
                    { "WorkCoreSeparately", _common.GetJsonDatatable(WorkCoreSeparately) },
                    { "WorkCoreDetail", _common.GetJsonDatatable(WorkCoreDetail) },
                    { "WorkLeave", _common.GetJsonDatatable(WorkLeave) },
                    { "WorkEmployeePersonalP", _common.GetJsonDatatable(WorkEmployeePersonalP) },
                    { "WorkIntraCompanyTransfer", _common.GetJsonDatatable(WorkIntraCompanyTransfer) },
                    { "WorkManPowerRequisit", _common.GetJsonDatatable(WorkManPowerRequisit) },
                    { "WorkTimeAdjustment", _common.GetJsonDatatable(WorkTimeAdjustment) },
                    { "WorkExpenseTransaction", _common.GetJsonDatatable(WorkExpenseTransaction) },
                    { "WorkPerformanceReview", _common.GetJsonDatatable(WorkPerformanceReview) },
                    { "WorkProbationEvaluation", _common.GetJsonDatatable(WorkProbationEvaluation) },
                    { "WorkSeparation", _common.GetJsonDatatable(WorkSeparation) },
                    { "WorkPMAppraisal", _common.GetJsonDatatable(WorkPMAppraisal) },
                    { "WorkTrainingNomination", _common.GetJsonDatatable(WorkTrainingNomination) },
                    { "WorkTrainingRecord", _common.GetJsonDatatable(WorkTrainingRecord) },
                    { "WorkRPEntry", _common.GetJsonDatatable(WorkRPEntry) },
                    { "WorkOTEntry", _common.GetJsonDatatable(WorkOTEntry) },
                    { "WorkManpowerBudget", _common.GetJsonDatatable(WorkManpowerBudget) },
                    { "WorkEmployeeLoanMaster", _common.GetJsonDatatable(WorkEmployeeLoanMaster) },
                    { "WorkAppointmentLetterWf", _common.GetJsonDatatable(WorkAppointmentLetterWf) },
                    { "WorkRecPositionApplied", _common.GetJsonDatatable(WorkRecPositionApplied) }

                };

            return row;
            }
            catch (Exception ex)
            {
                var row = new Dictionary<string, object>
    {
        { "ErrorMessage", ex.Message },
        { "StackTrace", ex.StackTrace },
        { "InnerException", ex.InnerException?.Message ?? "No inner exception" },
        { "Source", ex.Source }
    };
                return row;
            }
        }


        public async Task<bool> UpdateWorkFlows([FromBody] WorkFlows? data, string SelectedCompany, string OldEmpId, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            try
            {

                string CompanyId = SelectedCompany;
                string date = _common.ExecuteScalarQuery("select dbo.fn_general_getlocaldatetimeCompanyWise(" + SelectedCompany + ")");

                if (data != null)
                {
                    if (data?.WorkCoreSeparately != null && data?.WorkCoreSeparately?.Count != 0)
                    {
                       
                        foreach (var item in data.WorkCoreSeparately.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                Console.WriteLine("Updating with ID:" + item.NewEmp);
                                //int OldEmpId = Convert.ToInt32(_common.ExecuteScalarQuery("select EmpId from tblHodSetup where HodId = " + item.HodId + " "));

                                //string query1 = " update tblHodSetup set EmpId = " + item.EmpID + ", ApplicationId = 'HCMS', FormId = '" + _FormId + "',UserEmpId = '" + _UserEmpId + "',UserEmpName = '" + _UserEmpName + "'," +
                                //" UserEmpCode = '" + _UserEmpCode + "',EntTerminal='" + _EntTerminal + "',EntTerminalIP='" + _EntTerminalIP + "',EntOperation='Update',EntDate=GetDate()   where HodId = " + item.HodId + "";
                                //string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                //                " VALUES ('" + item.HodId + "','" + OldEmpId + "','" + item.EmpID + "','HoD','" + CompanyId + "',GetDate(),'" + _UserId + "','" + _FormId + "',GetDate(),'Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";
                                if (item.Id is null)
                                {
                                    string WorkFlow = @$"UPDATE TblHRPolicyDetail
       SET 
           DefaultEmployeeCompanyId = {item.CompanyId},
           EmpId ={item.NewEmp} ,
           DefaultApprover ={item.NewEmp} ,
           ApplicationId = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       where 
           EmpId={item.EmployeeId} and WFlowTypeId = {item.WFlowTypeId} and Companyid={CompanyId}";


                                    string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                          " VALUES ('" + item.Id + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + item.CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                    _common.ExecuteNonQuery(WorkFlow);
                                    _common.ExecuteNonQuery(query2);
                                }
                                else
                                {

                                    string WorkFlow = @$"UPDATE tblWorkFlowSetupDefaultAuthority
       SET 
           EmployeeId ={item.NewEmp} ,
           CompanyId = {item.CompanyId},
           ApplicationId = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       WHERE 
           EmployeeId={OldEmpId} and id={item.Id}";


                                    string query2 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                          " VALUES ('" + item.Id + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + item.CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                    _common.ExecuteNonQuery(WorkFlow);
                                    _common.ExecuteNonQuery(query2);
                                }
                            }
                        }
                    }

                    if (data?.WorkCoreDetail != null && data?.WorkCoreDetail?.Count != 0)
                    {
                        foreach (var item in data.WorkCoreDetail.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                string WorkCoreDetail = @$"UPDATE tblWorkFlow
       SET 
           FinalAuthorityEmpCompId = {item.EmpCompId},
           FinalAuthorityEmpId ={item.NewEmp} ,
           ApplicationId = 'HCMS', 
           FormId = '{_FormId}',
           UserEmpId = '{_UserEmpId}',
           UserEmpName = '{_UserEmpName}',
           UserEmpCode = '{_UserEmpCode}',
           EntTerminal='{_EntTerminal}',
           EntTerminalIP='{_EntTerminalIP}',
           EntDate='{date}',
           EntOperation = 'Update'
       where 
           FinalAuthorityEmpId={item.FinalAuthorityEmpId} and wfId={item.wfId}";


                                string query3 = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.wfId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                _common.ExecuteNonQuery(WorkCoreDetail);
                                _common.ExecuteNonQuery(query3);


                            }
                        }
                    }

                    if (data?.WorkLeave != null && data?.WorkLeave?.Count != 0)
                    {
                        foreach (var item in data.WorkLeave.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkLeave = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.EmpLeaveId}
and Id = {item.WorkflowID} 
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkLeave);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.EmpLeaveId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={item.EmpCompId} and cast(code as int)=1)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");

                                //                                if(checkCondition != "1")
                                //                                { 
                                string queryLeave = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId, Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.EmpLeaveId} 
And Id = {item.WorkflowID} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";



                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.EmpLeaveId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.EmpLeaveId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryLeave);



                                //}
                                //                                else
                                //                                {
                                //                                    string WorkLeaveUpdate = @$"UPDATE tblWorkFlowStatus
                                //SET AuthorityStatus = 'p'
                                //where ParentRecordId ={item.EmpLeaveId}
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}";
                                //                                    _common.ExecuteNonQuery(WorkLeaveUpdate);

                                //                                }

                                string queryTransactionLeave = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                   " VALUES ('" + item.WorkflowID + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.EmpLeaveId} And Id={item.WorkflowID} AND Email_To_EmpId = {item.Authority_EmpId}";

                                _common.ExecuteNonQuery(queryTransactionLeave);
                                _common.ExecuteNonQuery(queryEmail);

                            }
                        }
                    }
                    if (data?.WorkEmployeePersonalP != null && data?.WorkEmployeePersonalP?.Count != 0)
                    {
                        foreach (var item in data.WorkEmployeePersonalP.ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkEmployeePersonalP = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.Req_Id}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkEmployeePersonalP);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.Req_Id}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={item.EmpCompId} and cast(code as int)=17)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryEmployeePersonalP = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId, Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus,  AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus,  AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.Req_Id} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.Req_Id}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.Req_Id} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);

                                _common.ExecuteNonQuery(queryEmployeePersonalP);

                                //}

                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.Req_Id + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.Req_Id} AND Email_To_EmpId = {item.Authority_EmpId}";





                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkIntraCompanyTransfer != null && data?.WorkIntraCompanyTransfer?.Count != 0)
                    {
                        foreach (var item in data.WorkIntraCompanyTransfer.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkIntraCompanyTransfer = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.EntryId}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkIntraCompanyTransfer);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.EntryId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=21)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryWorkIntraCompanyTransfer = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId, Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus,  AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus,  AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.EntryId} 
AND AuthorityStatus = 'p' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.EntryId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.EntryId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryWorkIntraCompanyTransfer);

                                //}

                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.EntryId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.EntryId} AND Email_To_EmpId = {item.Authority_EmpId}";


                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);

                            }
                        }

                    }
                    if (data?.WorkManPowerRequisit != null && data?.WorkManPowerRequisit?.Count != 0)
                    {
                        foreach (var item in data.WorkManPowerRequisit.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.MPRId}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";

                                _common.ExecuteNonQuery(WorkFlowsTab);
                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.MPRId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=2)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus,  AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus,  AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.MPRId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.MPRId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.MPRId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.MPRId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.MPRId} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }
                    if (data?.WorkTimeAdjustment != null && data?.WorkTimeAdjustment?.Count != 0)
                    {
                        foreach (var item in data.WorkTimeAdjustment.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.TimeAdjId}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.TimeAdjId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=14)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId, Authority_EmpId,Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus,  AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.TimeAdjId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.TimeAdjId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.TimeAdjId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);

                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.TimeAdjId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.TimeAdjId} AND Email_To_EmpId = {item.Authority_EmpId}";




                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkExpenseTransaction != null && data?.WorkExpenseTransaction?.Count != 0)
                    {
                        foreach (var item in data.WorkExpenseTransaction.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.VFAllowanceTransId}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.VFAllowanceTransId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=15)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.VFAllowanceTransId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.VFAllowanceTransId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.VFAllowanceTransId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.VFAllowanceTransId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";
                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.VFAllowanceTransId} AND Email_To_EmpId = {item.Authority_EmpId}";




                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);

                            }
                        }
                    }

                    if (data?.WorkPerformanceReview != null && data?.WorkPerformanceReview?.Count != 0)
                    {
                        foreach (var item in data.WorkPerformanceReview.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.RId}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);
                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.RId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=11)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.RId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.RId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.RId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}

                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.RId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.RId} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);

                            }
                        }
                    }

                    if (data?.WorkProbationEvaluation != null && data?.WorkProbationEvaluation?.Count != 0)
                    {
                        foreach (var item in data.WorkProbationEvaluation.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.PId}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.PId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=10)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.PId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.PId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.PId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.PId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.PId} AND Email_To_EmpId = {item.Authority_EmpId}";




                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkSeparation != null && data?.WorkSeparation?.Count != 0)
                    {
                        foreach (var item in data.WorkSeparation.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.Sepid}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);
                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.Sepid}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=7)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.Sepid} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.Sepid}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.Sepid} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}

                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.Sepid + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.Sepid} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkPMAppraisal != null && data?.WorkPMAppraisal?.Count != 0)
                    {
                        foreach (var item in data.WorkPMAppraisal.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.AId}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.AId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=4)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.AId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.AId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.AId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.AId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.AId} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkTrainingNomination != null && data?.WorkTrainingNomination?.Count != 0)
                    {
                        foreach (var item in data.WorkTrainingNomination.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.Id}
and AuthorityStatus = 'p' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.Id}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=3)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.Id} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.Id}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.Id} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);

                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.Id + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.Id} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkTrainingRecord != null && data?.WorkTrainingNomination?.Count != 0)
                    {
                        foreach (var item in data.WorkTrainingRecord.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.TPID}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.TPID}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=18)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.TPID} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.TPID}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.TPID} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);

                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.TPID + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.TPID} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkRPEntry != null && data?.WorkRPEntry?.Count != 0)
                    {
                        foreach (var item in data.WorkRPEntry.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.RpEntryId}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.RpEntryId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=20)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.RpEntryId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.RpEntryId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.RpEntryId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.RpEntryId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";


                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.RpEntryId} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkOTEntry != null && data?.WorkOTEntry?.Count != 0)
                    {
                        foreach (var item in data.WorkOTEntry.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.OTEntryId}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.OTEntryId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=9)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.OTEntryId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.OTEntryId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.OTEntryId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.OTEntryId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.OTEntryId} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkManpowerBudget != null && data?.WorkManpowerBudget?.Count != 0)
                    {
                        foreach (var item in data.WorkManpowerBudget.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.Id}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);
                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.Id}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=5)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");
                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.Id} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.Id}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.Id} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";

                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.Id + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.Id} AND Email_To_EmpId = {item.Authority_EmpId}";




                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkEmployeeLoanMaster != null && data?.WorkEmployeeLoanMaster?.Count != 0)
                    {
                        foreach (var item in data.WorkEmployeeLoanMaster.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.EmpLoanId}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.EmpLoanId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=22)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");

                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.EmpLoanId} 
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.EmpLoanId}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.EmpLoanId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.EmpLoanId + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.EmpLoanId} AND Email_To_EmpId = {item.Authority_EmpId}";



                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkAppointmentLetterWf != null && data?.WorkAppointmentLetterWf?.Count != 0)
                    {
                        foreach (var item in data.WorkAppointmentLetterWf.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.tblOfferLetterWfId}
and Id={item.WorkflowID}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.EmpLoanId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=22)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");

                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.tblOfferLetterWfId} 
and Id={item.WorkflowID}
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.tblOfferLetterWfId} and Id={item.WorkflowID}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.tblOfferLetterWfId} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.WorkflowID + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.tblOfferLetterWfId} and Id={item.WorkflowID} AND Email_To_EmpId = {item.Authority_EmpId}";

                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }

                    if (data?.WorkRecPositionApplied != null && data?.WorkRecPositionApplied?.Count != 0)
                    {
                        foreach (var item in data.WorkRecPositionApplied.Where(x => !string.IsNullOrWhiteSpace(x.NewEmp)).ToList())
                        {
                            if (item.NewEmp != null && item.NewEmp != "")
                            {
                                var EmployeeSelected = new DataTable();
                                var querySelected = @$"select DivId,MdptId,DptId,RegionId,TeamId,PGid,dsgId,JobId from tblEmployee where Empid = {item.Authority_EmpId} and CompanyId= {item.Authority_CompanyID}";
                                EmployeeSelected = await _common.ExecuteSqlQuery(querySelected);
                                var EmployeeSelectedFirst = EmployeeSelected.AsEnumerable().FirstOrDefault();

                                string WorkFlowsTab = @$"UPDATE tblWorkFlowStatus
SET SkipBy = '{_UserEmpId}', SkipDate = '{date}', AuthorityStatus = 'S'
where ParentRecordId ={item.Id}
and Id={item.WorkflowID}
and AuthorityStatus = 'P' 
and Authority_EmpId = {item.Authority_EmpId}
and Authority_CompanyID = {item.Authority_CompanyID}";
                                _common.ExecuteNonQuery(WorkFlowsTab);

                                //                                string checkCondition = _common.ExecuteScalarQuery($@"select Count(*) from tblWorkFlowStatus where ParentRecordId ={item.EmpLoanId}
                                //	and WorkflowTypeId = (select Top 1 sdlid from tblsetupsdetail where smsid=85  and companyid={SelectedCompany} and cast(code as int)=22)
                                //and AuthorityStatus != 'S'
                                //and Authority_EmpId = {item.NewEmp}
                                //and Authority_CompanyID = {item.EmpCompId}");

                                //                                if (checkCondition != "1")
                                //                                {
                                string queryTab = @$"INSERT INTO tblWorkFlowStatus 
(WorkflowTypeId, WorkflowTypeCode,IsTemporaryAuthority,ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,Authority_EmpId, Authority_CompanyID, Authority_DivId, Authority_MDptId, Authority_DptId, Authority_LocId, Authority_RegionId, Authority_TeamId, Authority_PGId, Authority_DsgId, Authority_JobId, Authority_CategoryId, Authority_TypId, Authority_ReportTo, RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, ForwardingDate, ProcessDate, AuthorityStatus, AuthorityCheckPoint, TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy)
SELECT 
    WorkflowTypeId, WorkflowTypeCode,'1' as IsTemporaryAuthority,{item.Authority_EmpId} as ActualAuthority_EmpId, WorkflowTypeName, ParentRecordId,
    '{item.NewEmp}' AS Authority_EmpId,
'{item.EmpCompId}' AS Authority_CompanyID,
'{EmployeeSelectedFirst?["DivId"]}' AS Authority_DivId,
'{EmployeeSelectedFirst?["MdptId"]}' AS Authority_MDpild,
'{EmployeeSelectedFirst?["RegionId"]}' AS Authority_RegionId,
'{EmployeeSelectedFirst?["TeamId"]}' AS Authority_TeamId,
'{EmployeeSelectedFirst?["PGid"]}' AS Authority_PGId,
'{EmployeeSelectedFirst?["dsgId"]}' AS Authority_DsgId,
'{EmployeeSelectedFirst?["JobId"]}' AS Authority_JobId,
Authority_DptId, Authority_LocId, 
    Authority_CategoryId, Authority_TypId, Authority_ReportTo, 
    RequestForwardBy_EmpId, RequestBelongTo_EmpId, RequestBelongTo_DivId, RequestBelongTo_MDptId, 
    RequestBelongTo_DptId, RequestBelongTo_LocId, RequestBelongTo_RegionId, RequestBelongTo_TeamId, 
    RequestBelongTo_PGId, RequestBelongTo_DsgId, RequestBelongTo_JobId, 
    RequestBelongTo_CategoryId, RequestBelongTo_TypId, RequestBelongTo_ReportTo, 
    '{date}' AS ForwardingDate, ProcessDate, 'P' as AuthorityStatus, AuthorityCheckPoint + 1, 
    TotalAuthorityCount, IsDefaultApprover, Request_CreatedOn, Request_RaisedBy
FROM tblWorkFlowStatus
WHERE ParentRecordId = {item.Id} 
and Id={item.WorkflowID}
AND AuthorityStatus = 'S' 
AND Authority_EmpId = {item.Authority_EmpId} 
AND Authority_CompanyID = {item.Authority_CompanyID}";
                                string queryAuthoritySequ = $@"
UPDATE tblWorkFlowStatus
SET AuthorityCheckPoint = AuthorityCheckPoint+1
where ParentRecordId ={item.Id} and Id={item.WorkflowID}
  And Authority_CompanyID = {item.Authority_CompanyID} AND AuthorityCheckPoint > (select AuthorityCheckPoint from tblWorkFlowStatus where ParentRecordId ={item.Id} and Id={item.WorkflowID} and Authority_EmpId={item.Authority_EmpId} and Authority_CompanyID={item.Authority_CompanyID})
";
                                _common.ExecuteNonQuery(queryAuthoritySequ);
                                _common.ExecuteNonQuery(queryTab);
                                //}
                                string queryTransaction = " Insert into EmployeeAuthorityTransactionHistory (TransId, OldEmpId, NewEmpId, Area, CompanyId, TransDate, Userid, FormId, EntDate, EntOperation, UserEmpId, UserEmpCode, UserEmpName)" +
                                      " VALUES ('" + item.WorkflowID + "','" + OldEmpId + "','" + item.NewEmp + "','WorkFlowSetupDefaultAuthority','" + CompanyId + "','" + date + "','" + _UserId + "','" + _FormId + "','" + date + "','Update','" + _UserEmpId + "','" + _UserEmpCode + "','" + _UserEmpName + "')";

                                string queryEmail = @$"
INSERT INTO tblWorkFlowStatus_EmailLog (
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, AuthorityStatus, EmailPrepared,
    LoginCompanyId, LoginEmpId, LoginUserId,
    EntTerminal, EntTerminalIP, FormId, LoginApplication,
    Email_CcIds, Email_To_EmpId
)
SELECT 
    WorkflowTypeId, WorkflowTypeCode, EmailType,
    Module, LoginCulture, ParentRecordId, PrepareEmail,
    EmailSendingRemarks, 
    'P' AS AuthorityStatus,
    '0' AS EmailPrepared,
    '{CompanyId}' AS LoginCompanyId,
    '{_UserEmpId}' AS LoginEmpId,
    '{_UserId}' AS LoginUserId,
    '{_EntTerminal}' AS EntTerminal,
    '{_EntTerminalIP}' AS EntTerminalIP,
    '{_FormId}' AS FormId,
    'HCMS' AS LoginApplication,
    '{_UserEmpId},{item.Authority_EmpId}' AS Email_CcIds,
    '{item.NewEmp}' AS Email_To_EmpId  
FROM tblWorkFlowStatus_EmailLog 
WHERE ParentRecordId = {item.Id} and Id={item.WorkflowID} AND Email_To_EmpId = {item.Authority_EmpId}";

                                _common.ExecuteNonQuery(queryTransaction);
                                _common.ExecuteNonQuery(queryEmail);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception();
            }

        }


        public async Task<Dictionary<string, object>> GetWorkFlowsTrack(string CompanyId, string LoginEmpId, string ParentRecordId, string WorkflowID, string _Culture)
        {
            try { 
            string WorkFlowCode = _common.ExecuteScalarQuery(@$"Select Top 1 WorkflowTypeCode FROM tblWorkFlowStatus WHERE Id = {WorkflowID}");

            var Track = new DataTable();
            string query = @$"Select * from fn_Workflow_GetFinalizedTrack(" + CompanyId + ", " + LoginEmpId + ", '" + WorkFlowCode + "'," + ParentRecordId + ", '" + _Culture + "') order by AuthorityCheckPoint Asc";
            Track = await _common.ExecuteSqlQuery(query);

            var row = new Dictionary<string, object>
                {
                    { "WorkFlowsTrack", _common.GetJsonDatatable(Track) }

                };

            return row;
            }
            catch (Exception ex)
            {
                var row = new Dictionary<string, object>
    {
        { "ErrorMessage", ex.Message },
        { "StackTrace", ex.StackTrace },
        { "InnerException", ex.InnerException?.Message ?? "No inner exception" },
        { "Source", ex.Source }
    };
                return row;
            }
        }

    }
}
