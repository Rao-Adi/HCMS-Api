using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common;
using System.Data;
using Org.BouncyCastle.Ocsp;
using Dapper;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class EmployeeExitClearanceComponent
    {
        private readonly Utilities _utilities;
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly ClientContextService _clientContextService;
        private readonly IDapperDataService _dapperService;

        public EmployeeExitClearanceComponent(
            Utilities utilities
            , DataServices dataservice
            , IConfiguration configuration
            , ClientContextService clientContextService
            , IDapperDataService dapper)
        {
            _utilities = utilities;
            _dataservice = dataservice;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _dapperService = dapper;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        public async Task<DataTable> GetEmployeeInformationDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return null;

            try
            {
                string query = @"
                    SELECT 
                        a.EmpId,
                        a.EmpCode,
                        a.NameOnly AS [EmployeeName],
                        a.JobId,
                        a.JobGroup,
                        a.DivId,
                        a.DivisionName,
                        a.DptId,
                        a.Department,
                        a.MDptId,
                        a.MainDepartment,
                        a.DsgId,
                        a.Designation,
                        a.Email,
                        dbo.fn_GetDateFormat_DDMMMYYYY(a.DateJoin) AS [DateJoin],
                        dbo.fn_GetDateFormat_DDMMMYYYY(a.DateConfirm) AS [DateConfirm],
                        (SELECT dbo.fn_GetDateFormat_DDMMMYYYY(MAX(LeavingDate)) FROM tblSeparation WHERE EmpId = a.EmpId) AS LeavingDate,
                        (SELECT dbo.fn_GetDateFormat_DDMMMYYYY(MAX(ResigEffectDate)) FROM tblSeparation WHERE EmpId = a.EmpId) AS ResginDate,
                        a.Location,
                        ISNULL((
                            SELECT c.FirstName + ' ' + ISNULL(c.MidName, '') + ' ' + c.LastName
                            FROM tblEmployee c 
                            WHERE c.EmpId = a.ReportTo
                        ), '') AS ReportingTo,
                        ISNULL((
                            SELECT Processed 
                            FROM tblFinalSettlementMaster 
                            WHERE EmpId = a.EmpId
                        ), 0) AS Processed,
                        a.Phone,
                        a.PGId,
                        a.PayrollGroup,
                        a.CompanyId,
                        a.RegionName AS Region,
                        (SELECT TOP 1 Name FROM tblTeamMaster WHERE tmid = a.TeamID) AS Team,
                        a.EmployeeCategory,
                        a.EmployeeType,
                        ISNULL((
                            SELECT c.Name 
                            FROM dbo.fn_EmployeeAll('en-GB', 1) c 
                            WHERE c.EmpId = a.Dotted
                        ), '') AS InDirectReportingTo,
                        (
                            SELECT EmpPic 
                            FROM hcms_files.dbo.tblEmployee_ImageCv 
                            WHERE EmpId = @EmpId
                        ) AS EmpPic
                    FROM dbo.fn_EmployeeAll(@Culture, -1) a 
                    WHERE a.EmpId = @EmpId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@Culture", culture);

                var result = await _dapperService.QuerySingleAsync<EmployeeInfo>(query, parameters);

                var list = new List<EmployeeInfo>();
                if (result != null)
                    list.Add(result);

                return _configuration.ConvertToDataTable(list);
            }
            catch (Exception ex)
            {
                // Optionally log the error
                return new DataTable();
            }
        }


    }

    public class EmployeeInfo
    {
        public int EmpId { get; set; }
        public string? EmpCode { get; set; }
        public string? EmployeeName { get; set; }
        public int JobId { get; set; }
        public string? JobGroup { get; set; }
        public int DivId { get; set; }
        public string? DivisionName { get; set; }
        public int DptId { get; set; }
        public string? Department { get; set; }
        public int MDptId { get; set; }
        public string? MainDepartment { get; set; }
        public int DsgId { get; set; }
        public string? Designation { get; set; }
        public string? Email { get; set; }
        public string? DateJoin { get; set; }
        public string? DateConfirm { get; set; }
        public string? LeavingDate { get; set; }
        public string? ResginDate { get; set; }
        public string? Phone { get; set; }
        public int PGId { get; set; }
        public string? PayrollGroup { get; set; }
        public string? Location { get; set; }
        public string? ReportingTo { get; set; }
        public string? InDirectReportingTo { get; set; }
        public string? Processed { get; set; }
        public string? Region { get; set; }
        public string? Team { get; set; }
        public string? EmployeeType { get; set; }
        public string? EmployeeCategory { get; set; }
        public string? DirectReportTo { get; set; }
        //public string? EmpPic { get; set; }
        public int CompanyId { get; set; }
    }

}
