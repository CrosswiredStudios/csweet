using CSweet.Agent.Contracts.Grpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CSweet.Agent.SDK;

public static class DependencyInjection
{
    private static readonly HashSet<string> SupportedGrpcSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttps,
        Uri.UriSchemeHttp
    };

    public static IHostApplicationBuilder AddCSweetAgent<TAgent>(
        this IHostApplicationBuilder builder)
        where TAgent : class, ICSweetAgent
    {
        var section = builder.Configuration.GetSection(AgentBrokerOptions.SectionName);
        var brokerEndpoint = section[nameof(AgentBrokerOptions.BrokerEndpoint)]
            ?? "https+http://agenthost";

        builder.Services
            .AddOptions<AgentBrokerOptions>()
            .Bind(section)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.InstallationId),
                "CSweet:Agent:InstallationId is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.BusinessId),
                "CSweet:Agent:BusinessId is required.")
            .ValidateOnStart();

        builder.Services.AddGrpcClient<AgentBroker.AgentBrokerClient>(options =>
        {
            options.Address = CreateGrpcAddress(brokerEndpoint);
        });

        builder.Services.AddSingleton<TAgent>();
        builder.Services.AddTransient<GrpcAgentBrokerClient>();
        builder.Services.AddTransient<IAgentBrokerClient, GrpcAgentBrokerClient>();
        builder.Services.AddHostedService<AgentRuntimeWorker<TAgent>>();

        return builder;
    }

    internal static Uri CreateGrpcAddress(string brokerEndpoint)
    {
        var endpoint = new Uri(brokerEndpoint.Trim(), UriKind.Absolute);
        if (SupportedGrpcSchemes.Contains(endpoint.Scheme))
        {
            return endpoint;
        }

        var scheme = endpoint.Scheme
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(SupportedGrpcSchemes.Contains);

        if (scheme is null)
        {
            throw new InvalidOperationException(
                $"Agent broker endpoint scheme '{endpoint.Scheme}' is not supported. Use http, https, or an Aspire composite scheme such as https+http.");
        }

        return new Uri($"{scheme}://{endpoint.Authority}{endpoint.PathAndQuery}{endpoint.Fragment}", UriKind.Absolute);
    }
}
