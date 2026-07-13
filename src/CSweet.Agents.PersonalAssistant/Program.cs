using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant;
using CSweet.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();
builder.Services.AddSingleton<IAgentLlmClientFactory, PersonalAssistantLlmClientFactory>();
builder.AddCSweetAgent<PersonalAssistantAgent>();

var host = builder.Build();
host.Run();
