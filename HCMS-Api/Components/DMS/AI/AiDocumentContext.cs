namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// One document as the assistant is allowed to see it.
///
/// <see cref="Ref"/> is the only handle the model ever gets. It is assigned by the server for the
/// life of a single question ("D1", "D2", ...) and means nothing outside it. The model cites refs;
/// the server turns them back into real documents. A model cannot therefore name a document that
/// was not put in front of it, whether by mistake or because a question talked it into trying.
/// </summary>
public sealed class AiDocumentSummary
{
    public string Ref { get; set; } = "";
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string DocumentType { get; set; } = "";
    public string Status { get; set; } = "";
    public string Version { get; set; } = "";
    public string Division { get; set; } = "";
    public string Department { get; set; } = "";
    /// <summary>The review due date. A calendar date in the database, so a calendar date here.</summary>
    public DateOnly? NextReviewDate { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    /// <summary>
    /// True when this document is in the asking user's approvals inbox right now. Status cannot
    /// carry this: "Pending Approval" says the document is in a workflow, not that it is waiting
    /// on the person asking.
    /// </summary>
    public bool AwaitingYourApproval { get; set; }

    /// <summary>
    /// The document's own text, present only when AiOptions.AllowDocumentContent is on. Empty is
    /// the normal state: these are controlled documents and their content is not context by default.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>True when Content was cut to fit the budget, so the answer can say so.</summary>
    public bool ContentTruncated { get; set; }
}

/// <summary>
/// The documents assembled for one question, plus what had to be left out.
///
/// Omissions are carried explicitly rather than quietly dropped: an answer drawn from a partial
/// set must be able to say it is partial. "There is no such document" and "I was only shown the
/// first 40" are very different answers to give someone about a controlled document.
/// </summary>
public sealed class AiDocumentContext
{
    public List<AiDocumentSummary> Documents { get; } = new();

    /// <summary>How many documents matched in total, before the budget was applied.</summary>
    public int TotalMatched { get; set; }

    /// <summary>True when documents were left out to fit the budget.</summary>
    public bool Truncated => TotalMatched > Documents.Count;

    /// <summary>Whether document text was permitted in this context at all.</summary>
    public bool ContentIncluded { get; set; }
}
