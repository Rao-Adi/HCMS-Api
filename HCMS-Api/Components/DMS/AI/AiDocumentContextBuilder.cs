using System.Text.RegularExpressions;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Components.DMS.Common;
using Microsoft.Extensions.Options;

namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// Chooses which documents the assistant may see when answering one question.
///
/// This class is the feature's security boundary, so the rule it enforces is worth stating
/// plainly: <b>the assistant is shown exactly what the asking user could already open through the
/// application, and nothing else.</b> There is no administrative bypass, and no separate
/// "AI reads everything" path -- an assistant that answered from documents its user cannot open
/// would be a way around the document system rather than a way into it.
///
/// Concretely, the scope is the union of the three places a user already sees documents today:
///
///   * EFFECTIVE documents in their company -- the same set the "View Documents (Approved)" report
///     puts in front of anyone who can open that screen (see GetApprovedEffectiveDocumentsAsync,
///     which applies no per-user filter of its own).
///   * Documents they created -- their own "My Documents" list.
///   * Documents currently assigned to them in a workflow step -- their approvals inbox.
///
/// Everything else -- other people's drafts, documents in someone else's approval chain -- is not
/// selected here and therefore cannot reach the model at all.
/// </summary>
public sealed class AiDocumentContextBuilder
{
    // Words that carry no selectivity in a document search. Kept deliberately short: an aggressive
    // stop list would strip terms that are meaningful here ("policy", "SOP", "draft", "review").
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "what", "when", "which", "who", "whom", "whose", "where", "why", "how",
        "are", "was", "were", "does", "did", "can", "could", "would", "should", "will",
        "show", "tell", "give", "list", "find", "about", "please", "with", "from", "into", "that",
        "this", "these", "those", "there", "their", "them", "have", "has", "had", "you", "your",
        "any", "all", "not", "but", "its", "kya", "hai", "ka", "ki", "ke", "batao", "dikhao",
    };

    private static readonly Regex TokenPattern =
        new(@"[A-Za-z0-9][A-Za-z0-9\-_/]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly DMSCommon _common;
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly AiOptions _options;
    private readonly ILogger<AiDocumentContextBuilder> _logger;

    public AiDocumentContextBuilder(
        DMSCommon common,
        DMSUtilities utilities,
        ClientContextService clientContextService,
        IOptions<AiOptions> options,
        ILogger<AiDocumentContextBuilder> logger)
    {
        _common = common;
        _utilities = utilities;
        _clientContextService = clientContextService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Row shape returned by the scope query. Internal to this class.</summary>
    private sealed class DocumentRow
    {
        public int Id { get; set; }
        public string? DocumentNumber { get; set; }
        public string? Title { get; set; }
        public string? DocumentType { get; set; }
        public string? Status { get; set; }
        public string? Version { get; set; }
        public string? Division { get; set; }
        public string? Department { get; set; }

        // DateOnly, not DateTime: VW_Documents.NextReviewDate is a Postgres `date`, which Npgsql
        // hands back as DateOnly. Declaring it as DateTime made Dapper fail the whole row with
        // "Error parsing column 8 (nextreviewdate=... - Object)". Every other date on this view is
        // `timestamp` and maps to DateTime as usual. DocumentComponent reads this same column as
        // DateOnly too, so this matches the rest of the codebase rather than working around it.
        public DateOnly? NextReviewDate { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public string? VersionContent { get; set; }
        public bool AwaitingYourApproval { get; set; }
    }

    public async Task<AiDocumentContext> BuildAsync(string question, CancellationToken cancellationToken = default)
    {
        var clientIp = _clientContextService.GetClientIP();
        var companyId = int.Parse(_utilities.GetCompanyId(clientIp));
        var empCode = _utilities.GetEmpCodeForHCMS(_utilities.GetEmpid(clientIp).ToString()) ?? "";

        var tokens = ExtractTokens(question);

        // Ranking happens in SQL so the budget is spent on the documents most likely to be the
        // subject of the question rather than on whatever happened to be created last. A document
        // number is the strongest signal a user can give ("SOP-001"), the title next, type weakest.
        const string sql = @"
            WITH q(tok) AS (
                SELECT UNNEST(@Tokens::text[])
            ),
            scoped AS (
                SELECT
                    v.Id,
                    v.DocumentNumber,
                    v.Title,
                    v.DocumentType,
                    ds.Name            AS Status,
                    v.Version,
                    v.Division,
                    v.Department,
                    v.NextReviewDate,
                    v.CreatedByName,
                    v.CreatedAt,
                    v.LastModifiedAt,
                    v.VersionContent,
                    -- Whether this document is sitting in THIS user's approvals inbox right now:
                    -- an active step assigned to them that they have not decided yet. Status alone
                    -- cannot answer a question about the asker's OWN approvals -- a status of
                    -- Pending Approval only says the document is somewhere in a workflow, not
                    -- that it is waiting on the person asking.
                    EXISTS (
                        SELECT 1
                        FROM WorkflowExecutionSteps pend
                        JOIN WorkflowExecutions pex
                             ON pex.Id = pend.WorkflowExecutionId
                            AND pex.CompanyId = pend.CompanyId
                        WHERE pex.CompanyId = @CompanyId
                          AND pex.EntityType = 'Document'
                          AND pex.EntityId = v.Id
                          AND pend.IsActive = TRUE
                          AND COALESCE(NULLIF(TRIM(pend.Decision), ''), NULL) IS NULL
                          AND LTRIM(RTRIM(COALESCE(pend.AssignedUserId, '')), '0') = LTRIM(RTRIM(@EmpCode), '0')
                    ) AS AwaitingYourApproval
                FROM VW_Documents v
                JOIN LATERAL (
                    SELECT dsh.ToStateId
                    FROM DocumentStateHistory dsh
                    WHERE dsh.DocumentId = v.Id
                    ORDER BY dsh.ChangedAt DESC, dsh.Id DESC
                    LIMIT 1
                ) latest ON TRUE
                JOIN DocumentStates ds ON ds.Id = latest.ToStateId
                WHERE v.CompanyId = @CompanyId
                  AND v.IsDeleted = FALSE
                  AND (
                        -- Published documents: the same set the approved-documents report shows.
                        ds.Code = 'EFFECTIVE'
                        -- The user's own documents, in whatever state.
                        OR LTRIM(RTRIM(COALESCE(v.CreatedBy, '')), '0') = LTRIM(RTRIM(@EmpCode), '0')
                        -- Documents waiting on this user in a workflow -- their approvals inbox.
                        OR EXISTS (
                            SELECT 1
                            FROM WorkflowExecutionSteps wes
                            JOIN WorkflowExecutions we
                                 ON we.Id = wes.WorkflowExecutionId
                                AND we.CompanyId = wes.CompanyId
                            WHERE we.CompanyId = @CompanyId
                              AND we.EntityType = 'Document'
                              AND we.EntityId = v.Id
                              AND LTRIM(RTRIM(COALESCE(wes.AssignedUserId, '')), '0') = LTRIM(RTRIM(@EmpCode), '0')
                        )
                  )
            ),
            ranked AS (
                SELECT
                    s.*,
                    (
                        (SELECT COUNT(*) FROM q WHERE s.DocumentNumber ILIKE '%' || q.tok || '%') * 5
                      + (SELECT COUNT(*) FROM q WHERE s.Title          ILIKE '%' || q.tok || '%') * 3
                      + (SELECT COUNT(*) FROM q WHERE s.DocumentType   ILIKE '%' || q.tok || '%') * 2
                    ) AS Score
                FROM scoped s
            )
            SELECT Id, DocumentNumber, Title, DocumentType, Status, Version, Division, Department,
                   NextReviewDate, CreatedByName, CreatedAt, LastModifiedAt, VersionContent,
                   AwaitingYourApproval
            FROM ranked
            -- No token matched anything: fall back to the most recently touched documents rather
            -- than returning nothing, so 'what changed lately' style questions still have context.
            WHERE Score > 0 OR NOT EXISTS (SELECT 1 FROM ranked r2 WHERE r2.Score > 0)
            -- Relevance first, then anything waiting on this user. A document the asker has to
            -- act on is the likelier subject of a vague question than one merely touched recently;
            -- a document matched by number still outranks both, because Score sorts first.
            ORDER BY Score DESC, AwaitingYourApproval DESC, LastModifiedAt DESC NULLS LAST, CreatedAt DESC NULLS LAST
            LIMIT @Limit";

        var maxDocuments = Math.Max(1, _options.MaxContextDocuments);

        // One over the cap, so the context can tell the difference between "that is all of them"
        // and "there were more than I was able to show".
        var rows = (await _common.QueryAsync<DocumentRow>(sql, new
        {
            CompanyId = companyId,
            EmpCode = empCode,
            Tokens = tokens,
            Limit = maxDocuments + 1,
        })).ToList();

        var context = new AiDocumentContext
        {
            ContentIncluded = _options.AllowDocumentContent,
            TotalMatched = rows.Count,
        };

        var budget = Math.Max(1_000, _options.MaxContextCharacters);
        var used = 0;

        foreach (var row in rows.Take(maxDocuments))
        {
            var summary = new AiDocumentSummary
            {
                Ref = "D" + (context.Documents.Count + 1),
                Id = row.Id,
                DocumentNumber = row.DocumentNumber ?? "",
                Title = row.Title ?? "",
                DocumentType = row.DocumentType ?? "",
                Status = row.Status ?? "",
                Version = row.Version ?? "",
                Division = row.Division ?? "",
                Department = row.Department ?? "",
                NextReviewDate = row.NextReviewDate,
                CreatedBy = row.CreatedByName ?? "",
                CreatedAt = row.CreatedAt,
                LastModifiedAt = row.LastModifiedAt,
                AwaitingYourApproval = row.AwaitingYourApproval,
            };

            // Metadata is small and always worth including; content is the part that can run to six
            // figures of characters (one real document here holds 110,000), so it is added only
            // while there is room, and what is added is marked when it had to be cut.
            var metadataCost = EstimateMetadataCost(summary);
            if (used + metadataCost > budget && context.Documents.Count > 0)
                break;

            used += metadataCost;

            if (_options.AllowDocumentContent && !string.IsNullOrWhiteSpace(row.VersionContent))
            {
                var plain = StripHtml(row.VersionContent!);
                var room = budget - used;

                // Below this there is not enough left for a usable excerpt; say it was withheld
                // rather than hand the model a sentence and a half of a procedure.
                if (room > 400)
                {
                    summary.Content = plain.Length > room ? plain[..room] : plain;
                    summary.ContentTruncated = plain.Length > room;
                    used += summary.Content.Length;
                }
                else
                {
                    summary.ContentTruncated = true;
                }
            }

            context.Documents.Add(summary);
        }

        // TotalMatched was the raw row count, which is capped at maxDocuments + 1. Report the cap
        // itself as "more than this", so Truncated is honest without a second COUNT query.
        _logger.LogInformation(
            "AI context for {EmpCode}: {Included} of {Matched} documents, {Chars} characters, content {ContentState}.",
            empCode, context.Documents.Count, context.TotalMatched, used,
            _options.AllowDocumentContent ? "included" : "withheld");

        return context;
    }

    /// <summary>
    /// Pulls the words worth searching on out of a question. Short and common words are dropped;
    /// anything that looks like a document number is kept whole, hyphens and all. Urdu question
    /// words in Roman script are in the stop list too, because that is how this is actually asked.
    /// </summary>
    internal static string[] ExtractTokens(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Array.Empty<string>();

        return TokenPattern.Matches(question)
            .Select(m => m.Value.Trim('-', '_', '/'))
            .Where(t => t.Length >= 3 && !StopWords.Contains(t))
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToArray();
    }

    // The 260 is the labels the prompt wraps around each document ("Type:", "Next review date:",
    // and so on), measured against the rendered output rather than guessed -- counting only the
    // field values under-reported a document's real cost by more than half, which is how a
    // 12,000-character budget could still produce a prompt that overran the model's context.
    private static int EstimateMetadataCost(AiDocumentSummary d) =>
        260
        + d.DocumentNumber.Length + d.Title.Length + d.DocumentType.Length
        + d.Status.Length + d.Version.Length + d.Division.Length + d.Department.Length
        + d.CreatedBy.Length;

    /// <summary>
    /// Document content is stored as HTML. Tags cost context and tell the model nothing it needs,
    /// so only the text goes into the prompt.
    /// </summary>
    internal static string StripHtml(string html)
    {
        var withoutTags = HtmlTag.Replace(html, " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        return Whitespace.Replace(decoded, " ").Trim();
    }
}
