namespace ManagementApp.Domain.Common;

/// <summary>
/// The request is valid on its own but breaks a business rule given the entity's current state, e.g. editing an issued
/// invoice. Reported to clients as a conflict.
/// </summary>
public class DomainRuleException(string message) : Exception(message);
