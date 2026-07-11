using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant;
using CSweet.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddCSweetInfrastructure();
builder.AddCSweetAgent<PersonalAssistantAgent>();

var host = builder.Build();
host.Run();
