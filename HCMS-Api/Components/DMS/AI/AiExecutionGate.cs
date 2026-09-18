using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// Bounds how many AI requests run at once: one per user, and a small number across the server.
///
/// A single model request occupies the on-prem engine for seconds. Without this, one user holding
/// the Enter key, or twenty approvers opening the assistant at the same time, would queue requests
/// until the whole gateway stopped answering for everyone. Both limits refuse immediately rather
/// than waiting, so a busy assistant tells the user to try again instead of hanging the page.
/// </summary>
public sealed class AiExecutionGate : IDisposable
{
    private readonly SemaphoreSlim _global;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _perUser = new(StringComparer.Ordinal);

    public AiExecutionGate(IOptions<AiOptions> options)
    {
        var limit = Math.Clamp(options.Value.GlobalConcurrency, 1, 32);
        _global = new SemaphoreSlim(limit, limit);
    }

    /// <summary>Returns null when the caller should be told the assistant is busy.</summary>
    public async ValueTask<IAsyncDisposable?> TryEnterAsync(
        string userKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userKey);
        cancellationToken.ThrowIfCancellationRequested();

        var userGate = _perUser.GetOrAdd(userKey, static _ => new SemaphoreSlim(1, 1));

        if (!await userGate.WaitAsync(0, cancellationToken))
            return null;

        if (!await _global.WaitAsync(0, cancellationToken))
        {
            userGate.Release();
            return null;
        }

        return new Lease(_global, userGate);
    }

    public void Dispose()
    {
        _global.Dispose();
        foreach (var gate in _perUser.Values)
            gate.Dispose();
    }

    private sealed class Lease : IAsyncDisposable
    {
        private SemaphoreSlim? _global;
        private SemaphoreSlim? _user;

        public Lease(SemaphoreSlim global, SemaphoreSlim user)
        {
            _global = global;
            _user = user;
        }

        // Exchange so a double dispose cannot release a slot twice and let the limit drift upward.
        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _global, null)?.Release();
            Interlocked.Exchange(ref _user, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
