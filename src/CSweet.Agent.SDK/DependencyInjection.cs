using CSweet.Agent.Contracts.Grpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CSweet.Agent.SDK;

public static class DependencyInjection
{
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
            options.Address = new Uri(brokerEndpoint, UriKind.Absolute);
        });

        builder.Services.AddSingleton<TAgent>();
        builder.Services.AddSingleton<IAgentBrokerClient, GrpcAgentBrokerClient>();
        builder.Services.AddHostedService<AgentRuntimeWorker<TAgent>>();

        return builder;
    }
}
