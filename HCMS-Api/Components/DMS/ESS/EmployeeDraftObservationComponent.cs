using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Models;
using System;
using System.Threading.Tasks;

namespace HCMS_Api.Components.DMS.ESS;

public class EmployeeDraftObservationComponent
{
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly DMSCommon _common;

    public EmployeeDraftObservationComponent(
        DMSUtilities utilities,
        ClientContextService clientContextService,
        DMSCommon common)
    {
        _utilities = utilities;
        _clientContextService = clientContextService;
        _common = common;
    }

    public async Task<EmployeeDraftObservation?> GetByEmployeeCodeAsync(string employeeCode)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());
             

            var sql = @"
                SELECT id, employeecode, companyid, observationtext 
                FROM EmployeeDraftObservation 
                WHERE TRIM(EmployeeCode) = TRIM(@EmployeeCode) AND CompanyId = @CompanyId LIMIT 1";

            var result = await _common.QueryFirstOrDefaultAsync<dynamic>(sql, new { EmployeeCode = empCode, CompanyId = CompanyId });
            if (result == null) return null;

            return new EmployeeDraftObservation
            {
                Id = result.id,
                EmployeeCode = result.employeecode,
                CompanyId = result.companyid,
                ObservationText = result.observationtext
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<EmployeeDraftObservation> SaveObservationAsync(EmployeeDraftObservationDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input == null)
                throw new CustomException("Input details are required.", 400);
             

            if (string.IsNullOrWhiteSpace(input.ObservationText))
                throw new CustomException("Observation text is required.", 400);

            // Check if existing observation exists for this employee and company
            var checkSql = @"
                SELECT id FROM EmployeeDraftObservation 
                WHERE TRIM(EmployeeCode) = TRIM(@EmployeeCode) AND CompanyId = @CompanyId LIMIT 1";
            var existingIdObj = await _common.ExecuteScalarAsync<object>(checkSql, new { EmployeeCode = empCode, CompanyId = CompanyId });

            if (existingIdObj != null)
            {
                int existingId = Convert.ToInt32(existingIdObj);
                var updateSql = @"
                    UPDATE EmployeeDraftObservation 
                    SET ObservationText = @ObservationText 
                    WHERE Id = @Id AND CompanyId = @CompanyId";
                await _common.ExecuteAsync(updateSql, new { ObservationText = input.ObservationText, Id = existingId, CompanyId = CompanyId });

                return new EmployeeDraftObservation
                {
                    Id = existingId,
                    EmployeeCode = empCode,
                    CompanyId = CompanyId,
                    ObservationText = input.ObservationText
                };
            }
            else
            {
                var insertSql = @"
                    INSERT INTO EmployeeDraftObservation (EmployeeCode, CompanyId, ObservationText) 
                    VALUES (@EmployeeCode, @CompanyId, @ObservationText) 
                    RETURNING Id";
                int newId = await _common.ExecuteScalarAsync<int>(insertSql, new { EmployeeCode = empCode, CompanyId = CompanyId, ObservationText = input.ObservationText });

                return new EmployeeDraftObservation
                {
                    Id = newId,
                    EmployeeCode = empCode,
                    CompanyId = CompanyId,
                    ObservationText = input.ObservationText
                };
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
