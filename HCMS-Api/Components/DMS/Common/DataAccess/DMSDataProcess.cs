using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;

namespace HCMS_Api.Components.DMS.Common.DataAccess
{
    public class DMSDataProcess
    {
        private string _connectionString;


        private readonly IConfiguration _configuration;

        private SqlTransaction transaction;
        private SqlConnection connection;
        private SqlDataAdapter adapter;
        private SqlCommand command;

        public DMSDataProcess(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DMSConnectionString");
        }
        private DataTable DataTableObject(string selectStatement,
       bool schema, ref DMSErrorHandler errorHandler)
        {
            try
            {
                // Use a supported database provider and connection approach
                // For example, with Entity Framework Core:
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(selectStatement, connection))
                    {
                        var datatable = new DataTable();

                        if (schema)
                        {
                            using (var reader = command.ExecuteReader(CommandBehavior.SchemaOnly))
                            {
                                datatable.Load(reader);
                            }
                        }
                        else
                        {
                            using (var reader = command.ExecuteReader())
                            {
                                datatable.Load(reader);
                            }
                        }

                        return datatable;
                    }
                }
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            return null;
        }
        public DataTable GetRecordsDT(string strQuery, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = strQuery;
            return DataTableObject(selectStatement, false, ref errorHandler);
        }

        public void Initialize(string connectionString, ref DMSErrorHandler errorHandler)
        {
            if (connectionString != null && connectionString != "")
                _connectionString = connectionString;
            else
                _connectionString = _configuration.GetConnectionString("ConnectionString");
        }

        public object ExecuteScalarValue(string commandStatement, ref DMSErrorHandler errorHandler)
        {
            try
            {
                if (ConnectionOpened(ref errorHandler))
                {
                    command = new SqlCommand(commandStatement, connection);
                    command.CommandTimeout = 3600;
                    return command.ExecuteScalar();
                }
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            finally
            {
                CloseConnection(ref errorHandler);
            }
            return null;
        }

        public int ExecuteStatement(string commandStatement, ref DMSErrorHandler errorHandler)
        {
            try
            {
                if (ConnectionOpened(ref errorHandler))
                {
                    transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                    command = new SqlCommand(commandStatement, connection, transaction);

                    // Start Added by Danish Iftikhar for running Attendance Month End Process
                    command.CommandTimeout = 3600; // 3600 Means 1 Hour
                    // End Added by Danish Iftikhar for running Attendance Month End Process

                    int result = command.ExecuteNonQuery();

                    if (result != 0)
                    {
                        transaction.Commit();
                        return result;
                    }
                }
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
                transaction.Rollback();
            }
            finally
            {
                CloseConnection(ref errorHandler);
            }
            return 0;
        }

