using System.Data;

namespace HCMS_Api.Components.HCMS.Common
{
    public static class ExtensionMethods
    {
        public static string GetRequiredConnectionString(this IConfiguration config, string name)
        {
            var value = config.GetConnectionString(name);
            return !string.IsNullOrEmpty(value) ? value : throw new InvalidOperationException($"Connection string '{name}' not found.");
        }

        public static DataTable ConvertToDataTable<T>(this IConfiguration config, List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);

            // Get all the properties
            var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in props)
            {
                dataTable.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }

            foreach (var item in items)
            {
                var values = new object[props.Length];
                for (int i = 0; i < props.Length; i++)
                {
                    values[i] = props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }

            return dataTable;
        }

        public static List<Dictionary<string, object>> ConvertToList(this DataTable table)
        {
            // Convert DataTable to List of Dictionaries for JSON serialization
            var rows = new List<Dictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    var value = row[col];
                    if (value == DBNull.Value || (value is string strValue && string.IsNullOrWhiteSpace(strValue)))
                    {
                        rowDict[col.ColumnName] = null;
                    }
                    else
                    {
                        rowDict[col.ColumnName] = value;
                    }
                }
                rows.Add(rowDict);
            }
            return rows;
        }


    }
}
