using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using System.Dynamic;
using System.Net;
//using System.Reflection.Metadata;
 
using HCMS_Api.Components.HCMS.Common.Security;
using System.Reflection.Emit;
using HCMS_Api.Common;

namespace HCMS_Api.Controllers.HCMS.Common
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ValidateAntiForgeryTokenFilter _validateAntiForgeryTokenFilter;
        private readonly IAntiforgery _antiforgery;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private readonly LoginComponent _loginComponent;
        
        //private readonly IHttpContextAccessor _httpContextAccessor;
        public SecurityController(IConfiguration configuration, ValidateAntiForgeryTokenFilter validateAntiForgeryTokenFilter,
            IAntiforgery antiforgery, Utilities utilities, ClientContextService clientContextService
            , LoginComponent loginComponent)
        {
            _configuration = configuration;
            _validateAntiForgeryTokenFilter = validateAntiForgeryTokenFilter;
            _antiforgery = antiforgery;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _loginComponent = loginComponent;
        }
        [HttpGet("ValidateBaseUrl")]
        public IActionResult ValidateBaseUrl()
        {
            return Ok(new { result = "Base Url Match SuccessFully" });
        }

        [HttpGet("CheckPasswordChange")]
        public bool CheckPasswordChange(string userId, Utilities utilities)
        {
            bool passwordChange = false;

            try
            {
                _utilities.SetKeyInRedis(userId + "ChangePassword", "Y");
                passwordChange = true;
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                passwordChange = false;
            }

            return passwordChange;
        }




        [HttpGet("RemoveKeyInRedis")]
        public bool RemoveKeyInRedis([FromServices] Utilities utilities)
        {
            bool removeKey = false;

            try
            {                
                
                if (_loginComponent.GetLogOut(_clientContextService.GetClientIP()) == "Successful")
                {
                    removeKey = true;
                }
                else
                {
                    removeKey = false;
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                removeKey = false;
            }

            return removeKey;
        }

        [HttpGet("RemoveFormRightInRedis")]
        public async Task<string> RemoveFormRightInRedis(string _UserId)
        {            
            try
            {
                string[] values = _UserId.Split(',');

                for (int i = 0; i < values.Length; i++)
                {
                    // Trim and convert to lowercase
                    string trimmedUserId = values[i].Trim().ToLower();

                    // Check if removal was successful
                    if (_utilities.RemoveAllKeyInRedis(trimmedUserId, _configuration))
                    {
                        string check = "True";
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "Successful";
        }

        [HttpGet("SaveFormRightInRedis")]
        async public Task<string> SaveFormRightInRedis(string _ApplicationCode, string _UserId, string _CCode, string _Culture)
        {
            try
            {
                _UserId = _UserId.ToLower();
                if (!String.IsNullOrEmpty(_CCode))
                {
                    _CCode = Convert.ToInt32(_CCode).ToString();
                }                
                
                DataTable dtMenu = _loginComponent.GetMenuData(_ApplicationCode, _UserId, _CCode, _Culture, _configuration);
                DataTable dtSubMenu = new DataTable();
                _CCode = Convert.ToInt32(_CCode).ToString();
                int _totalChunksMenu = 0;

                if (dtMenu != null && dtMenu.Rows.Count > 0)
                {
                    if (_utilities.RemoveAllKeyInRedis(_UserId + _ApplicationCode + _CCode, _configuration))
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

                            await _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + _ApplicationCode + _CCode + i.ToString());

                            _skip = _skip + 100;

                            if (_reminder != 0)
                            {
                                if (_skip == totalRows)
                                {
                                    i = i + 1;
                                    _GenerateMenu = dtMenu.AsEnumerable().Skip(_skip).Take(_reminder).CopyToDataTable();
                                    JSONresultGenerateMenu = JsonConvert.SerializeObject((DataTable)_GenerateMenu);

                                    await _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + _ApplicationCode + _CCode + (i).ToString());
                                }
                            }

                            _totalChunksMenu = i;
                        }

                        await _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", _totalChunksMenu.ToString(), _UserId + _ApplicationCode + _CCode);
                    }
                    else
                    {
                        string JSONresultGenerateMenu = JsonConvert.SerializeObject(dtMenu);
                        await _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + _ApplicationCode + _CCode + "1");
                        await _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", "1", _UserId + _ApplicationCode + _CCode);
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return "Successful";
        }

        [HttpGet("SaveFormRightUsingRoleInRedis")]
        public async Task<string> SaveFormRightUsingRoleInRedis(string _UserId)
        {
            try
            {
                string _ApplicationCode = String.Empty;
                string _CCode = String.Empty;
                string _Culture = String.Empty;
                
                
                DataTable dtMenu = _loginComponent.GetMenuData(_ApplicationCode, _UserId, _CCode, _Culture, _configuration);
                DataTable dtSubMenu = new DataTable();
                _CCode = Convert.ToInt32(_CCode).ToString();
                int _totalChunksMenu = 0;

                if (dtMenu != null && dtMenu.Rows.Count > 0)
                {
                    if (_utilities.RemoveAllKeyInRedis(_UserId + _ApplicationCode + _CCode, _configuration))
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
                            await _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + _ApplicationCode + _CCode + i.ToString());

                            _skip = _skip + 100;

                            if (_reminder != 0)
                            {
                                if (_skip == totalRows)
                                {
                                    i = i + 1;
                                    _GenerateMenu = dtMenu.AsEnumerable().Skip(_skip).Take(_reminder).CopyToDataTable();
                                    JSONresultGenerateMenu = JsonConvert.SerializeObject((DataTable)_GenerateMenu);
                                    await _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + _ApplicationCode + _CCode + (i).ToString());
                                }
                            }

                            _totalChunksMenu = i;
                        }
                        await _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", _totalChunksMenu.ToString(), _UserId + _ApplicationCode + _CCode);
                    }
                    else
                    {
                        string JSONresultGenerateMenu = JsonConvert.SerializeObject(dtMenu);
                        await _utilities.SetUserRightsInRedis("GenerateMenu", JSONresultGenerateMenu, _UserId + _ApplicationCode + _CCode + "1");
                        await _utilities.SetUserRightsInRedis("GenerateMenuTotalChunks", "1", _UserId + _ApplicationCode + _CCode);
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return "Successful";
        }






        [HttpGet("GetLogin/{AppCode}")]
        public async Task<string> GetLogin(string AppCode)
        {
            string redirectLink = string.Empty;
            string prefix = string.Empty;
            

            try
            {                
                // var clientIP = "10.10.20.30";
                
                prefix = _loginComponent.GetPrefix(_clientContextService.GetClientIP());

                if (!string.IsNullOrEmpty(prefix))
                {
                    others otherProperty = new others();
                    string loginDetail = _utilities.GetKeyInRedis(prefix + "DecryptLoginDetail", _configuration);

                    if (!string.IsNullOrEmpty(loginDetail) && loginDetail.Length > 0)
                    {
                        DataTable dtloginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                        if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                        {
                            DataRow drloginDetail = dtloginDetail.Rows[0];

                            if (!string.IsNullOrEmpty(drloginDetail["CompanyId"].ToString()) &&
                                !string.IsNullOrEmpty(drloginDetail["CompanyName"].ToString()) &&
                                !string.IsNullOrEmpty(drloginDetail["Application"].ToString()) &&
                                !string.IsNullOrEmpty(drloginDetail["Password"].ToString()) &&
                                !string.IsNullOrEmpty(drloginDetail["UserId"].ToString()) &&
                                !string.IsNullOrEmpty(drloginDetail["UserIdCode"].ToString()))
                            {
                                if (!string.IsNullOrEmpty(drloginDetail["Culture"].ToString()))
                                {
                                    otherProperty.UserSelectedCulture = drloginDetail["Culture"].ToString();
                                }
                                else
                                {
                                    otherProperty.UserSelectedCulture = "en-GB";
                                }

                                redirectLink = _loginComponent.CheckAuthentication(
                                    drloginDetail["UserId"].ToString(),
                                    drloginDetail["UserIdCode"].ToString(),
                                    drloginDetail["Password"].ToString(),
                                    drloginDetail["CompanyId"].ToString(),
                                    drloginDetail["CompanyName"].ToString(),
                                    drloginDetail["Application"].ToString(),
                                    otherProperty.UserSelectedCulture,
                                    prefix, AppCode
                                );
                            }

                            if (!string.IsNullOrEmpty(await _utilities.GetKeyInRedisAsync(prefix + "qsSession")) &&
                                await _utilities.GetKeyInRedisAsync(prefix + "qsSession") == "SessionExpiry")
                            {
                                redirectLink = "ErrorPage.aspx?qsSession=SessionExpiry";
                            }
                            else if (!string.IsNullOrEmpty(await _utilities.GetKeyInRedisAsync(prefix + "qsAccess")) &&
                                await _utilities.GetKeyInRedisAsync(prefix + "qsAccess") == "Denied")
                            {
                                redirectLink = "MainPage.aspx?qsAccess=Denied";
                            }
                            else if (!string.IsNullOrEmpty(await _utilities.GetKeyInRedisAsync(prefix + "qsLogout")) &&
                                await _utilities.GetKeyInRedisAsync(prefix + "qsLogout") == "true")
                            {
                                redirectLink = "ErrorPage.aspx?qsLogout=true";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string exMsg = ex.Message;
            }
            ///
            _utilities.SetSessionTimeOut(prefix);
            return redirectLink;
        }


        [HttpGet("GetERPLogin")]
        public async Task<string> GetERPLogin(string _CompanyId, string _CompanyName, string _UserId, string _password, string _culture, string WebERP, string _KeepMeSigin)
        {
            ///_CompanyId = "1";
            if (_CompanyId == "0")
                return "";
            
            ERPLogin erpLogin = new ERPLogin();
            _UserId = _UserId.ToLower();

            try
            {
                string _clientIP = _clientContextService.GetClientIP();
                string CompetitorAllowed = "Yes";
                SecurityUserData SecurityUsrDat = new SecurityUserData();
                
                string prefix = String.Empty;

                if (WebERP != "No")
                {
                    _loginComponent.InsertUniqueId(_UserId, _clientIP.ToString());
                    prefix = _loginComponent.GetPrefix(_clientIP.ToString());
                }
                else
                {
                    prefix = _loginComponent.GetPrefix(_clientIP.ToString());
                    await _utilities.SetKeyInRedisAsync("IsLogin", "true", prefix, _KeepMeSigin);
                }

                SecurityUsrDat = _loginComponent.GetSecurityUserData(_UserId);

                erpLogin.CompID = SoftronicCrypto.Encrypt(_CompanyId, Constant.passPhrase, Constant.saltValue);
                erpLogin.password = SoftronicCrypto.Encrypt(_password, _password, _UserId.ToLower());
                erpLogin.CompName = SoftronicCrypto.Encrypt(_CompanyName, Constant.passPhrase, Constant.saltValue);
                erpLogin.USERIDCODE = SoftronicCrypto.Encrypt(SecurityUsrDat.UserIdCode, Constant.passPhrase, Constant.saltValue);
                erpLogin.USERID = SoftronicCrypto.Encrypt(SecurityUsrDat.DECUserId.ToLower(), Constant.passPhrase, Constant.saltValue);
                erpLogin.CompetitorAllowed = CompetitorAllowed;
                erpLogin.UserCulture = SoftronicCrypto.Encrypt(_culture, Constant.passPhrase, Constant.saltValue);
                erpLogin.FormUri = "ERPMain.aspx";

                erpLogin.App = SoftronicCrypto.Encrypt("true", Constant.passPhrase, Constant.saltValue);

                DataTable dtLogin = new DataTable();
                dtLogin.Columns.Add("CompanyId", typeof(string));
                dtLogin.Columns.Add("CompanyName", typeof(string));
                dtLogin.Columns.Add("Password", typeof(string));
                dtLogin.Columns.Add("UserIdCode", typeof(string));
                dtLogin.Columns.Add("UserId", typeof(string));
                dtLogin.Columns.Add("CompetitorAllowed", typeof(string));
                dtLogin.Columns.Add("Culture", typeof(string));
                dtLogin.Columns.Add("WebErp", typeof(string));
                dtLogin.Columns.Add("Application", typeof(string));

                dtLogin.Rows.Add(erpLogin.CompID, erpLogin.CompName, erpLogin.password, erpLogin.USERIDCODE, erpLogin.USERID.Trim(), erpLogin.CompetitorAllowed, erpLogin.UserCulture, WebERP, erpLogin.App);

                string JSONresult;
                JSONresult = JsonConvert.SerializeObject(dtLogin);
                await _utilities.SetKeyInRedisAsync("LoginDetail", JSONresult, prefix, _KeepMeSigin);

                dtLogin.Rows.RemoveAt(0);

                dtLogin.Rows.Add(Convert.ToInt32(_CompanyId).ToString(), _CompanyName, _password, SecurityUsrDat.UserIdCode, _UserId.Trim(), CompetitorAllowed, _culture, WebERP, "true");
                JSONresult = JsonConvert.SerializeObject(dtLogin);
                await _utilities.SetKeyInRedisAsync("DecryptLoginDetail", JSONresult, prefix, _KeepMeSigin);

                if (!String.IsNullOrEmpty(_KeepMeSigin))
                {
                    if (_KeepMeSigin.ToLower() == "true")
                    {
                        await _utilities.SetKeyInRedisAsync("KeepMeSignin", "true", prefix, _KeepMeSigin);
                    }
                }

                _utilities.SetKeyInRedis(_UserId + "ChangePassword", "N");
                await _utilities.SetSessionTimeOutAsync(prefix, _KeepMeSigin);

                // Increment the counter for each request to CheckPasswordChange
                //  _LoginCounter.Inc();

            }
            catch (Exception ex)
            {
                string msg = ex.Message;
            }
            return erpLogin.FormUri;
        }

        [HttpGet("GetLogout")]
        public string GetLogout()
        {
            string msg = string.Empty;
            try
            {
                
                msg = _loginComponent.GetLogOut(_clientContextService.GetClientIP());
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return msg;
        }

        [HttpGet("CanInsert")]
        public bool CanInsert(string FormId)
        {
            bool CanInsert = false;
            
            try
            {
                
                CanInsert = _loginComponent.CheckPermission(FormId, "CanInsert", _clientContextService.GetClientIP());
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }
            return CanInsert;
        }

        [HttpGet("CanEdit")]
        public bool CanEdit(string FormId)
        {
            bool CanEdit = false;
            
            try
            {
                
                CanEdit = _loginComponent.CheckPermission(FormId, "CanEdit", _clientContextService.GetClientIP());
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }
            return CanEdit;
        }

        [HttpGet("CanView")]
        public bool CanView(string FormId)
        {
            bool CanView = false;
            
            if (_clientContextService.GetClientIP() != null)
            {
                string prefix = _utilities.GetPrefix(_clientContextService.GetClientIP());
                try
                {
                    if (FormId == "IsLogin")
                    {
                        CanView = Convert.ToBoolean(_utilities.GetKeyInRedis(prefix + "IsLogin", _configuration));
                    }
                    else
                    {
                        
                        CanView = _loginComponent.CheckPermission(FormId, "CanView", prefix);
                    }
                }
                catch (Exception ex)
                {
                    // Handle the exception if needed
                    CanView = false;
                }
            }
            return CanView;
        }
        //

        [HttpGet("CanDelete")]
        public bool CanDelete(string FormId)
        {
            bool CanDelete = false;
            
            try
            {
                
                CanDelete = _loginComponent.CheckPermission(FormId, "CanDelete", _clientContextService.GetClientIP());
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }
            return CanDelete;
        }

        [HttpGet("CanProcessed")]
        public bool CanProcessed(string FormId)
        {
            bool CanProcessed = false;
            
            try
            {
                
                CanProcessed = _loginComponent.CheckPermission(FormId, "CanProcessed", _clientContextService.GetClientIP());
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }
            return CanProcessed;
        }

        [HttpGet("CanViewReport")]
        public bool CanViewReport(string FormId)
        {
            bool CanViewReport = false;
            
            try
            {
                
                CanViewReport = _loginComponent.CheckPermission(FormId, "CanViewReport", _clientContextService.GetClientIP());
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }
            return CanViewReport;
        }

        [HttpGet("GetAllCompanies")]
        public ActionResult<List<SecuritySetup>> GetAllCompanies()
        {
            try
            {
                List<SecuritySetup> list = new List<SecuritySetup>();
                
                DataSet ds = _loginComponent.GetAllCompanies();

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        SecuritySetup obj = new SecuritySetup();

                        if (dr["Id"] != DBNull.Value)
                            obj.Id = Convert.ToString(dr["Id"]);

                        if (dr["Name"] != DBNull.Value)
                            obj.Name = Convert.ToString(dr["Name"]);

                        list.Add(obj);
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }









        [HttpGet("GenerateUniqueKey")]
        public string GenerateUniqueKey()
        {
            string GuidString = String.Empty;

            
            Guid g = Guid.NewGuid();
            GuidString = Convert.ToBase64String(g.ToByteArray());
            GuidString = GuidString.Replace("=", "");
            GuidString = GuidString.Replace("+", "");
            GuidString = _loginComponent.Sha256(GuidString);

            return GuidString;
        }


        [HttpGet("getTerminalId")]
        public string GetTerminalId()
        {
            string returnVal = string.Empty;

            try
            {
                // System.Net.IPHostEntry obj = new System.Net.IPHostEntry();
                // obj = System.Net.Dns.GetHostByAddress(HttpContext.Current.Request.ServerVariables["REMOTE_HOST"]);
                // returnVal = obj.HostName.ToString();

                // Added 3 lines on 18/02/2016
                IPHostEntry obj = Dns.GetHostEntry(HttpContext.Connection.RemoteIpAddress);
                returnVal = obj.HostName;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                returnVal = HttpContext.Connection.RemoteIpAddress.ToString();
            }

            return returnVal;
        }

        [HttpGet("getTerminalIP")]
        public string GetTerminalIP()
        {
            string returnVal = string.Empty;

            try
            {
                returnVal = HttpContext.Connection.RemoteIpAddress.ToString();
            }
            catch (System.Net.Sockets.SocketException se)
            {
                returnVal = HttpContext.Connection.RemoteIpAddress.ToString();
            }

            return returnVal;
        }









        [HttpGet("CanViewForAuthGuard")]
        public string CanViewForAuthGuard(string formId, string AppCode)
        {
            string canView = String.Empty;


            var clientIP = _clientContextService.GetClientIP();

            if (clientIP != null)
            {
                string prefix = _utilities.GetPrefix(clientIP);

                try
                {
                    if (formId == "IsLogin")
                    {
                        canView = Convert.ToString(_utilities.GetKeyInRedis(prefix + "IsLogin", _configuration));
                    }
                    else
                    {
                        
                        canView = _loginComponent.CheckPermission(formId, "CanView", prefix.ToString(), clientIP, AppCode).ToString();

                        if (!String.IsNullOrEmpty(_utilities.CheckSessionTimeOut(clientIP)))
                        {
                            canView = "SessionTimeOut";
                        }
                    }

                    if (String.IsNullOrEmpty(canView))
                    {
                        canView = "false";
                    }
                }
                catch (Exception ex)
                {
                    canView = "false";
                }
            }
            else
            {
                canView = "SessionTimeOut";
            }

            return canView;
        }


        //[HttpGet("ValidateToken")]
        //[AutoValidateAntiforgeryToken]
        //public IActionResult ValidateToken()
        //{
        //    return Ok(true);
        //}


        [HttpGet("ValidateToken")]
        public IActionResult ValidateToken()
        {
            //bool check = false;

            //if (_validateAntiForgeryTokenFilter.CheckAntiForgeryToken(HttpContext))
            //{
            //    check = true;
            //}

            // return Ok(check);
            return Ok(true);
        }


        [HttpGet("GetTokken")]
        public IActionResult GetTokken()
        {
            var response = new ObjectResult(new AntiForgeryTokenModel());

            string cookieToken;
            string formToken;

            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            cookieToken = tokens.RequestToken;
            formToken = tokens.RequestToken;

            var content = new AntiForgeryTokenModel
            {
                Token = formToken,
                xsrftoken = cookieToken
            };

            response.Value = content;

            if (!string.IsNullOrEmpty(cookieToken))
            {
                Response.Cookies.Append("xsrf-token", cookieToken, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddMinutes(10),
                    Path = "/"
                });

                Response.Headers.Add("Token", formToken);
            }

            return response;
        }

        [HttpGet("GetHeaderDetails")]
        public IActionResult GetHeaderDetails(string? _URL, string? FormId, string applicationCode)
        {
            if (_URL == null)
            {
                _URL = "";
            }

            if (FormId == null)
            {
                FormId = "";
            }

            var clientIP = _clientContextService.GetClientIP();

            clsHeader obj = new clsHeader();
            
            UserInfo objuser = _utilities.GetCurrentUserMap(clientIP);
            string CompanyId = _utilities.GetCompanyId(clientIP);
            string Prefix = _utilities.GetPrefix(clientIP);
            string UserId = _utilities.GetUserid(Prefix);
            string Culture = _utilities.GetAppCurrentUICulture(clientIP);
            int UserEmpId = objuser.UserEmpId;

            DataSet ds = new DataSet();

            try
            {
                string query = " Select formName,ApplicationCode,formdescription,FormLocation, " +
                               " FormPath,FormId,IsFavorite,isPayrollGroupSecurityWillApply," +
                               " IsAuditTrailApplied " +
                               " from fn_General_GetFormHeader('" + applicationCode + "','" + Culture + "','" + UserId + "','" + CompanyId + "','" + UserEmpId + "','" + _URL + "','" + FormId + "')";

                DataServices _dataService = new DataServices(_configuration);
                string result = _dataService.ExecuteReader(query, ref ds);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    obj.formName = ds.Tables[0].Rows[0]["formName"].ToString();
                    obj.formdescription = ds.Tables[0].Rows[0]["formdescription"].ToString();
                    obj.FormLocation = ds.Tables[0].Rows[0]["FormLocation"].ToString();
                    obj.FormId = ds.Tables[0].Rows[0]["FormId"].ToString();
                    obj.IsFavorite = Convert.ToBoolean(ds.Tables[0].Rows[0]["IsFavorite"]);
                    obj.isPayrollGroupSecurityWillApply = Convert.ToBoolean(ds.Tables[0].Rows[0]["isPayrollGroupSecurityWillApply"]);
                    obj.IsAuditTrailApplied = Convert.ToBoolean(ds.Tables[0].Rows[0]["IsAuditTrailApplied"]);
                }

                return Ok(obj);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.ExpectationFailed, ex.Message);
            }
        }

        [HttpGet("GetFormName")]
        public string[] GetFormName(string _URL)
        {
            string[] _FormName = new string[2];

            var _clientIP = _clientContextService.GetClientIP();
            string _prefix = _utilities.GetPrefix(_clientIP);

            try
            {
                if (_URL.Contains("/") && _URL.Split('/')[1] == "dashboard")
                {
                    _FormName[0] = _utilities.GetMasterLabelByCode("dashboard", _utilities.GetAppCurrentUICulture(_clientIP));
                    _FormName[1] = "dashboard";
                }
                else
                {
                    
                    _FormName = _loginComponent.GetFormName(_URL, _prefix);
                }
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }
            return _FormName;
        }

        [HttpGet("GetEmailFormPath")]
        public string GetEmailFormPath(string FormKey)
        {
            try
            {
                string Prefix, _RedirectPath = String.Empty;
                dynamic objResponse = new ExpandoObject();
                var _clientIP = _clientContextService.GetClientIP();
                
                Prefix = _loginComponent.GetPrefix(_clientIP.ToString());
                if (!String.IsNullOrEmpty(Prefix))
                {
                    
                    others otherProperty = new others();
                    string _loginDetail = _utilities.GetKeyInRedis(Prefix + "LoginDetail", _configuration);
                    if (!String.IsNullOrEmpty(_loginDetail) && _loginDetail.Length > 0)
                    {
                        DataTable dtloginDetail = JsonConvert.DeserializeObject<DataTable>(_loginDetail);
                        if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                        {
                            DataRow drloginDetail = dtloginDetail.Rows[0];
                            if (!String.IsNullOrEmpty(drloginDetail["CompanyId"].ToString()))
                            {
                                if (!String.IsNullOrEmpty(drloginDetail["CompanyName"].ToString()))
                                {
                                    if (!String.IsNullOrEmpty(drloginDetail["Application"].ToString()))
                                    {
                                        if (!String.IsNullOrEmpty(drloginDetail["Password"].ToString()))
                                        {
                                            if (!String.IsNullOrEmpty(drloginDetail["UserId"].ToString()))
                                            {
                                                if (!String.IsNullOrEmpty(drloginDetail["UserIdCode"].ToString()))
                                                {
                                                    if (!String.IsNullOrEmpty(FormKey))
                                                    {
                                                        if (!String.IsNullOrEmpty(drloginDetail["Culture"].ToString()))
                                                        {
                                                            otherProperty.UserSelectedCulture = SoftronicCrypto.Decrypt(drloginDetail["Culture"].ToString().Replace(" ", "+"), Constant.passPhrase, Constant.saltValue);
                                                        }
                                                        else
                                                        {
                                                            otherProperty.UserSelectedCulture = "en-GB";
                                                        }

                                                        if (!String.IsNullOrEmpty(drloginDetail["WebErp"].ToString()) && drloginDetail["WebErp"].ToString() == "Yes")
                                                        {
                                                            _RedirectPath = _loginComponent.CheckAuthenticationRedirectFormUrl(SoftronicCrypto.Decrypt(drloginDetail["UserId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.Decrypt(drloginDetail["UserIdCode"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                drloginDetail["Password"].ToString(),
                                                                SoftronicCrypto.Decrypt(drloginDetail["CompanyId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.Decrypt(drloginDetail["CompanyName"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.Decrypt(drloginDetail["Application"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.DecryptPlain(FormKey.Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"), Prefix, _clientIP);
                                                        }
                                                        else
                                                        {
                                                            if (!String.IsNullOrEmpty(drloginDetail["Culture"].ToString()))
                                                            {
                                                                otherProperty.UserSelectedCulture = SoftronicCrypto.Decrypt(drloginDetail["Culture"].ToString().Replace(" ", "+"), Constant.passPhrase, Constant.saltValue);
                                                            }
                                                            else
                                                            {
                                                                otherProperty.UserSelectedCulture = "en-GB";
                                                            }

                                                            _RedirectPath = _loginComponent.CheckAuthenticationRedirectFormUrl(SoftronicCrypto.Decrypt(drloginDetail["UserIdCode"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.Decrypt(drloginDetail["UserIdCode"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                drloginDetail["Password"].ToString(),
                                                                SoftronicCrypto.Decrypt(drloginDetail["CompanyId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.Decrypt(drloginDetail["CompanyName"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.Decrypt(drloginDetail["Application"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
                                                                SoftronicCrypto.DecryptPlain(FormKey.Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"), Prefix, _clientIP);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (!String.IsNullOrEmpty(_utilities.GetKeyInRedis(Prefix + "qsSession", _configuration)) &&
                                _utilities.GetKeyInRedis(Prefix + "qsSession", _configuration) == "SessionExpiry")
                            {
                                _RedirectPath = "ErrorPage.aspx?qsSession=SessionExpiry";
                            }
                            else if (!String.IsNullOrEmpty(_utilities.GetKeyInRedis(Prefix + "qsAccess", _configuration)) &&
                                _utilities.GetKeyInRedis(Prefix + "qsAccess", _configuration) == "Denied")
                            {
                                _RedirectPath = "MainPage.aspx?qsAccess=Denied";
                            }
                            else if (!String.IsNullOrEmpty(_utilities.GetKeyInRedis(Prefix + "qsLogout", _configuration)) &&
                                _utilities.GetKeyInRedis(Prefix + "qsLogout", _configuration) == "true")
                            {
                                _RedirectPath = "ErrorPage.aspx?qsLogout=true";
                            }
                        }
                    }
                }
                //_RedirectPath = "crm/regionmanagementregionsandsmallocation";
                objResponse.Path = _RedirectPath;
                //return ToJson(objResponse);
                return _RedirectPath;
            }
            catch (Exception ex)
            {
                //return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                //{
                //    Content = new ObjectContent<string>(ex.Message, Configuration.Formatters.JsonFormatter)
                //};
            }
            return "";
        }

        //public string GetEmailFormPath(string FormKey)
        //{
        //    try
        //    {
        //        string Prefix, _RedirectPath = String.Empty;
        //        dynamic objResponse = new ExpandoObject();
        //        var _clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
        //        LoginComponent ERPLoginComp = new LoginComponent(_configuration);
        //        Prefix = ERPLoginComp.GetPrefix(_clientIP);
        //      //  FormKey = "D53E7583-1EF6-4C6B-9382-B55541072F23";
        //        //Prefix = "0473ba57e4c3df060036250d0e06d50b15821b36fb767fcd16e2c709764739c0";

        //        if (!String.IsNullOrEmpty(Prefix))
        //        {
        //            others otherProperty = new others();
        //            
        //            string _loginDetail = _utilities.GetKeyInRedis(Prefix + "LoginDetail", _configuration);
        //            if (!String.IsNullOrEmpty(_loginDetail) && _loginDetail.Length > 0)
        //            {
        //                var dtloginDetail = JsonConvert.DeserializeObject<DataTable>(_loginDetail);
        //                if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
        //                {
        //                    var drloginDetail = dtloginDetail.Rows[0];
        //                    if (!String.IsNullOrEmpty(drloginDetail["CompanyId"].ToString()))
        //                    {
        //                        // Additional checks omitted for brevity

        //                        if (!String.IsNullOrEmpty(drloginDetail["Culture"].ToString()))
        //                        {
        //                            otherProperty.UserSelectedCulture = SoftronicCrypto.Decrypt(drloginDetail["Culture"].ToString().Replace(" ", "+"), Constant.passPhrase, Constant.saltValue);
        //                        }
        //                        else
        //                        {
        //                            otherProperty.UserSelectedCulture = "en-GB";
        //                        }

        //                        if (!String.IsNullOrEmpty(drloginDetail["WebErp"].ToString()) && drloginDetail["WebErp"].ToString() == "Yes")
        //                        {
        //                            _RedirectPath = ERPLoginComp.CheckAuthenticationRedirectFormUrl(SoftronicCrypto.Decrypt(drloginDetail["UserId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(drloginDetail["UserId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                drloginDetail["Password"].ToString(),
        //                                SoftronicCrypto.Decrypt(drloginDetail["CompanyId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(drloginDetail["CompanyName"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(drloginDetail["Application"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(FormKey.Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"), Prefix, _clientIP);
        //                        }
        //                        else
        //                        {
        //                            _RedirectPath = ERPLoginComp.CheckAuthenticationRedirectFormUrl(SoftronicCrypto.Decrypt(drloginDetail["UserId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(drloginDetail["UserIdCode"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                drloginDetail["Password"].ToString(),
        //                                SoftronicCrypto.Decrypt(drloginDetail["CompanyId"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(drloginDetail["CompanyName"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(drloginDetail["Application"].ToString().Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"),
        //                                SoftronicCrypto.Decrypt(FormKey.Replace(" ", "+"), "7CAE37CB-ECD1-4D", "A7-A32E-B2BB3E65DDD9"), Prefix, _clientIP);

        //                        }
        //                    }
        //                }
        //            }

        //            // Handle other cases as before
        //        }
        //        //_RedirectPath = "http://localhost:4200/crm/regionmanagementregionsandsmallocation";

        //        objResponse.Path = _RedirectPath;
        //        return _RedirectPath;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle exception
        //    }
        //    return "";
        //}



        [HttpGet("GetCompanyDetail")]
        public ActionResult<clsCompDetail> GetCompanyDetail(string userId)
        {
            clsCompDetail obj = new clsCompDetail();
            DataSet ds = new DataSet();

            try
            {
                string query = $"exec GetCompDetail @paramUserId='{userId}'";
                DataServices dataservice = new DataServices(_configuration);
                string result = dataservice.ExecuteReader(query, ref ds);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    obj.UserId = ds.Tables[0].Rows[0]["UserId"].ToString();
                    obj.CCode = ds.Tables[0].Rows[0]["CCode"].ToString();
                    obj.CName = ds.Tables[0].Rows[0]["CName"].ToString();

                    return Ok(obj);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
 
        
         

        [HttpDelete("LogOut")]
        public IActionResult LogOut(string udid)
        {
            if (string.IsNullOrEmpty(udid))
            {
                return BadRequest(new { message = "Invalid UDID" });
            }
            _loginComponent.DeleteLogoutSession(udid);

            return Ok(new { message = "Logout Successfully" });
        }

        [HttpGet("TrackUserPageAccessWEB")]
        public IActionResult TrackUserPageAccessWEB([FromQuery] string URL)
        {            
            var clientIP = _clientContextService.GetClientIP();

            if (!string.IsNullOrEmpty(clientIP))
            {
                
                UserInfo objuser = _utilities.GetCurrentUserMap(clientIP);
                string CompanyId = _utilities.GetCompanyId(clientIP);
                string Prefix = _utilities.GetPrefix(clientIP);
                string UserId = _utilities.GetUserid(Prefix);
                string EmpId = _utilities.GetEmployeeId(HttpContext, UserId);
                //string EntTerminal = _utilities.GetTerminalId();
                string EntTerminalIP = _utilities.GetTerminalIP();
                int UserEmpId = objuser.UserEmpId;

                var user = new LogUserActivityRequest
                {
                    UserId = UserId,
                    UserEmpId = UserEmpId,
                    AppRelativeVirtualPath = URL,
                    LoginCompanyId = Convert.ToInt16(CompanyId),
                    AppCode = Constants.Param_LoginApplication,
                    //EntTerminal = EntTerminal,
                    EntTerminalIP = EntTerminalIP,
                    DevicePlatform = "WEB"
                };
                _loginComponent.LogPageAccessActivity(user);
            }
            else
            {
                return BadRequest(new { message = "Invalid request" });
            }

            return Ok(new { message = "Activity Logged Successfully" });
        }

        [HttpPost("TrackUserPageAccess")]
        public IActionResult TrackUserPageAccess([FromBody] LogUserActivityRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Invalid request" });
            }
            if (string.IsNullOrEmpty(request.UserId) || request.UserEmpId == 0 || string.IsNullOrEmpty(request.AppRelativeVirtualPath) ||
                request.LoginCompanyId == 0 || string.IsNullOrEmpty(request.AppCode))
            {
                return BadRequest(new { message = "One or more required fields are missing or null" });
            }
            _loginComponent.LogPageAccessActivity(request);

            return Ok(new { message = "Activity Logged Successfully" });
        }


        [HttpPost("SaveApplicationAccessLog")]
        public async Task<IActionResult> SaveAccessLog([FromBody] LogUserActivityRequest dto)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();

                if (!string.IsNullOrEmpty(clientIP))
                {
                    UserInfo? objuser = _utilities.GetCurrentUserMap(clientIP);

                    // Check if user exists and has a valid ID before doing ANYTHING else
                    if (objuser != null && !string.IsNullOrWhiteSpace(objuser.UserID))
                    {
                        // 1. Extract context using your utility logic
                        string companyIdStr = _utilities.GetCompanyId(clientIP);
                        string entTerminalIP = _utilities.GetTerminalIP();
                        string terminalId = _utilities.GetTerminalId();

                        // 2. Populate the DTO with the extracted values
                        dto.UserId = objuser.UserID;
                        dto.UserEmpId = objuser.UserEmpId;
                        dto.LoginCompanyId = int.TryParse(companyIdStr, out int cId) ? cId : 0;
                        dto.EntTerminalIP = entTerminalIP;
                        dto.EntTerminal = terminalId;
                        dto.AppCode = dto.AppCode;

                        // 3. 🔥 MOVE THIS INSIDE: Only call the Service if validation passed
                        await _loginComponent.SaveApplicationAccessLogAsync(dto);

                        return Ok(new { success = true, message = "Log inserted" });
                    }
                }

                // If we reach here, it means either IP was missing, User was null, or UserId was empty.
                // We return Ok so the Angular app doesn't show an error, but we skip the DB insert.
                return Ok(new { success = false, message = "Log skipped: User context missing" });
            }
            catch (Exception ex)
            {
                //_logger.LogError($"Internal server error during access logging: {ex}");
                // Log the actual exception (ex) to your server log here
                return StatusCode(500, "Internal server error during access logging");
            }
        }


        public class RefreshTokenRequest
        {
            public string UpdatedFcmToken { get; set; }
            public string UDID { get; set; }
            public enDeviceType DeviceType { get; set; }
        }

        public class Users
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class clsHeader
        {
            public string? formName { get; set; }
            public string? formdescription { get; set; }
            public string? FormLocation { get; set; }
            public string? FormId { get; set; }
            public bool IsFavorite { get; set; }
            public bool isPayrollGroupSecurityWillApply { get; set; }
            public bool IsAuditTrailApplied { get; set; }

        }

        public class clsCompDetail
        {
            public string? UserId { get; set; }
            public string? CCode { get; set; }
            public string? CName { get; set; }
        }



    }
}
