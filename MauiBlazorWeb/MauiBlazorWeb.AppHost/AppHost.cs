var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.MauiBlazorWeb_Web>("apiservice");

apiService.WithUrlForEndpoint("https", url =>
{
    url.DisplayText = "MauiBlazorWeb API Swagger UI";
    url.Url = "/swagger";
});

builder.AddProject<Projects.MauiBlazorWeb_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
