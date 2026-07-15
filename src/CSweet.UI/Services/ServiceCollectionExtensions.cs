using Microsoft.Extensions.DependencyInjection;

namespace CSweet.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCSweetApiClients(this IServiceCollection services)
    {
        services.AddScoped<ISetupApiClient, SetupApiClient>();
        services.AddScoped<ILlmProviderApiClient, LlmProviderApiClient>();
        services.AddScoped<IAgentRuntimeSettingsApiClient, AgentRuntimeSettingsApiClient>();
        services.AddScoped<IOrganizationApiClient, OrganizationApiClient>();
        services.AddScoped<IBusinessOnboardingApiClient, BusinessOnboardingApiClient>();
        services.AddScoped<IPlanningApiClient, PlanningApiClient>();
        services.AddScoped<IChatApiClient, ChatApiClient>();
        services.AddScoped<IAgentApiClient, AgentApiClient>();
        services.AddScoped<AuthSessionStore>();
        services.AddScoped<IAuthenticationApiClient, AuthenticationApiClient>();

        return services;
    }
}
