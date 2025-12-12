using Serilog;

namespace HCMS_Api.Common
{
    public static class LogHelper
    {
        public static void Info(string info)
        {
            Log.Information(info);
        }

        public static void Warn(string message, Exception ex)
        {
            Log.Warning(ex, message);
        }

        public static void Warn(string message)
        {
            Log.Warning(message);
        }

        public static void Error(string message, Exception ex)
        {
            Log.Error(ex, message);
        }
    }
}
