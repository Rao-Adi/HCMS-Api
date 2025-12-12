using HCMS_Api.Components.HCMS.Common.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using static HCMS_Api.Controllers.HCMS.Common.SecurityController;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;
using System.Data.SqlClient;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Common;

namespace HCMS_Api.Components.HCMS.Common.Security
{
    public class LoginComponent
    {
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;

        public LoginComponent(IConfiguration configuration, Utilities utilities
            , DataServices dataservice)
        {
            _configuration = configuration;
            _utilities = utilities;
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        public static void Initialize(IConfiguration configuration)
        {
            /*
            var LoginComponent = new LoginComponent(configuration);
            var ConnectionString = LoginComponent._configuration.GetConnectionString("ConnectionString");
            var auditTrailConnectionString = LoginComponent._configuration.GetConnectionString("vCurioAuditTrailConnectionString");

            dataService.BeginProcess(ConnectionString);
            // dataServiceAuditTrail.BeginProcess(auditTrailConnectionString);
            */
        }
        public string GetEncryptionKey()
        {
            string connectionString = _configuration.GetConnectionString("ConnectionString");
            string encryptionKey = "";

            try
            {
                string query = "select [dbo].[FN_GetUserIdEncryptionKey]()";

                // Assuming you have a method to execute scalar queries in your Utilities class
                encryptionKey = _utilities.GetScalarData(query).ToString();
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }

            return encryptionKey;
        }
        public string CheckAuthenticationRedirectFormUrl(string _UserId, string _UserIdCode,
        string _UserPwd,
        string _CompanyId,
        string _CompanyName,
        string _AppId,
        string _formLocation,
       string Prefix,
       string ClientiP)
        {
            string _Path = String.Empty;
            string UserId = _UserId;
            string UserPwd = _UserPwd;

            UserId = UserId.Replace("'", "");
            UserPwd = UserPwd.Replace("'", "");
            if (UserId != "" && UserPwd != "")
            {
                _Path = SetValuesRedirectFormUrl(_UserId, _UserIdCode, _CompanyId, _CompanyName, _formLocation, Prefix, ClientiP);
                //if (Secure.ERPSecurity.authenticateSimple(_UserId, _UserPwd, _CompanyId.Trim().PadLeft(3, '0'),_AppId.Trim()))

            }
            return _Path;
        }

        private string SetValuesRedirectFormUrl(string _UserId, string _UserIdCode, string _CompanyId, string _CompanyName, string _formLocation, string Prefix, string ClientiP)
        {
            string _RedirectPath = String.Empty;
            string strEmpId = String.Empty, strPGId = String.Empty, strEmpName = String.Empty, CCode = _CompanyId;
            DataServices dataservice = new DataServices(_configuration);
            string _Culture = _utilities.GetAppCurrentUICulture(ClientiP);
            string _MappedCompanies = string.Empty;

            string applicationId = _utilities.GetApplicationId();

            DataSet UserDS = new DataSet();
            string strQuery = "SELECT dbo.FN_GetUserIdBYuseridCode('" + _UserIdCode + "') as UserId";
            string strTemp = dataservice.ExecuteSecurityReader(strQuery, ref UserDS);
            if (UserDS != null && UserDS.Tables[0].Rows.Count != 0)
            {
                _UserId = UserDS.Tables[0].Rows[0]["UserId"].ToString();
            }

            if (!GetUserData("N'" + _UserId.ToUpper() + "'", _CompanyId, out strEmpId, out strPGId, out strEmpName))
            {
                _RedirectPath = "ErrorPageUnSuccessfulMapping";
                return _RedirectPath;
            }

            try
            {
                string _TotalMenuChunks = _utilities.GetTotalChunksInRedis(_UserId + applicationId + _CompanyId + "GenerateMenuTotalChunks", _configuration);
                if (String.IsNullOrEmpty(_TotalMenuChunks))
                {
                    DataTable dtMenu = GetMenuData(applicationId, _UserId, _CompanyId, _Culture, _configuration);
                    DataTable dtSubMenu = new DataTable();
                    int _totalChunksMenu = 0;

                    if (dtMenu != null && dtMenu.Rows.Count > 0)
                    {
                        if (_utilities.RemoveAllKeyInRedis(_UserId + applicationId + _CompanyId, _configuration))
                        {
                            string _Check = "True";
                        }

                        var totalRows = dtMenu.Rows.Count;
                        int _reminder = totalRows % 100;
                        totalRows = totalRows - (totalRows % 100);

                        if (totalRows > 100)
                        {
                            int halfway = totalRows / 100;
                            int _skip = 0;
                            var _GenerateMenu = dtMenu;

                            for (int i = 1; i <= halfway; i++)
                            {
                                if (i == 1)
                                {
                                    _GenerateMenu = dtMenu.AsEnumerable().Take(100).CopyToDataTable();
                                }
                                else
                                {
                                    _GenerateMenu = dtMenu.AsEnumerable().Skip(_skip).Take(100).CopyToDataTable();
                                }

                                dtSubMenu = (DataTable)_GenerateMenu;
                                string JSONresultGenerateMenu = JsonConvert.SerializeObject((DataTable)_GenerateMenu);
                                _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + applicationId + _CompanyId + i.ToString());
                                _skip += 100;

                                if (_reminder != 0 && _skip == totalRows)
                                {
                                    i++;
                                    _GenerateMenu = dtMenu.AsEnumerable().Skip(_skip).Take(_reminder).CopyToDataTable();
                                    JSONresultGenerateMenu = JsonConvert.SerializeObject((DataTable)_GenerateMenu);
                                    _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + applicationId + _CompanyId + i.ToString());
                                }

                                _totalChunksMenu = i;
                            }

                            _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", _totalChunksMenu.ToString(), _UserId + applicationId + _CompanyId);
                        }
                        else
                        {
                            string JSONresultGenerateMenu = JsonConvert.SerializeObject(dtMenu);
                            _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + applicationId + _CompanyId + "1");
                            _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", "1", _UserId + applicationId + _CompanyId);
                        }
                    }
                }
            }
            catch (Exception excp)
            {
                _RedirectPath = "ErrorPage.aspx?qsAccess=PermissionsNotSet&err=" + excp.Message;
                return _RedirectPath;
            }

