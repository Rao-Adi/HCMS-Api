using Microsoft.AspNetCore.Mvc;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.Models;
using System.Data;
using System.Globalization;
using System.Text;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Controllers.HCMS.ESS;
using Dapper;
using System.Data.SqlClient;


namespace HCMS_Api.Components.HCMS.ESS
{
    public class MedicalReimbursementComponent : Controller
    {
        private readonly Utilities _utilities;
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly ClientContextService _clientContextService;
        private readonly IDapperDataService _dapperService;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly IHttpContextAccessor _http;
        private string tblEmpMedicalType = "tblEmpMedicaltype";
        public MedicalReimbursementComponent(
            Utilities utilities
            , DataServices dataservice
            , IConfiguration configuration
            , ClientContextService clientContextService
            , IDapperDataService dapper
            , ILogger<UtilitiesController> logger
            , IHttpContextAccessor http
            )
        {
            _http = http;
            _logger = logger;
            _utilities = utilities;
            _dataservice = dataservice;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _dapperService = dapper;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);

        }

        public DataTable GetFiscalYears(string EmpId, string CompanyId, string culture)
        {


            
            string whereclause = "CompanyId=" + CompanyId + " order by StartDate Desc";
            
            int fyType = 0;
            object objfyid = _utilities.GetScalarData("ReimbursementFYType", "tblhrpolicy", "CompanyId=" + CompanyId);
            if (objfyid != null && objfyid != DBNull.Value && objfyid.ToString() != "")
            {
                fyType = Convert.ToInt32(objfyid);
            }
            else
            {
                
                var dtNA = new DataTable();
                dtNA.Columns.Add("ID", typeof(int));
                dtNA.Columns.Add("FiscalYear", typeof(string));
                var row = dtNA.NewRow();
                row["ID"] = 0;
                row["FiscalYear"] = "N/A";
                dtNA.Rows.Add(row);
                return dtNA;
            }

            
            var ds = new DataSet();
            string result;

            if (fyType == 0)
            {
                result = _dataservice.GetDataWithClause(
                    " FYID as  Id ,dbo.fn_GetDateFormat_DDMMMYYYY( StartDate) +' - ' + dbo.fn_GetDateFormat_DDMMMYYYY(EndDate) as Name  ",
                    "TblFiscalYearSetup",
                    whereclause,
                    ref ds
                );
            }
            else
            {
                result = _dataservice.GetDataWithClause(
                    " TYID as  Id ,dbo.fn_GetDateFormat_DDMMMYYYY( StartDate) +' - ' + dbo.fn_GetDateFormat_DDMMMYYYY(EndDate) as Name  ",
                    "TblTaxYearSetup",
                    whereclause,
                    ref ds
                );
            }

            
            if (ds != null && ds.Tables.Count > 0)
            {
                var dt = ds.Tables[0];

               
                if (dt.Columns.Contains("Id") && !dt.Columns.Contains("ID"))
                    dt.Columns["Id"].ColumnName = "ID";

                if (!dt.Columns.Contains("FiscalYear"))
                    dt.Columns.Add("FiscalYear", typeof(string));

                foreach (DataRow r in dt.Rows)
                    r["FiscalYear"] = r.Table.Columns.Contains("Name") ? Convert.ToString(r["Name"]) ?? "" : "";

                if (dt.Columns.Contains("Name"))
                    dt.Columns.Remove("Name");

                return dt;
            }

           
            var empty = new DataTable();
            empty.Columns.Add("ID", typeof(int));
            empty.Columns.Add("FiscalYear", typeof(string));
            return empty;
        }
        public async Task<List<EmpDependentsInfoSetup>> GetEmpDependentsInfoAsync(
            string EmpId, string companyId)
        {
            if (!int.TryParse(EmpId, out var empIdInt) || !int.TryParse(companyId, out var companyIdInt))
                return new List<EmpDependentsInfoSetup>();

            var strrelation = await GetRelationtypeallMedicalReimburesementAsync(EmpId, companyId);

            var whereParts = new List<string>
    {
        string.IsNullOrWhiteSpace(strrelation)
            ? "DRelationshipType IN ('Nothing')"
            : $"DRelationshipType IN ({strrelation})",
        "EmpId = @EmpId",
        "Medical = 1",
        "CompanyId = @CompanyId"
    };



            var whereClause = " WHERE " + string.Join(" And ", whereParts);

            var sql =
        $@"
SELECT 
    EmpdpdId,
    [Name] = ISNULL(RTRIM(FirstName) + ' ', '') 
           + ISNULL(RTRIM(MiddleName) + ' ', '') 
           + ISNULL(RTRIM(LastName), '')  
           + ' (' + (SELECT Name FROM tblsetupsdetail WHERE sdlid = tblEmpDependentsInfo.rlnid) + ')'
FROM tblEmpDependentsInfo
{whereClause};
";

            
            var rows = (await _dapperService.QueryAsync<EmpDependentsInfoSetup>(
                sql,
                new { EmpId, CompanyId = companyId }
            )).ToList();

            
            rows.Insert(0, new EmpDependentsInfoSetup { EMPDPDID = 0, NAME = "Self" });

            return rows;
        }
        public async Task<string> GetRelationtypeallMedicalReimburesementAsync(string empId, string companyId)
        {
            
            const string sql = @"
        SELECT MedicalEntitlementScope
        FROM tblEmpMedicaltype
        WHERE CompanyId = @CompanyId AND EmpId = @EmpId
        ORDER BY MedicalEntitlementScope DESC;";

            try
            {
                var scopes = (await _dapperService.QueryAsync<string>(
                    sql,
                    new { CompanyId = companyId, EmpId = empId }
                )).ToList();

                string medicalType = "Self";

                if (scopes.Count > 0)
                {
                   
                    if (scopes.Any(s => string.Equals(s, "Self & Family & Parent", StringComparison.OrdinalIgnoreCase)))
                        medicalType = "Self & Family & Parent";
                    else if (scopes.Any(s => string.Equals(s, "Self & Family", StringComparison.OrdinalIgnoreCase)))
                        medicalType = "Self & Family";
                    else if (scopes.Any(s => string.Equals(s, "Self & Spouse", StringComparison.OrdinalIgnoreCase)))
                        medicalType = "Self & Spouse";
                    else
                        medicalType = "Self";
                }
                else
                {
                    medicalType = "Self";
                }

                if (string.IsNullOrEmpty(medicalType))
                    medicalType = "Self";

                switch (medicalType)
                {
                    case "Self":
                        return "";
                    case "Self & Spouse":
                        return "'Is Spouse'";
                    case "Self & Family":
                        return "'Is Spouse','Is Child'";
                    case "Self & Family & Parent":
                        return "'Is Spouse','Is Parent','Is Child'";
                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }

        public string GetRelationtypeallMedicalReimburesement(string EmpId)
        {
            var companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            if (!int.TryParse(companyIdStr, out var companyId))
                companyId = 0; // or throw, per your convention

            return GetRelationtypeallMedicalReimburesementAsync(EmpId, companyIdStr).GetAwaiter().GetResult();
        }
        public List<EmpDependentsInfoSetup> GetEmpDependentsInfo(string EmpId)
        {
            var companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            // optional validation; keep the value as string:
            if (!int.TryParse(companyIdStr, out _))
                companyIdStr = "0";

            return GetEmpDependentsInfoAsync(EmpId, companyIdStr)
                   .GetAwaiter().GetResult();
        }
        public async Task<DataTable> GetMasterMedicalInformationAsync(int empId, int dpdId, int fiscalYearId)
        {
            
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("DpdId", typeof(int));
            dt.Columns.Add("Dependent Name", typeof(string));
            dt.Columns.Add("MId", typeof(int));
            dt.Columns.Add("Medical Type", typeof(string));
            dt.Columns.Add("Balance Limit", typeof(double));
            dt.Columns.Add("Remaining Balance", typeof(double));
            dt.Columns.Add("MonthlyAccumulated", typeof(double));
            dt.Columns.Add("AvailableMonthlyLimit", typeof(double));
            dt.Columns.Add("Prorate", typeof(bool));
            dt.Columns.Add("AmountScope", typeof(string));
            dt.Columns.Add("Pendingforapproval", typeof(double));
            try
            {

                var clientIp = _clientContextService.GetClientIP();
                var companyIdStr = _utilities.GetCompanyId(clientIp);
                int.TryParse(companyIdStr, out var companyId);

                const string depSql = @"
                 SELECT 
                    EmpdpdId    AS EMPDPDID,
                    [Name] = ISNULL(RTRIM(FirstName)+' ','')
                            + ISNULL(RTRIM(MiddleName)+' ','')
                            + ISNULL(RTRIM(LastName),'')
                            + ' (' + (SELECT Name FROM tblSetupsDetail WHERE sdlid = d.rlnid) + ')'
                FROM tblEmpDependentsInfo d
                WHERE d.EmpId = @EmpId AND d.Medical = 1 AND d.CompanyId = @CompanyId;";

                var dpdList = (await _dapperService.QueryAsync<EmpDependentsInfoSetup>(
                    depSql, new { EmpId = empId, CompanyId = companyId }
                )).ToList();

                dpdList.Insert(0, new EmpDependentsInfoSetup { EMPDPDID = 0, NAME = "Self" });

                int id = 1;

                if (dpdList != null)
                {
                    foreach (var item in dpdList)
                    {
                        if (item.EMPDPDID != dpdId) continue;

                        string scopeCsv = GetMedicalEntitlementScopeofRelations(empId, dpdId);
                        DataTable dt1 = GetMedicalInformation(empId, fiscalYearId, scopeCsv, dpdId);

                        
                        bool isChild = false;
                        var relObj = _utilities.GetScalarData(
                            "DRelationshipType",
                            "tblEmpDependentsInfo",
                            $"CompanyId={companyId} And EmpId={empId} and Empdpdid={dpdId}"
                        );
                        if (relObj != null && relObj != DBNull.Value && !string.IsNullOrWhiteSpace(relObj.ToString()))
                        {
                            isChild = string.Equals(relObj.ToString()?.Trim(), "Is Child", StringComparison.OrdinalIgnoreCase);
                        }

                        if (dt1 != null && dt1.Rows.Count > 0)
                        {
                            foreach (DataRow dr in dt1.Rows)
                            {
                                try
                                {
                                    DateTime currentDate = await _utilities.GetSysCurrentdateAsync();
                                    int medicalId = Convert.ToInt32(dr["MId"]);
                                    (bool chkAge, string ageMessage) = await this.checkChildrenAge(
                                        empId,
                                        Convert.ToInt32(dr["MId"].ToString()),
                                        _utilities.GetSysCurrentdate(),
                                        dpdId, companyIdStr
                                    );

                                    var whereClause =
                                        $"CompanyId={companyId} and EmpId={empId} And FYID={fiscalYearId}";

                                    var dtrow = dt.NewRow();
                                    dtrow["Id"] = id;
                                    dtrow["DpdId"] = item.EMPDPDID;
                                    dtrow["Dependent Name"] = item.NAME ?? "";
                                    dtrow["Medical Type"] = Convert.ToString(dr["MedicalName"]);
                                    dtrow["Pendingforapproval"] = Convert.ToString(dr["Pendingforapproval"].ToString());
                                    dtrow["MId"] = Convert.ToInt32(dr["MTypeId"]);
                                    dtrow["Balance Limit"] = Convert.ToDouble(dr["Amount"]);
                                    dtrow["MonthlyAccumulated"] = Convert.ToDouble(string.IsNullOrWhiteSpace(Convert.ToString(dr["prorateAmount"])) ? "0" : Convert.ToString(dr["prorateAmount"]));
                                    dtrow["Prorate"] = Convert.ToBoolean(string.IsNullOrWhiteSpace(Convert.ToString(dr["Prorate"])) ? "false" : Convert.ToString(dr["Prorate"]));

                                    bool medicalAmountScope = false;
                                    if (dr["MedicalAmountScope"] != System.DBNull.Value)
                                    {
                                        medicalAmountScope = Convert.ToBoolean(dr["MedicalAmountScope"].ToString());

                                    }
                                    if (medicalAmountScope)
                                    {
                                        dtrow["AmountScope"] = "Combined";
                                    }
                                    else
                                    {
                                        whereClause += $" And DpdId={item.EMPDPDID}";
                                        dtrow["AmountScope"] = "Individual";
                                    }
                                    whereClause += $" And MedTypeId={dr["MTypeId"].ToString()}";

                                    var countObj = _utilities.GetScalarData(
                                      "Count(*) as count",
                                      "tblEmpMedical",
                                      whereClause + " And SubMedTypeId > 0"
                                  );
                                    int medCount = 0;
                                    if (countObj != null && countObj != DBNull.Value && !string.IsNullOrWhiteSpace(countObj.ToString()))
                                        medCount = Convert.ToInt32(countObj);

                                    if (medCount > 0)
                                    {
                                        whereClause += " And ( SubMedTypeId in (select DMID from tblEmpdetailMedicaltype where IncludeInMaster=1) or SubMedTypeId=0 ) ";
                                    }
                                    else
                                    {
                                        whereClause += " And SubMedTypeId=0";
                                    }

                                   
                                    var totalObj = _utilities.GetScalarData(
                                        " sum( Convert(float,isnull(dbo.fnGetDecryptData(Convert( varchar(10),Empid) +  Convert( varchar(3) ,CompanyId),EmpCurrencyAmount),0.0)) )as Amount",
                                        "tblEmpMedical",
                                        whereClause + " and (claimstatus='A' or claimstatus='P')"
                                    );

                                    double amount = 0.0;
                                    if (totalObj != null && totalObj != DBNull.Value && !string.IsNullOrWhiteSpace(totalObj.ToString()))
                                        amount = Convert.ToDouble(totalObj);
                                    if (amount > 0.0)
                                    {
                                        dtrow["Remaining Balance"] = Convert.ToDouble(Convert.ToDouble(dr["amount"].ToString()) - (amount));
                                    }
                                    else
                                    {
                                        dtrow["Remaining Balance"] = Convert.ToDouble(Convert.ToDouble(dr["amount"].ToString()));
                                        amount = 0.0;
                                    }


                                    if (dr["Prorate"] != DBNull.Value &&
                                        !string.IsNullOrWhiteSpace(Convert.ToString(dr["Prorate"])) &&
                                        Convert.ToBoolean(Convert.ToString(dr["Prorate"])) == true)
                                    {
                                        var whereToToday = whereClause +
                                            " and mDate<='" + _utilities.ReturnDBDate(Convert.ToDateTime(_utilities.GetSysCurrentdate())) + "'";

                                        var toDateObj = _utilities.GetScalarData(
                                            " sum( Convert(float,isnull(dbo.fnGetDecryptData(Convert( varchar(10),Empid) +  Convert( varchar(3) ,CompanyId),EmpCurrencyAmount),0.0)) )as Amount",
                                            "tblEmpMedical",
                                            whereToToday + " and (claimstatus='A' or claimstatus='P')"
                                        );

                                        amount = 0.0;
                                        if (toDateObj != null && toDateObj != DBNull.Value && !string.IsNullOrWhiteSpace(toDateObj.ToString()))
                                            amount = Convert.ToDouble(toDateObj);
                                        if (amount > 0.0)
                                        {
                                            dtrow["AvailableMonthlyLimit"] = Convert.ToDouble(Convert.ToDouble(dr["prorateAmount"].ToString()) - (amount));

                                        }
                                        else
                                        {
                                            dtrow["AvailableMonthlyLimit"] = Convert.ToDouble(Convert.ToDouble(dr["prorateAmount"].ToString()));
                                        }

                                    }

                                    if (!isChild)
                                    {
                                        dt.Rows.Add(dtrow);
                                        dt.AcceptChanges();
                                    }
                                    else
                                    {
                                        if (!chkAge)
                                        {
                                            int hasEntries = 0;
                                            var existObj = _utilities.GetScalarData(
                                                "Count(*)",
                                                "tblEmpMedical",
                                                $"CompanyId={companyId} And EmpId={empId} and DpdId={dpdId} And MedTypeId={dtrow["MId"]}"
                                            );

                                            if (existObj != null && existObj != DBNull.Value && !string.IsNullOrWhiteSpace(existObj.ToString()))
                                                hasEntries = Convert.ToInt32(existObj);

                                            if (hasEntries > 0)
                                            {
                                                dtrow["Medical Type"] = dtrow["Medical Type"] + " (Ceased Due to age limit)";
                                                dt.Rows.Add(dtrow);
                                                dt.AcceptChanges();
                                            }
                                        }
                                        else
                                        {
                                            dt.Rows.Add(dtrow);
                                            dt.AcceptChanges();
                                        }
                                    }
                                    id++;
                                }

                                catch (Exception innerEx)
                                {
                                   
                                    _logger.LogError(innerEx, "Row processing error in GetMasterMedicalInformation");
                                }
                            }
                        }

                    }
                }


            }
            catch
            {

                return dt;
            }
            return dt;
        }
        public async Task<string> GetMedicalEntitlementScopeofRelationsAsync(int empId, int dpdId, int companyId)
        {
            
            if (dpdId == 0)
                return "'Self','Self & Family','Self & Spouse','Self & Family & Parent'";

            const string sql = @"
        SELECT DRelationshipType
        FROM tblEmpDependentsInfo
        WHERE CompanyId = @CompanyId
          AND EmpId     = @EmpId
          AND Empdpdid  = @DpdId;";

            try
            {
                var relationType = (await _dapperService.QueryAsync<string>(
       sql,
       new { CompanyId = companyId, EmpId = empId, DpdId = dpdId }
   )).FirstOrDefault();

                
                string dRelationType = "";
                switch (relationType)
                {
                    case "Is Child":
                        dRelationType = "'Self & Family','Self & Family & Parent'";
                        break;
                    case "Is Spouse":
                        dRelationType = "'Self & Family','Self & Spouse','Self & Family & Parent'";
                        break;
                    case "Is Parent":
                        dRelationType = "'Self & Family & Parent'";
                        break;
                       
                }

                return dRelationType;
            }
            catch
            {
                
                return "";
            }
        }
        public string GetMedicalEntitlementScopeofRelations(int EmpId, int DpdId)
        {
            var ip = _clientContextService.GetClientIP();
            var companyIdStr = _utilities.GetCompanyId(ip);
            int.TryParse(companyIdStr, out var companyId);

            return GetMedicalEntitlementScopeofRelationsAsync(EmpId, DpdId, companyId)
                   .GetAwaiter().GetResult();
        }

        public async Task<DataTable> GetMedicalInformationAsync(
    int EmpId, int FiscalyearId, string RelationShipType, int dpdId)
        {
            var empty = new DataTable();

            var ip = _clientContextService.GetClientIP();
            var companyIdStr = _utilities.GetCompanyId(ip);
            if (!int.TryParse(companyIdStr, out var companyId))
                companyId = 0;

            string strqry;
            if (CheckEmployeeConfirmation(EmpId, companyIdStr))
            {
                strqry =
                    " EmpId = " + EmpId +
                    " and isCurrent=1 And CompanyId = " + companyId +
                    " And FYID=" + FiscalyearId +
                    " And LTRIM(MedicalEntitlementScope) in (" + RelationShipType + ")";
            }
            else
            {
                strqry =
                    " EmpId = " + EmpId +
                    " and iscurrent=1 And CompanyId = " + companyId +
                    " And FYID=" + FiscalyearId +
                    " And  OnJoiningAndCofirmation=0 and LTRIM(MedicalEntitlementScope) in (" + RelationShipType + ")";
            }


            string select =
                @" MId,Prorate ,MtypeId,MedicalEntitlementScope,MedicalAmountScope,ApplyChildrenAgeScope,ChildrenAgeRange,
           ( Select Name from tblMedicaltype where MEDId= tblEmpMedicaltype.MtypeId) as MedicalName,
           Convert( decimal(18,2), isnull (dbo.fnGetDecryptData( cast ( EmpId as Varchar(10)) + cast (CompanyId as varchar(10)),Amount),0) ) as Amount,
           ( Select  isnull (sum(Cast( dbo.fnGetDecryptData( cast ( EmpId as Varchar(10)) + cast (CompanyId as varchar(10)),Amount) as decimal(18,3))) ,0)
               from    tblEmpMedical
               WHERE   EmpId = " + EmpId + @"
                       and CompanyId = " + companyId + @"
                       and FYID = " + FiscalyearId + @"
                       and ClaimStatus = 'P'
                       and MedTypeId= tblEmpMedicaltype.MtypeId
                       and DpdId = " + dpdId + @"
           ) as [Pendingforapproval],
           dbo.Fn_ValidateMedicalAmount(EmpId ,CompanyId ,FYID ,MTypeId ) as prorateAmount";

            string sql = "SELECT " + select + " FROM tblEmpMedicaltype WHERE " + strqry + ";";

            try
            {
                var rows = (await _dapperService.QueryAsync<MedicalInformationRow>(sql, null)).ToList();


                return _configuration.ConvertToDataTable<MedicalInformationRow>(rows);
            }
            catch
            {

                return empty;
            }
        }
        public DataTable GetMedicalInformation(int EmpId, int FiscalyearId, string RelationShipType, int dpdId)
            => GetMedicalInformationAsync(EmpId, FiscalyearId, RelationShipType, dpdId)
               .GetAwaiter().GetResult();
        public bool CheckEmployeeConfirmation(int EmpId, string companyId)
        {
            int joiningstatus = 0;
            bool checkflag = false;



            object obj = _utilities.GetScalarData("DateConfirm", "tblEmployee", "CompanyId=" + companyId + " And EmpId=" + EmpId.ToString());

            if (obj != null && obj != DBNull.Value && obj.ToString() != "")
            {

                checkflag = true;

            }

            return checkflag;

        }
     
        public async Task<(bool, string)> checkChildrenAge(int empId, int medicalId, DateTime claimDate, int dpdId, string companyId)
        {
            // If the claim is for the employee (dpdId = 0), validation passes immediately.
            if (dpdId == 0)
            {
                return (true, string.Empty);
            }

            try
            {
                // 1. Get the dependent's relationship type
                const string relationshipSql = "SELECT lTrim(DRelationshipType) FROM tblEmpDependentsInfo WHERE Empdpdid = @DpdId AND CompanyId = @CompanyId AND EmpId = @EmpId";
                var relationshipType = await _dapperService.ExecuteScalarAsync<string>(relationshipSql, new { DpdId = dpdId, CompanyId = companyId, EmpId = empId });

                // If not a child, the age rule doesn't apply.
                if (!"Is Child".Equals(relationshipType?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return (true, string.Empty);
                }

                // 2. Get the age limit for this specific medical policy
                const string ageLimitSql = "SELECT ChildrenAgeRange FROM TblEmpMedicalType WHERE ApplyChildrenAgeScope = 1 AND CompanyId = @CompanyId AND EmpId = @EmpId AND MId = @MedicalId";
                var conditionAge = await _dapperService.ExecuteScalarAsync<double?>(ageLimitSql, new { CompanyId = companyId, EmpId = empId, MedicalId = medicalId });

                // If no age limit is defined for this policy, validation passes.
                if (!conditionAge.HasValue || conditionAge.Value == 0.0)
                {
                    return (true, string.Empty);
                }

                // 3. Get the child's Date of Birth
                const string dobSql = "SELECT DOB FROM tblEmpDependentsInfo WHERE Empdpdid = @DpdId AND CompanyId = @CompanyId AND EmpId = @EmpId";
                var dob = await _dapperService.ExecuteScalarAsync<DateTime?>(dobSql, new { DpdId = dpdId, CompanyId = companyId, EmpId = empId });

                if (!dob.HasValue)
                {
                    return (false, "Validation failed: Date of Birth for the selected child is not available.");
                }

                // 4. Calculate age and perform the final check
                TimeSpan spanDate = claimDate - dob.Value;
                double years = spanDate.TotalDays / 365.25;

                if (years > conditionAge.Value)
                {
                    return (false, $"The claim cannot be processed as the dependent's age exceeds the limit of {conditionAge.Value} years for this entitlement.");
                }

                return (true, string.Empty); // All checks passed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CheckChildrenAgeAsync validation.");
                return (false, "An unexpected error occurred during child age validation.");
            }
        }
        public List<MedicalTypeSetup> GetEmpMedicalInfo(string EmpId, string ClaimStatus, string companyId, int fiscalYearid)
        {
            try
            {
                DataSet ds = new DataSet();
                List<MedicalTypeSetup> empMedLst = new List<MedicalTypeSetup>();
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;

                
                sbr.Append("EXEC sp_GetEmpMedical ");
                sbr.Append("@EmpId = " + EmpId + ", ");
                sbr.Append("@ClaimStatus = '" + (ClaimStatus ?? string.Empty).Replace("'", "''") + "', ");
                sbr.Append("@CompanyId = '" + (companyId ?? string.Empty).Replace("'", "''") + "', ");
                sbr.Append("@FiscalYearId = " + fiscalYearid);

                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0] != null)
                {
                    var http = _http?.HttpContext;
                    if (http != null)
                    {
                        http.Session.SetInt32("MedicalReimbursementess", ds.Tables[0].Rows.Count);
                    }

                    if (ds.Tables[0].Rows.Count > 0)
                    {
                        foreach (DataRow datarow in ds.Tables[0].Rows)
                        {
                            MedicalTypeSetup empMedObj = new MedicalTypeSetup
                            {
                                MedicalName = datarow["MedicalName"] == DBNull.Value ? "" : datarow["MedicalName"].ToString(),
                                MEDID = datarow["MedId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["MedId"]),
                                EMPID = datarow["EmpId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["EmpId"]),
                                MEDTYPEID = datarow["MedTypeId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["MedTypeId"]),
                                HOSID = datarow["HosId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["HosId"]),
                                WARDNO = datarow["WardNo"] == DBNull.Value ? "" : datarow["WardNo"].ToString(),
                                DOCTORNAME = datarow["DoctorName"] == DBNull.Value ? "" : datarow["DoctorName"].ToString(),
                                ADMREASON = datarow["AdmReason"] == DBNull.Value ? "" : datarow["AdmReason"].ToString(),
                                ADMMODE = datarow["AdmMode"] == DBNull.Value ? "" : datarow["AdmMode"].ToString(),
                                ADMDURATION = datarow["AdmDuration"] == DBNull.Value ? "" : datarow["AdmDuration"].ToString(),
                                PRCDONE = datarow["PrcDone"] == DBNull.Value ? false : Convert.ToBoolean(datarow["PrcDone"]),
                                MEDICINEPRESCRIBE = datarow["MedicinePrescribe"] == DBNull.Value ? "" : datarow["MedicinePrescribe"].ToString(),
                                VOUCHERNO = datarow["VoucherNo"] == DBNull.Value ? "" : datarow["VoucherNo"].ToString(),
                                Hospitalname = datarow["hospitalName"] == DBNull.Value ? "" : datarow["hospitalName"].ToString(),
                                DPDID = datarow["DpdId"] == DBNull.Value ? "0" : datarow["DpdId"].ToString(),
                                DpdName = datarow["DpdName"] == DBNull.Value ? "Self" : datarow["DpdName"].ToString(),
                                DpdRel = (datarow["DpdRel"] == DBNull.Value || string.IsNullOrEmpty(datarow["DpdRel"].ToString())) ? "" : datarow["DpdRel"].ToString(),
                                MDATE = (datarow["MDate1"] == DBNull.Value || string.IsNullOrEmpty(datarow["MDate1"].ToString())) ? "" : datarow["MDate1"].ToString(),
                                TOTALCLAIM = datarow["TotalClaim"] == DBNull.Value ? 0 : Convert.ToDecimal(datarow["TotalClaim"]),
                                LESSDISALLOWED = datarow["LessDisallowed"] == DBNull.Value ? 0 : Convert.ToDecimal(datarow["LessDisallowed"]),
                                AMOUNT = datarow["Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(datarow["Amount"]),
                                CurrencyCode = datarow["CurrencyCode"] == DBNull.Value ? "" : datarow["CurrencyCode"].ToString(),
                                CURRENCYID = datarow["CurrencyId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["CurrencyId"]),
                                CONVERSIONRATE = datarow["CurrencyRate"] == DBNull.Value ? 0 : Convert.ToDecimal(datarow["CurrencyRate"]),
                                PAID = datarow["Paid"] == DBNull.Value ? false : Convert.ToBoolean(datarow["Paid"]),
                                COMMENTS = datarow["Comments"] == DBNull.Value ? "" : datarow["Comments"].ToString(),
                                Extention = datarow["Extention"] == DBNull.Value ? ".doc" : datarow["Extention"].ToString(),
                                DocumentBody = datarow["DocumentBody"] == DBNull.Value ? null : (byte[])datarow["DocumentBody"],
                                Status = (datarow["DocumentBody"] == DBNull.Value) ? "File Not Attached" : "File Attached",
                                SUBMEDTYPEID = datarow["SubMedTypeId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["SubMedTypeId"]),
                                SubMedicalName = datarow["SubMedicalName"] == DBNull.Value ? "N/A" : datarow["SubMedicalName"].ToString(),
                                FiscalYearID = datarow["FYId"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["FYId"])
                            };

                            empMedLst.Add(empMedObj);
                        }
                    }
                }
                else
                {
                    var http = _http?.HttpContext;
                    if (http != null)
                    {
                        http.Session.SetInt32("MedicalReimbursementess", 0);
                    }
                }

                return empMedLst;
            }
            catch
            {
                return null;
            }
        }
        public async Task<MedicalTypeSetup?> GetEmpMedical(string empId, int recordId)
        {

            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            const string sql = @"
        SELECT 
            EM.MedId,
            (SELECT Name FROM tblMedicaltype 
                WHERE MedId IN (SELECT MTypeId FROM tblEmpMedicaltype 
                                WHERE MId = EM.MedTypeId AND CompanyId = @CompanyId)) AS MedicalName,
            EM.FYId AS FiscalYearID,
            CASE 
                WHEN EM.SubMedTypeId = 0 THEN 'N/A' 
                ELSE (SELECT Name FROM tblEmpDetailMedicalType WHERE DMId = EM.SubMedTypeId) 
            END AS SubMedicalName,
            EM.SubMedTypeId AS SUBMEDTYPEID, 
            EM.EmpId AS EMPID, 
            EM.MedTypeId AS MEDTYPEID, 
            EM.HosId AS HOSID,
            (SELECT Name FROM tblSetupsdetail WHERE sdlid = EM.HosId) AS Hospitalname,
            EM.WardNo AS WARDNO, 
            EM.DoctorName AS DOCTORNAME, 
            EM.AdmReason AS ADMREASON, 
            EM.AdmMode AS ADMMODE, 
            EM.prcDone AS PRCDONE, 
            EM.AdmDuration AS ADMDURATION, 
            EM.MedicinePrescribe AS MEDICINEPRESCRIBE, 
            EM.VoucherNo AS VOUCHERNO,
            (CASE WHEN EMD.MedId IS NOT NULL THEN 1 ELSE 0 END) AS HasAttachment,
            EMD.Extention,
            
            
            EM.DpdId AS DPDID,
            (
                SELECT CASE 
                    WHEN MiddleName = '' OR MiddleName IS NULL THEN FirstName + ' ' + LastName 
                    ELSE RTRIM(FirstName) + ' ' + RTRIM(MiddleName) + ' ' + RTRIM(LastName) 
                END
                FROM tblEmpDependentsInfo 
                WHERE Medical = 1 AND CompanyId = @CompanyId AND Empdpdid = EM.DpdId
            ) AS DpdName,
            EM.MDate AS MDATE, 
            EM.AdmissionDate, 
            EM.DischargeDate,
            CONVERT(decimal(18,2), dbo.fngetDecryptData(CAST(EM.Empid AS varchar) + CAST(EM.CompanyId AS varchar), EM.TotalClaim)) AS TOTALCLAIM,
            CONVERT(decimal(18,2), dbo.fngetDecryptData(CAST(EM.Empid AS varchar) + CAST(EM.CompanyId AS varchar), EM.LessDisallowed)) AS LESSDISALLOWED,
            CONVERT(decimal(18,2), dbo.fngetDecryptData(CAST(EM.Empid AS varchar) + CAST(EM.CompanyId AS varchar), EM.Amount)) AS AMOUNT,
            EM.CurrencyId AS CURRENCYID, 
            (SELECT Name FROM tblsetupsdetail WHERE sdlid = EM.CurrencyId) AS CurrencyName,
            EM.CurrencyRate AS CONVERSIONRATE, 
            EM.Paid AS PAID, 
            EM.Comments AS COMMENTS,
            (SELECT SD.Name FROM tblSetupsDetail SD 
                WHERE SD.sdlid = (SELECT EDI.rlnid FROM tblEmpDependentsInfo EDI WHERE EDI.Empdpdid = EM.DpdId)) AS DpdRel,
            EM.ChequeNumber, 
            dbo.fn_GetDateFormat_DDMMMYYYY(EM.ChequeDate) AS ChequeDate,
            ISNULL(
                (SELECT TOP 1 BB.bnkid FROM tblBankBranch BB WHERE BB.bnkbrnid = EM.bnkbrnid),
                (SELECT TOP 1 sdlid FROM tblSetupsDetail WHERE smsid = 53 AND Name LIKE '%N/A%' AND CompanyId = EM.CompanyId)
            ) AS bnkid,
            EM.Disallowedreason,
            (SELECT SD.Name FROM tblSetupsDetail SD 
                WHERE SD.sdlid = (SELECT TOP 1 BB.bnkid FROM tblBankBranch BB WHERE BB.bnkbrnid = EM.bnkbrnid)) AS BankName,
            EM.bnkbrnid,
            (SELECT TOP 1 BB.brnName FROM tblBankBranch BB WHERE BB.bnkbrnid = EM.bnkbrnid) AS BranchName,
            FORMAT(EM.DateOfEntry, 'dd/MMM/yyyy') AS DateOfEntry
        FROM tblEmpMedical EM
        LEFT JOIN HCMS_Files.dbo.tblEmpMedical_Document EMD ON EM.MedId = EMD.MedId
        WHERE EM.EmpId = @EmpId AND EM.CompanyId = @CompanyId AND EM.MedId = @RecordId;";

            try
            {
                var results = await _dapperService.QueryAsync<MedicalTypeSetup>(
              sql,
              new
              {
                  EmpId = empId,
                  RecordId = recordId,
                  CompanyId = companyId
              }
          ).ConfigureAwait(false);
                var empMedicalData = results.FirstOrDefault();

                if (empMedicalData != null)
                {
                    empMedicalData.DpdName ??= "Self";
                    empMedicalData.SubMedicalName ??= "N/A";
                }

                return empMedicalData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GetEmpMedical data.");
                return null;
            }
        }
        public async Task<DataTable> GetEmployeeNetAmountAsync(string empId, string companyId, int fiscalYearId, string claimStatus)
        {
            var empty = new DataTable();

            const string sql = @"
        SELECT
          Amount = CAST(
             ISNULL(SUM(CONVERT(float, ISNULL(
                   dbo.fnGetDecryptData(CONVERT(varchar(10), EmpId) + CONVERT(varchar(3), CompanyId), EmpCurrencyAmount)
                 , 0.0))), 0) AS decimal(18,2))
        FROM tblEmpMedical
        WHERE EmpId       = @EmpId
          AND CompanyId   = @CompanyId
          AND ClaimStatus = @ClaimStatus
          AND FYId        = @FiscalYearId;";

            try
            {
                
                var list = (await _dapperService.QueryAsync<decimal?>(
                    sql,
                    new
                    {
                        EmpId = empId,
                        CompanyId = companyId,
                        ClaimStatus = claimStatus,
                        FiscalYearId = fiscalYearId
                    }
                )).ToList();
                var amount = list.FirstOrDefault() ?? 0m;
                var formatted = amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

                var shaped = new List<NetAmountRow>
        {
                new NetAmountRow { Amount = amount, AmountText = formatted }
        };

                return _configuration.ConvertToDataTable(shaped);
            }
            catch
            {

                return empty;
            }
        }
        public List<EmpMedicalType> GetEmpMedicalType(int EmpId, string FyId, string depdId)
        {
            DataSet dataset = new DataSet();
            var ip = _clientContextService.GetClientIP();
            var companyId = _utilities.GetCompanyId(ip);
            List<EmpMedicalType> list = new List<EmpMedicalType>();
            string whereClause = "";
            if (FyId == null)
            {
                EmpMedicalType item112 = new EmpMedicalType();
                item112.ID = 0;
                item112.NAME = "N/A";
                list.Add(item112);
                return list;
            }
            int fyid = Convert.ToInt32(FyId);
            int did = Convert.ToInt32(depdId);
            string str = GetMedicalEntitlementScopeofRelations(EmpId, did);
            if (CheckEmployeeConfirmation(EmpId, companyId))
            {
                whereClause = " EmpId = " + EmpId + " And CompanyId = " + Convert.ToInt32(companyId) + " And FYID=" + FyId.ToString() + " And LTRIM(MedicalEntitlementScope) in ( " + str + " ) Order By Name";
            }
            else
            {
                whereClause = " EmpId = " + EmpId + " And CompanyId = " + Convert.ToInt32(companyId) + " And FYID=" + FyId.ToString() + " And  OnJoiningAndCofirmation=0 And LTRIM(MedicalEntitlementScope) in (" + str + ") Order By Name";
            }
            string result = _dataservice.GetDataWithClause("Mid as Id,MtypeId,(Select Name from TblMedicalType where MedId=tblEmpMedicalType.MtypeId and CompanyId=tblEmpMedicalType.CompanyId)as Name", tblEmpMedicalType, whereClause, ref dataset);
            EmpMedicalType item1 = new EmpMedicalType();
            item1.ID = 0;
            item1.NAME = "N/A";
            list.Add(item1);
            if (dataset != null && dataset.Tables.Count > 0)
            {
                DataTable dt = dataset.Tables[0];
                foreach (DataRow dr in dt.Rows)
                {
                    EmpMedicalType item = new EmpMedicalType();
                    if (!dr.IsNull("MtypeId"))
                        item.ID = Convert.ToInt32(dr["MtypeId"].ToString());
                    item.NAME = dr["Name"] == DBNull.Value ? "" : dr["Name"].ToString();
                    list.Add(item);

                }
            }
            return list;
        }
        public List<HospitalSetup> GetHospital()
        {
            var dataset = new DataSet();
            var hosLst = new List<HospitalSetup>();

            try
            {
                var ip = _clientContextService.GetClientIP();
                var companyId = _utilities.GetCompanyId(ip);

                // Columns/table passed to the helper
                string columns = "0 as RecId, sdlid, [Name]";
                string table = "tblSetupsDetail";

                // Keep exact UNION logic so 'N/A' comes first, then the rest, ordered by RecId, Name
                string whereClause =
                    $" smsId = 68 And CompanyId = {companyId} and name like 'N/A' " +
                    $" Union Select 1 as RecId, sdlid,[Name] from tblSetupsDetail " +
                    $" where smsId = 68 And CompanyId = {companyId} and name not like 'N/A' " +
                    $" order by Recid, name";

                string result = _dataservice.GetDataWithClause(columns, table, whereClause, ref dataset);

                if (dataset != null && dataset.Tables.Count > 0)
                {
                    foreach (DataRow datarow in dataset.Tables[0].Rows)
                    {
                        var hosObj = new HospitalSetup
                        {
                            HOSID = datarow["sdlid"] == DBNull.Value ? 0 : Convert.ToInt32(datarow["sdlid"]),
                            NAME = datarow["Name"] == DBNull.Value ? "" : Convert.ToString(datarow["Name"])
                        };
                        hosLst.Add(hosObj);
                    }
                }
            }
            catch
            {
                // Swallow to match existing pattern
            }

            return hosLst;
        }
        public List<FillComboSetupsDetail> GetBank()
        {
            var dataset = new DataSet();
            var banks = new List<FillComboSetupsDetail>();

            try
            {
                var ip = _clientContextService.GetClientIP();
                var companyId = _utilities.GetCompanyId(ip);

                // Keep the exact UNION/ordering logic and filter (same as your original)
                string columns = "sdlid,Name";
                string table =
                    "(Select sdlId, Name, '' as [Dummy] From tblSetupsDetail " +
                    $" Where smsId = 53 And CompanyId = {companyId} And Name Like '%N/A%' " +
                    " UNION " +
                    " Select sdlId, Name, Name as [Dummy] From tblSetupsDetail " +
                    $" Where smsId = 53 And CompanyId = {companyId} And Name <> 'N/A') a";

                string whereClause =
                    "1=1 and sdlid in (" +
                    $" select bnkId from tblBankBranch where CompanyId = {companyId} " +
                    " UNION " +
                    $" Select sdlId From tblSetupsDetail Where smsId = 53 And CompanyId = {companyId} And Name Like '%N/A%')" +
                    " Order by [Dummy]";

                string _ = _dataservice.GetDataWithClause(columns, table, whereClause, ref dataset);

                // Ensure "N/A" exists if not returned by the query (same safety as your code)
                if (dataset.Tables.Count > 0)
                {
                    var dt = dataset.Tables[0];

                    var naRows = dt.Select("Name = 'N/A'");
                    if (naRows.Length < 1)
                    {
                        banks.Add(new FillComboSetupsDetail { SdlID = 0, Name = "N/A" });
                    }

                    foreach (DataRow row in dt.Rows)
                    {
                        banks.Add(new FillComboSetupsDetail
                        {
                            SdlID = Convert.ToInt32(row["sdlId"]),
                            Name = Convert.ToString(row["Name"])
                        });
                    }
                }

                if (banks.Count < 1)
                {
                    banks.Add(new FillComboSetupsDetail { SdlID = 0, Name = "N/A" });
                }
            }
            catch
            {
                if (banks.Count < 1)
                {
                    banks.Add(new FillComboSetupsDetail { SdlID = 0, Name = "N/A" });
                }
            }
            return banks;
        }
        public List<Currency> GetCurrency()
        {
            
            var sql = @"
        SELECT 
            sdlId AS SDLID,
            Code,
            Name AS CURRENCY
        FROM tblSetupsDetail
        WHERE smsid = 87 AND CompanyId = @CompanyId
        ORDER BY
            CASE WHEN Name = 'N/A' THEN 0 ELSE 1 END,
            Code,
            Name;";
            try
            {
                var ip = _clientContextService.GetClientIP();
                var companyId = _utilities.GetCompanyId(ip);
                var task = _dapperService.QueryAsync<Currency>(sql, new { CompanyId = companyId });
                return task.GetAwaiter().GetResult().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the currency list.");
                return new List<Currency>();
            }
        }
        public async Task<MedicalDocument?> GetDocumentByRecordId(int recordId)
        {
            const string sql = @"
            SELECT 
                MedId,
                EmpId,
                DocumentBody,
                Extention
            FROM 
                HCMS_Files.dbo.tblEmpMedical_Document 
            WHERE 
                MedId = @RecordId;";

            try
            {
                var result = await _dapperService.QueryAsync<MedicalDocument>(
                    sql,
                    new { RecordId = recordId }
                ).ConfigureAwait(false);


                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document for RecordId: {RecordId}", recordId);
                return null;
            }
        }
        public async Task<int> GetConversionCountAsync(int currencyId)
        {
            const string sql = @"
        SELECT COUNT(*) 
        FROM tblExchangeRateHistory 
        WHERE CurrencyId = @CurrencyId;";

            try
            {

                var count = await _dapperService.ExecuteScalarAsync<int>(
                    sql,
                    new { CurrencyId = currencyId }
                ).ConfigureAwait(false);

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversion count for CurrencyId: {CurrencyId}", currencyId);
                return 0; 
            }
        }
        public async Task<bool> IsBaseCurrencyAsync(int currencyId)
        {
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            const string sql = "SELECT BaseCurrencyId FROM tblHRPolicy WHERE CompanyId = @CompanyId";

            try
            {
                var baseCurrencyId = await _dapperService.ExecuteScalarAsync<int>(sql, new { CompanyId = companyId });
                return baseCurrencyId == currencyId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking base currency for CompanyId: {CompanyId}", companyId);
                return false;
            }
        }
        public async Task<decimal> GetConversionRateForDateAsync(int currencyId, string transactionDate)
        {
            
            if (string.IsNullOrWhiteSpace(transactionDate)) return 0;

            const string sql = "SELECT ConversionRate FROM tblExchangeRateHistory WHERE CurrencyId = @CurrencyId AND ConversionDate = @TransactionDate";

            try
            {
                return await _dapperService.ExecuteScalarAsync<decimal>(sql, new { CurrencyId = currencyId, TransactionDate = transactionDate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversion rate for CurrencyId: {CurrencyId}", currencyId);
                return 0;
            }
        }
        public async Task<decimal> GetLastConversionRateAsync(int currencyId)
        {
            const string sql = "SELECT TOP 1 ConversionRate FROM tblExchangeRateHistory WHERE CurrencyId = @CurrencyId ORDER BY ConversionDate DESC";

            try
            {
                return await _dapperService.ExecuteScalarAsync<decimal>(sql, new { CurrencyId = currencyId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last conversion rate for CurrencyId: {CurrencyId}", currencyId);
                return 0;
            }
        }
        public async Task<int> GetEmpMedicalIdInSalaryTableAsync(int empId, int medicalId, int fyId)
        {
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            const string sql = @"
        SELECT Mid FROM tblEmpMedicaltype 
        WHERE CompanyId = @CompanyId 
          AND IsCurrent = 1 
          AND EmpId = @EmpId 
          AND Mtypeid = @MedicalId 
          AND FYID = @FyId;";

            try
            {
                return await _dapperService.ExecuteScalarAsync<int>(sql,
                    new { CompanyId = companyId, EmpId = empId, MedicalId = medicalId, FyId = fyId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting EmpMedicalIdInSalaryTable.");
                return 0;
            }
        }
        public async Task<string> CheckEmployeeServiceDateAsync(int empId, int medicalId, DateTime claimDate)
        {
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            const string settingSql = "SELECT OnJoiningAndCofirmation FROM tblEmpMedicaltype WHERE CompanyId = @CompanyId AND EmpId = @EmpId AND MId = @MedicalId";

            try
            {
                var joiningStatus = await _dapperService.ExecuteScalarAsync<int?>(settingSql,
                    new { CompanyId = companyId, EmpId = empId, MedicalId = medicalId });

                string dateColumn = (joiningStatus == 0) ? "DateJoin" : "DateConfirm";
                string errorMessage = (joiningStatus == 0)
                    ? "The amount cannot be claimed as Date of Claim is earlier than Employee Joining Date."
                    : "The amount cannot be claimed as Date of Claim is earlier than Employee Confirmation Date.";

                string employeeDateSql = $"SELECT {dateColumn} FROM tblEmployee WHERE CompanyId = @CompanyId AND EmpId = @EmpId";
                var result = await _dapperService.QueryAsync<DateTime?>(employeeDateSql,
           new { CompanyId = companyId, EmpId = empId });

                var employeeDate = result.FirstOrDefault();

                if (employeeDate.HasValue && employeeDate.Value.Date > claimDate.Date)
                {
                    return errorMessage;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckEmployeeServiceDateAsync.");
                return "An error occurred during service date validation.";
            }
        }
        private async Task<bool> GetStatusAmountScope(int medicalTypeId, int empId, int fiscalYearId)
        {
            try
            {
                var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

                const string sql = @"
            SELECT MedicalAmountScope
            FROM tblEmpMedicalType
            WHERE EmpId = @EmpId
              AND IsCurrent = 1
              AND CompanyId = @CompanyId
              AND FYID = @FiscalYearId
              AND MTypeId = @MedicalTypeId";

                var parameters = new { EmpId = empId, CompanyId = companyId, FiscalYearId = fiscalYearId, MedicalTypeId = medicalTypeId };

                var result = await _dapperService.ExecuteScalarAsync<bool?>(sql, parameters); // Removed 'await'

                return result ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStatusAmountScope for EmpId={EmpId}, MedicalTypeId={MedicalTypeId}, FYID={FiscalYearId}", empId, medicalTypeId, fiscalYearId);
                return false; 
            }
        }
        public async Task<bool> CheckAmountValidationAsync(int empId, int medicalId, int tranCurrencyId, int dependentId, double claimAmount, DateTime claimDate, int remId, int fiscalYearId)
        {
            bool flag = false;
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                const string empCurrencySql = "SELECT CurrencyId FROM tblEmpSalarySetup WHERE EmpId = @EmpId AND CompanyId = @CompanyId";
                var empCurrencyIdResult = await _dapperService.ExecuteScalarAsync<int?>(empCurrencySql, new { EmpId = empId, CompanyId = companyId });
                int empCurrencyId = empCurrencyIdResult ?? 0;
                if (tranCurrencyId == 0 && remId != 0 && empCurrencyId != 0)
                {
                    const string tranCurrencySql = "SELECT CurrencyId FROM tblEmpMedical WHERE EmpId = @EmpId AND CompanyId = @CompanyId AND medId = @RemId";
                    var tranCurrencyResult = await _dapperService.ExecuteScalarAsync<int?>(tranCurrencySql, new { EmpId = empId, CompanyId = companyId, RemId = remId });
                    tranCurrencyId = tranCurrencyResult ?? empCurrencyId; 
                }
                else if (tranCurrencyId == 0)
                {
                    tranCurrencyId = empCurrencyId; 
                }

                double empCurrencyRate = 1.0;
                double transCurrencyRate = 1.0;

                empCurrencyRate = _utilities.GetExchangeRate(empCurrencyId); 
                transCurrencyRate =  _utilities.GetExchangeRate(tranCurrencyId); 

                double originalClaimAmountForComparison = claimAmount;
                if (empCurrencyId != 0 && tranCurrencyId != 0 && empCurrencyId != tranCurrencyId)
                {
                    if (empCurrencyRate > 0 && transCurrencyRate > 0)
                    {
                        claimAmount = ((claimAmount * transCurrencyRate) / empCurrencyRate);
                    }
                    else
                    {
                        _logger.LogWarning($"Exchange rate is zero for EmpCurrencyId={empCurrencyId} or TranCurrencyId={tranCurrencyId}. Cannot convert claim amount.");
                        return (false);
                    }
                }
                var whereClause = new StringBuilder("EmpId = @EmpId AND CompanyId = @CompanyId AND FYID = @FiscalYearId");
                var parameters = new DynamicParameters(new { EmpId = empId, CompanyId = companyId, FiscalYearId = fiscalYearId});
                bool amountScope = await GetStatusAmountScope(medicalId, empId, fiscalYearId);
                if (medicalId != 0)
                {
                    whereClause.Append(" AND MedTypeId = @MedicalTypeIdParam");
                    parameters.Add("@MedicalTypeIdParam", medicalId);

                }
                
                if (!amountScope)
                {
                    whereClause.Append(" AND DpdId = @DependentId"); 
                }

                if (remId != 0)
                {
                    whereClause.Append(" AND MedId <> @RemId");
                }

                string availedAmountSql = $@"
        SELECT SUM(
            Convert(float, ISNULL(dbo.fnGetDecryptData(Convert(varchar(10), EmpId) + Convert(varchar(3), CompanyId), Amount), 0.0))
             *
            -- Pass parameters to the SQL function
            dbo.Fn_GetExchangeRateReturnInEmpCurrency(currencyId, dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyIdFuncParam), CompanyId, @EmpCurrencyIdParam)
        )
        -- The AS Amount alias is not needed for ExecuteScalar
        FROM tblEmpMedical
        WHERE {whereClause}";
                parameters.Add("@CompanyIdFuncParam", companyId); 
                parameters.Add("@EmpCurrencyIdParam", empCurrencyId); 

                var availedAmount = await _dapperService.ExecuteScalarAsync<double?>(availedAmountSql, parameters);

                string entitlementSql = "SELECT dbo.Fn_ValidateMedicalAmount(@EmpId, @CompanyId, @FiscalYearId, @MedicalId)";
                var netAmountResult = await _dapperService.ExecuteScalarAsync<double?>(entitlementSql, new { EmpId = empId, CompanyId = companyId, FiscalYearId = fiscalYearId, MedicalId = medicalId });
                double netAmount = (netAmountResult ?? 0) * empCurrencyRate;

                double remainingBalance = netAmount - (availedAmount ?? 0);

                if (remainingBalance < claimAmount)
                {
                flag = false;
            }
                else
                {
                flag = true;
                }

            return flag;
        }
        public async Task<bool> IsDependentAliveAsync(int empId, int dpdId)
        {
            if (dpdId == 0)
            {
                return true;
            }
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            const string sql = @"
                        SELECT status 
                        FROM tblEmpDependentsInfo 
                        WHERE CompanyId = @CompanyId AND EmpId = @EmpId AND Empdpdid = @DpdId;";

            try
            {
                var status = await _dapperService.ExecuteScalarAsync<int?>(sql,
                    new { CompanyId = companyId, EmpId = empId, DpdId = dpdId });
                return status.HasValue && status.Value == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if dependent is alive for DpdId: {DpdId}", dpdId);
                return false;
            }
        }
     
        public double EmpCurrenyRate(int empId, string companyId, double? fallbackRate = 1.0)
        {
            int EmpcurrencyId = 0;

            double empCurrencyRate = 1.0;
            try
            {
                
                object objCur = _utilities.GetScalarData(
                    "CurrencyId",
                    "tblEmpSalarySetup",
                    $"EmpId={empId} And CompanyId={companyId}"
                );

                if (objCur != null && objCur.ToString() != "" && objCur != DBNull.Value)
                {
                    EmpcurrencyId = Convert.ToInt32(objCur);
                }
                int currencyId = Convert.ToInt32(objCur);

                empCurrencyRate = _utilities.GetExchangeRate(EmpcurrencyId);

                return empCurrencyRate;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "EmpCurrenyRate failed for EmpId {EmpId}", empId);
                return fallbackRate ?? 1.0;
            }
        }
        public double GetAmountInEmpCurrency(int empId, string CompanyId, double Amount, double ConversionRate, bool ChangeCurr)

        {
            double empCurrencyRate = 1.0;
            int empCurrencyId = 0;
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            object objcur = _utilities.GetScalarData(
                "CurrencyId",
                "tblEmpSalarySetup",
                "EmpId=" + empId + " And CompanyId=" + companyId);

            if (objcur != null && objcur != DBNull.Value && objcur.ToString() != "")
            {
                empCurrencyId = Convert.ToInt32(objcur);
            }
            if (empCurrencyId != 0)
                empCurrencyRate = _utilities.GetExchangeRate(empCurrencyId);

            if (empCurrencyRate == 0) empCurrencyRate = 1.0;

            if (ChangeCurr)
                return (Amount * ConversionRate) / empCurrencyRate;
            else
                return Amount / empCurrencyRate;
        }
        private string ServerDate(string date)
        {
            
                DateTime DT = Convert.ToDateTime(date, new System.Globalization.CultureInfo("en-GB"));
                string dt = DT.Year.ToString() + "/" + DT.Month.ToString() + "/" + DT.Day.ToString();
                return dt;
            
        }

        public bool CheckCurrencyWithBaseCurrency(int currencyId)
        {
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int baseCurrencyId = Convert.ToInt32(_utilities.GetScalarData("BaseCurrencyId", "tblHRPolicy", "CompanyId = " + companyId));
            if (baseCurrencyId == currencyId)
                return true;
            else
                return false;
        }
        public string getTransactionDate(string transDate)
        {
            string transactionDate = transDate;
            transactionDate = transactionDate.Substring(6, 4) + "-" + transactionDate.Substring(3, 2) + "-" + transactionDate.Substring(0, 2);
            return transactionDate;
        }

        public string FormatDateForSP(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString) || dateString == "__/__/____")
            {
                return null;
            }

            if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                return parsedDate.ToString("dd/MM/yyyy");
            }

            if (DateTime.TryParse(dateString, out parsedDate))
            {
                return parsedDate.ToString("dd/MM/yyyy");
            }
            return null;
        }
        public async Task<APIResponse> InsertEmpMedicalAsync(MedicalTypeSetup medObj)
        {
            var response = new APIResponse { IsValid = false, Message = "" };

            try
            {
                var clientIp = _clientContextService.GetClientIP();
                var objUser = _utilities.GetCurrentUserMap(clientIp);
                var entDate = _utilities.ExecuteSQLFunction(
                    $"dbo.fn_General_GetLocalDateTimeCompanyWise({_utilities.GetCompanyId(_clientContextService.GetClientIP())})"
                )?.ToString();
                var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                var entTerminal = _utilities.GetTerminalId();
                var entTerminalIP = _utilities.GetTerminalIP();
                var userId = objUser.UserID;
                string strUserEmpName = objUser.UserEmpName;
                string strUserEmpCode = objUser.UserEmpCode;
                var empId = Convert.ToInt32(medObj.EMPID);
                int currencyId = Convert.ToInt32(medObj.CURRENCYID);
                decimal conversionRate = 0;
                if (!(CheckCurrencyWithBaseCurrency(currencyId)))
                {
                    conversionRate = await  GetLastConversionRateAsync(currencyId);                  
                }
                else
                    conversionRate = 1;
                var empCurrencyRate = EmpCurrenyRate(empId, companyId);
                var empCurrencyAmount = GetAmountInEmpCurrency(
                   empId, companyId, Convert.ToDouble(medObj.TOTALCLAIM), Convert.ToDouble( conversionRate), true);

                
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                var p = new DynamicParameters();
                p.Add("@MedID", medObj.MEDID);
                p.Add("@EMPID", empId);
                p.Add("@ClaimStatus", "P");
                p.Add("@MEDTYPEID", medObj.MEDTYPEID);
                p.Add("@HOSID", medObj.HOSID);
                p.Add("@CompanyId", Convert.ToInt32(companyId)); 
                p.Add("@WARDNO", string.IsNullOrWhiteSpace(medObj.WARDNO) ? null : medObj.WARDNO);
                p.Add("@DOCTORNAME", string.IsNullOrWhiteSpace(medObj.DOCTORNAME) ? null : medObj.DOCTORNAME);
                p.Add("@ADMREASON", string.IsNullOrWhiteSpace(medObj.ADMREASON) ? null : medObj.ADMREASON);
                p.Add("@ADMMODE", string.IsNullOrWhiteSpace(medObj.ADMMODE) ? null : medObj.ADMMODE);
                p.Add("@PRCDONE", medObj.PRCDONE); 
                p.Add("@ADMDURATION", string.IsNullOrWhiteSpace(medObj.ADMDURATION) ? null : medObj.ADMDURATION);
                p.Add("@MEDICINEPRESCRIBE", string.IsNullOrWhiteSpace(medObj.MEDICINEPRESCRIBE) ? null : medObj.MEDICINEPRESCRIBE);
                p.Add("@VOUCHERNO", string.IsNullOrWhiteSpace(medObj.VOUCHERNO) ? null : medObj.VOUCHERNO.PadRight(10, ' '));
                p.Add("@SUBMEDTYPEID", medObj.SUBMEDTYPEID);
                p.Add("@DischargeDate", FormatDateForSP(medObj.DischargeDate));
                p.Add("@AdmissionDate", FormatDateForSP(medObj.AdmissionDate));
                p.Add("@MDATE", (medObj.MDATE));
                p.Add("@DPDID", string.IsNullOrWhiteSpace(medObj.DPDID) || medObj.DPDID == "0" ? "" : medObj.DPDID);
                p.Add("@TOTALCLAIM", medObj.TOTALCLAIM ?? 0m);
                p.Add("@LESSDISALLOWED", medObj.LESSDISALLOWED ?? 0m);
                p.Add("@AMOUNT", medObj.AMOUNT ?? 0m);
                p.Add("@CURRENCYID", medObj.CURRENCYID);
                p.Add("@CONVERSIONRATE", conversionRate > 0 ? conversionRate : (decimal?)null);
                p.Add("@PAID", medObj.PAID);
                p.Add("@ChequeNumber", string.IsNullOrWhiteSpace(medObj.ChequeNumber) ? null : medObj.ChequeNumber);
                p.Add("@ChequeDate", string.IsNullOrWhiteSpace(medObj.ChequeDate) || medObj.ChequeDate == "__/__/____" ? null : medObj.ChequeDate);
                p.Add("@BnkbrnID", medObj.bnkbrnid);
                p.Add("@bnkId", medObj.bnkid);
                p.Add("@COMMENTS", string.IsNullOrWhiteSpace(medObj.COMMENTS) ? null : medObj.COMMENTS);
                p.Add("@DisallowedReason", string.IsNullOrWhiteSpace(medObj.DisallowedReason) ? null : medObj.DisallowedReason);
                if (medObj.DocumentBody != null && medObj.DocumentBody.Length > 0)
                {
                    p.Add("@DocumentBody", medObj.DocumentBody, DbType.Binary);
                }
                else
                {
                    p.Add("@DocumentBody", null, DbType.Binary);
                }
                p.Add("@Extention", string.IsNullOrWhiteSpace(medObj.Extention) ? null : medObj.Extention);
                p.Add("@FiscalYearID", medObj.FiscalYearID);

                p.Add("@UserId", objUser?.UserID);
                p.Add("@FormId", Constants.EmployeeMedicalReimbursement.ToString());
                p.Add("@UserEmpId", objUser?.UserEmpId);
                p.Add("@UserEmpName", objUser?.UserEmpName);
                p.Add("@UserEmpCode", objUser?.UserEmpCode);
                p.Add("@EntTerminal", string.IsNullOrWhiteSpace(entTerminal) ? null : entTerminal);
                p.Add("@EntTerminalIP", string.IsNullOrWhiteSpace(entTerminalIP) ? null : entTerminalIP);
                p.Add("@ApplicationID", _utilities.GetApplicationId());
                p.Add("@EmpCurrencyRate", empCurrencyRate > 0 ? empCurrencyRate : (double?)null);
                p.Add("@EmpCurrencyAmount", empCurrencyAmount);

                p.Add("@Result", dbType: DbType.String, size: 1000, direction: ParameterDirection.Output);

                await conn.ExecuteAsync(
                                    "sp_InsertUpdateEmpMedical",p,commandType: CommandType.StoredProcedure).ConfigureAwait(false);

                var resultMessage = p.Get<string>("@Result") ?? string.Empty;

                if (resultMessage.Contains("Successfully"))
                {
                    response.IsValid = true;
                }
                response.Message = resultMessage;

                return response;


            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error in InsertEmpMedicalAsync.");
                response.IsValid = false;
                response.Message = "An unexpected error occurred while saving the record."; // A user-friendly message
                return response;
            }
        }
        public async Task<string?> BuildInsertEmailBodyAsync(BuildEmailContext ctx)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var p = new DynamicParameters();
            p.Add("@Param_LoginCulture", ctx.Culture);
            p.Add("@Param_LoginCompanyId", ctx.CompanyId);
            p.Add("@Param_UserId", ctx.UserId);
            p.Add("@Param_UserEmpId", ctx.UserEmpId);
            p.Add("@Param_FormId", ctx.FormId);
            p.Add("@Param_EntTerminal", ctx.EntTerminal);
            p.Add("@Param_EntTerminalIP", ctx.EntTerminalIP);
            p.Add("@Param_EntOperation", ctx.EntOperation);
            p.Add("@Param_LoginEmpId", ctx.EmpId);
            p.Add("@Param_MedicalCategoryId", ctx.MedTypeId);
            p.Add("@Param_ClaimAmount", ctx.ClaimAmount);

            var body = await conn.ExecuteScalarAsync<string>(
                "dbo.SP_medicalReimbursement_EmailBodyforRequest",
                p, commandType: CommandType.StoredProcedure);

            return body;
        }
    }


    public class FiscalYearModel
    {
        public int? ID { get; set; }
        public string? Name { get; set; }
    }
    public class EmpDependentsInfoSetup
    {
        public int? EMPDPDID { get; set; }
        public string? NAME { get; set; }

    }
    public class MedicalInformationRow
    {
        public int? MId { get; set; }
        public bool? Prorate { get; set; }
        public int? MtypeId { get; set; }
        public string? MedicalEntitlementScope { get; set; }
        public bool? MedicalAmountScope { get; set; }
        public bool? ApplyChildrenAgeScope { get; set; }
        public string? MedicalName { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Pendingforapproval { get; set; }
        public decimal? prorateAmount { get; set; }

        public decimal? AvailableMonthlyLimit { get; set; }
    }
    public class MedicalTypeSetup
    {
        public int? MEDID { get; set; }
        public string? CurrencyCode { get; set; }
        public string? MedicalName { get; set; }
        public int? FiscalYearID { get; set; }
        public string? SubMedicalName { get; set; }
        public int? SUBMEDTYPEID { get; set; }
        public int? EMPID { get; set; }
        public int? MEDTYPEID { get; set; }
        public int? HOSID { get; set; }
        public string? Hospitalname { get; set; }
        public string? WARDNO { get; set; }
        public string? DOCTORNAME { get; set; }
        public string? ADMREASON { get; set; }
        public string? ADMMODE { get; set; }
        public bool? PRCDONE { get; set; }
        public string? ADMDURATION { get; set; }
        public string? MEDICINEPRESCRIBE { get; set; }
        public string? VOUCHERNO { get; set; }
        public byte[]? DocumentBody { get; set; }
        public bool HasAttachment { get; set; }
        public string? Extention { get; set; }
        public string? DPDID { get; set; }
        public string? DpdName { get; set; }
        public string? MDATE { get; set; }
        public string? AdmissionDate { get; set; }
        public string? DischargeDate { get; set; }
        public decimal? TOTALCLAIM { get; set; }
        public decimal? LESSDISALLOWED { get; set; }
        public decimal? AMOUNT { get; set; }
        public int? CURRENCYID { get; set; }
        public int? CurrencyRate { get; set; }
        public bool? PAID { get; set; }
        public string? COMMENTS { get; set; }
        public string? DpdRel { get; set; }
        public string? ChequeNumber { get; set; }
        public string? ChequeDate { get; set; }
        public int? bnkid { get; set; }
        public string? BankName { get; set; }
        public int? bnkbrnid { get; set; }
        public string? BranchName { get; set; }
        public decimal? CONVERSIONRATE { get; set; }
        public string? Status { get; set; }
        public string? DisallowedReason { get; set; }
        public string? DateOfEntry { get; set; }
        public string? CurrencyName { get; set; }
    }
    public class NetAmountRow
    {
        public decimal? Amount { get; set; }
        public string? AmountText { get; set; }
    }
    public class EmpMedicalType
    {
        public double? Amount { get; set; }
        public string? EmpId { get; set; }
        public int? CompanyId { get; set; }
        public int? ID { get; set; }
        public int? MEDTYPEID { get; set; }
        public string? NAME { get; set; }
    }
    public class EmpMedicalDocument
    {
        public byte[]? DocumentBody { get; set; }
        public string? Extention { get; set; } // e.g. ".pdf" or "pdf"
    }
    public class HospitalSetup
    {
        public int? HOSID { get; set; }
        public string? NAME { get; set; }
    }
    public class FillComboSetupsDetail
    {
        public int? SdlID { get; set; }
        public string? Name { get; set; }
    }
    public class Currency
    {
        public int SDLID { get; set; }
        public string Code { get; set; }
        public string CURRENCY { get; set; }
    }
    public class MedicalDocument
    {
        public byte[]? DocumentBody { get; set; }
        public string? Extention { get; set; }
    }
    public class APIResponse
    {
        public bool? IsValid { get; set; }
        public string? Message { get; set; }
    }
    public class InsertMedicalPayload
    {
        public int? MEDID { get; set; }
        public string? CurrencyCode { get; set; }
        public string? MedicalName { get; set; }
        public int? FiscalYearID { get; set; }
        public string? SubMedicalName { get; set; }
        public int? SUBMEDTYPEID { get; set; }
        public int? EMPID { get; set; }
        public int? MEDTYPEID { get; set; }
        public int? HOSID { get; set; }
        public string? Hospitalname { get; set; }
        public string? WARDNO { get; set; }
        public string? DOCTORNAME { get; set; }
        public string? ADMREASON { get; set; }
        public string? ADMMODE { get; set; }
        public bool? PRCDONE { get; set; }
        public string? ADMDURATION { get; set; }
        public string? MEDICINEPRESCRIBE { get; set; }
        public string? VOUCHERNO { get; set; }
        public byte[]? DocumentBody { get; set; }
        public bool HasAttachment { get; set; }
        public string? Extention { get; set; }
        public string? DPDID { get; set; }
        public string? DpdName { get; set; }
        public string? MDATE { get; set; }
        public string? AdmissionDate { get; set; }
        public string? DischargeDate { get; set; }
        public decimal? TOTALCLAIM { get; set; }
        public decimal? LESSDISALLOWED { get; set; }
        public decimal? AMOUNT { get; set; }
        public int? CURRENCYID { get; set; }
        public int? CurrencyRate { get; set; }
        public bool? PAID { get; set; }
        public string? COMMENTS { get; set; }
        public string? DpdRel { get; set; }
        public string? ChequeNumber { get; set; }
        public string? ChequeDate { get; set; }
        public int? bnkid { get; set; }
        public string? BankName { get; set; }
        public int? bnkbrnid { get; set; }
        public string? BranchName { get; set; }
        public decimal? CONVERSIONRATE { get; set; }
        public string? Status { get; set; }
        public string? DisallowedReason { get; set; }
        public string? DateOfEntry { get; set; }
        public string? CurrencyName { get; set; }
    }
    public sealed class BuildEmailContext
    {
        public string Culture { get; set; } = "";
        public string CompanyId { get; set; } = "";
        public string UserId { get; set; } = "";
        public int UserEmpId { get; set; }
        public string FormId { get; set; } = "";
        public string EntTerminal { get; set; } = "";
        public string EntTerminalIP { get; set; } = "";
        public string EntOperation { get; set; } = "Insert";
        public int EmpId { get; set; }
        public int MedTypeId { get; set; }
        public decimal ClaimAmount { get; set; }
    }
    public sealed class InsertMedicalEmailBodyRequest
    {
        public int EmpId { get; set; }
        public int MedTypeId { get; set; }
        public decimal ClaimAmount { get; set; }
    }
    //public class UserInfo
    //{
    //    private int? _userEmpId { get; set; }
    //    private string? _userEmpCode { get; set; }
    //    private string? _userEmpName { get; set; }
    //    private string? _userId { get; set; }
    //}
}