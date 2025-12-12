using HCMS_Api.Common;
using HCMS_Api.Services.HodService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static HCMS_Api.Controllers.HodSetupController;
using Microsoft.AspNetCore.Authorization;

namespace HCMS_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HodSetupController : ControllerBase
    {
        private readonly IHodService _hodService;
        private readonly Common.Common _common;


        public HodSetupController(IHodService hodService, Common.Common common)
        {
            _hodService = hodService;
            _common = common;
        }

        [HttpGet]
        public async Task<IActionResult> GetHodNonAtiveAsync()
        {
            DataTable table = await _hodService.GetHodNonAtiveAsync();
            return Ok(_common.GetJsonDatatable(table));
        }

        [HttpGet("employeeauthorityhod")]
        public async Task<IActionResult> GetEmployeeAuthorityHODAsync(string OrderBy, string _Culture, string _CompanyId, string OldEmpId)
        {
            DataTable table = await _hodService.GetEmployeeAuthorityHODAsync(OrderBy, _Culture, _CompanyId, OldEmpId);
            return Ok(_common.GetJsonDatatable(table));
        }
        [HttpPost("UpdateHoDAll")]
        public async Task<ActionResult<List<HoDData>>> UpdateHoDAll([FromBody] List<HoDData> hodData, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        {
            if (hodData == null || !hodData.Any())
            {
                return BadRequest(new { message = "No data received or data is empty." });
            }

            // Process the received data as needed
            // Example: 
            bool table = _hodService.UpdateHoDAll(hodData, SelectedCompany, _UserId, _UserEmpId, _UserEmpCode, _UserEmpName, _FormId, _EntTerminal, _EntTerminalIP);

            return Ok(hodData);  // Returning the received list for testing
        }

        //[HttpPost("UpdateHoDAll")]
        //public Task<ActionResult<List<HoDData>>> UpdateHoDAll(List<HoDData> data, string SelectedCompany, string _UserId, string _UserEmpId, string _UserEmpCode, string _UserEmpName, string _FormId, string _EntTerminal, string _EntTerminalIP)
        //{
        //    if (data == null || !data.Any())
        //    {
        //        return BadRequest(new { message = "No data received or data is empty." });
        //    }

        //    return Ok(data);
        //    bool table =  _hodService.UpdateHoDAll(data, SelectedCompany, _UserId, _UserEmpId, _UserEmpCode, _UserEmpName, _FormId, _EntTerminal, _EntTerminalIP);

        //}

        public class HoDData
        {
            public int HodId { get; set; }
            public int DivId { get; set; }
            public int MDeptId { get; set; }
            public int DeptId { get; set; }
            public int CompanyId { get; set; }
            public int EmpID { get; set; }
            public string Division { get; set; }
            public string MainDepartment { get; set; }
            public string Department { get; set; }
            public int CCode { get; set; }
            public string CompanyDescription { get; set; }
            public string EmpCode { get; set; }
            public string Name { get; set; }
        }



    }
}
