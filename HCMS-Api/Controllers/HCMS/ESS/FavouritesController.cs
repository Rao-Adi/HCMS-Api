using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.Common.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace HCMS_Api.Controllers.HCMS.ESS
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavouritesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;
        private DataServices? dataservice = null;

        public FavouritesController(IConfiguration configuration, ILogger<UtilitiesController> logger, Utilities utilities
            ,ClientContextService clientContextService)
        {
            _configuration = configuration;
            _logger = logger;
            _utilities = utilities;
            _clientContextService = clientContextService;
        }

        [HttpPost("AddDeleteFavouriteForm")]
        public IActionResult AddDeleteFavouriteForm([FromBody] clsFavourites obj)
        {
            try
            {
                object objresult = null;
                UserInfo objuser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                string UserId = _utilities.GetUserid(_clientContextService.GetClientIP());

                string UserEmpName = objuser.UserEmpName;
                int UserEmpId = objuser.UserEmpId;

                string query = "";
                if (obj.action == false)
                {
                    query = " delete from tblFavorites where FormId = '" + obj.FormId + "' and UserEmpId = " + UserEmpId + " and UserId = '" + UserId + "' and CompanyId = '" + CompanyId + "' and ApplicationId = 'HRIS';";
                    query += "    insert into tblFavorites (FormId,FormName,CompanyId,ApplicationId,UserId,UserEmpId,UserEmpName,FormUrl)" +
                               "    Values" +
                               "    ('" + obj.FormId + "', '" + obj.FormName + "', '" + CompanyId + "','HRIS','" + UserId + "', '" + UserEmpId + "', '" + UserEmpName + "', '" + obj.FormUrl + "')";
                }
                else
                {
                    query = " delete from tblFavorites where FormId = '" + obj.FormId + "' and UserEmpId = " + UserEmpId + " and UserId = '" + UserId + "' and CompanyId = '" + CompanyId + "' and ApplicationId = 'HRIS'";
                }

                string result = dataservice.ExecuteStatement(query, ref objresult, 1);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(417, ex.Message); // Equivalent of ExpectationFailed in ASP.NET Core
            }
        }

        [HttpGet("CheckFavouriteForm/{FormId}")]
        public IActionResult CheckFavouriteForm(string FormId)
        {
            bool returnval = false;

            try
            {                 
                UserInfo objuser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());
                string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                string UserId = _utilities.GetUserid(_clientContextService.GetClientIP());
                 
                int UserEmpId = objuser.UserEmpId;

                string query = "select count(*) from tblFavorites where FormId = '" + FormId + "' and UserEmpId = " + UserEmpId + " and UserId = '" + UserId + "' and CompanyId = '" + CompanyId + "' and ApplicationId = 'HRIS'";

                // Get scalar data from Utilities (assumed to be synchronous)
                object objvalue = _utilities.GetScalarData(query);

                if (objvalue != null && Convert.ToInt32(objvalue) > 0)
                {
                    returnval = true;
                }
            }
            catch (Exception ex)
            {
                // Log exception or handle it accordingly if needed
                return StatusCode(500, ex.Message); // Internal Server Error
            }

            return Ok(returnval);
        }

        [HttpGet("GetFavouitesMenu")]
        public IActionResult GetFavouitesMenu()
        {
            DataTable dt = new DataTable();

            try
            {                
                UserInfo objuser = _utilities.GetCurrentUserMap(_clientContextService.GetClientIP());

                string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                string UserId = _utilities.GetUserid(_clientContextService.GetClientIP());
                int UserEmpId = objuser.UserEmpId;

                string query = "select distinct f.FormId,f.FormName,f.FormUrl,m.FormDescription " +
                               "from tblFavorites f inner join " +
                               "FormMenuItems m on m.FormID = f.FormId " +
                               "where f.CompanyId = '" + CompanyId + "' and f.UserId = '" + UserId + "' and f.UserEmpId = '" + UserEmpId + "' and m.ApplicationCode = 'HRIS' and m.Active = 1";

                DataSet ds = new DataSet();
                string result = dataservice.ExecuteReader(query, ref ds);
                if (ds != null && ds.Tables.Count > 0)
                    dt = ds.Tables[0];

                return Ok(dt); // Return DataTable as a JSON result
            }
            catch (Exception ex)
            {
                return StatusCode(204); // Equivalent of NoContent in ASP.NET Core
            }
        }



    }


    public class clsFavourites
    {
        public string FormId { get; set; }
        public string FormName { get; set; }
        public string FormUrl { get; set; }
        public bool action { get; set; }
    }


}
