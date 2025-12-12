namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class UserInfo
    {
        private int _userEmpId;
        private string _userEmpCode;
        private string _userEmpName;
        private string _userId;

        public string UserID
        {
            get { return _userId; }
            set { _userId = value; }
        }

        public string UserEmpCode
        {
            get { return _userEmpCode == null ? String.Empty : _userEmpCode; }
            set { _userEmpCode = value; }
        }

        public string UserEmpName
        {
            get { return _userEmpName == null ? String.Empty : _userEmpName; }
            set { _userEmpName = value; }
        }

        public int UserEmpId
        {
            get { return _userEmpId; }
            set { _userEmpId = value; }
        }

        public UserInfo()
        {
            // TODO: Add constructor logic here
        }
    }

}
