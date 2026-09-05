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
    private readonly DocumentRequestComponent _documentRequestComponent;
    private readonly DocumentComponent _documentComponent;

    public DashboardComponent(
        DMSUtilities utilities,
        ClientContextService clientContextService,
        IDMSDapperDataService dapper,
        DMSCommon common,
        PeoplePartnersComponent peoplePartnersComponent,
        DocumentRequestComponent documentRequestComponent,
        DocumentComponent documentComponent)
    {
        _utilities = utilities;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common;
        _peoplePartnersComponent = peoplePartnersComponent;
        _documentRequestComponent = documentRequestComponent;
        _documentComponent = documentComponent;
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
                    -- ""Total Approved Documents"" previously bound to MyTotalDocuments above, which
                    -- has no status filter at all -- it counted every document you ever created
                    -- (Draft/InReview/Rejected included), not just approved ones, so it disagreed
                    -- with the actual approved-documents report (GetApprovedEffectiveDocumentsAsync),
                    -- which correctly filters to latest state = EFFECTIVE. This mirrors that same
                    -- state check, scoped to documents you created.
                    (SELECT COUNT(1) FROM Documents doc WHERE doc.CompanyId = @CompanyId AND doc.CreatedBy = @UserId AND doc.IsDeleted = FALSE
                        AND (
                            SELECT ds.Code FROM DocumentStateHistory dsh
                            JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                            WHERE dsh.DocumentId = doc.Id
                            ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                        ) = 'EFFECTIVE') AS MyApprovedDocuments,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND IsDeleted = FALSE) AS MyTotalRequests,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND Status IN (1, 2) AND IsDeleted = FALSE) AS MyPendingRequests,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND Status = 3 AND IsDeleted = FALSE) AS MyApprovedRequests,
                    (SELECT COUNT(1) FROM DocumentRequests WHERE CompanyId = @CompanyId AND CreatedBy = @UserId AND Status = 4 AND IsDeleted = FALSE) AS MyRejectedRequests,
                    -- Draft (Status = 0) requests fall outside the buckets above, which cover only
                    -- Submitted/InApproval/Approved/Rejected (1-4). A request sits at Draft both
                    -- when it's brand new and never submitted, and when an approver reverted it for
                    -- rework (see DocumentRequestComponent.GetDraftDocumentRequestAsync, which tells
                    -- the two apart the same way: a prior WorkflowExecutions row means it was
                    -- submitted before and got sent back, not that it's untouched).
                    (SELECT COUNT(1) FROM DocumentRequests dr WHERE dr.CompanyId = @CompanyId AND dr.CreatedBy = @UserId AND dr.Status = 0 AND dr.IsDeleted = FALSE
                        AND NOT EXISTS (SELECT 1 FROM WorkflowExecutions we WHERE we.EntityId = dr.Id AND we.EntityType = 'Request')
                        AND NOT EXISTS (SELECT 1 FROM DocumentRequests child WHERE child.ParentRequestId = dr.Id AND child.CompanyId = dr.CompanyId)) AS MyDraftRequests,
                    (SELECT COUNT(1) FROM DocumentRequests dr WHERE dr.CompanyId = @CompanyId AND dr.CreatedBy = @UserId AND dr.Status = 0 AND dr.IsDeleted = FALSE
                        AND EXISTS (SELECT 1 FROM WorkflowExecutions we WHERE we.EntityId = dr.Id AND we.EntityType = 'Request')
                        AND NOT EXISTS (SELECT 1 FROM DocumentRequests child WHERE child.ParentRequestId = dr.Id AND child.CompanyId = dr.CompanyId)) AS MyRevertedRequests,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.CompanyId = @CompanyId AND dut.EmployeeCode = @EmployeeCode AND dut.TrainingStatus = 0 AND dut.IsDeleted = FALSE) AS PendingTrainings,
                    -- Previously company-wide (no CreatedBy filter at all), so every user saw the
                    -- same shared number regardless of whether any of it was theirs. Scoped to
                    -- documents you created, matching MyApprovedDocuments above -- both are
                    -- creator stats, so the dashboard only shows them (see the *ngIf gates in
                    -- dashboard.html) to someone who has actually created something.
                    (SELECT COUNT(1) FROM DocumentTraining tr JOIN Documents doc ON tr.DocumentId = doc.Id
                        WHERE doc.CompanyId = @CompanyId AND doc.CreatedBy = @UserId AND tr.ReadyForAuthorization = TRUE AND tr.IsActive = TRUE) AS PendingAuthorizations;";

            var summary = await _common.QueryFirstOrDefaultAsync<DashboardSummaryDto>(summaryQuery, new { CompanyId = CompanyId, UserId = empCode, EmployeeCode = empCode })
                ?? new DashboardSummaryDto();

            // Approver-facing counts (PendingApprovals/PendingDocumentApprovals/PendingRequestApprovals/
            // ApprovedByMe/RejectedByMe) used to be a second, separately maintained copy of this same
            // logic. It had drifted: ApprovedByMe/RejectedByMe additionally required the *entire*
            // multi-step workflow to have reached we.Status = 'Completed'/'Rejected'/'Reworked', while
            // the actual "My Approvals" pages (via GetMyRequestCountsAsync/GetMyDocumentCountsAsync,
            // which also feed the sidebar badges) count the moment *this* step gets a Decision,
            // regardless of whether later steps are still running. That undercounted approvers on
            // multi-step workflows here relative to what they saw on the real page. Calling the same
            // methods the pages call guarantees the dashboard can't drift from them again.
            dynamic requestCounts = await _documentRequestComponent.GetMyRequestCountsAsync();
            dynamic documentCounts = await _documentComponent.GetMyDocumentCountsAsync();

            int pendingRequestApprovals = Convert.ToInt32(requestCounts.MyInbox.Pending);
            int pendingDocumentApprovals = Convert.ToInt32(documentCounts.MyInbox.pending);

            summary.PendingRequestApprovals = pendingRequestApprovals;
            summary.PendingDocumentApprovals = pendingDocumentApprovals;
            summary.PendingApprovals = pendingRequestApprovals + pendingDocumentApprovals;
            summary.ApprovedByMe = Convert.ToInt32(requestCounts.MyInbox.Approved);
            summary.RejectedByMe = Convert.ToInt32(requestCounts.MyInbox.RejectedOrReverted);

            dashboardData.Summary = summary;

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