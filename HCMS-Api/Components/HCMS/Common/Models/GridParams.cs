namespace HCMS_Api.Components.HCMS.Common.Models
{
    public class GridParams
    {
        internal string? Stage;

        public string? searchParam { get; set; }
        public string? Expression { get; set; }
        public string? Direction { get; set; }
        public string? pageSize { get; set; }
        public string? pageNumber { get; set; }

        public GridCols[] DisplayedCols { get; set; }

        public string GetFilterClause()
        {
            string resultentQuery = string.Empty;
            if (this.DisplayedCols != null)
            {
                DateTime date;
                bool isSearchParamDate = DateTime.TryParse(this.searchParam, out date);
                if (isSearchParamDate)
                {
                    foreach (GridCols col in this.DisplayedCols.Where(x => x.DataType == ColDataType.Date && !string.IsNullOrEmpty(x.ColName)))
                    {
                        resultentQuery = resultentQuery + $" CAST([{col.ColName}] as date) = CAST('{date.ToString("dd-MMM-yyyy")}' as date) OR";
                    }
                }
                else
                {
                    foreach (GridCols col in this.DisplayedCols.Where(x => !string.IsNullOrEmpty(x.ColName)))
                    {
                        resultentQuery = resultentQuery + $" {col.ColName} LIKE '%{this.searchParam}%' OR";
                    }
                }

                resultentQuery = resultentQuery != string.Empty ? resultentQuery.Remove(resultentQuery.Length - 2, 2) : resultentQuery;
            }
            return resultentQuery;
        }
    }

    public class GridCols
    {
        public string? ColName { get; set; }
        public ColDataType DataType { get; set; }
    }

    public enum ColDataType
    {
        Text, Date, Numeric, Code, Amount, Toggle
    }
}
