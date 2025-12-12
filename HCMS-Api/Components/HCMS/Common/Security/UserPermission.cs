namespace HCMS_Api.Components.HCMS.Common.Security
{
    public class UserPermission
    {
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly LoginComponent _loginComponent;

        public UserPermission(IConfiguration configuration, Utilities utilities, LoginComponent loginComponent)
        {
            _configuration = configuration;
            _utilities = utilities;
            _loginComponent = loginComponent;
        }

        public string CheckSessionTimeOut(string _Key)
        {
            string _res = "";            
            try
            {
                if (_Key != null)
                {
                    //var _Key = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
                    _Key = _utilities.GetPrefix(_Key);
                    _res = _utilities.GetKeyInRedis(_Key + "SessionTimeOut", _configuration);
                    string _KeepMeSigin = _utilities.GetKeyInRedis(_Key + "KeepMeSignin", _configuration);
                    string UserId = _utilities.GetUserid(_Key);

                    if (String.IsNullOrEmpty(_KeepMeSigin))
                    {
                        if (String.IsNullOrEmpty(_res))
                        {
                            _res = "SessionTimeOut";
                        }
                        else
                        {
                            _res = String.Empty;
                            _utilities.SetSessionTimeOut(_Key);
                        }

                        if (_utilities.GetUserRightInRedis(UserId + "ChangePassword").ToString().ToLower() == "y")
                        {
                            _res = "SessionTimeOut";
                        }
                    }
                    else
                    {
                        _res = String.Empty;
                        _utilities.SetSessionTimeOut(_Key);
                    }
                }
                //_logger.LogInformation("CheckSessionTimeOut executed successfully.");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

            }

            return _res;
        }
        //public bool CanInsert(string formId, string headerTokenKey)
        //{
        //    bool canInsert = false;

        //    try
        //    {
        //        string prefix = Utilities.GetPrefix(headerTokenKey);

        //        // Assuming LoginComponent is injected into your class
        //        // If not, you need to register it in the DI container and inject it
        //        LoginComponent lComp = new LoginComponent(_configuration);

        //        canInsert = lComp.CheckPermission(formId, "CanView", prefix, headerTokenKey) &&
        //                    lComp.CheckPermission(formId, "CanInsert", prefix, headerTokenKey);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle the exception (log, return default value, etc.)
        //        canInsert = false;
        //    }

        //    return canInsert;
        //}

        public bool CanEdit(string formId, string headerTokenKey, string AppCode)
        {
            bool canEdit = false;

            try
            {
                string prefix = _utilities.GetPrefix(headerTokenKey);

                // Assuming LoginComponent is injected into your class
                // If not, you need to register it in the DI container and inject it                

                canEdit = _loginComponent.CheckPermission(formId, "CanView", prefix, headerTokenKey, AppCode) &&
                          _loginComponent.CheckPermission(formId, "CanEdit", prefix, headerTokenKey, AppCode);
            }
            catch (Exception ex)
            {
                // Handle the exception (log, return default value, etc.)
                canEdit = false;
            }

            return canEdit;
        }

        public bool CanView(string formId, string headerTokenKey, string AppCode)
        {
            bool canView = false;

            try
            {
                string prefix = _utilities.GetPrefix(headerTokenKey);

                // Assuming LoginComponent is injected into your class
                // If not, you need to register it in the DI container and inject it                

                canView = _loginComponent.CheckPermission(formId, "CanView", prefix, headerTokenKey, AppCode);
            }
            catch (Exception ex)
            {
                // Handle the exception (log, return default value, etc.)
                canView = false;
            }

            return canView;
        }

        public bool CanInsert(string FormId, string HeaderTokenKey, string AppCode)
        {
            bool CanInsert = false;

            try
            {
                string _prefix = _utilities.GetPrefix(HeaderTokenKey);                
                CanInsert = _loginComponent.CheckPermission(FormId, "CanView", _prefix, HeaderTokenKey, AppCode) && _loginComponent.CheckPermission(FormId, "CanInsert", _prefix, HeaderTokenKey, AppCode);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
            }

            return CanInsert;
        }
        public bool CanDelete(string FormId, string HeaderTokenKey, string AppCode)
        {
            bool CanDelete = false;

            try
            {
                string _prefix = _utilities.GetPrefix(HeaderTokenKey);                
                CanDelete = _loginComponent.CheckPermission(FormId, "CanDelete", _prefix, HeaderTokenKey, AppCode) && _loginComponent.CheckPermission(FormId, "CanInsert", _prefix, HeaderTokenKey, AppCode);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
            }

            return CanDelete;
        }






    }
}
