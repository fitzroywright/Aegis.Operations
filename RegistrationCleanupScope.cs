internal sealed record RegistrationCleanupScope(string ApplicationId, bool IncludeChildren)
{
    internal bool Matches(string applicationId) =>
        applicationId.Equals(ApplicationId, StringComparison.OrdinalIgnoreCase) ||
        (IncludeChildren && applicationId.StartsWith(ApplicationId + ".", StringComparison.OrdinalIgnoreCase));

    internal bool IsValid =>
        !string.IsNullOrWhiteSpace(ApplicationId) &&
        ApplicationId.Split('.').All(part => part.Length > 0 &&
            part.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'));

    internal bool IsProtected => new[] { "Aegis.Operations", "Aegis.Configuration", "Aegis.Diagnostics" }
        .Any(Matches);
}
