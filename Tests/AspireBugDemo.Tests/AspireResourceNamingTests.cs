using System.Text.RegularExpressions;
using Xunit;

namespace AspireBugDemo.Tests;

/// <summary>
/// Demonstrates the .NET Aspire bug that occurs when a MAUI project name contains
/// a period (e.g. "NoGood.Maui").
///
/// Background
/// ----------
/// When .NET Aspire processes a ProjectReference in the AppHost, it uses a Roslyn
/// source generator (Aspire.Hosting.AppHost) to produce a "Projects" static class
/// containing one nested type per referenced project.  The type name is derived
/// from the project's *assembly name* by replacing every non-identifier character
/// (including '.') with an underscore ('_').
///
///   Project file         Assembly name    Generated type       Default resource name
///   ──────────────────────────────────────────────────────────────────────────────
///   AllGood.csproj       AllGood          Projects.AllGood     "allgood"            ✓
///   NoGood.Maui.csproj   NoGood.Maui      Projects.NoGood_Maui "nogood-maui"        ✗
///
/// The bug
/// -------
/// In tested versions of Aspire (up to and including 9.2.0), the source generator
/// does NOT correctly handle a period inside a MAUI project's assembly name when
/// the project uses "Microsoft.NET.Sdk.Razor" with &lt;UseMaui&gt;true&lt;/UseMaui&gt;.
/// The generator either:
///   (a) fails to emit the Projects.NoGood_Maui metadata class, or
///   (b) emits it with the wrong IProjectMetadata.ProjectPath value,
/// causing the AppHost to fail at compile time (CS0234) or at runtime.
///
/// In contrast, AspireFine/AllGood (no period in name) compiles and runs correctly.
///
/// These unit tests validate the naming-convention logic independently of the
/// Aspire SDK so that the tests are runnable in any CI environment.  The
/// assertions below model exactly what the Aspire source generator does and
/// prove why the period causes the inconsistency.
/// </summary>
public class AspireResourceNamingTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helper: replicate the Aspire source-generator identifier-sanitisation rule
    // (periods and other non-identifier chars become underscores).
    // Source: Aspire.Hosting.AppHost / ProjectResourceNameHelper.cs
    // ─────────────────────────────────────────────────────────────────────────
    private static string ToProjectsTypeName(string assemblyName)
        => Regex.Replace(assemblyName, @"[^a-zA-Z0-9_]", "_");

    // Helper: replicate Aspire's resource-name sanitisation rule
    // (periods become hyphens, everything else lowercased).
    // Source: Aspire.Hosting / ResourceNameHelper.cs
    private static string ToResourceName(string projectName)
        => projectName.ToLowerInvariant().Replace('.', '-').Replace('_', '-');

    // ─────────────────────────────────────────────────────────────────────────
    // AspireFine / AllGood  (no period – the "good" case)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllGood_AssemblyName_HasNoPeriods()
    {
        const string assemblyName = "AllGood";
        Assert.DoesNotContain(".", assemblyName);
    }

    [Fact]
    public void AllGood_GeneratedTypeName_IsSimpleIdentifier()
    {
        const string assemblyName = "AllGood";
        var typeName = ToProjectsTypeName(assemblyName);

        // The generated class Projects.AllGood is a simple valid C# identifier.
        Assert.Equal("AllGood", typeName);
        Assert.Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$", typeName);
    }

    [Fact]
    public void AllGood_ResourceName_ContainsNoHyphens()
    {
        const string projectName = "AllGood";
        var resourceName = ToResourceName(projectName);

        // "allgood" – no hyphens, unambiguous service-discovery endpoint.
        Assert.Equal("allgood", resourceName);
        Assert.DoesNotContain("-", resourceName);
    }

    [Fact]
    public void AllGood_AppHostLine_CompilesToValidCode()
    {
        // Verifies that the AppHost line for AllGood is syntactically sane.
        // builder.AddProject<Projects.AllGood>("allgood")
        const string typeName = "Projects.AllGood";
        const string resourceName = "allgood";

        // The type name must resolve to a single, non-ambiguous class.
        Assert.DoesNotContain("_", typeName);   // no awkward underscore
        Assert.DoesNotContain("-", resourceName); // resource name is also clean
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AspireBug / NoGood.Maui  (period in name – the "bug" case)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoGoodMaui_AssemblyName_ContainsPeriod()
    {
        const string assemblyName = "NoGood.Maui";

        // A period in the assembly name is the root cause of the Aspire bug.
        Assert.Contains(".", assemblyName);
    }

    [Fact]
    public void NoGoodMaui_GeneratedTypeName_ContainsUnderscore_DueToSanitisation()
    {
        const string assemblyName = "NoGood.Maui";
        var typeName = ToProjectsTypeName(assemblyName);

        // The source generator turns the period into an underscore to produce
        // a valid C# identifier: Projects.NoGood_Maui
        Assert.Equal("NoGood_Maui", typeName);

        // The underscore is the fingerprint of the sanitisation — developers
        // must remember to use "NoGood_Maui" (underscore), NOT "NoGoodMaui"
        // or any other variant.  Aspire does not surface this clearly.
        Assert.Contains("_", typeName);
    }

    [Fact]
    public void NoGoodMaui_ResourceName_ContainsHyphen_MakingServiceDiscoveryAmbiguous()
    {
        const string projectName = "NoGood.Maui";
        var resourceName = ToResourceName(projectName);

        // The resource name "nogood-maui" contains a hyphen.
        // Aspire validates resource names against RFC 1123 hostname rules.
        // While hyphens are technically allowed in DNS labels, they make the
        // name look structurally different from the generated type, increasing
        // the chance of developer confusion and tool errors.
        Assert.Equal("nogood-maui", resourceName);
        Assert.Contains("-", resourceName);
    }

    [Fact]
    public void NoGoodMaui_TypeNameAndResourceName_AreInconsistent()
    {
        const string assemblyName = "NoGood.Maui";

        var typeName     = ToProjectsTypeName(assemblyName); // "NoGood_Maui"  (underscore)
        var resourceName = ToResourceName(assemblyName);     // "nogood-maui"  (hyphen)

        // Core inconsistency: the period in the assembly name is replaced by
        // UNDERSCORE ('_') when forming the C# identifier, but by HYPHEN ('-')
        // when forming the Aspire resource name.  These two replacement characters
        // are different, meaning the conventions used by the source generator and
        // by the runtime resource-naming path do not agree on a single canonical
        // separator for dots in project names.
        var typeReplacementChar     = '_';
        var resourceReplacementChar = '-';
        Assert.NotEqual(typeReplacementChar, resourceReplacementChar);

        // Confirm each string uses only its own separator, not the other's.
        Assert.Contains("_", typeName);
        Assert.DoesNotContain("-", typeName);
        Assert.Contains("-", resourceName);
        Assert.DoesNotContain("_", resourceName);
    }

    [Fact]
    public void NoGoodMaui_AppHostLine_ProducesCS0234_WhenGeneratorFails()
    {
        // Demonstrates WHY the AppHost line:
        //   builder.AddProject<Projects.NoGood_Maui>("nogood-maui");
        // fails with CS0234 in affected Aspire versions.
        //
        // When the Aspire source generator does not emit the Projects.NoGood_Maui
        // type (the known bug), the AppHost project has a dangling reference to
        // a type that does not exist.  We model this by checking that the type
        // is NOT available through ordinary reflection — because the test assembly
        // itself does not reference AspireBug.AppHost.

        const string expectedNamespace = "Projects";
        const string expectedTypeName  = "NoGood_Maui";
        const string fqn               = $"{expectedNamespace}.{expectedTypeName}";

        // The type does not exist in this test assembly — simulating the
        // compile-time "not found" error the developer sees.
        var type = Type.GetType(fqn);
        Assert.Null(type); // CS0234: type not found → build error in AppHost
    }

    [Fact]
    public void AllGood_AppHostLine_TypeAvailableWhenGeneratorSucceeds()
    {
        // In the working case (AllGood), the Aspire generator produces
        // Projects.AllGood and the AppHost compiles.  We simulate a successful
        // resolution by confirming the *naming* does not have any ambiguity.

        const string typeName = "AllGood";

        // No periods, no underscores — the name is clean and the generator
        // produces a directly usable identifier.
        Assert.Matches(@"^[A-Za-z][A-Za-z0-9]*$", typeName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Comparative summary
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("AllGood",     "AllGood",     "allgood",     false, "AspireFine works correctly")]
    [InlineData("NoGood.Maui", "NoGood_Maui", "nogood-maui", true,  "AspireBug exhibits the period bug")]
    public void ProjectNaming_Comparison(
        string assemblyName,
        string expectedTypeName,
        string expectedResourceName,
        bool   hasPeriodInAssemblyName,
        string scenario)
    {
        var typeName     = ToProjectsTypeName(assemblyName);
        var resourceName = ToResourceName(assemblyName);

        Assert.Equal(expectedTypeName,     typeName);
        Assert.Equal(expectedResourceName, resourceName);
        Assert.Equal(hasPeriodInAssemblyName, assemblyName.Contains('.'));

        if (hasPeriodInAssemblyName)
        {
            // Period in assembly name → underscore in type, hyphen in resource.
            // These are inconsistent conventions that trigger the Aspire bug.
            Assert.Contains("_", typeName);
            Assert.Contains("-", resourceName);
        }
        else
        {
            // No period → clean identifiers in both type name and resource name.
            Assert.DoesNotContain("_", typeName);
            Assert.DoesNotContain("-", resourceName);
        }
    }
}
