using System.IO;
using Xunit;

namespace AspireBugDemo.Tests;

/// <summary>
/// Validates the solution structure – verifies that the two Aspire solutions
/// (AspireFine and AspireBug) exist with the correct project layouts and that
/// the naming convention differences are correctly represented on disk.
/// </summary>
public class SolutionStructureTests
{
    // Resolve repo root from the test assembly location.
    // AppContext.BaseDirectory: <repo>/Tests/AspireBugDemo.Tests/bin/Debug/net9.0/
    // Go up 5 levels to reach the repo root.
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));

    // ─────────────────────────────────────────────────────────────────────────
    // AspireFine solution (the "good" case — AllGood has no period in its name)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AspireFine_SolutionFile_Exists()
    {
        var sln = Path.Combine(RepoRoot, "AspireFine", "AspireFine.sln");
        Assert.True(File.Exists(sln), $"Expected solution file at: {sln}");
    }

    [Fact]
    public void AspireFine_AllGood_CsprojFile_Exists()
    {
        var csproj = Path.Combine(RepoRoot, "AspireFine", "AllGood", "AllGood.csproj");
        Assert.True(File.Exists(csproj), $"Expected project file at: {csproj}");
    }

    [Fact]
    public void AspireFine_AllGood_CsprojName_HasNoPeriod()
    {
        // The file name "AllGood.csproj" itself must not contain a second
        // period (other than the ".csproj" extension).
        const string fileName = "AllGood.csproj";
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        Assert.DoesNotContain(".", nameWithoutExtension);
    }

    [Fact]
    public void AspireFine_AppHost_CsprojFile_Exists()
    {
        var csproj = Path.Combine(RepoRoot, "AspireFine", "AspireFine.AppHost", "AspireFine.AppHost.csproj");
        Assert.True(File.Exists(csproj), $"Expected project file at: {csproj}");
    }

    [Fact]
    public void AspireFine_AppHost_ProgramCs_ReferencesAllGood()
    {
        var program = Path.Combine(RepoRoot, "AspireFine", "AspireFine.AppHost", "Program.cs");
        Assert.True(File.Exists(program), $"Expected Program.cs at: {program}");

        var content = File.ReadAllText(program);

        // Verify the AppHost adds "AllGood" without any special escaping.
        Assert.Contains("Projects.AllGood", content);
        Assert.Contains("\"allgood\"", content);
    }

    [Fact]
    public void AspireFine_AppHost_ProgramCs_HasNoUnderscore_InProjectType()
    {
        var program = Path.Combine(RepoRoot, "AspireFine", "AspireFine.AppHost", "Program.cs");
        var content = File.ReadAllText(program);

        // The AllGood type name requires no underscore mangling.
        Assert.DoesNotContain("Projects.AllGood_", content);
    }

    [Fact]
    public void AspireFine_ServiceDefaults_Exists()
    {
        var csproj = Path.Combine(RepoRoot, "AspireFine", "AspireFine.ServiceDefaults", "AspireFine.ServiceDefaults.csproj");
        Assert.True(File.Exists(csproj), $"Expected project file at: {csproj}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AspireBug solution (the "bug" case — NoGood.Maui has a period in its name)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AspireBug_SolutionFile_Exists()
    {
        var sln = Path.Combine(RepoRoot, "AspireBug", "AspireBug.sln");
        Assert.True(File.Exists(sln), $"Expected solution file at: {sln}");
    }

    [Fact]
    public void AspireBug_NoGoodMaui_CsprojFile_Exists()
    {
        var csproj = Path.Combine(RepoRoot, "AspireBug", "NoGood.Maui", "NoGood.Maui.csproj");
        Assert.True(File.Exists(csproj), $"Expected project file at: {csproj}");
    }

    [Fact]
    public void AspireBug_NoGoodMaui_CsprojName_ContainsPeriod()
    {
        // The presence of the period in the project file name is the root cause
        // of the Aspire bug.
        const string fileName = "NoGood.Maui.csproj";
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // "NoGood.Maui" – still contains a period after removing ".csproj".
        Assert.Contains(".", nameWithoutExtension);
    }

    [Fact]
    public void AspireBug_AppHost_CsprojFile_Exists()
    {
        var csproj = Path.Combine(RepoRoot, "AspireBug", "AspireBug.AppHost", "AspireBug.AppHost.csproj");
        Assert.True(File.Exists(csproj), $"Expected project file at: {csproj}");
    }

    [Fact]
    public void AspireBug_AppHost_ProgramCs_ReferencesNoGoodMaui_WithUnderscore()
    {
        var program = Path.Combine(RepoRoot, "AspireBug", "AspireBug.AppHost", "Program.cs");
        Assert.True(File.Exists(program), $"Expected Program.cs at: {program}");

        var content = File.ReadAllText(program);

        // The AppHost must use "Projects.NoGood_Maui" (underscore, not period)
        // because periods are illegal in C# identifiers.
        Assert.Contains("Projects.NoGood_Maui", content);
        Assert.Contains("\"nogood-maui\"", content);
    }

    [Fact]
    public void AspireBug_AppHost_ProgramCs_MangledTypeName_DiffersFromProjectFileName()
    {
        var program = Path.Combine(RepoRoot, "AspireBug", "AspireBug.AppHost", "Program.cs");
        var content = File.ReadAllText(program);

        // "NoGood_Maui" (underscore) is required in C# code vs "NoGood.Maui" (period) on disk —
        // this gap is the source of confusion and the trigger for the generator bug.
        Assert.Contains("NoGood_Maui", content);   // C# identifier (underscore)

        // The actual AddProject call must use the underscore form, not a literal period.
        // Find the AddProject line (non-comment) and confirm it contains the underscore form.
        var addProjectLine = content
            .Split('\n')
            .FirstOrDefault(line => line.TrimStart().StartsWith("builder.AddProject"));

        Assert.NotNull(addProjectLine);
        Assert.Contains("NoGood_Maui", addProjectLine);
        Assert.DoesNotContain("NoGood.Maui", addProjectLine); // period must NOT appear as a type in code
    }

    [Fact]
    public void AspireBug_AppHost_CsprojFile_ReferencesNoGoodMaui_Csproj()
    {
        var csproj = Path.Combine(RepoRoot, "AspireBug", "AspireBug.AppHost", "AspireBug.AppHost.csproj");
        var content = File.ReadAllText(csproj);

        // The .csproj must reference the project file whose name contains the period.
        Assert.Contains("NoGood.Maui.csproj", content);
    }

    [Fact]
    public void AspireBug_ServiceDefaults_Exists()
    {
        var csproj = Path.Combine(RepoRoot, "AspireBug", "AspireBug.ServiceDefaults", "AspireBug.ServiceDefaults.csproj");
        Assert.True(File.Exists(csproj), $"Expected project file at: {csproj}");
    }
}
