using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common;
using System.Data;
using HCMS_Api.Components.HCMS.ESS;
using static Azure.Core.HttpHeader;
using Org.BouncyCastle.Ocsp;
using HCMS_Api.Components.HCMS.Common.Dapper;
using System.ComponentModel.Design;
using System.Web.Http;
using Dapper;
using System;
using HCMS_Api.Components.HCMS.Common.Models;
using static System.Net.Mime.MediaTypeNames;
using HCMS_Api.Models;

namespace HCMS_Api.Components.HCMS.Payroll
{
    public class PFSlipViaEmailComponent
    {
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly HCMS_Api.Common.Common _common;
        private readonly IDapperDataService _dapperService;

        public PFSlipViaEmailComponent(IConfiguration configuration, Utilities utilities
            , DataServices dataservice, HCMS_Api.Common.Common common
            ,IDapperDataService dapper)
        {
            _configuration = configuration;
            _utilities = utilities;
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
            _common = common;
            _dapperService = dapper;
        }

        public async Task<IReadOnlyList<PayrollMonthDto>> GetActivePMonth(string CompanyID)
        {
            string sql = @" SELECT 
		                    ShortMonth,
                            LTRIM(RTRIM(PMonth2)) + ' ' + pYear 
                            + ' (' + REPLACE(RTRIM(LTRIM(CONVERT(CHAR(11), DateFrom, 106))),' ','/') 
                            + ' - ' + REPLACE(RTRIM(LTRIM(CONVERT(CHAR(11), DateTo, 106))),' ','/') 
                            + ')' AS MonthName
                        FROM tblPayrollMonth
                        WHERE CompanyId = @CompanyID AND Closed = 0";

            var args = new
            {
                CompanyId = CompanyID,
            };

            var rows = (await _dapperService.QueryAsync<PayrollMonthDto>(sql, args)).ToList();            
            return rows;
        }

        public async Task<IReadOnlyList<FiscalYearDto>> GetClosedFiscalYears(string CompanyID)
        {
            string sql = @"SELECT  FYId
                , DATENAME(M, StartDate) + ' ' + CAST( Year(StartDate) AS VARCHAR) + ' - ' + DATENAME(M, EndDate) + ' ' + CAST( Year(EndDate) AS VARCHAR) FiscalYear 
                FROM tblFiscalYearSetup WHERE CompanyId = @CompanyId AND Active = 1 ORDER BY FYId DESC ";

            var args = new
            {
                CompanyId = CompanyID,                
            };
            
            var rows = (await _dapperService.QueryAsync<FiscalYearDto>(sql, args)).ToList();
            rows.Insert(0, new FiscalYearDto { FYId = -1, FiscalYear = "--Select--" });
            return rows;              
        }

        public async Task<IReadOnlyList<IdNameDto>> GetPayrollGroups(
                string CompanyID, string ApplicationID, string UserEmpID, string FormID)
        {
            int companyIdInt, userEmpIdInt;
            if (!int.TryParse(CompanyID, out companyIdInt) || !int.TryParse(UserEmpID, out userEmpIdInt))
                return Array.Empty<IdNameDto>();

            const string sql = @"
                SELECT Id, Name
                FROM    dbo.fn_GeneralSetups_PayrollGroups(
                            @ApplicationID, @Culture, @CompanyID, 1, @UserEmpID, @FormID
                        )
                ORDER BY SNo;";

            var args = new
            {
                ApplicationID,
                Culture = "en-GB",
                CompanyID = companyIdInt,
                UserEmpID = userEmpIdInt,
                FormID
            };

            var rows = (await _dapperService.QueryAsync<IdNameDto>(sql, args)).ToList();
            return rows;
        }

        public async Task<IReadOnlyList<ProcessDateDto>> GetProcessDateList(string CompanyID)
        {
            if (!int.TryParse(CompanyID, out int companyIdInt))
                return Array.Empty<ProcessDateDto>();

            // 1. Keep the SQL query simple and data-focused
            const string sql = @" SELECT
                CONVERT(varchar, CAST(RequestAt AS DATE), 23) AS DateId,
                FORMAT(CAST(RequestAt AS DATE), 'dd/MMM/yyyy') AS DateName
                FROM HCMS_EmailLog.dbo.tblReleaseEmailPFSlip
                WHERE CompanyId = @CompanyID
                GROUP BY CAST(RequestAt AS DATE)
                ORDER BY CAST(RequestAt AS DATE) DESC; -- Still sort newest first";

            var args = new { CompanyID = companyIdInt };

            // 2. Fetch only the date records from the database
            var dateRecords = await _dapperService.QueryAsync<ProcessDateDto>(sql, args);

            // 3. Create a new list and add the hardcoded "New" item first
            var finalList = new List<ProcessDateDto>
            {
                new ProcessDateDto { DateId = "New", DateName = "New" }
            };

            // 4. Add the date records fetched from the database after the "New" item
            finalList.AddRange(dateRecords);

            return finalList;
        }