            DataTable dtOthers = new DataTable();
            dtOthers.Columns.Add("UICulture", typeof(string));
            dtOthers.Columns.Add("Culture", typeof(string));
            dtOthers.Columns.Add("UserIdCode", typeof(string));
            dtOthers.Columns.Add("UserId", typeof(string));
            dtOthers.Columns.Add("userid", typeof(string));
            dtOthers.Columns.Add("FileName", typeof(string));
            dtOthers.Columns.Add("FilePath", typeof(string));
            dtOthers.Columns.Add("CompanyId", typeof(string));
            dtOthers.Columns.Add("CompanyName", typeof(string));
            dtOthers.Columns.Add("CompanyName_UserWise", typeof(string));
            dtOthers.Columns.Add("BaseCompanyId", typeof(string));
            dtOthers.Columns.Add("EmpId", typeof(string));
            dtOthers.Columns.Add("PGid", typeof(string));
            dtOthers.Columns.Add("PGIDS", typeof(string));
            dtOthers.Columns.Add("EmpName", typeof(string));
            dtOthers.Columns.Add("Logo", typeof(string));

            string CompanyName = _utilities.GetScalarSecurityData("Select ISNULL(LTRIM(RTRIM(CompanyShortName)), '') from vCompanyManagement WHERE CCode = " + _CompanyId + "").ToString();
            string CompanyName_UserWise = String.Empty;

            if (!String.IsNullOrEmpty(CompanyName))
            {
                string multicompanyuser = " select * from dbo.FN_GetCompanyNameByUserId('" + _UserId + "')";
                DataTable dtmulticomany = _utilities.GetDataTableSecurity(multicompanyuser);

                if (dtmulticomany != null && dtmulticomany.Rows.Count == 1)
                {
                    CompanyName_UserWise = "";
                }
                else
                {
                    CompanyName_UserWise = CompanyName.Trim();
                }
            }

            string _GetBaseCompanyId = _utilities.GetScalarSecurityData($"Select BaseCompanyId from EXTERNAL_SECURITY_AssociatedCompanies WHERE AssociatedCompanyId = {_CompanyId}").ToString();

            dtOthers.Rows.Add(_Culture, _Culture, _UserIdCode, _UserId, _UserId,
                _UserId.Trim() + ".xml", "", Convert.ToString(Convert.ToInt32(_CompanyId.Trim())),
                CompanyName, CompanyName_UserWise, _GetBaseCompanyId.Trim(), strEmpId.Trim(), GetPGIdsSecurity(_UserId),
                GetPGIdsSecurity(_UserId), strEmpName.Trim(), _utilities.GetCompanyLogo(_CompanyId));

            string JSONresult;
            JSONresult = JsonConvert.SerializeObject(this.GetReportFormatData(_CompanyId.Trim()));
            _utilities.SetKeyInRedisAsync("ReportFormatData", JSONresult, Prefix);

            JSONresult = JsonConvert.SerializeObject(dtOthers);
            _utilities.SetKeyInRedisAsync("OtherData", JSONresult, Prefix);

