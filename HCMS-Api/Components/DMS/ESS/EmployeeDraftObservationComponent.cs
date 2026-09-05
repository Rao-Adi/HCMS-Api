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

    // entityType is "Document" or "Request" -- resolves which of the two mutually-exclusive
    // scope columns to filter on, matching WorkflowObservationDialogComponent's modalData.entityType.
    private static (string column, string other) ResolveEntityColumn(string entityType)
    {
        if (entityType.Equals("Document", StringComparison.OrdinalIgnoreCase))
            return ("DocumentId", "RequestId");
        if (entityType.Equals("Request", StringComparison.OrdinalIgnoreCase))
            return ("RequestId", "DocumentId");
        throw new CustomException("EntityType must be 'Document' or 'Request'.", 400);
    }

    public async Task<EmployeeDraftObservation?> GetDraftObservationAsync(string entityType, int entityId)
    {
        try
        {
            var (column, _) = ResolveEntityColumn(entityType);

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var sql = $@"
                SELECT id, employeecode, companyid, observationtext, documentid, requestid
                FROM EmployeeDraftObservation
                WHERE TRIM(EmployeeCode) = TRIM(@EmployeeCode) AND CompanyId = @CompanyId
                  AND {column} = @EntityId
                LIMIT 1";

            var result = await _common.QueryFirstOrDefaultAsync<dynamic>(sql, new { EmployeeCode = empCode, CompanyId = CompanyId, EntityId = entityId });
            if (result == null) return null;

            return new EmployeeDraftObservation
            {
                Id = result.id,
                EmployeeCode = result.employeecode,
                CompanyId = result.companyid,
                ObservationText = result.observationtext,
                DocumentId = result.documentid,
                RequestId = result.requestid
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
            if (input == null)
                throw new CustomException("Input details are required.", 400);

            if (string.IsNullOrWhiteSpace(input.ObservationText))
                throw new CustomException("Observation text is required.", 400);

            var (column, _) = ResolveEntityColumn(input.EntityType);

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check if a draft already exists for this employee AND this specific document/request
            var checkSql = $@"
                SELECT id FROM EmployeeDraftObservation
                WHERE TRIM(EmployeeCode) = TRIM(@EmployeeCode) AND CompanyId = @CompanyId
                  AND {column} = @EntityId
                LIMIT 1";
            var existingIdObj = await _common.ExecuteScalarAsync<object>(checkSql, new { EmployeeCode = empCode, CompanyId = CompanyId, EntityId = input.EntityId });

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
                    ObservationText = input.ObservationText,
                    DocumentId = column == "DocumentId" ? input.EntityId : null,
                    RequestId = column == "RequestId" ? input.EntityId : null
                };
            }
            else
            {
                var insertSql = $@"
                    INSERT INTO EmployeeDraftObservation (EmployeeCode, CompanyId, ObservationText, {column})
                    VALUES (@EmployeeCode, @CompanyId, @ObservationText, @EntityId)
                    RETURNING Id";
                int newId = await _common.ExecuteScalarAsync<int>(insertSql, new { EmployeeCode = empCode, CompanyId = CompanyId, ObservationText = input.ObservationText, EntityId = input.EntityId });

                return new EmployeeDraftObservation
                {
                    Id = newId,
                    EmployeeCode = empCode,
                    CompanyId = CompanyId,
                    ObservationText = input.ObservationText,
                    DocumentId = column == "DocumentId" ? input.EntityId : null,
                    RequestId = column == "RequestId" ? input.EntityId : null
                };
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