        public DataTable ExecuteDataReader(string commandStatement, ref DMSErrorHandler errorHandler)
        {
            try
            {
                if (ConnectionOpened(ref errorHandler))
                {
                    command = new SqlCommand(commandStatement, connection);
                    command.CommandTimeout = 3600;
                    SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                    DataTable datatable = new DataTable();
                    datatable.Load(reader);
                    reader.Close();
                    return datatable;
                }
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            finally
            {
                CloseConnection(ref errorHandler);
            }
            return null;
        }
        //private DataSet DataSetObject(string selectStatement, bool EnforceConstraints, bool IsProcedure, ref ErrorHandler errorHandler)
        //{
        //    try
        //    {
        //        if (ConnectionOpened(ref errorHandler))
        //        {
        //            command = new SqlCommand(selectStatement, connection);
        //            command.CommandType = IsProcedure ? CommandType.StoredProcedure : CommandType.Text;
        //            command.CommandTimeout = 3600;
        //            adapter = new SqlDataAdapter(command);
        //            DataSet dataset = new DataSet();
        //            if (EnforceConstraints)
        //                dataset.EnforceConstraints = true;
        //            adapter.Fill(dataset);
        //            return dataset;
        //        }
        //    }
        //    catch (SqlException exception)
        //    {
        //        if (!errorHandler.ErrorOccurred)
        //        {
        //            errorHandler.ErrorOccurred = true;
        //            errorHandler.ErrorMessage = exception.Message;
        //        }
        //    }
        //    finally
        //    {
        //        CloseConnection(ref errorHandler);
        //    }
        //    return null;
        //}
        private DataSet DataSetObject(string selectStatement, bool schema, ref DMSErrorHandler errorHandler)
        {
            try
            {
                if (ConnectionOpened(ref errorHandler))
                {


                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(selectStatement, _connectionString);

                    // Start Added by Danish Iftikhar for running Attendance Month End Process
                    sqlDataAdapter.SelectCommand.CommandTimeout = 3600;    // 3600 Means 1 Hour
                                                                           // End Added by Danish Iftikhar for running Attendance Month End Process

                    sqlDataAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                    DataSet dataset = new DataSet();

                    if (schema)
                        sqlDataAdapter.FillSchema(dataset, SchemaType.Source);
                    else
                        sqlDataAdapter.Fill(dataset);

                    return dataset;
                }
            }
            catch (OleDbException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            finally
            {
                CloseConnection(ref errorHandler);
            }
            return null;
        }
        private DataSet DataSetObject(bool enforceConstraints, string selectStatement, bool schema, ref DMSErrorHandler errorHandler)
        {
            try
            {
                if (ConnectionOpened(ref errorHandler))
                {
                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(selectStatement, _connectionString);

                    // Start Added by Danish Iftikhar for running Attendance Month End Process
                    sqlDataAdapter.SelectCommand.CommandTimeout = 3600;    // 3600 Means 1 Hour
                                                                           // End Added by Danish Iftikhar for running Attendance Month End Process

                    sqlDataAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                    DataSet dataset = new DataSet();
                    dataset.EnforceConstraints = enforceConstraints;
                    if (schema)
                        sqlDataAdapter.FillSchema(dataset, SchemaType.Source);
                    else
                        sqlDataAdapter.Fill(dataset);

                    return dataset;
                }
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            finally
            {
                CloseConnection(ref errorHandler);
            }
            return null;
        }

        public DataSet GetRecords(string tableName, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = "SELECT * FROM " + tableName;
            return DataSetObject(selectStatement, false, ref errorHandler);
        }

        public DataSet GetRecords(string columnsName, string tableName, string whereClause, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = "SELECT " + columnsName + " FROM " + tableName + " WHERE " + whereClause;
            return DataSetObject(selectStatement, false, ref errorHandler);
        }

        public DataSet GetRecords(string columnsName, string tableName, string whereClause, string orderBy, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = "SELECT " + columnsName + " FROM " + tableName;

            if (!whereClause.Equals(""))
                selectStatement += " WHERE " + whereClause;

            selectStatement += " ORDER BY " + orderBy;

            return DataSetObject(selectStatement, false, ref errorHandler);
        }

        public DataSet GetRecords(bool EnforceConstraints, string columnsName, string tableName, string whereClause, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = "SELECT " + columnsName + " FROM " + tableName + " WHERE " + whereClause;
            return DataSetObject(EnforceConstraints, selectStatement, false, ref errorHandler);
        }


        private bool ConnectionOpened(ref DMSErrorHandler errorHandler)
        {
            try
            {
                connection = new SqlConnection(_connectionString);

                connection.Open();
                return true;
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            return false;
        }

        private void CloseConnection(ref DMSErrorHandler errorHandler)
        {
            try
            {
                connection.Close();
            }
            catch (SqlException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
        }

        public DataSet GetRecordsDS(string strQuery, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = strQuery;
            return DataSetObject(selectStatement, ref errorHandler);
        }

        private DataSet DataSetObject(string selectStatement, ref DMSErrorHandler errorHandler)
        {
            try
            {
                if (ConnectionOpened(ref errorHandler))
                {
                    adapter = new SqlDataAdapter(selectStatement, _connectionString);
                    //DataSet dataset = new DataSet();
                    //Start Added by Munawar Zamman
                    adapter.SelectCommand.CommandTimeout = 3600;    //3600 Means 1 Hour
                    //End Added by  Munawar Zamman

                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    return ds;
                }
            }
            catch (OleDbException exception)
            {
                if (!errorHandler.ErrorOccurred)
                {
                    errorHandler.ErrorOccurred = true;
                    errorHandler.ErrorMessage = exception.Message;
                }
            }
            finally
            {
                CloseConnection(ref errorHandler);
            }
            return null;
        }

        public bool UpdateRecord(string tableName, string whereClause,
            DataSet dataset, ref DMSErrorHandler errorHandler)
        {
            string selectStatement = "SELECT * FROM " + tableName
                + " WHERE " + whereClause;

            if (UpdateDataSource(selectStatement,
                dataset.GetChanges(DataRowState.Modified), ref errorHandler) > 0)
                return true;

            return false;
        }

        private int UpdateDataSource(string selectStatement,
            DataSet dataset, ref DMSErrorHandler errorHandler)
        {
            int result = -1;

            if (ConnectionOpened(ref errorHandler))
            {
                try
                {
                    adapter = new SqlDataAdapter(selectStatement, _connectionString);

                    SqlCommandBuilder builder = new SqlCommandBuilder(adapter);

                    DataSet _dataset = new DataSet();
                    adapter.Fill(_dataset);
                    _dataset = dataset.Copy();
                    if (adapter.SelectCommand != null)
                    {
                        adapter.SelectCommand.CommandTimeout = 500;

                    }
                    result = adapter.Update(_dataset);
                }
                catch (OleDbException exception)
                {
                    if (!errorHandler.ErrorOccurred)
                    {
                        errorHandler.ErrorOccurred = true;
                        errorHandler.ErrorMessage = exception.Message;
                    }
                }
                finally
                {
                    CloseConnection(ref errorHandler);
                }
            }
            return result;
        }

    }
}
