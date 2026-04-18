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
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var whereClause = "WHERE CompanyId = @CompanyId";
        if (!string.IsNullOrWhiteSpace(search))
            whereClause += $" AND (UPPER(name) LIKE '%{search}%' OR UPPER(code) LIKE '%{search}%')";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "sdlid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "sdlid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblSetupsdetail {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblSetupsdetail {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllDeptstrMasterAsync(TableFiltersDto input)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var whereClause = "WHERE CompanyId = @CompanyId";
        if (!string.IsNullOrWhiteSpace(search))
            whereClause += $" AND (UPPER(userempname) LIKE '%{search}%' OR UPPER(userempcode) LIKE '%{search}%' OR UPPER(applicationid) LIKE '%{search}%')";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "dptmid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "dptmid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblDeptstrMaster {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblDeptstrMaster {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllDeptstrDetailAsync(TableFiltersDto input)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var whereClause = "WHERE companyId = @CompanyId";
        if (!string.IsNullOrWhiteSpace(search))
            whereClause += $" AND (UPPER(userempname) LIKE '%{search}%' OR UPPER(userempcode) LIKE '%{search}%' OR UPPER(applicationid) LIKE '%{search}%')";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "dptdid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "dptdid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM tblDeptstrDetail {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM tblDeptstrDetail {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllEmpJobProfileAsync(TableFiltersDto input)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var whereClause = "WHERE CompanyId = @CompanyId";
        if (!string.IsNullOrWhiteSpace(search))
            whereClause += $" AND (UPPER(jobtitle) LIKE '%{search}%' OR UPPER(jobcode) LIKE '%{search}%')";

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "jobprofileid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "jobprofileid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

        string dataSql = $@"SELECT * FROM TblEmpJobProfile {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) FROM TblEmpJobProfile {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic> { Items = items, TotalCount = totalCount };
    }

    public async Task<PaginationResult<dynamic>> GetAllEmployeesAsync(TableFiltersDto input)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var whereClause = "WHERE e.CompanyId = @CompanyId AND e.Active = 1";
        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause += $" AND (UPPER(e.firstname) LIKE '%{search}%' OR UPPER(e.lastname) LIKE '%{search}%' OR UPPER(e.empcode) LIKE '%{search}%' OR UPPER(e.email) LIKE '%{search}%' OR UPPER(COALESCE(des.name, des_fallback.name)) LIKE '%{search}%' OR UPPER(r.name) LIKE '%{search}%')";
        }

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
        if (sortColumn.Equals("empid", StringComparison.OrdinalIgnoreCase)) sortColumn = "e.empid";

        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

        string baseQuery = $@"
            FROM tblEmployee e
            LEFT JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND ejp.active = TRUE
            LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
            LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
            LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid";

        string dataSql = $@"SELECT e.*, COALESCE(des.name, des_fallback.name) AS Designation, r.name AS Role {baseQuery} {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) {baseQuery} {whereClause};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

        return new PaginationResult<dynamic>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<dynamic> GetEmployeeByEmpIdAsync(int empId)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);

        var whereClause = "WHERE e.CompanyId = @CompanyId AND e.EmpId = @EmpId";

        var queryParams = new { CompanyId = companyId, EmpId = empId };

        string baseQuery = $@"FROM tblEmployee e";

        string dataSql = $@"SELECT e.* {baseQuery} {whereClause};"; 

        var item = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).FirstOrDefault();

        return item;
    }
    public async Task<PaginationResult<dynamic>> GetEmployeesByRoleIdAsync(int roleId, EmployeeFilterDto input)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        if (string.IsNullOrWhiteSpace(input.DocumentTypeCode))
            throw new CustomException("DocumentTypeCode is mandatory to fetch employees.", 400);

        var whereClause = "WHERE e.CompanyId = @CompanyId AND ejp.roleid = @RoleId AND COALESCE(e.Active, 1) = 1";

        var ualConditions = new List<string> { 
            "TRIM(LEADING '0' FROM TRIM(ual.EmployeeCode::text)) = TRIM(LEADING '0' FROM TRIM(e.empcode::text))",
            "ual.IsActive = TRUE", 
            "ual.IsDeleted = FALSE",
            "ual.DocumentTypeCode = @DocumentTypeCode"
        };

        if (!string.IsNullOrWhiteSpace(input.DivisionCode))
            ualConditions.Add("ual.DivisionCode = @DivisionCode");

        if (!string.IsNullOrWhiteSpace(input.DepartmentCode))
            ualConditions.Add("ual.DepartmentCode = @DepartmentCode");

        if (!string.IsNullOrWhiteSpace(input.SubDepartmentCode))
            ualConditions.Add("ual.SubDepartmentCode = @SubDepartmentCode");

        if (!string.IsNullOrWhiteSpace(input.BusinessDomainCode))
            ualConditions.Add("ual.BusinessDomainCode = @BusinessDomainCode");

        whereClause += $" AND EXISTS (SELECT 1 FROM UserAccessLevels ual WHERE {string.Join(" AND ", ualConditions)})";

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause += $" AND (UPPER(e.firstname) LIKE '%{search}%' OR UPPER(e.lastname) LIKE '%{search}%' OR UPPER(e.empcode) LIKE '%{search}%' OR UPPER(e.email) LIKE '%{search}%' OR UPPER(COALESCE(des.name, des_fallback.name)) LIKE '%{search}%' OR UPPER(r.name) LIKE '%{search}%')";
        }

        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
        if (sortColumn.Equals("empid", StringComparison.OrdinalIgnoreCase)) sortColumn = "e.empid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new 
        { 
            CompanyId = companyId, 
            RoleId = roleId, 
            Offset = offset, 
            PageSize = input.PageSize,
            DocumentTypeCode = input.DocumentTypeCode,
            DivisionCode = input.DivisionCode,
            DepartmentCode = input.DepartmentCode,
            SubDepartmentCode = input.SubDepartmentCode,
            BusinessDomainCode = input.BusinessDomainCode
        };

        string baseQuery = $@"
            FROM tblEmployee e
            INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE
            LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
            LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
            LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid";

        string dataSql = $@"SELECT e.*, COALESCE(des.name, des_fallback.name) AS Designation, r.name AS Role {baseQuery} {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        string countSql = $@"SELECT COUNT(1) {baseQuery} {whereClause};";

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
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var offset = (input.PageNumber - 1) * input.PageSize;
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var conditions = new List<string> { "e.CompanyId = @CompanyId" };

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
            conditions.Add("(e.dsgId = @DesignationId OR EXISTS (SELECT 1 FROM TblEmpJobProfile p WHERE p.empid = e.empid AND (p.dsgid = @DesignationId OR p.ddsgid = @DesignationId) AND p.active = TRUE))");
        }

        if (input.RoleId.HasValue)
        {
            conditions.Add("EXISTS (SELECT 1 FROM TblEmpJobProfile p WHERE p.empid = e.empid AND p.roleid = @RoleId AND p.active = TRUE)");
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
                WHERE p.empid = e.empid AND b.smsid = 189 AND b.name IN ({rolesList}) AND p.active = TRUE
            )");
        }

        // Cabinet Filters via UserAccessLevels
        if (!string.IsNullOrEmpty(input.DivisionCode) ||
            !string.IsNullOrEmpty(input.DepartmentCode) ||
            !string.IsNullOrEmpty(input.SubDepartmentCode) ||
            !string.IsNullOrEmpty(input.BusinessDomainCode))
        {
            var ualConditions = new List<string> { "LTRIM(RTRIM(ual.EmployeeCode), '0') = LTRIM(RTRIM(e.EmpCode), '0')", "ual.IsActive = TRUE", "ual.IsDeleted = FALSE" };

            if (!string.IsNullOrEmpty(input.DivisionCode)) ualConditions.Add("ual.DivisionCode = @DivisionCode");
            if (!string.IsNullOrEmpty(input.DepartmentCode)) ualConditions.Add("ual.DepartmentCode = @DepartmentCode");
            if (!string.IsNullOrEmpty(input.SubDepartmentCode)) ualConditions.Add("ual.SubDepartmentCode = @SubDepartmentCode");
            if (!string.IsNullOrEmpty(input.BusinessDomainCode)) ualConditions.Add("ual.BusinessDomainCode = @BusinessDomainCode");

            conditions.Add($"EXISTS (SELECT 1 FROM UserAccessLevels ual WHERE {string.Join(" AND ", ualConditions)})");
        }

        string whereClause = "WHERE " + string.Join(" AND ", conditions);
        string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
        string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

        var queryParams = new
        {
            CompanyId = companyId,
            ReportingTo = input.ReportingTo,
            DesignationId = input.DesignationId,
            RoleId = input.RoleId,
            DivisionCode = input.DivisionCode,
            DepartmentCode = input.DepartmentCode,
            SubDepartmentCode = input.SubDepartmentCode,
            BusinessDomainCode = input.BusinessDomainCode,
            Offset = offset,
            PageSize = input.PageSize
        };
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
            string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            input.CompanyId = int.Parse(companyIdStr);
            string insertQuery = $@"
                INSERT INTO tblEmployee
                (
                    EmpCode, CompanyId, FirstName, LastName, Email, MobileNumber, 
                    dptId, dsgId, DateofBirth, DateJoin, ReportTo, Active
                )
                VALUES
                (
                    @EmpCode, @CompanyId, @FirstName, @LastName, @Email, @MobileNumber, 
                    @dptId, @dsgId, @DateofBirth, @DateJoin, @ReportTo, 1
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
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            string query = $@"
            select distinct  a.roleid,b.name from public.tblempjobprofile a
            inner join public.tblsetupsdetail b on a.roleid = b.sdlid
            where b.smsid = 189 and a.roleid is not null AND a.CompanyId = '{CompanyId}' AND a.Active =TRUE;";

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
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            string query = $@"select empcode, firstname, midname, lastname from tblEmployee where CompanyId = '{CompanyId}';";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("empcode"),
                    Value = row.Field<string>("firstname") + " " + row.Field<string>("midname") + " " + row.Field<string>("lastname")
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

            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            string query = $@"select DISTINCT a.dsgid, b.name from public.tblempjobprofile a
                    left join public.tblsetupsdetail b
                    on a.dsgid= b.sdlid
                    where b.smsid = 3 AND a.CompanyId = '{CompanyId}' AND a.Active =TRUE";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("dsgid"),
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
}