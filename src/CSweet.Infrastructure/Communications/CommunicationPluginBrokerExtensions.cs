using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Communications.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CSweet.Infrastructure.Communications;

public static class CommunicationPluginBrokerExtensions
{
    public static IServiceCollection AddCommunicationPluginBroker(this IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration["CSweet:CommunicationPlugins:BrokerEndpoint"] ?? "https+http://agenthost";
        services.AddGrpcClient<AgentBroker.AgentBrokerClient>(options => options.Address = Normalize(configured));
        services.AddTransient<GrpcAgentBrokerClient>();
        services.AddSingleton<CommunicationPluginBrokerConnection>();
        services.AddSingleton<ICommunicationPluginClient>(sp => sp.GetRequiredService<CommunicationPluginBrokerConnection>());
        services.AddHostedService<CommunicationPluginBrokerWorker>();
        return services;
    }

    private static Uri Normalize(string value)
    {
        var endpoint = new Uri(value, UriKind.Absolute);
        if (endpoint.Scheme is "http" or "https") return endpoint;
        var scheme = endpoint.Scheme.Split('+').FirstOrDefault(x => x is "http" or "https")
            ?? throw new InvalidOperationException("Communication plugin broker endpoint must use HTTP or HTTPS.");
        return new Uri($"{scheme}://{endpoint.Authority}{endpoint.PathAndQuery}");
    }
}
