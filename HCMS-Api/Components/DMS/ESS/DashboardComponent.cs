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

    public async Task<DashboardDataDto> GetDashboardDataAsync()
    {
        var dashboardData = new DashboardDataDto();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());
             

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
                        WHERE we.CompanyId = @CompanyId AND we.EntityType = 'Document' AND wes.AssignedUserId = @EmployeeCode AND wes.IsActive = TRUE AND wes.Decision IS NULL) AS PendingDocumentApprovals,
                    (SELECT COUNT(1) FROM WorkflowExecutionSteps wes JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                        WHERE we.CompanyId = @CompanyId AND we.EntityType = 'Request' AND wes.AssignedUserId = @EmployeeCode AND wes.IsActive = TRUE AND wes.Decision IS NULL) AS PendingRequestApprovals,
                    (SELECT COUNT(1) FROM WorkflowExecutionSteps wes
                        JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                        WHERE we.CompanyId = @CompanyId AND we.EntityType = 'Request' AND wes.AssignedUserId = @EmployeeCode 
                        AND we.Status = 'Completed' AND wes.Decision = 'Approved') AS ApprovedByMe,
                    (SELECT COUNT(1) FROM WorkflowExecutionSteps wes
                        JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                        WHERE we.CompanyId = @CompanyId AND we.EntityType = 'Request' AND wes.AssignedUserId = @EmployeeCode 
                        AND we.Status IN ('Rejected', 'Reworked') AND wes.Decision IN ('Rejected', 'Reworked')) AS RejectedByMe,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.CompanyId = @CompanyId AND dut.EmployeeCode = @EmployeeCode AND dut.TrainingStatus = 0 AND dut.IsDeleted = FALSE) AS PendingTrainings,
                    (SELECT COUNT(1) FROM DocumentTraining tr JOIN Documents doc ON tr.DocumentId = doc.Id 
                        WHERE doc.CompanyId = @CompanyId AND tr.ReadyForAuthorization = TRUE AND tr.IsActive = TRUE) AS PendingAuthorizations;";
            
            var summary = await _common.QueryFirstOrDefaultAsync<DashboardSummaryDto>(summaryQuery, new { CompanyId = CompanyId, UserId = empCode, EmployeeCode = empCode });
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

            var distribution = await _common.QueryAsync<DocumentTypeDistributionDto>(distributionQuery, new { CompanyId = CompanyId, UserId = empCode });
            dashboardData.DocumentTypeDistribution = distribution.ToList();

            // 3. Get Immediate Pending Tasks (Combine Approvals & Trainings)
            string tasksQuery = @"
                -- Pending Approvals
                SELECT 
                    'Approval' AS TaskType,
                    we.EntityId AS EntityId,
                    COALESCE(dr.RequestNumber, doc.DocumentNumber) AS ReferenceNumber,
                    COALESCE(dr.DocumentName, doc.Title) AS Title,
                    we.StartedAt::text AS AssignedDate,
                    we.EntityType
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
                    dut.CreatedAt::text AS AssignedDate,
                    'EntityType' AS EntityType
                FROM DocumentUserTraining dut
                JOIN Documents doc ON doc.Id = dut.DocumentId
                WHERE dut.CompanyId = @CompanyId 
                  AND dut.EmployeeCode = @EmployeeCode 
                  AND dut.TrainingStatus = 0 
                  AND dut.IsDeleted = FALSE
                
                ORDER BY AssignedDate DESC
                LIMIT 10;";

            var tasks = await _common.QueryAsync<DashboardPendingTaskDto>(tasksQuery, new { CompanyId = CompanyId, EmployeeCode = empCode });
            dashboardData.ImmediatePendingTasks = tasks.ToList();

            // 4. Get Recent Activities (Audit Logs)
            string auditQuery = @"
                SELECT 
                    Id, 
                    Action, 
                    EntityType, 
                    Timestamp::text AS Timestamp, 
                    EmployeeCode
                FROM AuditLogs
                WHERE CompanyId = @CompanyId AND EmployeeCode = @EmpId
                ORDER BY Timestamp DESC
                LIMIT 10;";
            
            var activities = await _common.QueryAsync<RecentActivityDto>(auditQuery, new { CompanyId = CompanyId, EmpId = empCode });
            dashboardData.RecentActivities = activities.ToList();

            // 5. Get Documents Approaching Review Date (Next 60 days)
            string reviewQuery = @"
                SELECT 
                    d.Id AS DocumentId,
                    d.DocumentNumber,
                    d.Title,
                    d.NextReviewDate ::text,
                    EXTRACT(DAY FROM d.NextReviewDate - NOW()) AS DaysUntilReview
                FROM Documents d
                WHERE d.CompanyId = @CompanyId
                  AND d.IsDeleted = FALSE
                  AND d.IsActive = TRUE
                  AND d.NextReviewDate IS NOT NULL
                  AND d.NextReviewDate BETWEEN NOW() AND NOW() + INTERVAL '60 days'
                ORDER BY d.NextReviewDate ASC
                LIMIT 10;";

            var approachingReview = await _common.QueryAsync<DashboardDocumentReviewDto>(reviewQuery, new { CompanyId = CompanyId });
            dashboardData.DocumentsApproachingReview = approachingReview.ToList();

            return dashboardData;
        }
        catch (Exception ex)
        {
            throw new CustomException($"Failed to retrieve dashboard data: {ex.Message}", 500);
        }
    }
}