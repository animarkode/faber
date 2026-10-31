using Faber.Bootstrap;
using Faber.Bootstrap.Clients;
using Faber.Bootstrap.Workflow;
using Faber.ServiceDefaults;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSerilog((_, config) => config.WriteTo.Console(), writeToProviders: true);

builder.Services.AddOptions<BootstrapOptions>()
    .BindConfiguration(BootstrapOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<KeycloakAdminClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BootstrapOptions>>().Value;
    client.BaseAddress = new Uri(options.KeycloakAddress.TrimEnd('/') + "/");
});

builder.Services.AddSingleton<IBootstrapWorkflow, BootstrapWorkflow>();
builder.Services.AddHostedService<BootstrapWorker>();

builder.Build().Run();
