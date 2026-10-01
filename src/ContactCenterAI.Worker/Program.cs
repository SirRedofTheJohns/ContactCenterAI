using ContactCenterAI.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddContactCenterInfrastructure();
using var host = builder.Build();
// Durable jobs and leases are introduced in B04/B07; B02 runs no work.
if (args.SequenceEqual(["--verify-startup"]))
{
    await host.StartAsync();
    await host.StopAsync();
    Console.WriteLine("PASS: worker host starts and stops; no durable jobs configured in B02.");
    return;
}
await host.RunAsync();
