namespace ManagementApp.Domain.Common;

public static class Money
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
