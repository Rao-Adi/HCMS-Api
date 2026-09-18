using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.AI;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.ESS;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HCMS_Api.Controllers.DMS.Common;

/// <summary>
/// DMS AI endpoints: proofreading for the rich text editor, and the document assistant.
///
/// Every AI failure is mapped to a status and a plain sentence here, so a model that is down or
/// unconfigured never reaches a document screen as a 500 with a stack trace.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/[controller]")]
public class DMSAiController : Controller
{
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly AiProofreadService _proofread;
    private readonly AiAssistantService _assistant;
    private readonly AiPolicyStore _policies;
    private readonly AuditLogComponent _auditLog;
    private readonly IAiProvider _provider;
    private readonly ILogger<DMSAiController> _logger;

    public DMSAiController(
        DMSUtilities utilities,
        ClientContextService clientContextService,
        AiProofreadService proofread,
        AiAssistantService assistant,
        AiPolicyStore policies,
        AuditLogComponent auditLog,
        IAiProvider provider,
        ILogger<DMSAiController> logger)
    {
        _utilities = utilities;
        _clientContextService = clientContextService;
        _proofread = proofread;
        _assistant = assistant;
        _policies = policies;
        _auditLog = auditLog;
        _provider = provider;
        _logger = logger;
    }

    public sealed class ProofreadRequest
    {
        public string? Html { get; set; }
    }

    public sealed class AskRequest
    {
        public string? Question { get; set; }
    }

    /// <summary>
    /// Lets the editor decide whether to show its AI action at all, rather than offering a button
    /// that fails when pressed on an installation where the model is not set up.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus() =>
        Ok(new HttpApiResponse<object>
        {
            Success = true,
            Data = new { Enabled = _provider.IsConfigured },
            Message = "Success",
            Code = 200
        });

