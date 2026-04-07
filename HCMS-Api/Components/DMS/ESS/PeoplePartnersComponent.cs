using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class PeoplePartnersComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;

    public PeoplePartnersComponent(
        DMSUtilities utilities,
        DMSDataServices dataservice,
        IConfiguration configuration,
        ClientContextService clientContextService,
        IDMSDapperDataService dapper,
        IHttpContextAccessor http,
        DMSCommon common)
    {
        _http = http;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common;
    }

    public async Task<PaginationResult<dynamic>> GetAllSetupsDetailAsync(TableFiltersDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var whereClause = string.IsNullOrWhiteSpace(search) 
            ? "" 
            : $"WHERE UPPER(name) LIKE '%{search}%' OR UPPER(code) LIKE '%{search}%'";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "sdlid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "sdlid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblSetupsdetail {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblSetupsdetail {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllDeptstrMasterAsync(TableFiltersDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var whereClause = string.IsNullOrWhiteSpace(search) 
            ? "" 
            : $"WHERE UPPER(userempname) LIKE '%{search}%' OR UPPER(userempcode) LIKE '%{search}%' OR UPPER(applicationid) LIKE '%{search}%'";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "dptmid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "dptmid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblDeptstrMaster {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblDeptstrMaster {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllDeptstrDetailAsync(TableFiltersDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var whereClause = string.IsNullOrWhiteSpace(search) 
            ? "" 
            : $"WHERE UPPER(userempname) LIKE '%{search}%' OR UPPER(userempcode) LIKE '%{search}%' OR UPPER(applicationid) LIKE '%{search}%'";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "dptdid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "dptdid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblDeptstrDetail {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblDeptstrDetail {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllEmpJobProfileAsync(TableFiltersDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var whereClause = string.IsNullOrWhiteSpace(search) 
            ? "" 
            : $"WHERE UPPER(jobtitle) LIKE '%{search}%' OR UPPER(jobcode) LIKE '%{search}%'";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "jobprofileid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "jobprofileid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM TblEmpJobProfile {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM TblEmpJobProfile {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllEmployeesAsync(TableFiltersDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var whereClause = string.IsNullOrWhiteSpace(search) 
            ? "" 
            : $"WHERE UPPER(firstname) LIKE '%{search}%' OR UPPER(lastname) LIKE '%{search}%' OR UPPER(empcode) LIKE '%{search}%' OR UPPER(email) LIKE '%{search}%'";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblEmployee {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblEmployee {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> 
        { 
            Items = items, 
            TotalCount = totalCount 
        };
    }

    public async Task<PaginationResult<dynamic>> GetEmployeesByRoleIdAsync(int roleId, TableFiltersDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var whereClause = "WHERE EXISTS (SELECT 1 FROM TblEmpJobProfile p WHERE p.empid = e.empid AND p.roleid = @RoleId)";
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause += $" AND (UPPER(e.firstname) LIKE '%{search}%' OR UPPER(e.lastname) LIKE '%{search}%' OR UPPER(e.empcode) LIKE '%{search}%' OR UPPER(e.email) LIKE '%{search}%')";
        }

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { RoleId = roleId, Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT e.* FROM tblEmployee e {whereClause} ORDER BY e.{sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblEmployee e {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> 
        { 
            Items = items, 
            TotalCount = totalCount 
        };
    }

    public async Task<PaginationResult<dynamic>> GetEmployeesByCustomFiltersAsync(EmployeeFilterDto input)
    {
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();
        
        var conditions = new List<string> { "1=1" };
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add($"(UPPER(e.firstname) LIKE '%{search}%' OR UPPER(e.lastname) LIKE '%{search}%' OR UPPER(e.empcode) LIKE '%{search}%' OR UPPER(e.email) LIKE '%{search}%')");
        }

        if (input.ReportingTo.HasValue)
        {
            conditions.Add("e.ReportTo >= @ReportingTo");
        }

        if (input.DesignationId.HasValue)
        {
            conditions.Add("e.dsgId = @DesignationId");
        }

        if (input.RoleId.HasValue)
        {
            conditions.Add("EXISTS (SELECT 1 FROM TblEmpJobProfile p WHERE p.empid = e.empid AND p.roleid = @RoleId)");
        }

        // Dynamic Role Checking for Head of Divisions/Departments based on standard setup detail names
        var headRoles = new List<string>();
        if (input.IsHeadOfDivision == true) headRoles.Add("'Division Head'");
        if (input.IsHeadOfDepartment == true) headRoles.Add("'Department Head'");
        if (input.IsHeadOfSubDepartment == true) headRoles.Add("'Sub-Department Head'");

        if (headRoles.Any())
        {
            string rolesList = string.Join(",", headRoles);
            conditions.Add($@"EXISTS (
                SELECT 1 FROM TblEmpJobProfile p 
                INNER JOIN tblsetupsdetail b ON p.roleid = b.sdlid 
                WHERE p.empid = e.empid AND b.smsid = 189 AND b.name IN ({rolesList})
            )");
        }

        string whereClause = "WHERE " + string.Join(" AND ", conditions);
        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { ReportingTo = input.ReportingTo, DesignationId = input.DesignationId, RoleId = input.RoleId, Offset = offset, PageSize = input.PageSize };
        string dataSql = $@"SELECT e.* FROM tblEmployee e {whereClause} ORDER BY e.{sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblEmployee e {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<int> CreateEmployeeAsync(EmployeeCreateDto input)
    {
        try
        {
            string insertQuery = $@"
                INSERT INTO tblEmployee
                (
                    EmpCode, CompanyId, FirstName, LastName, Email, MobileNumber, 
                    dptId, dsgId, DateofBirth, DateJoin, Reportto Active
                )
                VALUES
                (
                    @EmpCode, @CompanyId, @FirstName, @LastName, @Email, @MobileNumber, 
                    @dptId, @dsgId, @DateofBirth, @DateJoin, @ReportTo 1
                )
                RETURNING EmpId;";

            int newId = await _common.ExecuteScalarAsync<int>(insertQuery, input);
            return newId;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IQueryable<SelectList2Dto>> GetRoleListAsync()
    {
        try
        {
            string query = @"
            select distinct b.name, a.roleid from public.tblempjobprofile a
            inner join public.tblsetupsdetail b on a.roleid = b.sdlid
            where b.smsid = 189 and a.roleid is not null;";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("roleid"),
                    Value = row.Field<string>("name")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IQueryable<SelectListDto>> GetAllEmployeeList()
    {
        try
        {
            string query = @"select empcode,firstname,lastname from tblEmployee;";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("empcode"),
                    Value = row.Field<string>("firstname") + " " + row.Field<string>("lastname")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IQueryable<SelectList2Dto>> GetDesignationListAsync()
    {
        try
        {
            string query = @"
                SELECT DISTINCT 
                    dsgid AS Designation_Id, 
                    jobtitle AS Designation_Value 
                FROM 
                    public.tblempjobprofile 
                WHERE 
                    active = true 
                ORDER BY 
                    jobtitle;";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("Designation_Id"),
                    Value = row.Field<string>("Designation_Value")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }
}