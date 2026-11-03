using HCMS_Api.Common;
using HCMS_Api.Components.DMS.Common.Models;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// The rules about the Distribution List that more than one component has to apply when something is submitted.
/// </summary>
public static class DistributionListRules
{
    public const string RequiredMessage = "The Distribution List is mandatory. Please add at least one row to it before submitting.";

    /// <summary>
    /// A request or a document is not submitted without a Distribution List: it is what says who the document goes to once it
    /// is in force. Applies to Creation and Revision. An Obsoletion does not come here: its list is the document's own, shown
    /// for confirmation only, and nothing is added to it. A draft saved on the way is not held to this either.
    /// </summary>
    public static void Require(IEnumerable<DistributionListCreateDto>? rows)
    {
        if (rows == null || !rows.Any())
            throw new CustomException(RequiredMessage, 400);
    }
}
