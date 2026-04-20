using Azure.Storage.Blobs;
using HCMS_Api.Components.DMS.Common.DataAccess; 
using MailKit.Security;
//using Microsoft.IdentityModel.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MimeKit;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Net;
using System.Text;
using HCMS_Api.Common; 
//using Microsoft.EntityFrameworkCore.Storage;
using Dapper;
using HCMS_Api.Components.DMS.Common.Dapper;
using CommandFlags = StackExchange.Redis.CommandFlags;
using HCMS_Api.Components.HCMS.Common.Models;

namespace HCMS_Api.Components.DMS.Common
{
    public class RedisService
    {
        private readonly IConfiguration _configuration;
        private readonly string _redisConnectionString;
        private readonly IConnectionMultiplexer _connection;
        public IDatabase RedisDatabase { get; private set; }

        public IConnectionMultiplexer Connection => _connection; // A


        private readonly Lazy<IConnectionMultiplexer> _connectionMultiplexer;

        public RedisService(IConfiguration configuration)
        {
            _configuration = configuration;
            _redisConnectionString = _configuration.GetConnectionString("RedisConnectionString");


            _connectionMultiplexer = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(_redisConnectionString));

            _connection = ConnectionMultiplexer.Connect(_redisConnectionString);

            RedisDatabase = _connection.GetDatabase();
            // InitializeConnection();
        }

        public async Task<string> GetUserRightsInRedis(string key)
        {
            string value = "";
            try
            {
                var redisDatabase = _connectionMultiplexer.Value;
                value = await redisDatabase.GetDatabase().StringGetAsync(key);
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
            return value;
        }

        //private void InitializeConnection()
        //{
        //    _connection = ConnectionMultiplexer.Connect(_redisConnectionString);
        //    RedisDatabase = _connection.GetDatabase();
        //}
    }

    public class DMSUtilities
    {

        private readonly DMSDataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly ClientContextService _clientContextService;
        private readonly IDMSDapperDataService _dapperService;
        private IDatabase? _redisDatabase = null;

        public DMSUtilities(IConfiguration configuration
            , ClientContextService clientContextService, DMSDataServices dataservice, IDMSDapperDataService dapper)
        {
            _configuration = configuration;
            _clientContextService = clientContextService;
            _dataservice = dataservice;
            _dapperService = dapper;
            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }


        public string GetReadOnlyConnectionString()
        {
            return _configuration.GetConnectionString("ConnectionString");
        }
        public string GetRedisConnection()
        {
            return _configuration.GetConnectionString("RedisConnectionString");
        }
        public string GetRedisHost()
        {
            return _configuration.GetConnectionString("GlobalRedisHost");
        }
        public string GetRedisGlobalHost()
        {
            return _configuration.GetConnectionString("RedisHost");
        }
        public int GetRedisPort()
        {
            return _configuration.GetValue<int>("RedisPort");
        }
        public string UploadtoDrive(IFormFile htp, string strID, string strModuleID, string strFormIDParam, bool NameAsIs, string Compcode)
        {
            string Result = "0";
            try
            {
                ContentType contentType;
                string chkDotinExt = string.Empty;
                string strIsCloudDeployment = _configuration["CorsSettings:IsCloudDeployment"].ToString().ToLower();
                string strFileName = Path.GetFileNameWithoutExtension(htp.FileName);
                string strFileExt = !string.IsNullOrEmpty(Path.GetExtension(htp.FileName)) ? Path.GetExtension(htp.FileName) : ".jpg";
                if (strFileExt[0].Equals('.'))
                    chkDotinExt = strFileExt.Substring(1);

                bool VideoExists = FormatNames.GetAllVideoFormats().Any(a => a.Equals(chkDotinExt));
                bool ImgExists = FormatNames.GetAllImgFormats().Any(a => a.Equals(chkDotinExt));
                bool DocExists = FormatNames.GetAllDocFormats().Any(a => a.Equals(chkDotinExt));

                if (!strFileExt[0].Equals('.'))
                    strFileExt = "." + strFileExt;

                contentType = VideoExists ? ContentType.Video : ImgExists ? ContentType.Image : ContentType.Document;
                if (strIsCloudDeployment.Trim().ToUpper().Equals("NO"))
                    Result = UploadtoPremisis(htp, strID.ToLower(), strModuleID.ToLower(),
                                                     strFormIDParam.ToLower(), contentType, strFileName.ToLower(),
                                                     strFileExt.ToLower(), NameAsIs, Compcode);
                else if (strIsCloudDeployment.Trim().ToUpper().Equals("YES"))
                    Result = UploadToAzure(htp, strID.ToLower(), strModuleID.ToLower(),
                                                   strFormIDParam.ToLower(), contentType, strFileName.ToLower(),
                                                   strFileExt.ToLower(), NameAsIs, Compcode);
            }
            catch (Exception ex)
            {
                Result = "0";
            }

            return Result;
        }

        public string UploadtoDriveForApp(IFormFile htp, string strID, string strModuleID, string strFormIDParam, bool NameAsIs, string Compcode)
        {
            string result = string.Empty;
            try
            {
                ContentType contentType;
                string chkDotinExt = string.Empty;
                string strIsCloudDeployment = _configuration["CorsSettings:IsCloudDeployment"].ToString().ToLower();
                string strFileName = Path.GetFileNameWithoutExtension(htp.FileName);
                string strFileExt = !string.IsNullOrEmpty(Path.GetExtension(htp.FileName)) ? Path.GetExtension(htp.FileName) : ".jpg";

                if (strFileExt[0].Equals('.'))
                    chkDotinExt = strFileExt.Substring(1);

                bool videoExists = FormatNames.GetAllVideoFormats().Any(a => a.Equals(chkDotinExt));
                bool imgExists = FormatNames.GetAllImgFormats().Any(a => a.Equals(chkDotinExt));
                bool docExists = FormatNames.GetAllDocFormats().Any(a => a.Equals(chkDotinExt));

                if (!strFileExt[0].Equals('.'))
                    strFileExt = "." + strFileExt;

                contentType = videoExists ? ContentType.Video : imgExists ? ContentType.Image : ContentType.Document;

                if (strIsCloudDeployment.Trim().ToUpper().Equals("NO"))
                {
                    result = UploadtoPremisis(htp, strID.ToLower(), strModuleID.ToLower(),
                                            strFormIDParam.ToLower(), contentType, strFileName.ToLower(),
                                            strFileExt.ToLower(), NameAsIs, Compcode);
                }
                else if (strIsCloudDeployment.Trim().ToUpper().Equals("YES"))
                {
                    result = UploadToAzure(htp, strID.ToLower(), strModuleID.ToLower(),
                                           strFormIDParam.ToLower(), contentType, strFileName.ToLower(),
                                           strFileExt.ToLower(), NameAsIs, Compcode);
                }

                if (!string.IsNullOrEmpty(result))
                {
                    //Commented Code by Areeb 29092024 Shoaib Code Commented.
                    //string baseDirectory = _configuration["CorsSettings:AttachmentsPathForDD"];
                    //result = Path.Combine(baseDirectory, result);
                    result = result;
                }
            }
            catch (Exception ex)
            {
                result = string.Empty;
            }

            return result;
        }


        public string GetBlobSasUri(string blobName, string policyName = null)
        {
            string strCloudURLSAS = _configuration["blobUrl"].ToLower();
            string sasBlobToken;

            // Get a reference to a blob within the container.
            // Note that the blob may not exist yet, but a SAS can still be created for it.
            CloudBlockBlob blob = getBlockReference().GetBlockBlobReference(blobName);

            if (policyName == null)
            {
                // Create a new access policy and define its constraints.
                // Note that the SharedAccessBlobPolicy class is used both to define the parameters of an ad-hoc SAS, and
                // to construct a shared access policy that is saved to the container's shared access policies.
                SharedAccessBlobPolicy adHocSAS = new SharedAccessBlobPolicy()
                {
                    // When the start time for the SAS is omitted, the start time is assumed to be the time when the storage service receives the request.
                    // Omitting the start time for a SAS that is effective immediately helps to avoid clock skew.
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24),
                    Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Create
                };

                // Generate the shared access signature on the blob, setting the constraints directly on the signature.
                sasBlobToken = blob.GetSharedAccessSignature(adHocSAS);
            }
            else
            {
                // Generate the shared access signature on the blob. In this case, all of the constraints for the
                // shared access signature are specified on the container's stored access policy.
                sasBlobToken = blob.GetSharedAccessSignature(null, policyName);
            }

