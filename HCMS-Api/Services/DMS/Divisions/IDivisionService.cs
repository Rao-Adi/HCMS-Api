using HCMS_Api.Common;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models.Divisions;

namespace HCMS_Api.Services.DMS.Divisions;

public interface IDivisionService
{
    Task<PaginationResult<DivisionReadDto>> GetAllAsync(TableFiltersDto input);

    Task<DivisionReadDto> GetByCodeAsync(string code);

    Task<IQueryable<SelectListDto>> GetAllSelectList();

    Task<DivisionReadDto> CreateAsync(DivisionCreateDto input);

    Task<DivisionReadDto> UpdateAsync(DivisionUpdateDto input);

    Task<bool> DeleteAsync(string code);
}
