using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Components.HCMS.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.ESS;

namespace HCMS_Api.Controllers.HCMS.Common
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabelsFetchingController : ControllerBase
    {
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;

        public LabelsFetchingController(IConfiguration configuration, Utilities utilities, DataServices dataservice)
        {
            _configuration = configuration;
            _utilities = utilities;            
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        [HttpGet("LabelsFetching")]
        public ActionResult<List<clsLabelsFetching>> LabelsFetching(string? CompanyId, string Culture, string? code, string? formid)
        {
            try
            {
                var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
                
                DataSet ds = new DataSet();
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty, Query = string.Empty;

                List<clsLabelsFetching> list = new List<clsLabelsFetching>();
                clsLabelsFetching obj;
                CompanyId = "-1";

                sbr.Append("Exec sp_GetLabelDescription");
                sbr.Append($" @CompanyId = {(string.IsNullOrEmpty(CompanyId) ? _utilities.GetCompanyId(clientIP) : CompanyId)},");
                sbr.Append($" @Culture = '{(string.IsNullOrEmpty(Culture) ? _utilities.GetAppCurrentUICulture(clientIP) : Culture)}'");

                if (!string.IsNullOrEmpty(code))
                {
                    sbr.Append($", @Code = '{code}'");
                }
                if (!string.IsNullOrEmpty(formid))
                {
                    sbr.Append($", @formid = '{formid}'");
                }
                //sbr.Append($" @Code = '{code}',");
                //sbr.Append($" @formid = '{formid}'");

                Query = Convert.ToString(sbr);
                result = _dataservice.ExecuteReader(Query, ref ds);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        obj = new clsLabelsFetching();
                        obj.Code = Convert.ToString(dr["Code"]);
                        obj.Description = Convert.ToString(dr["Description"]);
                        list.Add(obj);
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                return BadRequest($"Some Error occured while fetching Labels, Error Description: {ex.Message}");
            }
        }

        [HttpGet("LabelsFetchingForFilters")]
        public ActionResult<List<clsLabelsFetchingForFilter>> LabelsFetchingForFilters(string CompanyId, string Culture, string code, string formid, string? filter)
        {
            try
            {
                DataSet ds = new DataSet();
                var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
                
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty, Query = string.Empty;

                List<clsLabelsFetchingForFilter> list = new List<clsLabelsFetchingForFilter>();
                clsLabelsFetchingForFilter obj;

                sbr.Append("Exec sp_GetlabelDescriptionForFilters @IsFilter=1,");
                sbr.Append($" @CompanyId = {(string.IsNullOrEmpty(CompanyId) ? _utilities.GetCompanyId(clientIP) : CompanyId)},");
                sbr.Append($" @Culture = '{Culture}',");
                sbr.Append($" @Code = '{code}',");
                sbr.Append($" @formid = '{formid}',");
                sbr.Append($" @filter = '{filter}'");

                Query = Convert.ToString(sbr);
                result = _dataservice.ExecuteReader(Query, ref ds);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        obj = new clsLabelsFetchingForFilter();
                        obj.Code = Convert.ToString(dr["Code"]);
                        obj.Description = Convert.ToString(dr["Description"]);
                        list.Add(obj);
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                return BadRequest($"Some Error occured while fetching Labels, Error Description: {ex.Message}");
            }
        }

        [HttpGet("LabelsFetchingGeneralSetups")]
        public ActionResult<List<GenSetups>> LabelsFetchingGeneralSetups(string smnid, string _companyid, string _culture)
        {
            try
            {
                var clientIP = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();
                
                if (string.IsNullOrEmpty(smnid) || smnid == "undefined")
                {
                    return BadRequest("Undefined Values");
                }

                if (string.IsNullOrEmpty(_companyid) || _companyid == "undefined")
                {
                    _companyid = _utilities.GetCompanyId(clientIP);
                }

                if (string.IsNullOrEmpty(_culture) || _culture == "undefined")
                {
                    _culture = _utilities.GetAppCurrentUICulture(clientIP);
                }

                DataSet ds = new DataSet();
                StringBuilder sbr = new StringBuilder();
                string result = string.Empty, Query = string.Empty;

                sbr.Append("Exec sp_GetDataofGeneralSetups");
                sbr.Append(" @sdlid = 0,");
                sbr.Append($" @smnid = {Convert.ToInt16(smnid)},");
                sbr.Append(" @smsid = 0,");
                sbr.Append($" @companyid = {_companyid},");
                sbr.Append(" @OperationName = 'GetWholeSetups',");
                sbr.Append($" @Culture = '{_culture}'");

                Query = Convert.ToString(sbr);
                result = _dataservice.ExecuteReader(Query, ref ds);

                List<GenSetups> lstGenSetups = new List<GenSetups>();

                foreach (DataRow datarow in ds.Tables[0].Rows)
                {
                    GenSetups objGenSetups = new GenSetups();
                    objGenSetups.smsId = Convert.ToString(datarow["smsid"]);
                    objGenSetups.Code = Convert.ToString(datarow["Code"]);
                    objGenSetups.Name = Convert.ToString(datarow["Name"]);
                    objGenSetups.Flag = Convert.ToString(datarow["Color"]);
                    lstGenSetups.Add(objGenSetups);
                }

                return lstGenSetups;
            }
            catch (Exception ex)
            {
                return BadRequest($"Some Error occured while fetching Labels, Error Description: {ex.Message}");
            }
        }


        [HttpGet("validationFetching")]
        public IActionResult ValidationFetching(string? CompanyId, string? Culture, string? code, string? formid)
        {
            try
            {
                DataSet ds = new DataSet();
                StringBuilder sbr = new StringBuilder();
                string query;

                List<clsValidationFetching> list = new List<clsValidationFetching>();

                sbr.Append("Exec sp_GetValidation");

                if (!string.IsNullOrEmpty(CompanyId))
                    sbr.Append(" @CompanyId = " + CompanyId + ",");

                if (!string.IsNullOrEmpty(Culture))
                    sbr.Append(" @Culture = '" + Culture + "',");

                if (!string.IsNullOrEmpty(code))
                    sbr.Append(" @Code = '" + code + "',");

                if (!string.IsNullOrEmpty(formid))
                    sbr.Append(" @formid = '" + formid + "',");

                // Remove trailing comma if any
                if (sbr[sbr.Length - 1] == ',')
                    sbr.Length--;

                query = sbr.ToString();

                string result = _dataservice.ExecuteReader(query, ref ds);

                if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        var obj = new clsValidationFetching
                        {
                            Code = dr["Code"] != DBNull.Value ? Convert.ToString(dr["Code"]) : null,
                            Description = dr["Description"] != DBNull.Value ? Convert.ToString(dr["Description"]) : null
                        };
                        list.Add(obj);
                    }
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status417ExpectationFailed,
                    $"Some error occurred while fetching labels. Error: \"{ex.Message}\"");
            }
        }

    }
     
}
