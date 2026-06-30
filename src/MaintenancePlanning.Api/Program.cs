using MaintenancePlanning.Api;

// ApiApplication owns endpoint composition so Program stays as the thin runtime entry point.
var app = ApiApplication.Create(new WebApplicationOptions { Args = args });

await app.RunAsync();

public partial class Program;
