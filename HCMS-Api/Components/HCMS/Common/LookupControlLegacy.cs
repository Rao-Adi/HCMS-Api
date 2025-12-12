using HCMS_Api.Components.HCMS.Common.DataAccess;
using System.Configuration;
using System.Data;
using System.Text;

namespace HCMS_Api.Components.HCMS.Common
{
    public class LookupControlLegacy
    {        
        private readonly IConfiguration _configuration;
        private readonly DataServices _dataservice;

        public LookupControlLegacy(IConfiguration configuration, DataServices dataservice)
        {
            _configuration = configuration;
            _dataservice = dataservice;

            string connectionString = configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }

        public List<object> GetLookupQuery(string wc, string tableName, params string[] columnName)
        {
            DataSet ds = new DataSet();
            List<object> list = new List<object>();
            string addColumn = "";
            if (columnName.Length > 0)
            {
                foreach (string col in columnName)
                {
                    if (columnName[columnName.Length - 1] == col)
                        addColumn += col;
                    else
                        addColumn += col + ",";
                }
                _dataservice.GetDataWithClause("" + addColumn + "",
                "" + tableName + "",
                "" + wc + "",
                ref ds);
                if (!(ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0))
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {

                        Dictionary<String, Object> dd = new Dictionary<string, object>();
                        foreach (string col in columnName)
                        {
                            dd.Add(col, dr[col]);
                        }
                        list.Add(dd);
                    }
                    return list;
                }
                else
                {
                    return list;
                }
            }
            else
            {
                return list;
            }

        }
        public List<object> GetSearchQuery(string Search, string FieldName, string whereClauseSearchBar, string tableName, string hideColumn, params string[] columnName)
        {
            DataSet ds = new DataSet();
            List<object> list = new List<object>();
            string addColumn = "";
            if (Search != "" && Search != null && FieldName != "0")
                whereClauseSearchBar += " and (" + FieldName + " Like N'%" + Search + "%')";
            else
            {
                StringBuilder likeAllColumns = new StringBuilder();
                var HideColumns = hideColumn.Trim().Split(',');
                var finalColumnName = columnName.Except(HideColumns);
                foreach (String Column in finalColumnName)
                {
                    likeAllColumns.Append(" or " + Column + " Like N'%" + Search + "%'");
                }
                whereClauseSearchBar += " and (" + likeAllColumns.ToString().Remove(0, 3) + " )";
            }
            if (columnName.Length > 0)
            {
                foreach (string col in columnName)
                {
                    if (columnName[columnName.Length - 1] == col)
                        addColumn += col;
                    else
                        addColumn += col + ",";
                }
                _dataservice.GetDataWithClause("" + addColumn + "",
                "" + tableName + "",
                "" + whereClauseSearchBar + "",
                ref ds);
                if (!(ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0))
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        Dictionary<String, Object> dd = new Dictionary<string, object>();
                        foreach (string col in columnName)
                        {
                            dd.Add(col, dr[col]);
                        }
                        list.Add(dd);
                    }
                    return list;
                }
                else
                {
                    return list;
                }
            }
            else
            {
                return list;
            }

        }
        public List<object> GetSearchCountry(string Search, string whereClauseSearchBar, string tableName, params string[] columnName)
        {
            DataSet ds = new DataSet();
            List<object> list = new List<object>();
            string addColumn = "";
            if (Search != "" && Search != null)
            {
                StringBuilder likeAllColumns = new StringBuilder();
                for (int index = 0; index < columnName.Length; index++)
                {
                    if (index == 0)
                        likeAllColumns.Append(columnName[index] + " Like N'%" + Search + "%'");
                    else
                        likeAllColumns.Append(" or " + columnName[index] + " Like N'%" + Search + "%'");
                }
                whereClauseSearchBar += " and (" + likeAllColumns.ToString() + " )";
            }

            if (columnName.Length > 0)
            {
                foreach (string col in columnName)
                {
                    if (columnName[columnName.Length - 1] == col)
                        addColumn += col;
                    else
                        addColumn += col + ",";
                }
                _dataservice.GetDataWithClause("" + addColumn + "",
                "" + tableName + "",
                "" + whereClauseSearchBar + "",
                ref ds);
                if (!(ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0))
                {
                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        Dictionary<String, Object> dd = new Dictionary<string, object>();
                        foreach (string col in columnName)
                        {
                            dd.Add(col, dr[col]);
                        }
                        list.Add(dd);
                    }
                    return list;
                }
                else
                {
                    return list;
                }
            }
            else
            {
                return list;
            }

        }
    }
}
