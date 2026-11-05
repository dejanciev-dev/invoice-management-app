namespace ManagementApp.Application.Common.Exceptions;

/// <summary>The user is signed in but may not do this, e.g. because of their role or the tenant's subscription.</summary>
public class ForbiddenException(string message) : Exception(message);
