using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class DBHelper
    {
        private static readonly IConfiguration _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        private static readonly string CONNECTION_STRING = "";
        private static readonly string FINANCE_CONNECTION_STRING = "";
        private static readonly string MS_ACCESS_CONNECTION_STRING = "";
        private static readonly string INTEGRATION_CONNECTION_STRING = "";

        static DBHelper()
        {
            CONNECTION_STRING = _configuration.GetConnectionString("ConnectionString");
            FINANCE_CONNECTION_STRING = _configuration.GetConnectionString("ConnectionString");
            MS_ACCESS_CONNECTION_STRING = _configuration.GetConnectionString("ConnectionString"); ;
            INTEGRATION_CONNECTION_STRING = _configuration.GetConnectionString("ConnectionString");

        }
        /// <summary>
        /// Execute a SqlCommand (that returns no resultset) against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParamters used to execute the command</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                    int val = cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    return val;
                }
            }
            catch
            {
                throw;
            }
        }

        public static int ExecuteNonQueryFinance(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            try
            {
                using (SqlConnection conn = new SqlConnection(FINANCE_CONNECTION_STRING))
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                    int val = cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    return val;
                }
            }
            catch
            {
                throw;
            }
        }




        public static string ExecuteNonQueryAnalyzer(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                    int val = cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    //return val;
                    return "(" + val.ToString() + " row(s) affected)";
                }
            }
            catch (Exception ex)
            {
                string str = "Message : " + ex.Message + " Source : " + ex.Source;
                return str;
            }
        }



        /// <summary>
        /// Execute a SqlCommand (that returns no resultset) using an existing SQL Transaction 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="trans">an existing sql transaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParamters used to execute the command</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            PrepareCommand(cmd, trans.Connection, trans, cmdType, cmdText, cmdParms);
            int val = cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            return val;
        }




        /// <summary>
        /// Execute a SqlCommand a dataset against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParamters used to execute the command</param>
        /// <returns>dataset representing the data in table</returns>
        public static DataSet ExecuteQueryReturnDS(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                DataSet ds = new DataSet();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(ds);
                cmd.Parameters.Clear();
                return ds;
            }
        }

        public static DataSet ExecuteFinanceQueryReturnDS(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(FINANCE_CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                DataSet ds = new DataSet();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(ds);
                cmd.Parameters.Clear();
                return ds;
            }
        }
        public static DataSet ExecuteQueryReturnDS(SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            PrepareCommand(cmd, trans.Connection, trans, cmdType, cmdText, cmdParms);
            DataSet ds = new DataSet();
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            da.Fill(ds);
            cmd.Parameters.Clear();
            return ds;
        }








        /// <summary>
        /// Execute a SqlCommand that returns a resultset against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SqlDataReader r = ExecuteReader(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParamters used to execute the command</param>
        /// <returns>A SqlDataReader containing the results</returns>
        public static SqlDataReader ExecuteReader(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            SqlConnection conn = new SqlConnection(CONNECTION_STRING);

            // we use a try/catch here because if the method throws an exception we want to 
            // close the connection throw code, because no datareader will exist, hence the 
            // commandBehaviour.CloseConnection will not work
            try
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                cmd.Parameters.Clear();
                return rdr;
            }
            catch
            {
                conn.Close();
                throw;
            }
        }



        public static OleDbDataReader ExecuteReaderMSAccess(CommandType cmdType, string cmdText, params OleDbParameter[] cmdParms)
        {
            OleDbCommand cmd = new OleDbCommand();
            OleDbConnection conn = new OleDbConnection(MS_ACCESS_CONNECTION_STRING);

            // we use a try/catch here because if the method throws an exception we want to 
            // close the connection throw code, because no datareader will exist, hence the 
            // commandBehaviour.CloseConnection will not work
            try
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                OleDbDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                cmd.Parameters.Clear();
                return rdr;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                conn.Close();
                throw;
            }
        }
        public static SqlDataReader ExecuteReader(SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            // we use a try/catch here because if the method throws an exception we want to 
            // close the connection throw code, because no datareader will exist, hence the 
            // commandBehaviour.CloseConnection will not work

            PrepareCommand(cmd, trans.Connection, trans, cmdType, cmdText, cmdParms);
            SqlDataReader rdr = cmd.ExecuteReader();
            cmd.Parameters.Clear();
            return rdr;

        }


        /// <summary>
        /// Execute a SQL command that returns the first field, of the first row of result
        /// </summary>
        /// <param name="cmdType">The command type</param>
        /// <param name="cmdText">SQL query</param>
        /// <param name="cmdParms">Array of SQL paramters</param>
        /// <returns>Returns first column value, of the first row</returns>
        public static Object ExecuteScalar(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                Object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
        }

        /// <summary>
        /// Execute a SQL command that returns the first field, of the first row of result
        /// </summary>
        /// <param name="cmdType">The command type</param>
        /// <param name="cmdText">SQL query</param>
        /// <param name="cmdParms">Array of SQL paramters</param>
        /// <returns>Returns first column value, of the first row</returns>
        /// added by maaz [06-sep-2016] for connectionstring parameter
        public static Object ExecuteScalarJobPortal(CommandType cmdType, string cmdText, string Connectionstring, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(Connectionstring))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                Object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
        }

        // added by hafiz saad - 
        // Use for SP with output parameter,it returns key value pair like (dictionary("parametername","value"))
        public static Dictionary<string, string> ExecuteSPWithOutputParams(CommandType cmdType, string cmdText, SqlParameter[] OutputParams, params SqlParameter[] cmdParms)
        {
            Dictionary<string, string> lstOutPutParams = new Dictionary<string, string>();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, OutputParams, cmdParms);
                Object RowsAffected = cmd.ExecuteScalar();

                foreach (SqlParameter param in OutputParams)
                    lstOutPutParams.Add(param.ParameterName, cmd.Parameters[param.ParameterName].Value.ToString());


                cmd.Parameters.Clear();
            }

            return lstOutPutParams;
        }
        public static Dictionary<string, string> ExecuteSPWithOutputParams(CommandType cmdType, string cmdText, SqlParameter[] OutputParams, ref DataTable dt, params SqlParameter[] cmdParms)
        {
            Dictionary<string, string> lstOutPutParams = new Dictionary<string, string>();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, OutputParams, cmdParms);
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt);

                foreach (SqlParameter param in OutputParams)
                    lstOutPutParams.Add(param.ParameterName, cmd.Parameters[param.ParameterName].Value.ToString());


                cmd.Parameters.Clear();
            }

            return lstOutPutParams;
        }
        public static Object ExecuteScalarFinance(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(FINANCE_CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                Object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
        }

        public static Object ExecuteScalar(SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                PrepareCommand(cmd, trans.Connection, trans, cmdType, cmdText, cmdParms);
                Object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
        }


        /// <summary>
        /// Prepare a command for execution
        /// </summary>
        /// <param name="cmd">SqlCommand object</param>
        /// <param name="conn">SqlConnection object</param>
        /// <param name="trans">SqlTransaction object</param>
        /// <param name="cmdType">Cmd type e.g. stored procedure or text</param>
        /// <param name="cmdText">Command text, e.g. Select * from Products</param>
        /// <param name="cmdParms">SqlParameters to use in the command</param>
        private static void PrepareCommand(SqlCommand cmd, SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, SqlParameter[] cmdParms)
        {

            if (conn.State != ConnectionState.Open)
                conn.Open();

            cmd.Connection = conn;
            cmd.CommandText = cmdText;

            if (trans != null)
                cmd.Transaction = trans;

            cmd.CommandType = cmdType;

            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                    cmd.Parameters.Add(parm);
            }
        }

        private static void PrepareCommand(SqlCommand cmd, SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, SqlParameter[] OutputParams, SqlParameter[] cmdParms)
        {

            if (conn.State != ConnectionState.Open)
                conn.Open();

            cmd.Connection = conn;
            cmd.CommandText = cmdText;

            if (trans != null)
                cmd.Transaction = trans;

            cmd.CommandType = cmdType;

            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                    cmd.Parameters.Add(parm);
            }
            if (OutputParams != null)
            {
                foreach (SqlParameter param in OutputParams)
                {
                    cmd.Parameters.Add(param);
                    param.Direction = ParameterDirection.Output;
                }
            }
        }



        private static void PrepareCommand(OleDbCommand cmd, OleDbConnection conn, OleDbTransaction trans, CommandType cmdType, string cmdText, OleDbParameter[] cmdParms)
        {

            if (conn.State != ConnectionState.Open)
                conn.Open();

            cmd.Connection = conn;
            cmd.CommandText = cmdText;

            if (trans != null)
                cmd.Transaction = trans;

            cmd.CommandType = cmdType;

            if (cmdParms != null)
            {
                foreach (OleDbParameter parm in cmdParms)
                    cmd.Parameters.Add(parm);
            }
        }

        public static DataTable ExecuteQueryReturnDT(string sqlQuery)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                DataTable dt = new DataTable();
                SqlDataAdapter ada = new SqlDataAdapter(sqlQuery, conn);
                ada.Fill(dt);
                cmd.Parameters.Clear();
                return dt;
            }
        }

        public static DataTable ExecuteQueryReturnDT(string sqlQuery, string ConnectionString)
        {

            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                DataTable dt = new DataTable();
                SqlDataAdapter ada = new SqlDataAdapter(sqlQuery, conn);
                ada.Fill(dt);
                cmd.Parameters.Clear();
                return dt;
            }
        }




        public static bool ExecuteNonQuery(string tableName, string[] values, SqlParameter[] param)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 3600;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                string sqlQuery = "";
                //: Creat a QueryString			
                sqlQuery = GenerateInsertQuery(tableName, values, param);
                //Step1: Create a command object
                SqlCommand sqlCommand = CreateCommand(sqlQuery, CommandType.Text, param, conn);
                //Step2: call Text Query and return boolean

                conn.Open();
                int affected_rows = sqlCommand.ExecuteNonQuery();
                conn.Close();

                if (affected_rows < 1)
                    return false;
                else
                    return true;
            }
        }



        #region GenerateInsertQuery(string tableName,string[] colNamee,SqlParameter[] values)

        public static string GenerateInsertQuery(string tableName, string[] colNames, SqlParameter[] values)
        {
            string sqlQuery = "insert into " + tableName + " (";
            for (int k = 0; k < colNames.Length; k++)
            {
                string colname = colNames[k].ToString();
                sqlQuery += colname.ToString();
                if (k < colNames.Length - 1)
                    sqlQuery += ",";
                if (k == colNames.Length - 1)
                    sqlQuery += ") values (";
            }

            for (int j = 0; j < values.Length; j++)
            {
                sqlQuery += values[j];
                if (j != values.Length - 1)
                    sqlQuery += ",";
                if (j == values.Length - 1)
                    sqlQuery += ")";
            }
            return sqlQuery;
        }



        #endregion

        #region CreateCommand(string sql_query,CommandType command_type,SqlParameter[] param,SqlConnection currConn)
        private static SqlCommand CreateCommand(string sql_query, CommandType command_type, SqlParameter[] param, SqlConnection currConn)
        {

            //'Step2: Create sql command and set its properties
            SqlCommand sql_command = new SqlCommand(sql_query);
            sql_command.CommandType = command_type;

            //'Step3: if there are parameters in the system add them to query

            if (param != null)
            {
                int i = param.Length;
                foreach (SqlParameter sql_param in param)
                {
                    sql_command.Parameters.Add(sql_param);
                }
            }
            try
            {
                //connection = new SqlConnection(ConnectionString)
                sql_command.Connection = currConn;
                return sql_command;
            }
            catch (Exception e1)
            {
                return null;
            }
        }


        #endregion




        /// <summary>
        /// Execute non query against [HCMS_Integration] Database
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="cmdText"></param>
        /// <param name="cmdParms"></param>
        /// <returns></returns>
        public static int ExecuteNonQueryIntegrationDB(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            try
            {
                using (SqlConnection conn = new SqlConnection(INTEGRATION_CONNECTION_STRING))
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                    int val = cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    return val;
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Execute scalar against [HCMS_Integration] database
        /// </summary>
        /// <param name="cmdType"></param>
        /// <param name="cmdText"></param>
        /// <param name="cmdParms"></param>
        /// <returns></returns>
        public static Object ExecuteScalarIntegrationDB(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {

            SqlCommand cmd = new SqlCommand();
            using (SqlConnection conn = new SqlConnection(INTEGRATION_CONNECTION_STRING))
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                Object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
        }

    }
}
