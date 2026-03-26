var builder = DistributedApplication.CreateBuilder(args);

// AllGood has no period in its project name.
// Aspire source-generates "Projects.AllGood" and assigns resource name "allgood".
// This compiles and runs correctly.
builder.AddProject<Projects.AllGood>("allgood");

builder.Build().Run();
