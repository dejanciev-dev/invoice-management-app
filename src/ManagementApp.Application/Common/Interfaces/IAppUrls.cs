namespace ManagementApp.Application.Common.Interfaces;

/// <summary>Absolute links to pages of the web app, for emails and anything else read outside the browser.</summary>
public interface IAppUrls
{
    /// <summary>The page where an invited person accepts the invitation with this token.</summary>
    string AcceptInvitation(string token);
}
