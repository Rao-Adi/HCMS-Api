using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.ESS;
using Microsoft.AspNetCore.Mvc;

namespace HCMS_Api.Controllers.DMS.Common;

[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSDocumentReviewPolicyController : Controller
{
    private readonly DocumentReviewPolicyComponent _component;

    public DMSDocumentReviewPolicyController(DocumentReviewPolicyComponent component)
    {
        _component = component;
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] DocumentReviewPolicyCreateDto input)
    {
        var result = await _component.CreateAsync(input);
        return Ok(result);
    }

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] DocumentReviewPolicyUpdateDto input)
    {
        var result = await _component.UpdateAsync(input);
        return Ok(result);
    }

    [HttpDelete("Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _component.DeleteAsync(id);
        return Ok(result);
    }

    [HttpPost("get-all-document-review-policies")]
    public async Task<IActionResult> GetAll([FromBody] TableFiltersDto input)
    {
        var result = await _component.GetAllAsync(input);
        return Ok(result);
    }

    [HttpGet("get-document-review-policy-by-id")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _component.GetByIdAsync(id);
        if (result == null)
            return NotFound();
        return Ok(result);
    }
}
