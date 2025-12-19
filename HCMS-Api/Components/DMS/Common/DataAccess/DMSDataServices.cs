using System.Data;

namespace HCMS_Api.Components.DMS.Common.DataAccess
{
    public class DMSDataServices
    {
        private string ConString = string.Empty;
        private string ConStringJobPortal = string.Empty;

        private readonly IConfiguration _configuration;

        public string ConStringProperty
        {
            get { return ConString; }
            set { ConString = value; }
        }

        public string ConStringJobPortalProperty
        {
            get { return ConStringJobPortal; }
            set { ConStringJobPortal = value; }
        }

        public DMSDataServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void BeginProcess(string connectionString)
        {
            ConString = connectionString;
        }

        public string ExecuteStatementForSecurity(string commandStatement,
            ref object returnObject,
            int returnType)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            
            string _ConStringSecurity = _configuration.GetRequiredConnectionString("SecurityConnectionString");
            objDataProcess.Initialize(_ConStringSecurity, ref objErrorHandler);

            switch (returnType)
            {
                case 1:
                    returnObject = objDataProcess.ExecuteScalarValue(commandStatement, ref objErrorHandler);
                    break;
                case 2:
                    returnObject = objDataProcess.ExecuteStatement(commandStatement, ref objErrorHandler);
                    break;
            }

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }
        public string GetDataWithClauseDS(string strQuery, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset.Tables.Add(objDataProcess.GetRecordsDT(strQuery, ref objErrorHandler));

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }
        public string GetDataWithClauseSecurityDS(string strQuery, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            ConString = _configuration.GetRequiredConnectionString("SecurityConnectionString");
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset.Tables.Add(objDataProcess.GetRecordsDT(strQuery, ref objErrorHandler));

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }




        public string GetDataWithClause(bool EnforceConstraints, string columnNames, string tableName, string whereClause, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset = objDataProcess.GetRecords(EnforceConstraints, columnNames, tableName, whereClause, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }


        public string GetDataWithClause(string columnNames, string tableName, string whereClause, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset = objDataProcess.GetRecords(columnNames, tableName, whereClause, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }
        public string GetDataWithOrder(string columnNames,
            string tableName,
            string whereClause,
            string orderBy,
            ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset = objDataProcess.GetRecords(columnNames,
                tableName,
                whereClause,
                orderBy,
                ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successfull.";
        }
        public string GetDataWithClauseApplicationDB(string columnNames, string tableName, string whereClause, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            ConString = _configuration.GetRequiredConnectionString("DMSConnectionString");
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset = objDataProcess.GetRecords(columnNames, tableName, whereClause, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }


        public string ExecuteReader(string commandStatement, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            //ConString=_configuration.GetConnectionString("SecurityConnectionString");

            objDataProcess.Initialize(ConString, ref objErrorHandler);

            DataTable datatable = objDataProcess.ExecuteDataReader(commandStatement, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
            {
                return objErrorHandler.ErrorMessage;
            }
            else
            {
                dataset.Tables.Add(datatable);
                return "successful";
            }
        }

        public string ExecuteReaderDS(string commandStatement,
          ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            DataSet ds = objDataProcess.GetRecordsDS(commandStatement, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
            {
                dataset = ds;
                return "successfull";
            }
        }

        public string ExecuteSecurityReader(string commandStatement, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            ConString = _configuration.GetRequiredConnectionString("SecurityConnectionString");

            objDataProcess.Initialize(ConString, ref objErrorHandler);

            DataTable datatable = objDataProcess.ExecuteDataReader(commandStatement, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
            {
                return objErrorHandler.ErrorMessage;
            }
            else
            {
                dataset.Tables.Add(datatable);
                return "successful";
            }
        }


        public string ExecuteStatement(string commandStatement,
            ref object returnObject,
            int returnType)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);

            objDataProcess.Initialize(ConString, ref objErrorHandler);

            switch (returnType)
            {
                case 1:
                    returnObject = objDataProcess.ExecuteScalarValue(commandStatement, ref objErrorHandler);
                    break;
                case 2:
                    returnObject = objDataProcess.ExecuteStatement(commandStatement, ref objErrorHandler);
                    break;
            }

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }
        public string ExecuteSecurityStatement(string commandStatement,
            ref object returnObject,
            int returnType)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            ConString = _configuration.GetRequiredConnectionString("SecurityConnectionString");
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            switch (returnType)
            {
                case 1:
                    returnObject = objDataProcess.ExecuteScalarValue(commandStatement, ref objErrorHandler);
                    break;
                case 2:
                    returnObject = objDataProcess.ExecuteStatement(commandStatement, ref objErrorHandler);
                    break;
            }

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successful";
        }

        public string UpdateData(string tableName,
            DataSet dataset,
            string whereClause)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            try
            {
                if (objDataProcess.UpdateRecord(tableName, whereClause, dataset, ref objErrorHandler))
                    return "Record is updated, Successfully!";
            }
            catch (Exception exception)
            {
                if (!objErrorHandler.ErrorOccurred)
                {
                    objErrorHandler.ErrorOccurred = true;
                    objErrorHandler.ErrorMessage = exception.Message;
                }
            }
            return objErrorHandler.ErrorMessage;
        }

        public string GetDataSet(string strQuery, ref DataSet dataset)
        {
            DMSErrorHandler objErrorHandler = new DMSErrorHandler();
            DMSDataProcess objDataProcess = new DMSDataProcess(_configuration);
            objDataProcess.Initialize(ConString, ref objErrorHandler);

            dataset = objDataProcess.GetRecordsDS(strQuery, ref objErrorHandler);

            if (objErrorHandler.ErrorOccurred)
                return objErrorHandler.ErrorMessage;
            else
                return "successfull";
        }
    }
}
