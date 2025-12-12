using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Controllers.HCMS.ESS;
using System.Data;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class EmployeeInformation
    {        
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly DataServices _dataservice;

        public EmployeeInformation(IConfiguration configuration, Utilities utilities
            , ClientContextService clientContextService, DataServices dataservice)
        {
            _configuration = configuration;
            _utilities = utilities;
            _clientContextService = clientContextService;

            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        public void checkIsRoot(ref DataSet dsCheckIsRoot)
        {
            dsCheckIsRoot = DBHelper.ExecuteQueryReturnDS(CommandType.StoredProcedure, "Sp_EmployeeJobInformation_GetCheckedRootValue", null);


        }
        public List<ReportToEmployees> GetSubordinates(string EmpId)
        {
            
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            rptLst = GetHeirarchy(Convert.ToInt32(EmpId), 0);
            //ReportToEmployees tempObj = new ReportToEmployees();
            //tempObj.EMPID = "0";
            //tempObj.NAME = "Self";
            //rptLst.Insert(0, tempObj);
            return rptLst;
        }
        public List<ReportToEmployees> GetSubordinates(string EmpId, string Level)
        {
            
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            rptLst = GetHeirarchy(Convert.ToInt32(EmpId), Convert.ToInt32(Level));
            //ReportToEmployees tempObj = new ReportToEmployees();
            //tempObj.EMPID = "0";
            //tempObj.NAME = "Self";
            //rptLst.Insert(0, tempObj);
            return rptLst;
        }
        public List<ReportToEmployees> GetSubordinates(string EmpId, string Level, string Active)
        {
            
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            rptLst = GetHeirarchy(Convert.ToInt32(EmpId), Convert.ToInt32(Level), Active);

            return rptLst;
        }
        private List<ReportToEmployees> GetHeirarchy(int EmpId, int Level)
        {            
            DataSet dataset = new DataSet();
            string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string Self = "Self";
            Self = Self == "" ? "Self" : Self;
            string errorMessage = _dataservice.ExecuteReader("Select EmpId, NameWithShortCompany as Name from dbo.FN_ESS_GetSubOrdinatesList(" + Level + "," + EmpId + ",'-1','" + Self + "','" + culture + "', '1,2')", ref dataset);
            if (dataset != null && dataset.Tables.Count > 0)
            {
                try
                {
                    DataTable datatable = dataset.Tables[0];
                    DataView dvNA = datatable.DefaultView;
                    dvNA.RowFilter = "Name='" + Self + "'";
                    DataTable dtNA = dvNA.ToTable();
                    if (dtNA.Rows.Count > 0)
                    {
                        ReportToEmployees rptObj = new ReportToEmployees();
                        if (dtNA.Rows[0]["EmpId"] != DBNull.Value)
                            rptObj.EMPID = Convert.ToString(dtNA.Rows[0]["EmpId"]);
                        if (dtNA.Rows[0]["Name"] != DBNull.Value)
                            rptObj.NAME = dtNA.Rows[0]["Name"].ToString();
                        rptLst.Add(rptObj);
                    }
                    DataView dv = datatable.DefaultView;
                    dv.RowFilter = " Name<>'" + Self + "'";
                    DataTable dt = dv.ToTable();
                    foreach (DataRow datarow in dt.Rows)
                    {
                        ReportToEmployees rptObj = new ReportToEmployees();
                        if (datarow["EmpId"] != DBNull.Value)
                            rptObj.EMPID = Convert.ToString(datarow["EmpId"]);
                        if (datarow["Name"] != DBNull.Value)
                            rptObj.NAME = datarow["Name"].ToString();
                        rptLst.Add(rptObj);
                    }
                }
                catch (Exception ex)
                {

                }
            }
            return rptLst;
        }
        public DataTable GetHeirarchy(int EmpId, int Level, string Active, int a)
        {
            
            DataSet dataset = new DataSet();
            string Self = "Self";
            string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            Self = Self == "" ? "Self" : Self;
            DataTable dtReturn = new DataTable();
            DataTable dt1 = new DataTable();
            string errorMessage = _dataservice.ExecuteReader("Select EmpId,NameWithShortCompany as Name,Reportto,Level from dbo.FN_ESS_GetSubOrdinatesList(" + Level + "," + EmpId + ",'-1','" + Self + "','" + culture + "', '" + Active + "')", ref dataset);
            DataTable dtAll = new DataTable();

            dtAll.Columns.Add("EmpId", typeof(int));
            dtAll.Columns.Add("Name", typeof(string));
            dtAll.Columns.Add("Reportto", typeof(int));
            dtAll.Columns.Add("Level", typeof(int));

            if (dataset != null && dataset.Tables.Count > 0)
            {
                dt1 = dataset.Tables[0];

                DataRow dtAllRow = dtAll.NewRow();
                dtAllRow["EmpId"] = "-1";
                dtAllRow["Name"] = "All";
                dtAllRow["Reportto"] = "0";
                dtAllRow["Level"] = "0";
                dtAll.Rows.Add(dtAllRow);
                dtAll.TableName = "Table1";

                DataView dvNA = dt1.DefaultView;
                dvNA.RowFilter = "Name='Self'";
                DataTable dtNA = dvNA.ToTable();

                DataView dv = dt1.DefaultView;
                dv.RowFilter = " Name<>'Self'";
                dv.Sort = "Name Asc";
                DataTable dt = dv.ToTable(true);

                if (Level != 0)
                {
                    dtAll.Merge(dtNA);
                    dtAll.Merge(dt);
                }
                else
                    dtAll.Merge(dtNA);
                dtReturn = dtAll;

            }
            return dtReturn;
        }
        public List<ReportToEmployees> GetHeirarchy(int EmpId, int Level, string Active)
        {
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            DataTable datatable = GetHeirarchy(EmpId, Level, Active, 0);

            if (datatable != null && datatable.Rows.Count > 0)
            {
                if (Level != 0)
                {
                    DataView dvAll = datatable.DefaultView;
                    dvAll.RowFilter = "Name='All'";
                    DataTable dtAll = dvAll.ToTable();

                    if (dtAll.Rows.Count > 0)
                    {
                        ReportToEmployees rptObj = new ReportToEmployees();
                        if (dtAll.Rows[0]["EmpId"] != DBNull.Value)
                            rptObj.EMPID = Convert.ToString(dtAll.Rows[0]["EmpId"]);
                        if (dtAll.Rows[0]["Name"] != DBNull.Value)
                            rptObj.NAME = dtAll.Rows[0]["Name"].ToString();
                        rptLst.Add(rptObj);
                    }
                }
                DataView dvNA = datatable.DefaultView;
                dvNA.RowFilter = "Name='Self'";
                DataTable dtNA = dvNA.ToTable();
                if (dtNA.Rows.Count > 0)
                {
                    ReportToEmployees rptObj = new ReportToEmployees();
                    if (dtNA.Rows[0]["EmpId"] != DBNull.Value)
                        rptObj.EMPID = Convert.ToString(dtNA.Rows[0]["EmpId"]);
                    if (dtNA.Rows[0]["Name"] != DBNull.Value)
                        rptObj.NAME = dtNA.Rows[0]["Name"].ToString();
                    rptLst.Add(rptObj);
                }
                DataView dv = datatable.DefaultView;
                dv.RowFilter = " Name<>'Self'";
                DataTable dt = dv.ToTable();
                foreach (DataRow datarow in dt.Rows)
                {
                    if (datarow["Name"] != "All")
                    {
                        ReportToEmployees rptObj = new ReportToEmployees();
                        if (datarow["EmpId"] != DBNull.Value)
                            rptObj.EMPID = Convert.ToString(datarow["EmpId"]);
                        if (datarow["Name"] != DBNull.Value)
                            rptObj.NAME = datarow["Name"].ToString();
                        rptLst.Add(rptObj);
                    }
                }


            }
            return rptLst;
        }
        public List<ReportToEmployees> GetUserLevels(string EmpId)
        {
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            rptLst = GetUserLevels(EmpId, "-1");
            return rptLst;
        }

        /// <summary>
        /// Get subordinate level with respect to Emp [Active] Status
        /// </summary>
        /// <param name="EmpId"></param>
        /// <param name="Active"></param>
        /// <returns></returns>

        public List<ReportToEmployees> GetUserLevels(string EmpId, string Active)
        {
            

            DataTable dt = new DataTable();
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();

            string Upto = string.Empty;
            Upto = "Upto";
            Upto = Upto == "" ? "Up to" : Upto;

            dt = GetUserLevelsData(EmpId, Active);

            foreach (DataRow dRow in dt.Rows)
            {
                ReportToEmployees rptObj = new ReportToEmployees();

                if (dRow["Level"].ToString().Trim() == "0")
                    rptObj.NAME = dRow["Level"] == null ? "" : dRow["Level"].ToString();
                else
                    rptObj.NAME = dRow["Level"] == null ? "" : Upto + " " + dRow["Level"].ToString();
                rptObj.Level = dRow["Level"] == null ? "" : dRow["Level"].ToString();
                rptLst.Add(rptObj);
            }
            // HttpContext.Current.Session["Employee_Subordinate_Levels"] = dt;

            return rptLst;
        }

        public DataTable GetUserLevelsData(string EmpId, string Active)
        {
            

            DataTable dtReturn = null;
            DataSet dataset = new DataSet();

            if (Active.Trim().Equals("-1") || Active.Trim().Length == 0)
                Active = "0,1,2";
            else if (Active.Trim().Equals("1"))
                Active = "1,2";
            else if (Active.Trim().Equals("0"))
                Active = "0";

            string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string query = "Select distinct [Level] from dbo.FN_ESS_GetSubOrdinatesList(999999,'" + EmpId + "','-1','Self','" + culture + "','" + Active + "')";
            string errorMessage = _dataservice.ExecuteReader(query, ref dataset);
            if (dataset != null && dataset.Tables.Count > 0)
                dtReturn = dataset.Tables[0];
            return dtReturn;
        }

        public List<ReportToEmployees> GetUserLevelsforRosterPlan(string EmpId)
        {
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            rptLst = GetUserLevels(EmpId);
            return rptLst;
        }
        public List<ReportToEmployees> GetSubordinatesforRosterPlanworkflow(string EmpId, string Level)
        {
            
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            rptLst = GetHeirarchyforRosterPlanworkflow(Convert.ToInt32(EmpId), Convert.ToInt32(Level));
            return rptLst;
        }
        private List<ReportToEmployees> GetHeirarchyforRosterPlanworkflow(int EmpId, int Level)
        {
            
            DataSet dataset = new DataSet();
            List<ReportToEmployees> rptLst = new List<ReportToEmployees>();
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string errorMessage = _dataservice.ExecuteReader("EXEC Employee_Subordinates_Upto_Fixed_Level " + EmpId + "," + Level + "," + CompanyId + " , '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' ", ref dataset);
            if (dataset != null && dataset.Tables.Count > 0)
            {

                DataTable datatable = dataset.Tables[0];
                //DataView dvNA = datatable.DefaultView;
                foreach (DataRow dr in dataset.Tables[0].Rows)
                {
                    if (dr["Level"].ToString() == "0")
                    {
                        dr.Delete();
                        dr.AcceptChanges();
                        break;
                    }
                }

                foreach (DataRow datarow in datatable.Rows)
                {
                    ReportToEmployees rptObj = new ReportToEmployees();
                    if (datarow["EmpId"] != DBNull.Value)
                        rptObj.EMPID = Convert.ToString(datarow["EmpId"]);
                    if (datarow["Name"] != DBNull.Value)
                        rptObj.NAME = datarow["Name"].ToString();
                    rptLst.Add(rptObj);
                }

            }
            return rptLst;
        }

        private bool CheckSubOrdinate(int EmpId)
        {
            int count = Convert.ToInt32(_utilities.GetScalarData("Count(EmpId)", "TblEmployee", "Active<>0 and ReportTo=" + EmpId));
            if (count > 0)
                return true;
            else
            {

            }
            return false;
        }

        public List<EmployeeInformationList> GetEmpInfo(string EmpId)
        {
            
             
            string _culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string imagename = _utilities.GetScalarData("Select PhotoPath from tblEmployee where EmpId = '" + EmpId + "'").ToString();
            DataSet dataset = new DataSet();
            // changed by FS on 05/05/2016
            //Changed by Hasan Ayub on 03/01/2017
            string errorMessage = _dataservice.GetDataWithClause("a.EmpID, isnull(E.FName,'') as FName, a.EmpCode, dbo.fn_General_GetEmployeeName(" + EmpId + ", (select CompanyId from TblEmployee where EmpId=" + EmpId + "), 1, 0,0, 'en-GB') as Name, " +
                                                         " isnull(a.MainDepartment,'') as MainDepartment, isnull(a.Department,'')as Department,FORMAT(a.DateJoin, 'dd/MMM/yyyy') As DateJoin, " +
                                                         " isnull(E.Email,'') as Email, isnull(a.Designation,'') as Designation, " +
                                                         " '' As dReportToCompny," +
                                                         " '' As iDReportToCompny ," +
                                                         " dbo.fn_General_GetEmployeeName(e.ReportTo ,'" + CompanyId + "',1,1,1, '" + _culture + "') As ReportingPerson, " +
                                                         " dbo.fn_General_GetEmployeeName(e.Dotted ,'" + CompanyId + "',1,1,1, '" + _culture + "')As DottedPerson, " +
                                                         " E.Shift, (Select Case when isnull(Description,'') = '' then '(' +TimeIn + ' - ' + TimeOut + ')' else '(' + TimeIn + ' - ' + TimeOut + ')' end as Description from tblRosterShift where CompanyId = (SELECT CompanyId from TblEmployee where EmpId=" + EmpId + ") AND Code = E.Shift) as ShiftDescript, " +
                                                         " isnull((Select CompanyName from vCompanyManagement where ccode = a.Companyid)  , '') As CompanyName," +
                                                         " DivisionName,Location,JobGroup,PayrollGroup,EmployeeType,FORMAT(a.DateConfirm, 'dd/MMM/yyyy') As DateConfirm,FORMAT(a.CDueDate,'dd/MMM/yyyy') As DateConfirmDue, FORMAT(E.ContractExpireDate,'dd/MMM/yyyy') As ContractExpiryDate," +
                                                         "FORMAT(E.InternExpiryDate,'dd/MMM/yyyy') As InternExpiryDate, FORMAT(a.ExtDate,'dd/MMM/yyyy') As ProbhationExtDate, " +
                                                         "E.IdCardRemarks,E.FamilyCardNo,e.IqamaNo,I. Name as IqamaProfession," +
                                                         " Convert(char,E.IqamaExpiryHijri,103)IqamaExpiryHijri,Convert(char,E.IqamaExpiryGregorian,103)IqamaExpiryGregorian,CurrSpnsName," +
                                                         " case when isnull(SpnsTransferable,0)=0 Then 'N/A' when SpnsTransferable= 1 then 'Yes' else 'No' End SpnsTransferable , " +
                                                         " S.Name As SpnsType,cnt.Name as SpnsCountry,cty.Name as SpnsCity ,SpnsContactDetails,SpnsNatureOfBusiness," +
                                                         " Convert(char,E.SpnsExpiryHijri,103)SpnsExpiryHijri,Convert(char,E.SpnsExpiryGregorian,103)SpnsExpiryGregorian,EC.Name as EmployeeCategory ", " " +
                                                         " fn_Lookup_EmployeeProfile('" + _culture + "'," + "(select CompanyId from TblEmployee where EmpId=" + EmpId + ")" + ") as a " +
                                                         " inner join tblEmployee E on E.EmpId =a.EmpId " +
                                                         " left outer join vwtblSetupsDetail I on E.IqamaProfession=I.sdlid and I.Culture = '" + _culture + "' " +
                                                         " left join vwtblSetupsDetail S on E.SpnsType=S.sdlid  and S.Culture = '" + _culture + "' " +
                                                         " left join tblCountry Cnt on E.SpnsCountry=Cnt.cntid " +
                                                         " left join tblCity  Cty on E.SpnsCity =cty.ctyid  left join vwtblSetupsDetail EC on EC.sdlid =E.empCategoryid  and EC.Culture = '" + _culture + "'   ", "a.EmpId = " + EmpId, ref dataset);

            List<EmployeeInformationList> empInfoLst = new List<EmployeeInformationList>();

            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    EmployeeInformationList empObj = new EmployeeInformationList();
                    empObj.FName = datarow["FName"] == DBNull.Value ? "" : datarow["FName"].ToString();
                    empObj.EMPCODE = datarow["EmpCode"] == DBNull.Value ? "" : datarow["EmpCode"].ToString();
                    empObj.EMPNAME = datarow["Name"] == DBNull.Value ? "" : datarow["Name"].ToString();
                    empObj.EMPMAINDEPT = datarow["MainDepartment"] == DBNull.Value ? "" : datarow["MainDepartment"].ToString();
                    empObj.EMPDEPT = datarow["Department"] == DBNull.Value ? "" : datarow["Department"].ToString();
                    //empObj.EMPDATEJOIN = datarow["DateJoin"].ToString().Length<=0 ? "" : Convert.ToDateTime(datarow["DateJoin"].ToString().Trim()).ToShortDateString();
                    empObj.EMPDATEJOIN = datarow["DateJoin"].ToString().Length <= 0 ? "" : datarow["DateJoin"].ToString().Trim();
                    empObj.EMPMAIL = datarow["Email"] == DBNull.Value ? "" : datarow["Email"].ToString();
                    empObj.EMPDESIGNATION = datarow["Designation"] == DBNull.Value ? "" : datarow["Designation"].ToString();
                    //empObj.EMPPIC = datarow["EmpPic"] == null ? null : (byte[])datarow["EmpPic"];
                    empObj.EMPPIC = _utilities.GetCompletePathForReading(_utilities.GetPath(CompanyId, "Personnel", "EmployeeProfileBasic", 1, false) + imagename, 1);
                    empObj.DReportToCompny = datarow["dReportToCompny"] == DBNull.Value ? "" : datarow["dReportToCompny"].ToString();
                    empObj.IDReportToCompny = datarow["iDReportToCompny"] == DBNull.Value ? "" : datarow["iDReportToCompny"].ToString();
                    empObj.REPORTINGPERSON = datarow["ReportingPerson"] == DBNull.Value ? "" : datarow["ReportingPerson"].ToString();
                    empObj.DOTTEDPERSON = datarow["DottedPerson"] == DBNull.Value ? "" : datarow["DottedPerson"].ToString();
                    empObj.SHIFT = datarow["Shift"] == DBNull.Value ? "" : datarow["Shift"].ToString().Trim() + " " + datarow["ShiftDescript"].ToString().Trim();
                    empObj.CompanyName = datarow["CompanyName"] == DBNull.Value ? "" : datarow["CompanyName"].ToString();
                    empObj.Division = datarow["DivisionName"] == DBNull.Value ? "" : datarow["DivisionName"].ToString();
                    empObj.Location = datarow["Location"] == DBNull.Value ? "" : datarow["Location"].ToString();
                    empObj.Grade = datarow["JobGroup"] == DBNull.Value ? "" : datarow["JobGroup"].ToString();
                    empObj.PayrollGroup = datarow["PayrollGroup"] == DBNull.Value ? "" : datarow["PayrollGroup"].ToString();
                    empObj.EmployeeType = datarow["EmployeeType"] == DBNull.Value ? "" : datarow["EmployeeType"].ToString();
                    //empObj.DateConfirm = datarow["DateConfirm"].ToString().Length <= 0 ? "" : Convert.ToDateTime(datarow["DateConfirm"].ToString().Trim()).ToShortDateString();
                    empObj.DateConfirm = datarow["DateConfirm"].ToString().Length <= 0 ? "" : datarow["DateConfirm"].ToString().Trim();
                    empObj.DateConfirmDue = datarow["DateConfirmDue"].ToString().Length <= 0 ? "" : datarow["DateConfirmDue"].ToString().Trim();
                    empObj.ContractExpiryDate = datarow["ContractExpiryDate"].ToString().Length <= 0 ? "" : datarow["ContractExpiryDate"].ToString().Trim();
                    empObj.InternExpiryDate = datarow["InternExpiryDate"].ToString().Length <= 0 ? "" : datarow["InternExpiryDate"].ToString().Trim();
                    empObj.ProbhationExtDate = datarow["ProbhationExtDate"].ToString().Length <= 0 ? "" : datarow["ProbhationExtDate"].ToString().Trim();
                    empObj.IDCardRemarks = datarow["IdCardRemarks"] == DBNull.Value ? "" : datarow["IdCardRemarks"].ToString();
                    empObj.FamilyCardNo = datarow["FamilyCardNo"] == DBNull.Value ? "" : datarow["FamilyCardNo"].ToString();

                    empObj.IqamaNo = datarow["FamilyCardNo"] == DBNull.Value ? "" : datarow["IqamaNo"].ToString();
                    empObj.IqamaProfession = datarow["IqamaProfession"] == DBNull.Value ? "" : datarow["IqamaProfession"].ToString();
                    empObj.IqamaExpiryHijri = datarow["IqamaExpiryHijri"] == DBNull.Value ? "" : datarow["IqamaExpiryHijri"].ToString();
                    empObj.IqamaExpiryGregorian = datarow["IqamaExpiryGregorian"] == DBNull.Value ? "" : datarow["IqamaExpiryGregorian"].ToString();

                    empObj.CurrSpnsName = datarow["CurrSpnsName"] == DBNull.Value ? "" : datarow["CurrSpnsName"].ToString();
                    empObj.SpnsTransferable = datarow["SpnsTransferable"] == DBNull.Value ? "" : datarow["SpnsTransferable"].ToString();
                    empObj.SpnsType = datarow["SpnsType"] == DBNull.Value ? "" : datarow["SpnsType"].ToString();
                    empObj.SpnsCountry = datarow["SpnsCountry"] == DBNull.Value ? "" : datarow["SpnsCountry"].ToString();
                    empObj.SpnsCity = datarow["SpnsCity"] == DBNull.Value ? "" : datarow["SpnsCity"].ToString();
                    empObj.SpnsContactDetails = datarow["SpnsContactDetails"] == DBNull.Value ? "" : datarow["SpnsContactDetails"].ToString();
                    empObj.SpnsNatureOfBusiness = datarow["SpnsNatureOfBusiness"] == DBNull.Value ? "" : datarow["SpnsNatureOfBusiness"].ToString();
                    empObj.SpnsExpiryHijri = datarow["SpnsExpiryHijri"] == DBNull.Value ? "" : datarow["SpnsExpiryHijri"].ToString();
                    empObj.SpnsExpiryGregorian = datarow["SpnsExpiryGregorian"] == DBNull.Value ? "" : datarow["SpnsExpiryGregorian"].ToString();
                    empObj.EmployeeCategory = datarow["EmployeeCategory"] == DBNull.Value ? "" : datarow["EmployeeCategory"].ToString();

                    empInfoLst.Add(empObj);
                }
            return empInfoLst;
        }

        /// <summary>
        /// Get EmpCode by EmpID
        /// </summary>
        /// <param name="EmpId"></param>
        /// <returns></returns>
        public string GetEmpCode(string EmpId)
        {
            

            string empCode = "";

            DataSet dataset = new DataSet();

            string errorMessage = _dataservice.GetDataWithClause("EmpCode", "fn_Lookup_EmployeeProfile('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'," + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ")", "EmpId = " + EmpId, ref dataset);

            List<EmployeeInformationList> empInfoLst = new List<EmployeeInformationList>();

            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    empCode = datarow["EmpCode"] == DBNull.Value ? "" : datarow["EmpCode"].ToString();
                }
            return empCode;
        }

        /// <summary>
        /// Get EmpName by EmpID
        /// </summary>
        /// <param name="EmpId"></param>
        /// <returns></returns>
        public string GetEmpName(string EmpId)
        {
            

            string empName = "";

            DataSet dataset = new DataSet();

            string errorMessage = _dataservice.GetDataWithClause("Name", "fn_Lookup_EmployeeProfile('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'," + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ")", "EmpId = " + EmpId, ref dataset);

            List<EmployeeInformationList> empInfoLst = new List<EmployeeInformationList>();

            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    empName = datarow["Name"] == DBNull.Value ? "" : datarow["Name"].ToString();
                }
            return empName;
        }

        public List<HRMessages> GetHRMessages(string EmpId, string SortExpression, string SortDirection)
        {
            
            DataSet dataset = new DataSet();
            string whereClause = "a.EmpId = " + EmpId + " AND YEAR(a.MDate) = YEAR(dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ")) AND a.Active=1";

            if (SortExpression != null && SortExpression != "" && SortDirection != null && SortDirection != "")
                whereClause += " Order By " + SortExpression + " " + SortDirection;

            string errorMessage = _dataservice.GetDataWithClause("(SELECT COUNT(*) + 1 " +
                                                                "FROM tblMFHr b WHERE b.EmpId = " + EmpId + " and " +
                                                                "b.MFHRId < a.MFHRId and " +
                                                                "YEAR(b.MDate) = YEAR(dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + "))) AS SrNo, " +
                                                                "isnull(a.MFHRId,0) as MFHRId,FORMAT(a.MDate, 'dd/MMM/yyyy') As MDate, " +
                                                                "SUBSTRING(a.HrMsg,1,10) AS HrMsg, isnull(c.Name,'') as Name "
                                                                , "tblMFHr a INNER JOIN dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") c ON c.EmpId = a.HRId", whereClause, ref dataset);
            List<HRMessages> hrLst = new List<HRMessages>();
            if (dataset != null)
            {
                if (dataset.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow datarow in dataset.Tables[0].Rows)
                    {
                        HRMessages hrObj = new HRMessages();
                        hrObj.MFHRID = datarow["MFHRId"] == null ? 0 : Convert.ToInt32(datarow["MFHRId"].ToString());
                        hrObj.SRNO = datarow["SrNo"] == null ? "" : datarow["SrNo"].ToString();
                        hrObj.HRID = datarow["Name"] == null ? "" : datarow["Name"].ToString();
                        hrObj.HRMSG = datarow["HrMsg"] == null ? "" : datarow["HrMsg"].ToString().Replace("''", "'").ToString();
                        hrObj.MDATE = datarow["MDate"] == null ? "" : datarow["MDate"].ToString();
                        hrLst.Add(hrObj);
                    }
                    return hrLst;
                }
                else
                    return hrLst;
            }
            else
                return hrLst;
        }

        public List<ExpiryDates> GetExpiryDate(string EmpId, string SortExpression, string SortDirection)
        {
            
            DataSet dataset = new DataSet();
            string whereClause = "(a.EmpId = " + EmpId + ")";

            if (SortExpression != null && SortExpression != "" && SortDirection != null && SortDirection != "")
                whereClause += " Order By " + SortExpression + " " + SortDirection;
            string errorMessage = _dataservice.GetDataWithClause("(Select Count(*)+1 " +
                                                                "From tblEmpLicenceInfo x " +
                                                                "Where x.EmpLicId < a.EmpLicId And (x.EmpId = " + EmpId + ") " +
                                                                " ) as SrNo, " +

                                                                "b.[Name], FORMAT(a.ExpiryDate, 'dd/MMM/yyyy') as Date,a.Remarks", "tblEmpLicenceInfo a Inner Join vwtblSetupsDetail b on b.sdlId = a.LicenceId  and b.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' ", whereClause, ref dataset);
            List<ExpiryDates> expLst = new List<ExpiryDates>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    ExpiryDates expObj = new ExpiryDates();
                    expObj.SRNO = datarow["SrNo"] == null ? "" : datarow["SrNo"].ToString();
                    expObj.NAME = datarow["Name"] == null ? "" : datarow["Name"].ToString();
                    expObj.DATE = datarow["Date"] == null ? "" : datarow["Date"].ToString();
                    expObj.REMARKS = datarow["REMARKS"] == null ? "" : datarow["REMARKS"].ToString();
                    expLst.Add(expObj);
                }
            return expLst;
        }

        public List<EmpJobDescription> GetEmpJobDescription(string EmpId)
        {
            

            DataSet dataset = new DataSet();
            string whereClause = "a.Empid = " + EmpId;
            string errorMessage = _dataservice.GetDataWithClause(" Top 1 isnull(a.JobSummary,'')as JobSummary,isnull(a.Responsibilities,'') as Responsibilities,isnull(a.EOWorkCondition,'') as EOWorkCondition, isnull((Select Name from dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") where empid = b.ReportTo), '')As ReportingPerson, isnull((Select Name from dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") where empid = b.Dotted), '')As DottedPerson ",
                                                                "tblEmpJobProfile a Inner Join dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") b On b.EmpId = a.EmpId ", whereClause, ref dataset);

            List<EmpJobDescription> empJLst = new List<EmpJobDescription>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    EmpJobDescription empJObj = new EmpJobDescription();
                    empJObj.JOBSUMMARY = datarow["JobSummary"] == null ? "" : datarow["JobSummary"].ToString();
                    empJObj.RESPONSIBILITIES = datarow["Responsibilities"] == null ? "" : datarow["Responsibilities"].ToString();
                    empJObj.EOWORKCONDITION = datarow["EOWorkCondition"] == null ? "" : datarow["EOWorkCondition"].ToString();
                    //empJObj.REPORTINGPERSON = datarow["ReportingPerson"] == null ? "" : datarow["ReportingPerson"].ToString();
                    //empJObj.DOTTEDPERSON = datarow["DottedPerson"] == null ? "" : datarow["DottedPerson"].ToString();
                    empJLst.Add(empJObj);
                }
            return empJLst;
        }

        public List<EmpJobDescription> GetEmpJobDescription(string _EmpId, string _JobProfileId)
        {
            

            DataSet dataset = new DataSet();
            string whereClause = " a.JobProfileId = " + _JobProfileId + " and a.EmpId = " + _EmpId;
            string errorMessage = _dataservice.GetDataWithClause(" isnull(a.JobSummary,'') as JobSummary, isnull(a.Responsibilities,'') as Responsibilities, isnull(a.EOWorkCondition,'') as EOWorkCondition, " +
                                                                " isnull((Select Name from dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") where empid = b.ReportTo), '')As ReportingPerson, " +
                                                                " isnull((Select Name from dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") where empid = b.Dotted)  , '')As DottedPerson, " +
                                                                " isnull(a.accountabilities,'') as accountabilities, isnull(a.KeyPerformanceIndicators,'') as KeyPerformanceIndicators,a.AppDoc ",
                                                                " tblEmpJobProfile a Inner Join dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") b On b.EmpId = a.EmpId ", whereClause, ref dataset);

            List<EmpJobDescription> empJLst = new List<EmpJobDescription>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    EmpJobDescription empJObj = new EmpJobDescription();
                    empJObj.JOBSUMMARY = datarow["JobSummary"] == null ? "" : datarow["JobSummary"].ToString();
                    empJObj.RESPONSIBILITIES = datarow["Responsibilities"] == null ? "" : datarow["Responsibilities"].ToString();
                    empJObj.EOWORKCONDITION = datarow["EOWorkCondition"] == null ? "" : datarow["EOWorkCondition"].ToString();
                    //empJObj.REPORTINGPERSON = datarow["ReportingPerson"] == null ? "" : datarow["ReportingPerson"].ToString();
                    //empJObj.DOTTEDPERSON = datarow["DottedPerson"] == null ? "" : datarow["DottedPerson"].ToString();
                    empJObj.ACCOUNTIBILITIES = datarow["accountabilities"] == null ? "" : datarow["accountabilities"].ToString();
                    empJObj.KPIS = datarow["KeyPerformanceIndicators"] == null ? "" : datarow["KeyPerformanceIndicators"].ToString();
                    empJObj.Cv = datarow["AppDoc"] == DBNull.Value ? null : (byte[])datarow["AppDoc"];

                    empJLst.Add(empJObj);
                }
            return empJLst;
        }

        public List<CalendarInfo> GetCalendarInfo(string EmpId, string SortExpression, string SortDirection)
        {
            

            DataSet dataset = new DataSet();
            string whereClause = "(Year(a.DateFrom)=Year(dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + "))) And a.CompanyId = " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + " and HolId not in (select HolId from tblHoliday where rlgId is not null and rlgid<>-1 and rlgId not in (select rlgId from tblEmployee where Empid=" + EmpId + ") ) ";
            if (SortExpression != null && SortExpression != "" && SortDirection != null && SortDirection != "")
                whereClause += " Order By " + SortExpression + " " + SortDirection;
            else
                whereClause += " Order By DateFrom, DateTo ";
            string errorMessage = _dataservice.GetDataWithClause("(Select Count(*) + 1 " +
                                                                " From tblHoliday b " +
                                                                "Where (b.HolId < a.HolId) And (Year(b.DateFrom)=Year(dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + "))) And " +
                                                                "a.CompanyId = b.CompanyId) AS SrNo, isnull(Description,'') as Description, Case when Flag = 1 then 'National' else 'Festival' end as HolidayType,FORMAT(a.DateFrom, 'dd/MMM/yyyy') As dateFr, " +
                                                                "FORMAT(a.DateTo, 'dd/MMM/yyyy') As DateTo", "tblHoliday a", whereClause, ref dataset);
            List<CalendarInfo> clndrLst = new List<CalendarInfo>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    CalendarInfo clndrObj = new CalendarInfo();
                    clndrObj.SRNO = datarow["SrNo"] == null ? "" : datarow["SrNo"].ToString();
                    clndrObj.DESCRIPTION = datarow["Description"] == null ? "" : datarow["Description"].ToString();
                    clndrObj.HOLIDAYTYPE = datarow["HolidayType"] == null ? "" : datarow["HolidayType"].ToString();
                    clndrObj.DATEFROM = datarow["DateFr"] == null ? "" : datarow["DateFr"].ToString();
                    clndrObj.DATETO = datarow["DateTo"] == null ? "" : datarow["DateTo"].ToString();
                    clndrLst.Add(clndrObj);
                }
            return clndrLst;
        }
        public List<LeaveInfo> GetLeaveInfoFDHD(string EmpId, string SortExpression, string SortDirection)
        {
            if (EmpId == null)
                return null;
            bool isAnyLeaveTypeCarryForward = false;
            bool blnAvailLeavesFromNextLeaveYearAllowed = false;
            DataSet dataset = new DataSet();
            string strCompanyId = Convert.ToString(_utilities.GetScalarData("CompanyId", "TblEmployee", "EmpId=" + EmpId));//_utilities.GetCompanyId();

            string OrderByClause = "";
            if (SortExpression != null && SortExpression != "" && SortDirection != null && SortDirection != "")
                OrderByClause += " Order By " + SortExpression + " " + SortDirection;
            else
                OrderByClause = "Order By Balance ASC";

            string strQuery = "Select * from fn_Leave_GetLeaveBalanceData(" + EmpId + " , '1') " + OrderByClause + "";

            string errorMessage = _dataservice.ExecuteReader(strQuery, ref dataset);

            List<LeaveInfo> levLst = new List<LeaveInfo>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    LeaveInfo levObj = new LeaveInfo();
                    levObj.DESCRIPTION = datarow["Description"] == null || datarow["Description"] == DBNull.Value ? "" : datarow["Description"].ToString();
                    levObj.LCode = datarow["LCode"] == null || datarow["LCode"] == DBNull.Value ? "" : datarow["LCode"].ToString();
                    levObj.MAXALLOWED = datarow["MaxAllowed"] == null || datarow["MaxAllowed"] == DBNull.Value ? "" : datarow["MaxAllowed"].ToString();
                    levObj.AVAILED = datarow["Availed"] == null || datarow["Availed"] == DBNull.Value ? "" : datarow["Availed"].ToString();
                    levObj.BALANCE = datarow["Balance"] == null || datarow["Balance"] == DBNull.Value ? "" : datarow["Balance"].ToString();
                    levObj.PREVBALANCE = datarow["PrevBalance"] == null || datarow["PrevBalance"] == DBNull.Value ? "" : datarow["PrevBalance"].ToString();
                    levObj.CURRBALANCE = datarow["CurrBalance"] == null || datarow["CurrBalance"] == DBNull.Value ? "" : datarow["CurrBalance"].ToString();
                    levObj.PENDINGLEAVES = datarow["PendingLeaves"] == null || datarow["PendingLeaves"] == DBNull.Value ? "" : datarow["PendingLeaves"].ToString();
                    levObj.FISCALYEAR = datarow["FiscalYear"] == null || datarow["FiscalYear"] == DBNull.Value ? "" : datarow["FiscalYear"].ToString();
                    levObj.IsGradeWiseLeaveEntitlementDifferent = datarow["IsGradeWiseLeaveEntitlementDifferent"] == null || datarow["IsGradeWiseLeaveEntitlementDifferent"] == DBNull.Value ? "" : datarow["IsGradeWiseLeaveEntitlementDifferent"].ToString();
                    levObj.AVAILED_NEXTYEAR = datarow["Availed_Next"].ToString();
                    levObj.PENDINGLEAVES_NEXTYEAR = datarow["PendingLeaves_Next"].ToString();
                    levObj.PenaltiesDays = datarow["PenaltyDaysDeduction"].ToString();

                    if (isAnyLeaveTypeCarryForward == false && Convert.ToBoolean(datarow["CarryForward"]) == true)
                        isAnyLeaveTypeCarryForward = true;

                    if (blnAvailLeavesFromNextLeaveYearAllowed == false && Convert.ToBoolean(datarow["isAvailLeavesFromNextLeaveYearAllowed"]) == true)
                        blnAvailLeavesFromNextLeaveYearAllowed = true;


                    levLst.Add(levObj);
                }

            //    HttpContext.Current.Session["ESS_LeaveEntry_IsAnyLeaveTypeCarryForward"] = isAnyLeaveTypeCarryForward;
            //  HttpContext.Current.Session["ESS_ShowNextLeaveYearColumn"] = blnAvailLeavesFromNextLeaveYearAllowed;

            return levLst;
        }
        public DataTable GetTrainingInfo(string EmpId, string SortExpression, string SortDirection)
        {
            try
            {                
                DataSet dataset = new DataSet();
                string whereClause = "a.empid= " + EmpId + " and ISNULL(isCompleted,0)=0 " +
                                     "and a.todate>=dbo.fn_General_GetLocalDateTimeCompanyWise(a.companyid) Order By " + SortExpression + " " + SortDirection;

                string errorMessage = _dataservice.GetDataWithClause("(Select Count(*) From tblTrainingRecord x " +
                                                                    "Where x.TPId < a.TPId And " +
                                                                    "ISNULL(x.isCompleted,0)=0 And x.ToDate>=dbo.fn_General_GetLocalDateTimeCompanyWise(x.companyid)) AS SrNo, " +
                                                                    "isnull(b.Name,'') as InstituteName, isnull(c.course,'') as Course,FORMAT(a.FromDate, 'dd/MMM/yyyy') As FromDate, " +
                                                                    "FORMAT(a.Todate, 'dd/MMM/yyyy') As DateTo,isnull(a.IsNominatedByDept,0) as IsNominatedByDept,isnull(a.IsConfirm,0) as IsConfirm", "tblTrainingRecord a " +
                                                                    "inner join tblTrainingInstitutes b on a.Instituteid=b.Instituteid and b.InstituteType not in (select sdlid from tblSetupsDetail where smsid=128 and cast(code as int)=3) " +
                                                                    "inner join tblTrainingCourses c on a.CourseID=c.CourseID ", whereClause, ref dataset);

                return dataset.Tables[0];
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public List<HRMessagesRead> GetMessages(int mfhrId)
        {
            
            DataSet dataset = new DataSet();
            string whereClause = "a.MFHrId = " + mfhrId.ToString() + " And a.Active = 1 And a.CompanyId = " + _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string errorMessage = _dataservice.GetDataWithClause("isnull(b.Name,'') as Name,FORMAT(a.MDate, 'dd/MMM/yyyy') As MDate,isnull(a.HrMsg,'') as HrMsg "
                                                              , "tblMFHR a INNER JOIN dbo.fn_Employee('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") b ON b.EmpId = a.HRId", whereClause, ref dataset);
            List<HRMessagesRead> hrReadLst = new List<HRMessagesRead>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    HRMessagesRead hrReadObj = new HRMessagesRead();
                    hrReadObj.HRID = datarow["Name"] == null ? "" : datarow["Name"].ToString();
                    hrReadObj.DATE = datarow["MDate"] == null ? "" : datarow["MDate"].ToString();
                    hrReadObj.MSG = datarow["HrMsg"] == null ? "" : datarow["HrMsg"].ToString();
                    hrReadLst.Add(hrReadObj);
                }
            return hrReadLst;
        }

        public List<EmpJobProfile> GetEmpJobProfiles(int _EmpId, string SortExpression, string SortDirection)
        {
            DataSet dataset = new DataSet();
            
            string whereClause = " a.EmpId = " + _EmpId + "";


            if (SortExpression != null && SortExpression != "" && SortDirection != null && SortDirection != "")
                whereClause += " Order By " + SortExpression + " " + SortDirection;

            string errorMessage = _dataservice.GetDataWithClause(" JobProfileId,isnull(JobCode,'') as JobCode, isnull(JobTitle,'') as JobTitle ", " TblEmpJobProfile a ", whereClause, ref dataset);
            List<EmpJobProfile> lstJobProfile = new List<EmpJobProfile>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    EmpJobProfile objJobProfile = new EmpJobProfile();
                    objJobProfile.JobProfileId = (datarow["JobProfileId"] == null ? -1 : Convert.ToInt32(datarow["JobProfileId"]));
                    objJobProfile.JobCode = (datarow["JobCode"] == null ? "" : datarow["JobCode"].ToString());
                    objJobProfile.JobTitle = (datarow["JobTitle"] == null ? "" : datarow["JobTitle"].ToString());
                    lstJobProfile.Add(objJobProfile);
                }
            return lstJobProfile;
        }
        public List<EmpMeeting> GetJobProfileMeetings(int _JobProfileId)
        {
            

            DataSet dataset = new DataSet();
            string whereClause = " a.JobProfileId = " + _JobProfileId + " And a.CompanyId = " + _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string errorMessage = _dataservice.GetDataWithClause(" type.Name as MeetingType, criteria.Name as MeetingCriteria, Frequency ",
                                                                " tblEmpJobProfileMeetings a " +
                                                                " Inner join vwTblSetupsDetail as type on a.MeetingType = type.sdlid  and type.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                                " Inner join vwTblSetupsDetail as criteria on a.meetingcriteria = criteria.sdlid and criteria.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' ", whereClause, ref dataset);
            List<EmpMeeting> lstMeetings = new List<EmpMeeting>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    EmpMeeting objMeeting = new EmpMeeting();
                    objMeeting.MeetingType = (datarow["MeetingType"] == null ? "" : datarow["MeetingType"].ToString());
                    objMeeting.MeetingCriteria = (datarow["MeetingCriteria"] == null ? "" : datarow["MeetingCriteria"].ToString());
                    objMeeting.MeetingFrequency = (datarow["Frequency"] == null ? "" : datarow["Frequency"].ToString());
                    lstMeetings.Add(objMeeting);
                }
            return lstMeetings;
        }
        public List<EmpReports> GetJobProfileReports(int _JobProfileId)
        {
            

            DataSet dataset = new DataSet();
            string whereClause = " a.JobProfileId = " + _JobProfileId + " And a.CompanyId = " + _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string errorMessage = _dataservice.GetDataWithClause(" ReportType, Frequency ",
                                                                " tblEmpJobProfileDescriptionReports a ", whereClause, ref dataset);
            List<EmpReports> lstReports = new List<EmpReports>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    EmpReports objReport = new EmpReports();
                    objReport.ReportType = (datarow["ReportType"] == null ? "" : datarow["ReportType"].ToString());
                    objReport.ReportFrequency = (datarow["Frequency"] == null ? "" : datarow["Frequency"].ToString());
                    lstReports.Add(objReport);
                }
            return lstReports;
        }
        public List<gvAssetClass> GettblEmpAssetInfo(String _EmpId, String p_sortExpression, String p_sortDirection)
        {
            
            DataSet dataset = new DataSet();
            string whereClause = "";

            //  Added By Danish Iftikhar
            //  Dated: (Nov 2010)
            //  Purpose: For addition of HandingOver/TakingOver Dates from tblAssetHistory table
            //  ********************************************************************************
            if (p_sortExpression != null && p_sortExpression != "" && p_sortDirection != null && p_sortDirection != "")
                whereClause = "  AH.EmpId = " + _EmpId + " order by " + p_sortExpression + " " + p_sortDirection;
            else
                whereClause = "  AH.EmpId = " + _EmpId;//+ " AND AD.CompanyId = " + _CompanyId;
                                                       //  ********************************************************************************
                                                       //  Added By Danish Iftikhar
                                                       //  Dated: (Nov 2010)
                                                       //  Purpose: For addition of HandingOver/TakingOver Dates from tblAssetHistory table
                                                       //  ********************************************************************************
            try
            {
                string errorMessage = _dataservice.GetDataWithClause(" AH.AHid, AD.EmpId, AD.sDate,AD.AssetId, AD.AssetNo, AD.AssetDesc, AD.CatId, SD.Name AS Category,"
                        + "AD.Model, AD.Make, format(AH.HandingOverDate,'dd/MMM/yyyy') HandingOverDate, format(AH.TakingOverDate,'dd/MMM/yyyy') TakingOverDate ",
                        "  tblAssetDetail AD "
                        + " LEFT OUTER JOIN tblAssetHistory AH ON AH.AssetId=AD.AssetId "
                        + " INNER JOIN vwtblSetupsDetail as SD on AD.CatId = SD.SdlId and SD.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'",
                        whereClause,
                        ref dataset);

            }
            catch (Exception excp)
            {

            }
            //  ********************************************************************************

            List<gvAssetClass> items = new List<gvAssetClass>();
            if (dataset != null && dataset.Tables.Count > 0)
                foreach (DataRow datarow in dataset.Tables[0].Rows)
                {
                    gvAssetClass item = new gvAssetClass();
                    item.AssetNo = Convert.ToString(_utilities.HandleNull(datarow["AssetNo"], (Utilities.ReturnType)ReturnType.Int));
                    item.sDate = datarow["sDate"].ToString() == "" ? "" : Convert.ToDateTime(datarow["sDate"]).ToShortDateString();
                    item.AssetDesc = Convert.ToString(_utilities.HandleNull(datarow["AssetDesc"], (Utilities.ReturnType)ReturnType.Int));
                    item.catid = Convert.ToString(_utilities.HandleNull(datarow["catid"], (Utilities.ReturnType)ReturnType.Int));
                    item.Category = Convert.ToString(_utilities.HandleNull(datarow["Category"], (Utilities.ReturnType)ReturnType.String));
                    item.Model = Convert.ToString(_utilities.HandleNull(datarow["Model"], (Utilities.ReturnType)ReturnType.String));
                    item.Make = Convert.ToString(_utilities.HandleNull(datarow["Make"], (Utilities.ReturnType)ReturnType.String));

                    //  Added By Danish Iftikhar
                    //  Dated: (Nov 2010)
                    //  Purpose: For addition of HandingOver/TakingOver Dates from tblAssetHistory table
                    //  ********************************************************************************
                    //item.HandingOverDate = Convert.ToString(Utilities.HandleNull(datarow["HandingOverDate"], Utilities.ReturnType.DateTime));
                    //item.TakingOverDate = Convert.ToString(Utilities.HandleNull(datarow["TakingOverDate"], Utilities.ReturnType.DateTime));
                    item.HandingOverDate = datarow["HandingOverDate"].ToString().Length <= 0 ? "" : datarow["HandingOverDate"].ToString().Trim();
                    item.TakingOverDate = datarow["TakingOverDate"].ToString().Length <= 0 ? "" : datarow["TakingOverDate"].ToString().Trim();

                    //  ********************************************************************************

                    items.Add(item);
                }
            return items;
        }
        #region  EmpJDReportForm

        public DataTable GetDataForEmployeeSeparation(string searchClause, string sCompanyId)
        {
            

            DataSet ds = new DataSet();
            string strResult = _dataservice.ExecuteReader("Select a.CompanyId, a.EmpId, a.EmpCode, a.Name, a.Fname, FORMAT(a.DateJoin, 'dd/MMM/yyyy') As DateJoin, a.NICNew,  a.Designation, a.MainDepartment, a.Department, Isnull(Convert(VARCHAR(10), (Select MAX(AttDate) from tblAttendance WHERE EmpId = a.EmpId AND CompanyId = a.CompanyId AND ALCode = 'P'), 103), '') [LastWorkingDate], ISNULL(vr.Name, '') [ReportingTo] from dbo.fn_EmployeeAll('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") a Left outer join dbo.fn_EmployeeAll('" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "', " + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") vr on(a.ReportTo=vr.empid) WHERE 1=1 " + searchClause + "", ref ds);
            return ds.Tables[0];
        }
        public DataTable GetEmpJDFormReport_Master(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select JobProfileId,mprd.EmpId, JobCode ,JobCDate," +
                                                      " WorkExperience = mprd.WorkExperience,Responsibilities = mprd.Responsibilities," +
                                                      " AcademicQualifications = mprd.AcademicQualifications,EYears, Grade =  sd4.Name,   MainDepartment =  sd6.Name, SubDepartment =  sd7.Name,  " +
                                                      " KeyRelationshipInt,KeyRelationshipExt,EOWorkCondition,PAgeRange,sd12.Name as Division, " +
                                                      " sd15.Name as prefgender ,JobTitle,Case When MidName is null OR len(MidName)=0 Then FirstName + ' ' + LastName Else FirstName +' ' + MidName + ' ' + LastName End 'EmployeeName',EmpCode,RExpYears,Desg.Name as Designation,PageRangeTo,EYearsTo ,RExpYearsTo,IncPackage,OtherBenifit,SalaryRemarks,visaStatus,sd26.Name as 'VisaSponsorshipStatus',SalaryAmountFrom,SalaryAmountTo,SalarycurrencyId,sd27.Name as 'SalaryCurrencyCode'" +
                                                      " From tblEmpJobProfile  as mprd  " +
                                                      " Left outer join vwtblSetupsDetail  as sd4 on sd4.sdlid = mprd.JobId and sd4.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'  " +
                                                      " Left outer join vwtblSetupsDetail  as sd6 on sd6.sdlid = mprd.MdptId and sd6.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'  " +
                                                      " Left outer join vwtblSetupsDetail  as sd7 on sd7.sdlid = mprd.dptId and sd7.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'  " +
                                                      " Left outer join vwtblSetupsDetail  as sd12 on sd12.sdlid = mprd.DivId and sd12.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'  " +
                                                      " Left outer join vwtblSetupsDetail  as sd15 on sd15.sdlid = mprd.PrefGender and sd15.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                      " Left outer join tblEmployee  as Emp on Emp.EmpId = mprd.EmpId " +
                                                      " Left outer join vwtblSetupsDetail  as Desg on Desg.sdlid = mprd.DsgId and Desg.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                      " Left outer join vwtblSetupsDetail  as sd26 on sd26.sdlid = mprd.visaStatus and sd26.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                      " Left outer join vwtblSetupsDetail  as sd27 on sd27.sdlid = mprd.SalaryCurrencyId and sd27.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                      " Where  JobProfileId = '" + _JobCode + "'", ref dataset);

                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_Qualification(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select EMPJOBPROFILEID,QualificationName  = sd.Name " +
                                                     " From tblEmpJdQualifications  as Qual" +
                                                     " left outer join vwtblSetupsDetail as sd on sd.sdlid = Qual.QUALID and sd.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'" +
                                                     " Where  EMPJOBPROFILEID = '" + _JobCode + "'", ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_Certification(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select EMPJOBPROFILEID,CertificationName  = sd.Name" +
                                                       " From tbleMPJdCertificate  as mprdCert " +
                                                       " inner join vwtblSetupsDetail  as sd on sd.sdlid = mprdCert.CERTID sd.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                       " Where mprdCert.EMPJOBPROFILEID = '" + _JobCode + "'", ref dataset);

                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmployeeExtended_QualificationforReport(string empId)
        {
            

            DataSet dataset = new DataSet();
            try
            {
                string whereClause = " empId=" + empId;
                string errorMessage = _dataservice.GetDataWithClause("q.qlfid ,s.Name as Qualification", "tblEmpQualificationInfo Q left outer join vwtblsetupsdetail S on s.sdlid =q.qlfid and S.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'", whereClause, ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dataset.Tables[0];

            }
        }
        public DataTable GetEmployeeBasicInformationforReport(string empId)
        {
            

            DataSet dataset = new DataSet();
            try
            {
                string whereClause = " empId=" + empId;
                string errorMessage = _dataservice.GetDataWithClause("(year (dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") -DateofBirth)-1900) as Years,(DAY (dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") -DateofBirth)-1) as dayss,(MONTH (dbo.fn_General_GetLocalDateTimeCompanyWise(" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + ") -DateofBirth)-1) months,s.Name as Gender", " tblemployee left outer join vwtblsetupsdetail S on s.sdlid = tblemployee.gndid and S.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'", whereClause, ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dataset.Tables[0];

            }
        }
        public DataTable GetEmployeeExtended_CertificationforReport(string empId)
        {
            

            DataSet dataset = new DataSet();
            try
            {

                string whereClause = "empId=" + empId;
                string errorMessage = _dataservice.GetDataWithClause("q.crtid ,s.Name as Certification", " tblEmpCertification Q left outer join vwtblsetupsdetail S on s.sdlid =q.crtid and S.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'", whereClause, ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dataset.Tables[0];

            }
        }
        public DataTable GetEmpJDFormReport_Training(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select EMPJOBPROFILEID,Training  = sd.Name,C.Name as Subjects " +
                                                      " From tblEmpJdTrainings  as mprdTrain " +
                                                      " inner join vwtblSetupsDetail as sd on sd.sdlid = mprdTrain.TRAINID and sd.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'" +
                                                      " left outer JOIN tblTrainingSubjectwiseCategory c ON C.Id= mprdTrain.Subjectid AND C.CompanyId =mprdTrain.COMPANYID " +
                                                      " Where  EMPJOBPROFILEID = '" + _JobCode + "'", ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_Authorities(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select EMPJOBPROFILEID ,Authority = sd.Name " +
                                                     " From tblempjdAuthorities  as mprdAuth " +
                                                     " inner join vwtblSetupsDetail as sd on sd.sdlid = mprdAuth.AUTHID and sd.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'" +
                                                     " Where  EMPJOBPROFILEID = '" + _JobCode + "'", ref dataset);

                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_reports(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select JOBPROFILEID,ReportType,Frequency = sd.Name " +
                                                     " From tblEmpJobProfileDescriptionReports  as mprreports " +
                                                     " inner join vwtblSetupsDetail as sd on sd.sdlid = mprreports.MeetingCriteria and sd.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'" +
                                                     " Where JOBPROFILEID  = '" + _JobCode + "'", ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_Meeting(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select JOBPROFILEID ,Frequency = sd.Name,MeetingType=sd1.Name " +
                                                     " From tblEmpJobProfileMeetings  as mprMeetings " +
                                                     " inner join vwtblSetupsDetail as sd on sd.sdlid = mprMeetings.MeetingCriteria and sd.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'" +
                                                     " inner join vwtblSetupsDetail as sd1 on sd1.sdlid = mprMeetings.MeetingType and sd1.Culture = '" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "' " +
                                                     " Where JOBPROFILEID = '" + _JobCode + "'", ref dataset);


                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_Competency(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select JOBPROFILEID , Competency = QSSD.Name,RatingDescription = CR.Description," +
                                                      " RatingValue = CR.RatingValue,Weightage,isNull(CAST(Ap.FromDate as varchar(12)) + ' - ' + CAST(Ap.ToDate as VARChar(12)),'N/A') as Appraisalperiod ," +
                                                      " isNull(CAST(CR1.Description as varchar(100)) + ' - ' + CAST(CR1.RatingValue as VARChar(100)),'N/A') as ActualRating, " +
                                                      " isNull(case When mrfComp.ReviewPeriod = '-1' then 'N/A' else mrfComp.ReviewPeriod  End  ,'N/A')ReviewFrequency " +
                                                      " From tblEmpJobProfileCompetency as mrfComp " +
                                                      " Inner join tblQuizSubSectionDetail as QSSD on QSSD.subsecdetailId = mrfComp.SubSecDetailId " +
                                                      " Inner join tblCompetencyRating as CR on CR.ID = mrfComp.CompetencyRatingId " +
                                                      " left outer join tblAppraisalPeriod Ap on AP.ApId =mrfComp.ApId and AP.CompanyId =mrfComp.CompanyId " +
                                                      " left outer join tblCompetencyRating CR1 on CR1.Id =mrfComp.ActualRating and CR1.CompanyId =mrfComp.CompanyId " +
                                                      " Where  JOBPROFILEID = '" + _JobCode + "'", ref dataset);

                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        public DataTable GetEmpJDFormReport_Nationality(string _JobCode)
        {
            

            DataTable dt = new DataTable();
            try
            {
                DataSet dataset = new DataSet();
                string r = _dataservice.ExecuteReader(" Select EMPJOBPROFILEID,CountryName  = sd.Name " +
                                                    " From tblEmpJDnationalities  as Country" +
                                                      //" left outer join tblCountry as sd on sd.CntId = Country.CountryId " +
                                                      " left outer join vwtblsetupsdetail as sd on sd.sdlId = Country.CountryId  and sd.Culture='" + _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP()) + "'" +
                                                    " Where  EMPJOBPROFILEID = '" + _JobCode + "'", ref dataset);
                return dataset.Tables[0];
            }
            catch
            {
                return dt = null;

            }
        }
        #endregion

        public string UpdateMessages(int mfhrId)
        {
            

            DataSet dataset = new DataSet();
            string whereClause = "MFHrId = " + mfhrId.ToString() + " And Active = 1 And CompanyId = " + _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string errorMessage = _dataservice.GetDataWithClause("*", "tblMFHR", whereClause, ref dataset);
            DataRow dRow = dataset.Tables[0].Rows[0];
            dRow["Active"] = 0;
            string result = _dataservice.UpdateData("tblMFHr", dataset, whereClause);
            return result;
        }

    }

    public class ReportToEmployees
    {
        public string EMPID { get; set; }
        public string NAME { get; set; }
        public string Level { get; set; }
    }

    public class EmployeeInformationList
    {
        public string EMPCODE { get; set; }
        public string EMPNAME { get; set; }
        public string EMPMAINDEPT { get; set; }
        public string EMPDEPT { get; set; }
        public string EMPDATEJOIN { get; set; }
        public string EMPMAIL { get; set; }
        public string EMPDESIGNATION { get; set; }
        public string EMPPIC { get; set; }

        public string DReportToCompny { get; set; }
        public string IDReportToCompny { get; set; }

        public string REPORTINGPERSON { get; set; }
        public string DOTTEDPERSON { get; set; }
        public string SHIFT { get; set; }
        public string CompanyName { get; set; }
        public string Division { get; set; }
        public string Location { get; set; }
        public string Grade { get; set; }
        public string PayrollGroup { get; set; }
        public string EmployeeType { get; set; }
        public string DateConfirm { get; set; }
        public string ProbhationExtDate { get; set; }
        public string DateConfirmDue { get; set; }
        public string ContractExpiryDate { get; set; }
        public string InternExpiryDate { get; set; }
        public string IDCardRemarks { get; set; }
        public string FamilyCardNo { get; set; }
        public string IqamaNo { get; set; }
        public string IqamaProfession { get; set; }
        public string IqamaExpiryHijri { get; set; }
        public string IqamaExpiryGregorian { get; set; }
        public string CurrSpnsName { get; set; }
        public string SpnsTransferable { get; set; }
        public string SpnsType { get; set; }
        public string SpnsCountry { get; set; }
        public string SpnsCity { get; set; }
        public string SpnsContactDetails { get; set; }
        public string SpnsNatureOfBusiness { get; set; }
        public string SpnsExpiryHijri { get; set; }
        public string SpnsExpiryGregorian { get; set; }
        public string EmployeeCategory { get; set; }
        public string FName { get; set; }
    }

    public class HRMessages
    {
        public string SRNO { get; set; }
        public int MFHRID { get; set; }
        public string HRID { get; set; }
        public string HRMSG { get; set; }
        public string MDATE { get; set; }
    }

    public class ExpiryDates
    {
        public string SRNO { get; set; }
        public string NAME { get; set; }
        public string DATE { get; set; }
        public string REMARKS { get; set; }
    }

    public class EmpJobDescription
    {
        public string JOBSUMMARY { get; set; }
        public string RESPONSIBILITIES { get; set; }
        public string EOWORKCONDITION { get; set; }
        public string ACCOUNTIBILITIES { get; set; }
        public string KPIS { get; set; }
        public byte[] Cv { get; set; }
    }

    public class CalendarInfo
    {
        private string m_SrNo;
        private string m_Description;
        private string m_holidaytype;
        private string m_DateFrom;
        private string m_DateTo;

        public string SRNO
        {
            get { return m_SrNo; }
            set { m_SrNo = value; }
        }
        public string DESCRIPTION
        {
            get { return m_Description; }
            set { m_Description = value; }
        }
        public string HOLIDAYTYPE
        {
            get { return m_holidaytype; }
            set { m_holidaytype = value; }
        }
        public string DATEFROM
        {
            get { return m_DateFrom; }
            set { m_DateFrom = value; }
        }
        public string DATETO
        {
            get { return m_DateTo; }
            set { m_DateTo = value; }
        }
    }
    public class LeaveInfo
    {
        private string m_Description;
        private string m_LCode;
        private string m_MaxAllowed;
        private string m_Availed;
        private string m_Balance;
        private string m_PrevBalance;
        private string m_CurrBalance;
        private string m_PendingLeaves;
        private string m_FiscalYear;
        private string m_IsGradeWiseLeaveEntitlementDifferent;
        private string m_Availed_NextYear;
        private string m_PendingLeaves_NextYear;
        private string m_PenaltiesDays;


        public string PenaltiesDays
        {
            get { return m_PenaltiesDays; }
            set { m_PenaltiesDays = value; }
        }
        public string DESCRIPTION
        {
            get { return m_Description; }
            set { m_Description = value; }
        }

        public string LCode
        {
            get { return m_LCode; }
            set { m_LCode = value; }
        }
        public string MAXALLOWED
        {
            get { return m_MaxAllowed; }
            set { m_MaxAllowed = value; }
        }
        public string AVAILED
        {
            get { return m_Availed; }
            set { m_Availed = value; }
        }
        public string BALANCE
        {
            get { return m_Balance; }
            set { m_Balance = value; }
        }
        public string PREVBALANCE
        {
            get { return m_PrevBalance; }
            set { m_PrevBalance = value; }
        }
        public string CURRBALANCE
        {
            get { return m_CurrBalance; }
            set { m_CurrBalance = value; }
        }
        public string PENDINGLEAVES
        {
            get { return m_PendingLeaves; }
            set { m_PendingLeaves = value; }
        }
        public string FISCALYEAR
        {
            get { return m_FiscalYear; }
            set { m_FiscalYear = value; }
        }
        public string IsGradeWiseLeaveEntitlementDifferent
        {
            get { return m_IsGradeWiseLeaveEntitlementDifferent; }
            set { m_IsGradeWiseLeaveEntitlementDifferent = value; }
        }

        public string AVAILED_NEXTYEAR
        {
            get { return m_Availed_NextYear; }
            set { m_Availed_NextYear = value; }
        }
        public string PENDINGLEAVES_NEXTYEAR
        {
            get { return m_PendingLeaves_NextYear; }
            set { m_PendingLeaves_NextYear = value; }
        }
    }
    public class LeaveInfoPrev
    {
        private string m_Description;
        private string m_MaxAllowed;
        private string m_Availed;
        private string m_PrevBalance;

        public string DESCRIPTION
        {
            get { return m_Description; }
            set { m_Description = value; }
        }
        public string MAXALLOWED
        {
            get { return m_MaxAllowed; }
            set { m_MaxAllowed = value; }
        }
        public string AVAILED
        {
            get { return m_Availed; }
            set { m_Availed = value; }
        }
        public string PREVBALANCE
        {
            get { return m_PrevBalance; }
            set { m_PrevBalance = value; }
        }
    }

    public class HRMessagesRead
    {
        private string m_hrId;
        private string m_Date;
        private string m_Msg;

        public string HRID
        {
            get { return m_hrId; }
            set { m_hrId = value; }
        }
        public string DATE
        {
            get { return m_Date; }
            set { m_Date = value; }
        }
        public string MSG
        {
            get { return m_Msg; }
            set { m_Msg = value; }
        }
    }

    public class EmpJobProfile
    {
        private int m_JobProfileId;
        private string m_JobCode;
        private string m_JobTitle;

        public int JobProfileId
        {
            get { return m_JobProfileId; }
            set { m_JobProfileId = value; }
        }
        public string JobCode
        {
            get { return m_JobCode; }
            set { m_JobCode = value; }
        }
        public string JobTitle
        {
            get { return m_JobTitle; }
            set { m_JobTitle = value; }
        }
    }
    public class EmpMeeting
    {
        private int m_MeetingId;
        private int m_JobProfileId;
        private string m_MeetingType;
        private string m_MeetingCriteria;
        private string m_MeetingFrequency;

        public int MeetingId
        {
            get { return m_MeetingId; }
            set { m_MeetingId = value; }
        }
        public int JobProfileId
        {
            get { return m_JobProfileId; }
            set { m_JobProfileId = value; }
        }
        public string MeetingType
        {
            get { return m_MeetingType; }
            set { m_MeetingType = value; }
        }
        public string MeetingCriteria
        {
            get { return m_MeetingCriteria; }
            set { m_MeetingCriteria = value; }
        }
        public string MeetingFrequency
        {
            get { return m_MeetingFrequency; }
            set { m_MeetingFrequency = value; }
        }
    }
    public class EmpReports
    {
        private int m_ReportId;
        private int m_JobProfileId;
        private string m_ReportType;
        private string m_ReportFrequency;

        public int ReportId
        {
            get { return m_ReportId; }
            set { m_ReportId = value; }
        }
        public int JobProfileId
        {
            get { return m_JobProfileId; }
            set { m_JobProfileId = value; }
        }
        public string ReportType
        {
            get { return m_ReportType; }
            set { m_ReportType = value; }
        }
        public string ReportFrequency
        {
            get { return m_ReportFrequency; }
            set { m_ReportFrequency = value; }
        }
    }

    public class gvAssetClass
    {
        private String _AssetNo;
        private String _sDate;
        private String _AssetDesc;
        private String _catid;
        private String _Category;
        private String _Model;
        private String _Make;

        //  Added By Danish Iftikhar
        //  Dated: (Nov 2010)
        //  Purpose: For addition of HandingOver/TakingOver Dates from tblAssetHistory table
        //  ********************************************************************************
        private String _HandingOverDate;
        private String _TakingOverDate;
        //  ********************************************************************************

        public String AssetNo { get { return _AssetNo; } set { _AssetNo = value; } }
        public String sDate { get { return _sDate; } set { _sDate = value; } }
        public String AssetDesc { get { return _AssetDesc; } set { _AssetDesc = value; } }
        public String catid { get { return _catid; } set { _catid = value; } }
        public String Category { get { return _Category; } set { _Category = value; } }
        public String Model { get { return _Model; } set { _Model = value; } }
        public String Make { get { return _Make; } set { _Make = value; } }

        //  Added By Khurram Rafi
        //  Dated: (8thOct2009)
        //  Purpose: For addition of HandingOver/TakingOver Dates from tblAssetHistory table
        //  ********************************************************************************
        public String HandingOverDate { get { return _HandingOverDate; } set { _HandingOverDate = value; } }
        public String TakingOverDate { get { return _TakingOverDate; } set { _TakingOverDate = value; } }
        //  ********************************************************************************
    }

    public enum ReturnType { String, Int, Bool, DateTime, Double, Byte }
}
