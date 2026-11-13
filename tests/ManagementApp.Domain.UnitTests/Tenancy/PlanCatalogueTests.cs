using ManagementApp.Domain.Tenancy;
using System.Text.RegularExpressions;

namespace ManagementApp.Domain.UnitTests.Tenancy;

public partial class PlanCatalogueTests
{
    [Fact]
    public void TheCatalogue_HasTheDecidedPlans()
    {
        SubscriptionPlans.Currency.ShouldBe("EUR");
        SubscriptionPlans.All.Select(p => (p.Code, p.MaxUsers, p.MaxInvoicesPerMonth, p.MonthlyPrice, p.YearlyPrice)).ShouldBe(
        [
            ("free", 1, 10, 0m, 0m),
            ("trial", 5, 100, 0m, 0m),
            ("starter", 3, 50, 9m, 90m),
            ("professional", 10, 500, 19m, 190m),
            ("business", 25, 2000, 39m, 390m),
        ]);
        SubscriptionPlans.Free.Features.ShouldBe(PlanFeature.None);
        SubscriptionPlans.Starter.Features.ShouldBe(PlanFeature.CsvExport);
    }

    /// <summary>Plans are only defined in the catalogue; code refers to them through <see cref="SubscriptionPlans"/>.</summary>
    [Fact]
    public void PlanCodes_AreOnlyWrittenInTheCatalogue()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var codes = string.Join('|', SubscriptionPlans.All.Select(p => Regex.Escape(p.Code)).Append("enterprise"));
        var literal = new Regex($"[\"']({codes})[\"']");

        var offenders = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".ts", StringComparison.Ordinal) || path.EndsWith(".tsx", StringComparison.Ordinal))
            .Where(path => !IsExcluded(path))
            .Where(path => literal.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(src, path))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    private static bool IsExcluded(string path) =>
        path.EndsWith(Path.Combine("Tenancy", "Subscription.cs"), StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains(".test.", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ManagementApp.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root (with ManagementApp.slnx) was not found.");
    }
}
