namespace HCMS_Api.Components.HCMS.Common.Models
{
    /// <summary>
    /// This data model class will be used to store the validation message data from the database table [tblValidationMessages]
    /// </summary>
    public class ValMessage
    {
        public string? ValidationCode { get; set; }
        public string? ValidationKey { get; set; }
        public string? ValidationMessage { get; set; }
        public string? CrtlId { get; set; }
        public string? FieldName { get; set; }
    }

    public class clsShiftCode
    {
        public string? ShiftCode { get; set; }
    }

    /// <summary>
    /// Returns a response when an employee is selected in the lookup by typing empcode
    /// </summary>
    public class VMsgLookup
    {
        public string? Msg1 { get; set; }
        public string? Msg2 { get; set; }
        public string? EmpName { get; set; }
        public string? EmpId { get; set; }
    }

    /// <summary>
    /// Returns Lookup query and Where clause to load the lookup window 
    /// </summary>
    public class LookupQueryAndWhere
    {
        public string? LookupQuery { get; set; }
        public string? WhereClause { get; set; }
    }

    public class ValidationStatus
    {
        public string? ValidationCode { get; set; }
        public string? AdditionalMsg { get; set; }
        public bool ValidStatus { get; set; }
        public bool IsForControl { get; set; }
        public string? ControlId { get; set; }
        public bool IsForPopup { get; set; }
        public string? IsTab { get; set; }
    }

}
