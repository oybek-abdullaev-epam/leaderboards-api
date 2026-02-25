using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Aspire injects Service Bus as ConnectionStrings:service-bus, but the Functions isolated
// worker trigger binding looks for service-bus__connectionString (new-style SDK format).
var serviceBusConn = builder.Configuration.GetConnectionString("service-bus");
if (!string.IsNullOrEmpty(serviceBusConn))
    builder.Configuration["service-bus__connectionString"] = serviceBusConn;

builder.Build().Run();