        public async Task<IReadOnlyList<EmployeeDetailPFSlipDto>> GetPFEmployees(string companyId, string fyid, string pgid, string userEmpID, string PdateId)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return Array.Empty<EmployeeDetailPFSlipDto>();
            int pgidInt; if (!int.TryParse(pgid, out pgidInt)) pgidInt = 0;
            DateTime pDate = new DateTime();

            string sql = "";

            if (PdateId == "New")
            {
                sql = @"
                SELECT  e.EmpId AS EmpID,
                        e.EmpCode AS EmployeeCode,
                        dbo.fn_General_GetEmployeeName(e.EmpId, e.CompanyId, 1, 0, 0, 'en-GB') AS EmployeeName,
                        div.Name AS Division, dept.Name AS Department, dsg.Name AS Designation, grd.Name AS Grade,
                        e.Email, '' AS EmailStatus, CAST(0 AS bit) AS SendEmail
                FROM    dbo.tblEmployee AS e
                JOIN    dbo.tblEmpSalarySetup AS b ON b.EmpId = e.EmpId AND b.CompanyId = e.CompanyId
                JOIN    dbo.tblSetupsDetail div  ON div.sdlid  = e.DivId
                JOIN    dbo.tblSetupsDetail dept ON dept.sdlid = e.MdptId
                JOIN    dbo.tblSetupsDetail dsg  ON dsg.sdlid  = e.dsgId
                JOIN    dbo.tblSetupsDetail grd  ON grd.sdlid  = e.JobId
                JOIN	dbo.TblFiscalYearSetup fy ON fy.FYID    = @pfyid
                WHERE   e.Active <> 0
                  AND   e.CompanyId = @companyId
                  AND   e.PayrollStatus = 1
                  AND   ISNULL(b.PFMember, 0) = 1
                  AND   b.PFDate IS NOT NULL
                        -- include during or before FY, exclude starts AFTER FY end:
                        AND b.PFDate < DATEADD(DAY, 1, CAST(fy.EndDate AS date))
                  AND  (
                        (@pgid = 0 AND e.PGID IN (
                            SELECT Id
                            FROM dbo.fn_GeneralSetups_PayrollGroups(@appCode, @culture, @companyId, 0, @userEmpID, @formid)
                        ))
                        OR
                        (@pgid <> 0 AND e.PGID = @pgid)
                  )
                ORDER BY TRY_CAST(e.EmpCode AS int) ASC;";
            }
            else
            {
                
                if (!DateTime.TryParse(PdateId, out pDate))
                    return Array.Empty<EmployeeDetailPFSlipDto>();

                sql = @"Select e.EmpId,  e.EmpCode AS EmployeeCode,
                            dbo.fn_General_GetEmployeeName(e.EmpId, e.CompanyId, 1, 0, 0, 'en-GB') AS EmployeeName,
                            div.Name AS Division, dept.Name AS Department, dsg.Name AS Designation, grd.Name AS Grade,
                            p.EmailTo as Email, 
		                    case when isnull(p.Status,0)=0 then 'Pending' 
			                    when isnull(p.Status,0)=1 then 'Sent on ' + IIF(p.SentAt IS NOT NULL, FORMAT(p.SentAt, 'dd/MMM/yyyy hh:mm:ss tt'), '')
			                    when isnull(p.Status,0)=2 then ('Failed - ' + p.EmailStatusMsg) 
		                    end AS EmailStatus
		                    , 1 as SendEmail		
                    from HCMS_EmailLog.dbo.tblReleaseEmailPFSlip p
                    JOIN    dbo.tblEmployee AS e on e.EmpId=p.EmpId
                    JOIN    dbo.tblEmpSalarySetup AS b ON b.EmpId = e.EmpId AND b.CompanyId = e.CompanyId
                    JOIN    dbo.tblSetupsDetail div  ON div.sdlid  = e.DivId
                    JOIN    dbo.tblSetupsDetail dept ON dept.sdlid = e.MdptId
                    JOIN    dbo.tblSetupsDetail dsg  ON dsg.sdlid  = e.dsgId
                    JOIN    dbo.tblSetupsDetail grd  ON grd.sdlid  = e.JobId
                    where cast(p.RequestAt as date) = @pDate and p.companyid=@companyId  
                    AND  (
                        (@pgid = 0 AND b.PGID IN (
                            SELECT Id
                            FROM dbo.fn_GeneralSetups_PayrollGroups(@appCode, @culture, @companyId, 0, @userEmpID, @formid)
                        ))
                        OR
                        (@pgid <> 0 AND b.PGID = @pgid)
                  )
                    and p.FyId=@pfyid
                    order by RequestAt asc ";
            }


