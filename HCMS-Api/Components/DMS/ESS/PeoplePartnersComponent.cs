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
        try
        {

            string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int companyId = int.Parse(companyIdStr);
            var offset = (input.PageNumber - 1) * input.PageSize;
            var search = input.SearchText?.Replace("'", "''").ToUpper();

            // Sorting logic - mapping frontend names to SQL column aliases
            string innerSortColumn = input.SortColumn?.ToLower() switch
            {
                "employeename" => "LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, '')))",
                "designation" => "designation",
                "division" => "division",
                "department" => "department",
                "subdepartment" => "subdepartment",
                "empcode" => "e.empcode",
                "role" => "role_name",
                _ => "e.empid"
            };

            string outerSortColumn = input.SortColumn?.ToLower() switch
            {
                "employeename" => "LTRIM(RTRIM(COALESCE(fe.firstname, '') || ' ' || COALESCE(fe.midname, '') || ' ' || COALESCE(fe.lastname, '')))",
                "designation" => "fe.designation",
                "division" => "fe.division",
                "department" => "fe.department",
                "subdepartment" => "fe.subdepartment",
                "empcode" => "fe.empcode",
                "role" => "fe.role_name",
                _ => "fe.empid"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";
            var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

            // Search condition for all specific columns
            var searchCondition = "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                searchCondition = $@" AND (
                    UPPER(e.firstname) LIKE '%{search}%' OR 
                    UPPER(e.lastname) LIKE '%{search}%' OR 
                    UPPER(LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, '')))) LIKE '%{search}%' OR 
                    UPPER(LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, '')))) LIKE '%{search}%' OR 
                    UPPER(e.empcode) LIKE '%{search}%' OR 
                    UPPER(r.name) LIKE '%{search}%' OR 
                    UPPER(div.name) LIKE '%{search}%' OR 
                    UPPER(dept.name) LIKE '%{search}%' OR 
                    UPPER(subdept.name) LIKE '%{search}%' OR
                    UPPER(COALESCE(des.name, des_fallback.name)) LIKE '%{search}%'
                )";
            }

            // CTE Approach: Paging aur Filtering aik sath
            // Base Joins count aur data dono queries mein use honge
            string joinClause = $@"
                LEFT JOIN tblsetupsdetail div ON e.divid = div.sdlid AND div.smsid = 70
                LEFT JOIN tblsetupsdetail dept ON e.mdptid = dept.sdlid AND dept.smsid = 84
                LEFT JOIN tblsetupsdetail subdept ON e.dptid = subdept.sdlid AND subdept.smsid = 24
                LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
                LEFT JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND ejp.active = TRUE
                LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
                LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid";

                    string dataSql = $@"
                    WITH FilteredEmployees AS (
                        SELECT 
                            e.empid, e.empcode, e.firstname, e.midname, e.lastname, 
                            e.dsgid, e.divid, e.mdptid, e.dptid, e.nicnew, 
                            e.mobile, e.email, e.datejoin,
                            COALESCE(des.name, des_fallback.name) AS designation,
                            div.name AS division,
                            dept.name AS department,
                            subdept.name AS subdepartment,
                            r.name AS role_name,
                            ejp.roleid AS role_id
                        FROM tblEmployee e
                        {joinClause}
                        WHERE e.CompanyId = @CompanyId AND e.Active = 1 {searchCondition}
                        ORDER BY {innerSortColumn} {sortDirection}
                        LIMIT @PageSize OFFSET @Offset
                    )
                    SELECT 
                        fe.empid,
                        fe.empcode,
                        fe.firstname,
                        fe.midname,
                        fe.lastname,
                        fe.designation,
                        fe.dsgid AS designation_id,
                        fe.division,
                        fe.divid AS division_id,
                        fe.department,
                        fe.mdptid AS department_id,
                        fe.subdepartment,
                        fe.dptid AS subdepartment_id,
                        fe.role_name AS role,
                        fe.role_id,
                        fe.nicnew,
                        fe.mobile,
                        fe.email,
                        fe.datejoin
                    FROM FilteredEmployees fe
                    ORDER BY {outerSortColumn} {sortDirection};";

                    string countSql = $@"
                SELECT COUNT(1) 
                FROM tblEmployee e 
                {joinClause}
                WHERE e.CompanyId = @CompanyId AND e.Active = 1 {searchCondition};";

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    //public async Task<PaginationResult<dynamic>> GetAllEmployeesAsync(TableFiltersDto input)
    //{
    //    string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
    //    int companyId = int.Parse(companyIdStr);
    //    var offset = (input.PageNumber - 1) * input.PageSize;
    //    var search = input.SearchText?.Replace("'", "''").ToUpper();

    //    var whereClause = "WHERE e.CompanyId = @CompanyId AND e.Active = 1";
    //    if (!string.IsNullOrWhiteSpace(search))
    //    {
    //        whereClause += $" AND (UPPER(e.firstname) LIKE '%{search}%' OR UPPER(e.lastname) LIKE '%{search}%' OR UPPER(e.empcode) LIKE '%{search}%' OR UPPER(e.email) LIKE '%{search}%' OR UPPER(COALESCE(des.name, des_fallback.name)) LIKE '%{search}%' OR UPPER(r.name) LIKE '%{search}%')";
    //    }

    //    string sortColumn = string.IsNullOrWhiteSpace(input.SortColumn) ? "empid" : new string(input.SortColumn.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    //    if (string.IsNullOrWhiteSpace(sortColumn)) sortColumn = "empid";
    //    if (sortColumn.Equals("empid", StringComparison.OrdinalIgnoreCase)) sortColumn = "e.empid";

    //    string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

    //    var queryParams = new { CompanyId = companyId, Offset = offset, PageSize = input.PageSize };

    //    string baseQuery = $@"
    //        FROM tblEmployee e
    //        LEFT JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND ejp.active = TRUE
    //        LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
    //        LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
    //        LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid";

    //    string dataSql = $@"SELECT e.*, COALESCE(des.name, des_fallback.name) AS Designation, r.name AS Role {baseQuery} {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
    //    string countSql = $@"SELECT COUNT(1) {baseQuery} {whereClause};";

    //    var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
    //    var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

    //    return new PaginationResult<dynamic>
    //    {
    //        Items = items,
    //        TotalCount = totalCount
    //    };
    //}

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
    // Returns every active employee holding the given Role -- no DocumentType requirement and
    // no pagination, since this now backs a "select all by default" flow (DRUsersComponent) as
    // well as the "modify selection" picker (UsersInRoleModal), both of which need the complete
    // set of candidates in one shot rather than a browsable page.
    public async Task<PaginationResult<dynamic>> GetEmployeesByRoleIdAsync(int roleId, EmployeeFilterDto input)
    {
        string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int companyId = int.Parse(companyIdStr);
        var search = input.SearchText?.Replace("'", "''").ToUpper();

        var whereClause = "WHERE e.CompanyId = @CompanyId AND ejp.roleid = @RoleId AND COALESCE(e.Active, 1) = 1";

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
            RoleId = roleId
        };

        string baseQuery = $@"
            FROM tblEmployee e
            INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE
            LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
            LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
            LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid";

        string dataSql = $@"SELECT e.*, COALESCE(des.name, des_fallback.name) AS Designation, r.name AS Role {baseQuery} {whereClause} ORDER BY {sortColumn} {sortDirection};";

        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();

        return new PaginationResult<dynamic>
        {
            Items = items,
            TotalCount = items.Count
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);
            input.CompanyId = CompanyId;

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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
            select distinct  a.roleid,b.name from public.tblempjobprofile a
            inner join public.tblsetupsdetail b on a.roleid = b.sdlid
            where b.smsid = 189 and a.roleid is not null AND a.CompanyId = {CompanyId} AND a.Active =TRUE;";

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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"select empcode, firstname, midname, lastname from tblEmployee where CompanyId = {CompanyId};";

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

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"select DISTINCT a.dsgid, b.name from public.tblempjobprofile a
                    left join public.tblsetupsdetail b
                    on a.dsgid= b.sdlid
                    where b.smsid = 3 AND a.CompanyId = {CompanyId} AND a.Active =TRUE";

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


    public async Task<IQueryable<SelectList2Dto>> GetDivisionListAsync()
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"SELECT sdlid, Name from tblsetupsdetail where smsid= 70 AND CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("sdlid"),
                    Value = row.Field<string>("Name")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IQueryable<SelectList2Dto>> GetDepartmentListAsync()
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"SELECT sdlid, Name from tblsetupsdetail where smsid = 84 and CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("sdlid"),
                    Value = row.Field<string>("Name")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IQueryable<SelectList2Dto>> GetSubDepartmentListAsync()
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"select DISTINCT a.dsgid, b.name from public.tblempjobprofile a
                    left join public.tblsetupsdetail b
                    on a.dsgid= b.sdlid
                    where b.smsid = 3 AND a.CompanyId = {CompanyId}";

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

    public async Task<List<dynamic>> GetHeadByDivisionIdAsync(int divId)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);


            string query = $@"SELECT 
                            empid, 
                            e.empcode AS EmployeeCode, 
                            e.firstname || ' ' || e.midname || ' ' || e.lastname AS FullName,
                            dsg.name AS Designation
                        FROM public.tblemployee e
                        LEFT JOIN public.tblsetupsdetail dsg ON e.dsgid = dsg.sdlid
                        WHERE e.CompanyId = {CompanyId} AND  e.divid = {divId} 
                          -- Only targeting top designations
                          AND (dsg.name LIKE '%Director%' OR dsg.name LIKE '%General Manager%')
                        ORDER BY 
                            CASE 
                                WHEN dsg.name LIKE '%Senior Director%' THEN 1
                                WHEN dsg.name LIKE '%Director%' THEN 2
                                WHEN dsg.name LIKE '%Sr. General Manager%' THEN 3
                                WHEN dsg.name LIKE '%General Manager%' THEN 4
                                ELSE 5 
                            END ASC
                        LIMIT 1;";

            var result = (await _common.QueryAsync<dynamic>(query)).ToList();

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IQueryable<SelectList2Dto>> GetDepartmentsByDivisionIdAsync(int divisionId)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT DISTINCT
                    dept.sdlid AS DepartmentId,
                    dept.Name AS DepartmentName
                FROM public.tbldeptstrmaster m
                JOIN public.tblsetupsdetail dept ON m.mdptid = dept.sdlid
                WHERE m.divid = {divisionId}
                  AND m.companyid = {CompanyId}
                  AND dept.smsid = 84
                  AND dept.inactive = FALSE;";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("DepartmentId"),
                    Value = row.Field<string>("DepartmentName")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }



    public async Task<List<dynamic>> GetEmployeeByDivisionIdAsync(int divId)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);


            string query = $@"SELECT 
                    e.empcode AS EmployeeCode, 
                     e.firstname || ' ' || e.midname || ' ' || e.lastname AS FullName,
                    dsg.name AS Designation,
                    dept.name AS Department,
                    e.mobile AS MobileNumber,
                    e.email AS EmailAddress
                FROM public.tblemployee e
                -- Join for Designation
                LEFT JOIN public.tblsetupsdetail dsg 
                    ON e.dsgid = dsg.sdlid
                -- Join for Department (Using mdptid as per your confirmed data)
                LEFT JOIN public.tblsetupsdetail dept 
                    ON e.mdptid = dept.sdlid 
                    AND dept.smsid = 84
                WHERE e.divid = {divId}
                  AND e.companyid = {CompanyId}
                ORDER BY dept.name, e.firstname";

            var result = (await _common.QueryAsync<dynamic>(query)).ToList();

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }
}