var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MauiBlazorWeb_Web>("mauiblazorweb-web");

builder.Build().Run();
