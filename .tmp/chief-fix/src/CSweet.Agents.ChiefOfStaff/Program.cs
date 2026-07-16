using CSweet.Agent.SDK;
using CSweet.Agents.ChiefOfStaff;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddCSweetAgent<ChiefOfStaffAgent>();

var host = builder.Build();
host.Run();
