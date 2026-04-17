using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DashboardComponent
{
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    private readonly DMSCommon _common;
    private readonly PeoplePartnersComponent _peoplePartnersComponent;

    public DashboardComponent(
        DMSUtilities utilities,
        ClientContextService clientContextService,
        IDMSDapperDataService dapper,
        DMSCommon common,
        PeoplePartnersComponent peoplePartnersComponent)
    {
        _utilities = utilities;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common;
        _peoplePartnersComponent = peoplePartnersComponent;
    }

    public async Task<DashboardDataDto> GetDashboardDataAsync(int empId)
    {
        var dashboardData = new DashboardDataDto();

        try
        {
            string companyIdStr = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int companyId = int.Parse(companyIdStr);
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            // Fetch actual Employee Details to get EmployeeCode
            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(empId);
            string employeeCode = empDetail != null ? Convert.ToString(empDetail.empcode) : "";

            // 1. Get Dashboard Summary (High-Level Metrics)
            string summaryQuery = @"
                SELECT 
                    (SELECT COUNT(1) FROM Documents WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND IsDeleted = FALSE) AS MyTotalDocuments,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND IsDeleted = FALSE) AS MyTotalRequests,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND Status IN (1, 2) AND IsDeleted = FALSE) AS MyPendingRequests,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND Status = 3 AND IsDeleted = FALSE) AS MyApprovedRequests,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND Status = 4 AND IsDeleted = FALSE) AS MyRejectedRequests,
                    (SELECT COUNT(1) FROM WorkflowExecutionSteps wes JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId 
                        WHERE we.CompanyId = @CompanyId AND wes.AssignedUserId = @EmployeeCode AND wes.IsActive = TRUE AND wes.Decision IS NULL) AS PendingApprovals,
                    (SELECT COUNT(1) FROM WorkflowExecutionSteps wes JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId 
                        WHERE we.CompanyId = @CompanyId AND wes.AssignedUserId = @EmployeeCode AND wes.Decision = 'Approved') AS ApprovedByMe,
                    (SELECT COUNT(1) FROM WorkflowExecutionSteps wes JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId 
                        WHERE we.CompanyId = @CompanyId AND wes.AssignedUserId = @EmployeeCode AND wes.Decision IN ('Rejected', 'Rework')) AS RejectedByMe,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.CompanyId = @CompanyId AND dut.EmployeeCode = @EmployeeCode AND dut.TrainingStatus = 0 AND dut.IsDeleted = FALSE) AS PendingTrainings,
                    (SELECT COUNT(1) FROM DocumentTraining tr JOIN Documents doc ON tr.DocumentId = doc.Id 
                        WHERE doc.CompanyId = @CompanyId AND tr.ReadyForAuthorization = TRUE AND tr.IsActive = TRUE) AS PendingAuthorizations;";
            
            var summary = await _common.QueryFirstOrDefaultAsync<DashboardSummaryDto>(summaryQuery, new { CompanyId = companyId, UserId = userId, EmployeeCode = employeeCode });
            if (summary != null)
            {
                dashboardData.Summary = summary;
            }

            // 2. Get Document Distribution by Type
            string distributionQuery = @"
                SELECT 
                    dt.Code AS DocumentTypeCode, 
                    dt.Name AS DocumentTypeName, 
                    COUNT(d.Id) AS Count
                FROM DocumentTypes dt
                LEFT JOIN Documents d ON d.DocumentTypeCode = dt.Code AND d.CompanyId = @CompanyId 
                --AND d.CreatedBy = @UserId AND d.IsDeleted = FALSE
                WHERE dt.IsActive = TRUE AND dt.IsDeleted = FALSE
                GROUP BY dt.Code, dt.Name
                ORDER BY Count DESC;";

            var distribution = await _common.QueryAsync<DocumentTypeDistributionDto>(distributionQuery, new { CompanyId = companyId, UserId = userId });
            dashboardData.DocumentTypeDistribution = distribution.ToList();

            // 3. Get Immediate Pending Tasks (Combine Approvals & Trainings)
            string tasksQuery = @"
                -- Pending Approvals
                SELECT 
                    'Approval' AS TaskType,
                    we.EntityId AS EntityId,
                    COALESCE(dr.RequestNumber, doc.DocumentNumber) AS ReferenceNumber,
                    COALESCE(dr.DocumentName, doc.Title) AS Title,
                    we.StartedAt::text AS AssignedDate
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                LEFT JOIN DocumentRequests dr ON dr.Id = we.EntityId AND we.EntityType = 'Request'
                LEFT JOIN Documents doc ON doc.Id = we.EntityId AND we.EntityType = 'Document'
                WHERE we.CompanyId = @CompanyId 
                  AND wes.AssignedUserId = @EmployeeCode 
                  AND wes.IsActive = TRUE 
                  AND wes.Decision IS NULL
                
                UNION ALL
                
                -- Pending Trainings
                SELECT 
                    'Training' AS TaskType,
                    dut.DocumentId AS EntityId,
                    doc.DocumentNumber AS ReferenceNumber,
                    doc.Title AS Title,
                    dut.CreatedAt::text AS AssignedDate
                FROM DocumentUserTraining dut
                JOIN Documents doc ON doc.Id = dut.DocumentId
                WHERE dut.CompanyId = @CompanyId 
                  AND dut.EmployeeCode = @EmployeeCode 
                  AND dut.TrainingStatus = 0 
                  AND dut.IsDeleted = FALSE
                
                ORDER BY AssignedDate DESC
                LIMIT 10;";

            var tasks = await _common.QueryAsync<DashboardPendingTaskDto>(tasksQuery, new { CompanyId = companyId, EmployeeCode = employeeCode });
            dashboardData.ImmediatePendingTasks = tasks.ToList();

            // 4. Get Recent Activities (Audit Logs)
            string auditQuery = @"
                SELECT 
                    Id, 
                    Action, 
                    EntityType, 
                    Timestamp::text AS Timestamp, 
                    UserId::text AS User
                FROM AuditLogs
                WHERE CompanyId = @CompanyId AND UserId = @EmpId
                ORDER BY Timestamp DESC
                LIMIT 10;";
            
            var activities = await _common.QueryAsync<RecentActivityDto>(auditQuery, new { CompanyId = companyId, EmpId = empId });
            dashboardData.RecentActivities = activities.ToList();

            return dashboardData;
        }
        catch (Exception ex)
        {
            throw new CustomException($"Failed to retrieve dashboard data: {ex.Message}", 500);
        }
    }
}