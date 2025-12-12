using HCMS_Api.Components.HCMS.Common.DataAccess;
using System.Configuration;
using System.Data;
using System.Text;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Common;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class EmployeeDashboardComponent
    {
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly DataServices _dataservice;

        public EmployeeDashboardComponent(IConfiguration configuration, Utilities utilities
            , ClientContextService clientContextService, DataServices dataservice)
        {
            _configuration = configuration;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);

        }


        public List<WidgetDetail> GetAppraisalReport(int EmpID)
        {
            try
            {
                DataSet ds = new DataSet();
                List<WidgetDetail> columns = new List<WidgetDetail> { };
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;

                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_PerformanceReport ");

                sbr.Append("@Param_LoginEmpId ='" + EmpID + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            WidgetDetail col = new WidgetDetail();
                            col.AID = Convert.ToInt32(dr["AId"]);
                            col.RequisitionStatus = (dr["RequisitionStatus"]).ToString();
                            columns.Add(col);
                        }

                }


                return columns;
            }
            catch (Exception ex)
            {
                //  throw ex;
                return null;
            }
        }
        public List<WidgetDetail> GetPerformanceReviewReport(int EmpID)
        {
            try
            {
                DataSet ds = new DataSet();
                List<WidgetDetail> columns = new List<WidgetDetail> { };
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;

                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_PerformanceReviewReport ");

                sbr.Append("@Param_LoginEmpId ='" + EmpID + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            WidgetDetail col = new WidgetDetail();
                            col.AID = Convert.ToInt32(dr["RID"]);
                            col.RequisitionStatus = (dr["RequisitionStatus"]).ToString();
                            columns.Add(col);
                        }

                }


                return columns;
            }
            catch (Exception ex)
            {
                //  throw ex;
                return null;
            }
        }

      
        public DataTable getFiscalYearWithNewFormat()
        {
            

            //  string q = "Select SUBSTRING(CAST(DATENAME(month, FromDate) as varchar),1,3) + ' ' + DATENAME(year, FromDate) + ' - ' + SUBSTRING(CAST(DATENAME(month, ToDate) as varchar),1,3) + ' ' + DATENAME(year, ToDate) AS Range, APId FYID,FromDate,ToDate  FROM tblAppraisalPeriod WHERE CompanyId =" + Utilities.GetCompanyId() + " ORDER BY ToDate DESC";
            string query = "    Select distinct " +
          "    a.APId as FYID, dbo.fn_GetDateFormat_DDMMMYYYY(a.FromDate) + ' - ' + dbo.fn_GetDateFormat_DDMMMYYYY(a.ToDate) as Range " + ",Active " +
          "    from tblAppraisalPeriod a" +
          "    where a.CompanyId=" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) +
          " or (select count(*) from tblPMAppraisal where CompanyId=" + _utilities.GetCompanyId(_clientContextService.GetClientIP()) + " and APId=a.APId) >0";
            //  "    union all select - 1 as APId, dbo.fn_GetCultureWiseTranslation('All', '" + culture + "') as Range " +
            //    "    order by Active desc";

            DataSet ds = new DataSet();
            string errorMessage = _dataservice.ExecuteReader(query, ref ds);
            return ds.Tables[0];
        }
        

        
        public List<AppraisalYear> AppraisalYear()
        {
            List<AppraisalYear> AppYear = new List<AppraisalYear>();
            try
            {
                DataTable dsData = getFiscalYearWithNewFormat();
                if (dsData != null)
                {
                    // dsData = dsData.Tables[5];
                    for (int i = 0; i < dsData.Rows.Count; i++)
                    {
                        AppYear.Add(new AppraisalYear
                        {
                            FYID = dsData.Rows[i]["FYID"].ToString(),
                            Range = dsData.Rows[i]["Range"].ToString()
                        });
                    }
                }

            }
            catch (Exception ex) { }

            return AppYear;

        }
        


        
        public EmpDashboard GetColumns(int EmpID, int Count, int Levels, string Operation, string ModuleId)
        {
            try
            {
                UserInfo objuser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                var CompanyID = Convert.ToInt32(_utilities.GetCompanyId(_clientContextService.GetClientIP()));
                // string Self = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "Self")).ToString();
                //Self = Self == "" ? "Self" : Self;

                string Self = "Self";
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                var empStatus = "1,2";
                string userId = objuser.UserID.ToString();
                string ApplicationCode = _utilities.GetApplicationId();
                DataSet ds = new DataSet();
                EmpDashboard columns = new EmpDashboard();
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                var CCode = Convert.ToString(CompanyID);
                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_GetData ");
                sbr.Append("@Param_LoginEmpId=" + EmpID + ",");
                sbr.Append("@Param_LoginCompanyId=" + CompanyID + ",");
                sbr.Append("@Param_Count =" + Count + ",");
                sbr.Append("@Subordinate_Levels=" + Levels + ",");
                sbr.Append("@Self='" + Self + "',");
                sbr.Append("@Param_LoginCulture ='" + culture + "',");
                sbr.Append("@Param_EmployeeStatus ='" + empStatus + "',");
                sbr.Append("@Param_LoginUserId ='" + objuser.UserID.ToString() + "',");
                sbr.Append("@Param_ModuleId ='" + ModuleId + "',");
                sbr.Append("@Param_Operation ='" + Operation + "',");
                sbr.Append("@Param_LoginApplicationCode ='" + "HRIS" + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            Columns col = new Columns();
                            col.ColumnId = Convert.ToInt32(dr["ColumnID"]);
                            //  col.Name = dr["ColumnName"].ToString();
                            col.Width = dr["Width"].ToString();
                            col.MarginLeft = dr["MarginLeft"].ToString();
                            if (dr["Type"] != DBNull.Value)
                                col.Type = Convert.ToString(dr["Type"]);
                            columns.Columns.Add(col);
                        }
                    if (ds.Tables[1].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[1].Rows)
                        {
                            Widgets list = new Widgets();
                            list.WidgetName = dr["WidgetName"].ToString();
                            list.WidgetId = Convert.ToInt32(dr["WidgetID"]);
                            list.ColumnId = Convert.ToInt32(dr["ColumnID"].ToString());
                            list.Width = dr["Width"].ToString();
                            list.Height = dr["Height"].ToString();
                            // list.BackgroundColor = dr["BackgroundColor"].ToString();
                            list.ColorWidth = dr["ColorWidth"].ToString();
                            list.ModuleId = dr["ModuleID"].ToString();
                            list.ItemId = Convert.ToInt32(dr["ItemId"]);
                            list.ParentItemId = Convert.ToInt32(dr["ParentItemId"]);
                            if (dr["Type"] != DBNull.Value)
                                list.Type = Convert.ToString(dr["Type"]);
                            columns.Widgets.Add(list);
                        }

                    if (ds.Tables[2].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[2].Rows)
                        {
                            WidgetDetail listDetail = new WidgetDetail();

                            listDetail.Id = Convert.ToInt32(dr["Id"].ToString());
                            listDetail.Alert = dr["Alert"].ToString();
                            //  listDetail.Widgets = dr["Module"].ToString();
                            listDetail.Widths = dr["Width"].ToString();
                            listDetail.BackgroundColor = dr["BackgroundColor"].ToString();
                            listDetail.Count = (dr["Count"].ToString());
                            listDetail.Url = dr["Url"].ToString();
                            listDetail.ModulesId = dr["ModuleID"].ToString();
                            if (dr["Type"] != DBNull.Value)
                                listDetail.Types = Convert.ToString(dr["Type"]);
                            columns.WidgetDetail.Add(listDetail);
                        }
                }




                return columns;
            }
            catch (Exception ex)
            {
                //  throw ex;
                return null;
            }
        }
        

        public List<Widgets> GetEmpImage(string EmpID, string Level)
        {
            try
            {
                 
                DataSet ds = new DataSet();
                List<Widgets> image = new List<Widgets> { };
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                //string Self = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "Self")).ToString();
                string Self = "Self";
                DataTable DT = new DataTable();
                var CompanyId = Convert.ToInt32(_utilities.GetCompanyId(_clientContextService.GetClientIP()));
                sbr.Append("Exec sp_InsertEmployeeData  ");

                sbr.Append("@Subordinate_Levels='" + Level + "',");
                sbr.Append("@Param_LoginEmpId='" + EmpID + "',");
                sbr.Append("@Param_LoginCompanyId='" + "-1" + "',");//-1 for cross company issue
                sbr.Append("@Self='" + Self + "',");
                sbr.Append("@Param_LoginCulture='" + culture + "',");
                sbr.Append("@Param_EmployeeStatus='" + "1,2" + "'");

                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            Widgets col = new Widgets();
                            col.Name = dr["EmpName"].ToString();
                            col.Id = Convert.ToInt32(dr["EmpId"]);
                            col.Designation = dr["Designation"].ToString();

                            //if (dr["EmpImage"] == DBNull.Value)
                            //{
                            //    //  col.Image = "../Images/EmpImgD.jpg";
                            //    col.Image = "./assets/images/EmpImgNew.png";
                            //}
                            //  else
                            //   {
                            //Byte[] EmpPic = (Byte[])dr["EmpImage"];
                            //var base64String = Convert.ToBase64String(EmpPic, 0, EmpPic.Length);
                            //col.Image = "data:image/png;base64," + base64String;
                            col.Image = _utilities.GetEmployeePicPath(col.Id.ToString(), _utilities.GetCompanyId(_clientContextService.GetClientIP()));
                            //  col.Image += ImageUrl;
                            //col.Image = Utilities.GetCompletePathForReading(Path);


                            //   }

                            // (col.Image == DBNull.Value) ? "~/Images/EmpImgD.jpg" : "data:image/jpg;base64," + Convert.ToBase64String((byte[])img)
                            image.Add(col);
                        }

                }
                return image;
            }
            catch (Exception ex)
            {
                // throw ex;
                return null;
            }
        }
        public List<Widgets> GetColumns_Dashlets(int EmpID, int Count, int Levels, string Operation, string ModuleId)
        {
            List<Widgets> lnWidgets = new List<Widgets>();
            try
            {
                

                DataTable dtWidgets = new DataTable();
                UserInfo objuser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                var userid = objuser.UserID.ToString();
                var CompanyID = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                // string Self = Utilities.NullHandleObject(HttpContext.GetGlobalResourceObject("Global", "Self")).ToString();
                //Self = Self == "" ? "Self" : Self;

                string Self = "Self";
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                var empStatus = "1,2";
                string userId = objuser.UserID.ToString();
                string ApplicationCode = _utilities.GetApplicationId();
                DataSet ds = new DataSet();
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                //var CCode = Convert.ToString(CompanyID);
                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_GetData_Mobile ");
                sbr.Append("@Param_LoginEmpId=" + EmpID + ",");
                sbr.Append("@Param_LoginCompanyId=" + CompanyID + ",");
                sbr.Append("@Param_Count =" + Count + ",");
                sbr.Append("@Subordinate_Levels=" + Levels + ",");
                sbr.Append("@Self='" + Self + "',");
                sbr.Append("@Param_LoginCulture ='" + culture + "',");
                sbr.Append("@Param_EmployeeStatus ='" + empStatus + "',");
                sbr.Append("@Param_LoginUserId ='" + objuser.UserID.ToString() + "',");//objuser.UserID.ToString()
                sbr.Append("@Param_ModuleId ='" + ModuleId + "',");
                sbr.Append("@Param_Operation ='" + Operation + "',");
                sbr.Append("@Param_LoginApplicationCode ='" + "HRIS" + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)

                        if (ds.Tables[0].Rows.Count > 0)


                            dtWidgets = ds.Tables[0];
                    for (int i = 0; i < dtWidgets.Rows.Count; i++)
                    {
                        lnWidgets.Add(new Widgets
                        {
                            WidgetName = dtWidgets.Rows[i]["WidgetName"].ToString(),
                            ModuleId = dtWidgets.Rows[i]["ModuleId"].ToString()


                        });
                    }

                }


            }
            catch (Exception ex) { }

            return lnWidgets;

        }

        public List<WidgetDetail> GetColumns_SubDashlets(int EmpID, int Count, int Levels, string Operation, string ModuleId)
        {
            List<WidgetDetail> lnWidgets = new List<WidgetDetail>();
            try
            {

                

                DataTable dtWidgets = new DataTable();
                UserInfo objuser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                var CompanyID = Convert.ToInt32(_utilities.GetCompanyId(_clientContextService.GetClientIP()));
                string Self = "Self";
                Self = Self == "" ? "Self" : Self;
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                var empStatus = "1,2";
                string userId = objuser.UserID.ToString();

                string ApplicationCode = _utilities.GetApplicationId();
                DataSet ds = new DataSet();
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                var CCode = Convert.ToString(CompanyID);
                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_GetData_SubMobile ");
                sbr.Append("@Param_LoginEmpId=" + EmpID + ",");
                sbr.Append("@Param_LoginCompanyId=" + CompanyID + ",");
                sbr.Append("@Param_Count =" + 1 + ",");
                sbr.Append("@Subordinate_Levels=" + Levels + ",");
                sbr.Append("@Self='" + Self + "',");
                sbr.Append("@Param_LoginCulture ='" + culture + "',");
                sbr.Append("@Param_EmployeeStatus ='" + empStatus + "',");
                sbr.Append("@Param_LoginUserId ='" + objuser.UserID.ToString() + "',");
                sbr.Append("@Param_ModuleId ='" + ModuleId + "',");
                sbr.Append("@Param_Operation ='" + Operation + "',");
                sbr.Append("@Param_LoginApplicationCode ='" + "HRIS" + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)

                        if (ds.Tables[0].Rows.Count > 0)


                            dtWidgets = ds.Tables[0];
                    for (int i = 0; i < dtWidgets.Rows.Count; i++)
                    {
                        lnWidgets.Add(new WidgetDetail
                        {
                            Alert = dtWidgets.Rows[i]["Alert"].ToString(),
                            Count = dtWidgets.Rows[i]["Count"].ToString(),
                            ModulesId = dtWidgets.Rows[i]["ModuleId"].ToString(),
                            Id = Convert.ToInt16(dtWidgets.Rows[i]["Id"].ToString()),


                        });
                    }

                }


            }
            catch (Exception ex) { }

            return lnWidgets;

        }
        public List<WidgetDetail> GetAppraisalPerformance(int EmpId)
        {
            try
            {

                

                DataSet ds = new DataSet();
                List<WidgetDetail> columns = new List<WidgetDetail> { };
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                var CompanyID = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_AppraisalPerformance ");
                sbr.Append("@Param_LoginCompanyId  ='" + CompanyID + "',");
                sbr.Append("@Param_LoginEmpId ='" + EmpId + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            WidgetDetail appraisal = new WidgetDetail();
                            appraisal.Date = dr["Date"].ToString();
                            appraisal.Active = dr["Active"].ToString();

                            if (dr["Type"] != DBNull.Value)
                                appraisal.Types = Convert.ToString(dr["Type"]);
                            columns.Add(appraisal);
                        }

                    if (ds.Tables[1].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[1].Rows)
                        {
                            WidgetDetail currentPerformance = new WidgetDetail();
                            currentPerformance.Date = dr["Date"].ToString();
                            //  currentPerformance.Active = dr["Active"].ToString();

                            if (dr["Type"] != DBNull.Value)
                                currentPerformance.Types = Convert.ToString(dr["Type"]);
                            columns.Add(currentPerformance);
                        }


                    if (ds.Tables[2].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[2].Rows)
                        {
                            WidgetDetail previousPerformance = new WidgetDetail();
                            previousPerformance.Date = dr["Date"].ToString();
                            //  currentPerformance.Active = dr["Active"].ToString();

                            if (dr["Type"] != DBNull.Value)
                                previousPerformance.Types = Convert.ToString(dr["Type"]);
                            columns.Add(previousPerformance);
                        }

                }




                return columns;
            }
            catch (Exception ex)
            {
                // throw ex;
                return null;
            }
        }
        public List<WidgetDetail> GetPayroll()
        {
            try
            {
                

                DataSet ds = new DataSet();
                List<WidgetDetail> columns = new List<WidgetDetail> { };
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                var CompanyID = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                sbr.Append("Exec sp_Personnel_EmpInfoDashboard_Payroll ");
                sbr.Append("@Param_LoginCompanyId ='" + CompanyID + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    if (ds.Tables[0].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            WidgetDetail attendance = new WidgetDetail();
                            attendance.Attendance = dr["Attendance"].ToString();

                            if (dr["Type"] != DBNull.Value)
                                attendance.Types = Convert.ToString(dr["Type"]);
                            columns.Add(attendance);
                        }
                    if (ds.Tables[1].Rows.Count > 0)
                        foreach (DataRow dr in ds.Tables[1].Rows)
                        {
                            WidgetDetail processed = new WidgetDetail();
                            processed.Processed = dr["Processed"].ToString();

                            if (dr["Type"] != DBNull.Value)
                                processed.Types = Convert.ToString(dr["Type"]);
                            columns.Add(processed);
                        }
                }




                return columns;
            }
            catch (Exception ex)
            {
                // throw ex;
                return null;
            }
        }

        public List<clsNineBoxReportsOutput> GetNineBoxReportsData(string Operation, int Level, int EmpId, int AppId)
        {
            

            DataSet ds = new DataSet();
            List<clsNineBoxReportsOutput> clsNineBoxRptOutput = new List<clsNineBoxReportsOutput>();
            StringBuilder sbr = new StringBuilder();
            string result = string.Empty;
            UserInfo userinfo = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
            string userId = Convert.ToString(userinfo.UserID);
            string userEmpId = Convert.ToString(userinfo.UserEmpId);
            string userEmpCode = Convert.ToString(userinfo.UserEmpCode);
            string userEmpName = Convert.ToString(userinfo.UserEmpName);
            string terminal = "";
            string terminalIp = "";
            string applicationId = _utilities.GetApplicationId();
            string formId = Constants.NineBoxPerformance;
            string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            string _companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()).ToString();

            sbr.Append("Exec sp_Personnel_EmpInfoDashboard_NineBox ");
            sbr.Append("@Param_Operation='" + Operation + "', ");
            sbr.Append("@Param_EmpId=" + EmpId + ", ");//EmpId //12
            sbr.Append("@Param_AppId=" + AppId + ", ");//AppId //1
            sbr.Append("@Param_Level=" + Level + ", ");


            sbr.Append("@Param_LoginCompanyId=" + _companyId + ", ");
            sbr.Append("@Param_LoginUserId='" + userId + "', ");
            sbr.Append("@Param_LoginUserEmpCode='" + userEmpCode + "', ");
            sbr.Append("@Param_LoginEmpId='" + userEmpId + "', ");
            sbr.Append("@Param_LoginUserEmpName='" + userEmpName + "', ");
            sbr.Append("@Param_FormId='" + formId + "', ");
            sbr.Append("@Param_TimeStamp='', ");
            sbr.Append("@Param_LoginCulture='" + culture + "', ");
            sbr.Append("@Param_EntTerminal='" + terminal + "', ");
            sbr.Append("@param_EntTerminalIP='" + terminalIp + "', ");
            sbr.Append("@param_ApplicationId='" + "HRIS" + "'");
            result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
            if (ds != null && ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        clsNineBoxReportsOutput obj = new clsNineBoxReportsOutput();
                        if (dr["Id"] != DBNull.Value)
                            obj.Id = Convert.ToInt32(dr["Id"]);
                        if (dr["Name"] != DBNull.Value)
                            obj.Name = Convert.ToString(dr["Name"]);
                        if (dr["Count"] != DBNull.Value)
                            obj.Count = Convert.ToInt32(dr["Count"]);
                        if (dr["Type"] != DBNull.Value)
                            obj.Type = Convert.ToString(dr["Type"]);
                        clsNineBoxRptOutput.Add(obj);
                    }
                if (ds.Tables[1].Rows.Count > 0)
                    foreach (DataRow dr in ds.Tables[1].Rows)
                    {
                        clsNineBoxReportsOutput obj = new clsNineBoxReportsOutput();
                        if (dr["Id"] != DBNull.Value)
                            obj.Id = Convert.ToInt32(dr["Id"]);
                        if (dr["Name"] != DBNull.Value)
                            obj.Name = Convert.ToString(dr["Name"]);
                        if (dr["Count"] != DBNull.Value)
                            obj.Count = Convert.ToInt32(dr["Count"]);
                        if (dr["Type"] != DBNull.Value)
                            obj.Type = Convert.ToString(dr["Type"]);
                        clsNineBoxRptOutput.Add(obj);
                    }
                if (ds.Tables[2].Rows.Count > 0)
                    foreach (DataRow dr in ds.Tables[2].Rows)
                    {
                        clsNineBoxReportsOutput obj = new clsNineBoxReportsOutput();
                        if (dr["BoxId"] != DBNull.Value)
                            obj.BoxId = Convert.ToString(dr["BoxId"]);
                        if (dr["Color"] != DBNull.Value)
                            obj.Color = Convert.ToString(dr["Color"]);
                        if (dr["Description"] != DBNull.Value)
                            obj.Description = Convert.ToString(dr["Description"]);
                        if (dr["Type"] != DBNull.Value)
                            obj.Type = Convert.ToString(dr["Type"]);
                        clsNineBoxRptOutput.Add(obj);
                    }

                if (ds.Tables[3].Rows.Count > 0)
                    foreach (DataRow dr in ds.Tables[3].Rows)
                    {
                        clsNineBoxReportsOutput obj = new clsNineBoxReportsOutput();
                        if (dr["BoxId"] != DBNull.Value)
                            obj.BoxId = Convert.ToString(dr["BoxId"]);
                        if (dr["Color"] != DBNull.Value)
                            obj.Color = Convert.ToString(dr["Color"]);
                        if (dr["Type"] != DBNull.Value)
                            obj.Type = Convert.ToString(dr["Type"]);
                        clsNineBoxRptOutput.Add(obj);
                    }

                if (ds.Tables[4].Rows.Count > 0)
                    foreach (DataRow dr in ds.Tables[4].Rows)
                    {
                        clsNineBoxReportsOutput obj = new clsNineBoxReportsOutput();
                        if (dr["BoxId"] != DBNull.Value)
                            obj.BoxId = Convert.ToString(dr["BoxId"]);
                        if (dr["Color"] != DBNull.Value)
                            obj.Color = Convert.ToString(dr["Color"]);
                        if (dr["Type"] != DBNull.Value)
                            obj.Type = Convert.ToString(dr["Type"]);
                        clsNineBoxRptOutput.Add(obj);
                    }

                if (ds.Tables[5].Rows.Count > 0)
                    //  HttpContext.Current.Session["DashboardNineBoxAnalysisReportsData"] = ds.Tables[5];
                    foreach (DataRow dr in ds.Tables[5].Rows)
                    {
                        clsNineBoxReportsOutput obj = new clsNineBoxReportsOutput();
                        if (dr["Id"] != DBNull.Value)
                            obj.Id = Convert.ToInt32(dr["Id"]);
                        if (dr["EmpId"] != DBNull.Value)
                            obj.EmpId = Convert.ToInt32(dr["EmpId"]);
                        if (dr["BoxId"] != DBNull.Value)
                            obj.BoxId = Convert.ToString(dr["BoxId"]);
                        if (dr["Count"] != DBNull.Value)
                            obj.Count = Convert.ToInt32(dr["Count"]);
                        if (dr["Percentage"] != DBNull.Value)
                            obj.Percentage = Convert.ToString(dr["Percentage"]);
                        if (dr["ApId"] != DBNull.Value)
                            obj.ApId = Convert.ToInt32(dr["ApId"]);
                        if (dr["Type"] != DBNull.Value)
                            obj.Type = Convert.ToString(dr["Type"]);
                        if (dr["RelativeRating"] != DBNull.Value)
                            obj.RelativeRating = Convert.ToInt32(dr["RelativeRating"]);
                        if (dr["ValidationGroup"] != DBNull.Value)
                            obj.ValidationGroup = Convert.ToString(dr["ValidationGroup"]);
                        clsNineBoxRptOutput.Add(obj);
                    }
            }
            return clsNineBoxRptOutput;
        }

        public DataTable GetRptNineBoxEmpDetail(string BoxId, int RRating, List<clsNineBoxReportsOutput> BoxDetail)
        {

            try
            {
                

                DataSet ds = new DataSet();
                StringBuilder sbr = new StringBuilder();
                string EmpId = string.Empty; string EmpIdAll = string.Empty;
                int ApId = 0;
                //  var  lst_NineBox = JsonConvert.DeserializeObject<List<string>>(BoxDetail);
                //  if(ds1.Tables[5].Rows.Count == 0)
                if (BoxDetail == null || BoxDetail.Count == 0)
                {
                    EmpId = "0";
                }
                else
                {
                    //DataView dv = new DataView();
                    //DataTable dt = new DataTable();
                    //DataTable dt2 = new DataTable();
                    //dv.Table = (DataTable)HttpContext.Current.Session["DashboardNineBoxAnalysisReportsData"];
                    //dt2 = dv.ToTable();
                    foreach (var dr in BoxDetail)
                    {
                        EmpIdAll += dr.EmpId + ",";
                        ApId = dr.ApId;
                    }
                    // dv.RowFilter = "BoxId= '" + BoxId + "'";
                    //dt = dv.ToTable();
                    foreach (var dr in BoxDetail)
                    {
                        EmpId += dr.EmpId + ",";
                        ApId = dr.ApId;
                    }
                }


                UserInfo userinfo = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                string userId = userinfo.UserID.ToString();
                string userEmpId = userinfo.UserEmpId.ToString();
                string userEmpCode = userinfo.UserEmpCode.ToString();
                string userEmpName = userinfo.UserEmpName.ToString();
                string terminal = "";
                string terminalIp = "";
                string applicationId = _utilities.GetApplicationId();
                string formId = Constants.NineBoxPerformance;

                string _companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()).ToString();
                string culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                string result = string.Empty;


                sbr.Append("Exec sp_BoxPerformance_Report ");
                sbr.Append("@Param_Operation='GetEmployeeDetail', ");

                sbr.Append("@Param_EmpIdsFinalized='" + EmpId + "', ");
                sbr.Append("@Param_EmpIdsFinalizedAll='" + EmpIdAll + "', ");
                sbr.Append("@Param_RRating='" + RRating + "', ");
                sbr.Append("@Param_AppId=" + ApId + ", ");

                sbr.Append("@Param_LoginCompanyId=" + _companyId + ", ");
                sbr.Append("@Param_LoginUserId='" + userId + "', ");
                sbr.Append("@Param_LoginUserEmpCode='" + userEmpCode + "', ");
                sbr.Append("@Param_LoginEmpId='" + userEmpId + "', ");
                sbr.Append("@Param_LoginUserEmpName='" + userEmpName + "', ");
                sbr.Append("@Param_FormId='" + formId + "', ");
                sbr.Append("@Param_TimeStamp='', ");
                sbr.Append("@Param_LoginCulture='" + culture + "', ");
                sbr.Append("@Param_EntTerminal='" + terminal + "', ");
                sbr.Append("@param_EntTerminalIP='" + terminalIp + "', ");
                sbr.Append("@param_ApplicationId='" + applicationId + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (ds != null && ds.Tables.Count > 0)
                {
                    // HttpContext.Current.Session["DashboardNineBoxEmpDetailCount"] = ds.Tables[0].Rows.Count;
                    return ds.Tables[0];
                }
                else
                {
                    //HttpContext.Current.Session["DashboardNineBoxEmpDetailCount"] = 0;
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }

        }
        public List<clsNineBoxStrategyDesc> GetNineBoxStrategyDesc(string Operation, string boxId)
        {
            

            DataSet ds = new DataSet();
            List<clsNineBoxStrategyDesc> clsNineBoxStrategyDesc = new List<clsNineBoxStrategyDesc>();
            StringBuilder sbr = new StringBuilder();
            var _companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()).ToString();

            sbr.Append("Exec sp_BoxPerformance_Report ");
            sbr.Append("@Param_Operation='" + Operation + "',");
            sbr.Append("@Param_LoginCompanyId=" + _companyId + ",");
            sbr.Append("@Param_BoxId='" + boxId + "'");

            var result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
            if (ds != null && ds.Tables.Count > 0)
            {
                if (ds.Tables[0].Rows.Count > 0)
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        clsNineBoxStrategyDesc obj = new clsNineBoxStrategyDesc();

                        if (dr["BoxId"] != DBNull.Value)
                            obj.BoxId = Convert.ToString(dr["BoxId"]);
                        if (dr["Description"] != DBNull.Value)
                            obj.Description = Convert.ToString(dr["Description"]);
                        clsNineBoxStrategyDesc.Add(obj);
                    }
            }

            return clsNineBoxStrategyDesc;
        }

        public DataSet GetDashletAlerts(string EmpId)
        {
            try
            {
                

                DataTable dt = new DataTable();
                string query = string.Empty;
                DataSet dataset = new DataSet();
                StringBuilder sbr = new StringBuilder();
                var CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()).ToString();
                UserInfo userinfo = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                string userId = userinfo.UserID.ToString();
                var ApplicationCode = _utilities.GetApplicationId();
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                sbr.Append("Exec [sp_Personnel_EmpInfoDashboard_Transaction]");
                sbr.Append("@Param_LoginEmpId='" + EmpId + "',");
                sbr.Append("@Param_LoginCompanyId='" + CompanyId + "',");

                sbr.Append("@Param_LoginUserId='" + userId + "',");
                sbr.Append("@Param_LoginApplicationCode='" + ApplicationCode + "',");
                sbr.Append("@Param_LoginCulture='" + culture + "'");
                string result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref dataset);
                if (result == "successfull")
                {
                    return dataset;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public DataSet SetDashletAlerts(string Mode, string Module, string Alert)
        {

            try
            {
                

                DataTable dt = new DataTable();
                string query = string.Empty;
                DataSet dataset = new DataSet();
                StringBuilder sbr = new StringBuilder();
                var Applicationcode = _utilities.GetApplicationId();
                var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()).ToString();
                UserInfo userinfo = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                var userId = userinfo.UserID.ToString();
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                var EntTerminalIP = "";
                string EnterTerminal = System.Net.Dns.GetHostName();
                sbr.Append("Exec [sp_Personnel_EmpInfoDashboard_SetDashletAlerts]");
                sbr.Append(" @Param_LoginApplicationCode='" + Applicationcode + "',");
                sbr.Append(" @Param_Mod='" + Mode + "',");
                sbr.Append(" @Param_Module='" + Module + ",");
                sbr.Append(" @Param_Alert='" + Alert + "',");
                sbr.Append(" @Param_LoginCompanyId='" + companyId + "',");
                sbr.Append(" @Param_LoginUserId='" + userId + "',");
                sbr.Append(" @Param_LoginCulture='" + culture + "',");
                sbr.Append(" @Param_EntUserId='" + userId + "',");
                sbr.Append(" @Param_EntTerminal='" + EnterTerminal + "',");
                sbr.Append(" @Param_EntTerminalIP='" + EntTerminalIP + "'");

                string result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref dataset);
                if (result == "successfull")
                {
                    return dataset;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public DataSet FavoriteTabTransaction(string ParentId, string ItemId)
        {
            

            DataSet ds = new DataSet();
            StringBuilder sbr = new StringBuilder();
            string result = string.Empty;
            var Applicationcode = _utilities.GetApplicationId();
            var companyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()).ToString();
            UserInfo userinfo = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
            var userId = userinfo.UserID.ToString();
            var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
            var EntTerminalIP = "";
            string EnterTerminal = System.Net.Dns.GetHostName();
            sbr.Append("Exec [sp_Personnel_EmpInfoDashboard_RemoveTabTransaction] ");
            sbr.Append(" @Param_LoginApplicationCode='" + Applicationcode + "',");
            sbr.Append(" @Param_Module='" + ParentId + "',");
            sbr.Append(" @Param_Alert='" + ItemId + "',");
            sbr.Append(" @Param_LoginCompanyId='" + companyId + "',");
            sbr.Append(" @Param_LoginUserId='" + userId + "',");
            sbr.Append(" @Param_LoginCulture='" + culture + "',");
            sbr.Append(" @Param_EntUserId='" + userId + "',");
            sbr.Append(" @Param_EntTerminal='" + EnterTerminal + "',");
            sbr.Append(" @Param_EntTerminalIP='" + EntTerminalIP + "'");
            result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);

            if (result == "successfull")
            {
                return ds;
            }
            else
                return null;
        }


        public DataSet GetEmpInfo(string EmpId, string Level)
        {
            try
            {
                

                DataSet ds = new DataSet();
                //   List<clsColumnWidget> EmpInfo = new List<EmployeeDashboardComponent.clsColumnWidget> { };
                var Self = "Self";
                var empStatus = "1,2";
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                var getCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                sbr.Append("Exec dbo.sp_Personnel_EmpInfoDashboard_PendingApproval  ");
                sbr.Append("@Param_LoginEmpId=" + EmpId + ",");
                sbr.Append("@Param_LoginCompanyId=" + getCompanyId + ",");
                sbr.Append("@Param_LoginCulture ='" + culture + "',");
                sbr.Append("@Self='" + Self + "',");
                sbr.Append("@Param_ApplicationCode='" + "HRIS" + "',");
                sbr.Append("@Subordinate_Levels='" + Level + "',");
                sbr.Append("@Param_EmployeeStatus ='" + empStatus + "'");

                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);

                if (result == "successfull")
                {
                    return ds;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                // throw ex;
                return null;
            }
        }
        public DataSet GetIncompleteHiringChecklist(string EmpId, string EmpCode)
        {
            try
            {
                

                DataSet ds = new DataSet();
                var culture = _utilities.GetAppCurrentUICulture(_clientContextService.GetClientIP());
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty;
                var getCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                sbr.Append("Exec dbo.sp_Personnel_EmpInfoDashboard_IncompleteHiringChecklist  ");
                sbr.Append("@Param_LoginEmpId=" + EmpId + ",");
                sbr.Append("@Param_LoginCompanyId=" + getCompanyId + ",");
                sbr.Append("@Param_LoginCulture ='" + culture + "',");
                sbr.Append("@EmpCode='" + EmpCode + "'");
                result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref ds);
                if (result == "successfull")
                {
                    return ds;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                // throw ex;
                return null;
            }
        }

        //


    }

    public class clsNineBoxStrategyDesc
    {
        int _Id; public int Id { get { return _Id; } set { _Id = value; } }
        string _boxId; public string BoxId { get { return _boxId; } set { _boxId = value; } }
        string _description; public string Description { get { return _description; } set { _description = value; } }
        string _sessionExpire; public string SessionExpire { get { return _sessionExpire; } set { _sessionExpire = value; } }
        string _error; public string Error { get { return _error; } set { _error = value; } }
    }
    public class clsNineBoxReportsOutput
    {
        int _Id; public int Id { get { return _Id; } set { _Id = value; } }
        int _empId; public int EmpId { get { return _empId; } set { _empId = value; } }
        string _Name; public string Name { get { return _Name; } set { _Name = value; } }
        string _boxId; public string BoxId { get { return _boxId; } set { _boxId = value; } }
        string _color; public string Color { get { return _color; } set { _color = value; } }
        string _desc; public string Description { get { return _desc; } set { _desc = value; } }
        int _count; public int Count { get { return _count; } set { _count = value; } }
        string _type; public string Type { get { return _type; } set { _type = value; } }
        string _sessionExpire; public string SessionExpire { get { return _sessionExpire; } set { _sessionExpire = value; } }
        string _error; public string Error { get { return _error; } set { _error = value; } }
        int _relativeRating; public int RelativeRating { get { return _relativeRating; } set { _relativeRating = value; } }
        string _ValidationGroup; public string ValidationGroup { get { return _ValidationGroup; } set { _ValidationGroup = value; } }
        string _percent; public string Percentage { get { return _percent; } set { _percent = value; } }
        int _ApId; public int ApId { get { return _ApId; } set { _ApId = value; } }
    }
    public class AppraisalYear
    {
        public string FYID { get; set; }
        public string Range { get; set; }

    }
    public class fn_Leave_GetAuthorizedLeaveTypes_Result
    {
        public string LeaveType { get; set; }
        public string LCode { get; set; }
        public string Description { get; set; }
        public Nullable<bool> CarryForward { get; set; }
    }
    public class EmpDashboard
    {
        public List<Columns> Columns { get; set; } = new List<Columns>();
        public List<Widgets> Widgets { get; set; } = new List<Widgets>();
        public List<WidgetDetail> WidgetDetail { get; set; } = new List<WidgetDetail>();
    }


    public class Columns
    {
        public int ColumnId { get; set; }
        public string Width { get; set; }
        public string MarginLeft { get; set; }
        public string Type { get; set; }
    }

    public class Widgets
    {
        public string WidgetName { get; set; }
        public int WidgetId { get; set; }
        public int ColumnId { get; set; }
        public string Width { get; set; }

        public string Height { get; set; }

        public string ColorWidth { get; set; }

        public string ModuleId { get; set; }

        public int ItemId { get; set; }

        public int ParentItemId { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public int Id { get; set; }
        public string Designation { get; set; }

        public string Image { get; set; }
    }

    public class WidgetDetail
    {
        public int Id { get; set; }
        public string Alert { get; set; }

        public string Widths { get; set; }

        public string BackgroundColor { get; set; }

        public string Count { get; set; }

        public string Url { get; set; }

        public string ModulesId { get; set; }

        public string Types { get; set; }

        public string Date { get; set; }
        public string Active { get; set; }

        public string Attendance { get; set; }
        public string Processed { get; set; }

        public int AID;
        public string RequisitionStatus;

    }
}
