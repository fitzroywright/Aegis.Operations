using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class OperationsTelemetryAuthenticatorTests
{
    [Fact]
    public async Task Successful_validation_does_not_authenticate_another_credential_with_the_same_identifiers()
    {
        using var authority = new Authority(HttpStatusCode.Unauthorized);
        var authenticator = Create(authority);

        Assert.True(await authenticator.IsAuthorizedAsync(Request("valid-credential"), "Aegis.Studio", default));
        Assert.False(await authenticator.IsAuthorizedAsync(Request("invalid-credential"), "Aegis.Studio", default));
        Assert.Equal(2, authority.Calls);
    }

    [Theory]
    [InlineData("VALID-credential")]
    [InlineData("valid-CREDENTIAL")]
    public async Task Credential_comparison_is_case_sensitive(string credential)
    {
        using var authority = new Authority(HttpStatusCode.Unauthorized);
        var authenticator = Create(authority);

        Assert.True(await authenticator.IsAuthorizedAsync(Request("valid-credential"), "Aegis.Studio", default));
        Assert.False(await authenticator.IsAuthorizedAsync(Request(credential), "Aegis.Studio", default));
    }

    [Fact]
    public async Task Authority_outage_does_not_allow_an_unvalidated_credential_to_borrow_a_valid_identity()
    {
        using var authority = new Authority(HttpStatusCode.ServiceUnavailable);
        var authenticator = Create(authority);

        Assert.True(await authenticator.IsAuthorizedAsync(Request("valid-credential"), "Aegis.Studio", default));
        Assert.False(await authenticator.IsAuthorizedAsync(Request("unknown-credential"), "Aegis.Studio", default));
    }

    [Fact]
    public async Task Repeated_valid_credential_uses_the_existing_validation_cache()
    {
        using var authority = new Authority(HttpStatusCode.Unauthorized);
        var authenticator = Create(authority);

        Assert.True(await authenticator.IsAuthorizedAsync(Request("valid-credential"), "Aegis.Studio", default));
        Assert.True(await authenticator.IsAuthorizedAsync(Request("valid-credential"), "Aegis.Studio", default));
        Assert.Equal(1, authority.Calls);
    }

    [Theory]
    [InlineData("X-Aegis-Instance-Id")]
    [InlineData("X-Aegis-Installation-Id")]
    [InlineData("X-Aegis-Runtime-Session-Id")]
    public async Task Validation_cache_is_scoped_to_each_machine_identity(string changedHeader)
    {
        using var authority = new Authority(HttpStatusCode.Unauthorized);
        var authenticator = Create(authority);
        Assert.True(await authenticator.IsAuthorizedAsync(Request("valid-credential"), "Aegis.Studio", default));
        DefaultHttpContext changed = Request("valid-credential");
        changed.Request.Headers[changedHeader] = "another-value";
        Assert.True(await authenticator.IsAuthorizedAsync(changed, "Aegis.Studio", default));
        Assert.Equal(2, authority.Calls);
    }

    [Fact]
    public async Task Identifiers_without_a_credential_never_authenticate()
    {
        using var authority = new Authority(HttpStatusCode.Unauthorized);
        var authenticator = Create(authority);
        DefaultHttpContext request = Request("valid-credential");
        request.Request.Headers.Remove("Authorization");
        Assert.False(await authenticator.IsAuthorizedAsync(request, "Aegis.Studio", default));
        Assert.Equal(0, authority.Calls);
    }

    private static OperationsTelemetryAuthenticator Create(Authority authority) => new(
        authority,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ControlPlane:Internal:Configuration"] = "https://configuration.example.test",
            ["ControlPlane:Internal:Operations"] = "https://operations.example.test"
        }).Build(),
        NullLogger<OperationsTelemetryAuthenticator>.Instance);

    private static DefaultHttpContext Request(string credential)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Aegis-Application-Id"] = "Aegis.Studio";
        context.Request.Headers["X-Aegis-Instance-Id"] = "instance-one";
        context.Request.Headers["X-Aegis-Installation-Id"] = "installation-one";
        context.Request.Headers["X-Aegis-Runtime-Session-Id"] = "runtime-one";
        context.Request.Headers.Authorization = "Bearer " + credential;
        return context;
    }

    private sealed class Authority(HttpStatusCode invalidStatus) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(
                request.Headers.Authorization?.Parameter == "valid-credential" ? HttpStatusCode.OK : invalidStatus));
        }
    }
}
