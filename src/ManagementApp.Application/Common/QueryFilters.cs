namespace ManagementApp.Application.Common;

/// <summary>Names of the query filters that sit next to the tenant filter.</summary>
public static class QueryFilters
{
    /// <summary>Hides soft-deleted master data (customers, products).</summary>
    public const string NotDeleted = "NotDeleted";
}
