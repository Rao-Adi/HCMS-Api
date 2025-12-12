using Microsoft.AspNetCore.Mvc;
using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.Models;
using System.Data;
using System.Globalization;
using System.Text;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;
using HCMS_Api.Controllers.HCMS.ESS;
using Dapper;
using System.Data.SqlClient;
using System.Data.Common;
using Azure;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
namespace HCMS_Api.Components.HCMS.ESS
{
    public class AttendanceSheetComponent : Controller
    {
        private readonly Utilities _utilities;
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly ClientContextService _clientContextService;
        private readonly IDapperDataService _dapperService;
        private readonly ILogger<UtilitiesController> _logger;
        private readonly IHttpContextAccessor _http;
        public AttendanceSheetComponent(
            Utilities utilities
            , DataServices dataservice
            , IConfiguration configuration
            , ClientContextService clientContextService
            , IDapperDataService dapper
            , ILogger<UtilitiesController> logger
            , IHttpContextAccessor http
            )
        {
            _http = http;
            _logger = logger;
            _utilities = utilities;
            _dataservice = dataservice;
            _configuration = configuration;
            _clientContextService = clientContextService;
            _dapperService = dapper;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);

        }
        public async Task<List<UserInfo>> GetUsersAsync(string companyId)
        {
            var clientIp = _clientContextService.GetClientIP();
            var objUser = _utilities.GetCurrentUserMap(clientIp);
            var userId = objUser.UserID;
            var userList = new List<UserInfo>
    {
        new UserInfo { UserID = "All", UserEmpName = "All" }
    };

            string sql = @"
        SELECT DISTINCT p.UserEmpName, p.CreatedBy AS UserID
        FROM tblAttnSheetParameter p 
        INNER JOIN tblAttnParamSharedUser s ON p.attendid = s.attendid
        WHERE p.CreatedBy != @CurrentUserId AND p.companyid = @CompanyId";

            var dbUsers = await _dapperService.QueryAsync<UserInfo>(sql, new
            {
                CurrentUserId = userId,
                CompanyId = companyId
            });

            userList.AddRange(dbUsers);
            return userList;
        }
        public async Task<List<IDNameDataModel>> Getcompanies(string loginCompanyId)
        {
            try
            {
                List<IDNameDataModel> dataList = new List<IDNameDataModel>();
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var parameters = new DynamicParameters();
                    parameters.Add("@LoginCompanyID", loginCompanyId);
                    var rows = await connection.QueryAsync<IDNameDataModel>(
            "PFA_SP_GetCompanies",
            parameters,
            commandType: CommandType.StoredProcedure);

                    return rows.AsList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCompaniesAsync (LoginCompanyID: {LoginCompanyID})", loginCompanyId);               
                return new List<IDNameDataModel>();
            }
        }
        public async Task<PagedResult<AttnSheetParameter>> GetAttnParametersAsync(
        string UserID, string Self, string ShareUserId)
        {
            
            string sql;
            var parameters = new DynamicParameters();
            parameters.Add("@UserID", UserID);
            parameters.Add("@ShareUserId", ShareUserId);
            string defaultOrderBy = "ORDER BY p.attendid";

            if (UserID == Self)
            {
                sql = $@"
                SELECT
                    p.attendid as AttendId,
                    p.UserEmpName as CreatedBy, 
                    p.templateCode,
                    p.Description as description,
                    p.CreatedBy as CreatorUsername, 
                    dbo.fn_GetDateFormat_DDMMMYYYY(p.DateCreated) as DateCreated
                FROM tblAttnSheetParameter p
                WHERE p.UserEmpName = @UserID
                {defaultOrderBy};";
            }
            else if (UserID == "All")
            {
                sql = $@"
                SELECT DISTINCT
                    p.attendid as AttendId,
                    p.UserEmpName as CreatedBy,
                    p.templateCode,
                    p.Description as description,
                    p.CreatedBy as CreatorUsername,
                    dbo.fn_GetDateFormat_DDMMMYYYY(p.DateCreated) as DateCreated
                FROM tblAttnSheetParameter p
                WHERE
                    p.Createdby = @ShareUserId
                    OR p.attendid IN (
                        SELECT s.attendid
                        FROM tblAttnParamSharedUser s
                        WHERE s.UserId = @ShareUserId
                    )
                {defaultOrderBy};";
            }
            else
            {
                sql = $@"
                SELECT
                    p.attendid as AttendId,
                    p.UserEmpName as CreatedBy,
                    p.templateCode,
                    p.Description as description,
                    p.CreatedBy as CreatorUsername,
                    dbo.fn_GetDateFormat_DDMMMYYYY(p.DateCreated) as DateCreated
                FROM tblAttnSheetParameter p
                INNER JOIN tblAttnParamSharedUser s ON p.attendid = s.attendid
                WHERE
                    p.Createdby = @UserID
                    AND s.UserId = @ShareUserId
                {defaultOrderBy};";
            }

            var data = await _dapperService.QueryAsync<AttnSheetParameter>(sql, parameters);
            var listData = data.ToList();

            return new PagedResult<AttnSheetParameter>
            {
                Data = listData,
                TotalCount = listData.Count 
            };
        }
        public async Task<string> DeletetblAttnSheetParameterAsync(int original_Attendid)
        {
            string deleteQuery = @"
            DELETE FROM tblAttnParamSharedUser WHERE attendid = @AttnId;
            DELETE FROM AttendanceFiltersAdvance WHERE attendid = @AttnId;
            DELETE FROM tblattnmultiselparameter WHERE attendid = @AttnId;
            DELETE FROM tblAttendanceStatusHighlighter WHERE attendid = @AttnId;
            DELETE FROM tblActualTimeInOutHighlighter WHERE attendid = @AttnId;
            DELETE FROM tblWorkedHoursHighlighter WHERE attendid = @AttnId;
            DELETE FROM tblAttnSheetParameter WHERE attendid = @AttnId;";


            try
            {

                _dapperService.BeginTransaction();
                await _dapperService.ExecuteAsync(
                    deleteQuery,
                    new { AttnId = original_Attendid }
                );
                _dapperService.Commit();

                return "Record Deleted successfully";
            }
            catch (Exception ex)
            {
                _dapperService.Rollback();

                _logger.LogError(ex, "Error deleting AttnSheetParameter with ID {AttnId}", original_Attendid);
                return ex.Message;
            }

        }
        public async Task<PayrollDatesDto> GetPayrollStartEndDatesAsync()
        {
            var CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            string sql = @"
        SELECT TOP 1 dtfr AS StartDate, dtto AS EndDate
        FROM (
            -- Query 1: Open payroll month (if exists)
            SELECT 
                1 AS SNo,
                CONVERT(char, datefrom, 103) AS dtfr,
                CONVERT(char, DateTo, 103) AS dtto
            FROM tblPayrollMonth
            WHERE CompanyId = @CompanyId AND Closed = 0

            UNION

            -- Query 2: Fallback to current month (aap ki original logic)
            SELECT 
                2 AS SNo, 
                CONVERT(char, cast(cast(year(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))as varchar)+'/'+ REPLICATE('0', 2-LEN(cast(month(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))as varchar))) + cast(month(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))as varchar) +'/01'as datetime),103) AS dtfr,
                CONVERT(char, DATEadd(day,-1, DATEADD(month,1, cast(cast(year(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))as varchar)+'/'+ REPLICATE('0', 2-LEN(cast(month(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))as varchar))) + cast(month(dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId))as varchar) +'/01'as datetime))),103) AS dtto
        ) AS PayrollDates
        ORDER BY SNo;
        ";

            var resultsList = await _dapperService.QueryAsync<PayrollDatesDto>(
         sql,
         new { companyId = CompanyId }
     );
            var dates = resultsList?.FirstOrDefault();

            if (dates == null)
            {
                return new PayrollDatesDto { StartDate = "", EndDate = "" };
            }

            return dates;
        }
        public async Task<List<AttendanceStatus>> GetAttStatusAsync(string companyId)
        {
            var statusList = new List<AttendanceStatus>
        {
            new AttendanceStatus { Value = "1", Text = "Absent Day (All)" },
            new AttendanceStatus { Value = "2", Text = "Absent Day (Full Day)" },
            new AttendanceStatus { Value = "3", Text = "Absent Day (Half Day)" },
            new AttendanceStatus { Value = "4", Text = "Present (All)" },
            new AttendanceStatus { Value = "5", Text = "Present (Half Day)" },
            new AttendanceStatus { Value = "6", Text = "Present (Holiday)" },
            new AttendanceStatus { Value = "7", Text = "Present (Off Day)" },
            new AttendanceStatus { Value = "8", Text = "Half Day (All)" },
            new AttendanceStatus { Value = "9", Text = "Off Day (All)" },
            new AttendanceStatus { Value = "10", Text = "Off Day (Unattended)" },
            new AttendanceStatus { Value = "11", Text = "Holiday (All)" },
            new AttendanceStatus { Value = "12", Text = "Holiday (Unattended)" },
            new AttendanceStatus { Value = "13", Text = "Present (Late-In)" },
            new AttendanceStatus { Value = "14", Text = "Present (Early-Out)" },
            new AttendanceStatus { Value = "15", Text = "Present (On-Time)" },
            new AttendanceStatus { Value = "16", Text = "Leave Day (All)" },
            new AttendanceStatus { Value = "17", Text = "Leave Day (Full Day)" },
            new AttendanceStatus { Value = "18", Text = "Leave Day (Half Day)" }
        };

            string sqlLayoff = "SELECT ISNULL(LayOffEnt, 0) FROM tblLeavePolicy WHERE CompanyId = @CompanyId";
            var layoffEnt = await _dapperService.ExecuteScalarAsync<bool>(
                sqlLayoff,
                new { CompanyId = companyId }
            );

            if (layoffEnt == true)
            {
                statusList.Add(new AttendanceStatus { Value = "19", Text = "Layoff" });
            }
            var sortedList = statusList.OrderBy(s => s.Text).ToList();
            sortedList.Insert(0, new AttendanceStatus { Value = "0@", Text = "All" });

            return sortedList;
        }
        public async Task<List<RosterShift>> GetRosterShiftForAttSheetAgaintsShiftTypeAsync(string companyId, string isShiftDay)
        {
            string nightFilterClause = string.Empty;
            if (isShiftDay != "-1")
            {
                nightFilterClause = " AND Night = @IsShiftDay ";
            }
            else
            {
                nightFilterClause = " AND Night IN (0, 1) ";
            }
            string companyFilterClause;
            string finalCompanyId;

            if (companyId == "0")
            {
                finalCompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
                companyFilterClause = " CompanyId = @FinalCompanyId ";
            }
            else
            {
                finalCompanyId = companyId;
                companyFilterClause = " CompanyId = @FinalCompanyId ";
            }
            string sqlQuery = $@"
            SELECT 
                CAST(RTRIM(LTRIM(Code)) AS NVARCHAR(50)) AS RstId, 
                Code + ' (' + CASE WHEN ISNULL(Night, 0) = 1 THEN 'night' ELSE 'day' END + ') (' + TimeIn + ' - ' + TimeOut + ')' AS Code,
                ISNULL(Description,'') as Description
            FROM tblRosterShift
            WHERE {companyFilterClause} {nightFilterClause}
            ORDER BY LEN(RTRIM(LTRIM(Code))), Code";

            var parameters = new DynamicParameters();
            parameters.Add("@FinalCompanyId", finalCompanyId);

            if (isShiftDay != "-1")
            {
                parameters.Add("@IsShiftDay", int.Parse(isShiftDay));
            }

            var resultList = (await _dapperService.QueryAsync<RosterShift>(sqlQuery, parameters)).ToList();

            resultList.Insert(0, new RosterShift
            {
                RstId = "0",
                Code = "All",
                Description = "All Roster Shifts"
            });

            return resultList;
        }
        public Task<List<AttendanceStatus>> NewGetDaysStatusAsync()
        {
            var statusList = new List<AttendanceStatus>
        {
            new AttendanceStatus { Value = "1", Text = "Sunday" },
            new AttendanceStatus { Value = "2", Text = "Monday" },
            new AttendanceStatus { Value = "3", Text = "Tuesday" },
            new AttendanceStatus { Value = "4", Text = "Wednesday" },
            new AttendanceStatus { Value = "5", Text = "Thursday" },
            new AttendanceStatus { Value = "6", Text = "Friday" },
            new AttendanceStatus { Value = "7", Text = "Saturday" }
        };

            statusList.Insert(0, new AttendanceStatus { Value = "0@", Text = "All" });
            return Task.FromResult(statusList);
        }
        public Task<List<AttendanceStatus>> GetAttendanceModeAsync()
        {
            var statusList = new List<AttendanceStatus>
    {
        new AttendanceStatus {
            Value = " Access=''M'' AND [TI-Edit-AR] = 1 and Id not in (Select AttId from tblTimeAdjustment WHERE AttId = a.Id)@",
            Text = "Changed via Attendance Register (Time In)"
        },
        new AttendanceStatus {
            Value = " Access=''M'' AND [TO-Edit-AR] = 1 and Id not in (Select AttId from tblTimeAdjustment WHERE AttId = a.Id)@",
            Text = "Changed via Attendance Register (Time Out)"
        },
        new AttendanceStatus {
            Value = " Access=''M'' AND Id in (Select AttId from tblTimeAdjustment WHERE AttId = a.Id )@",
            Text = "Changed via Attendance Adjustment Workflow (All)"
        },
        new AttendanceStatus {
            Value = " Access=''M'' AND [TI-Edit-TA] = 1 AND Id in (Select AttId from tblTimeAdjustment WHERE AttId = a.Id )@",
            Text = "Changed via Attendance Adjustment Workflow (Time In)"
        },
        new AttendanceStatus {
            Value = " Access=''M'' AND [TO-Edit-TA] = 1 AND Id in (Select AttId from tblTimeAdjustment WHERE AttId = a.Id )@",
            Text = "Changed via Attendance Adjustment Workflow (Time Out)"
        },
        new AttendanceStatus {
            Value = " Access=''A''@",
            Text = "Machine Fetched Records"
        }
    };

            statusList.Insert(0, new AttendanceStatus { Value = "0@", Text = "All" });
            return Task.FromResult(statusList);
        }
        public DataTable ConvertListToDataTable(List<FilterObject> list)
        {
            DataTable dt = new DataTable();

            foreach (var obj in list)
            {
                DataRow dr = dt.NewRow();
                dr["id"] = obj.id;
                dr["color"] = obj.color;
                dt.Rows.Add(dr);
            }
            return dt;
        }
        public string GetAttendanceStatusHighlightersSql(List<FilterObject> filters)
        {
            if (filters == null || filters.Count == 0)
                return string.Empty;

            string rtnAttendanceStatus = " CASE ";
            foreach (var filterObj in filters)
            {
                string filter = filterObj.id!.ToString();
                string color = filterObj.color!;

                if (filter == "-100") { continue; }
                if (filter == "1")
                {
                    rtnAttendanceStatus += " WHEN ((ALCode=''A'' and isnull(IsPartialLeave, 0)=0)  OR ((AlCode=''A'' and isnull(IsPartialLeave, 0) = 1) OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0))) THEN ''" + color + "''";
                }
                else if (filter == "2")
                {
                    rtnAttendanceStatus += " WHEN (AlCode=''A'' and isnull(IsPartialLeave, 0) = 0) THEN ''" + color + "''";
                }
                else if (filter == "3")
                {
                    rtnAttendanceStatus = " WHEN ((AlCode=''A'' and isnull(IsPartialLeave, 0) = 1)  OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0)) THEN ''" + color + "''";
                }
                else if (filter == "4")
                {
                    rtnAttendanceStatus = " WHEN (((AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 1)  OR (AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 1 ) OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=1 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0) OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0)) OR (AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 0) OR (AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 0) OR (AlCode=''P'' and isnull(IsHoliday, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHalfDay, 0)=0 AND ISNULL(isLateArrival, 0) = 1) OR (AlCode=''P'' and isnull(IsHoliday, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHalfDay, 0)=0 AND ISNULL(isEarlyDeparture, 0) = 1) OR (AlCode=''P'' and isnull(IsHoliday, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHalfDay, 0)=0 AND ISNULL(isLateArrival, 0) = 0))  THEN ''" + color + "''";
                }
                else if (filter == "5")
                {
                    rtnAttendanceStatus += " WHEN ((AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 1) OR (AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 1 )  OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=1 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0) OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0))  THEN ''" + color + "''";
                }
                else if (filter == "6")
                {
                    rtnAttendanceStatus += " WHEN (AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 0)  THEN ''" + color + "''";
                }
                else if (filter == "7")
                {
                    rtnAttendanceStatus += " WHEN (AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 0 )  THEN ''" + color + "''";
                }
                else if (filter == "8")
                {
                    rtnAttendanceStatus += " WHEN (((AlCode=''A'' and isnull(IsPartialLeave, 0) = 1) OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0)) OR ((AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 1) OR (AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 1 )OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=1 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0) OR (AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0)) OR ((AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=1 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0) OR (AlCode=''A'' and isnull(IsPartialLeave, 0) = 1)) OR ((AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 1 ) OR (AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 1)))  THEN ''" + color + "''";
                }
                else if (filter == "9")
                {
                    rtnAttendanceStatus += " WHEN ((isnull(IsShiftOff, 0)=1 AND (LEN(LTRIM(RTRIM(ISNULL(TimeIN, '''')))) = 0 OR LEN(LTRIM(RTRIM(ISNULL(TimeOut, '''')))) = 0)) OR ((AlCode=''P'' and isnull(IsShiftOff, 0)=1 and isnull(isHalfDay, 0) = 0 )))  THEN ''" + color + "''";
                }
                else if (filter == "10")
                {
                    rtnAttendanceStatus += " WHEN (isnull(IsShiftOff, 0)=1 AND (LEN(LTRIM(RTRIM(ISNULL(TimeIN, '''')))) = 0 OR LEN(LTRIM(RTRIM(ISNULL(TimeOut, '''')))) = 0))  THEN ''" + color + "''";
                }
                else if (filter == "11")
                {
                    rtnAttendanceStatus += " WHEN ((AlCode=''P'' and isnull(IsHoliday, 0)=1 and isnull(isHalfDay, 0) = 0) OR (AlCode=''H'' and isnull(IsHoliday, 0)=1 AND (LEN(LTRIM(RTRIM(ISNULL(TimeIN, '''')))) = 0 OR LEN(LTRIM(RTRIM(ISNULL(TimeOut, '''')))) = 0)))  THEN ''" + color + "''";
                }
                else if (filter == "12")
                {
                    rtnAttendanceStatus += " WHEN (isnull(IsHoliday, 0)=1 AND (LEN(LTRIM(RTRIM(ISNULL(TimeIN, '''')))) = 0 OR LEN(LTRIM(RTRIM(ISNULL(TimeOut, '''')))) = 0))  THEN ''" + color + "''";
                }
                else if (filter == "13")
                {
                    rtnAttendanceStatus += " WHEN (AlCode=''P'' and isnull(IsHoliday, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHalfDay, 0)=0 AND ISNULL(isLateArrival, 0) = 1)  THEN ''" + color + "''";
                }
                else if (filter == "14")
                {
                    rtnAttendanceStatus += " WHEN (AlCode=''P'' and isnull(IsHoliday, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHalfDay, 0)=0 AND ISNULL(isEarlyDeparture, 0) = 1)  THEN ''" + color + "''";
                }
                else if (filter == "15")
                {
                    rtnAttendanceStatus += " WHEN (AlCode=''P'' and isnull(IsHoliday, 0)=0 and isnull(IsShiftOff, 0)=0 and isnull(isHalfDay, 0)=0 AND ISNULL(isLateArrival, 0) = 0)  THEN ''" + color + "''";
                }
                else if (filter == "16")
                {
                    rtnAttendanceStatus += " WHEN (((AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=1 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0) OR (AlCode=''A'' and isnull(IsPartialLeave, 0) = 1)) OR (isnull(isOnLeave, 0)=1 ))  THEN ''" + color + "''";
                }
                else if (filter == "17")
                {
                    rtnAttendanceStatus += " WHEN (isnull(isOnLeave, 0)=1)  THEN ''" + color + "''";
                }
                else if (filter == "18")
                {
                    rtnAttendanceStatus += " WHEN ((AlCode=''P'' and isnull(isHalfDay, 0)=1 and isnull(IsPartialLeave, 0)=1 and isnull(IsShiftOff, 0)=0 and isnull(isHoliday, 0)=0) OR (AlCode=''A'' and isnull(IsPartialLeave, 0) = 1))  THEN ''" + color + "''";
                }
                else if (filter == "19")
                {
                    rtnAttendanceStatus += " WHEN (AlCode = ''$'')  THEN ''" + color + "''";
                }
            }

            rtnAttendanceStatus += " END AttendanceStatusHighlighter,";

            return rtnAttendanceStatus;
        }
        public StringBuilder GetWorkedHoursHighlightersSql(List<FilterObject> filters)
        {
            StringBuilder strWorkedHoursHighlighter = new StringBuilder();

            if (filters == null || filters.Count == 0)
                return strWorkedHoursHighlighter;

            foreach (var filterObj in filters)
            {

                string FieldID = filterObj.id!.ToString();
                string Oprator = filterObj.operatorid!.ToString();
                string firstValue = filterObj.firstValue1!;
                string SecondValue = filterObj.secondValue1!;
                string ColorId = filterObj.color!;

                string FieldName = string.Empty;
                switch (Oprator)
                {
                    case "0": FieldName = " < "; break;
                    case "1": FieldName = " > "; break;
                    case "2": FieldName = " <= "; break;
                    case "3": FieldName = " >= "; break;
                    case "4": FieldName = " = "; break;
                    case "5": FieldName = " Between "; break;
                    default: continue;
                }

                string colorHex = ColorId;
                if (Oprator == "5")
                {
                    strWorkedHoursHighlighter.Append($" CASE when WorkedHours BETWEEN ''{firstValue}'' AND ''{SecondValue}'' THEN ''{colorHex}'' END AS WorkedHoursHighlighters,");
                }
                else
                {
                    strWorkedHoursHighlighter.Append($" CASE when WorkedHours {FieldName} ''{firstValue}'' THEN ''{colorHex}'' END AS WorkedHoursHighlighters,");
                }
            }

            return strWorkedHoursHighlighter;
        }
        public StringBuilder GetActualTimeInOutHighlightersSql(List<FilterObject> filters)
        {
           
            StringBuilder strActualTimeInOutHighlighter = new StringBuilder();

            if (filters == null || filters.Count == 0)
                return strActualTimeInOutHighlighter;

            foreach (var filterObj in filters)
            {
                string FieldID = filterObj.id!.ToString();

                if (FieldID == "-100") { continue; } 

                string Oprator = filterObj.operatorid!.ToString();
                string actualTimeField = (filterObj.name == "Actual Time-In") ? "AttDateIn" : "AttDateOut";
                string hourValuePadded = filterObj.firstValue1!; 
                string shiftTimeField = (filterObj.secondValue1!.ToString() == "1") ? "ShiftDateTimeIn" : "ShiftDateTimeOut";
                string color = filterObj.color!;

               
                strActualTimeInOutHighlighter.Append(" CASE "); 
                if (Oprator == "0")
                {
                    strActualTimeInOutHighlighter.Append($" WHEN {actualTimeField} < DATEADD(MINUTE, -1 *(dbo.FN_HHMM_To_MM(''{hourValuePadded}'')), {shiftTimeField} ) THen ''{color}'' ");
                }

                else if (Oprator == "1")
                {
                    strActualTimeInOutHighlighter.Append($" WHEN {actualTimeField} > DATEADD(MINUTE, (dbo.FN_HHMM_To_MM(''{hourValuePadded}'')), {shiftTimeField} ) THen ''{color}'' ");
                }

                strActualTimeInOutHighlighter.Append($" END [{filterObj.name!.Replace(" ", "").Replace("-", "")}Highlighter],"); // Column name creation
            }
            return strActualTimeInOutHighlighter;
        }
    }
    public class AttnSheetParameter
    {
        public int? AttendId { get; set; }
        public string? templateCode { get; set; }
        public string? description { get; set; }
        public string? CreatedBy { get; set; } 
        public string? DateCreated { get; set; }
        public string? CreatorUsername { get; set; }
    }
    public class PagedResult<T>
    {
        public List<T>? Data { get; set; }
        public int? TotalCount { get; set; }
    }
    public class PayrollDatesDto
    {
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
    }
    public class AttendanceStatus
    {
        public string? Value { get; set; }
        public string? Text { get; set; }
    }
    public class RosterShift
    {
        public string? RstId { get; set; }
        public string? Code { get; set; }
        public string? Description { get; set; }
    }
    public class ReportRequestDto
    {
        public string? DateFrom { get; set; }
        public string? DateTo { get; set; }
        public string? Group1Value { get; set; } 
        public string? Group2Value { get; set; } 
        public string? SubOrdinatesFilter { get; set; }
        public string? WholeSearchClause { get; set; }
        public List<FilterObject>? AttendanceFilters { get; set; }
        public List<FilterObject>? WorkedHoursFilters { get; set; }
        public List<FilterObject>? ActualTimeInOutFilters { get; set; }
    }
    public class FilterObject
    {
        public string? id { get; set; }
        public string? color { get; set; }
        public string? operatorid { get; set; } 
        public string? firstValue { get; set; }
        public string? SecondValue { get; set; }
        public string? name { get; set; }
        public string? operationName { get; set; }
        public string? diffchecked { get; set; }
        public string? secondtext { get; set; }
        public string? firstValue1 { get; set; }
        public string? secondValue1 { get; set; }
    }
    public class AttendanceReportRequest
    {
        public List<FilterObject>? Filters { get; set; }
        public List<FilterObject>? WorkedHoursFilters { get; set; } 
    }



}
