using HCMS_Api.Models;
using HCMS_Api.Services.LookupService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text.Json;

namespace HCMS_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LookupController : ControllerBase
    {
        private readonly ILookupService _lookupService;
        private readonly Common.Common _common;

        public LookupController(ILookupService lookupService, Common.Common common)
        {
            _lookupService = lookupService;
            _common = common;
        }

        [HttpGet("{companyId}")]
        public async Task<IActionResult> GetHodNonAtiveAsync(int companyId)
        {
            DataTable table = await _lookupService.GetEmployeeLookupAsync(companyId);
            return Ok(_common.GetJsonDatatable(table));
        }
        [HttpGet("employeeauthority/lookupemployee/{companyId}")]
        public async Task<IActionResult> GetEmployeeHODLookupAsync(int companyId)
        {
            DataTable table = await _lookupService.GetEmployeeLookupAsync(companyId);
            return Ok(_common.GetJsonDatatable(table));
        }

        [HttpGet("company")]
        public async Task<IActionResult> GetCompanyLookupAsync()
        {
            //var userDataClaim = User.FindFirst("UserData")?.Value;
            //var userData = JsonSerializer.Deserialize<JwtArray>(userDataClaim);
            DataTable table = await _lookupService.GetCompanyLookupAsync();
            return Ok(_common.GetJsonDatatable(table));
        }


        [HttpGet("employee/{companyId}/{culture}/{status}")]
        public async Task<IActionResult> lookUpDefinedAuthority(int companyId, string culture, string status)
        {
            DataTable table = await _lookupService.lookUpDefinedAuthority(companyId, culture, status);
            return Ok(_common.GetJsonDatatable(table));
        }
        [HttpGet("employeeSelectedAuthority/{Company}/{_Culture}/{EmpDefined}")]
        public async Task<IActionResult> lookUpSelectedAuthority(string Company, string _Culture, string EmpDefined)
        {
            DataTable table = await _lookupService.lookUpSelectedAuthority(Company, _Culture, EmpDefined);
            return Ok(_common.GetJsonDatatable(table));
        }

        [HttpGet("lookUpEmpGrid/{Company}/{_Culture}")]
        public async Task<IActionResult> lookUpEmpGrid(string Company, string _Culture)
        {
            DataTable table = await _lookupService.lookUpEmpGrid(Company, _Culture);
            return Ok(_common.GetJsonDatatable(table));
        }

        [HttpGet("employeestatuslist")]
        public async Task<IActionResult> GetEmployeeStatusAsync()
        {
            DataTable table = await _lookupService.GetEmployeeStatusAsync();
            return Ok(_common.GetJsonDatatable(table));
        }

        [HttpGet("resignemployeehod")]
        public string GetResignEmployeeHoD(string Company, string EmpId, string _culture)
        {
            string result = _lookupService.GetResignEmployeeHoD(Company, EmpId, _culture);
            return result;
        }

    }
}
