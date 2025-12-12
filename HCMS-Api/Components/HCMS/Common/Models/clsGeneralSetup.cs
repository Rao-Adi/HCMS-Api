namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class clsGeneralSetup
    {
        public string? SetupCode { get; set; }
        public string? SetupDescription { get; set; }

        public string? SetupDescriptionArabic { get; set; }

        public int smsid { get; set; }

        public string? timeStamp { get; set; }
        public string? CompanyId { get; set; }
        public string? Culture { get; set; }
        public string? FormId { get; set; }
    }

    public class GenSetups
    {
        private string? _smsId;
        private string? _sdlid;
        private string? _code;
        private string? _Name;
        private string? _ArabicName;
        private string? _Flag;
        private int _FlagDisable;
        private string? _timeStamp;

        public string? CompanyId { get; set; }
        public string? Culture { get; set; }
        public string? FormId { get; set; }

        public string TimeStamp
        {
            get { return _timeStamp; }
            set { _timeStamp = value; }
        }

        public int DisableFlag
        {
            get { return _FlagDisable; }
            set { _FlagDisable = value; }
        }

        public string Flag
        {
            get { return _Flag; }
            set { _Flag = value; }
        }


        public string smsId
        {
            get { return _smsId; }
            set { _smsId = value; }
        }

        public string sdlid
        {
            get { return _sdlid; }
            set { _sdlid = value; }
        }

        public string Code
        {
            get { return _code; }
            set { _code = value; }
        }

        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        public string ArabicName
        {
            get { return _ArabicName; }
            set { _ArabicName = value; }
        }
    }

    public class Headings
    {
        private string _Variable;
        private string _Name;
        private string _NameAR;
        private int _Flag;

        public string Variable
        {
            get { return _Variable; }
            set { _Variable = value; }
        }
        public string DisplayName
        {
            get { return _Name; }
            set { _Name = value; }
        }
        public string NameAR
        {
            get { return _NameAR; }
            set { _NameAR = value; }
        }
        public int Flag
        {
            get { return _Flag; }
            set { _Flag = value; }
        }
    }
}
