using Microsoft.Extensions.Options;

namespace HCMS_Api.Components.DMS.AI;

/// <summary>
/// Wires up the DMS AI feature. One call from Program.cs, so everything AI-related is registered
/// in one place and can be removed as cleanly as it was added.
/// </summary>
public static class AiServiceRegistration
{
    public static IServiceCollection AddDmsAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection("Ai:Gateway"));

        // The certificate pin applies to this named client only. Registering it here rather than
        // on a shared client is what keeps the relaxed chain validation from leaking into any
        // other outbound call the application makes.
        services.AddHttpClient(AiGatewayProvider.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AiGatewayProvider));
                return AiGatewayProvider.CreatePinnedHandler(options, logger);
            });

        services.AddSingleton<AiExecutionGate>();

        // Singleton: it caches the parsed policy files and checks their timestamps, so one instance
        // across the app is both correct and what makes the reload cheap.
        services.AddSingleton<AiPolicyStore>();
        services.AddScoped<IAiProvider, AiGatewayProvider>();
        services.AddScoped<AiProofreadService>();

        // Scoped, not singleton: the context builder resolves the CALLING user from the request
        // (ClientContextService), and that identity is the whole basis for what the assistant is
        // allowed to read. A singleton here would share one user's scope with everyone.
        services.AddScoped<AiDocumentContextBuilder>();
        services.AddScoped<AiAssistantService>();

        return services;
    }
}
