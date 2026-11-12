using ManagementApp.Application.Common.Interfaces;

namespace ManagementApp.Application.UnitTests.Common;

public class FileStorageKeyTests
{
    private static readonly Guid TenantId = Guid.Parse("6f1c2a4e-0d3b-4c8a-9e57-1b2d3c4e5f60");

    [Theory]
    [InlineData("invoices/42/INV-0001.pdf")]
    [InlineData("logo.png")]
    [InlineData("a_b-c.d/e")]
    public void ValidPaths_GoUnderTheTenantsPrefix(string path)
    {
        FileStorageKey.For(TenantId, path).ShouldBe($"tenants/6f1c2a4e-0d3b-4c8a-9e57-1b2d3c4e5f60/{path}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/invoices/1.pdf")]
    [InlineData("invoices//1.pdf")]
    [InlineData("invoices/1.pdf/")]
    [InlineData("../other-tenant/secret.pdf")]
    [InlineData("invoices/../../escape.pdf")]
    [InlineData("./invoice.pdf")]
    [InlineData(@"invoices\1.pdf")]
    [InlineData("C:/windows/win.ini")]
    [InlineData("invoices/name with spaces.pdf")]
    [InlineData("tenants/%2e%2e/escape")]
    public void PathsThatCouldEscapeOrAreMalformed_AreRefused(string path)
    {
        Should.Throw<ArgumentException>(() => FileStorageKey.For(TenantId, path));
    }

    [Fact]
    public void OverlongPaths_AreRefused()
    {
        Should.Throw<ArgumentException>(() => FileStorageKey.For(TenantId, new string('a', FileStorageKey.MaxPathLength + 1)));
    }

    [Fact]
    public void FilesMustBelongToATenant()
    {
        Should.Throw<ArgumentException>(() => FileStorageKey.For(Guid.Empty, "logo.png"));
        Should.Throw<ArgumentException>(() => FileStorageKey.TenantPrefix(Guid.Empty));
    }
}
