using System.Data;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Dapper;
using Dapper;
using HCMS_Api.Components.HCMS.Common.Models;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class EmployeeJobInformationComponent
    {
        private readonly Utilities _utilities;
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly ClientContextService _clientContextService;
        private readonly IDapperDataService _dapperService;

        public EmployeeJobInformationComponent(
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
            if (EmpId == null)
                return null;

            DataSet dataset = new DataSet();

            try
            {
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
                    "EC.Name AS EmployeeCategory, ISNULL(sd.Name, '') AS RoleName, ejp.JobProfileId",
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

                if (dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0)
                {
                    var row = dataset.Tables[0].Rows[0];
                    var employeeProfile = new EmployeeProfile
                    {
                        EmpCode = row["EmpCode"]?.ToString(),
                        Name = row["Name"]?.ToString(),
                        MainDepartment = row["MainDepartment"]?.ToString(),
                        Department = row["Department"]?.ToString(),
                        DateJoin = row["DateJoin"]?.ToString(),
                        Email = row["Email"]?.ToString(),
                        Designation = row["Designation"]?.ToString(),
                        EmpPic = row["EmpPic"]?.ToString(),
                        DReportToCompny = row["dReportToCompny"]?.ToString(),
                        iDReportToCompny = row["iDReportToCompny"]?.ToString(),
                        ReportingPerson = row["ReportingPerson"]?.ToString(),
                        DottedPerson = row["DottedPerson"]?.ToString(),
                        Shift = row["Shift"]?.ToString(),
                        CompanyName = row["CompanyName"]?.ToString(),
                        DivisionName = row["DivisionName"]?.ToString(),
                        Location = row["Location"]?.ToString(),
                        JobGroup = row["JobGroup"]?.ToString(),
                        PayrollGroup = row["PayrollGroup"]?.ToString(),
                        EmployeeType = row["EmployeeType"]?.ToString(),
                        DateConfirm = row["DateConfirm"]?.ToString(),
                        DateConfirmDue = row["DateConfirmDue"]?.ToString(),
                        ContractExpiryDate = row["ContractExpiryDate"]?.ToString(),
                        InternExpiryDate = row["InternExpiryDate"]?.ToString(),
                        ProbhationExtDate = row["ProbhationExtDate"]?.ToString(),
                        IdCardRemarks = row["IdCardRemarks"]?.ToString(),
                        FamilyCardNo = row["FamilyCardNo"]?.ToString(),
                        IqamaNo = row["IqamaNo"]?.ToString(),
                        IqamaProfession = row["IqamaProfession"]?.ToString(),
                        IqamaExpiryHijri = row["IqamaExpiryHijri"]?.ToString(),
                        IqamaExpiryGregorian = row["IqamaExpiryGregorian"]?.ToString(),
                        CurrSpnsName = row["CurrSpnsName"]?.ToString(),
                        SpnsTransferable = row["SpnsTransferable"]?.ToString(),
                        SpnsType = row["SpnsType"]?.ToString(),
                        SpnsCountry = row["SpnsCountry"]?.ToString(),
                        SpnsCity = row["SpnsCity"]?.ToString(),
                        SpnsContactDetails = row["SpnsContactDetails"]?.ToString(),
                        SpnsNatureOfBusiness = row["SpnsNatureOfBusiness"]?.ToString(),
                        SpnsExpiryHijri = row["SpnsExpiryHijri"]?.ToString(),
                        SpnsExpiryGregorian = row["SpnsExpiryGregorian"]?.ToString(),
                        EmployeeCategory = row["EmployeeCategory"]?.ToString(),
                        RoleName = row["RoleName"]?.ToString(),
                        JobProfileId = row["JobProfileId"]?.ToString()
                    };

                    // You can use employeeProfile here if needed (e.g. for logging, caching, or setting to session)

                    return dataset.Tables[0]; // ✅ Returning DataTable
                }
            }
            catch (Exception ex)
            {
                // Optional: log the error
            }

            return new DataTable(); // Empty fallback
        }

        public async Task<DataTable> GetEmployeeLeavesInfoDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return new DataTable();

            try
            {
                // Call function with parameter
                string query = $"SELECT * FROM fn_Leave_GetLeaveBalanceData(@EmpId, '1')";

                // Map to your model using Dapper
                var leavesList = (await _dapperService.QueryAsync<EmployeeLeaves>(query, new { EmpId = empId })).ToList();

                // Convert list to DataTable
                return _configuration.ConvertToDataTable(leavesList);
            }
            catch (Exception ex)
            {
                // TODO: Log exception
                return new DataTable();
            }
        }

        public async Task<DataTable> GetEmployeeHolidaysInfoDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = $@"
            SELECT 
                (SELECT COUNT(*) + 1 
                 FROM tblHoliday b 
                 WHERE b.HolId < a.HolId 
                   AND YEAR(b.DateFrom) = YEAR(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId)) 
                   AND a.CompanyId = b.CompanyId) AS SrNo,
                ISNULL(Description, '') AS Description,
                CASE WHEN Flag = 1 THEN 'National' ELSE 'Festival' END AS HolidayType,
                FORMAT(a.DateFrom, 'dd/MMM/yyyy') AS DateFrom,
                FORMAT(a.DateTo, 'dd/MMM/yyyy') AS DateTo
            FROM tblHoliday a
            WHERE 
                YEAR(a.DateFrom) = YEAR(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))
                AND a.CompanyId = @CompanyId
                AND HolId NOT IN (
                    SELECT HolId 
                    FROM tblHoliday 
                    WHERE rlgId IS NOT NULL AND rlgId <> -1 
                      AND rlgId NOT IN (
                          SELECT rlgId 
                          FROM tblEmployee 
                          WHERE EmpId = @EmpId
                      )
                )
                AND HolId IN (
                    SELECT HolId 
                    FROM tblHoliday 
                    WHERE BrnIds = '0' OR ',' + BrnIds + ',' LIKE '%,' + CAST((
                        SELECT brnId 
                        FROM tblEmployee 
                        WHERE EmpId = @EmpId AND CompanyId = @CompanyId
                    ) AS VARCHAR) + ',%'
                )
                AND HolId IN (
                    SELECT HolId 
                    FROM tblHoliday 
                    WHERE RegionIds = 0 OR ',' + RegionIds + ',' LIKE '%,' + CAST((
                        SELECT RegionIds 
                        FROM tblEmployee 
                        WHERE EmpId = @EmpId AND CompanyId = @CompanyId
                    ) AS VARCHAR) + ',%'
                )
        ";

                var resultList = (await _dapperService.QueryAsync<EmployeeHolidays>(query, new { EmpId = empId, CompanyId = companyId })).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // TODO: Log error
                return new DataTable();
            }
        }

        public async Task<DataTable> GetSubjectsDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT DISTINCT 
                Subject AS subject,
                Subject AS subjectName
            FROM tblEmployeeDocument
            WHERE ShowToEmployee = 1 
              AND IssuanceDate <= dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId)
              AND EmpId = @EmpId
              AND CompanyId = @CompanyId
            ORDER BY Subject";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@CompanyId", companyId);

                var resultList = (await _dapperService.QueryAsync<Subjects>(query, parameters)).ToList();

                // Prepend the "All" row
                resultList.Insert(0, new Subjects { subject = "0", subjectName = "All" });

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Optionally log the error
                return new DataTable();
            }
        }

        public async Task<DataTable> GetDocumentsDataAsync(string empId, string companyId, string culture, string subject, string fIssueDate, string tIssueDate)
        {
            if (string.IsNullOrWhiteSpace(empId) || string.IsNullOrWhiteSpace(companyId))
                return new DataTable();

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@CompanyId", companyId);

                string whereClause = @"
            ShowToEmployee = 1 
            AND IssuanceDate <= dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId)
            AND EmpId = @EmpId 
            AND CompanyId = @CompanyId";

                if (subject?.Trim() != "0")
                {
                    whereClause += " AND Subject = @Subject";
                    parameters.Add("@Subject", subject);
                }

                bool isFIssueDateEmpty = string.IsNullOrWhiteSpace(fIssueDate) || fIssueDate == "__/__/____" || fIssueDate == "null";
                bool isTIssueDateEmpty = string.IsNullOrWhiteSpace(tIssueDate) || tIssueDate == "__/__/____" || tIssueDate == "null";

                if (!isFIssueDateEmpty && !isTIssueDateEmpty)
                {
                    string fromDate = Utilities.SetDate(fIssueDate);
                    string toDate = Utilities.SetDate(tIssueDate);
                    whereClause += " AND DocDate BETWEEN @FromDate AND @ToDate";
                    parameters.Add("@FromDate", fromDate);
                    parameters.Add("@ToDate", toDate);
                }

                string query = $@"
            SELECT 
                EmpDocId, 
                EmpId, 
                FORMAT(IssuanceDate, 'dd/MMM/yyyy') AS IssueDate, 
                Subject, 
                Remarks, 
                Received, 
                CompanyId
            FROM tblEmployeeDocument
            WHERE {whereClause}
            ORDER BY IssuanceDate DESC";

                var resultList = (await _dapperService.QueryAsync<DocumentsGrid>(query, parameters)).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Log the exception as needed
                return new DataTable();
            }
        }

        public async Task<string> UpdateEmpDocRecStatusAsync(int empDocId, bool received)
        {
            try
            {
                string sql = @"
            UPDATE tblEmployeeDocument 
            SET 
                Received = @Received,
                ReceivedFromPortal = 1
            WHERE EmpDocId = @EmpDocId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpDocId", empDocId);
                parameters.Add("@Received", received);

                int rowsAffected = await _dapperService.ExecuteAsync(sql, parameters);

                return rowsAffected > 0 ? "Success" : "No rows affected";
            }
            catch (Exception ex)
            {
                // Log error
                return $"Error: {ex.Message}";
            }
        }

        public async Task<DataTable> GetMiscAlertsDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                (SELECT COUNT(*) + 1 
                 FROM tblEmpLicenceInfo x 
                 WHERE x.EmpLicId < a.EmpLicId AND x.EmpId = @EmpId) AS SrNo,
                b.[Name], 
                FORMAT(a.ExpiryDate, 'dd/MMM/yyyy') AS [Date], 
                a.Remarks
            FROM tblEmpLicenceInfo a 
            INNER JOIN vwtblSetupsDetail b 
                ON b.sdlId = a.LicenceId AND b.Culture = @Culture
            WHERE a.EmpId = @EmpId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@Culture", culture);

                var resultList = (await _dapperService.QueryAsync<MiscAlerts>(query, parameters)).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                return new DataTable();
            }
        }

        public async Task<DataTable> GetAwardsGridDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                FORMAT(a.AwardDate, 'dd/MMM/yyyy') AS AwardDate,
                ISNULL(a.Comments, '') AS Comments,
                ISNULL(b.Name, '') AS Name
            FROM tblEmpAwards a
            INNER JOIN tblSetupsDetail b 
                ON a.Awdid = b.sdlid AND a.Companyid = b.Companyid
            WHERE a.EmpId = @EmpId AND a.CompanyId = @CompanyId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@CompanyId", companyId);

                var resultList = (await _dapperService.QueryAsync<AwardsGrid>(query, parameters)).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                return new DataTable();
            }
        }

        public async Task<DataTable> GetDisciplinaryActionsDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                EDA.DspEmpid,
                EDA.Empid,
                FORMAT(EDA.dspDate, 'dd/MMM/yyyy') AS DspDate,
                SD.Name AS Dsp,
                SD1.Name AS DspAct,
                EDA.Remarks,
                EDA.Status,
                EDA.BlackListed,
                EDA.CompanyId
            FROM tblEmpDisciplinaryActions EDA
            INNER JOIN tblSetupsDetail SD ON SD.SdlId = EDA.dspid
            INNER JOIN tblSetupsDetail SD1 ON SD1.SdlId = EDA.dspactid
            WHERE EDA.EmpId = @EmpId AND EDA.CompanyId = @CompanyId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@CompanyId", companyId);

                var resultList = (await _dapperService.QueryAsync<DisciplinaryActions>(query, parameters)).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Log the error if needed
                return new DataTable();
            }
        }

        public async Task<DataTable> GetCompletedTrainingsDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                ISNULL(c.Name, '') AS Institute,
                ISNULL(d.Course, '') AS Course,
                FORMAT(a.FromDate, 'dd/MMM/yyyy') AS DateFrom,
                FORMAT(a.ToDate, 'dd/MMM/yyyy') AS DateTo,
                ISNULL(a.Need, '') AS Need
            FROM tblTrainingRecord a
            INNER JOIN tblSetupsDetail b ON a.Catid = b.Sdlid AND a.Companyid = b.CompanyId
            INNER JOIN tblTrainingInstitutes c ON a.InstituteID = c.Instituteid AND a.Companyid = c.Companyid
            INNER JOIN tblTrainingCourses d ON a.CourseID = d.CourseID AND a.Companyid = d.CompanyId
            WHERE a.EmpId = @EmpId AND a.Companyid = @CompanyId AND a.IsCompleted = 1

            UNION

            SELECT 
                '' AS Institute,
                ISNULL(a.Course, '') AS Course,
                FORMAT(a.sDate, 'dd/MMM/yyyy') AS DateFrom,
                FORMAT(a.EDate, 'dd/MMM/yyyy') AS DateTo,
                '' AS Need
            FROM tblEmpTraining a
            INNER JOIN tblSetupsDetail b ON b.sdlId = a.EmpTrnId
            WHERE a.EmpId = @EmpId AND a.CompanyId = @CompanyId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@CompanyId", companyId);

                var resultList = (await _dapperService.QueryAsync<CompletedTrainings>(query, parameters)).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Log error if needed
                return new DataTable();
            }
        }

        public async Task<DataTable> GetAssetsDetailDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                AD.AssetNo,
                FORMAT(AD.sDate, 'dd/MMM/yyyy') AS sDate,
                AD.AssetDesc,
                CAST(AD.CatId AS VARCHAR) AS CatId,
                SD.Name AS Category,
                AD.Model,
                AD.Make,
                FORMAT(AH.HandingOverDate, 'dd/MMM/yyyy') AS HandingOverDate,
                FORMAT(AH.TakingOverDate, 'dd/MMM/yyyy') AS TakingOverDate
            FROM tblAssetDetail AD
            LEFT OUTER JOIN tblAssetHistory AH ON AH.AssetId = AD.AssetId
            INNER JOIN vwtblSetupsDetail SD ON AD.CatId = SD.SdlId AND SD.Culture = @Culture
            WHERE AH.EmpId = @EmpId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@Culture", culture);

                var resultList = (await _dapperService.QueryAsync<AssetsDetail>(query, parameters)).ToList();

                return _configuration.ConvertToDataTable(resultList);
            }
            catch (Exception ex)
            {
                // Optionally log error
                return new DataTable();
            }
        }

        public async Task<DataTable> GetTrainingNominationsConfirmationsDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                (SELECT COUNT(*) 
                 FROM tblTrainingRecord x 
                 WHERE x.TPId < a.TPId 
                   AND ISNULL(x.isCompleted, 0) = 0 
                   AND x.ToDate >= dbo.fn_General_GetLocalDateTimeCompanyWise(x.companyid)) AS SrNo,
                ISNULL(b.Name, '') AS InstituteName,
                ISNULL(c.Course, '') AS Course,
                FORMAT(a.FromDate, 'dd/MMM/yyyy') AS FromDate,
                FORMAT(a.ToDate, 'dd/MMM/yyyy') AS DateTo,
                ISNULL(a.IsNominatedByDept, 0) AS IsNominatedByDept,
                ISNULL(a.IsConfirm, 0) AS IsConfirm
            FROM tblTrainingRecord a
            INNER JOIN tblTrainingInstitutes b ON a.Instituteid = b.Instituteid
                AND b.InstituteType NOT IN (
                    SELECT sdlid FROM tblSetupsDetail 
                    WHERE smsid = 128 AND CAST(code AS INT) = 3
                )
            INNER JOIN tblTrainingCourses c ON a.CourseID = c.CourseID
            WHERE a.EmpId = @EmpId 
              AND ISNULL(a.IsCompleted, 0) = 0 
              AND a.ToDate >= dbo.fn_General_GetLocalDateTimeCompanyWise(a.CompanyId)";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);

                var resultList = (await _dapperService.QueryAsync<TrainingNominationsConfirmations>(query, parameters)).ToList();
                return _configuration.ConvertToDataTable(resultList);
            }
            catch
            {
                return new DataTable();
            }
        }

        public async Task<DataTable> GetJobDescriptionDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = @"
            SELECT 
                JobProfileId,
                ISNULL(JobCode, '') AS JobCode,
                ISNULL(JobTitle, '') AS JobTitle,
                (
                    SELECT Name 
                    FROM TblSetupsDetail 
                    WHERE smsid = 189 AND SdlId = a.RoleId AND CompanyId = a.CompanyId
                ) AS Role
            FROM TblEmpJobProfile a
            WHERE a.EmpId = @EmpId";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);

                var resultList = (await _dapperService.QueryAsync<JobDescription>(query, parameters)).ToList();
                return _configuration.ConvertToDataTable(resultList);
            }
            catch
            {
                return new DataTable();
            }
        }

        public async Task<DataTable> GetActiveLoansDataAsync(string empId, string companyId, string culture)
        {
            if (string.IsNullOrEmpty(empId) || string.IsNullOrEmpty(companyId))
                return new DataTable();

            try
            {
                string query = $@"
            SELECT 
                LM.LoanMstId, LM.LoanID, LM.EmpId, LM.Active, 
                vEMP.EmpCode, vEMP.Name AS FullName,
                REPLACE(CONVERT(VARCHAR, CONVERT(MONEY, LM.LoanAmount), 1), '.00', '') AS LoanAmtWithoutInt,
                REPLACE(CONVERT(VARCHAR, CONVERT(MONEY, LM.InterestAmount), 1), '.00', '') AS Interest,
                LM.Installment, LM.CompanyId,
                REPLACE(CONVERT(VARCHAR, CONVERT(MONEY, (LM.LoanAmount - LM.PaidAmount)), 1), '.00', '') AS BalanceAmount,
                LM.LoanName AS LoanType,
                FORMAT(LM.AppDate, 'dd/MMM/yyyy') AS AppDate,
                LM.ReceivedFromPortal
            FROM MM_LOAN LM
            INNER JOIN dbo.fn_Employee(@Culture, @CompanyId) vEMP ON vEMP.EmpId = LM.EmpId
            WHERE 
                vEMP.EmpId = @EmpId AND 
                vEMP.CompanyId = @CompanyId AND 
                vEMP.Active = 1 AND 
                LM.Active = 1 AND 
                LM.LoanID <> (SELECT ISNULL(HiddenLoan, 0) FROM tblVariable WHERE CompanyId = @CompanyId) AND 
                vEMP.PayrollStatus = 1";

                var parameters = new DynamicParameters();
                parameters.Add("@EmpId", empId);
                parameters.Add("@CompanyId", companyId);
                parameters.Add("@Culture", culture);

                var resultList = (await _dapperService.QueryAsync<ActiveLoans>(query, parameters)).ToList();
                return _configuration.ConvertToDataTable(resultList);
            }
            catch
            {
                return new DataTable();
            }
        }

        public async Task<string> UpdateLoanAsync(int loanMstId, bool received)
        {
            try
            {
                // Fetch current user info
                UserInfo objUser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());

                var parameters = new DynamicParameters();
                parameters.Add("@LoanMstId", loanMstId);
                parameters.Add("@ReceivedFromPortal", true);
                parameters.Add("@LoanAckUserId", _utilities.GetUserid(_clientContextService.GetClientIP()));
                parameters.Add("@LoanAckUserEmpId", objUser.UserEmpId);
                parameters.Add("@LoanAckUserEmpName", objUser.UserEmpName);
                parameters.Add("@LoanAckUserEmpCode", objUser.UserEmpCode);
                parameters.Add("@LoanAckEntTerminal", _utilities.GetTerminalId());
                parameters.Add("@LoanAckEntTerminalIP", _utilities.GetTerminalIP());
                parameters.Add("@LoanAckEntDate", _utilities.GetSysCurrentdate());

                string updateQuery = @"
            UPDATE tblloanmaster
            SET 
                ReceivedFromPortal = @ReceivedFromPortal,
                LoanAckUserId = @LoanAckUserId,
                LoanAckUserEmpId = @LoanAckUserEmpId,
                LoanAckUserEmpName = @LoanAckUserEmpName,
                LoanAckUserEmpCode = @LoanAckUserEmpCode,
                LoanAckEntTerminal = @LoanAckEntTerminal,
                LoanAckEntTerminalIP = @LoanAckEntTerminalIP,
                LoanAckEntDate = @LoanAckEntDate
            WHERE LoanMstId = @LoanMstId";

                int rowsAffected = await _dapperService.ExecuteAsync(updateQuery, parameters);

                return rowsAffected > 0 ? "Success" : "No rows updated";
            }
            catch (Exception ex)
            {
                // Log the exception
                return $"Error: {ex.Message}";
            }
        }


    }

    public class EmployeeProfile
    {
        public string? EmpCode { get; set; }
        public string? Name { get; set; }
        public string? MainDepartment { get; set; }
        public string? Department { get; set; }
        public string? DateJoin { get; set; }
        public string? Email { get; set; }
        public string? Designation { get; set; }
        public string? EmpPic { get; set; }
        public string? DReportToCompny { get; set; }
        public string? iDReportToCompny { get; set; }
        public string? ReportingPerson { get; set; }
        public string? DottedPerson { get; set; }
        public string? Shift { get; set; }
        public string? CompanyName { get; set; }
        public string? DivisionName { get; set; }
        public string? Location { get; set; }
        public string? JobGroup { get; set; }
        public string? PayrollGroup { get; set; }
        public string? EmployeeType { get; set; }
        public string? DateConfirm { get; set; }
        public string? DateConfirmDue { get; set; }
        public string? ContractExpiryDate { get; set; }
        public string? InternExpiryDate { get; set; }
        public string? ProbhationExtDate { get; set; }
        public string? IdCardRemarks { get; set; }
        public string? FamilyCardNo { get; set; }
        public string? IqamaNo { get; set; }
        public string? IqamaProfession { get; set; }
        public string? IqamaExpiryHijri { get; set; }
        public string? IqamaExpiryGregorian { get; set; }
        public string? CurrSpnsName { get; set; }
        public string? SpnsTransferable { get; set; }
        public string? SpnsType { get; set; }
        public string? SpnsCountry { get; set; }
        public string? SpnsCity { get; set; }
        public string? SpnsContactDetails { get; set; }
        public string? SpnsNatureOfBusiness { get; set; }
        public string? SpnsExpiryHijri { get; set; }
        public string? SpnsExpiryGregorian { get; set; }
        public string? EmployeeCategory { get; set; }
        public string? RoleName { get; set; }
        public string? JobProfileId { get; set; }
    }

    public class EmployeeLeaves
    {
        public string? Description { get; set; }
        public string? LCode { get; set; }
        public string? MaxAllowed { get; set; }
        public string? Availed { get; set; }
        public string? Balance { get; set; }
        public string? PrevBalance { get; set; }
        public string? CurrBalance { get; set; }
        public string? PendingLeaves { get; set; }
        public string? FiscalYear { get; set; }
        public string? IsGradeWiseLeaveEntitlementDifferent { get; set; }
        public string? Availed_Next { get; set; }
        public string? PendingLeaves_Next { get; set; }
        public string? PenaltyDaysDeduction { get; set; }
    }

    public class EmployeeHolidays
    {
        public string? SrNo { get; set; }
        public string? Description { get; set; }
        public string? HolidayType { get; set; }
        public string? dateFr { get; set; }
        public string? DateTo { get; set; }
    }

    public class Subjects
    {
        public int? empDocId { get; set; }
        public string? subject { get; set; }
        public string? subjectName { get; set; }
    }

    public class DocumentsGrid
    {
        public int? EmpDocId { get; set; }
        public int? EmpId { get; set; }
        public string? subject { get; set; }
        public string? Remarks { get; set; }
        public bool? Received { get; set; }
        public string? IssueDate { get; set; }
        public int? CompanyId { get; set; }
    }

    public class MiscAlerts
    {
        public string? SrNo { get; set; }
        public string? Name { get; set; }
        public string? Date { get; set; }
        public string? Remarks { get; set; }
    }

    public class AwardsGrid
    {
        public string? Name { get; set; }
        public string? AwardDate { get; set; }
        public string? Comments { get; set; }
    }

    public class DisciplinaryActions
    {
        public int DspEmpid { get; set; }
        public int Empid { get; set; }
        public string? DspDate { get; set; }
        public string? Dsp { get; set; }
        public string? DspAct { get; set; }
        public string? Remarks { get; set; }
        public bool Status { get; set; }
        public bool BlackListed { get; set; }
        public int CompanyId { get; set; }
    }

    public class CompletedTrainings
    {
        public string? Institute { get; set; }
        public string? Course { get; set; }
        public string? DateFrom { get; set; }
        public string? DateTo { get; set; }
        public string? Need { get; set; }
    }

    public class AssetsDetail
    {
        public string? AssetNo { get; set; }
        public string? sDate { get; set; }
        public string? AssetDesc { get; set; }
        public string? catid { get; set; }
        public string? Category { get; set; }
        public string? Model { get; set; }
        public string? Make { get; set; }
        public string? HandingOverDate { get; set; }
        public string? TakingOverDate { get; set; }
    }

    public class TrainingNominationsConfirmations
    {
        public int? SrNo { get; set; }
        public string? InstituteName { get; set; }
        public string? Course { get; set; }
        public string? FromDate { get; set; }
        public string? DateTo { get; set; }
        public bool? IsNominatedByDept { get; set; }
        public bool? IsConfirm { get; set; }
    }

    public class JobDescription
    {
        public int? JobProfileId { get; set; }
        public string? JobCode { get; set; }
        public string? JobTitle { get; set; }
        public string? Role { get; set; }
    }

    public class ActiveLoans
    {
        public int? LoanMstId { get; set; }
        public int? LoanID { get; set; }
        public int? EmpId { get; set; }
        public bool? Active { get; set; }
        public string? EmpCode { get; set; }
        public string? FullName { get; set; }
        public decimal? LoanAmtWithoutInt { get; set; }
        public decimal? Interest { get; set; }
        public decimal? Installment { get; set; }
        public int? CompanyId { get; set; }
        public decimal? BalanceAmount { get; set; }
        public string? LoanType { get; set; }
        public string? AppDate { get; set; }
        public bool? ReceivedFromPortal { get; set; }
    }

}
