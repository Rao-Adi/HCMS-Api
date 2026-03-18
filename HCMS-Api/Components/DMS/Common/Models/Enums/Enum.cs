namespace HCMS_Api.Components.DMS.Common.Models.Enums;

public class Enum
{
}

#region Document & Request Enums
public enum DocumentStatus
{
    Draft = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Obsolete = 5
}

public enum DocumentRequestType
{
    Creation = 1,
    Revision = 2,
    Obsoletion = 3
}

public enum RequestStatusEnum
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Reverted = 4
}
#endregion Document & Request Enums


#region Workflow Enums

public enum WorkflowPolicyType
{
    RequestCreation = 1,
    DocumentCreation = 2,
    RevisionObsoletion = 3,
    ExternalSharing = 4
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Reverted = 4
}


#endregion Workflow Enums

#region Training Enums
public enum TrainingMode
{
    Classroom = 1,
    Online = 2
}

public enum TrainingStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4
}

public enum TrainingValidationStatus
{
    Pending = 1,
    Valid = 2,
    Invalid = 3
}

#endregion Training Enums

#region ESignature
public enum SignatureType
{
    Drawn = 1,
    Uploaded = 2
}

#endregion ESignature

#region Template Enums
public enum TemplateType
{
    Word = 1,
    Pdf = 2,
    Html = 3
}

#endregion Template Enums

#region Responsibility Transfer Enums
public enum ResponsibilityTransferStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum ResponsibilityTransferReason
{
    Leave = 1,
    Resignation = 2,
    TemporaryAssignment = 3,
    PermanentChange = 4
}

#endregion Responsibility Transfer Enums

#region Distribution / Sharing
public enum DistributionType
{
    Digital = 1,
    Physical = 2
}

#endregion Distribution / Sharing

public enum DocumentRequestStatus
{
    Draft = 0,
    Submitted = 1,
    InApproval = 2,
    Approved = 3,
    Rejected = 4
}