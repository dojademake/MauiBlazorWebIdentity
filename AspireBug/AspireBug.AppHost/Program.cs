var builder = DistributedApplication.CreateBuilder(args);

// BUG: NoGood.Maui has a period in its project name.
//
// Aspire's source generator should produce "Projects.NoGood_Maui" (replacing '.'
// with '_'), but this fails in practice because the source generator does not
// correctly handle the period in the MAUI project's assembly name.
//
// Expected compile result: CS0234 - The type or namespace name 'NoGood_Maui' does
// not exist in the namespace 'Projects' (are you missing an assembly reference?)
//
// This line compiles and runs correctly in AspireFine with "AllGood" (no period):
//   builder.AddProject<Projects.AllGood>("allgood");
//
// This line FAILS to compile (or fails at runtime) with "NoGood.Maui" (has period):
builder.AddProject<Projects.NoGood_Maui>("nogood-maui");

builder.Build().Run();
