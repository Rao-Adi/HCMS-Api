using HCMS_Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.Json;
using static Azure.Core.HttpHeader;
using static HCMS_Api.Controllers.HCMS.Common.SecurityController;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace HCMS_Api.Common
{
    public class Common
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public Common(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _httpContextAccessor = httpContextAccessor;
        }
        public List<Dictionary<string, object>> GetJsonDatatable(DataTable table)
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


        public async Task<DataTable> ExecuteSqlQuery(string query)
        {
            var dataTable = new DataTable();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                await connection.OpenAsync();
                adapter.Fill(dataTable);
            }
            return dataTable;
        }
        public async Task<Dictionary<string, object>> ExecuteSqlQuerySingleRow(string query)
        {
            var result = new Dictionary<string, object>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync()) // Agar koi row hai toh read karega
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                result[reader.GetName(i)] = reader.GetValue(i);
                            }
                        }
                    }
                }
            }
            return result;
        }

        public async Task<JwtArray> GetJwtUser()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userDataClaim = user?.FindFirst("UserData")?.Value;

            if (string.IsNullOrEmpty(userDataClaim))
                throw new Exception("UserData claim not found.");

            var userData = JsonSerializer.Deserialize<JwtArray>(userDataClaim);
            return userData;
        }


        public string ExecuteScalarQuery(string query)
        {
            try
            {


                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();
                        return result?.ToString(); // Null-safe conversion
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null; // Return null in case of an error
            }
        }
        public object ExecuteScalarQuery(string colName, string tableName, string whereClause)
        {
            try
            {


                string query = "select " + colName + " from  " + tableName + " where " + whereClause;
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        object result = command.ExecuteScalar();
                        return result; // Null-safe conversion
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null; // Return null in case of an error
            }
        }


        public bool ExecuteNonQuery(string query)
        {

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Connection string is not initialized. Call Initialize() first.");
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Execute the query
                        int rowsAffected = command.ExecuteNonQuery();

                        // Return true if at least one row is affected
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return false;
            }
        }


        public bool CheckDate(string dateVal)
        {///////////////// By Zeenix.net ////////////////////////
            DateTime DT = new DateTime();
            bool check = false;

            check = DateTime.TryParse(dateVal, out DT);
            if (check)
            {
                if (DT.Year < 1900 || DT.Year > 2099)
                {
                    check = false;
                }
            }
            return check;
        }



        //public static DateTime GetSysdate(string GetCompanyId)
        //{
        //    object obj = new object();
        //    string query = "Select dbo.fn_General_GetLocalDateTimeCompanyWise(" + GetCompanyId + ")";
        //    string r = dataService.ExecuteStatement(query, ref obj, 1);

        //    return DateTime.Parse(obj.ToString());
        //}
    }
}