                var args = new
                {
                    pgid = pgidInt,
                    companyId = companyId,
                    appCode = "HCMSPayroll",
                    culture = "en-GB",
                    userEmpID = userEmpID,
                    formid = "PFSlipViaEmail",
                    pDate = pDate,
                    pfyid = fyid
                };

            var list = (await _dapperService.QueryAsync<EmployeeDetailPFSlipDto>(sql, args)).AsList();
            return list;
        }


        public async Task<IReadOnlyList<int>> InsertReleaseEmailPFSlipBatchAsync(
    int companyId,
    int pgId,
    int fyId,
    IEnumerable<int> empIds,
    JwtArray loginUserData,
    string applicationId)
        {
            var list = (empIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (list.Count == 0) return Array.Empty<int>();

            int? userEmpId = ToIntOrNull(loginUserData?.UserEmpID);

            const string sql = @"
            INSERT INTO HCMS_EmailLog.dbo.tblReleaseEmailPFSlip
            (
                CompanyId, EmpId, PgId, FyId, Status, EmailTo, EmailStatusMsg, ErrorMsg,
                UserId, UserEmpId, UserEmpName, UserEmpCode, EntTerminal, EntTerminalIP,
                ApplicationID, FormId, EntOperation
            )
            OUTPUT INSERTED.RID
            SELECT
                @CompanyId,
                e.EmpId,
                @PgId,
                @FyId,
                CASE WHEN e.Email IS NULL OR LTRIM(RTRIM(e.Email)) = '' THEN 2 ELSE 0 END,
                ISNULL(NULLIF(LTRIM(RTRIM(e.Email)), ''), ''),
                NULL,                
                CASE WHEN e.Email IS NULL OR LTRIM(RTRIM(e.Email)) = '' THEN 'Missing email' END,                
                @UserId, @UserEmpId, @UserEmpName, @UserEmpCode, @EntTerminal, @EntTerminalIP,
                @ApplicationID, @FormId, @EntOperation
            FROM dbo.tblEmployee e
            WHERE e.EmpId = @EmpId AND e.CompanyId = @CompanyId;";

            var created = new List<int>(list.Count);

            _dapperService.BeginTransaction();
            try
            {
                foreach (var empId in list)
                {
                    var args = new
                    {
                        CompanyId = companyId,
                        EmpId = empId,
                        PgId = pgId,
                        FyId = fyId,

                        // audit (UserId is string per your schema)
                        UserId = loginUserData?.UserID,
                        UserEmpId = userEmpId,
                        UserEmpName = loginUserData?.UserEmpName,
                        UserEmpCode = loginUserData?.UserEmpCode,
                        EntTerminal = loginUserData?.EntTerminal,
                        EntTerminalIP = loginUserData?.EntTerminalIP,
                        ApplicationID = applicationId,
                        FormId = loginUserData?.FormId,
                        EntOperation = "INSERT"
                    };

                    // QuerySingleAsync<T> in your service maps to FirstOrDefault; returns 0 if no row inserted
                    var rid = await _dapperService.QuerySingleAsync<int>(sql, args);
                    if (rid != 0) created.Add(rid);                    
                }

                _dapperService.Commit();
            }
            catch
            {
                _dapperService.Rollback();
                throw;
            }

            return created;
        }


        private static int? ToIntOrNull(string s)
        {
            int v; return int.TryParse(s, out v) ? v : (int?)null;
        }

    }


    public sealed class FiscalYearDto
    {
        public int FYId { get; set; }
        public string FiscalYear { get; set; } = string.Empty;
    }

    public sealed class PayrollMonthDto
    {
        public string ShortMonth { get; set; } = string.Empty;
        public string MonthName { get; set; } = string.Empty;
    }
     
    public class EmployeeDetailPFSlipDto
    {
        public int EmpID { get; set; } = -1;
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmailStatus { get; set; } = string.Empty;
        public bool SendEmail { get; set; } = false;
    }

    public sealed class QueuePFEmployeesRequestDTo
    {
        public int PgId { get; set; }
        public int FyId { get; set; }
        public List<int> EmpIds { get; set; } = new List<int>();
    }

}
