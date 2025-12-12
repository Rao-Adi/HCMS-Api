using HCMS_Api.Common;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.DataAccess;

namespace HCMS_Api.Components.HCMS.ESS
{
    public class AppraisalEvaluationComponent
    {
        private readonly DataServices _dataservice;
        private readonly IConfiguration _configuration;
        private readonly Utilities _utilities;
        private readonly ClientContextService _clientContextService;

        public AppraisalEvaluationComponent(IConfiguration configuration, Utilities utilities
            , ClientContextService clientContextService, DataServices dataservice)
        {
            _configuration = configuration;
            _utilities = utilities;
            _clientContextService = clientContextService;
            _dataservice = dataservice;

            string connectionString = _configuration.GetRequiredConnectionString("ConnectionString");
            _dataservice.BeginProcess(connectionString);
        }



    }
}