    [HttpPost("proofread")]
    public async Task<IActionResult> Proofread([FromBody] ProofreadRequest input, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _proofread.ProofreadAsync(CurrentUserKey(), input?.Html, cancellationToken);

            return Ok(new HttpApiResponse<object>
            {
                Success = true,
                Data = new { result.CorrectedHtml, result.Changed, result.Notice },
                Message = result.Changed ? "Corrections suggested." : "No spelling or grammar issues found.",
                Code = 200
            });
        }
        catch (AiException ex)
        {
            return AiError(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proofread failed unexpectedly.");
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    /// <summary>
    /// The AI Report assistant. Answers from the documents the CALLING user can already open --
    /// the scope is decided server-side by AiDocumentContextBuilder, never by the question.
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest input, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _assistant.AskAsync(CurrentUserKey(), input?.Question, cancellationToken);

            return Ok(new HttpApiResponse<object>
            {
                Success = true,
                Data = new { result.Answer, result.Sources, result.Notice },
                Message = "Success",
                Code = 200
            });
        }
        catch (AiException ex)
        {
            return AiError(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assistant question failed unexpectedly.");
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Behaviour rules.
    //
    // These endpoints expose the editable Markdown that tells each AI feature how to behave, so a
    // wording change at a client site is an edit rather than a release. Two things they cannot do,
    // by design:
    //
    //   * They cannot widen what the assistant may read. Which documents reach the model is decided
    //     in SQL by AiDocumentContextBuilder, from the asking user's own access. No wording here
    //     touches it.
    //   * They are not reachable from the answering path. Nothing inside a question, and nothing
    //     inside a document the assistant reads, can cause a write here -- a person has to call it.
    //     That separation is what stops a document from editing the rules by containing the right
    //     sentence.
    //
    // Every write keeps the version it replaced and records who made it.
    // ---------------------------------------------------------------------------------------

    public sealed class PolicyUpdateRequest
    {
        public string? Content { get; set; }

        /// <summary>Why it was changed. Recorded with the audit entry.</summary>
        public string? Reason { get; set; }
    }

    public sealed class PolicyRestoreRequest
    {
        /// <summary>A file name from the policy's history listing.</summary>
        public string? Version { get; set; }

        public string? Reason { get; set; }
    }

    /// <summary>Lists the editable policies and where they are read from.</summary>
    [HttpGet("policies")]
    public IActionResult GetPolicies()
    {
        var policies = _policies.Names.Select(name =>
        {
            var view = _policies.Read(name);
            return new
            {
                view.Name,
                view.Path,
                view.FromFile,
                view.LastModifiedUtc,
                view.RuleCharacters,
            };
        }).ToList();

        return Ok(new HttpApiResponse<object>
        {
            Success = true,
            Data = new { Directory = _policies.PolicyDirectory, Policies = policies },
            Message = "Success",
            Code = 200
        });
    }

    /// <summary>The full text of one policy, as it would be edited.</summary>
    [HttpGet("policies/{name}")]
    public IActionResult GetPolicy(string name)
    {
        try
        {
            var view = _policies.Read(name);
            return Ok(new HttpApiResponse<object>
            {
                Success = true,
                Data = new
                {
                    view.Name,
                    view.Path,
                    view.Content,
                    view.FromFile,
                    view.LastModifiedUtc,
                    view.RuleCharacters,
                    History = _policies.History(view.Name)
                        .Select(h => new { h.File, h.WhenUtc })
                        .ToList(),
                },
                Message = "Success",
                Code = 200
            });
        }
        catch (AiException ex)
        {
            return AiError(ex);
        }
    }

    /// <summary>Replaces a policy. The previous version is kept and the change is audited.</summary>
    [HttpPut("policies/{name}")]
    public async Task<IActionResult> UpdatePolicy(string name, [FromBody] PolicyUpdateRequest input)
    {
        try
        {
            // Read before writing, so the audit entry carries what it replaced and not just what
            // it became. On a controlled system, "what changed" is the question that gets asked.
            var previous = _policies.Read(name);
            var result = _policies.Write(name, input?.Content ?? "");

            await AuditPolicyChangeAsync(
                "AI Policy Updated", result.Name, previous.Content, result, input?.Reason);

            return Ok(new HttpApiResponse<object>
            {
                Success = true,
                Data = new { result.Name, result.Path, result.RuleCharacters, PreviousVersion = result.BackupPath },
                Message = "The AI rules were updated. They take effect on the next request.",
                Code = 200
            });
        }
        catch (AiException ex)
        {
            return AiError(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Updating AI policy {Policy} failed.", name);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    /// <summary>Puts a previous version of a policy back.</summary>
    [HttpPost("policies/{name}/restore")]
    public async Task<IActionResult> RestorePolicy(string name, [FromBody] PolicyRestoreRequest input)
    {
        try
        {
            var previous = _policies.Read(name);

            // No version named means "back to what shipped with this build" -- the way out when a
            // site has edited itself into a corner and no saved version is known to be good.
            var result = string.IsNullOrWhiteSpace(input?.Version)
                ? _policies.ResetToBuiltIn(name)
                : _policies.Restore(name, input!.Version!);

            await AuditPolicyChangeAsync(
                "AI Policy Restored", result.Name, previous.Content, result,
                input?.Reason ?? (string.IsNullOrWhiteSpace(input?.Version) ? "Reset to built-in rules" : input!.Version));

            return Ok(new HttpApiResponse<object>
            {
                Success = true,
                Data = new { result.Name, result.Path, result.RuleCharacters, PreviousVersion = result.BackupPath },
                Message = "The AI rules were restored. They take effect on the next request.",
                Code = 200
            });
        }
        catch (AiException ex)
        {
            return AiError(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restoring AI policy {Policy} failed.", name);
            var response = HttpResponseCatchReturn.ReturnException(ex, new { });
            return StatusCode(response.Code, response);
        }
    }

    /// <summary>
    /// Records a rule change the way the rest of DMS records a document change. An audit failure
    /// must not undo a write that already happened, so it is logged and swallowed.
    /// </summary>
    private async Task AuditPolicyChangeAsync(
        string action, string policyName, string oldContent, AiPolicyStore.WriteResult result, string? reason)
    {
        try
        {
            var clientIp = _clientContextService.GetClientIP();
            var companyId = int.Parse(_utilities.GetCompanyId(clientIp));
            var empCode = _utilities.GetEmpCodeForHCMS(_utilities.GetEmpid(clientIp).ToString()) ?? "";

            await _auditLog.LogActionAsync(
                companyId, empCode, action, "AiPolicy", 0, clientIp ?? "",
                System.Text.Json.JsonSerializer.Serialize(new { Policy = policyName, Content = oldContent }),
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    Policy = policyName,
                    result.RuleCharacters,
                    PreviousVersion = result.BackupPath,
                    Reason = reason ?? "",
                }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI policy {Policy} was changed but the audit entry could not be written.", policyName);
        }
    }

    /// <summary>
    /// Identifies the caller for the per-user concurrency gate. The session key already
    /// distinguishes users; it is used here only as a key, never sent anywhere.
    /// </summary>
    private string CurrentUserKey()
    {
        var clientIp = _clientContextService.GetClientIP();
        try
        {
            var companyId = _utilities.GetCompanyId(clientIp);
            var empId = _utilities.GetEmpid(clientIp);
            return companyId + ":" + empId;
        }
        catch
        {
            // Never let key construction be the thing that fails a request; a shared key simply
            // means those callers queue against each other.
            return "dms:" + (clientIp ?? "unknown");
        }
    }

    private IActionResult AiError(AiException ex)
    {
        var status = ex.Failure switch
        {
            AiFailure.NotConfigured => HttpStatusCode.ServiceUnavailable,
            AiFailure.Unavailable => HttpStatusCode.ServiceUnavailable,
            AiFailure.Unauthorized => HttpStatusCode.BadGateway,
            AiFailure.Busy => (HttpStatusCode)429,
            AiFailure.InvalidResponse => HttpStatusCode.UnprocessableEntity,
            _ => HttpStatusCode.InternalServerError
        };

        // Logged at warning, not error: a model being down or busy is an expected operating
        // condition for this feature, not a fault in the document system.
        _logger.LogWarning("AI request refused ({Failure}): {Message}", ex.Failure, ex.Message);

        return StatusCode((int)status, new HttpApiResponse<object>
        {
            Success = false,
            Data = new { },
            Message = ex.Message,
            Code = (int)status
        });
    }
}
