namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class clsLeaveLookUp
    {

        public int EmpId { get; set; }
        public string Level { get; set; }
        public string search { get; set; }
        public string fieldName { get; set; }
        public string CompanyId { get; set; }
        public string hideColumn { get; set; }
        public ObjWhrClause whereClauseSearchBar { get; set; }

    }

    public class clsLeaveCountParam
    {
        public string leavetype { get; set; }
        public string datefrom { get; set; }
        public string dateto { get; set; }
        public string empid { get; set; }
        public bool isLFAEntitled { get; set; }
        public string days { get; set; }
        public string description { get; set; }
    }

    public class LeaveRequest
    {
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public string EmpId { get; set; }
        public string leavedays_FDHD { get; set; }
        public string leavedays_DHM { get; set; }
        public string? leaveAvailType { get; set; }
        public string Remarks { get; set; }
        public bool DeductPrev { get; set; }
        public string attachedFileName { get; set; }
        public string attachedFileExtension { get; set; }
        public string ContentType { get; set; }
        public string LCode { get; set; }
        public byte[]? Attachment { get; set; }
        public bool is_LFA_Entry { get; set; }
        public bool is_Document_Attached { get; set; }
        public bool is_leaveadj_confirmed { get; set; }
        public string Description { get; set; }
    }

    public class clsLeaveBalancePolicy
    {
        public bool IsMaintainPrevYrsLvHistory { get; set; }
        public bool IsAvailLeavesFromNextLeaveYearAllowed { get; set; }
        public bool IsAnyLeaveTypeCarryForward { get; set; }
        public bool IsPenaltyApplicable { get; set; }
    }
}
