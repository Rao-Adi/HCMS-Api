using System;
using System.Collections.Generic;

namespace HCMS_Api.Components.DMS.Common.Models;

public class DashboardSummaryDto
{
    public int MyTotalDocuments { get; set; }
    public int MyTotalRequests { get; set; }
    public int MyPendingRequests { get; set; }
    public int MyApprovedRequests { get; set; }
    public int MyRejectedRequests { get; set; }
    public int PendingApprovals { get; set; }
    public int ApprovedByMe { get; set; }
    public int RejectedByMe { get; set; }
    public int PendingTrainings { get; set; }
    public int PendingAuthorizations { get; set; }
}

public class DocumentTypeDistributionDto
{
    public string DocumentTypeCode { get; set; }
    public string DocumentTypeName { get; set; }
    public int Count { get; set; }
}

public class DashboardPendingTaskDto
{
    public string TaskType { get; set; } // "Approval", "Training", "Authorization"
    public int EntityId { get; set; }
    public string EntityType { get; set; }
    public string ReferenceNumber { get; set; } // DocumentNumber or RequestNumber
    public string Title { get; set; }
    public string AssignedDate { get; set; }
}

public class RecentActivityDto
{
    public int Id { get; set; }
    public string Action { get; set; }
    public string EntityType { get; set; }
    public string Timestamp { get; set; }
    public string User { get; set; }
}

public class DashboardDataDto
{
    public DashboardSummaryDto Summary { get; set; }
    public List<DocumentTypeDistributionDto> DocumentTypeDistribution { get; set; }
    public List<DashboardPendingTaskDto> ImmediatePendingTasks { get; set; }
    public List<RecentActivityDto> RecentActivities { get; set; }
    public List<DashboardDocumentReviewDto> DocumentsApproachingReview { get; set; } 
    public DashboardDataDto()
    {
        Summary = new DashboardSummaryDto();
        DocumentTypeDistribution = new List<DocumentTypeDistributionDto>();
        ImmediatePendingTasks = new List<DashboardPendingTaskDto>();
        RecentActivities = new List<RecentActivityDto>();
    }
}

public class DashboardDocumentReviewDto
{
    public int DocumentId { get; set; }
    public string DocumentNumber { get; set; }
    public string Title { get; set; }
    public DateTime NextReviewDate { get; set; }
    public int DaysUntilReview { get; set; }
}