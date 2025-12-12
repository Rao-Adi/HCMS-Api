using System.Data;
using System.Data.SqlClient;

namespace HCMS_Api.Components.HCMS.Common.DataAccess
{
    public static class ExtensionMethod
    {
        public static T TryGetValue<T>(this SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);

                if (reader.IsDBNull(ordinal))
                {
                    return default(T);
                }

                object value = reader.GetValue(ordinal);
                if (value is T)
                {
                    return (T)value;
                }
                Type targetType = typeof(T);
                return (T)Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                return default(T);
            }
        }

        public static T SafeGetValue<T>(this DataRow row, string columnName)
        {
            if (row.IsNull(columnName))
            {
                return default; // Return default value for type T (null for reference types, 0 for numeric types, etc.)
            }

            var targetType = typeof(T);

            // Handle nullable types
            if (Nullable.GetUnderlyingType(targetType) != null)
            {
                // Get the underlying non-nullable type
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            // Use Convert.ChangeType to safely convert object to target type
            return (T)Convert.ChangeType(row[columnName], targetType);
        }
    }
}
