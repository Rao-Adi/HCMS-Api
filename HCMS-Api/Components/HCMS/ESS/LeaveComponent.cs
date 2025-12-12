using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using Org.BouncyCastle.Ocsp;
using System.Data;
using System.Globalization;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class LeaveComponent
    {
        //private static readonly IConfiguration _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        //static DataServices dataservice = new DataServices(_configuration);
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;

        public LeaveComponent(IConfiguration configuration, Utilities utilities
            , ClientContextService clientContextService, DataServices dataservice)
        {            
            _configuration = configuration;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        public string lvType(string _strSelectedEmpId)
        {
            string lvType = string.Empty;
            DataSet dsData = GetLeaveAvailType(_strSelectedEmpId);
            if (dsData != null && dsData.Tables[0] != null && dsData.Tables[0].Rows.Count > 0)
            {
                lvType = Convert.ToString(dsData.Tables[0].Rows[0][0]);
            }
            return lvType;
        }

        public string lvType(string _strSelectedEmpId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            string lvType = string.Empty;
            DataSet dsData = GetLeaveInfoForSelectedEmployee(_strSelectedEmpId, strLoginEmpId, strLoginCompanyId, _culture);
            if (dsData != null && dsData.Tables[1] != null && dsData.Tables[1].Rows.Count > 0)
            {
                lvType = Convert.ToString(dsData.Tables[1].Rows[0][0]);
            }
            return lvType;
        }
        public DataSet GetLeaveAvailType(string EmpId)
        {
            if (EmpId == null)
                return null;

            DataSet dsReturn = new DataSet();

            try
            {
                string q = "Select dbo.fn_Leave_GetEmployeeLeaveAvailType('" + EmpId + "', NULL)";

                string errorMessage = _dataservice.GetDataSet(q, ref dsReturn);
            }
            catch (Exception ex) { }
            return dsReturn;
        }
        public string GetAnnualLeaveCode(string companyID)
        {
            DataSet ds = new DataSet();
            string query = "Select Top 1 EL from tblVariable where Companyid=" + companyID;
            string EL = "";

            string strMessage = _dataservice.ExecuteReader(query, ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                EL = Convert.ToString(ds.Tables[0].Rows[0]["EL"]).Trim();

            return EL;
        }

        public bool isAvailLeavesFromNextLeaveYearAllowed(string _EmpId)
        {
            DataSet ds = new DataSet();
            bool blnReturnVal = false;

            string query = " Select isAvailLeavesFromNextLeaveYearAllowed from fn_Leave_GetLeaveBalanceData('" + _EmpId + "', '1') where isAvailLeavesFromNextLeaveYearAllowed = 1";
            string errorMessage = _dataservice.ExecuteReader(query, ref ds);

            if (errorMessage.Equals("successfull") && ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                blnReturnVal = Convert.ToBoolean(ds.Tables[0].Rows[0][0]);

            return blnReturnVal;
        }

        public bool isAnyLeaveTypeCarryForward(string _EmpId)
        {
            DataSet ds = new DataSet();
            bool blnReturnVal = false;

            string query = " Select CarryForward from fn_Leave_GetLeaveBalanceData('" + _EmpId + "', '1') where CarryForward = 1";
            string errorMessage = _dataservice.ExecuteReader(query, ref ds);

            if (errorMessage.Equals("successfull") && ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                blnReturnVal = Convert.ToBoolean(ds.Tables[0].Rows[0][0]);

            return blnReturnVal;
        }
        public int MinumumLFADays(string CompanyID)
        {
            int LFADays = 0;
            DataSet ds = new DataSet();

            string strMessage = _dataservice.ExecuteReader(" Select LFADays = dbo.fn_Leave_MinimumLFADays(" + CompanyID + ") ", ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                LFADays = Convert.ToInt32(ds.Tables[0].Rows[0]["LFADays"]);

            return LFADays;
        }
        public bool blnGradeWiseLeaveAvailTypeDefined(string _strSelectedEmpId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            bool blnGradeWiseLeaveAvailTypeDefined = false;
            DataSet dsData = GetLeaveInfoForSelectedEmployee(_strSelectedEmpId, strLoginEmpId, strLoginCompanyId, _culture);
            if (dsData != null && dsData.Tables[0] != null && dsData.Tables[0].Rows.Count > 0)
            {
                blnGradeWiseLeaveAvailTypeDefined = Convert.ToBoolean(dsData.Tables[0].Rows[0][0]);
            }
            return blnGradeWiseLeaveAvailTypeDefined;
        }        

        public List<fn_Leave_GetAuthorizedLeaveTypes_Result> LoadEmpLeaveData(string _strSelectedEmpId)
        {
            List<fn_Leave_GetAuthorizedLeaveTypes_Result> LeaveTypes = new List<fn_Leave_GetAuthorizedLeaveTypes_Result>();
            try
            {

                DataSet dsData = GetLeaveTypeDropDown(_strSelectedEmpId);
                DataTable dtLeaveTypes = new DataTable();
                if (dsData != null && dsData.Tables[0] != null)
                {
                    dtLeaveTypes = dsData.Tables[0];
                    for (int i = 0; i < dtLeaveTypes.Rows.Count; i++)
                    {
                        LeaveTypes.Add(new fn_Leave_GetAuthorizedLeaveTypes_Result
                        {

                            LeaveType = dtLeaveTypes.Rows[i]["LeaveType"].ToString(),
                            LCode = dtLeaveTypes.Rows[i]["LCode"].ToString(),
                            Description = dtLeaveTypes.Rows[i]["Description"].ToString()

                        });
                    }
                }

            }
            catch (Exception ex) { }

            return LeaveTypes;

        }

        public DataSet GetLeaveTypeDropDown(string EmpId)
        {
            if (EmpId == null)
                return null;

            DataSet dsReturn = new DataSet();

            string q = "Select * from " +
                        " ( " +
                        " 	Select	0 as SNo, 'N/A' LeaveType,	'000' LCode,'N/A' [Description]   " +
                        " 	Union " +
                        " 	Select	ROW_NUMBER() over(order by Description) as SNo, LeaveType, LCode,Description  " +
                        " 	from	dbo.fn_Leave_GetAuthorizedLeaveTypes('" + EmpId + "',1) " +
                        " ) as a; ";


            string errorMessage = _dataservice.GetDataSet(q, ref dsReturn);
            return dsReturn;
        }

        public ValidateUserInput CountLeaveDaysWeb(string leavetype, string datefrom, string dateto, string empid, bool isLFAEntitled, string days, string description, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            
            ValidateUserInput vmsg = new ValidateUserInput();
            string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string ColName = "";
            if (culture == "en-GB")
                ColName = "MessageEng";
            else
                ColName = "MessageArb";

            try
            {
                if (datefrom != null && datefrom.Length > 0 && dateto != null && dateto.Length > 0)
                {

                    DateTime dateFrom = new DateTime(), dateTo = new DateTime();

                    if (!DateTime.TryParse(datefrom, out dateFrom))
                    {
                        vmsg.FDHDDays = "";
                        vmsg.DDHMDays = "";
                        return vmsg;
                    }

                    if (!DateTime.TryParse(dateto, out dateTo))
                    {
                        vmsg.FDHDDays = "";
                        vmsg.DDHMDays = "";
                        return vmsg;
                    }

                    int checkDt = DateTime.Compare(dateFrom, dateTo);

                    if (checkDt > 0)
                    {
                        vmsg.FDHDDays = "";
                        vmsg.DDHMDays = "";
                        return vmsg;
                    }

                    datefrom = datefrom.Replace("-", "/");
                    dateto = dateto.Replace("-", "/");
                    //DateTime dateFrom2 = DateTime.Parse(datefrom);
                    //DateTime dateTo2 = DateTime.Parse(dateto);
                    DateTime dateFrom2 = DateTime.ParseExact(datefrom, "yyyy/MM/dd", new CultureInfo("en-US"));
                    DateTime dateTo2 = DateTime.ParseExact(dateto, "yyyy/MM/dd", new CultureInfo("en-US"));

                    TimeSpan ts = dateTo2.Subtract(dateFrom2);

                    double dtDiff = ts.Days + 1;

                    double dblExcludingDays = 0.0;
                    string strFromDate = dateFrom2.Year.ToString() + "/" + (dateFrom2.Month < 10 ? "0" + dateFrom2.Month.ToString() : dateFrom2.Month.ToString()) + "/" + (dateFrom2.Day < 10 ? "0" + dateFrom2.Day.ToString() : dateFrom2.Day.ToString());
                    string strToDate = dateTo2.Year.ToString() + "/" + (dateTo2.Month < 10 ? "0" + dateTo2.Month.ToString() : dateTo2.Month.ToString()) + "/" + (dateTo2.Day < 10 ? "0" + dateTo2.Day.ToString() : dateTo2.Day.ToString());

                    try
                    {
                        dblExcludingDays = Convert.ToDouble(_utilities.GetScalarData("top 1 dbo.fn_GetExcludingLeaveDays (" + _utilities.GetEmployeeCompanyId(empid) + ", '" + leavetype + "',  " + empid + ", '" + strFromDate + "', '" + strToDate + "', 1)", "tblemployee", "1=1"));
                        dtDiff = dtDiff - dblExcludingDays;
                    }
                    catch (Exception excep)
                    { }

                    object objlvType = _utilities.ExecuteSQLFunction("dbo.fn_Leave_GetEmployeeLeaveAvailType(" + empid + ",NULL)");

                    if (objlvType.ToString() == "FDHD")
                    {
                        vmsg.FDHDDays = Convert.ToString(dtDiff);

                        if (days != "" && days == "0.5" && vmsg.FDHDDays == "1")
                        {
                            vmsg.FDHDDays = days;
                        }

                        vmsg.LeaveDays = "FDHD";
                    }

                    else
                    {
                        vmsg.DDHMDays = Convert.ToString(dtDiff).PadLeft(5, '0') + ":0:00";
                        vmsg.LeaveDays = "DDHM";
                    }

                    if (isLFAEntitled == true)
                    {                        
                        string companyid = _utilities.GetEmployeeCompanyId(empid);
                        double LvDays = dtDiff - dblExcludingDays;
                        string EL = GetAnnualLeaveCode(companyid);
                        int minLFADays = MinumumLFADays(companyid);
                        if (EL.Trim() == leavetype.Trim() && LvDays >= minLFADays)
                        {
                            vmsg.isLFAAllowed = true;
                        }
                        else
                        {
                            vmsg.isLFAAllowed = false;
                        }
                    }
                    else
                    {
                        vmsg.isLFAAllowed = false;
                    }

                    string strSandwichIncurring = Convert.ToString(_utilities.GetScalarData("top 1 dbo.fn_GetExcludingLeaveDays (" + _utilities.GetEmployeeCompanyId(empid) + ", '" + leavetype + "',  " + empid + ", '" + strFromDate + "', '" + strToDate + "', 3)", "tblemployee", "1=1"));
                    if (strSandwichIncurring.Trim().ToUpper().Equals("YES"))
                    {
                        DataSet ds = new DataSet();
                        string msg = _dataservice.GetDataWithClause(ColName, "tblValidationMessages", "FormId = '" + Constants.LeaveRequest + "' and ApplicationCode = 'HRIS' and ValidationCode = '0056'", ref ds);
                        string valmsg = ds.Tables[0].Rows[0][0].ToString();
                        if (valmsg != "")
                            valmsg = valmsg.Replace("<br />", " ");
                        vmsg.sandwitch = valmsg;
                        return vmsg;
                    }

                }
                return vmsg;
            }

            catch { return vmsg; }

        }

        public DataSet GetLeaveWorkflowTrack(string EmpId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            if (EmpId == null)
                return null;

            DataSet dsReturn = new DataSet();

            string q = "Declare @Param_EmpId int = " + EmpId + ";" +
                        " Exec SP_GetWorkFlowTrack @Param_EmpId, '001', Null, Null, " + strLoginEmpId + ", '" + _culture + "', " + strLoginCompanyId + "; ";


            string errorMessage = _dataservice.GetDataSet(q, ref dsReturn);
            return dsReturn;
        }

        public bool blnGradeWiseLeaveAvailTypeDefined(string _strSelectedEmpId)
        {
            bool blnGradeWiseLeaveAvailTypeDefined = false;
            try
            {
                DataSet dsData = new DataSet();
                string query = "Select dbo.fn_Leave_isEmpLeaveAvailTypeDefined('" + _strSelectedEmpId + "')";

                string msg = _dataservice.ExecuteReader(query, ref dsData);

                if (dsData != null && dsData.Tables[0] != null && dsData.Tables[0].Rows.Count > 0)
                {
                    blnGradeWiseLeaveAvailTypeDefined = Convert.ToBoolean(dsData.Tables[0].Rows[0][0]);
                }
            }
            catch (Exception ex) { }

            return blnGradeWiseLeaveAvailTypeDefined;
        }

        public ValidateUserInput CountLeaveDays(string leavetype, string datefrom, string dateto, string empid, bool isLFAEntitled, string days, string description, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {

            if (string.IsNullOrEmpty(leavetype))
            {                
                var LCODE = LoadEmployeeData(empid.ToString(), strLoginEmpId, strLoginCompanyId, _culture);

                if (LCODE.Count > 0)
                {
                    //  lstLeave.Add("N/A");
                    foreach (var a in LCODE)
                    {
                        //lstLeave.Add(a.Description);

                        if (a.Description.ToUpper() == description.ToUpper())
                        {
                            leavetype = a.LCode.Trim();
                        }
                    }
                }

            }
            ValidateUserInput vmsg = new ValidateUserInput();
            string culture = _culture;
            string ColName = "";
            if (culture == "en-GB")
                ColName = "MessageEng";
            else
                ColName = "MessageArb";

            try
            {
                if (datefrom != null && datefrom.Length > 0 && dateto != null && dateto.Length > 0)
                {

                    DateTime dateFrom = new DateTime(), dateTo = new DateTime();

                    if (!DateTime.TryParse(datefrom, out dateFrom))
                    {
                        vmsg.FDHDDays = "";
                        vmsg.DDHMDays = "";
                        return vmsg;
                    }

                    if (!DateTime.TryParse(dateto, out dateTo))
                    {
                        vmsg.FDHDDays = "";
                        vmsg.DDHMDays = "";
                        return vmsg;
                    }

                    int checkDt = DateTime.Compare(dateFrom, dateTo);

                    if (checkDt > 0)
                    {
                        vmsg.FDHDDays = "";
                        vmsg.DDHMDays = "";
                        return vmsg;
                    }



                    datefrom = datefrom.Replace("-", "/");
                    dateto = dateto.Replace("-", "/");
                    //DateTime dateFrom2 = DateTime.Parse(datefrom);
                    //DateTime dateTo2 = DateTime.Parse(dateto);
                    DateTime dateFrom2 = DateTime.ParseExact(datefrom, "dd/MM/yyyy", new CultureInfo("en-US"));
                    DateTime dateTo2 = DateTime.ParseExact(dateto, "dd/MM/yyyy", new CultureInfo("en-US"));

                    TimeSpan ts = dateTo2.Subtract(dateFrom2);

                    double dtDiff = ts.Days + 1;

                    double dblExcludingDays = 0.0;
                    string strFromDate = dateFrom2.Year.ToString() + "/" + (dateFrom2.Month < 10 ? "0" + dateFrom2.Month.ToString() : dateFrom2.Month.ToString()) + "/" + (dateFrom2.Day < 10 ? "0" + dateFrom2.Day.ToString() : dateFrom2.Day.ToString());
                    string strToDate = dateTo2.Year.ToString() + "/" + (dateTo2.Month < 10 ? "0" + dateTo2.Month.ToString() : dateTo2.Month.ToString()) + "/" + (dateTo2.Day < 10 ? "0" + dateTo2.Day.ToString() : dateTo2.Day.ToString());

                    try
                    {
                        dblExcludingDays = Convert.ToDouble(_utilities.GetScalarData("top 1 dbo.fn_GetExcludingLeaveDays (" + _utilities.GetEmployeeCompanyId(empid) + ", '" + leavetype + "',  " + empid + ", '" + strFromDate + "', '" + strToDate + "', 1)", "tblemployee", "1=1"));
                        dtDiff = dtDiff - dblExcludingDays;
                    }
                    catch (Exception excep)
                    { }

                    object objlvType = _utilities.ExecuteSQLFunction("dbo.fn_Leave_GetEmployeeLeaveAvailType(" + empid + ",NULL)");

                    if (objlvType.ToString() == "FDHD")
                    {
                        vmsg.FDHDDays = Convert.ToString(dtDiff);

                        if (days != "" && days == "0.5" && vmsg.FDHDDays == "1")
                        {
                            vmsg.FDHDDays = days;
                        }

                        vmsg.LeaveDays = "FDHD";
                    }

                    else
                    {
                        vmsg.DDHMDays = Convert.ToString(dtDiff).PadLeft(5, '0') + ":0:00";
                        vmsg.LeaveDays = "DDHM";
                    }

                    if (isLFAEntitled == true)
                    {                        
                        string companyid = _utilities.GetEmployeeCompanyId(empid);
                        double LvDays = dtDiff - dblExcludingDays;
                        string EL = GetAnnualLeaveCode(companyid);
                        int minLFADays = MinumumLFADays(companyid);
                        if (EL.Trim() == leavetype.Trim() && LvDays >= minLFADays)
                        {
                            vmsg.isLFAAllowed = true;
                        }
                        else
                        {
                            vmsg.isLFAAllowed = false;
                        }
                    }
                    else
                    {
                        vmsg.isLFAAllowed = false;
                    }

                    string strSandwichIncurring = Convert.ToString(_utilities.GetScalarData("top 1 dbo.fn_GetExcludingLeaveDays (" + _utilities.GetEmployeeCompanyId(empid) + ", '" + leavetype + "',  " + empid + ", '" + strFromDate + "', '" + strToDate + "', 3)", "tblemployee", "1=1"));
                    if (strSandwichIncurring.Trim().ToUpper().Equals("YES"))
                    {
                        DataSet ds = new DataSet();
                        string msg = _dataservice.GetDataWithClause(ColName, "tblValidationMessages", "FormId = '" + Constants.LeaveRequest + "' and ApplicationCode = 'HRIS' and ValidationCode = '0056'", ref ds);
                        string valmsg = ds.Tables[0].Rows[0][0].ToString();
                        if (valmsg != "")
                            valmsg = valmsg.Replace("<br />", " ");
                        vmsg.sandwitch = valmsg;
                        return vmsg;
                    }

                }
                return vmsg;
            }

            catch { return vmsg; }

        }

        public List<fn_Leave_GetAuthorizedLeaveTypes_Result> LoadEmployeeData(string _strSelectedEmpId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            List<fn_Leave_GetAuthorizedLeaveTypes_Result> LeaveTypes = new List<fn_Leave_GetAuthorizedLeaveTypes_Result>();
            try
            {

                DataSet dsData = GetLeaveInfoForSelectedEmployee(_strSelectedEmpId, strLoginEmpId, strLoginCompanyId, _culture);
                DataTable dtLeaveTypes = new DataTable();
                if (dsData != null && dsData.Tables[5] != null)
                {
                    dtLeaveTypes = dsData.Tables[5];
                    for (int i = 0; i < dtLeaveTypes.Rows.Count; i++)
                    {
                        LeaveTypes.Add(new fn_Leave_GetAuthorizedLeaveTypes_Result
                        {

                            LeaveType = dtLeaveTypes.Rows[i]["LeaveType"].ToString(),
                            LCode = dtLeaveTypes.Rows[i]["LCode"].ToString(),
                            Description = dtLeaveTypes.Rows[i]["Description"].ToString()

                        });
                    }
                }

            }
            catch (Exception ex) { }

            return LeaveTypes;

        }

        public List<WorkFlowTrack_Result> BindWorkflowAuthoritiesGrid(string _strSelectedEmpId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            List<WorkFlowTrack_Result> wfAuth = new List<WorkFlowTrack_Result>();
            try
            {
                int count = 0;
                DataSet dsData = GetLeaveInfoForSelectedEmployee(_strSelectedEmpId, strLoginEmpId, strLoginCompanyId, _culture);
                DataTable DT = new DataTable();
                var WFTrackMessage = string.Empty;
                if (dsData != null && dsData.Tables[4] != null && dsData.Tables[4].Rows.Count > 0)
                {
                    WFTrackMessage = Convert.ToString(dsData.Tables[4].Rows[0][0]);
                }
                if (dsData != null && dsData.Tables[3] != null)
                {
                    DT = dsData.Tables[3];
                    for (int i = 0; i < DT.Rows.Count; i++)
                    {
                        count++;
                        wfAuth.Add(new WorkFlowTrack_Result
                        {

                            EmpId = Convert.ToInt32(DT.Rows[i]["EmpId"].ToString()),
                            EmpCode = DT.Rows[i]["EmpCode"].ToString(),
                            EmpName = count.ToString() + '.' + " " + DT.Rows[i]["EmpName"].ToString(),
                            Designation = DT.Rows[i]["Designation"].ToString(),
                            Department = DT.Rows[i]["Department"].ToString(),
                            SubDepartment = DT.Rows[i]["SubDepartment"].ToString(),
                            ApproverLevel = DT.Rows[i]["ApproverLevel"].ToString(),
                            isDefaultApprover = Convert.ToBoolean(DT.Rows[i]["isDefaultApprover"].ToString()),
                            ApproverStatus = Convert.ToBoolean(DT.Rows[i]["ApproverStatus"].ToString()),
                            isTemporaryApprover = Convert.ToBoolean(DT.Rows[i]["isTemporaryApprover"].ToString()),
                            ActualApproverEmpId = Convert.ToInt32(DT.Rows[i]["ActualApproverEmpId"].ToString()),
                            Remarks = DT.Rows[i]["Remarks"].ToString(),

                        });
                    }
                }

            }
            catch (Exception ex) { }

            return wfAuth;
        }

        public DataSet GetLeaveInfoForSelectedEmployee(string EmpId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            if (EmpId == null)
                return null;

            DataSet dsReturn = new DataSet();
      
            if (strLoginEmpId.Trim().Equals("") || strLoginEmpId.Trim().Equals("0"))
                strLoginEmpId = "-1";

            string q = "Declare @Param_EmpId int = " + EmpId + ";" +
                        " Select dbo.fn_Leave_isEmpLeaveAvailTypeDefined(@Param_EmpId); " +
                        " Select dbo.fn_Leave_GetEmployeeLeaveAvailType(@Param_EmpId,NULL); " +
                        " Select EmpCode from TblEmployee where EmpId = @Param_EmpId; " +
                        " Exec SP_GetWorkFlowTrack @Param_EmpId, '001', Null, Null, " + strLoginEmpId + ", '" + _culture + "', " + strLoginCompanyId + "; " +
                        "Select * from " +
                        " ( " +
                        " 	Select	0 as SNo, 'N/A' LeaveType,	'000' LCode,'N/A' [Description]   " +
                        " 	Union " +
                        " 	Select	ROW_NUMBER() over(order by Description) as SNo, LeaveType, LCode,Description  " +
                        " 	from	dbo.fn_Leave_GetAuthorizedLeaveTypes(@Param_EmpId,1) " +
                        " ) as a; ";


            string errorMessage = _dataservice.GetDataSet(q, ref dsReturn);
            return dsReturn;
        }

        public int NoEntriesForLeaveType(string EmpId, string LeaveCode, string EmpleaveId)
        {
            
            int count = 0;
            DataSet dataset = new DataSet();
            string whereClause = "EmpId = " + EmpId + " And LCode='" + LeaveCode + "'" + " And CompanyId=" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + " And Leavestatus=1 And leaveDays>0";

            if (EmpleaveId != "")
                whereClause = whereClause + " And   EmpLeaveId<>" + EmpleaveId;

            string errorMessage = _dataservice.GetDataWithClause(" Count (*) as TotalRecords ", "tblEmpLeave", whereClause, ref dataset);

            if (dataset != null && dataset.Tables.Count > 0)
            {
                if (dataset.Tables[0].Rows.Count > 0)
                {
                    DataRow dr = dataset.Tables[0].Rows[0];

                    if (!dr.IsNull("TotalRecords"))
                    {
                        count = Convert.ToInt32(dr["TotalRecords"].ToString());
                    }
                }
            }
            object obj1 = _utilities.ExecuteSQLFunction("OpeningAvailedTime from tblEmpspecialleave where EmpId=" + EmpId + "And LeaveCode='" + LeaveCode.ToString().Trim() + "' and CompanyId=" + _utilities.GetCompanyId(_clientContextService.GetClientIP()));
            int balance = 0;
            if (obj1 != null)
                balance = Convert.ToInt32(obj1);
            count = count + balance;
            return count;
        }

        public bool CheckIsPenaltyApplicable(string EmpId)
        {
            DataSet ds = new DataSet();
            bool returnVal = false;

            try
            {
                string qry = "Select dbo.fn_Leave_IsPenaltyApplicable('" + EmpId + "')";
                string errormessage = _dataservice.ExecuteReader(qry, ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                    {
                        returnVal = Convert.ToBoolean(ds.Tables[0].Rows[0][0].ToString());
                    }
                }
            }

            catch { returnVal = false; };

            return returnVal;
        }

        public decimal GetSalaryDeductionDays(string EmpID)
        {
            decimal Days = 0;
            DataSet ds = new DataSet();

            string query = "Select SalaryDeductionDays = ISNULL(dbo.fn_Leave_SalaryDudectionDaysDueToPenalty( " + EmpID + "), 0)";
            string strMessage = _dataservice.ExecuteReader(query, ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                Days = Convert.ToDecimal(ds.Tables[0].Rows[0]["SalaryDeductionDays"]);

            return Days;
        }
        public DateTime GetCurrentPayrollMonthStartDate(string CompanyID, out bool IsPmonthExists)
        {
            string sql = "select Top 1 DateFrom from tblPayrollMonth where CompanyId= " + CompanyID + " and Closed=0";

            DateTime dtResult = new DateTime();

            DataSet ds = new DataSet();
            string strMessage = _dataservice.ExecuteReader(sql, ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                dtResult = Convert.ToDateTime(ds.Tables[0].Rows[0]["DateFrom"]);
                IsPmonthExists = true;
            }
            else
            {
                DateTime currDare = _utilities.GetSysCurrentdate();
                dtResult = new DateTime(currDare.Year, currDare.Month, 1);
                IsPmonthExists = false;
            }


            return dtResult;
        }
        public DataTable GetEmployeeAbsentDays(string EmpId, DateTime DateFrom, DateTime DateTo)
        {
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();

            string sql = " dbo.SP_Leave_EmployeeAbsentDays " +
                         " @Param_EmpID = " + EmpId + ", " +
                         " @Param_AttDateFrom = '" + _utilities.ReturnDBDate(DateFrom) + "', " +
                         " @Param_AttDateTo = '" + _utilities.ReturnDBDate(DateTo) + "'";
            try
            {
                string result = _dataservice.ExecuteReader(sql, ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        dt = ds.Tables[0];
                    return dt;
                }
            }
            catch { return dt; }

            return dt;

        }
        public bool IsEmployeeLFAEntitled(string EmpID)
        {
            bool retVal = false;
            DataSet ds = new DataSet();

            string strMessage = _dataservice.ExecuteReader(" Select isEntitled = dbo.fn_SalarySetup_IsEmployeeLFAEntitled(" + EmpID + ") ", ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                retVal = Convert.ToBoolean(ds.Tables[0].Rows[0]["isEntitled"]);

            return retVal;
        }
        public int GetEmployeeAvailableLFA(string EmpID)
        {
            int LFACnt = 0;
            DataSet ds = new DataSet();

            string strMessage = _dataservice.ExecuteReader(" Select LFACnt = dbo.fn_Leave_GetAvailableLFACount(" + EmpID + ", NULL) ", ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                LFACnt = Convert.ToInt32(ds.Tables[0].Rows[0]["LFACnt"]);

            return LFACnt;
        }
        public ValMessage ValidateLeaveRequest(TblEmpLeave EmpLeaveValidate, string EmpCompanyId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            //   List<ValMessage> valMsg = new List<ValMessage>();
            LeaveAvailType lvAvlType;
            ValMessage valMsg = new ValMessage();
            try
            {
                bool blnGradeWiseLeaveAvailTypeDefined = false;
                string lvType = string.Empty;
                DataSet dsData = GetLeaveInfoForSelectedEmployee(EmpLeaveValidate.EmpId.ToString(), strLoginEmpId, strLoginCompanyId, _culture);
                if (dsData != null && dsData.Tables[0] != null && dsData.Tables[0].Rows.Count > 0)
                    blnGradeWiseLeaveAvailTypeDefined = Convert.ToBoolean(dsData.Tables[0].Rows[0][0]);
                if (blnGradeWiseLeaveAvailTypeDefined)
                {
                    if (dsData != null && dsData.Tables[1] != null && dsData.Tables[1].Rows.Count > 0)
                    {
                        lvType = Convert.ToString(dsData.Tables[1].Rows[0][0]);

                        //  hf_LeaveAvailType.Value = lvType;
                    }
                    switch (lvType)
                    {
                        case "FDHD":
                            lvAvlType = LeaveAvailType.FDHD;
                            break;
                        case "DHM":
                            lvAvlType = LeaveAvailType.DHM;
                            break;

                    }
                }
                string txtLvDays = string.Empty;
                
                txtLvDays = EmpLeaveValidate.LeaveDays.ToString();
                
                string ChkDeductVal = string.Empty;
                if (EmpLeaveValidate.DeductlPrev == true)
                {
                    ChkDeductVal = "1";
                }
                else
                {
                    ChkDeductVal = "0";
                }
                string remarks = "";
                remarks = EmpLeaveValidate.Remarks.Replace("'", "''");
                if (remarks != "" && remarks.Contains("&"))
                    remarks = remarks.Replace("&", "&amp;");
                DataSet ds = new DataSet();

                EmpLeaveValidate.is_leaveadj_confirmed = "0";

                string strQuery = "Exec SP_Leave_ValidateRequest " +
                          " @Param_EmpId = " + EmpLeaveValidate.EmpId.ToString() + ", @Param_LCode = '" + EmpLeaveValidate.LCode + "', @Param_StrDateFrom = '" + EmpLeaveValidate.DateFrom + "', " +
                          " @Param_StrDateTo = '" + EmpLeaveValidate.DateTo + "', @Param_strLeaveDays = '" + txtLvDays + "', @Param_Reason = '" + remarks + "'," +
                          " @Param_AvailFromPreviousYearBalance = " + ChkDeductVal + ", " +
                          " @Param_FormId = '" + Constants.LeaveRequest + "', @Param_Language = '" + _culture + "', @Param_ApplicationCode = '" + "HRIS" + "', @EmpCompanyId = " + EmpCompanyId + " , @Param_ApprovalStatus = '1', " +
                          " @Param_ValidationEvent = '1', @Param_isDocumentAttached = '" + EmpLeaveValidate.is_Document_Attached + "',@Param_isLeaveAdjustmentConfirmed = " + EmpLeaveValidate.is_leaveadj_confirmed + ",@Param_IsLFAEntry = " + (EmpLeaveValidate.is_LFA_Entry == true ? "1" : "0");

                string result = _dataservice.ExecuteReader(strQuery, ref ds);
                // string ValidateMessage = "";

                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                    {

                        valMsg.ValidationCode = ds.Tables[0].Rows[0]["ValidationCode"].ToString();
                        valMsg.FieldName = ds.Tables[0].Rows[0]["FieldId"].ToString();
                        valMsg.ValidationMessage = ds.Tables[0].Rows[0]["Msg"].ToString();
                    }
                }

            }
            catch (Exception ex)
            {
            }
            return valMsg;

        }


        public ValMessage ValidateLeaveRequestWeb(LeaveRequest EmpLeaveValidate, string EmpCompanyId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            ValMessage valMsg = new ValMessage();
            try
            {
                
                string lvAvlType = lvType(EmpLeaveValidate.EmpId);
                string txtLvDays = string.Empty;
                if (lvAvlType == "FDHD")
                {
                    txtLvDays = EmpLeaveValidate.leavedays_FDHD.ToString();
                }
                else
                {
                    txtLvDays = EmpLeaveValidate.leavedays_DHM.ToString();
                }
                string ChkDeductVal = string.Empty;
                if (EmpLeaveValidate.DeductPrev == true)
                {
                    ChkDeductVal = "1";
                }
                else
                {
                    ChkDeductVal = "0";
                }
                string remarks = "";
                remarks = EmpLeaveValidate.Remarks.Replace("'", "''");
                if (remarks != "" && remarks.Contains("&"))
                    remarks = remarks.Replace("&", "&amp;");
                DataSet ds = new DataSet();

                string chkis_leaveadj_confirmed = string.Empty;
                if (EmpLeaveValidate.is_leaveadj_confirmed == true)
                    chkis_leaveadj_confirmed = "1";
                else
                    chkis_leaveadj_confirmed = "0";

                string strQuery = "Exec SP_Leave_ValidateRequest " +
                          " @Param_EmpId = " + EmpLeaveValidate.EmpId.ToString() + ", @Param_LCode = '" + EmpLeaveValidate.LCode + "', @Param_StrDateFrom = '" + EmpLeaveValidate.FromDate + "', " +
                          " @Param_StrDateTo = '" + EmpLeaveValidate.ToDate + "', @Param_strLeaveDays = '" + txtLvDays + "', @Param_Reason = '" + remarks + "'," +
                          " @Param_AvailFromPreviousYearBalance = " + ChkDeductVal + ", " +
                          " @Param_FormId = '" + Constants.LeaveRequest + "', @Param_Language = '" + _culture + "', @Param_ApplicationCode = '" + "HRIS" + "', @EmpCompanyId = " + EmpCompanyId + " , @Param_ApprovalStatus = '1', " +
                          " @Param_ValidationEvent = '1', @Param_isDocumentAttached = '" + EmpLeaveValidate.is_Document_Attached + "',@Param_isLeaveAdjustmentConfirmed = " + chkis_leaveadj_confirmed + ",@Param_IsLFAEntry = " + (EmpLeaveValidate.is_LFA_Entry == true ? "1" : "0");

                string result = _dataservice.ExecuteReader(strQuery, ref ds);

                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                    {
                        //valMsg.ValidationKey = ds.Tables[0].Rows[0]["ValidationKey"].ToString();
                        valMsg.ValidationCode = ds.Tables[0].Rows[0]["ValidationCode"].ToString();
                        valMsg.FieldName = ds.Tables[0].Rows[0]["FieldId"].ToString();
                        //valMsg.CrtlId = ds.Tables[0].Rows[0]["CrtlId"].ToString();
                        valMsg.ValidationMessage = ds.Tables[0].Rows[0]["Msg"].ToString();
                    }
                }

            }
            catch (Exception ex)
            {
            }
            return valMsg;

        }

        //old start
        //public List<ValMessage> ValidateLeaveRequest(TblEmpLeaveValidate EmpLeaveValidate)
        //{
        //    List<ValMessage> valMsg = new List<ValMessage>();
        //    try
        //    {

        //        DataSet ds = new DataSet();
        //        string strQuery = "Exec SP_Leave_ValidateRequest " +
        //                  " @Param_EmpId = " + EmpLeaveValidate.EmpId.ToString() + ", @Param_LCode = '" + EmpLeaveValidate.LCode + "', @Param_StrDateFrom = '" + EmpLeaveValidate.DateFrom + "', " +
        //                  " @Param_StrDateTo = '" + EmpLeaveValidate.DateTo + "', @Param_strLeaveDays = '" + EmpLeaveValidate.LeaveDays + "', @Param_Reason = '" + EmpLeaveValidate.Reason + "'," +
        //                  " @Param_AvailFromPreviousYearBalance = " + EmpLeaveValidate.AvailPrev + ", " +
        //                  " @Param_FormId = '" + EmpLeaveValidate.FormID + "', @Param_Language = '" + EmpLeaveValidate.LangCode + "', @Param_ApplicationCode = 'HRIS', @EmpCompanyId = " + Utilities.GetEmployeeCompanyId(EmpLeaveValidate.EmpId.ToString()) + " , @Param_ApprovalStatus = '1', " +
        //                  " @Param_ValidationEvent = '1', @Param_isDocumentAttached = '" + EmpLeaveValidate.is_Document_Attached + "',@Param_isLeaveAdjustmentConfirmed = " + EmpLeaveValidate.is_leaveadj_confirmed + ",@Param_IsLFAEntry = " + (EmpLeaveValidate.is_LFA_Entry == true ? "1" : "0");

        //        string result = _dataservice.ExecuteReader(strQuery, ref ds);
        //        // string ValidateMessage = "";

        //        if (ds != null && ds.Tables.Count > 0)
        //        {
        //            if (ds.Tables[0].Rows.Count > 0)
        //            {

        //                valMsg.Add(new ValMessage
        //                {

        //                    ValidationCode = ds.Tables[0].Rows[0]["ValidationCode"].ToString(),
        //                    FieldName = ds.Tables[0].Rows[0]["FieldId"].ToString(),
        //                    ValidationMessage = ds.Tables[0].Rows[0]["Msg"].ToString()

        //                });

        //            }
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    return valMsg;

        //}
        //old end
        public string getLeaveAvailType_EmployeeWise(string _EmpId)
        {
            DataSet ds = new DataSet();
            string strMessage = string.Empty, strReturnVal = string.Empty;

            strMessage = _dataservice.ExecuteReader(" Select LeaveAvailType = dbo.fn_Leave_GetEmployeeLeaveAvailType(" + _EmpId + ",NULL)", ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                strReturnVal = Convert.ToString(ds.Tables[0].Rows[0]["LeaveAvailType"]);
            //else
            //    strReturnVal = getLeaveAvailType_CompanyWise();

            return strReturnVal;
        }

        public string GetProperLeaveDays(string _LeaveDays, string _EmpId, string _TxnDate)
        {
            
            object var = null;
            string errorMessage = string.Empty, TxnDate = string.Empty;
            DateTime dtTxn = new DateTime();
            if (DateTime.TryParse(_TxnDate, out dtTxn))
            { }

            string vObj = Convert.ToString(getLeaveAvailType_EmployeeWise(_EmpId));
            TxnDate = dtTxn.Year.ToString() + "/" + (dtTxn.Month < 10 ? "0" + dtTxn.Month.ToString() : dtTxn.Month.ToString()) + "/" + (dtTxn.Day < 10 ? "0" + dtTxn.Day.ToString() : dtTxn.Day.ToString());

            if (vObj.Equals("DHM"))
            {
                errorMessage = _dataservice.ExecuteStatement("Select dbo.DHMtoFDHD_New('" + _LeaveDays + "', CAST( dbo.fn_GetEmpWorkingHours(" + _EmpId + ", '" + TxnDate + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + " ) as decimal(18,2)))", ref var, 1);
                if (var != null && _utilities.isNumaric(var))
                    return var.ToString();
            }
            else
            {
                errorMessage = _dataservice.ExecuteStatement("Select dbo.FDHDtoDHM_New(" + _LeaveDays + ", CAST( dbo.fn_GetEmpWorkingHours(" + _EmpId + ", '" + TxnDate + "', " + _utilities .GetCompanyId(_clientContextService.GetClientIP()) + " ) as decimal(18,2)))", ref var, 1);
                if (var != null)
                    return var.ToString();
            }

            return _LeaveDays;
        }

        public string getLeaveDays(string _DateFrom, string _DateTo, string _LeaveDays, string EmpId)
        {
            DateTime dtFrom, dtTo;
            string strLeaveAvailType, strReturnVal = string.Empty;
            //Getting Leave Avail Type of Currently Selected Employee
            strLeaveAvailType = getLeaveAvailType_EmployeeWise(EmpId);

            if (strLeaveAvailType.Trim().Equals("FDHD"))
            {
                strReturnVal = _LeaveDays;
            }
            else
                if (strLeaveAvailType.Trim().Equals("DHM"))
            {
                if (DateTime.TryParse(_DateFrom, out dtFrom) && DateTime.TryParse(_DateTo, out dtTo))
                {
                    if ((DateTime.Compare(dtTo, dtFrom)) == 0)
                    {
                        strReturnVal = GetProperLeaveDays(_LeaveDays, EmpId, _DateFrom);
                    }
                    else
                    {
                        strReturnVal = _LeaveDays.Substring(0, 5);// +":0:00";
                    }
                }
            }

            return strReturnVal;

        }

        public string getLeaveDaysInDHMFormat(string _DateFrom, string _DateTo, string _LeaveDays, string EmpId)
        {
            DateTime dtFrom, dtTo;
            string strLeaveAvailType, strReturnVal = string.Empty;
            string[] strArr;
            //Getting Leave Avail Type of Currently Selected Employee
            strLeaveAvailType = getLeaveAvailType_EmployeeWise(EmpId);


            if (DateTime.TryParse(_DateFrom, out dtFrom) && DateTime.TryParse(_DateTo, out dtTo))
            {
                if ((DateTime.Compare(dtTo, dtFrom)) == 0)
                {
                    if (strLeaveAvailType == "FDHD")
                    {
                        strArr = _LeaveDays.Split('.');
                        strReturnVal = GetProperLeaveDays(strArr.Length == 2 ? strArr[1] : strArr[0], EmpId, _DateFrom);
                    }
                    else
                    {
                        strReturnVal = getLeaveDaysInDHMFormat(EmpId, _LeaveDays);
                    }
                }
                else
                {
                    if (strLeaveAvailType == "FDHD")
                    {
                        strArr = _LeaveDays.Split('.');
                        strReturnVal = (Convert.ToInt32(strArr[0]) < 100 ? (Convert.ToInt32(strArr[0]) < 10 ? "00" : "0") : "") + strArr[0] + ":0:00";//only decimal part will be taken... like if it is 1.25 then 1 
                    }
                    else
                        strReturnVal = getLeaveDaysInDHMFormat(EmpId, _LeaveDays);
                }
            }
            return strReturnVal;
        }
        public string getLeaveDaysInDHMFormat(string _EmpId, string _LeaveDays)
        {
            
            DataSet ds = new DataSet();
            string strMessage = string.Empty, strReturnVal = string.Empty;
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            strMessage = _dataservice.ExecuteReader(" select dbo.fdhdtodhm_new(dbo.dhmtofdhd_new('" + _LeaveDays + "', CAST( dbo.fn_GetEmpWorkingHours(" + _EmpId + ", GETDATE(), " + _CompanyId + ") as decimal(18,0))),CAST( dbo.fn_GetEmpWorkingHours(" + _EmpId + ", GETDATE(), " + _CompanyId + ") as decimal(18,0))) ", ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                strReturnVal = Convert.ToString(ds.Tables[0].Rows[0][0]);

            return strReturnVal;
        }

        public string InsertLeave(TblEmpLeave EmpLeaveReq, string forwardByEmpId, string MdeptId, string DeptId, string DesigId, int wfLevel, int authorityType, int dApprover, bool IsOnReportToLevels)
        {
            string strReturnVal = string.Empty;

            //string strQuery = " if Exists (Select name from sys.objects where name = 'Sp_Leave_SaveRecord') " +
            //                    " Select 'Exists' " +
            //                " Else " +
            //                    " Select 'DoNotExist' ";

            //string strMsg = dataService.GetDataSet(strQuery, ref ds);

            //if (ds != null && ds.Tables.Count > 0)
            //{
            //    if (ds.Tables[0] != null && ds.Tables[0].Rows.Count > 0)
            //    {
            //if (Convert.ToString(ds.Tables[0].Rows[0][0]).Equals("Exists"))
            strReturnVal = InsertLeave_New(EmpLeaveReq, forwardByEmpId, MdeptId, DeptId, DesigId, wfLevel, authorityType, dApprover, IsOnReportToLevels);
            //else
            //    strReturnVal = InsertLeave_Old(EmpLeave, forwardByEmpId, MdeptId, DeptId, DesigId, wfLevel, authorityType, dApprover, IsOnReportToLevels);
            //}
            //}
            return strReturnVal;
        }
        public string InsertLeave_New(TblEmpLeave EmpLeaveReq, string forwardByEmpId, string MdeptId, string DeptId, string DesigId, int wfLevel, int authorityType, int dApprover, bool IsOnReportToLevels)
        {
            
            DataSet ds = new DataSet();
            string strReturnVal = string.Empty;

            string strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            string strUserId = _utilities.GetUserid(_clientContextService.GetClientIP());
            string strFormId = Constants.LeaveRequest.ToString();
            string strUserEmpId = Convert.ToString(_utilities.GetEmpid(_clientContextService.GetClientIP()));
            string strEntTerminal = _utilities.GetTerminalId();
            string strEntTerminalIP = _utilities.GetTerminalIP();
            string _culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string chkDeductFromPrevBalance = string.Empty;
            string IsLFAEntry = string.Empty;
            if (EmpLeaveReq.DeductlPrev == true)
            {
                chkDeductFromPrevBalance = "1";
            }
            else
            {
                chkDeductFromPrevBalance = "0";
            }

            if (EmpLeaveReq.is_LFA_Entry == true)
            {
                IsLFAEntry = "1";
            }
            else
            {
                IsLFAEntry = "0";
            }

            if (EmpLeaveReq.is_Mobile != 1)
            {
                EmpLeaveReq.is_Mobile = 0;
            }

            string strQuery = " Exec dbo.Sp_Leave_SaveRecord " +
                        " @Param_LoginCulture = '" + _culture + "', " +
                        " @LoginCompanyId = " + strLoginCompanyId + ", @EmpId = " + EmpLeaveReq.EmpId + ", @LCode = '" + EmpLeaveReq.LCode + "', @DateFrom = '" + EmpLeaveReq.DateFrom + "', " +
                        " @DateTo = '" + EmpLeaveReq.DateTo + "', @LeaveDays = " + EmpLeaveReq.LeaveDays + ", @Reason = '" + EmpLeaveReq.Remarks + "'," +
                        " @DeductFromPreviousYearBalance = " + chkDeductFromPrevBalance + ", @LeaveDays_DHM = '" + EmpLeaveReq.leavedays_DHM + "', " +

                       " @isPortalEntry = 1, @isWorkFlowEntry = 1, " +
                        " @AttchFileName = " + (EmpLeaveReq.attachedFileName != null ? "'" + EmpLeaveReq.attachedFileName + "'" : "NULL") + ", " +
                        " @AttchExtension = " + (EmpLeaveReq.attachedFileExtension != null ? "'" + EmpLeaveReq.attachedFileExtension + "'" : "NULL") + ", " +
                        " @AttchContentType = " + (EmpLeaveReq.ContentType != null ? "'" + EmpLeaveReq.ContentType + "'" : "NULL") + ", " +
                        " @AttchFileData = " + (EmpLeaveReq.Attachment != null ? "0x" + BitConverter.ToString(EmpLeaveReq.Attachment).Replace("-", "") : "NULL") + ", " +
                        " @UserId = '" + strUserId + "', @FormId = '" + strFormId + "', @UserEmpId = " + strUserEmpId + ", @EntTerminal = '" + strEntTerminal + "', " +
                        " @EntTerminalIP = '" + strEntTerminalIP + "', @isMobileAppEntry = " + EmpLeaveReq.is_Mobile + " , @isSMSEntry = 0,@Param_IsLFAEntry = " + IsLFAEntry;

            try
            {
                //DataAccessClasswithIsolation objCls = new DataAccessClasswithIsolation();
                //ds = objCls.ExecuteQueryReturnDataSet(strQuery);
                string strMsg = _dataservice.GetDataSet(strQuery, ref ds);

                if (ds != null && ds.Tables.Count > 0)
                {
                    DataTable dtForwardEmployeeInfo = new DataTable(), dtMessage = new DataTable();

                    if (ds.Tables.Count == 2)
                    {
                        dtForwardEmployeeInfo = ds.Tables[0];
                        dtMessage = ds.Tables[1];

                        if (dtMessage != null && dtMessage.Rows.Count == 1)
                        {
                            if (dtMessage.Rows[0][0].ToString() != "50000")
                                strReturnVal = Convert.ToString(dtMessage.Rows[0][0]);
                        }

                        else if (dtMessage.Rows[0][0].ToString() == "50000")
                        {
                            strReturnVal = Convert.ToString(dtMessage.Rows[0][0]);
                        }


                        if (dtForwardEmployeeInfo != null && dtForwardEmployeeInfo.Rows.Count > 0)
                        {
                            //string[] str = Convert.ToString(dtForwardEmployeeInfo.Rows[0][0]).Split(',');
                            //string strEmpLeaveId = string.Empty;

                            //if (str.Length == 2)
                            //{
                            //    strEmpLeaveId = str[0];
                            //    strRequestForwardedTo = str[1];
                            //    int intRequestForwardedTo, intEmpLeaveId;

                            //    if (int.TryParse(strEmpLeaveId, out intEmpLeaveId)) { }
                            //    if (int.TryParse(strRequestForwardedTo, out intRequestForwardedTo)) { }

                            //if (intRequestForwardedTo > 0 && intEmpLeaveId > 0)
                            //{
                            //    strReturnVal = strReturnVal + HandleIntermediateEmail(strRequestForwardedTo, strEmpLeaveId);
                            //}
                            //}
                        }
                        else
                        {
                            if (dtMessage != null && dtMessage.Rows.Count == 1)
                                strReturnVal = Convert.ToString(dtMessage.Rows[0][1]);
                        }
                    }
                    else
                    {
                        //dtForwardEmployeeInfo = ds.Tables[0];
                        dtMessage = ds.Tables[0];

                        if (dtMessage != null && dtMessage.Rows.Count == 1)
                            strReturnVal = Convert.ToString(dtMessage.Rows[0][1]);
                    }
                }
            }
            catch (Exception _excep)
            {
                strReturnVal = _excep.Message;
            }
            return strReturnVal;
        }

        public string InsertLeave_Web(LeaveRequest EmpLeaveReq)
        {
            
            DataSet ds = new DataSet();
            string strReturnVal = string.Empty;

            string strLoginCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            string strUserId = _utilities.GetUserid(_clientContextService.GetClientIP());
            string strFormId = Constants.LeaveRequest.ToString();
            string strUserEmpId = Convert.ToString(_utilities.GetEmpid(_clientContextService.GetClientIP()));
            string strEntTerminal = _utilities.GetTerminalId();
            string strEntTerminalIP = _utilities.GetTerminalIP();
            string _culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string chkDeductFromPrevBalance = string.Empty;
            string IsLFAEntry = string.Empty;
            if (EmpLeaveReq.DeductPrev == true)
            {
                chkDeductFromPrevBalance = "1";
            }
            else
            {
                chkDeductFromPrevBalance = "0";
            }

            if (EmpLeaveReq.is_LFA_Entry == true)
            {
                IsLFAEntry = "1";
            }
            else
            {
                IsLFAEntry = "0";
            }

            string lvAvlType = lvType(EmpLeaveReq.EmpId);

            string txtLvDays = string.Empty;
            if (lvAvlType == "FDHD")
            {
                txtLvDays = EmpLeaveReq.leavedays_FDHD.ToString();
            }
            else
            {
                txtLvDays = EmpLeaveReq.leavedays_DHM.ToString();
            }

            string strQuery = " Exec dbo.Sp_Leave_SaveRecord " +
                        " @Param_LoginCulture = '" + _culture + "', " +
                        " @LoginCompanyId = " + strLoginCompanyId + ", @EmpId = " + EmpLeaveReq.EmpId + ", @LCode = '" + EmpLeaveReq.LCode + "', @DateFrom = '" + EmpLeaveReq.FromDate + "', " +
                        " @DateTo = '" + EmpLeaveReq.ToDate + "', @LeaveDays = '" + txtLvDays + "', @Reason = '" + EmpLeaveReq.Remarks + "'," +
                        " @DeductFromPreviousYearBalance = " + chkDeductFromPrevBalance + ", @LeaveDays_DHM = '" + EmpLeaveReq.leavedays_DHM + "', " +

                        " @isPortalEntry = 1, @isWorkFlowEntry = 1, " +
                        " @AttchFileName = " + (EmpLeaveReq.attachedFileName != null ? "'" + EmpLeaveReq.attachedFileName + "'" : "NULL") + ", " +
                        " @AttchExtension = " + (EmpLeaveReq.attachedFileExtension != null ? "'" + EmpLeaveReq.attachedFileExtension + "'" : "NULL") + ", " +
                        " @AttchContentType = " + (EmpLeaveReq.ContentType != null ? "'" + EmpLeaveReq.ContentType + "'" : "NULL") + ", " +
                        " @AttchFileData = " + (EmpLeaveReq.Attachment != null ? "0x" + BitConverter.ToString(EmpLeaveReq.Attachment).Replace("-", "") : "NULL") + ", " +
                        " @UserId = '" + strUserId + "', @FormId = '" + strFormId + "', @UserEmpId = " + strUserEmpId + ", @EntTerminal = '" + strEntTerminal + "', " +
                        " @EntTerminalIP = '" + strEntTerminalIP + "', @isMobileAppEntry = 0 , @isSMSEntry = 0,@Param_IsLFAEntry = " + IsLFAEntry;

            try
            {
                string strMsg = _dataservice.GetDataSet(strQuery, ref ds);

                if (ds != null && ds.Tables.Count > 0)
                {
                    DataTable dtForwardEmployeeInfo = new DataTable(), dtMessage = new DataTable();

                    if (ds.Tables.Count == 2)
                    {
                        dtForwardEmployeeInfo = ds.Tables[0];
                        dtMessage = ds.Tables[1];

                        if (dtMessage != null && dtMessage.Rows.Count == 1)
                        {
                            if (dtMessage.Rows[0][0].ToString() != "50000")
                                strReturnVal = Convert.ToString(dtMessage.Rows[0][0]);
                        }

                        else if (dtMessage.Rows[0][0].ToString() == "50000")
                        {
                            strReturnVal = Convert.ToString(dtMessage.Rows[0][0]);
                        }


                        if (dtForwardEmployeeInfo != null && dtForwardEmployeeInfo.Rows.Count > 0)
                        {
                            //string[] str = Convert.ToString(dtForwardEmployeeInfo.Rows[0][0]).Split(',');
                            //string strEmpLeaveId = string.Empty;

                            //if (str.Length == 2)
                            //{
                            //    strEmpLeaveId = str[0];
                            //    strRequestForwardedTo = str[1];
                            //    int intRequestForwardedTo, intEmpLeaveId;

                            //    if (int.TryParse(strEmpLeaveId, out intEmpLeaveId)) { }
                            //    if (int.TryParse(strRequestForwardedTo, out intRequestForwardedTo)) { }

                            //if (intRequestForwardedTo > 0 && intEmpLeaveId > 0)
                            //{
                            //    strReturnVal = strReturnVal + HandleIntermediateEmail(strRequestForwardedTo, strEmpLeaveId);
                            //}
                            //}
                        }
                        else
                        {
                            if (dtMessage != null && dtMessage.Rows.Count == 1)
                                strReturnVal = Convert.ToString(dtMessage.Rows[0][1]);
                        }
                    }
                    else
                    {
                        dtMessage = ds.Tables[0];

                        if (dtMessage != null && dtMessage.Rows.Count == 1)
                            strReturnVal = Convert.ToString(dtMessage.Rows[0][1]);
                    }
                }
            }
            catch (Exception _excep)
            {
                strReturnVal = _excep.Message;
            }
            return strReturnVal;
        }
        public ValMessage SaveLeaveRequest(TblEmpLeave EmpLeaveReq, string _EmpCompanyId, string strLoginEmpId, string strLoginCompanyId, string _culture)
        {
            

            string errorMessage3 = string.Empty;
            ValMessage VMsg = null;

            if (_utilities.isLeaveRequestForSubordinate(_utilities.GetCompanyId(_clientContextService.GetClientIP()), _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP())))
            {

                if (string.IsNullOrEmpty(EmpLeaveReq.LCode))
                {
                    var LCODE = LoadEmployeeData(EmpLeaveReq.EmpId.ToString(), strLoginEmpId, strLoginCompanyId, _culture);

                    if (LCODE.Count > 0)
                    {
                        //  lstLeave.Add("N/A");
                        foreach (var a in LCODE)
                        {
                            //lstLeave.Add(a.Description);

                            if (a.Description.ToUpper() == EmpLeaveReq.Description.ToUpper())
                            {
                                EmpLeaveReq.LCode = a.LCode.Trim();
                            }
                        }
                    }

                }
                else
                {
                    EmpLeaveReq.LCode = EmpLeaveReq.LCode.Trim();
                }
                VMsg = ValidateLeaveRequest(EmpLeaveReq, _EmpCompanyId, strLoginEmpId, strLoginCompanyId, _culture);
                if (VMsg == null || VMsg.ValidationMessage == null)
                {
                    DataSet dsLoggedIn = new DataSet();
                    DataSet dsHRPDetail = new DataSet();
                    string dateFrom = EmpLeaveReq.DateFrom.Substring(6, 4) + "-" + EmpLeaveReq.DateFrom.Substring(3, 2) + "-" + EmpLeaveReq.DateFrom.Substring(0, 2);
                    string dateTo = EmpLeaveReq.DateTo.Substring(6, 4) + "-" + EmpLeaveReq.DateTo.Substring(3, 2) + "-" + EmpLeaveReq.DateTo.Substring(0, 2);
                    EmpLeaveReq.DateFrom = dateFrom;
                    EmpLeaveReq.DateTo = dateTo;

                    object objmap = _utilities.ExecuteSQLFunction("El from tblvariable where companyId=" + _EmpCompanyId);
                    string leaveMapCode = "";
                    if (objmap != null)
                        leaveMapCode = Convert.ToString(objmap);

                    object objlfs = _utilities.ExecuteSQLFunction("lFAEnt from  tblEmpSalarysetup where companyId=" + _EmpCompanyId + " and EmpId=" + EmpLeaveReq.EmpId);
                    bool LFAFlag = false;

                    if (objlfs != null && objlfs != DBNull.Value && objlfs != "")
                    {
                        LFAFlag = Convert.ToBoolean(objlfs);
                    }
                    string errorMessage1 = _dataservice.GetDataWithClause("MdptId, dptId, dsgId", "TblEmployee", "EmpId = " + EmpLeaveReq.EmpId, ref dsLoggedIn);
                    string MdeptId = Convert.ToString(dsLoggedIn.Tables[0].Rows[0][0]);
                    string DeptId = Convert.ToString(dsLoggedIn.Tables[0].Rows[0][1]);
                    string DesigId = Convert.ToString(dsLoggedIn.Tables[0].Rows[0][2]);
                    string errorMessage2 = _dataservice.GetDataWithClause("a.DefaultApprover, a.AuthorityType", "tblHRPolicyDetail a Inner Join tblSetupsDetail b On a.WFlowTypeId = b.sdlid ", "Cast(b.Code as int) = 1 and a.CompanyId = " + _EmpCompanyId, ref dsHRPDetail);
                    if (dsHRPDetail == null || dsHRPDetail.Tables.Count <= 0 || dsHRPDetail.Tables[0].Rows.Count <= 0)
                    {
                        // lblMessage.Text = "Cannot continue! Workflow policy not defined.";
                        // lblMessage.Text = GetLocalResourceObject("CanNotContinueWFPNotDefined").ToString();

                        //   return;
                    }
                    int defaultApprover = Convert.ToInt32(dsHRPDetail.Tables[0].Rows[0][0]);
                    int authorityType = Convert.ToInt32(dsHRPDetail.Tables[0].Rows[0][1]);
                    int wfLevel = 0;

                    //Start Added By Danish Iftikhar for New WorkFlow Changes
                    bool IsOnReportToLevels = false;
                    //End Added By Danish Iftikhar for New WorkFlow Changes

                    if (authorityType == 1)
                    {
                        wfLevel = Convert.ToInt32(_utilities.GetScalarData("Count(*)", "tblWorkFlow a " +
                                                                          "Inner Join tblSetupsDetail b on b.sdlid=a.typeId", "b.Code = '001' and a.MDeptId = " + MdeptId + " and a.DeptId=" + DeptId + " and a.CompanyId=" + _EmpCompanyId));
                    }
                    else if (authorityType == 2)
                    {
                        //Start Added By Danish Iftikhar for New WorkFlow Changes
                        IsOnReportToLevels = Convert.ToBoolean(_utilities.GetScalarData("isnull(IsOnReportToLevels,0) as IsOnReportToLevels", "tblWorkFlow a " +
                                                              "Inner Join tblSetupsDetail b on b.sdlid=a.typeId", "b.Code = '001' and a.WorkFlowDesignationId = " + DesigId + " and a.CompanyId=" + _EmpCompanyId));
                        if (IsOnReportToLevels == false)
                        {
                            wfLevel = Convert.ToInt32(_utilities.GetScalarData("Count(*)", "tblWorkFlow a " +
                                                                              "Inner Join tblSetupsDetail b on b.sdlid=a.typeId", "b.Code = '001' and a.WorkFlowDesignationId = " + DesigId + " and a.CompanyId=" + _EmpCompanyId));
                        }
                        else
                        {
                            wfLevel = 1;
                        }



                    }

                    string lvAvlType = lvType(EmpLeaveReq.EmpId);
                    string LeaveDays;
                    if (lvAvlType == "FDHD")
                        LeaveDays = EmpLeaveReq.LeaveDays;//txtDaysFDHD;
                    else
                        LeaveDays = EmpLeaveReq.LeaveDays;

                    string leaveDays = getLeaveDays(EmpLeaveReq.DateFrom.ToString(), EmpLeaveReq.DateTo.ToString(), EmpLeaveReq.LeaveDays, EmpLeaveReq.EmpId.ToString());
                    EmpLeaveReq.LeaveDays = leaveDays;
                    EmpLeaveReq.Remarks = EmpLeaveReq.Remarks.Replace("'", "''").Trim();

                    if (lvAvlType == "FDHD")
                    {
                        EmpLeaveReq.leavedays_DHM = getLeaveDaysInDHMFormat(EmpLeaveReq.DateFrom, EmpLeaveReq.DateTo, EmpLeaveReq.LeaveDays, EmpLeaveReq.EmpId.ToString());
                    }
                    else
                    {
                        EmpLeaveReq.leavedays_DHM = LeaveDays;
                    }
                    errorMessage3 = InsertLeave(EmpLeaveReq, EmpLeaveReq.EmpId.ToString(), MdeptId, DeptId, DesigId, wfLevel, authorityType, defaultApprover, IsOnReportToLevels);
                    //End Added By Danish Iftikhar for New WorkFlow Changes
                    VMsg.ValidationMessage = errorMessage3;
                }
                else
                {
                    if (VMsg.ValidationCode == "060" || VMsg.ValidationCode == "061")
                    {
                        errorMessage3 = VMsg.ValidationMessage;
                    }

                    else
                    {
                        errorMessage3 = VMsg.ValidationMessage;
                    }
                }
            }
            else
            {
                VMsg.ValidationMessage = "System will tell the user that policy to raise that request cannot be created as policy doesn't allow.";
            }
            return VMsg;

        }

        //   public DataTable GetLeaveRequestFDHD(TblEmpGetLeaveRequest tblGetLeaveRequest)

        public List<Sp_Leave_GetRequest_Result1> GetLeaveRequestFDHD(TblEmpGetLeaveRequest tblGetLeaveRequest)
        {
            
            DataTable dt = new DataTable();
            List<Sp_Leave_GetRequest_Result1> GetAttRequests = new List<Sp_Leave_GetRequest_Result1>();

            try
            {
                if (tblGetLeaveRequest.SortColumns != "" && tblGetLeaveRequest.SortColumns.Length > 0)
                {
                    if (tblGetLeaveRequest.SortColumns.Contains("DESC"))
                        tblGetLeaveRequest.SortColumns = tblGetLeaveRequest.SortColumns.Replace("DESC", "");
                }

                if (tblGetLeaveRequest.SortColumns.Trim() == "")
                    tblGetLeaveRequest.SortColumns = "RequestCreatedOn";

                //   string Culture = HttpContext.Current.Session["AppUICulture"].ToString();
                string Culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                DataSet dataset = new DataSet();
                string whereClause = "";
                int LoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP());
                //whereClause += "Where pgid in (" + Utilities.GetPGIdsWRTFormWiseUserWise(Constants.LeaveStatus, Utilities.GetCompanyId()) + ") ";

                //if (_DivisionIds.Split(',')[0] == "0")
                //    _DivisionIds = "";

                //if (_PayrollIds.Split(',')[0] == "0")
                //    _PayrollIds = "";

                //if (_DeptIds.Split(',')[0] == "0")
                //    _DeptIds = "";

                //if (_DsgIds.Split(',')[0] == "0")
                //    _DsgIds = "";

                //if (_SubDeptIds.Split(',')[0] == "0")
                //    _SubDeptIds = "";

                //if (_GradeIds.Split(',')[0] == "0")
                //    _GradeIds = "";

                //if (_LocationIds.Split(',')[0] == "0")
                //    _LocationIds = "";

                //if (_EmpCatgIds.Split(',')[0] == "0")
                //    _EmpCatgIds = "";

                //if (_TeamIds.Split(',')[0] == "0")
                //    _TeamIds = "";

                //if (_EmpTypeIds.Split(',')[0] == "0")
                //    _EmpTypeIds = "";

                //if (_RegionIds.Split(',')[0] == "0")
                //    _RegionIds = "";

                //if (_EmpStatusIds.Split(',')[0] == "0")
                //_EmpStatusIds = "";

                //if (_ReportToIds.Split(',')[0] == "0")
                //    _ReportToIds = "";



                if (tblGetLeaveRequest.FallingDateFrom != null)
                    tblGetLeaveRequest.FallingDateFrom = Convert.ToDateTime(tblGetLeaveRequest.FallingDateFrom).ToString("yyyy-MM-dd");

                if (tblGetLeaveRequest.FallingDateTo != null)
                    tblGetLeaveRequest.FallingDateTo = Convert.ToDateTime(tblGetLeaveRequest.FallingDateTo).ToString("yyyy-MM-dd");

                if (tblGetLeaveRequest.CreatedDateFrom != null)
                    tblGetLeaveRequest.CreatedDateFrom = Convert.ToDateTime(tblGetLeaveRequest.CreatedDateFrom).ToString("yyyy-MM-dd");

                if (tblGetLeaveRequest.CreatedDateTo != null)
                    tblGetLeaveRequest.CreatedDateTo = Convert.ToDateTime(tblGetLeaveRequest.CreatedDateTo).ToString("yyyy-MM-dd");

                string errorMessage = "";
                int EmpId = _utilities.GetEmpid(_clientContextService.GetClientIP());
                string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                //string qry = "Exec Sp_Leave_GetRequest " + LoginEmpId + ",''," + CompanyId + ",'" + Status + "','1'," + _RaisedFor + "," + _RaisedBy + ",'','','', '','" + _FromDateFalling + "','" + _ToDateFalling + "','" + _FromDateCreated + "','" + _ToDateCreated + "'," + startRow + "," + pageSize + ",'HRIS','" + sortColumns + "','" + sortDirection + "','Table','" + _DivisionIds + "','" + _DeptIds + "','" + _SubDeptIds + "','" + _GradeIds + "','" + _TeamIds + "','" + _RegionIds + "','" + _PayrollIds + "','" + _DsgIds + "','" + _GradeIds + "','" + _EmpTypeIds + "','" + _ReportToIds + "','" + _EmpCatgIds + "','" + Constants.Leave + "'";
                string qry = "Exec Sp_Leave_GetRequest " + tblGetLeaveRequest.LoginEmpID + ",''," + tblGetLeaveRequest.LoginCompanyID + ",'" + tblGetLeaveRequest.LeaveStatus + "','1'," + tblGetLeaveRequest.RaisedForEmpID + "," + tblGetLeaveRequest.RaisedByEmpID + ",'','','','','', '','" + tblGetLeaveRequest.FallingDateFrom + "','" + tblGetLeaveRequest.FallingDateTo + "','" + tblGetLeaveRequest.CreatedDateFrom + "','" + tblGetLeaveRequest.CreatedDateTo + "'," + tblGetLeaveRequest.StartRowNo + "," + tblGetLeaveRequest.RowNo + ",'HRIS','" + tblGetLeaveRequest.SortColumns + "','" + tblGetLeaveRequest.SortDir + "','Table','','','','','','','','','','','','','" + tblGetLeaveRequest.EmpStatus + "','" + Constants.Leave + "','" + Culture + "'";
                errorMessage = _dataservice.ExecuteReader(qry, ref dataset);

                if (dataset != null)
                {
                    if (dataset.Tables.Count > 0)
                    {
                        if (dataset.Tables[0].Rows.Count > 0)
                        {
                            dt = dataset.Tables[0];

                            int numberOfRecords = dt.AsEnumerable().Where(x => Convert.ToBoolean(x["IsLFA"]) == true).ToList().Count;
                            //  HttpContext.Current.Session["ESS_LeaveApproval_IsAnyLeaveWithLFA"] = (numberOfRecords > 0 ? true : false);


                            GetAttRequests = dt.AsEnumerable().Select(r => new Sp_Leave_GetRequest_Result1()
                            {
                                RaisedFor = r["RaisedFor"].ToString(),
                                LeaveType = r["LeaveType"].ToString(),
                                DtFr = Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy"),
                                DtTo = Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy"),
                                Date = Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") == Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy") ? Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") : Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") + " " + '-' + " " + Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy"),

                                // Date = Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") + " " + '-' + " " + Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy"),
                                LeaveDays = r["LeaveDays"].ToString(),
                                LeaveStatus = r["LeaveStatus"].ToString() == "P" ? "Pending" : r["LeaveStatus"].ToString() == "A" ? "Approved" : r["LeaveStatus"].ToString() == "D" ? "Disapproved" : "",
                                Designation = r["Designation"].ToString(),
                                Remarks = r["Remarks"].ToString(),
                                WorkflowTypeId = Convert.ToInt32(r["WorkflowTypeId"]),
                                EmpLeaveId = Convert.ToInt32(r["EmpLeaveId"]),
                                AuthorityStatus = r["AuthorityStatus"].ToString() == "P" ? "Pending" : r["AuthorityStatus"].ToString() == "A" ? "Approved" : r["AuthorityStatus"].ToString() == "D" ? "Disapproved" : "",
                                //Id = Convert.ToInt32(r["Id"]),
                                //AttId = Convert.ToInt32(r["AttId"]),
                                //EmpId = Convert.ToInt32(r["EmpId"]),
                                //AuthorityCheckPoint = Convert.ToInt32(r["AuthorityCheckPoint"]),
                                //RequisitionStatus = r["RequisitionStatus"].ToString(),
                                //DtFr = r["DtFr"].ToString(),
                                //DtTo = r["DtTo"].ToString(),
                                //Remarks = r["Remarks"].ToString(),
                                //AttendanceDate = r["AttendanceDate"].ToString(),
                                //DivisionName = r["DivisionName"].ToString(),
                                //MainDepartment = r["MainDepartment"].ToString(),
                                //Department = r["Department"].ToString(),
                                //Location = r["Location"].ToString(),
                                //Team = r["Team"].ToString(),
                                //Region = r["Region"].ToString(),
                                //PayrollGroup = r["PayrollGroup"].ToString(),
                                //Designation = r["Designation"].ToString(),
                                //JobGroup = r["JobGroup"].ToString(),
                                //EmployeeType = r["EmployeeType"].ToString(),
                                //EmployeeCategory = r["EmployeeCategory"].ToString(),
                                //ReportToName = r["ReportToName"].ToString(),
                                //Active = r["Active"].ToString(),
                                //PendingWith = r["PendingWith"].ToString(),
                                //RaisedByEmp = r["RaisedByEmp"].ToString(),
                                //AuthorityStatus = r["AuthorityStatus"].ToString() == "P" ? "Pending" : r["AuthorityStatus"].ToString() == "A" ? "Approved" : r["AuthorityStatus"].ToString() == "D" ? "Disapproved" : "",
                                //RaisedFor = r["RaisedFor"].ToString(),
                                //ProposedTimeInAndDate = r["ProposedTimeInAndDate"].ToString(),
                                //ProposedTimeOutAndDate = r["ProposedTimeOutAndDate"].ToString(),
                                //ActualStatus = r["ActualStatus"].ToString(),
                                //ProposedStatus = r["ProposedStatus"].ToString(),
                                //ActualTimeInAndDate = r["ActualTimeInAndDate"].ToString(),
                                //ActualTimeOutAndDate = r["ActualTimeOutAndDate"].ToString(),
                                //ActualShiftCode = r["ActualShiftCode"].ToString(),
                                //ShiftCode = r["ShiftCode"].ToString(),
                                //SortCol = r["SortCol"].ToString(),
                                //CompanyId = Convert.ToInt32(r["CompanyId"]),
                                //SkipBy = r["SkipBy"].ToString(),
                                //CreatedOn = r["CreatedOn"].ToString(),
                                //DateInChangeRequested = Convert.ToBoolean(r["DateInChangeRequested"]),
                                //DateOutChangeRequested = Convert.ToBoolean(r["DateOutChangeRequested"]),
                                //TimeInChangeRequested = Convert.ToBoolean(r["TimeInChangeRequested"]),
                                //ResultSetRowNumber = Convert.ToInt32(r["ResultSetRowNumber"])
                            }).ToList();

                        }
                    }
                }
                return GetAttRequests;
            }
            catch (Exception excp)
            {
                return GetAttRequests;
            }

        }

        public List<Sp_Leave_GetRequest_Result1> GetLeaveRequest(TblEmpGetLeaveRequest tblGetLeaveRequest)
        {
            

            DataTable dt = new DataTable();
            List<Sp_Leave_GetRequest_Result1> GetAttRequests = new List<Sp_Leave_GetRequest_Result1>();

            try
            {
                if (tblGetLeaveRequest.SortColumns != "" && tblGetLeaveRequest.SortColumns.Length > 0)
                {
                    if (tblGetLeaveRequest.SortColumns.Contains("DESC"))
                        tblGetLeaveRequest.SortColumns = tblGetLeaveRequest.SortColumns.Replace("DESC", "");
                }

                if (tblGetLeaveRequest.SortColumns.Trim() == "")
                    tblGetLeaveRequest.SortColumns = "RequestCreatedOn";

                //   string Culture = HttpContext.Current.Session["AppUICulture"].ToString();
                string Culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                DataSet dataset = new DataSet();
                string whereClause = "";
                int LoginEmpId = _utilities.GetEmpid(_clientContextService.GetClientIP());
                //whereClause += "Where pgid in (" + Utilities.GetPGIdsWRTFormWiseUserWise(Constants.LeaveStatus, Utilities.GetCompanyId()) + ") ";

                //if (_DivisionIds.Split(',')[0] == "0")
                //    _DivisionIds = "";

                //if (_PayrollIds.Split(',')[0] == "0")
                //    _PayrollIds = "";

                //if (_DeptIds.Split(',')[0] == "0")
                //    _DeptIds = "";

                //if (_DsgIds.Split(',')[0] == "0")
                //    _DsgIds = "";

                //if (_SubDeptIds.Split(',')[0] == "0")
                //    _SubDeptIds = "";

                //if (_GradeIds.Split(',')[0] == "0")
                //    _GradeIds = "";

                //if (_LocationIds.Split(',')[0] == "0")
                //    _LocationIds = "";

                //if (_EmpCatgIds.Split(',')[0] == "0")
                //    _EmpCatgIds = "";

                //if (_TeamIds.Split(',')[0] == "0")
                //    _TeamIds = "";

                //if (_EmpTypeIds.Split(',')[0] == "0")
                //    _EmpTypeIds = "";

                //if (_RegionIds.Split(',')[0] == "0")
                //    _RegionIds = "";

                //if (_EmpStatusIds.Split(',')[0] == "0")
                //_EmpStatusIds = "";

                //if (_ReportToIds.Split(',')[0] == "0")
                //    _ReportToIds = "";



                if (tblGetLeaveRequest.FallingDateFrom != null)
                    tblGetLeaveRequest.FallingDateFrom = Convert.ToDateTime(tblGetLeaveRequest.FallingDateFrom).ToString("yyyy-MM-dd");

                if (tblGetLeaveRequest.FallingDateTo != null)
                    tblGetLeaveRequest.FallingDateTo = Convert.ToDateTime(tblGetLeaveRequest.FallingDateTo).ToString("yyyy-MM-dd");

                if (tblGetLeaveRequest.CreatedDateFrom != null)
                    tblGetLeaveRequest.CreatedDateFrom = Convert.ToDateTime(tblGetLeaveRequest.CreatedDateFrom).ToString("yyyy-MM-dd");

                if (tblGetLeaveRequest.CreatedDateTo != null)
                    tblGetLeaveRequest.CreatedDateTo = Convert.ToDateTime(tblGetLeaveRequest.CreatedDateTo).ToString("yyyy-MM-dd");

                string errorMessage = "";
                int EmpId = _utilities.GetEmpid(_clientContextService.GetClientIP());
                string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                //string qry = "Exec Sp_Leave_GetRequest " + LoginEmpId + ",''," + CompanyId + ",'" + Status + "','1'," + _RaisedFor + "," + _RaisedBy + ",'','','', '','" + _FromDateFalling + "','" + _ToDateFalling + "','" + _FromDateCreated + "','" + _ToDateCreated + "'," + startRow + "," + pageSize + ",'HRIS','" + sortColumns + "','" + sortDirection + "','Table','" + _DivisionIds + "','" + _DeptIds + "','" + _SubDeptIds + "','" + _GradeIds + "','" + _TeamIds + "','" + _RegionIds + "','" + _PayrollIds + "','" + _DsgIds + "','" + _GradeIds + "','" + _EmpTypeIds + "','" + _ReportToIds + "','" + _EmpCatgIds + "','" + Constants.Leave + "'";
                string qry = "Exec Sp_Leave_GetRequest " + tblGetLeaveRequest.LoginEmpID + ",'" + tblGetLeaveRequest.Level + "'," + tblGetLeaveRequest.LoginCompanyID + ",'" + tblGetLeaveRequest.LeaveStatus + "','1'," + tblGetLeaveRequest.RaisedForEmpID + "," + tblGetLeaveRequest.RaisedByEmpID + ",'','','','','', '','" + tblGetLeaveRequest.FallingDateFrom + "','" + tblGetLeaveRequest.FallingDateTo + "','" + tblGetLeaveRequest.CreatedDateFrom + "','" + tblGetLeaveRequest.CreatedDateTo + "'," + tblGetLeaveRequest.StartRowNo + "," + tblGetLeaveRequest.RowNo + ",'HRIS','" + tblGetLeaveRequest.SortColumns + "','" + tblGetLeaveRequest.SortDir + "','Table','','','','','','','','','','','','','" + tblGetLeaveRequest.EmpStatus + "','" + Constants.LeaveStatus + "','" + Culture + "'";

                // string qry = "Exec Sp_Leave_GetRequest " + LoginEmpId + "," + Level + "," + CompanyId + ",'" + _LeaveStatus + "','1'," + EmpIdRaisedFor + "," + EmpIdRaisedBy + ",'','','','','','','" + _FallingFrom + "','" + _FallingTo + "','" + _CreatedFrom + "','" + _CreatedTo + "'," + startRow + "," + tatalrecord + ",'HRIS','" + sortColumns + "','" + sortDirection + "','Table','','','','','','','','','','','','','','" + Constants.LeaveStatus + "','" + Utilities.GetAppCurrentUICulture() + "','" + _DefaultSort + "'";
                errorMessage = _dataservice.ExecuteReader(qry, ref dataset);

                if (dataset != null)
                {
                    if (dataset.Tables.Count > 0)
                    {
                        if (dataset.Tables[0].Rows.Count > 0)
                        {
                            dt = dataset.Tables[0];

                            int numberOfRecords = dt.AsEnumerable().Where(x => Convert.ToBoolean(x["IsLFA"]) == true).ToList().Count;
                            //  HttpContext.Current.Session["ESS_LeaveApproval_IsAnyLeaveWithLFA"] = (numberOfRecords > 0 ? true : false);


                            GetAttRequests = dt.AsEnumerable().Select(r => new Sp_Leave_GetRequest_Result1()
                            {
                                RaisedFor = r["RaisedFor"].ToString(),
                                LeaveType = r["LeaveType"].ToString(),
                                DtFr = Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy"),
                                DtTo = Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy"),
                                Date = Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") == Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy") ? Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") : Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") + " " + '-' + " " + Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy"),

                                // Date = Convert.ToDateTime(r["DtFr"]).ToString("dd MMM yyyy") + " " + '-' + " " + Convert.ToDateTime(r["DtTo"]).ToString("dd MMM yyyy"),
                                LeaveDays = r["LeaveDays"].ToString(),
                                LeaveStatus = r["LeaveStatus"].ToString() == "P" ? "Pending" : r["LeaveStatus"].ToString() == "A" ? "Approved" : r["LeaveStatus"].ToString() == "D" ? "Disapproved" : "",
                                Designation = r["Designation"].ToString(),
                                Remarks = r["Remarks"].ToString(),
                                WorkflowTypeId = Convert.ToInt32(r["WorkflowTypeId"]),
                                EmpLeaveId = Convert.ToInt32(r["EmpLeaveId"]),
                                AuthorityStatus = r["AuthorityStatus"].ToString() == "P" ? "Pending" : r["AuthorityStatus"].ToString() == "A" ? "Approved" : r["AuthorityStatus"].ToString() == "D" ? "Disapproved" : "",
                                //Id = Convert.ToInt32(r["Id"]),
                                //AttId = Convert.ToInt32(r["AttId"]),
                                //EmpId = Convert.ToInt32(r["EmpId"]),
                                //AuthorityCheckPoint = Convert.ToInt32(r["AuthorityCheckPoint"]),
                                //RequisitionStatus = r["RequisitionStatus"].ToString(),
                                //DtFr = r["DtFr"].ToString(),
                                //DtTo = r["DtTo"].ToString(),
                                //Remarks = r["Remarks"].ToString(),
                                //AttendanceDate = r["AttendanceDate"].ToString(),
                                //DivisionName = r["DivisionName"].ToString(),
                                //MainDepartment = r["MainDepartment"].ToString(),
                                //Department = r["Department"].ToString(),
                                //Location = r["Location"].ToString(),
                                //Team = r["Team"].ToString(),
                                //Region = r["Region"].ToString(),
                                //PayrollGroup = r["PayrollGroup"].ToString(),
                                //Designation = r["Designation"].ToString(),
                                //JobGroup = r["JobGroup"].ToString(),
                                //EmployeeType = r["EmployeeType"].ToString(),
                                //EmployeeCategory = r["EmployeeCategory"].ToString(),
                                //ReportToName = r["ReportToName"].ToString(),
                                //Active = r["Active"].ToString(),
                                //PendingWith = r["PendingWith"].ToString(),
                                //RaisedByEmp = r["RaisedByEmp"].ToString(),
                                //AuthorityStatus = r["AuthorityStatus"].ToString() == "P" ? "Pending" : r["AuthorityStatus"].ToString() == "A" ? "Approved" : r["AuthorityStatus"].ToString() == "D" ? "Disapproved" : "",
                                //RaisedFor = r["RaisedFor"].ToString(),
                                //ProposedTimeInAndDate = r["ProposedTimeInAndDate"].ToString(),
                                //ProposedTimeOutAndDate = r["ProposedTimeOutAndDate"].ToString(),
                                //ActualStatus = r["ActualStatus"].ToString(),
                                //ProposedStatus = r["ProposedStatus"].ToString(),
                                //ActualTimeInAndDate = r["ActualTimeInAndDate"].ToString(),
                                //ActualTimeOutAndDate = r["ActualTimeOutAndDate"].ToString(),
                                //ActualShiftCode = r["ActualShiftCode"].ToString(),
                                //ShiftCode = r["ShiftCode"].ToString(),
                                //SortCol = r["SortCol"].ToString(),
                                //CompanyId = Convert.ToInt32(r["CompanyId"]),
                                //SkipBy = r["SkipBy"].ToString(),
                                //CreatedOn = r["CreatedOn"].ToString(),
                                //DateInChangeRequested = Convert.ToBoolean(r["DateInChangeRequested"]),
                                //DateOutChangeRequested = Convert.ToBoolean(r["DateOutChangeRequested"]),
                                //TimeInChangeRequested = Convert.ToBoolean(r["TimeInChangeRequested"]),
                                //ResultSetRowNumber = Convert.ToInt32(r["ResultSetRowNumber"])
                            }).ToList();

                        }
                    }
                }
                return GetAttRequests;
            }
            catch (Exception excp)
            {
                return GetAttRequests;
            }

        }



        public string ForwardToNextWithApprove(TblForwardToNextWithApprove objParam)
        {
            

            string Forwardmsg = "";
            DataSet ds = new DataSet();
            string Language = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string UserId = _utilities.GetUserid(_clientContextService.GetClientIP());
            string TerminalId = _utilities.GetTerminalId();
            string TerminalIp = _utilities.GetTerminalIP();
            string FormId = Constants.Leave;
            int EmpId = _utilities.GetEmpid(_clientContextService.GetClientIP());
            try
            {
                string Query = "declare @ReturnSpMessage nvarchar(max) = null " +
                          " EXEC[dbo].[Sp_Ess_Leave_ForwardToNextWithApprove]" +
                          " @Param_LoginCulture = '" + Language + "'," +
                          " @Param_LoginApplication = 'HRIS'," +
                          " @Param_Module = 'Leave', " +
                          " @Param_Login_CompanyId = " + CompanyId + ", " +
                          " @Param_Login_EmpId = '" + EmpId + "', " +
                          " @Param_LoginUserId = '" + UserId + "'," +
                          " @Param_EntTerminal = '" + TerminalId + "'," +
                          " @Param_EntTerminalIP = '" + TerminalIp + "'," +
                          " @Param_WorkflowCode = 1," +
                          " @EmpLeaveIds = '" + objParam.EmpLeaveID + "'," +
                          " @AuthorityRemarks = '" + objParam.AuthorityRemarks + "'," +
                          " @Param_AuthorityStatus= '" + objParam.AuthorityStatus + "'," +
                          " @Param_TotalRequestCount = " + objParam.TotalRequestCount + "," +
                          " @Param_FormId = '" + FormId + "'," +
                          " @Param_IsApprovalViaEmail = 0," +
                          " @ReturnVal = @ReturnSpMessage output " +
                          " Select @ReturnSpMessage";
                string result = _dataservice.ExecuteReader(Query, ref ds);
                if (result == "successfull")
                {
                    if (ds.Tables.Count > 0)
                    {
                        if (ds.Tables[0].Rows.Count > 0)
                        {
                            string Message = ds.Tables[0].Rows[0][0].ToString();
                            string[] RequestStatus = Message.Split('|');
                            if (RequestStatus[0] == "SUCCESS")
                            {

                                Forwardmsg = RequestStatus[1];
                                // gvLeaveReq.SelectedIndex = -1;
                                //    pnlLeaveTrack.Visible = false;
                                //  pnlLvBalFDHD.Visible = false;
                                //   BindGrid();
                                //ShowMessage(RequestStatus[1]);
                                //   AlertWithNewPopup(RequestStatus[1], "Success");
                            }

                            else
                            {
                                //   AlertWithNewPopup(RequestStatus[1], "Error");
                                //ShowMessageLbl(RequestStatus[1],"error");
                                Forwardmsg = Message;
                            }

                        }
                    }
                }


            }
            catch (Exception ex)
            {

            }

            return Forwardmsg;
        }

        
    }

    public class LeaveAttachmentInfo
    {
        public int AttachmentID { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public string ContentType { get; set; }
        public byte[] FileData { get; set; }
        public string CompanyID { get; set; }
        public int EmpLeaveID { get; set; }
    }

    public class TblEmpGetLeaveRequest
    {
        public int LoginEmpID;
        public int? Level;
        public int LoginCompanyID;
        public string LeaveStatus;
        public int WorkFlowTypeCode;
        public int RaisedForEmpID;
        public int RaisedByEmpID;
        public int? RequestPendingWithEmpId;
        public int? WfAuthorityEmpID;
        public string AuthorityEmpStatus;
        public string AuthorityApprovalStatus;
        public string SkippedByStatus;
        public int? SkippedByEmpId;
        public string FallingDateFrom;
        public string FallingDateTo;
        public string CreatedDateFrom;
        public string CreatedDateTo;
        public int StartRowNo;
        public int RowNo;
        public string ApplicationCode;
        public string SortColumns;
        public string SortDir;
        public string ReturnType;
        public string DivId;
        public string MdptId;
        public string DptId;
        public string BrnId;
        public string TeamId;
        public string RegionId;
        public string PgId;
        public string DsgId;
        public string JobId;
        public string TypeId;
        public string ReportTo;
        public string EmpCatgId;
        public string EmpStatus;
        public string FormId;
        public string LoginCulture;
    }

    public class Sp_Leave_GetRequest_Result1
    {
        public Nullable<int> EmpLeaveId { get; set; }
        public Nullable<int> WorkflowTypeId { get; set; }
        public Nullable<int> Id { get; set; }
        public Nullable<int> EmpId { get; set; }
        public string LCode { get; set; }
        public Nullable<int> AuthorityCheckPoint { get; set; }
        public string LeaveStatus { get; set; }
        public string LeaveType { get; set; }
        public string DtFr { get; set; }
        public string DtTo { get; set; }

        public string Date { get; set; }
        public string LeaveDays { get; set; }
        public string Remarks { get; set; }
        public string DivisionName { get; set; }
        public string MainDepartment { get; set; }
        public string Department { get; set; }
        public string Location { get; set; }
        public string Team { get; set; }
        public string Region { get; set; }
        public string PayrollGroup { get; set; }
        public string Designation { get; set; }
        public string JobGroup { get; set; }
        public string EmployeeType { get; set; }
        public string EmployeeCategory { get; set; }
        public string tblReportToName { get; set; }
        public string Active { get; set; }
        public string RaisedByEmp { get; set; }
        public string AuthorityStatus { get; set; }
        public string RaisedFor { get; set; }
        public string SortCol { get; set; }
        public Nullable<bool> Attachment { get; set; }
        public Nullable<System.DateTime> Request_CreatedOn { get; set; }
        public string PendingWith { get; set; }
        public Nullable<int> SkipBy { get; set; }
        public Nullable<bool> IsLFA { get; set; }
    }

    public class TblEmpLeave
    {
        public string loginculture;
        public string EmpId;
        public string DateFrom;
        public string DateTo;
        public string LeaveDays;
        public string Remarks;
        public bool DeductlPrev;
        public string leavedays_DHM;
        public bool Is_Portal;
        public bool Is_Workflow;
        public string attachedFileName;
        public string attachedFileExtension;
        public string ContentType;
        public string LCode;
        public byte[] Attachment;
        public string UserID;
        public string formID;
        public int UserEmpID;
        public string endterminal;
        public string endterminalIP;
        public int CompanyID;
        public int is_Mobile;
        public bool is_SMS;
        public bool is_LFA_Entry;
        public int is_Document_Attached;
        public string is_leaveadj_confirmed;
        public string Description;

    }

    public class TblEmpLeaveValidate
    {
        public int EmpId;
        public string LCode;
        public string DateFrom;
        public string DateTo;
        public string Reason;
        public bool AvailPrev;
        public string FormID;
        public string LangCode;
        public string AppCode;
        public int ApprovalStatus;
        public int CompID;
        public string LeaveDays;
        public int ValidEvent;
        public int ApproverEmpID;
        public string ApproverRemarks;
        public string EmpLeaveIds;
        public bool DisplayMessage;
        public bool is_Document_Attached;
        public bool is_leaveadj_confirmed;
        public bool is_LFA_Entry;
    }

    public class TblForwardToNextWithApprove
    {
        public string LoginCulture;
        public string LoginApp;
        public string Module;
        public int CompanyID;
        public string LoginEmpID;
        public string LoginUserID;
        public string TerminalId;
        public string EndTerminalIP;
        public int WorkFlowCode;
        public string EmpLeaveID;
        public string AuthorityRemarks;
        public string AuthorityStatus;
        public int TotalRequestCount;
        public string FormID;
        public bool IsApprovalViaEmail;
    }
    enum LeaveAvailType
    {
        FDHD,
        DHM
    };

    public class ValidateUserInput
    {
        public string leavetype { get; set; }
        public string remarks { get; set; }
        public string datefrom { get; set; }
        public string dateto { get; set; }
        public string LeaveDays { get; set; }
        public string FDHDDays { get; set; }
        public string DDHMDays { get; set; }
        public string result { get; set; }
        public string sandwitch { get; set; }
        public string DaysMsg { get; set; }

        public bool isLFAAllowed { get; set; }
    }
}
