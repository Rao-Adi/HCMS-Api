using HCMS_Api.Components.HCMS.Common.Security;
using HCMS_Api.Components.HCMS.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using System.Net;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Common;

namespace HCMS_Api.Controllers.HCMS.Common
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ValidateAntiForgeryTokenFilter _validateAntiForgeryTokenFilter;
        private readonly ClientContextService _clientContextService;
        private readonly Utilities _utilities;
        private readonly LoginComponent _loginComponent;

        //private readonly IAntiforgery _antiforgery;
        public MenuController(IConfiguration configuration
            , ValidateAntiForgeryTokenFilter validateAntiForgeryTokenFilter
            , ClientContextService clientContextService
            , Utilities utilities, LoginComponent loginComponent)
        {
            _configuration = configuration;
            _validateAntiForgeryTokenFilter = validateAntiForgeryTokenFilter;
            _clientContextService = clientContextService;
            //_antiforgery = antiforgery;
            _utilities= utilities;
            _loginComponent = loginComponent;
        }

        [HttpGet("GetMenuCounts/{AppCode}")]
        public IActionResult GetMenuCounts(string AppCode)
        {
            try
            {
                List<MenuCounts> cls = new List<MenuCounts>();
                cls = GetModuleWisePendingRecordCount(AppCode);

                return Ok(cls);
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // 417 Expectation Failed
            }
        }

        public List<MenuCounts> GetModuleWisePendingRecordCount(string AppCode)
        {
            DataServices _dataService = new DataServices(_configuration);
            
            var clientIP = _clientContextService.GetClientIP();

            UserInfo objuser = _utilities.GetCurrentUserMap(clientIP);
            string CompanyId = _utilities.GetCompanyId(clientIP);
            string Prefix = _utilities.GetPrefix(clientIP);
            string UserId = _utilities.GetUserid(Prefix);
            string Culture = _utilities.GetAppCurrentUICulture(clientIP);
            int UserEmpId = objuser.UserEmpId;
            string applicationId = AppCode; // _configuration.GetSection("CorsSettings:ApplicationId").Value;

            DataSet dsReturn = new DataSet();
            string strQuery = "Declare " +
                " @Param_LoginApplicationCode nvarchar(10) = '" + applicationId + "', " +
                " @Param_LoginEmpId int = " + UserEmpId + ", " +
                " @Param_LoginUserId nvarchar(max) = '" + UserId + "', " +
                " @Param_LoginCompanyId int = " + CompanyId + "; " +

            "Exec Sp_Workflow_GetModuleWisePendingRecordCount " +
                " @Param_LoginApplicationCode = @Param_LoginApplicationCode, " +
                " @Param_LoginEmpId = @Param_LoginEmpId,  " +
                " @Param_LoginUserId = @Param_LoginUserId,  " +
                " @Param_LoginCompanyId  = @Param_LoginCompanyId ";

            string strMessage = _dataService.ExecuteReaderDS(strQuery, ref dsReturn);

            List<MenuCounts> cls = new List<MenuCounts>();


            if (dsReturn != null && dsReturn.Tables.Count > 0)
            {
                if (dsReturn.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in dsReturn.Tables[0].Rows)
                    {
                        MenuCounts obj = new MenuCounts();
                        if (dr["ModuleId"] != DBNull.Value)
                            obj.ModuleId = Convert.ToString(dr["ModuleId"]);
                        if (dr["PendingCount"] != DBNull.Value)
                            obj.TotalCount = Convert.ToInt32(dr["PendingCount"]);
                        obj.Table = 0;
                        cls.Add(obj);
                    }
                }
                if (dsReturn.Tables[1].Rows.Count > 0)
                {
                    foreach (DataRow dr in dsReturn.Tables[1].Rows)
                    {
                        MenuCounts obj = new MenuCounts();
                        if (dr["FormPath"] != DBNull.Value)
                            obj.FormPath = Convert.ToString(dr["FormPath"]);
                        if (dr["ParentItem"] != DBNull.Value)
                            obj.ParentItem = Convert.ToString(dr["ParentItem"]);
                        if (dr["PendingCount"] != DBNull.Value)
                            obj.Count = Convert.ToInt32(dr["PendingCount"]);
                        obj.Table = 1;
                        cls.Add(obj);
                    }
                }
            }

            return cls;
        }



        [HttpGet("GetMenuDataThroughRedis/{AppCode}")]
        public async Task<IActionResult> GetMenuDataThroughRedis(string AppCode)
        {
            try
            {
                var clientIP = _clientContextService.GetClientIP();
                string prefix = _utilities.GetPrefix(clientIP);
                
                DataTable dt = await GetMenu(prefix, AppCode);

                if (dt != null && dt.Rows.Count != 0)
                {
                    List<clsRootMenu> list = new List<clsRootMenu>();

                    DataTable tblRootMenuItems = dt.AsEnumerable()
                    .Where(row =>
                    {
                        var isRootValue = row["isRoot"];
                        return isRootValue != null && int.TryParse(isRootValue.ToString(), out var isRoot) && isRoot == 1;
                    })
                    .CopyToDataTable();

                    DataTable tblNonRootMenuItems = dt.Clone(); // Clone the structure of the original DataTable

                    foreach (DataRow row in dt.AsEnumerable()
                        .Where(row =>
                        {
                            var isRootValue = row["isRoot"];
                            return isRootValue != null && int.TryParse(isRootValue.ToString(), out var isRoot) && isRoot == 0;
                        }))
                    {
                        tblNonRootMenuItems.ImportRow(row);
                    }

                    foreach (DataRow drRootMenu in tblRootMenuItems.Rows)
                    {
                        var obj = new clsRootMenu
                        {
                            Text = drRootMenu["Description"].ToString(),
                            Value = drRootMenu["FormID"].ToString() + " root",
                            child = AddChildMenuItems(drRootMenu["FormID"].ToString(), tblNonRootMenuItems)
                        };




                        if (obj.Text == "Favourites" || (obj.child != null && !RemoveParent(obj)))
                        {
                            list.Add(obj);
                        }

                    }



                    return Ok(list);
                }
                else
                {
                    return StatusCode((int)HttpStatusCode.ExpectationFailed, "Error in Code");
                }
            }
            catch (Exception ex)
            {
                // Handle any other unexpected exceptions
                return StatusCode((int)HttpStatusCode.ExpectationFailed, ex.Message);
            }
        }
        [HttpGet("GetMenu")]
        private async Task<DataTable> GetMenu(string prefix, string AppCode)
        {
            try
            {
                var clientIP = Request.Headers["login"].FirstOrDefault();
                string applicationId = AppCode; //_configuration.GetSection("CorsSettings:ApplicationId").Value;
                
                string userId = _utilities.GetUserid(prefix);



                string companyId = _utilities.GetCompanyId(clientIP);


                //companyId = "1";


                string totalMenuChunks = await _utilities.GetUserRightsInRedis($"{userId}{applicationId}{companyId}GenerateMenuTotalChunks");
                //string totalMenuChunks = await utilities.GetUserRightsInRedis($"{userId}PharmaCRMv2{companyId}GenerateMenuTotalChunks");

                if (!String.IsNullOrEmpty(totalMenuChunks))
                {
                    if (Convert.ToInt32(totalMenuChunks) != 1)
                    {
                        string getMenu = String.Empty;

                        for (int i = 1; i <= Convert.ToInt32(totalMenuChunks); i++)
                        {
                            string getMenuChunks = await _utilities.GetUserRightsInRedis($"{userId}{applicationId}{companyId}{i}GenerateMenu");

                            //string getMenuChunks = await utilities.GetUserRightsInRedis($"{userId}PharmaCRMv2{companyId}{i}GenerateMenu");

                            if (!String.IsNullOrEmpty(getMenuChunks))
                            {
                                getMenuChunks = getMenuChunks.TrimStart('[');
                                getMenuChunks = getMenuChunks.TrimEnd(']');
                                getMenuChunks = getMenuChunks + ",";
                                getMenu = getMenu + getMenuChunks;
                            }
                        }

                        string formMenuItemsWithOutRights = await _utilities.GetUserRightsInRedis($"{applicationId}{companyId}FormMenuItemsWithOutRights");

                        //string formMenuItemsWithOutRights = await utilities.GetUserRightsInRedis($"PharmaCRMv2{companyId}FormMenuItemsWithOutRights");

                        if (!String.IsNullOrEmpty(formMenuItemsWithOutRights))
                        {
                            formMenuItemsWithOutRights = formMenuItemsWithOutRights.TrimStart('[');
                            formMenuItemsWithOutRights = formMenuItemsWithOutRights.TrimEnd(']');
                            getMenu = getMenu + formMenuItemsWithOutRights;
                        }
                        else
                        {                            
                            DataTable dtMenu = _loginComponent.GetFormMenuItemsWithOutRights(companyId);
                            string jsonResultGenerateMenu = JsonConvert.SerializeObject(dtMenu);

                            _utilities.SetUserRightsInRedis("FormMenuItemsWithOutRights", jsonResultGenerateMenu, $"{applicationId}{companyId}");

                            //utilities.SetUserRightsInRedis("FormMenuItemsWithOutRights", jsonResultGenerateMenu, $"PharmaCRMv2{companyId}");

                            jsonResultGenerateMenu = jsonResultGenerateMenu.TrimStart('[');
                            jsonResultGenerateMenu = jsonResultGenerateMenu.TrimEnd(']');
                            getMenu = getMenu + jsonResultGenerateMenu;
                        }

                        getMenu = getMenu.TrimEnd(',');
                        DataTable dtGenerateMenu = (DataTable)JsonConvert.DeserializeObject($"[{getMenu}]", typeof(DataTable));
                        return dtGenerateMenu;
                    }
                    else
                    {
                        string getMenuChunks = await _utilities.GetUserRightsInRedis($"{userId}{applicationId}{companyId}1GenerateMenu");
                        //string getMenuChunks = await utilities.GetUserRightsInRedis($"{userId}PharmaCRMv2{companyId}1GenerateMenu");
                        string formMenuItemsWithOutRights = await _utilities.GetUserRightsInRedis($"{applicationId}{companyId}FormMenuItemsWithOutRights");
                        //string formMenuItemsWithOutRights = await utilities.GetUserRightsInRedis($"PharmaCRMv2{companyId}FormMenuItemsWithOutRights");

                        if (!String.IsNullOrEmpty(formMenuItemsWithOutRights) && formMenuItemsWithOutRights != "[]")
                        {
                            getMenuChunks = getMenuChunks + formMenuItemsWithOutRights;
                        }
                        else
                        {                            
                            DataTable dtMenu = _loginComponent.GetFormMenuItemsWithOutRights(companyId);
                            string jsonResultGenerateMenu = JsonConvert.SerializeObject(dtMenu);
                            _utilities.SetUserRightsInRedis("FormMenuItemsWithOutRights", jsonResultGenerateMenu, $"{applicationId}{companyId}");

                            //utilities.SetUserRightsInRedis("FormMenuItemsWithOutRights", jsonResultGenerateMenu, $"PharmaCRMv2{companyId}");

                            if (jsonResultGenerateMenu != "[]")
                            {
                                getMenuChunks = getMenuChunks + jsonResultGenerateMenu;
                            }
                        }

                        DataTable dtGenerateMenu = (DataTable)JsonConvert.DeserializeObject(getMenuChunks, typeof(DataTable));
                        return dtGenerateMenu;
                    }
                }
                else
                {
                    // Handle the case where totalMenuChunks is empty or null
                    // You can throw an appropriate exception or return a default DataTable
                    return new DataTable(); // Example: Return an empty DataTable
                }
            }
            //catch (SomeSpecificException ex)
            //{
            //    // Handle SomeSpecificException
            //    throw new Exception($"Error processing the menu data: {ex.Message}");
            //}
            //catch (AnotherSpecificException ex)
            //{
            //    // Handle AnotherSpecificException
            //    throw new Exception($"Error processing the menu data: {ex.Message}");
            //}
            catch (Exception ex)
            {
                // Handle any other unexpected exceptions
                throw new Exception($"Internal Server Error: {ex.Message}");
            }
        }
        bool IsNumeric(object value)
        {
            return int.TryParse(value.ToString(), out _);
        }

        //private List<clsChildMenu> AddChildMenuItems(string parentMenuItem, DataTable dtMenus)
        //{
        //    DataView dvChilds = dtMenus.DefaultView;
        //    dvChilds.RowFilter = $"ParentItem='{parentMenuItem}'";
        //    DataTable tblChilds = dvChilds.ToTable();

        //    if (tblChilds.Rows.Count > 0)
        //    {
        //        List<clsChildMenu> list = new List<clsChildMenu>();

        //        foreach (DataRow drChilds in tblChilds.Rows)
        //        {
        //            clsChildMenu obj = new clsChildMenu
        //            {
        //                Value = drChilds["FormID"].ToString(),
        //                NavigateUrl = drChilds["formpath"].ToString(),
        //                FormDescription = drChilds["FormDescription"].ToString()
        //            };

        //            // ... (populate other properties)

        //            obj.SubChild = AddSubChildMenuItems(obj.Value, dtMenus);

        //            list.Add(obj);
        //        }

        //        list = RemoveChid(list);
        //        return list;
        //    }
        //    else
        //    {
        //        return new List<clsChildMenu>();
        //    }
        //}








        private List<clsChildMenu> AddChildMenuItems(string parentMenuItem, DataTable dtMenus)
        {
            DataView dvChilds = null;
            DataTable tblChilds = null;
            List<clsChildMenu> list = new List<clsChildMenu>();
            clsChildMenu obj;

            bool childNode = false;
            bool isSeparator = false;
            string checkSeparator = "";

            dvChilds = dtMenus.DefaultView;
            dvChilds.RowFilter = $"ParentItem='{parentMenuItem}'";
            tblChilds = dvChilds.ToTable();

            if (tblChilds.Rows.Count > 0)
            {
                foreach (DataRow drChilds in tblChilds.Rows)
                {
                    childNode = false;

                    obj = new clsChildMenu
                    {
                        Value = drChilds["FormID"].ToString(),
                        NavigateUrl = drChilds["formpath"].ToString(),
                        formdescription = drChilds["FormDescription"].ToString()
                    };

                    if (drChilds["ISSEPARATOR"].ToString().ToUpper() == "TRUE")
                    {
                        obj.Text = "";
                        obj.ClsSep = "separator";
                    }
                    else
                    {
                        obj.Text = drChilds["Description"].ToString();
                        obj.ClsSep = "";
                    }

                    if (!string.IsNullOrEmpty(checkSeparator) && ((checkSeparator == obj.ClsSep) || (checkSeparator == "hiddenNode" && obj.ClsSep == "separator")))
                    {
                        obj.ClsSep = "hiddenNode";
                    }

                    if (!(obj.ClsSep == "hiddenNode"))
                    {
                        if (drChilds["isParent"].ToString().Equals("1") || drChilds["isParent"].ToString().ToUpper() == "TRUE")
                        {
                            childNode = true;
                            obj.subChild = AddSubChildMenuItems(obj.Value, dtMenus);
                            obj.Class = "has-sub hasChild child";
                        }
                        else
                        {
                            obj.Class = "child";
                        }
                    }
                    else
                    {
                        obj.Class = "hiddenNode";
                    }

                    if (childNode)
                    {
                        if (obj.subChild != null && !RemoveChild(obj))
                        {
                            list.Add(obj);
                        }
                    }
                    else
                    {
                        list.Add(obj);
                    }

                    checkSeparator = obj.ClsSep;
                }

                list = RemoveChid(list);
                return list;
            }
            else
            {
                return null;
            }
        }


        private void HandleSeparatorLogic(clsChildMenu obj, DataRow drChilds)
        {
            bool isSeparator = drChilds["ISSEPARATOR"].ToString().ToUpper() == "TRUE";

            obj.Text = isSeparator ? "" : drChilds["Description"].ToString();
            obj.ClsSep = isSeparator ? "separator" : "";

            string checkSeparator = "";

            if (!string.IsNullOrEmpty(checkSeparator) && ((checkSeparator == obj.ClsSep) || (checkSeparator == "hiddenNode" && obj.ClsSep == "separator")))
            {
                obj.ClsSep = "hiddenNode";
            }

            checkSeparator = obj.ClsSep;
        }


        private List<clsSubChildMenu> AddSubChildMenuItems(string _ParentMenuItem, DataTable _dtMenus)
        {
            DataView dvSubChilds = null;
            DataTable _TblSubChilds = null;
            List<clsSubChildMenu> list = new List<clsSubChildMenu>();
            clsSubChildMenu obj;

            bool childNode = false;
            bool isSeparator = false;
            string checkSeparator = "";

            dvSubChilds = _dtMenus.DefaultView;
            dvSubChilds.RowFilter = "ParentItem='" + _ParentMenuItem + "'";
            _TblSubChilds = dvSubChilds.ToTable();

            if (_TblSubChilds.Rows.Count > 0)
            {
                foreach (DataRow drSubChilds in _TblSubChilds.Rows)
                {
                    childNode = false;

                    obj = new clsSubChildMenu();
                    obj.Value = drSubChilds["FormID"].ToString();
                    obj.NavigateUrl = drSubChilds["formpath"].ToString();
                    obj.formdescription = drSubChilds["FormDescription"].ToString();

                    if (drSubChilds["ISSEPARATOR"].ToString().ToUpper().Equals("TRUE"))
                    {
                        obj.Text = "";
                        obj.CLSSEP = "separator";
                    }
                    else
                    {
                        obj.Text = drSubChilds["Description"].ToString();
                        obj.CLSSEP = "";
                    }

                    if (!string.IsNullOrEmpty(checkSeparator) && ((checkSeparator == obj.CLSSEP) || (checkSeparator == "hiddenNode" && obj.CLSSEP == "separator")))
                        obj.CLSSEP = "hiddenNode";

                    if (!(obj.CLSSEP == "hiddenNode"))
                    {
                        if (drSubChilds["isParent"].ToString().Equals("1") || drSubChilds["isParent"].ToString().ToUpper().Equals("TRUE"))
                        {
                            childNode = true;
                            obj.subSubChild = AddSubSubChildMenuItems(obj.Value, _dtMenus);
                            obj.Class = "has-sub hasChild child";
                        }
                        else
                            obj.Class = "child";
                    }
                    else
                        obj.Class = "hiddenNode";

                    if (childNode)
                    {
                        if (obj.subSubChild != null)
                        {
                            if (!RemoveSubChild(obj)) list.Add(obj);
                        }
                    }
                    else
                        list.Add(obj);

                    checkSeparator = obj.CLSSEP;
                }
                list = RemoveSubChid(list);
                return list;
            }
            else
                return null;
        }

        private List<clsSubSubChildMenu> AddSubSubChildMenuItems(string _ParentMenuItem, DataTable _dtMenus)
        {
            DataView dvChilds = null;
            DataTable _TblChilds = null;

            List<clsSubSubChildMenu> list = new List<clsSubSubChildMenu>();
            clsSubSubChildMenu obj;

            dvChilds = _dtMenus.DefaultView;
            dvChilds.RowFilter = "ParentItem='" + _ParentMenuItem + "'";// +" and isSeparator=0";
            _TblChilds = dvChilds.ToTable();

            if (_TblChilds.Rows.Count > 0)
            {
                foreach (DataRow drChilds in _TblChilds.Rows)
                {
                    obj = new clsSubSubChildMenu();
                    obj.Value = drChilds["FormID"].ToString();
                    obj.NavigateUrl = drChilds["formpath"].ToString();
                    obj.formdescription = drChilds["FormDescription"].ToString();
                    //obj.ImageUrl = _ParentMenuItem;

                    if (drChilds["ISSEPARATOR"].ToString().ToUpper().Equals("TRUE"))
                    {
                        obj.Text = "";
                        obj.CLSSEP = "separator";
                    }
                    else
                    {
                        obj.Text = drChilds["Description"].ToString();
                    }

                    list.Add(obj);
                }

                return list;
            }
            else
            {
                return null;
            }
        }
        private bool RemoveChild(clsChildMenu obj)
        {
            bool remove = true;

            foreach (clsSubChildMenu child in obj.subChild)
            {
                if (child.CLSSEP != "separator" && child.CLSSEP != "hiddenNode")
                {
                    remove = false;
                    break;  // Optimize to exit loop early if a non-removable child is found
                }
            }

            return remove;
        }


        private List<clsChildMenu> RemoveChid(List<clsChildMenu> list)
        {
            int count = list.Count;
            int i = 0;
            bool remove = false;
            string indexes = string.Empty;

            if (count > 0)
            {
                foreach (clsChildMenu lst in list)
                {
                    if (count - 1 > i && (list[i].ClsSep == "separator" || list[i].ClsSep == "hiddenNode") && list[i + 1].ClsSep != "")
                    {
                        indexes += i + ",";
                        remove = true;
                    }
                    i++;
                }

                if (remove)
                {
                    string indexValues = indexes.Substring(0, indexes.Length - 1);
                    string[] separator = { "," };
                    string[] array = indexValues.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                    for (int k = array.Length - 1; k >= 0; k--)
                    {
                        list.RemoveAt(Convert.ToInt32(array[k]));
                    }
                }

                if (list.Count > 2 && list[0].ClsSep == "separator" && list[1].ClsSep == "")
                {
                    list.RemoveAt(0);
                }

                int checkListCount = list.Count;
                if (checkListCount > 0 && (list[checkListCount - 1].ClsSep == "separator" || list[checkListCount - 1].ClsSep == "hiddenNode"))
                {
                    list.RemoveAt(checkListCount - 1);
                }
            }

            return list;
        }


        private bool RemoveSubChild(clsSubChildMenu obj)
        {
            bool remove = true;

            foreach (clsSubSubChildMenu child in obj.subSubChild)
            {
                if (child.CLSSEP != "separator" && child.CLSSEP != "hiddenNode")
                {
                    remove = false;
                    break;  // Optimize to exit loop early if a non-removable child is found
                }
            }

            return remove;
        }
        private List<clsSubChildMenu> RemoveSubChid(List<clsSubChildMenu> list)
        {
            int count = list.Count;
            int i = 0;
            bool remove = false;
            string indexes = string.Empty;

            if (count > 0)
            {
                foreach (clsSubChildMenu lst in list)
                {
                    if (count - 1 > i && (list[i].CLSSEP == "separator" || list[i].CLSSEP == "hiddenNode") && list[i + 1].CLSSEP != "")
                    {
                        indexes += i + ",";
                        remove = true;
                    }
                    i++;
                }

                if (remove)
                {
                    string indexValues = indexes.Substring(0, indexes.Length - 1);
                    string[] separator = { "," };
                    string[] array = indexValues.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                    for (int k = array.Length - 1; k >= 0; k--)
                    {
                        list.RemoveAt(Convert.ToInt32(array[k]));
                    }
                }

                if (list.Count > 2 && list[0].CLSSEP == "separator" && list[1].CLSSEP == "")
                {
                    list.RemoveAt(0);
                }

                int checkListCount = list.Count;
                if (checkListCount > 0 && (list[checkListCount - 1].CLSSEP == "separator" || list[checkListCount - 1].CLSSEP == "hiddenNode"))
                {
                    list.RemoveAt(checkListCount - 1);
                }
            }

            return list;
        }


        private bool RemoveParent(clsRootMenu obj)
        {
            bool remove = true;
            foreach (clsChildMenu child in obj.child)
            {
                if (child.ClsSep != "separator" && child.ClsSep != "hiddenNode")
                {
                    remove = false;
                }
            }
            return remove;
        }






        // If you have more levels, you can continue defining additional classes like clsSubSubChildMenu.
         
    }

    public class MenuCounts
    {
        public string ModuleId { get; set; }
        public int TotalCount { get; set; }
        public string FormPath { get; set; }
        public string ParentItem { get; set; }
        public int Count { get; set; }
        public int Table { get; set; }
        public string Error { get; set; }
    }
}
