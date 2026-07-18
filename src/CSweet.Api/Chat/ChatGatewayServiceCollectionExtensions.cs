using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Api.Communications;
using CSweet.Application.Communications;

namespace CSweet.Api.Chat;

public static class ChatGatewayServiceCollectionExtensions
{
    private static readonly HashSet<string> SupportedGrpcSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttps,
        Uri.UriSchemeHttp
    };

    public static IServiceCollection AddChatGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ApiGatewayOptions>()
            .Bind(configuration.GetSection(ApiGatewayOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.AgentId), "CSweet:ApiGateway:AgentId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BrokerEndpoint), "CSweet:ApiGateway:BrokerEndpoint is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.InstallationId), "CSweet:ApiGateway:InstallationId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BusinessId), "CSweet:ApiGateway:BusinessId is required.")
            .ValidateOnStart();

        var brokerEndpoint = configuration[$"{ApiGatewayOptions.SectionName}:BrokerEndpoint"]
            ?? "https+http://agenthost";

        services.AddGrpcClient<AgentBroker.AgentBrokerClient>(options =>
        {
            options.Address = CreateGrpcAddress(brokerEndpoint);
        });

        services.AddTransient<GrpcAgentBrokerClient>();
        services.AddSingleton<ApiGatewayBrokerConnection>();
        services.AddSingleton<IAgentBrokerClient>(sp => sp.GetRequiredService<ApiGatewayBrokerConnection>());
        services.AddSingleton<IChatStreamRouter, ChatStreamRouter>();
        services.AddHostedService<ApiGatewayBrokerWorker>();
        services.AddSingleton<ICommunicationEventPublisher, BrokerCommunicationEventPublisher>();
        services.AddHostedService<CommunicationEventOutboxWorker>();

        return services;
    }

    private static Uri CreateGrpcAddress(string brokerEndpoint)
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
