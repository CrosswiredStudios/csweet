using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddCSweetAgent<PersonalAssistantAgent>();

var host = builder.Build();
host.Run();
