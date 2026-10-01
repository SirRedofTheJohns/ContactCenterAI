using ContactCenterAI.Simulator;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:7453");
var store = new ReservationStore(Path.Combine(builder.Environment.ContentRootPath, ".local", "source", "reservations.db"), TimeProvider.System);
await using var app = SourceApi.Build(builder, store);
await app.RunAsync();
