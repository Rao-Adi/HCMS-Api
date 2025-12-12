namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class clsLabelsFetching
    {
        public string? Code { get; set; }
        public string? Description { get; set; }
    }

    public class clsLabelsFetchingForFilter
    {
        public string? Code { get; set; }
        public string? Description { get; set; }
        //public string SmsId { get; set; }
        //public string Filter { get; set; }
        //public int IsGrouping { get; set; }
    }

    public class GridParamsExtended : GridParams
    {
        public string SearchParam
        {
            get { return this.SearchParam; }
        }
        public int PageSize
        {
            get { return Convert.ToInt32(this.pageSize); }
        }
        public int PageNumber
        {
            get { return Convert.ToInt32(this.pageNumber); }
        }
        public int CompanyId { get; set; }
        public string FilterId { get; set; }
        public string Stage { get; set; }
        public string Action { get; set; }
        public string Flag { get; set; }
        public string AppIds { get; set; }
        public int Type { get; set; }
        public int TaskType { get; set; }
        public int? TaskNature { get; set; }
        public int TaskSubmissionId { get; set; }
        public int? FormNameId { get; set; }
        public int ProductId { get; set; }
        public string? StatusFilter { get; set; }
        public string? MemberFilter { get; set; }
        public int? ModuleId { get; set; }
        public int? Id { get; set; }
        public int ParentId { get; set; }
        public int? AppId { get; set; }
        public int? FormTypeId { get; set; }
        public int? ResourceId { get; set; }
        public int? Status { get; set; }
        public int? Ownership { get; set; }
        public int? ControlId { get; set; }
        public int? PriorityId { get; set; }
        public int? MemberId { get; set; }
        public int? SubDocumentCategoryId { get; set; }
        public int? DocumentCategoryId { get; set; }
        public string? TaskStatus { get; set; }
        public int ReviewPolicy { get; set; }
        public int? RoleOrEmployee { get; set; }
        public string? QAMemberFilter { get; set; }
        public string? DevMemberFilter { get; set; }
        public string? CustomerFilter { get; set; }
    }

    public class clsValidationFetching
    {
        public string Code { get; set; }
        public string Description { get; set; }
    }
}
