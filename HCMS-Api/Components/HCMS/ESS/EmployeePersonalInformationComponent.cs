using System.Data;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Dapper;
using Dapper;
using HCMS_Api.Components.HCMS.Common.Models;
using Org.BouncyCastle.Ocsp;
using System.Reflection.Emit;
using System.ComponentModel.Design;
using System.Data.Entity.Core.Common.CommandTrees;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class EmployeePersonalInformationComponent
    {
        private readonly Utilities _utilities;
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly ClientContextService _clientContextService;
        private readonly IDapperDataService _dapperService;

        public EmployeePersonalInformationComponent(
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

        public DataTable GetEmployeeInfoData(string EmpId, string CompanyId, string culture)
        {

            if (string.IsNullOrWhiteSpace(EmpId)) return new DataTable();

            DataSet dataset = new DataSet();

                string result = _dataservice.GetDataWithClause(
     "a.EmpCode, ISNULL(a.Name,'') AS Name, " +
     "ISNULL(a.MainDepartment,'') AS MainDepartment, ISNULL(a.Department,'') AS Department, FORMAT(a.DateJoin, 'dd/MMM/yyyy') AS DateJoin, " +
     "ISNULL(E.Email,'') AS Email, ISNULL(a.Designation,'') AS Designation, " +
     "f.EmpPic, " +
     "'' AS dReportToCompny, " +
     "'' AS iDReportToCompny, " +
     "dbo.fn_General_GetEmployeeName(e.ReportTo ,'" + CompanyId + "',1,1,1, '" + culture + "') AS ReportingPerson, " +
     "dbo.fn_General_GetEmployeeName(e.Dotted ,'" + CompanyId + "',1,1,1, '" + culture + "') AS DottedPerson, " +
     "E.Shift, " +
     "ISNULL((SELECT CompanyName FROM vCompanyManagement WHERE ccode = a.Companyid), '') AS CompanyName, " +
     "DivisionName, Location, JobGroup, PayrollGroup, EmployeeType, " +
     "FORMAT(a.DateConfirm, 'dd/MMM/yyyy') AS DateConfirm, FORMAT(a.CDueDate,'dd/MMM/yyyy') AS DateConfirmDue, " +
     "FORMAT(E.ContractExpireDate,'dd/MMM/yyyy') AS ContractExpiryDate, " +
     "FORMAT(E.InternExpiryDate,'dd/MMM/yyyy') AS InternExpiryDate, FORMAT(a.ExtDate,'dd/MMM/yyyy') AS ProbhationExtDate, " +
     "E.IdCardRemarks, E.FamilyCardNo, E.IqamaNo, I.Name AS IqamaProfession, " +
     "CONVERT(CHAR,E.IqamaExpiryHijri,103) AS IqamaExpiryHijri, CONVERT(CHAR,E.IqamaExpiryGregorian,103) AS IqamaExpiryGregorian, CurrSpnsName, " +
     "CASE WHEN ISNULL(SpnsTransferable,0)=0 THEN 'N/A' WHEN SpnsTransferable=1 THEN 'Yes' ELSE 'No' END AS SpnsTransferable, " +
     "S.Name AS SpnsType, Cnt.Name AS SpnsCountry, Cty.Name AS SpnsCity, SpnsContactDetails, SpnsNatureOfBusiness, " +
     "CONVERT(CHAR,E.SpnsExpiryHijri,103) AS SpnsExpiryHijri, CONVERT(CHAR,E.SpnsExpiryGregorian,103) AS SpnsExpiryGregorian, " +
     "EC.Name AS EmployeeCategory, ISNULL(sd.Name, '') AS RoleName, ejp.JobProfileId",  // <-- JobProfileId added here

     "fn_Lookup_EmployeeProfile('" + culture + "', (SELECT CompanyId FROM TblEmployee WHERE EmpId = " + EmpId + ")) AS a " +
     "INNER JOIN tblEmployee E ON E.EmpId = a.EmpId " +
     "LEFT JOIN tblempjobprofile ejp ON ejp.EmpId = a.EmpId " +
     "LEFT JOIN Tblsetupsdetail sd ON sd.sdlid = ejp.RoleId " +
     "LEFT OUTER JOIN vwtblSetupsDetail I ON E.IqamaProfession = I.sdlid AND I.Culture = '" + culture + "' " +
     "LEFT JOIN vwtblSetupsDetail S ON E.SpnsType = S.sdlid AND S.Culture = '" + culture + "' " +
     "LEFT JOIN tblCountry Cnt ON E.SpnsCountry = Cnt.cntid " +
     "LEFT JOIN tblCity Cty ON E.SpnsCity = Cty.ctyid " +
     "LEFT JOIN vwtblSetupsDetail EC ON EC.sdlid = E.empCategoryid AND EC.Culture = '" + culture + "' " +
     "LEFT OUTER JOIN EXTERNAL_HCMS_FILES_TBLEMPLOYEE_IMAGECV AS f ON f.EmpId = a.EmpId",

     "a.EmpId = " + EmpId, ref dataset);

            return (dataset.Tables.Count > 0) ? dataset.Tables[0] : new DataTable();



        }

        public DataTable GetEmployeeData(string EmpId, string CompanyId, string culture)
        {
            var dataSet = new DataSet();
            // (Optional) basic validation to avoid SQL errors
            if (!int.TryParse(EmpId, out var empIdInt)) return new DataTable();
            if (!int.TryParse(CompanyId, out var companyIdInt)) return new DataTable();

            var safeCulture = (culture ?? string.Empty).Replace("'", "''");


            string ColumnsName = "A.empid,A.empCode,FORMAT(a.DateJoin, 'dd/MMM/yyyy') As DateJoin,isnull(a.Name,'') as Name,isnull(A.FName,'') as FName,isnull(Department,'') as Department,isnull(Designation,'') as Designation, FORMAT(A.DateofBirth, 'dd/MMM/yyyy') As DateofBirth," +
                                 "isnull(A.Address,'') as Address,isnull(A.Phone,'') as Phone,isnull(A.Mobile,'') as Mobile,isnull(A.Email,'') as Email,isnull(A.Email2,'') as EmailPer,isnull(A.NICNew,'') as NICNew,isnull(A.NTN,'') as NTN,isnull(b.Name,'') as Blood,isnull(c.Name,'') as Marital," +
                                 "IsNull(d.Name,'') as Religion,isnull(E.IdCardRemarks,'') IdCardRemarks,isnull(E.FamilyCardNo,'')FamilyCardNo,FORMAT(E.CNICExpiryDate, 'dd/MMM/yyyy') As CNICExpiryDate,isnull(Nationality,'')Nationality,isnull(E.PassportNo,'')PassportNo," +
                                 "isnull(Gender,'')Gender,(select Name from tblsetupsDetail where sdlid=isnull(E.SectId,''))SectId,isnull(E.Identification,'')Identification,(select Name from tblsetupsDetail where sdlid=isnull(E.MTId,''))MTId,FORMAT(a.PassportExpiryDate, 'dd/MMM/yyyy') As PassportExpiryDate";
            string TableName = "dbo.fn_Employee('" + culture + "', " + CompanyId + ") a left outer join tblSetupsDetail b on a.bldid=b.sdlid " +
                               "inner join tblSetupsDetail c on a.mrtid=c.sdlid " +
                               "inner join tblSetupsDetail d on a.rlgId=d.sdlid " +
                               "inner join tblEmployee E on a.EmpId=E.Empid ";
            string whereClause = "a.empId=" + EmpId;
            string Message = _dataservice.GetDataWithClause(ColumnsName, TableName, whereClause, ref dataSet);


            return (dataSet.Tables.Count > 0) ? dataSet.Tables[0] : new DataTable();
        }
        public DataTable GetEmergencyContact(string EmpId, string CompanyId, string culture)
        {
            var dataSet = new DataSet();

            try
            {
                // Guards: avoid SQL errors
                if (!int.TryParse(EmpId, out var empIdInt)) return new DataTable();
                if (!int.TryParse(CompanyId, out var companyIdInt)) return new DataTable();

                string columns =
                    "isnull(emgPerson,'') as ContactPerson," +
                    "isnull(emgPhone,'') as Phone," +
                    "isNull(emgMobile,'') as Mobile," +
                    "isnull(emgAddress,'') as Address," +
                    "RelId," +
                    "isNull((Select Name from tblSetupsDetail where sdlid=TblEmployee.relId),'') as Relation";

                string table = "TblEmployee";
                string where = $"empId={empIdInt} and Companyid={companyIdInt}";

                string msg = _dataservice.GetDataWithClause(columns, table, where, ref dataSet);

                return (dataSet.Tables.Count > 0) ? dataSet.Tables[0] : new DataTable();
            }
            catch (Exception)
            {

                return new DataTable();
            }
        }
        public async Task<DataTable> GetEmployeeCertificatesAsync(string empId, string companyId, string culture)
        {
            var empty = new DataTable();

            const string sql = @"
                                SELECT
                                ISNULL(b.Name,'')                          AS Name,
                                FORMAT(a.ExpiryDate,  'dd/MMM/yyyy')       AS ExpiryDate,
                                FORMAT(a.DateAchieved,'dd/MMM/yyyy')       AS DateAchieved
                                FROM tblEmpCertification a
                                INNER JOIN tblSetupsDetail b
                                ON a.crtid = b.sdlid
                                AND a.CompanyId = b.CompanyId
                                WHERE a.EmpId = @EmpId
                                AND a.CompanyId = @CompanyId;";

            try
            {

                var list = (await _dapperService.QueryAsync<EmployeeCertificates>(
                    sql,
                    new { empId, companyId }
                )).ToList();

                return _configuration.ConvertToDataTable(list);
            }
            catch
            {
                // optionally log the exception
                return empty;
            }
        }
        public async Task<DataTable> GetEmployeeAcademicQualificationAsync(string empId, string companyId, string culture)
        {
            var empty = new DataTable();

            const string sql = @"
                                SELECT
                                ISNULL(a.qlfid, 0) AS qlfid,
                                ISNULL(b.Name, '') AS Qulification,
                                ISNULL(a.PassingYear, '') AS PassingYear,
                                ISNULL(d.Name, '') AS City,
                                a.Insid
                                FROM tblEmpQualificationInfo a
                                INNER JOIN tblSetupsDetail b ON a.qlfid = b.sdlid
                                LEFT JOIN tblCity d ON d.ctyid = a.ctyId
                                WHERE a.EmpId = @EmpId
                                AND a.CompanyId = @CompanyId;";


            try
            {

                var baselist = (await _dapperService.QueryAsync<EmployeeAcademicQualification>(
                    sql,
                    new { empId, companyId }
                )).ToList();
                var result = new List<EmployeeAcademicQualification>(baselist.Count);
                foreach (var row in baselist)
                {
                    result.Add(new EmployeeAcademicQualification
                    {
                        qlfid = row.qlfid,
                        Qulification = row.Qulification,
                        PassingYear = row.PassingYear,
                        City = row.City,
                        Insid = row.Insid,
                        institute = Convert.ToString(
                                _utilities.GetScalarData("Name", "tblsetupsdetail", " sdlid=" + row.Insid))
                    });
                }
                return _configuration.ConvertToDataTable(result);
            }
            catch
            {
                // optionally log the exception
                return empty;
            }
        }
        public async Task<DataTable> GetEmployeeDependentsAsync(string empId, string companyId, string culture)
        {
            var empty = new DataTable();
            const string sql = @"
                               SELECT
                                ISNULL(a.FirstName,'') + ' ' + RTRIM(ISNULL(a.MiddleName,'')) + ' ' + RTRIM(ISNULL(a.LastName,'')) AS Name,
                                ISNULL(b.Name,'') AS Relation,
                                ISNULL(FORMAT(a.DOB, 'dd/MMM/yyyy'), '') AS DOB,
                                ISNULL(c.Name,'') AS Qualifaction,
                                ISNULL(d.Name,'') AS Gender,
                                ISNULL(FORMAT(a.DateofAnniversary, 'dd/MMM/yyyy'), '') AS DateOfAnniversary
                                FROM tblEmpDependentsInfo a
                                INNER JOIN tblSetupsDetail b ON a.rlnid = b.sdlid AND a.CompanyId = b.CompanyId
                                INNER JOIN tblSetupsDetail c ON a.qlfid = c.sdlid AND a.CompanyId = c.CompanyId
                                INNER JOIN tblSetupsDetail d ON a.gndid = d.sdlid AND a.CompanyId = d.CompanyId
                                WHERE a.EmpId = @EmpId
                                AND a.CompanyId = @CompanyId;
                                ";
            try
            {

                var list = (await _dapperService.QueryAsync<EmployeeDependents>(
                    sql,
                    new { empId, companyId }
                )).ToList();

                return _configuration.ConvertToDataTable(list);
            }
            catch
            {
                // optionally log the exception
                return empty;
            }
        }
    }
    public class EmployeeCertificates
    {
        public string? Name { get; set; }
        public string? ExpiryDate { get; set; }
        public string? DateAchieved { get; set; }

    }
    public class EmployeeAcademicQualification
    {
        public string? qlfid { get; set; }
        public string? Insid { get; set; }
        public string? institute { get; set; }
        public string? Qulification { get; set; }
        public string? PassingYear { get; set; }
        public string? City { get; set; }
    }
    public class EmployeeDependents
    {
        public string? Name { get; set; }
        public string? Relation { get; set; }
        public string? Gender { get; set; }
        public string? Qualifaction { get; set; }
        public string? DOB { get; set; }
    }
}