            // Return the URI string for the container, including the SAS token.
            return sasBlobToken;
        }
        /// <summary>
        /// This Method will get the Container which resides in Azure Portal
        /// This method has all the necessary settings which is required to get the reference of the container
        /// </summary>
        public CloudBlobContainer getBlockReference()
        {
            if (MachineId() == true && MachinePwd() == true)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_configuration["CorsSettings:IsCloudDeployment"].ToString().ToLower());
                //CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_configuration["azureKey"]);
                // Create the blob client.
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                // Retrieve a reference to a container.
                //CloudBlobContainer container = blobClient.GetContainerReference("sft-media");
                string ContainerNameOfBlob = _configuration["CorsSettings:ContainerNameofBlob"].ToString().ToLower(); //_configuration["ContainerNameofBlob"].ToLower();
                CloudBlobContainer container = blobClient.GetContainerReference(ContainerNameOfBlob);
                return container;
            }
            else
            {
                return null;
            }
        }
        public bool MachineId()
        {
            //Asad 15-05-2018
            string Machindid = _configuration["CorsSettings:machineId"].ToString().ToLower(); //Configuration["machineId"];
            return Machindid.ToLower() == "machine.account";
        }

        public bool MachinePwd()
        {
            //Asad 15-05-2018
            string MachindPwd = _configuration["CorsSettings:machinePwd"].ToString().ToLower(); //Configuration["machinePwd"];
            return MachindPwd.ToLower() == "machine123";
        }
        public string UploadToAzure(IFormFile htp, string strID, string strModuleID, string strFormIDParam,
                                    ContentType contentType, string strFileName, string strFileExt, bool NameAsIs, string Compcode)
        {
            string Result = "0";
            int FileType = 0;
            string strFilename = string.Empty;
            try
            {
                if (NameAsIs)
                    strFilename = strFileName.ToLower() + strFileExt;
                else
                    strFilename = strFileName.ToLower() + "_" + "1" + "_" + strID.ToLower() + strFileExt;

                FileType = contentType == ContentType.Video ? 2 : contentType == ContentType.Image ? 1 : 3;
                string strPathRetrieval = GetPath(Compcode.ToString(), strModuleID.ToLower(), strFormIDParam.ToLower(), FileType, false);
                if (contentType == ContentType.Document || contentType == ContentType.Image)
                {
                    var connectionString = "<YourAzureStorageConnectionString>";
                    var containerName = "<YourContainerName>";
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    var blobClient = blobContainerClient.GetBlobClient(strPathRetrieval + strFilename);
                    blobClient.Upload(htp.OpenReadStream(), true);
                    Result = strFilename;
                }
            }
            catch (Exception ex)
            {
                Result = "0";
            }
            return Result;
        }
        public string GetPath(string companyID, string ModuleName, string FormID, int Type, bool Read)
        {
            string filpath = "";
            string returnPath = "";
            string strCloudURL = _configuration["CorsSettings:blobUrlSAS"].ToString().ToLower();

            if (_configuration["CorsSettings:IsCloudDeployment"].ToString().ToLower() == "yes")
            {
                switch (Type)
                {
                    case 1:
                        returnPath = !Read
                            ? $"attachment/{companyID}/{ModuleName}/{FormID}/{_configuration["CorsSettings:ImagesToCloud"].ToLower()}"
                            : $"{strCloudURL}attachment/{companyID}/{ModuleName}/{FormID}/{_configuration["CorsSettings:ImagesToCloud"].ToLower()}";
                        break;
                    case 2:
                        returnPath = !Read
                            ? $"attachment/{companyID}/{ModuleName}/{FormID}/{_configuration["CorsSettings:videosToCloud"].ToLower()}"
                            : $"{strCloudURL}attachment/{companyID}/{ModuleName}/{FormID}/{_configuration["CorsSettings:videosToCloud"].ToLower()}";
                        break;
                    case 3:
                        returnPath = !Read
                            ? $"attachment/{companyID}/{ModuleName}/{FormID}/{_configuration["CorsSettings:DocumentsToCloud"].ToLower()}"
                            : $"{strCloudURL}attachment/{companyID}/{ModuleName}/{FormID}/{_configuration["CorsSettings:DocumentsToCloud"].ToLower()}";
                        break;
                }
            }
            else
            {
                switch (Type)
                {
                    case 1:
                        filpath = _configuration["CorsSettings:ImagesFromServer"]; // backSlash included in this

                        //Commented Code by Areeb 29092024 Shoaib Code Commented.
                        //var imagesDirectory = Path.Combine(_configuration["CorsSettings:AttachmentsPath"], companyID, ModuleName, FormID, filpath);
                        var imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filpath);
                        if (!Directory.Exists(imagesDirectory))
                            Directory.CreateDirectory(imagesDirectory);
                        returnPath = imagesDirectory;
                        break;
                    case 2:
                        filpath = _configuration["CorsSettings:VideosFromServer"]; // backSlash included in this

                        //Commented Code by Areeb 29092024 Shoaib Code Commented.
                        //var videosDirectory = Path.Combine(_configuration["CorsSettings:AttachmentsPath"], companyID, ModuleName, FormID, filpath);
                        var videosDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filpath);
                        if (!Directory.Exists(videosDirectory))
                            Directory.CreateDirectory(videosDirectory);
                        returnPath = videosDirectory;
                        break;
                    case 3:
                        filpath = _configuration["CorsSettings:DocumentsFomServer"]; // backSlash included in this

                        //Commented Code by Areeb 29092024 Shoaib Code Commented.
                        //var documentsDirectory = Path.Combine(_configuration["CorsSettings:AttachmentsPath"], companyID, ModuleName, FormID, filpath);
                        var documentsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filpath);
                        if (!Directory.Exists(documentsDirectory))
                            Directory.CreateDirectory(documentsDirectory);
                        returnPath = documentsDirectory;
                        break;
                }
            }
            return returnPath;
        }
        public string UploadtoPremisis(IFormFile htp, string strID, string strModuleID, string strFormIDParam,
                                        ContentType contentType,
                                        string strFileName, string strFileExt, bool NameAsIs, string Compcode)
        {
            string Result = "0";
            int FileType = 0;
            string strFilename = string.Empty;
            try
            {
                if (NameAsIs)
                    strFilename = strFileName.ToLower() + strFileExt;
                else
                    strFilename = strFileName.ToLower() + "_" + Guid.NewGuid() + strFileExt;

                FileType = contentType == ContentType.Video ? 2 : contentType == ContentType.Image ? 1 : 3;
                string strPathRetrieval = GetPath(Compcode, strModuleID.ToLower(), strFormIDParam.ToLower(), FileType, false);

                if (contentType == ContentType.Image || contentType == ContentType.Document)
                {
                    using (FileStream fs = new FileStream(Path.Combine(strPathRetrieval, strFilename),
                                                            FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        htp.CopyTo(fs);
                    }
                    Result = strPathRetrieval + strFilename;
                }
                else if (contentType == ContentType.Video)
                {
                    if (NameAsIs)
                        strFilename = strFileName.ToLower();
                    else
                        strFilename = strFileName.ToLower() + "_1_" + strID.ToLower();

                    if (!strFileExt.Equals(".mp4"))
                    {
                        var convert = new NReco.VideoConverter.FFMpegConverter();
                        var convertSettings = new NReco.VideoConverter.ConvertSettings();
                        convertSettings.SetVideoFrameSize(640, 380);
                        convertSettings.VideoCodec = "libx264";
                        convertSettings.CustomOutputArgs = "-preset ultrafast -crf 50 -x264-params 'nal-hrd=cbr' -b:v 2M -minrate 2M -maxrate 2M -bufsize 4M -b:a 128k -movflags +faststart";

                        using (FileStream fs = new FileStream(Path.Combine(strPathRetrieval, strFilename + ".mp4"),
                                                                FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            convert.ConvertLiveMedia(htp.OpenReadStream(), strFileExt.Substring(1), fs, NReco.VideoConverter.Format.mp4, convertSettings);
                        }
                        Result = strPathRetrieval + strFilename + ".mp4";
                    }
                    else
                    {
                        using (FileStream fs = new FileStream(Path.Combine(strPathRetrieval, strFilename + ".mp4"),
                                                                FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            htp.CopyTo(fs);
                        }
                        Result = strPathRetrieval + strFilename + ".mp4";
                    }
                }
            }
            catch (Exception ex)
            {
                Result = "0";
            }
            return Result;
        }

        public DataTable GetReportFormat(string CompanyId)
        {
            DataTable objDataTable = new DataTable();
            DataSet objDataSet = new DataSet();
            string query = "SELECT ISNULL(HR_Month, 0) AS HRMonth, ISNULL(HR_Day, 0) AS HRDay, ISNULL(HR_Year, 0) AS HRYear, " +
                           "ISNULL(HR_Decimals, 0) AS HRDecimals, ISNULL(HR_Rounding, 0) AS HRRounding, ISNULL(HR_Separator, '/') AS HRSeparator " +
                           $"FROM tblHRPolicy WHERE CompanyId = {CompanyId}";

            string r = _dataservice.ExecuteReader(query, ref objDataSet);
            if (r.Equals("successfull") && objDataSet.Tables.Count > 0 && objDataSet.Tables[0].Rows.Count > 0)
            {
                return objDataSet.Tables[0];
            }
            else
            {
                objDataTable.Columns.Add("HRMonth", typeof(string));
                objDataTable.Columns.Add("HRDay", typeof(string));
                objDataTable.Columns.Add("HRYear", typeof(string));
                objDataTable.Columns.Add("HRDecimals", typeof(string));
                objDataTable.Columns.Add("HRRounding", typeof(string));
                objDataTable.Columns.Add("HRSeparator", typeof(string));

                objDataTable.Rows.Add("0", "0", "0", "0", "0", "/");
                objDataSet.Tables.Add(objDataTable);
            }

            return objDataSet.Tables[0];
        }

        //[HttpPost("SaveAttachmentListSPO")]
        //public IActionResult SaveAttachmentListSPO([FromBody] string AttachmentFileName)
        //{
        //    APIResponse response = new APIResponse();
        //    try
        //    {
        //        string result = "";
        //        if (IncentivePolicyManagement.Insert(_configuration.GetConnectionString("ConnectionString").ToString(), AttachmentFileName, out result))
        //        {
        //            response.IsValid = true;
        //            response.Message = result;
        //        }
        //        else
        //        {
        //            response.IsValid = false;
        //            response.Message = result;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(StatusCodes.Status500InternalServerError, "Error");
        //    }

        //    return Json(response);
        //}


        //[HttpGet("ValidateFile")]
        //public IActionResult ValidateFile(string FileName, int FileSize)
        //{
        //    string Response = string.Empty;
        //    if (!string.IsNullOrEmpty(FileName))
        //    {
        //        List<ValidationStatus> ObjLiValidationStatus;
        //        String fileExtension = Path.GetExtension(FileName).ToLower();
        //        String[] allowedExtensions = { ".doc", ".docx", ".pdf", ".xlsx", ".xls", ".txt", ".jpg", ".png", ".ppt", ".pptx", ".jpeg" };
        //        bool IsValidExtension = false;

        //        for (int i = 0; i < allowedExtensions.Length; i++)
        //        {
        //            if (fileExtension == allowedExtensions[i])
        //                IsValidExtension = true;
        //        }

        //        if (IsValidExtension)
        //        {
        //            if (FileSize > 5242880)
        //            {

        //                Response = "File size exceeded the limit of 5MB";
        //                return Ok(Response);
        //            }
        //            else
        //            {
        //                Response = "Success";
        //                return Ok(Response);
        //            }
        //        }
        //        else
        //        {
        //            Response = "Only documents of the following extensions can be attached: .jpg, .docx, .doc, .pdf, .ppt, .pptx, .xlsx, .xls, .png, .txt.";
        //            return Ok(Response);
        //        }
        //    }
        //    else
        //    {
        //        Response = "File Not Found.";
        //        return StatusCode((int)HttpStatusCode.PreconditionFailed, Response);
        //    }
        //}

        //[HttpPost("UploadFile")]
        //public async Task<string> UploadFile()
        //{
        //    try
        //    {
        //        var httpRequest = HttpContext.Request;
        //        var formCollection = await httpRequest.ReadFormAsync();
        //        var files = formCollection.Files;
        //        var postedFile = files.FirstOrDefault();
        //        Utilities Utilities = new Utilities(_configuration);

        //        if (postedFile != null && postedFile.Length > 0)
        //        {
        //            string UniqueFileName = Utilities.UploadtoDrive(postedFile, "PharmaCrm", "PharmaCrm", Constants.IncentivePolicyManagementSetups, false);
        //            return UniqueFileName;
        //        }
        //        else
        //        {
        //            return "No file uploaded";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return "Failed";
        //    }
        //}





        public byte[] GetCompanyLogo(string CompanyId)
        {
            try
            {
                DataSet dataset = new DataSet();
                string strQuery = "Select Logo From EXTERNAL_SECURITY_COMPANYMANAGEMENT Where CCode = '" + CompanyId.Trim().PadLeft(3, '0') + "'";
                string strTemp = _dataservice.ExecuteReader(strQuery, ref dataset);
                if (dataset != null && dataset.Tables[0] != null && dataset.Tables[0].Rows.Count > 0)
                    return (byte[])(dataset.Tables[0].Rows[0][0]);
                else
                    return null;
            }
            catch
            {
                return null;

            }

        }

        public void Initialize(IConfiguration configuration)
        {
            /*
            var redisService = new RedisService(configuration);
            // Pass the RedisDatabase instance to Utilities
            var utilities = new Utilities(configuration);
            var ConnectionString = utilities._configuration.GetConnectionString("ConnectionString");
            var auditTrailConnectionString = utilities._configuration.GetConnectionString("vCurioAuditTrailConnectionString");

            dataService.BeginProcess(ConnectionString);
            // dataServiceAuditTrail.BeginProcess(auditTrailConnectionString);
            */
        }

        public DataTable GetDataTable(string strQuery)
        {
            DataSet dataset = new DataSet();
            string result = _dataservice.GetDataWithClauseDS(strQuery, ref dataset);
            if (dataset == null || dataset.Tables.Count == 0)
                return null;
            return dataset.Tables[0];
        }
        public DataTable GetDataTableSecurity(string strQuery)
        {
            DataSet dataset = new DataSet();
            string result = _dataservice.GetDataWithClauseSecurityDS(strQuery, ref dataset);
            if (dataset == null || dataset.Tables.Count == 0)
                return null;
            return dataset.Tables[0];
        }

        public string GetTerminalId()
        {
            string returnVal = string.Empty;

            var httpContextAccessor = ServiceLocator.GetService<IHttpContextAccessor>();
            var httpContext = httpContextAccessor?.HttpContext;

            if (httpContext != null)
            {
                try
                {
                    IPHostEntry obj = Dns.GetHostEntry(httpContext.Connection.RemoteIpAddress);
                    returnVal = obj.HostName;
                }
                catch (System.Net.Sockets.SocketException se)
                {
                    returnVal = httpContext.Connection.RemoteIpAddress.ToString();
                }
            }
            else
            {
                returnVal = "N/A";
            }

            return returnVal;
        }

        public string GetTerminalIP()
        {

            string returnVal = string.Empty;

            var httpContextAccessor = ServiceLocator.GetService<IHttpContextAccessor>();
            var httpContext = httpContextAccessor?.HttpContext;

            if (httpContext != null)
            {
                try
                {
                    return httpContext.Connection.RemoteIpAddress.ToString();
                }
                catch (System.Net.Sockets.SocketException)
                {
                    // Handle the exception if needed
                    return string.Empty; // or any default value
                }
            }
            else
            {
                returnVal = "N/A";
            }

            return returnVal;
        }

        public string GetEmployeeId(HttpContext context, string UserID)
        {
            string _EmpId = string.Empty;

            if (context.Request.Headers.TryGetValue("login", out var loginValues))
            {
                string prefix = GetPrefix(loginValues.FirstOrDefault());
                string loginDetail = GetKeyInRedis(prefix + "OtherData", _configuration);
                DataTable dtloginDetail = new DataTable();

                if (!string.IsNullOrEmpty(loginDetail))
                {
                    dtloginDetail = (DataTable)JsonConvert.DeserializeObject(loginDetail, typeof(DataTable));

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        _EmpId = drloginDetail["EmpId"].ToString();
                    }
                }
                else
                {
                    _EmpId = GetScalarData($"SELECT EmpId FROM EXTERNAL_SECURITY_USERMAPPING WHERE UserId='{UserID}'").ToString();
                }
            }

            return _EmpId;
        }


        public string GetMasterLabelByCode(string Code, string Culture)
        {
            string result = "";

            try
            {
                string query = $"Select LabelDescription from tblLabelMaster where Code = '{Code}' and Culture = '{Culture}'";
                string data = GetScalarData(query)?.ToString();

                if (!string.IsNullOrEmpty(data))
                {
                    result = data;
                }
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }

            return result;
        }

        public string NullHandleObject(object obj)
        {
            string result = string.Empty;

            if (obj != null && obj.ToString().Trim().Length > 0)
            {
                result = obj.ToString().Trim();
            }

            return result;
        }


        public async Task<string> GetUserRightsInRedis(string key)
        {
            RedisService redisService = new RedisService(_configuration);
            string result = await redisService.GetUserRightsInRedis(key);

            // Add a return statement here or modify your logic accordingly
            return result;
        }



        public string GetPrefix(string _ClientIP)
        {
            string _Prefix = String.Empty;
            Object obj = new object();
            obj = GetScalarDataForSecurity("SELECT top 1 UniqueKey FROM tblUniqueKeyForRedis Where EntTerminal='" + _ClientIP + "' order by  Id desc");
            if (obj != null)
            {
                _Prefix = obj.ToString();
            }
            return _Prefix;
        }


        public int IsTrainingVideoExists(IConfiguration configuration, string FormId)
        {
            int TotalCount = 0;
            object objCount = null;

            try
            {
                string connectionString = configuration.GetConnectionString("KnowledgebaseConnectionString");


                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = "SELECT COUNT(*) AS VideoCount FROM TblTrainingVideos WHERE FormID = @FormId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FormId", FormId);

                        object result = command.ExecuteScalar();

                        if (result != null)
                        {
                            objCount = result;
                            TotalCount = Convert.ToInt32(objCount);
                        }
                    }
                }

                //using (SqlConnection connectionHCMS = new SqlConnection(connectionString))
                //{
                //    connectionHCMS.Open();
                //    // If you have additional logic for the HCMS connection, add it here.
                //}
            }
            catch (Exception ex)
            {
                // Handle the exception or log it as needed.
                TotalCount = 0;
            }

            return TotalCount;
        }

        //public async Task<string> GetUserRightsInRedis(string key)
        //{
        //    string value = "";
        //    try
        //    {
        //        var redisDatabase = _connectionMultiplexer.Value;
        //        value = await redisDatabase.GetDatabase().StringGetAsync(key);
        //    }
        //    catch (Exception ex)
        //    {
        //        string exceptionMessage = ex.Message;
        //        // Handle the exception as needed
        //    }
        //    return value;
        //}



        //public async Task<string> GetUserRightsInRedis(string key)
        //{
        //    string value = "";
        //    try
        //    {
        //        if (_redisDatabase == null)
        //        {
        //            // Initialize the RedisService instance here if not initialized already
        //            var redisService = new RedisService(_configuration);
        //            _redisDatabase = redisService.RedisDatabase;
        //        }

        //        //IDatabase db = _connection.GetDatabase();
        //        //value = await db.StringGetAsync(key);

        //        // Await the result here
        //        var redisValue = await _redisDatabase.StringGetAsync(key);

        //        // Convert RedisValue to string
        //        value = redisValue.ToString();
        //    }
        //    catch (Exception ex)
        //    {
        //        string errorMessage = ex.Message;
        //        // Handle the exception as needed
        //    }
        //    return value;
        //}


        public string GetUserRightsInRedisWithoutAsync(string Key)
        {

            string _value = "";
            try
            {

                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                _value = _redisDatabase.StringGet(Key);

            }
            catch (Exception ex)
            {
                string exx = ex.Message;
            }
            return _value;

        }
        //public string GetCompanyId()
        //{
        //    string _CompanyId = string.Empty;

        //    try
        //    {
        //        var _clientIP =Request.Headers["login"];
        //        string prefix = Utilities.GetPrefix(_clientIP);

        //        if (prefix != null)
        //        {
        //            string _loginDetail = Utilities.GetKeyInRedis(prefix + "DecryptLoginDetail");
        //            DataTable dtloginDetail = JsonConvert.DeserializeObject<DataTable>(_loginDetail);

        //            if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
        //            {
        //                DataRow drloginDetail = dtloginDetail.Rows[0];
        //                _CompanyId = drloginDetail["CompanyId"].ToString();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle the exception if needed
        //    }

        //    return _CompanyId;
        //}

        public String GetApplicationId()
        {
            return _configuration.GetSection("CorsSettings:ApplicationId").Value;

            //return "PharmaCRMv2";
        }


        public string GetAppCurrentUICulture(string _clientIP)
        {
            try
            {
                string AppUICulture = string.Empty;

                // var _clientIP = HttpContext.Current.Request.Headers["login"];
                string prefix = GetPrefix(_clientIP);

                string _loginDetail = GetKeyInRedis(prefix + "OtherData", _configuration);
                DataTable dtloginDetail = new DataTable();

                if (!String.IsNullOrEmpty(_loginDetail))
                {
                    dtloginDetail = JsonConvert.DeserializeObject<DataTable>(_loginDetail);

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        AppUICulture = drloginDetail["Culture"].ToString();
                    }
                }

                return string.IsNullOrEmpty(AppUICulture) ? "en-GB" : AppUICulture;
            }
            catch (Exception ex)
            {
                return "en-GB";
            }
        }


        public UserInfo GetCurrentUserMap(String _clientIP)
        {
            UserInfo objuser = new UserInfo();
            //var _clientIP = HttpContext.Request.Headers["login"];
            string prefix = string.Empty;

            try
            {
                prefix = GetPrefix(_clientIP);
            }
            catch
            {
                prefix = "x";
            }

            string _loginDetail = GetKeyInRedis(prefix + "OtherData", _configuration);
            DataTable dtloginDetail = new DataTable();

            if (!String.IsNullOrEmpty(_loginDetail))
            {
                dtloginDetail = JsonConvert.DeserializeObject<DataTable>(_loginDetail);

                if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                {
                    DataRow drloginDetail = dtloginDetail.Rows[0];
                    objuser.UserID = drloginDetail["UserId"].ToString();
                    objuser.UserEmpId = Convert.ToInt32(drloginDetail["EmpId"].ToString());

                    DataSet dataset = new DataSet();
                    _dataservice.ExecuteReader("select Empcode, rtrim(ltrim(firstName + ' ' + isnull(midName,''))) + ' ' + lastName as Name  from tblEmployee  where EmpId ='" + objuser.UserEmpId + "'", ref dataset);

                    if (dataset != null && dataset.Tables.Count > 0 && dataset.Tables[0].Rows.Count > 0)
                    {
                        DataRow drEmp = dataset.Tables[0].Rows[0];

                        if (drEmp["Name"] != System.DBNull.Value)
                            objuser.UserEmpName = drEmp["Name"].ToString();

                        if (drEmp["Empcode"] != System.DBNull.Value)
                            objuser.UserEmpCode = drEmp["Empcode"].ToString();
                    }
                }
            }

            return objuser;
        }


        public string GetCompanyId(string clientIP)
        {
            string companyId = string.Empty;

            try
            {
                //   var clientIP = _httpContextAccessor.HttpContext.Request.Headers["login"].FirstOrDefault();

                string prefix = GetPrefix(clientIP);

                //string prefix = clientIP;


                if (!string.IsNullOrEmpty(prefix))

                {

                    string loginDetail = GetKeyInRedis(prefix + "DecryptLoginDetail", _configuration);
                    DataTable dtLoginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                    if (dtLoginDetail != null && dtLoginDetail.Rows.Count != 0)
                    {
                        DataRow drLoginDetail = dtLoginDetail.Rows[0];
                        companyId = drLoginDetail["CompanyId"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }

            return companyId;
        }

        public string GetCompanyName(string clientIP)
        {
            string companyName = string.Empty;

            try
            {
                //   var clientIP = _httpContextAccessor.HttpContext.Request.Headers["login"].FirstOrDefault();

                string prefix = GetPrefix(clientIP);

                //string prefix = clientIP;


                if (!string.IsNullOrEmpty(prefix))

                {

                    string loginDetail = GetKeyInRedis(prefix + "DecryptLoginDetail", _configuration);
                    DataTable dtLoginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                    if (dtLoginDetail != null && dtLoginDetail.Rows.Count != 0)
                    {
                        DataRow drLoginDetail = dtLoginDetail.Rows[0];
                        companyName = drLoginDetail["CompanyName"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception if needed
            }

            return companyName;
        }




        public object GetScalarData(string colName, string tableName, string whereClause)
        {
            object obj = null;
            string result = _dataservice.ExecuteStatement("select " + colName + " from  " + tableName + " where " + whereClause, ref obj, 1);
            if (obj == null)
                return "";
            return obj;
        }
        public object GetScalarSecurityData(string strQuery)
        {
            object obj = null;
            string result = _dataservice.ExecuteSecurityStatement(strQuery, ref obj, 1);
            if (obj == null)
                return "";
            return obj;
        }
        public object GetScalarData(string strQuery)
        {
            object obj = null;
            string result = _dataservice.ExecuteStatement(strQuery, ref obj, 1);
            if (obj == null)
                return "";
            return obj;
        }
        public string GetUserEmpName(string clientIP)
        {
            string EmpName = string.Empty;
            string prefix = clientIP;
            try
            {
                //var clientIP = HttpContext.Request.Headers["login"].FirstOrDefault();
                //string prefix = Utilities.GetPrefix(clientIP);

                string loginDetail = GetKeyInRedis(prefix + "OtherData", _configuration);
                DataTable dtloginDetail = new DataTable();

                if (!string.IsNullOrEmpty(loginDetail))
                {
                    dtloginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        EmpName = drloginDetail["EmpName"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }

            return EmpName;
        }

        public string GetUserRole(string clientIP)
        {
            string UserRole = string.Empty;
            string prefix = clientIP;
            try
            {
                //var clientIP = HttpContext.Request.Headers["login"].FirstOrDefault();
                //string prefix = Utilities.GetPrefix(clientIP);

                string loginDetail = GetKeyInRedis(prefix + "OtherData", _configuration);
                DataTable dtloginDetail = new DataTable();

                if (!string.IsNullOrEmpty(loginDetail))
                {
                    dtloginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        UserRole = drloginDetail["UserRole"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }

            return UserRole;
        }

        public string UserIdCode(string clientIP)
        {
            string UserIdCode = string.Empty;
            string prefix = clientIP;
            try
            {
                //var clientIP = HttpContext.Request.Headers["login"].FirstOrDefault();
                //string prefix = Utilities.GetPrefix(clientIP);

                string loginDetail = GetKeyInRedis(prefix + "DecryptLoginDetail", _configuration);
                DataTable dtloginDetail = new DataTable();

                if (!string.IsNullOrEmpty(loginDetail))
                {
                    dtloginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        UserIdCode = drloginDetail["UserIdCode"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }

            return UserIdCode;
        }
        public string GetUserid(string clientIP)
        {
            string userId = string.Empty;
            string prefix = clientIP;
            try
            {
                //var clientIP = HttpContext.Request.Headers["login"].FirstOrDefault();
                //string prefix = Utilities.GetPrefix(clientIP);

                string loginDetail = GetKeyInRedis(prefix + "DecryptLoginDetail", _configuration);
                DataTable dtloginDetail = new DataTable();

                if (!string.IsNullOrEmpty(loginDetail))
                {
                    dtloginDetail = JsonConvert.DeserializeObject<DataTable>(loginDetail);

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        userId = drloginDetail["UserId"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }

            return userId;
        }
        public string GetUserRightInRedis(string Key)
        {
            string _value = "";
            try
            {

                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                _value = _redisDatabase.StringGet(Key);

            }
            catch (Exception ex)
            {
                string exx = ex.Message;
            }
            return _value;
        }



        public void SetKeyInRedis(string key, string value)
        {
            try
            {
                string timeout = "2";
                if (_configuration["SessionTimeOut"] != null)
                {
                    timeout = _configuration["SessionTimeOut"];
                }

                TimeSpan timeSpan = TimeSpan.FromDays(Convert.ToDouble(timeout));

                // Use the provided IDatabase instance
                _redisDatabase.StringSet(key, value, timeSpan);
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }
        public async Task SetKeyInRedisAsync(string key, string value, string prefix, string KeepMeSignIn)
        {

            try
            {
                string timeout = "2";
                string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
                if (_configuration["SessionTimeOut"] != null)
                {
                    timeout = _configuration["SessionTimeOut"];
                }
                if (!String.IsNullOrEmpty(KeepMeSignIn) && KeepMeSignIn.ToLower() == "true")
                {
                    timeout = "365";
                }


                TimeSpan timeSpan = TimeSpan.FromDays(1); // Set time 1 day

                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                if (key == "LoginDetail" || key == "DecryptLoginDetail" || key == "IsLogin")
                {
                    await _redisDatabase.StringSetAsync(prefix + key, value, timeSpan, When.Always, CommandFlags.FireAndForget);
                }
                else
                {
                    await _redisDatabase.StringSetAsync($"{prefix}{key}{applicationId}", value, timeSpan, When.Always, CommandFlags.FireAndForget);

                    // await _redisDatabase.StringSetAsync(prefix + key + "PharmaCRMv2", value, timeSpan, When.Always, CommandFlags.FireAndForget);
                }
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }

        public string GetTotalChunksInRedis(string Key, IConfiguration configuration)
        {
            string _value = "";
            try
            {
                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                _value = _redisDatabase.StringGet(Key);


            }
            catch (Exception ex)
            {
                string exx = ex.Message;
                // Handle the exception as needed
            }
            return _value;
        }

        public void RemoveKeyInRedis(string Key)
        {
            try
            {

                //IConnectionMultiplexer redisConn = ConnectionMultiplexer.Connect(Utilities.GetRedisConnection());
                //IDatabase redDb = redisConn.GetDatabase();
                //redDb.KeyDelete(Key, CommandFlags.None);
                _redisDatabase.KeyDelete(Key, CommandFlags.None);

            }
            catch (Exception ex)
            {
                string exx = ex.Message;
            }
        }

        public bool RemoveAllKeyInRedis(string pattern, IConfiguration configuration)
        {
            bool isRecordDeleted = false;
            try
            {
                var redisService = new RedisService(configuration);
                var redisDatabase = redisService.RedisDatabase;
                var connectionMultiplexer = redisService.Connection;

                string host = configuration.GetConnectionString("RedisHost").ToString();
                int port = Convert.ToInt32(configuration.GetConnectionString("RedisPort"));
                var server = connectionMultiplexer.GetServer(host, port);

                foreach (var key in server.Keys(pattern: "*" + pattern + "*").Take(100))
                {
                    redisDatabase.KeyDelete(key, CommandFlags.None);
                }

                isRecordDeleted = true;
            }
            catch (Exception ex)
            {
                string exMessage = ex.Message;
                isRecordDeleted = false;
            }
            return isRecordDeleted;
        }
        public string CheckSessionTimeOut(String key)
        {
            string result = "";

            try
            {
                //var key = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "login").Value.FirstOrDefault();


                key = GetPrefix(key);
                string userId = GetUserid(key);
                string keepMeSignIn = GetKeyInRedis(key + "KeepMeSignin", _configuration);

                if (String.IsNullOrEmpty(keepMeSignIn))
                {
                    if (String.IsNullOrEmpty(GetKeyInRedis(key + "SessionTimeOut", _configuration)))
                    {
                        result = "SessionTimeOut";
                    }
                    else
                    {
                        SetSessionTimeOut(key);
                    }
                }
                else
                {
                    SetSessionTimeOut(key);
                }

                string changePwd = GetUserRightInRedis(userId + "ChangePassword").ToString().ToLower();

                if (changePwd == "y")
                {
                    SetKeyInRedis(userId + "ChangePassword", "N");
                    result = "SessionTimeOut";
                }
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
            }

            return result;
        }






        public string GetKeyInRedis(string Key, IConfiguration configuration)
        {
            string _value = "";
            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            try
            {

                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                if (Key.Contains("LoginDetail") || Key.Contains("DecryptLoginDetail") || Key.Contains("IsLogin") || Key.Contains("KeepMeSignin"))
                {
                    _value = _redisDatabase.StringGet(Key);
                }
                else
                {
                    _value = _redisDatabase.StringGet(Key + applicationId);

                    //_value = _redisDatabase.StringGet(Key + "PharmaCRMv2");
                }



            }
            catch (Exception ex)
            {
                string exx = ex.Message;
                // Handle the exception as needed
            }
            return _value;
        }
        public async Task SetUserRightsInRedis(string key, string value, string prefix)
        {
            try
            {
                TimeSpan timeSpan = TimeSpan.FromDays(365);

                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }

                await _redisDatabase.StringSetAsync(prefix + key, value, timeSpan, When.Always, CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                string exMessage = ex.Message;
                // Handle the exception as needed
            }
        }

        public async Task<string> GetKeyInRedisAsync(string key)
        {
            string _value = "";
            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            try
            {
                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                if (key == "LoginDetail" || key == "DecryptLoginDetail" || key == "IsLogin")
                {
                    _value = await _redisDatabase.StringGetAsync(key);
                }
                else
                {
                    _value = await _redisDatabase.StringGetAsync(key + applicationId);

                    // _value=await _redisDatabase.StringGetAsync(key + "PharmaCRMv2");
                }
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
            return _value;
        }


        public async Task SetKeyInRedisAsyncs(string key, string value, string prefix)
        {
            try
            {
                string timeout = "2";
                string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
                if (_configuration["SessionTimeOut"] != null)
                {
                    timeout = _configuration["SessionTimeOut"];
                }
                if (GetKeyInRedis(prefix + "KeepMeSignin", _configuration) != null && GetKeyInRedis(prefix + "KeepMeSignin", _configuration).ToString().ToLower() == "true")
                {
                    timeout = "365";
                }





                TimeSpan timeSpan = TimeSpan.FromDays(1); // Set time 1 day

                if (_redisDatabase == null)
                {
                    // Initialize the RedisService instance here if not initialized already
                    var redisService = new RedisService(_configuration);
                    _redisDatabase = redisService.RedisDatabase;
                }
                if (key == "LoginDetail" || key == "DecryptLoginDetail" || key == "IsLogin")
                {
                    await _redisDatabase.StringSetAsync(prefix + key, value, timeSpan, When.Always, CommandFlags.FireAndForget);
                }
                else
                {
                    await _redisDatabase.StringSetAsync(prefix + key + applicationId, value, timeSpan, When.Always, CommandFlags.FireAndForget);

                    //await _redisDatabase.StringSetAsync(prefix + key + "PharmaCRMv2", value, timeSpan, When.Always, CommandFlags.FireAndForget);
                }
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }

        public async Task SetKeyInRedisAsync(string key, string value, string prefix)
        {
            try
            {
                string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
                TimeSpan timeSpan = TimeSpan.FromDays(1); // Set time 1 day

                if (key == "LoginDetail" || key == "DecryptLoginDetail" || key == "IsLogin")
                {
                    await _redisDatabase.StringSetAsync(prefix + key, value, timeSpan, When.Always, CommandFlags.FireAndForget);
                }
                else
                {
                    await _redisDatabase.StringSetAsync(prefix + key + applicationId, value, timeSpan, When.Always, CommandFlags.FireAndForget);

                    // await _redisDatabase.StringSetAsync(prefix + key + "PharmaCRMv2", value, timeSpan, When.Always, CommandFlags.FireAndForget);
                }
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }
        public void SetSessionTimeOut(string prefix)
        {
            string timeout = "2";
            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            if (_configuration["SessionTimeOut"] != null)
            {
                timeout = _configuration["SessionTimeOut"];
            }


            TimeSpan timeSpan = TimeSpan.FromDays(Convert.ToDouble(timeout));

            try
            {
                _redisDatabase.StringSet(prefix + "SessionTimeOut" + applicationId, "1", timeSpan);

                // _redisDatabase.StringSet(prefix + "SessionTimeOutPharmaCRMv2", "1", timeSpan);
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }



        public void SetSessionTimeOut(string prefix, string keepMeSignIn)
        {
            string timeout = "2";
            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            if (_configuration["SessionTimeOut"] != null)
            {
                timeout = _configuration["SessionTimeOut"];
            }
            if (!string.IsNullOrEmpty(keepMeSignIn) && keepMeSignIn.ToLower() == "true")
            {
                timeout = "365";
            }

            TimeSpan timeSpan = TimeSpan.FromDays(Convert.ToDouble(timeout));

            try
            {
                _redisDatabase.StringSet(prefix + "SessionTimeOut" + applicationId, "1", timeSpan);

                //_redisDatabase.StringSet(prefix + "SessionTimeOutPharmaCRMv2", "1", timeSpan);
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }


        public async Task SetSessionTimeOutAsync(string prefix, string keepMeSignIn)
        {
            string timeout = "2";
            string applicationId = _configuration.GetSection("CorsSettings:ApplicationId").Value;
            if (_configuration["SessionTimeOut"] != null)
            {
                timeout = _configuration["SessionTimeOut"];
            }
            if (!string.IsNullOrEmpty(keepMeSignIn) && keepMeSignIn.ToLower() == "true")
            {
                timeout = "365";
            }

            TimeSpan timeSpan = TimeSpan.FromDays(Convert.ToDouble(timeout));

            try
            {
                await _redisDatabase.StringSetAsync(prefix + "SessionTimeOut" + applicationId, "1", timeSpan);

                //await _redisDatabase.StringSetAsync(prefix + "SessionTimeOutPharmaCRMv2", "1", timeSpan);
            }
            catch (Exception ex)
            {
                string exceptionMessage = ex.Message;
                // Handle the exception as needed
            }
        }








        public object GetScalarDataForSecurity(string strQuery)
        {
            object obj = null;
            string result = _dataservice.ExecuteStatementForSecurity(strQuery, ref obj, 1);
            if (obj == null)
                return "";
            return obj;
        }

        public object GetScalarDataForHCMS(string strQuery)
        {
            object obj = null;
            string result = _dataservice.ExecuteStatementForHCMS(strQuery, ref obj, 1);
            if (obj == null)
                return "";
            return obj;
        }


        public async Task SendEmailAsync(string recipient, string subject, string body)
        {
            await SendEmailAsync(new List<string> { recipient }, subject, body);
        }

        public async Task SendEmailAsync(List<string> recipients, string subject, string body)
        {
            var email = new MimeMessage();
            email.Sender = MailboxAddress.Parse(_configuration.GetSection("MailSettings:Email").Value);

            foreach (var recipient in recipients)
                email.To.Add(MailboxAddress.Parse(recipient));

            email.Subject = subject;
            var builder = new BodyBuilder();
            builder.HtmlBody = body;
            email.Body = builder.ToMessageBody();

            using (var smtp = new MailKit.Net.Smtp.SmtpClient())
            {
                int.TryParse(_configuration.GetSection("MailSettings:Port").Value, out int _port);
                await smtp.ConnectAsync(_configuration.GetSection("MailSettings:Host").Value, _port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_configuration.GetSection("MailSettings:Email").Value, _configuration.GetSection("MailSettings:Password").Value);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
        }

        public enum LockControl
        {
            UnLock,
            Lock
        }

        public enum ReturnType
        {
            String,
            Int,
            Bool,
            DateTime,
            Double,
            Byte
        }

        public enum ConCurrencyConfliction
        {
            Notupdated,
            Updated,
            Deleted
        }

        public enum ValidationMessagePosition
        {
            TopLeft,
            BottomLeft,
            TopRight,
            BottomRight
        }

        public enum ValidationType
        {
            Required,
            Date
        }

        public enum JavascriptEvent
        {
            Blur,
            Click,
            Change,
            NoEvent
        }



        public void ProcessRequest(HttpContext _hCon)
        {
            // Implementation for handling requests in ASP.NET Core
        }

        public bool IsReusable => true;


        public string GetCompletePathForReading(string NameOfTheBlob, int ContentType = 0)
        {
            string DefaultImageName = "DefaultEmpImage.PNG";

            if (ContentType == 1)
            {
                BlobClient blobClient = GetBlobReference().GetBlobClient(NameOfTheBlob);

                if (blobClient.Exists())
                    return _configuration["blobUrlSAS"].ToLower() + NameOfTheBlob + GetBlobSasUri(NameOfTheBlob);
                else
                    return _configuration["blobUrlSAS"].ToLower() + DefaultImageName + GetBlobSasUri(DefaultImageName);
            }
            else
            {
                return _configuration["blobUrlSAS"].ToLower() + NameOfTheBlob + GetBlobSasUri(NameOfTheBlob);
            }
        }


        public object SaveDate(object oDate)
        {
            try
            {
                IFormatProvider culture = new CultureInfo("en-GB", true);
                DateTime objDT = Convert.ToDateTime(oDate, culture);
                return objDT.ToString("yyyy/MM/dd", culture);

            }
            catch (Exception ex)
            {
                IFormatProvider culture = new CultureInfo("en-GB", true);

                DateTime objDT = Convert.ToDateTime(DateTime.Now.ToString(), culture);
                return objDT.ToString("yyyy/MM/dd", culture);
            }
        }


        private BlobContainerClient GetBlobReference()
        {
            string connectionString = _configuration.GetConnectionString("YourStorageConnectionString");
            string containerName = "your-container-name";
            return new BlobContainerClient(connectionString, containerName);
        }

        private string GetBlobSasUri(string blobName)
        {
            return "";
            // Your implementation for getting Blob SAS URI
        }

        public string GetEmployeePicPath(string EmpId, string CompanyId)
        {
            string imagePath = "";
            try
            {
                string imagename = GetScalarData("Select PhotoPath from tblEmployee where EmpId = '" + EmpId + "'").ToString();

                if (imagename != "" && imagename.Length > 0)
                {
                    string moduleName = "Personnel";
                    string formID = "EmployeeProfileBasic";

                    //todo
                    //string path = Utilities.GetPath(CompanyId, moduleName, formID, 1, false);
                    //imagePath = Utilities.GetCompletePathForReading(path + imagename);
                }
                else
                    imagePath = "./assets/images/pro.png";
            }
            catch (Exception ex)
            {
                imagePath = "./assets/images/pro.png";
            }

            return imagePath;
        }

        #region Enums

        public enum ContentType
        {
            Image,
            Video,
            Document
        }

        #endregion




        #region FormatNames
        public class FormatNames
        {
            #region Video Formats

            public const string MP4 = "mp4";
            public const string AVI = "avi";
            public const string GP3 = "3gp";
            public const string MKV = "mkv";

            #endregion

            #region Images Formats

            public const string JPG = "jpg";
            public const string JPEG = "jpeg";
            public const string PNG = "png";
            public const string BMP = "bmp";
            public const string GIF = "gif";

            #endregion

            #region Document Formats

            public const string DOC = "doc";
            public const string DOCx = "docx";
            public const string XLS = "xls";
            public const string XlSx = "xlsx";
            public const string PPT = "ppt";
            public const string PPTx = "pptx";
            public const string PDF = "pdf";

            #endregion

            public FormatNames() { }

            public static List<string> GetAllVideoFormats()
            {
                List<string> VideoFormats = new List<string>();
                VideoFormats.AddRange(new List<string>() { MP4, AVI, GP3, MKV });
                return VideoFormats;
            }
            public static List<string> GetAllDocFormats()
            {
                List<string> DocFormats = new List<string>();
                DocFormats.AddRange(new List<string>() { DOC, DOCx, XLS, XlSx, PPT, PPTx, PDF });
                return DocFormats;
            }
            public static List<string> GetAllImgFormats()
            {
                List<string> ImgFormats = new List<string>();
                ImgFormats.AddRange(new List<string>() { JPG, JPEG, PNG, BMP, GIF });
                return ImgFormats;
            }
        }
        #endregion




        public class FileRequest
        {
            public string FileNameWithPath { get; set; }
        }

        public object HandleNull(object value, ReturnType rt)
        {
            object returnValue = null;
            if (value is DBNull)
            {
                switch (rt)
                {
                    case ReturnType.String:
                        returnValue = "";
                        break;
                    case ReturnType.Int:
                        returnValue = -1;
                        break;
                    case ReturnType.DateTime:
                        returnValue = "";
                        break;
                    case ReturnType.Double:
                        returnValue = -1;
                        break;
                    case ReturnType.Bool:
                        returnValue = false;
                        break;
                    case ReturnType.Byte:
                        returnValue = 0;
                        break;
                }
            }
            else
            {
                if (rt == ReturnType.String)
                    if (value.ToString().Length == 0)
                        returnValue = "";
                    else
                        returnValue = value;

                if (rt == ReturnType.Int)
                    returnValue = value;

                if (rt == ReturnType.DateTime)
                    returnValue = GetDate(Convert.ToDateTime(value).ToShortDateString());

                if (rt == ReturnType.Double)
                    returnValue = value;

                if (rt == ReturnType.Bool)
                    if (Convert.ToBoolean(value) == true)
                        returnValue = true;
                    else
                        returnValue = false;

                if (rt == ReturnType.Byte)
                    returnValue = value;
            }
            return returnValue;
        }

        public string GetDate(string date)
        {
            try
            {
                DateTime DT = Convert.ToDateTime(date, new System.Globalization.CultureInfo("en-GB"));
                return DT.Day.ToString() + "/" + DT.Month.ToString() + "/" + DT.Year.ToString();
            }
            catch (Exception exp)
            {
                return date;
            }
        }

        public static string SetDate(string date)
        {
            try
            {
                DateTime DT = Convert.ToDateTime(date, new System.Globalization.CultureInfo("en-GB"));
                string dt = DT.Year.ToString() + "/" + DT.Month.ToString() + "/" + DT.Day.ToString();
                return dt;
            }
            catch (Exception exp)
            {
                return date;
            }
        }

        public DataSet CheckRightsForDashboardButtonESSPORTAL(string Applicationcode, string Mode, int companyId, string userId)
        {
            DataSet dataset = new DataSet();
            StringBuilder sbr = new StringBuilder();

            sbr.Append("Exec [sp_Dashboard_Transaction_ESS]");
            sbr.Append(" @Param_LoginApplicationCode='" + Applicationcode + "',");
            sbr.Append(" @Param_Mod='" + Mode + "',");
            sbr.Append(" @Param_LoginCompanyId='" + companyId + "',");
            sbr.Append(" @Param_LoginUserId='" + userId + "'");

            string result = _dataservice.ExecuteReaderDS(Convert.ToString(sbr), ref dataset);
            if (result == "successfull")
            {
                return dataset;
            }
            else
                return null;
        }

        public object ExecuteSQLFunction(string functionwithParams)
        {
            object obj = null;
            string result = _dataservice.ExecuteStatement("select " + functionwithParams, ref obj, 1);
            return obj;
        }

        public String GetEmployeeCompanyId(string _EmpId)
        {
            String _CompanyId = String.Empty;
            _CompanyId = Convert.ToString(GetScalarData("Companyid", "tblEmployee", " EmpId = " + _EmpId));
            return _CompanyId;
        }

        public DateTime GetSysCurrentdate()
        {
            string CompanyId = GetCompanyId(_clientContextService.GetClientIP());
            object obj = new object();
            string query = "Select dbo.fn_General_GetLocalDateTimeCompanyWise(" + CompanyId + ")";
            string r = _dataservice.ExecuteStatement(query, ref obj, 1);
            if (obj != null)
                return DateTime.Parse(obj.ToString());
            else
                return DateTime.Now;

        }


        public string ReturnDBDate(DateTime dt)
        {
            string date = dt.ToShortDateString();
            date = date.Substring(6, 4) + "-" + date.Substring(3, 2) + "-" + date.Substring(0, 2);
            return date;
        }
        public string ReturnDBDateTime(DateTime dt)
        {
            string date = dt.ToString("yyyy/MM/dd HH:mm:ss");
            //date = date.Substring(6, 4) + "-" + date.Substring(3, 2) + "-" + date.Substring(0, 2);
            return date;
        }

        public bool isNumaric(object obj)
        {//Only check object can convert into integer or not
            if (obj == null)
                return false;
            int i = 0;
            try
            {
                i = Convert.ToInt32(obj);
                return true;
            }
            catch (Exception exp)
            {
                return false;
            }//return false;
        }

        public int GetEmpid(string _clientIP)
        {
            int _EmpId = 0;

            try
            {
                string prefix = GetPrefix(_clientIP);
                string _loginDetail = GetKeyInRedis(prefix + "OtherData", _configuration);
                DataTable dtloginDetail = new DataTable();
                if (!String.IsNullOrEmpty(_loginDetail))
                {
                    dtloginDetail = (DataTable)JsonConvert.DeserializeObject(_loginDetail, (typeof(DataTable)));

                    if (dtloginDetail != null && dtloginDetail.Rows.Count != 0)
                    {
                        DataRow drloginDetail = dtloginDetail.Rows[0];
                        _EmpId = Convert.ToInt32(drloginDetail["EmpId"].ToString());
                    }
                }
            }
            catch (Exception ex) { }

            return _EmpId;
        }

        public bool isLeaveRequestForSubordinate(string CompanyID, string AppCulture)
        {
            DataSet ds = new DataSet();
            bool blnReturnVal = false;

            string query = " Select TOP 1 isNull(RequestForSubordinate,0) as RequestForSubordinate From tblHRPOLICYDEtail " +
                           " WHERE Companyid = " + CompanyID + " And WFlowTypeId = (SELECT SDLID FROM vwtblsetupsdetail WHERE culture='" + AppCulture + "' and smsid = 85 and cast(Code as integer) = '001' and Companyid = " + CompanyID + ") ";
            string errorMessage = _dataservice.ExecuteReader(query, ref ds);

            if (errorMessage.Equals("successfull") && ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                blnReturnVal = Convert.ToBoolean(ds.Tables[0].Rows[0][0]);

            return blnReturnVal;
        }

        public bool MaintainPreviousYearLeaveBalances(string _EmpId, string _CompanyId)
        {
            DataSet ds = new DataSet();
            bool blnReturnVal = false;

            string query = " select ispreviouscurrent from tblleavepolicy where Companyid = " + (_EmpId.Trim().Length > 0 ? "(Select CompanyId from TblEmployee where EmpId = " + _EmpId + ")" : " " + _CompanyId);
            string errorMessage = _dataservice.ExecuteReader(query, ref ds);

            if (errorMessage.Equals("successfull") && ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                blnReturnVal = Convert.ToBoolean(ds.Tables[0].Rows[0][0]);

            return blnReturnVal;
        }

        public bool CheckDate(string dateVal)
        {
            if (dateVal.Length == 0 || dateVal == "__/__/____" || dateVal == "__-__-____")
                return false;
            if (dateVal.Length < 10)
                return false;
            try
            {
                if (Convert.ToInt32(dateVal.Substring(0, 2)) < 1 || Convert.ToInt32(dateVal.Substring(0, 2)) > 31)
                {
                    return false;
                }
                if (Convert.ToInt32(dateVal.Substring(3, 2)) < 1 || Convert.ToInt32(dateVal.Substring(3, 2)) > 12)
                {
                    return false;
                }
                if (Convert.ToInt32(dateVal.Substring(6, 4)) < 1900 || Convert.ToInt32(dateVal.Substring(6, 4)) > 2099)
                {
                    return false;
                }
                DateTime dtime = new DateTime();
                if (!DateTime.TryParse(dateVal, out dtime))
                    return false;
            }
            catch { return false; }
            return true;
        }

        public DataTable GetUserLevelsData(string EmpId, string Active, string culture)
        {
            DataTable dtReturn = null;
            DataSet dataset = new DataSet();

            try
            {
                if (Active.Trim().Equals("-1") || Active.Trim().Length == 0)
                    Active = "0,1,2";
                else if (Active.Trim().Equals("1"))
                    Active = "1,2";
                else if (Active.Trim().Equals("0"))
                    Active = "0";

                string query = "Select distinct [Level] from dbo.FN_ESS_GetSubOrdinatesList(999999,'" + EmpId + "','-1','Self','" + culture + "','" + Active + "')";

                string errorMessage = _dataservice.ExecuteReader(query, ref dataset);
                if (dataset != null && dataset.Tables.Count > 0)
                    dtReturn = dataset.Tables[0];
            }
            catch (Exception ex) { }

            return dtReturn;
        }

        public DataTable GetAppraisalPeriodsData(string CompanyId)
        {
            DataTable dtReturn = null;
            DataSet dataset = new DataSet();

            try
            {
                string query = @"Select APId
                    , FORMAT(FromDate, 'dd/MMM/yyyy') AS FromDate
                    , FORMAT(ToDate, 'dd/MMM/yyyy') AS ToDate
                    , Active, CompanyId
                    From tblAppraisalPeriod
                    where CompanyId =" + CompanyId + " Order By FromDate DESC;";

                string errorMessage = _dataservice.ExecuteReader(query, ref dataset);
                if (dataset != null && dataset.Tables.Count > 0)
                    dtReturn = dataset.Tables[0];
            }
            catch (Exception ex) { }

            return dtReturn;
        }

        public DataTable GetSelectedEmployeePeriodsData(int empId)
        {
            DataTable dtReturn = null;
            DataSet dataset = new DataSet();

            try
            {
                string query = @"select * from fn_PerformanceJournal_GetEmployeeWisePeriod(" + empId + ")";

                string errorMessage = _dataservice.ExecuteReader(query, ref dataset);
                if (dataset != null && dataset.Tables.Count > 0)
                    dtReturn = dataset.Tables[0];
            }
            catch (Exception ex) { }

            return dtReturn;
        }

        public async Task<byte[]?> GetEmployeeImageAsync(int empId, string companyId)
        {
            string query = @"
                                SELECT EmpPic 
                                FROM EXTERNAL_HCMS_FILES_TBLEMPLOYEE_IMAGECV 
                                WHERE EmpId = @EmpId AND CompanyId = @CompanyId";

            var parameters = new DynamicParameters();
            parameters.Add("@EmpId", empId);
            parameters.Add("@CompanyId", companyId);

            var result = await _dapperService.QuerySingleAsync<byte[]>(query, parameters);
            return result;
        }

        public string GetEmpCode(string EmpId)
        {
            object objCode = null;
            string EmpCode = "";
            try
            {
                string query = " Select EmpCode from tblEmployee where EmpId = '" + EmpId + "'";
                string result = _dataservice.ExecuteStatement(query, ref objCode, 1);
                if (objCode != null)
                    EmpCode = objCode.ToString();
            }
            catch (Exception ex) { }

            return EmpCode;
        }

        public string GetEmpCodeForHCMS(string EmpId)
        {
            string empcode = "";
            try
            {  
                Object obj = new object();
                obj = GetScalarDataForHCMS("Select empcode from tblEmployee where EmpId = '" + EmpId + "'");
                if (obj != null)
                {
                    empcode = obj.ToString();
                }
                return empcode;
            }
            catch (Exception ex)
            {
                empcode = "";
            }

            return empcode;
        }

        

        public DataSet GetSubordinates(string EmpId, string CompanyId, string Culture)
        {
            DataSet dsRecursive = new DataSet();
            DataTable table1 = new DataTable();
            table1.Columns.Clear();
            DataColumn col1 = new DataColumn("EmpId", System.Type.GetType("System.Int32"));
            DataColumn col2 = new DataColumn("EmpName", System.Type.GetType("System.String"));
            DataColumn col3 = new DataColumn("DesigId", System.Type.GetType("System.Int32"));
            table1.Columns.Add(col1);
            table1.Columns.Add(col2);
            table1.Columns.Add(col3);
            dsRecursive.Tables.Add(table1);

            //InitializeDataSet();
            dsRecursive.Tables[0].Clear();
            DataSet dsTemp = new DataSet();
            dsTemp = GetHeirarchy(Convert.ToInt32(EmpId), dsRecursive, CompanyId, Culture);
            DataRow dRow = dsRecursive.Tables[0].NewRow();
            dRow["EmpId"] = Convert.ToInt32(EmpId);
            dRow["EmpName"] = GetScalarData("Name", "dbo.fn_Employee('" + Culture + "', " + CompanyId + ")", "EmpId=" + EmpId);
            dRow["DesigId"] = Convert.ToInt32(GetScalarData("dsgId", "dbo.fn_Employee('" + Culture + "', " + CompanyId + ")", "EmpId=" + EmpId));
            dsRecursive.Tables[0].Rows.InsertAt(dRow, 0);
            return dsRecursive;
        }

        public DataSet GetHeirarchy(int EmpId, DataSet dsRecur, string CompanyId, string Culture)
        {
            DataSet dataset = new DataSet();
            string errorMessage = _dataservice.GetDataWithClause("EmpId, Name, dsgId", "dbo.fn_Employee('" + Culture + "', " + CompanyId + ")", "ReportTo=" + EmpId, ref dataset);
            foreach (DataRow dRow in dataset.Tables[0].Rows)
            {
                DataRow dRowTemp = dsRecur.Tables[0].NewRow();
                dRowTemp["EmpId"] = dRow["EmpId"] == null ? 0 : Convert.ToInt32(dRow["EmpId"]);
                dRowTemp["EmpName"] = dRow["Name"] == null ? "" : Convert.ToString(dRow["Name"]);
                dRowTemp["DesigId"] = dRow["dsgId"] == null ? 0 : Convert.ToInt32(dRow["dsgId"]);
                dsRecur.Tables[0].Rows.Add(dRowTemp);
                if (CheckSubOrdinate(Convert.ToInt32(dRow["EmpId"].ToString()), CompanyId, Culture))
                    GetHeirarchy(Convert.ToInt32(dRow["EmpId"].ToString()), dsRecur, CompanyId, Culture);
            }
            return dsRecur;
        }

        public bool CheckSubOrdinate(int EmpId, string CompanyId, string Culture)
        {
            int count = Convert.ToInt32(GetScalarData("Count(*)", "dbo.fn_Employee('" + Culture + "', " + CompanyId + ")", "ReportTo=" + EmpId));
            if (count > 0)
                return true;
            else
                return false;
        }

        public DataTable GetEmployeeStatus(string Culture)
        {
            DataTable dt = new DataTable();
            dt = GetEmployeeStatus(false, Culture);

            if (dt != null && dt.Rows.Count > 0)
                return dt;
            else
                return null;
        }

        public DataTable GetEmployeeStatus(bool _All_Item_Required, string Culture)
        {
            DataSet dataset = new DataSet();
            string strQuery = string.Empty;
            string strCurrentCulture = Culture;
            DataTable dt = new DataTable();

            strQuery = "Select ID, Name from dbo.fn_EmployeeStatuses('" + strCurrentCulture.Trim() + "') Order by Name";
            string r = _dataservice.ExecuteReader(strQuery, ref dataset);

            if (dataset != null && dataset.Tables.Count > 0)
            {
                dt = dataset.Tables[0];
                if (_All_Item_Required)
                {
                    DataRow drow = dt.NewRow();
                    drow["ID"] = -1;
                    drow["Name"] = Get_All_AsPerCulture(strCurrentCulture);
                    dt.Rows.InsertAt(drow, 0);
                }
            }
            return dt;
        }

        public string Get_All_AsPerCulture(string Culture)
        {
            string strCurrentCulture = Culture;
            string strReturnVal = string.Empty;

            DataSet ds = new DataSet();
            string strQuery = "Select  '" + (strCurrentCulture.Trim().Equals("en-GB") ? "Item_In_English" : "Arabic_Translation") + "' as Name from Application_ArabicTranslation where Item_In_English like 'All'";
            string str = _dataservice.GetDataSet(strQuery, ref ds);

            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0] != null && ds.Tables[0].Rows.Count > 0)
                strReturnVal = Convert.ToString(ds.Tables[0].Rows[0][0]);
            else
                strReturnVal = "All";

            return strReturnVal;
        }

        public string GetEmployeeName(string _EmpId, string CompanyId, string Culture)
        {
            DataSet objDataSet = new DataSet();
            //string query = "SELECT Empcode+' - '+Name as EmpName " + // removed by Sandesh due to replication of Employee Code
            //string query = "SELECT NameWithShortCompany as EmpName " +
            //               "FROM dbo.fn_Employee('" + Utilities.GetAppCurrentUICulture() + "',-1) " +
            //               "WHERE Active = 1 And EmpId = " + _EmpId;

            string query = "  Select dbo.fn_General_GetEmployeeName(" + _EmpId + ", " + CompanyId + ", 1, 1, 1, '" + Culture + "') as EmpName ";

            string r = _dataservice.ExecuteReader(query, ref objDataSet);
            if (r.Equals("successfull") && objDataSet.Tables.Count > 0 && objDataSet.Tables[0].Rows.Count > 0)
                return objDataSet.Tables[0].Rows[0]["EmpName"].ToString();
            else
                return "";
        }

        public async Task<int?> GetEmployeeCurrencyIdAsync(string empId, string companyId)
        {
            const string sql = @"
        SELECT CurrencyId
        FROM tblempSalarysetup
        WHERE EmpId = @EmpId AND CompanyId = @CompanyId;";

            var p = new DynamicParameters();
            p.Add("@EmpId", empId);
            p.Add("@CompanyId", companyId);

            var val = await _dapperService.QuerySingleAsync<int?>(sql, p);
            return val;
        }
        public async Task<string?> GetCurrencyCodeAsync(int currencyId, string culture, string companyId)
        {
            const string sql = @"
            SELECT Code
            FROM vwTblSetupsDetail
            WHERE culture = @Culture AND sdlid = @SdlId AND CompanyId = @CompanyId;";

            var p = new DynamicParameters();
            p.Add("@Culture", culture);
            p.Add("@SdlId", currencyId);
            p.Add("@CompanyId", companyId);

            try
            {
                return await _dapperService.QuerySingleAsync<string?>(sql, p);
            }
            catch
            {
                return null;
            }
        }
        public async Task<DateTime> GetSysCurrentdateAsync()
        {
            var companyId = GetCompanyId(_clientContextService.GetClientIP());
            const string sql = "SELECT dbo.fn_General_GetLocalDateTimeCompanyWise(@CompanyId)";

            try
            {
                var result = await _dapperService.ExecuteScalarAsync<DateTime?>(
                    sql,
                    new { CompanyId = companyId }
                );

                if (result.HasValue)
                {
                    return result.Value;
                }
                else
                {
                    return DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                return DateTime.Now;
            }
        }

        public DataTable GetBaseCurrency()
        {
            string clientIp = _clientContextService.GetClientIP();
            string culture = GetAppCurrentUICulture(clientIp);
            string companyId = GetCompanyId(clientIp);

            var ds = new DataSet();

            string query =
                "SELECT Sdlid, Code, Name " +
                "FROM TblHRPolicy AS hr " +
                "INNER JOIN vwTblSetupsDetail AS sd " +
                "  ON sd.culture = '" + culture + "' " +
                " AND sd.sdlid = hr.BaseCurrencyId " +
                " AND sd.companyid = hr.companyid " +
                "WHERE sd.companyId = " + companyId;

            string result = _dataservice.ExecuteReader(query, ref ds);
            if (result.Equals("successful") && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                return ds.Tables[0];
            else
                return null;

        }
        public double GetExchangeRate(int currencyId)
        {
            DataSet objDataSet = new DataSet();
            DataTable baseCur = GetBaseCurrency();
            int baseCurrencyId = Convert.ToInt32(baseCur.Rows[0][0].ToString());
            string clientIp = _clientContextService.GetClientIP();
            if (baseCurrencyId != currencyId)
            {
                string query = "SELECT ConversionRate " +
                               "FROM tblExchangeRateHistory " +
                               "WHERE currencyId = " + currencyId + " and companyId = " + GetCompanyId(clientIp) + " " +
                               "ORDER BY ConversionDate desc ";


                string r = _dataservice.ExecuteReader(query, ref objDataSet);
                if (r.Equals("successfull") && objDataSet.Tables.Count > 0 && objDataSet.Tables[0].Rows.Count > 0)
                    return Convert.ToDouble(objDataSet.Tables[0].Rows[0][0].ToString());
                else
                    return Convert.ToDouble(0);
            }

            else
                return Convert.ToDouble(1);


        }
        public bool isMultiCompanyEnabled()
        {
            var IsMultiCompanyEnabled = _configuration.GetValue<string>("CorsSettings:IsMultiCompany");
            if (IsMultiCompanyEnabled == "Y")
                return true;
            else
                return false;
        }
        public async Task<IReadOnlyList<CompanyDto>> GetCompanyWRTUserAsync(string userId, string formId)
        {
            const string sql = @"SELECT * FROM fn_GetCompanyWRTUser(@UserId, @FormId) ORDER BY CCode;";

            await using var con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var rows = await con.QueryAsync<CompanyDto>(sql, new { UserId = userId, FormId = formId });
            return rows.AsList();
        }
    }

    public sealed class CompanyDto
    {
        public int CCode { get; set; }
        public string CompanyDescription { get; set; } = "";
        // add other columns returned by the function
    }
}
