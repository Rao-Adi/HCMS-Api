namespace HCMS_Api.Components.HCMS.Common.DataAccess
{
    public class ErrorHandler
    {
        private int errorCode;
        private string errorMessage;
        private bool errorOccurred;

        public ErrorHandler()
        {
        }
        public string DisplayError(int errorCode, string errorMessage)
        {
            switch (errorCode)
            {
                default:
                    errorMessage = "Un-defined error.";
                    break;
            }

            return errorMessage;
        }
        public int ErrorCode
        {
            set { errorCode = value; }
            get { return errorCode; }
        }
        public string ErrorMessage
        {
            get { return errorMessage; }
            set { errorMessage = value; }
        }
        public bool ErrorOccurred
        {
            set { errorOccurred = value; }
            get { return errorOccurred; }
        }
    }

    public enum ErrorCodes
    {
        CONNECTION_ERROR = 1,
        RETRIEVAL_ERROR = 2,
        DUPLICATE_ERROR = 3,
        NULL_VALUES_ERROR = 4,
        REFERENCE_ERROR = 5,
        UNDEFINED_ERROR = 6
    }
}