            _RedirectPath = _formLocation;
            _utilities.SetKeyInRedisAsync("IsLogin", "true", Prefix);
            return _RedirectPath;
        }


        public DataTable GetReportFormatData(string CompanyId)
        {
            DataTable dsReport = _utilities.GetReportFormat(CompanyId);

            return dsReport;
        }
        public string GetBaseCompanyIdData(string LoginCompanyId)
        {
            DataServices objdataservice = new DataServices(_configuration);
            DataSet objDataSet = new DataSet();
            string query = "SELECT BaseCompanyId " +
                           "FROM EXTERNAL_SECURITY_AssociatedCompanies " +
                           "WHERE AssociatedCompanyId = @LoginCompanyId";
            // Assuming you're using ADO.NET with SQL Server
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@LoginCompanyId", LoginCompanyId);
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(objDataSet);
                    }
                }
            }

            if (objDataSet.Tables.Count > 0 && objDataSet.Tables[0].Rows.Count > 0)
            {
                string baseCompanyId = objDataSet.Tables[0].Rows[0]["BaseCompanyId"].ToString();
                return string.IsNullOrEmpty(baseCompanyId) ? string.Empty : baseCompanyId;
            }
            else
            {
                return string.Empty;
            }
        }
        public SecurityUserData GetSecurityUserData(string userId)
        {
            string connectionString = _configuration.GetConnectionString("SecurityConnectionString");
            SecurityUserData securityUserData = new SecurityUserData();

            string key = GetEncryptionKey();

            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand("SP_GetUser", con);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@key", key);
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    cmd.Parameters.Add("@GetUserId", SqlDbType.NVarChar, 35).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@GetUserName", SqlDbType.NVarChar, 100).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@GetEncUserId", SqlDbType.VarBinary, 1000).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@GetDecUserId", SqlDbType.NVarChar, 1000).Direction = ParameterDirection.Output;

                    con.Open();
                    cmd.ExecuteNonQuery();

                    securityUserData.UserIdCode = Convert.ToString(cmd.Parameters["@GetUserId"].Value);
                    securityUserData.UserName = Convert.ToString(cmd.Parameters["@GetUserName"].Value);
                    securityUserData.ENCUserID = cmd.Parameters["@GetEncUserId"].Value as byte[];
                    securityUserData.DECUserId = Convert.ToString(cmd.Parameters["@GetDecUserId"].Value);
                }
            }
            catch (SqlException exception)
            {
                // Handle the exception as needed
            }

            return securityUserData;
        }
        public DataTable GetFormMenuItemsWithOutRights(string companyId)
        {
            if (string.IsNullOrEmpty(companyId))
            {
                companyId = "-1";
            }

            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand("SELECT * FROM FormMenuItemsWithOutRights WHERE ParentItem = 'CustomizedForms' AND companyId = @CompanyId AND canview = 1", connection))
                {
                    command.Parameters.AddWithValue("@CompanyId", companyId);

                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }
        public string GetPrefix(string _TerminalId)
        {
            string _Prefix = String.Empty;
            Object obj = new object();
            obj = _utilities.GetScalarDataForSecurity("SELECT top 1 UniqueKey FROM tblUniqueKeyForRedis Where EntTerminal='" + _TerminalId + "' order by  Id desc");
            if (obj != null)
            {
                _Prefix = obj.ToString();
            }
            return _Prefix;
        }
        public DataSet GetAllCompanies()
        {

            DataServices objdataservice = new DataServices(_configuration);
            DataSet ds = new DataSet();            
            //string str = objdataservice.ExecuteSecurityReader("select ccode as [Id],CompanyName as [Name] from EXTERNAL_SECURITY_COMPANYMANAGEMENT where delflag = 'N' order by CompanyName", ref ds);
            string str = objdataservice.ExecuteReader("select ccode as [Id],CompanyName as [Name] from EXTERNAL_SECURITY_COMPANYMANAGEMENT where delflag = 'N' order by CompanyName", ref ds);
            return ds;
        }
        public void InsertUniqueId(string userId, string IP)
        {
            try
            {
                string _Query = "";

                _Query = "Insert Into tblUniqueKeyForRedis (UniqueKey,EntTerminal,EntTerminalIP)"
                    + "values ('" + GenerateUniqueKey(userId) + "','" + IP + "','" + IP + "')";

                //Hammad Query for v5 Start
                try
                {
                    object obj = new object();
                    _dataservice.ExecuteStatementForSecurity(_Query, ref obj, 2);

                }
                catch (SqlException exception)
                {

                }
            }
            catch (Exception ex)
            {
                // Handle the exception
            }
        }
        public string CheckAuthentication(string _UserId, string _UserIdCode, string _UserPwd, string _CompanyId,
                                   string _CompanyName, string _AppId, string _Culture, string _prefix, string AppCode)
        {
            string _redirectLink = string.Empty;
            string UserId = _UserId;
            string UserPwd = _UserPwd;

            UserId = UserId.Replace("'", "");
            UserPwd = UserPwd.Replace("'", "");

            if (!string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(UserPwd))
            {
                // Assuming you have a method named AuthenticateUser in the Security class
                // that performs user authentication.
                _redirectLink = SetValues(_UserId, _UserIdCode, _CompanyId, _CompanyName, _Culture, _AppId, _prefix, AppCode);
            }
            else
            {
                _redirectLink = "ErrorPageUnSuccessfulMapping";
                return _redirectLink;
            }

            return _redirectLink;
        }
        public DataTable GetMenuData(string applicationCode, string paramLoginUserId, string cCode, string loginCulture, IConfiguration configuration)
        {
            DataSet ds = new DataSet();
            try
            {
                DataServices dataservice = new DataServices(configuration);
                StringBuilder sb = new StringBuilder();
                string result = string.Empty;

                sb.Append("EXEC sp_Security_GetFormRightsForUser @ApplicationCode  = '" + applicationCode + "',  @Param_LoginUserId  = '" + paramLoginUserId + "',  @CCode = '" + cCode + "',@separatorRequired  = 0, @LoginCulture  = '" + loginCulture + "'");
                result = dataservice.ExecuteReader(sb.ToString(), ref ds);
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }
            return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }
         
        public bool GetUserData(String _UserId,
        String _CompanyId,
        out String _strEmpId,
        out String _strPGid,
        out string _strEmpName)
        {
            // string connectionString = ConfigurationManager.ConnectionStrings["HCMSEntities"].ConnectionString;

            // dataservice.BeginProcess(connectionString);
            DataSet UserMapping = new DataSet();

            string strQuery = "SELECT * FROM EXTERNAL_SECURITY_USERMAPPING WHERE UserId=" + _UserId + "";
            string strTemp = _dataservice.ExecuteReader(strQuery, ref UserMapping);

            _strEmpId = string.Empty;
            _strPGid = string.Empty;
            _strEmpName = string.Empty;

            if (UserMapping != null && UserMapping.Tables[0].Rows.Count != 0)
            {
                DataSet _loginResult = new DataSet();

                string expres = "Convert(Ccode,'System.Int32')= " + _CompanyId.ToString();

                DataRow[] Newrows = UserMapping.Tables[0].Select(expres);
                if (Newrows.Length > 0)
                {
                    DataRow dr = Newrows[0];
                    String strResult = _dataservice.GetDataWithClause(" * ", "tblEmployee",
                   " EmpCode = '" + dr["EmployeeId"] + "' And CompanyId = " + Convert.ToInt32(dr["Ccode"].ToString()).ToString(),
                   ref _loginResult);

                }
                else
                {

                    String strResult = _dataservice.GetDataWithClause(" * ", "tblEmployee",
                     " EmpCode = '" + UserMapping.Tables[0].Rows[0]["EmployeeId"] + "' And CompanyId = " + Convert.ToInt32(UserMapping.Tables[0].Rows[0]["CCode"]).ToString(),
                     ref _loginResult);

                }
                if (_loginResult.Tables[0].Rows.Count > 0)
                {
                    _strEmpId = _loginResult.Tables[0].Rows[0]["EmpId"].ToString();

                    StringBuilder sbAllPGids = new StringBuilder();

                    foreach (DataRow dtRow in _loginResult.Tables[0].Rows)
                    {
                        if (sbAllPGids.Length != 0 && dtRow["PGIds"].ToString().Trim().Length != 0) sbAllPGids.Append(",");
                        sbAllPGids.Append(dtRow["PGIds"].ToString());
                    }
                    _strPGid = sbAllPGids.ToString().Trim(',');

                    _strEmpName = _loginResult.Tables[0].Rows[0]["FirstName"].ToString() +
                        " " +
                        _loginResult.Tables[0].Rows[0]["LastName"].ToString();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            //return true;
        }

        public string GetPGIDsMultiCompany(string userId)
        {
            string strPGid = "0";

            try
            {
                object objPGIDs = null;

                StringBuilder queryBuilder = new StringBuilder();
                queryBuilder.Append("DECLARE @PGIds NVARCHAR(MAX) = ''");
                queryBuilder.Append("Select");
                queryBuilder.Append(" @PGIds = @PGIds + y.PGIds + ',' From EXTERNAL_SECURITY_USERMAPPING x");
                queryBuilder.Append(" INNER JOIN tblEmployee y ON CAST(y.EmpCode AS INT) = CAST(x.EmployeeId AS INT) AND x.CCode = y.CompanyId AND y.Active = 1");
                queryBuilder.Append(" Where x.UserId = N'" + userId + "'");
                queryBuilder.Append(" SELECT LTRIM(RTRIM(LEFT(@PGIds, LEN(@PGIds) -1))) PGIds");

                string result = _dataservice.ExecuteStatement(queryBuilder.ToString(), ref objPGIDs, 1);

                if (objPGIDs != null && Convert.ToString(objPGIDs).Length > 0)
                    strPGid = Convert.ToString(objPGIDs);

                return strPGid;
            }
            catch (Exception)
            {
                return strPGid;
            }
        }
        public string GetPGIDsMultiCompanySecurity(string userId)
        {
            string strPGid = "0";
            string DBName = _configuration.GetSection("CorsSettings:HCMSDBName").Value;

            try
            {
                object objPGIDs = null;


                StringBuilder queryBuilder = new StringBuilder();
                //$"Exec SP_EXTERNAL_SECURITY_USERMAPPING '{HCMSDBName}', {_UserId}";
                //object objPGIDs = null;
                queryBuilder.Append($"Exec SP_GetPGIDs '{DBName}', '{userId}'");


                //queryBuilder.Append("DECLARE @PGIds NVARCHAR(MAX) = ''");
                //queryBuilder.Append("Select");
                //queryBuilder.Append(" @PGIds = @PGIds + y.PGIds + ',' From EXTERNAL_SECURITY_USERMAPPING x");
                //queryBuilder.Append(" INNER JOIN " + DBName + ".dbo.tblEmployee y ON CAST(y.EmpCode AS INT) = CAST(x.EmployeeId AS INT) AND x.CCode = y.CompanyId AND y.Active = 1");
                //queryBuilder.Append(" Where x.UserId = N'" + userId + "'");
                //queryBuilder.Append(" SELECT LTRIM(RTRIM(LEFT(@PGIds, LEN(@PGIds) -1))) PGIds");





                //StringBuilder queryBuilder = new StringBuilder();
                //queryBuilder.Append("DECLARE @PGIds NVARCHAR(MAX) = ''");
                //queryBuilder.Append("Select");
                //queryBuilder.Append(" @PGIds = @PGIds + y.PGIds + ',' From EXTERNAL_SECURITY_USERMAPPING x");
                //queryBuilder.Append(" INNER JOIN tblEmployee y ON CAST(y.EmpCode AS INT) = CAST(x.EmployeeId AS INT) AND x.CCode = y.CompanyId AND y.Active = 1");
                //queryBuilder.Append(" Where x.UserId = N'" + userId + "'");
                //queryBuilder.Append(" SELECT LTRIM(RTRIM(LEFT(@PGIds, LEN(@PGIds) -1))) PGIds");

                string result = _dataservice.ExecuteSecurityStatement(queryBuilder.ToString(), ref objPGIDs, 1);

                if (objPGIDs != null && Convert.ToString(objPGIDs).Length > 0)
                    strPGid = Convert.ToString(objPGIDs);

                return strPGid;
            }
            catch (Exception)
            {
                return strPGid;
            }
        }
        public String GetPGIds(string UserId)
        {

            String _PGIds = String.Empty;

            if (!String.IsNullOrEmpty(UserId))
            {
                _PGIds = GetPGIDsMultiCompany(UserId);

            }



            if (_PGIds.Length == 0)
                _PGIds = "0";
            return _PGIds;


        }
        public String GetPGIdsSecurity(string UserId)
        {

            String _PGIds = String.Empty;

            if (!String.IsNullOrEmpty(UserId))
            {
                _PGIds = GetPGIDsMultiCompanySecurity(UserId);

            }



            if (_PGIds.Length == 0)
                _PGIds = "0";
            return _PGIds;


        }

        public string GetUserId(string prefix)
        {
            string userId = string.Empty;
            
            string loginDetails = _utilities.GetKeyInRedis(prefix + "LoginDetail", _configuration);

            if (!string.IsNullOrEmpty(loginDetails))
            {
                DataTable dt = JsonConvert.DeserializeObject<DataTable>(loginDetails);

                var results = from myRow in dt.AsEnumerable()
                              select myRow["UserId"].ToString();

                if (results.Any())
                {
                    userId = SoftronicCrypto.Decrypt(results.First(), Constant.passPhrase, Constant.saltValue);
                }
            }

            return userId;
        }

        public bool CheckPermission(string formId, string permission, string prefix)
        {
            if (string.IsNullOrEmpty(formId))
            {
                return false;
            }
            
            string userId = GetUserId(prefix);
            bool hasPermission = false;
            string companyId = _utilities.GetCompanyId(prefix);
            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;

            string totalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync($"{userId}{applicationId}{companyId}GenerateMenuTotalChunks");
            //string totalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(userId + "PharmaCRMv2" + companyId + "GenerateMenuTotalChunks");

            if (!string.IsNullOrEmpty(totalPermissionChunks))
            {
                for (int i = 1; i <= Convert.ToInt32(totalPermissionChunks); i++)
                {
                    string getPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync($"{userId}{applicationId}{companyId}{i}GenerateMenu");

                    //string getPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(userId + "PharmaCRMv2" + companyId + i + "GenerateMenu");

                    if (!string.IsNullOrEmpty(getPermissionChunks))
                    {
                        DataTable dt = JsonConvert.DeserializeObject<DataTable>(getPermissionChunks);

                        switch (permission)
                        {
                            case "CanInsert":
                                var insertResults = from myRow in dt.AsEnumerable()
                                                    where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                    select myRow["CanInsert"].ToString();

                                if (insertResults.Any() && insertResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanView":
                                var viewResults = from myRow in dt.AsEnumerable()
                                                  where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                  select myRow["CANVIEW"].ToString();

                                if (viewResults.Any() && viewResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanEdit":
                                var editResults = from myRow in dt.AsEnumerable()
                                                  where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                  select myRow["CanEdit"].ToString();

                                if (editResults.Any() && editResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanDelete":
                                var deleteResults = from myRow in dt.AsEnumerable()
                                                    where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                    select myRow["CanDelete"].ToString();

                                if (deleteResults.Any() && deleteResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanProcessed":
                                var processedResults = from myRow in dt.AsEnumerable()
                                                       where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                       select myRow["CanProcessed"].ToString();

                                if (processedResults.Any() && processedResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanViewReport":
                                var viewReportResults = from myRow in dt.AsEnumerable()
                                                        where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                        select myRow["CanViewReport"].ToString();

                                if (viewReportResults.Any() && viewReportResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;
                        }

                        if (hasPermission)
                        {
                            break;
                        }
                    }
                }
            }

            return hasPermission;
        }
        public bool CheckPermission(string formId, string permission, string prefix, String clientIP, string AppCode)
        {
            if (string.IsNullOrEmpty(formId))
            {
                return false;
            }
            
            string userId = GetUserId(prefix);
            bool hasPermission = false;
            string companyId = _utilities.GetCompanyId(clientIP);
            string applicationId = AppCode;

            string totalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync($"{userId}{applicationId}{companyId}GenerateMenuTotalChunks");

            //string totalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(userId + "PharmaCRMv2" + companyId + "GenerateMenuTotalChunks");

            if (!string.IsNullOrEmpty(totalPermissionChunks))
            {
                for (int i = 1; i <= Convert.ToInt32(totalPermissionChunks); i++)
                {
                    string getPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync($"{userId}{applicationId}{companyId}{i}GenerateMenu");

                    //string getPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(userId + "PharmaCRMv2" + companyId + i + "GenerateMenu");

                    if (!string.IsNullOrEmpty(getPermissionChunks))
                    {
                        DataTable dt = JsonConvert.DeserializeObject<DataTable>(getPermissionChunks);

                        switch (permission)
                        {
                            case "CanInsert":
                                var insertResults = from myRow in dt.AsEnumerable()
                                                    where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                    select myRow["CanInsert"].ToString();

                                if (insertResults.Any() && insertResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanView":
                                var viewResults = from myRow in dt.AsEnumerable()
                                                  where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                  select myRow["CANVIEW"].ToString();

                                if (viewResults.Any() && viewResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanEdit":
                                var editResults = from myRow in dt.AsEnumerable()
                                                  where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                  select myRow["CanEdit"].ToString();

                                if (editResults.Any() && editResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanDelete":
                                var deleteResults = from myRow in dt.AsEnumerable()
                                                    where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                    select myRow["CanDelete"].ToString();

                                if (deleteResults.Any() && deleteResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanProcessed":
                                var processedResults = from myRow in dt.AsEnumerable()
                                                       where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                       select myRow["CanProcessed"].ToString();

                                if (processedResults.Any() && processedResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;

                            case "CanViewReport":
                                var viewReportResults = from myRow in dt.AsEnumerable()
                                                        where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
                                                        select myRow["CanViewReport"].ToString();

                                if (viewReportResults.Any() && viewReportResults.First() == "1")
                                {
                                    hasPermission = true;
                                }
                                break;
                        }

                        if (hasPermission)
                        {
                            break;
                        }
                    }
                }
            }

            return hasPermission;
        }


        //public bool CheckPermission(string formId, string permission, string prefix)
        //{
        //    if (string.IsNullOrEmpty(formId))
        //    {
        //        return false;
        //    }

        //    string userId = GetUserId(prefix); // Assuming you have a method to get the user ID
        //    bool hasPermission = false;
        //    string companyId = _utilities.GetCompanyId();
        //    string totalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(userId + "PharmaCRMv2" + companyId + "GenerateMenuTotalChunks");

        //    if (!string.IsNullOrEmpty(totalPermissionChunks))
        //    {
        //        for (int i = 1; i <= Convert.ToInt32(totalPermissionChunks); i++)
        //        {
        //            string getPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(userId + "PharmaCRMv2" + companyId + i + "GenerateMenu");

        //            if (!string.IsNullOrEmpty(getPermissionChunks))
        //            {
        //                DataTable dt = JsonConvert.DeserializeObject<DataTable>(getPermissionChunks);

        //                switch (permission)
        //                {
        //                    case "CanInsert":
        //                        hasPermission = CheckPermission(dt, formId, "CanInsert");
        //                        break;
        //                    case "CanView":
        //                        hasPermission = CheckPermission(dt, formId, "CANVIEW");
        //                        break;
        //                    case "CanEdit":
        //                        hasPermission = CheckPermission(dt, formId, "CanEdit");
        //                        break;
        //                    case "CanDelete":
        //                        hasPermission = CheckPermission(dt, formId, "CanDelete");
        //                        break;
        //                    case "CanProcessed":
        //                        hasPermission = CheckPermission(dt, formId, "CanProcessed");
        //                        break;
        //                    case "CanViewReport":
        //                        hasPermission = CheckPermission(dt, formId, "CanViewReport");
        //                        break;
        //                    default:
        //                        // Handle other permissions as needed
        //                        break;
        //                }

        //                if (hasPermission)
        //                {
        //                    break;
        //                }
        //            }
        //        }
        //    }

        //    return hasPermission;
        //}

        //private bool CheckPermission(DataTable dt, string formId, string permissionColumn)
        //{
        //    var results = from myRow in dt.AsEnumerable()
        //                  where myRow.Field<string>("FORMID").ToUpper() == formId.ToUpper()
        //                  select myRow[permissionColumn].ToString();

        //    return results.Any(result => result == "1");
        //}



        private string SetValues(string _UserId, string _UserIdCode, string _CompanyId, string _CompanyName, string _Culture, string Application, string _prefix, string AppCode)
        {
            string Code = _CompanyId;
            
            string _RedirectLink = string.Empty;
            string strEmpId = string.Empty, strPGId = string.Empty, strEmpName = string.Empty,
            strPrefix = string.Empty;
            string CCode = _CompanyId;

            strPrefix = _prefix;
            string applicationId = AppCode;

            if (!GetUserData($"N'{_UserId.ToUpper()}'", _CompanyId, out strEmpId, out strPGId, out strEmpName))
            {
                _RedirectLink = "ErrorPageUnSuccessfulMapping";
                return _RedirectLink;
            }

            try
            {
                string _TotalMenuChunks = _utilities.GetTotalChunksInRedis($"{_UserId}{applicationId}{_CompanyId}GenerateMenuTotalChunks", _configuration);

                //string _TotalMenuChunks = _utilities.GetTotalChunksInRedis($"{_UserId}PharmaCRMv2{_CompanyId}GenerateMenuTotalChunks",_configuration);

                if (string.IsNullOrEmpty(_TotalMenuChunks))
                //if (1==1)
                {                     
                    DataTable dtMenu = GetMenuData(applicationId, _UserId, _CompanyId, _Culture, _configuration);

                    //DataTable dtMenu = logComp.GetMenuData("PharmaCRMv2", _UserId, _CompanyId, _Culture,_configuration);

                    DataTable dtSubMenu = new DataTable();

                    int _totalChunksMenu = 0;

                    if (dtMenu != null && dtMenu.Rows.Count > 0)
                    {
                        if (_utilities.RemoveAllKeyInRedis($"{_UserId}{applicationId}{_CompanyId}", _configuration))

                        //if (_utilities.RemoveAllKeyInRedis($"{_UserId}PharmaCRMv2{_CompanyId}",_configuration))
                        {
                            string _Check = "True";
                        }

                        var totalRows = dtMenu.Rows.Count;
                        int _reminder = totalRows % 100;
                        totalRows = totalRows - (totalRows % 100);

                        if (totalRows > 100)
                        {
                            int halfway = totalRows / 100;
                            int _skip = 0;
                            var _GenerateMenu = dtMenu;

                            for (int i = 1; i <= halfway; i++)
                            {
                                if (i == 1)
                                {
                                    _GenerateMenu = dtMenu.AsEnumerable().Take(100).CopyToDataTable();
                                }
                                else
                                {
                                    _GenerateMenu = dtMenu.AsEnumerable().Skip(_skip).Take(100).CopyToDataTable();
                                }

                                dtSubMenu = (DataTable)_GenerateMenu;
                                int a = dtSubMenu.Rows.Count;
                                string JSONresultGenerateMenu = JsonConvert.SerializeObject((DataTable)_GenerateMenu);
                                _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, $"{_UserId}{applicationId}{_CompanyId}{i}");

                                //_utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, $"{_UserId}PharmaCRMv2{_CompanyId}{i}");
                                _skip = _skip + 100;

                                if (_reminder != 0)
                                {
                                    if (_skip == totalRows)
                                    {
                                        i = i + 1;
                                        _GenerateMenu = dtMenu.AsEnumerable().Skip(_skip).Take(_reminder).CopyToDataTable();
                                        JSONresultGenerateMenu = JsonConvert.SerializeObject((DataTable)_GenerateMenu);
                                        _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, $"{_UserId}{applicationId}{_CompanyId}{i}");


                                        //_utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, $"{_UserId}PharmaCRMv2{_CompanyId}{i}");
                                    }
                                }

                                _totalChunksMenu = i;
                            }
                            _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", _totalChunksMenu.ToString(), $"{_UserId}{applicationId}{_CompanyId}");

                            //_utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", _totalChunksMenu.ToString(), $"{_UserId}PharmaCRMv2{_CompanyId}");
                        }
                        else
                        {
                            string JSONresultGenerateMenu = JsonConvert.SerializeObject(dtMenu);
                            _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, $"{_UserId}{applicationId}{_CompanyId}1");
                            _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", "1", $"{_UserId}{applicationId}{_CompanyId}");


                            // _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, $"{_UserId}PharmaCRMv2{_CompanyId}1");
                            // _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", "1", $"{_UserId}PharmaCRMv2{_CompanyId}");
                        }
                    }
                }
            }
            catch (Exception excp)
            {
                _RedirectLink = $"ErrorPage.aspx?qsAccess=PermissionsNotSet&err={excp.Message}";
                return _RedirectLink;
            }

            DataTable dtOthers = new DataTable();
            dtOthers.Columns.Add("UICulture", typeof(string));
            dtOthers.Columns.Add("Culture", typeof(string));
            dtOthers.Columns.Add("UserIdCode", typeof(string));
            dtOthers.Columns.Add("UserId", typeof(string));
            dtOthers.Columns.Add("userid", typeof(string));
            dtOthers.Columns.Add("FileName", typeof(string));
            dtOthers.Columns.Add("FilePath", typeof(string));
            dtOthers.Columns.Add("CompanyId", typeof(string));
            dtOthers.Columns.Add("CompanyName", typeof(string));
            dtOthers.Columns.Add("CompanyName_UserWise", typeof(string));
            dtOthers.Columns.Add("BaseCompanyId", typeof(string));
            dtOthers.Columns.Add("EmpId", typeof(string));
            dtOthers.Columns.Add("PGid", typeof(string));
            dtOthers.Columns.Add("PGIDS", typeof(string));
            dtOthers.Columns.Add("EmpName", typeof(string));
            dtOthers.Columns.Add("Logo", typeof(string));            

            string CompanyName = _utilities.GetScalarSecurityData($"Select ISNULL(LTRIM(RTRIM(CompanyShortName)), '') from vCompanyManagement WHERE CCode = {_CompanyId}").ToString();
            string CompanyName_UserWise = string.Empty;
            string _GetBaseCompanyId = _utilities.GetScalarSecurityData($"Select BaseCompanyId from EXTERNAL_SECURITY_AssociatedCompanies WHERE AssociatedCompanyId = {_CompanyId}").ToString();

            if (!string.IsNullOrEmpty(CompanyName))
            {
                // Additional logic related to CompanyName if needed
            }
            dtOthers.Rows.Add(_Culture, _Culture, _UserIdCode, _UserId, _UserId,
                              $"{_UserId.Trim()}.xml", "", Convert.ToString(Convert.ToInt32(_CompanyId.Trim())),
                              CompanyName, CompanyName_UserWise, _GetBaseCompanyId, strEmpId.Trim(), "",
                              GetPGIdsSecurity(_UserId), strEmpName.Trim(), "");

            string JSONresult;
            JSONresult = JsonConvert.SerializeObject(dtOthers);
            _utilities.SetKeyInRedisAsyncs("OtherData", JSONresult, strPrefix);

            _RedirectLink = "MainPage.aspx";
            _utilities.SetKeyInRedisAsyncs("IsLogin", "true", _prefix);

            return _RedirectLink;
        }

        //public async Task<DataTable> GetMenuDataAsync(string _ApplicationCode, string _Param_LoginUserId, string _CCode, string _LoginCulture)
        //{
        //    DataSet ds = new DataSet();
        //    try
        //    {
        //        DataServices dataservice = new DataServices(_configuration);
        //        StringBuilder sbr = new StringBuilder();
        //        string result = string.Empty;

        //        sbr.Append("EXEC sp_Security_GetFormRightsForUser @ApplicationCode  = '" + _ApplicationCode + "',  @Param_LoginUserId  = '" + _Param_LoginUserId + "',  @CCode = '" + _CCode + "',@separatorRequired  = 0, @LoginCulture  = '" + _LoginCulture + "'");
        //        result = await dataservice.ExecuteReaderAsync(Convert.ToString(sbr), ref ds);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle the exception appropriately
        //    }
        //    return ds.Tables[0];
        //}



        public string[] GetFormName(string _FormPath, string _Prefix)
        {
            
            _FormPath = _FormPath.Substring(1);
            string Prefix = _Prefix;// _utilities.GetPrefix(_Prefix);
            string _UserId = GetUserId(Prefix); //"moiz.ismaili"; //
            string[] _FormName = new string[2];
            string _CompanyId = _utilities.GetCompanyId(_Prefix);

            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            string _TotalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync($"{_UserId}{applicationId}{_CompanyId}GenerateMenuTotalChunks");
            // string _TotalPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(_UserId + "PharmaCRMv2" + _CompanyId + "GenerateMenuTotalChunks");

            if (!String.IsNullOrEmpty(_TotalPermissionChunks))
            {
                for (int i = 1; i <= Convert.ToInt32(_TotalPermissionChunks); i++)
                {
                    string _GetPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync($"{_UserId}{applicationId}{_CompanyId}{i}GenerateMenu");

                    //                    string _GetPermissionChunks = _utilities.GetUserRightsInRedisWithoutAsync(_UserId + "PharmaCRMv2" + _CompanyId + i + "GenerateMenu");

                    if (!String.IsNullOrEmpty(_GetPermissionChunks))
                    {
                        DataTable dt = (DataTable)JsonConvert.DeserializeObject(_GetPermissionChunks, (typeof(DataTable)));

                        if (!string.IsNullOrEmpty(_FormPath) && _FormPath == "Favourites/favourite")
                        {
                            string[] FormId = _FormPath.Split('/');

                            var results = from myRow in dt.AsEnumerable()
                                          where myRow.Field<string>("FormId").ToLower() == FormId[0].ToLower().ToString()
                                          select myRow["Description"].ToString();

                            if (Convert.ToInt32(results.Count()) != 0)
                            {
                                foreach (string Name in results)
                                {
                                    _FormName[0] = Name;
                                    _FormName[1] = "Favourites";
                                }
                                break;
                            }
                        }
                        else
                        {
                            var results = from myRow in dt.AsEnumerable()
                                          where myRow.Field<string>("FORMPATH").ToLower() == _FormPath.ToLower()
                                          select myRow["Description"].ToString();

                            if (Convert.ToInt32(results.Count()) != 0)
                            {
                                foreach (string Name in results)
                                {
                                    _FormName[0] = Name;
                                }
                                //break;
                            }

                            var results2 = from myRow in dt.AsEnumerable()
                                           where myRow.Field<string>("FORMPATH").ToLower() == _FormPath.ToLower()
                                           select myRow["FormId"].ToString();

                            if (Convert.ToInt32(results2.Count()) != 0)
                            {
                                foreach (string Name in results2)
                                {
                                    _FormName[1] = Name;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return _FormName;
        }


        public string GetLogOut(string clientIP)
        {
            
            try
            {
                string msg = "Prefix not found";
                string prefix = _utilities.GetPrefix(clientIP);
                string userId = _utilities.GetUserid(clientIP);

                if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(userId))
                {
                    _utilities.RemoveAllKeyInRedis(prefix, _configuration);

                    // Remove specific keys
                    _utilities.RemoveKeyInRedis(prefix + "LoginDetail");
                    _utilities.RemoveKeyInRedis(prefix + "DecryptLoginDetail");
                    _utilities.RemoveKeyInRedis(prefix + "KeepMeSignin");
                    _utilities.RemoveKeyInRedis(userId + "ReportFormatData");

                    _utilities.RemoveAllKeyInRedis(prefix, _configuration);

                    DataServices ds = new DataServices(_configuration);
                    object obj = new object();
                    ds.ExecuteStatement($"Delete FROM tblUniqueKeyForRedis Where UniqueKey='{prefix}'", ref obj, 2);

                    msg = "Successful";
                }

                return msg;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }


        


        private string GenerateUniqueKey(string _UserId)
        {
            Guid g = Guid.NewGuid();
            string GuidString = Convert.ToBase64String(g.ToByteArray());
            GuidString = GuidString.Replace("=", "");
            GuidString = GuidString.Replace("+", "");
            GuidString = Sha256(GuidString);

            return GuidString;
        }
        public string Sha256(string randomString)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] crypto = sha256.ComputeHash(Encoding.UTF8.GetBytes(randomString));
                StringBuilder hash = new StringBuilder();

                foreach (byte theByte in crypto)
                {
                    hash.Append(theByte.ToString("x2"));
                }

                return hash.ToString();
            }
        }

        


        public SaveFCMToken SaveLoginSession(RefreshTokenRequest updatedToken, string userId, int empId, string newJwtToken, int deviceType)
        {
            string connectionString = _configuration.GetConnectionString("connectionString");
            SaveFCMToken response = new SaveFCMToken();
            DeleteLogoutSession(updatedToken.UDID);

            using (TransactionScope scope = new TransactionScope())
            {
                try
                {
                    using (SqlConnection con = new SqlConnection(connectionString))
                    {
                        con.Open();
                        string query = "DD_SP_SaveFcmTokenAndUserDetails";
                        using (SqlCommand cmd = new SqlCommand(query, con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@FcmToken", updatedToken.UpdatedFcmToken);
                            cmd.Parameters.AddWithValue("@Udid", updatedToken.UDID);
                            cmd.Parameters.AddWithValue("@EmpId", empId);
                            cmd.Parameters.AddWithValue("@Token", newJwtToken);
                            cmd.Parameters.AddWithValue("@DeviceType", deviceType);
                            cmd.ExecuteNonQuery();
                        }

                        string selectQuery = @"SELECT TOP 1 * FROM DAS_DD_SaveFCMToken ORDER BY Id DESC";
                        using (SqlCommand selectCmd = new SqlCommand(selectQuery, con))
                        {
                            using (SqlDataReader reader = selectCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    response = new SaveFCMToken
                                    {
                                        UserId = reader.TryGetValue<string>("UserId"),
                                        FcmToken = reader.TryGetValue<string>("FCM_Token"),
                                        Udid = reader.TryGetValue<string>("UDID"),
                                        EmpId = reader.TryGetValue<int>("EmpId"),
                                        JwtToken = reader.TryGetValue<string>("Token"),
                                        LoginTime = reader.TryGetValue<DateTime>("LoginTime"),
                                        DeviceType = reader.TryGetValue<int>("DeviceType")
                                    };
                                }
                            }
                        }
                    }


                    UserLoggedIn("Das_MobileLogin", "Y", userId, updatedToken.UDID);
                    scope.Complete();

                }
                catch (Exception ex)
                {
                    throw new Exception("An error occurred while saving FCM token.", ex);
                }
            }

            return response;
        }

        public bool UserLoggedIn(string appId, string loggedIn, string loginUser, string udId)
        {
            bool isvalid = false;
            try
            {
                if (Convert.ToInt32(CheckifLoggedOut(udId, appId, Convert.ToString(loggedIn), loginUser)) < 1)
                {

                    string Query = "Insert Into DAS_DDSecurityLogs (LoginDateTime , LoginUserId ,AppId , IsLogin , UdId) Values (GetDate() , @LoginUser , @AppId , @LoggedIn , @UdId)";
                    using (var connection = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                    {
                        // Create the command with the SQL query
                        using (var command = new SqlCommand(Query, connection))
                        {
                            // Add parameters to the command
                            command.Parameters.AddWithValue("@LoginUser", loginUser ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@UdId", udId);
                            command.Parameters.AddWithValue("@AppId", appId ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@LoggedIn", loggedIn);

                            // Open the connection
                            connection.Open();

                            // Execute the command
                            command.ExecuteNonQuery();
                        }
                    }
                    isvalid = true;
                }
            }
            catch (Exception ex)
            {
                
            }
            return isvalid;
        }

        public int CheckifLoggedOut(string udId, string appId, string loggedIn, string loginUser)
        {
            int ATCOunt = 0;
            try
            {
                string Query = $"Select Count(*) From DAS_DDSecurityLogs Where UdId = '{udId}' AND LoggedIn = '{loggedIn}' AND AppId = '{appId}' AND LoginUserId = '{loginUser}'";
                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    SqlCommand cmd = new SqlCommand(Query, con);

                    con.Open();
                    ATCOunt = (int)cmd.ExecuteScalar();
                    con.Close();
                }
            }
            catch (Exception ex)
            {
                
            }
            return ATCOunt;
        }

        public int DeleteLogoutSession(string udid)
        {
            string connectionString = _configuration.GetConnectionString("connectionString");
            int rowsAffected = 0;
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string _sp = "DD_SP_DeleteLogoutSession";
                SqlCommand cmd = new SqlCommand(_sp, con);
                cmd.CommandType = CommandType.StoredProcedure;
                con.Open();
                cmd.Parameters.AddWithValue("@UDID", udid);

                rowsAffected = cmd.ExecuteNonQuery();
            }

            UserLoggedOut(udid, "Das_MobileLogin");
            return rowsAffected;
        }
        public bool UserLoggedOut(string UdId, string appId)
        {
            bool isvalid = false;
            DataTable dt = new DataTable();
            try
            {
                if (Convert.ToInt32(CheckifLoggedIn(appId, UdId)) > 0)
                {
                    string Query = $"Select top(1) * From DAS_DDSecurityLogs Where LogoutdateTime IS NULL AND AppId = '{appId}' AND UdId = '{UdId}' AND IsLogin = 'Y' Order by LoginDateTime desc";

                    using (var connection = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                    {
                        connection.Open();
                        // Create the command with the SQL query
                        using (var command = new SqlCommand(Query, connection))
                        {
                            using (SqlDataAdapter da = new SqlDataAdapter(command))
                            {
                                da.Fill(dt);
                            }
                            if (dt != null && dt.Rows.Count > 0)
                            {
                                int? id = dt.Rows[0]["Id"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["Id"]) : (int?)null;
                                string UpdateQuery = $"Update DAS_DDSecurityLogs Set LogoutdateTime = GetDate() , IsLogin = 'N' Where Id = '{id}'";
                                command.CommandText = UpdateQuery;
                                int res = command.ExecuteNonQuery();
                                if (res > 0)
                                    isvalid = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(String.Format("{0} {1}", "User LoggedOut Error :", ex.Message), ex);
            }
            return isvalid;
        }

        public int CheckifLoggedIn(string appId, string UdId)
        {
            int AtCount = 0;
            try
            {
                string Query = $"Select Count(*) From DAS_DDSecurityLogs Where AppId = '{appId}'  AND  UdId = '{UdId}'";
                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    SqlCommand cmd = new SqlCommand(Query, con);

                    con.Open();
                    AtCount = (int)cmd.ExecuteScalar();
                    con.Close();
                }

            }
            catch (Exception ex)
            {
                LogHelper.Error(String.Format("{0} {1}", "Check if LoggedIn Error :", ex.Message), ex);
            }

            return AtCount;
        }

        public string GetRole(string designation)
        {
            string[] names = designation.Split(' ');

            StringBuilder initials = new StringBuilder();

            foreach (string name in names)
                if (!string.IsNullOrEmpty(name))
                    initials.Append(name[0]);

            return initials.ToString().ToLower();
        }
        public List<EmployeeDataResponse> GetRole()
        {
            List<EmployeeDataResponse> emp = new List<EmployeeDataResponse>();
            string connectionString = _configuration.GetConnectionString("connectionString");
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string _sp = "DD_SP_GetUserDetails";
                SqlCommand cmd = new SqlCommand(_sp, con);
                cmd.CommandType = CommandType.StoredProcedure;
                con.Open();

                using (SqlCommand selectCmd = new SqlCommand(_sp, con))
                {
                    using (SqlDataReader reader = selectCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            EmployeeDataResponse model = new EmployeeDataResponse();
                            model.EmployeeId = reader.TryGetValue<int>("EmpId").ToString();
                            model.EmployeeName = reader.TryGetValue<string>("Name");
                            model.EmployeeRole = reader.TryGetValue<string>("Role").ToLower();

                            emp.Add(model);
                        }
                    }
                }
            }

            return emp;
        }

        public bool LogPageAccessActivity(LogUserActivityRequest request)
        {

            try
            {
                string connectionString = _configuration.GetConnectionString("SecurityConnectionString");

                string storedProcedureName = "SP_SaveApplicationAccessLog";
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(storedProcedureName, con);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Param_UserId", request.UserId);
                    cmd.Parameters.AddWithValue("@Param_UserEmpId", request.UserEmpId);
                    cmd.Parameters.AddWithValue("@Param_appRelativeVirtualPath", request.AppRelativeVirtualPath);
                    cmd.Parameters.AddWithValue("@Param_LoginCompanyId", request.LoginCompanyId);
                    cmd.Parameters.AddWithValue("@Param_AppCode", request.AppCode);
                    cmd.Parameters.AddWithValue("@Param_EntTerminal", request.EntTerminal);
                    cmd.Parameters.AddWithValue("@Param_EntTerminalIP", request.EntTerminalIP);
                    cmd.Parameters.AddWithValue("@Param_EntDevice", request.EntDevice);
                    cmd.Parameters.AddWithValue("@Param_MachineId", request.MachineId);
                    cmd.Parameters.AddWithValue("@Param_GeoLocation", request.GeoLocation);
                    cmd.Parameters.AddWithValue("@Param_DevicePlatform", request.DevicePlatform);
                    cmd.Parameters.AddWithValue("@Param_Model", request.Model);
                    cmd.Parameters.AddWithValue("@Param_Manufacturer", request.Manufacturer);
                    cmd.Parameters.AddWithValue("@Param_Version", request.Version);
                    con.Open();

                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(String.Format("{0} {1}", "Unable to TRACK ACTIVITY Error :", ex.Message), ex);
                return false;
            }
        }
        public void YourMethod()
        {
            // Example: Reading a connection string
            string connectionString = _configuration.GetConnectionString("YourConnectionString");

            // Example: Using SqlConnection
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Your data access logic here...
            }

            // Example: Using Newtonsoft.Json
            string jsonResult = JsonConvert.SerializeObject(new { Key = "Value" });

            // Other code...
        }
    }
    public class YourDbContext : DbContext
    {
        public YourDbContext(DbContextOptions<YourDbContext> options)
            : base(options)
        {
        }

        public DbSet<UniqueKeyEntity> UniqueKeys { get; set; }
    }

    public class LogUserActivityRequest
    {
        public string UserId { get; set; }
        public int UserEmpId { get; set; }
        public string AppRelativeVirtualPath { get; set; }
        public int LoginCompanyId { get; set; }
        public string AppCode { get; set; }
        public string EntTerminal { get; set; }
        public string EntTerminalIP { get; set; }
        public string? EntDevice { get; set; } = string.Empty;
        public string? MachineId { get; set; } = string.Empty;
        public string? GeoLocation { get; set; } = string.Empty;
        public string? DevicePlatform { get; set; } = string.Empty;
        public string? Model { get; set; } = string.Empty;
        public string? Manufacturer { get; set; } = string.Empty;
        public string? Version { get; set; } = string.Empty;
    }
    public class UniqueKeyEntity
    {
        public int Id { get; set; }
        public string? UniqueKey { get; set; }
        public string? EntTerminal { get; set; }
        public string? EntTerminalIP { get; set; }
    }
    public class SecurityUserData
    {
        public string? UserIdCode;
        public string? UserName;
        public string? DECUserId;
        public byte[] ENCUserID;
    }

    public class SecuritySetup
    {

        //  [JsonProperty("Id")]
        public string? Id { get; set; }
        //    [JsonProperty("Name")]
        public string? Name { get; set; }
    }


    //

    public class SaveFCMToken
    {
        public string UserId { get; set; }
        public string FcmToken { get; set; }
        public string Udid { get; set; }
        public int EmpId { get; set; }
        public string JwtToken { get; set; }
        public DateTime LoginTime { get; set; }
        public int DeviceType { get; set; }
    }


}
