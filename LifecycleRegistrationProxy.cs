using Common.Security;
using System.Collections.Concurrent;
using System.Net;

internal static class LifecycleRegistrationProxy
{
    private sealed record CachedAuthorityResponse(string Content, string ContentType, int StatusCode, DateTimeOffset CapturedAtUtc);
    private static readonly ConcurrentDictionary<string, CachedAuthorityResponse> LastKnown = new(StringComparer.OrdinalIgnoreCase);
    internal static void MapLifecycleRegistrationProxy(this WebApplication app, string configurationUrl)
    {
        app.MapGet("/api/lifecycle-registrations", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.View")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/requests",
                HttpMethod.Get,
                null,
                ct);
        });

        app.MapGet("/api/lifecycle-registrations/flow", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.View")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/flow?limit=200",
                HttpMethod.Get,
                null,
                ct);
        });

        app.MapGet("/api/lifecycle-registrations/reconciliation", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/reconciliation",
                HttpMethod.Get,
                null,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/approve", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/approve",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/reject", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/reject",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/revoke", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/revoke",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapDelete("/api/lifecycle-registrations/{registrationId}", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId),
                HttpMethod.Delete,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/remove-orphan", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/remove-orphan",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/conflict/retain-existing", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/conflict/retain-existing",
                HttpMethod.Post,
                request,
                ct);
        });


        app.MapPost("/api/lifecycle-registrations/{registrationId}/conflict/replace-with-incoming", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" +
                Uri.EscapeDataString(registrationId) +
                "/conflict/replace-with-incoming",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapGet("/api/lifecycle-registrations/cleanup-targets", async (
            HttpContext context, SuiteSecurity suiteSecurity, IHttpClientFactory factory, CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.StatusCode(403);
            return await ForwardAsync(context, suiteSecurity, factory,
                configurationUrl.TrimEnd('/') + "/api/registration/cleanup-targets",
                HttpMethod.Get, null, ct);
        });
        app.MapGet("/api/lifecycle-registrations/cleanup-preview", async (
            HttpContext context, string applicationId, bool includeChildren,
            SuiteSecurity suiteSecurity, IHttpClientFactory factory, CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.StatusCode(403);
            return await ForwardAsync(context, suiteSecurity, factory,
                configurationUrl.TrimEnd('/') + "/api/registration/cleanup-preview?applicationId=" +
                Uri.EscapeDataString(applicationId) + "&includeChildren=" + (includeChildren ? "true" : "false"),
                HttpMethod.Get, null, ct);
        });

        app.MapPost("/api/lifecycle-registrations/cleanup", async (
            HttpContext context,
            [Microsoft.AspNetCore.Mvc.FromBody] ApplicationCleanup request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Json(new { error = "Configuration.Manage permission is required." }, statusCode: 403);
            if (context.Request.Headers["X-Aegis-Action"] != "registration-cleanup")
                return Results.BadRequest(new { error = "Explicit cleanup action header is required." });
            var scope = new RegistrationCleanupScope(request.ApplicationId ?? "", request.IncludeChildren);
            if (!scope.IsValid || scope.IsProtected || request.Confirmation != request.ApplicationId ||
                !request.ApplicationsStopped || string.IsNullOrWhiteSpace(request.Reason) ||
                request.ExpectedRegistrationIds is null || request.ExpectedContracts is null)
                return Results.BadRequest(new { error = "Review the preview, confirm the Application ID, stop its applications, and provide a reason." });
            return await ForwardAsync(
                context, suiteSecurity, factory,
                configurationUrl.TrimEnd('/') + "/api/registration/cleanup",
                HttpMethod.Post, request, ct,
                async token => await ApplicationControlPlaneCleanup.ClearCurrentStateAsync(store, scope, token));
        });

        app.MapGet("/registration-lifecycle", () => Results.Redirect("/registration/lifecycle", permanent: true));
    }

    private static async Task<IResult> ForwardAsync(
        HttpContext source,
        SuiteSecurity suiteSecurity,
        IHttpClientFactory factory,
        string url,
        HttpMethod method,
        object? body,
        CancellationToken ct,
        Func<CancellationToken, Task>? afterSuccess = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = System.Net.Http.Json.JsonContent.Create(body);

        if (!source.Items.TryGetValue("SuiteIdentity", out object? value) || value is not SuiteIdentity identity)
            return Results.Unauthorized();

        request.Headers.TryAddWithoutValidation(
            "X-Aegis-Suite-Handoff",
            suiteSecurity.CreateHandoff(identity, "Aegis.Configuration"));
        request.Headers.TryAddWithoutValidation(
            "X-Aegis-Correlation-Id",
            Guid.NewGuid().ToString("N"));

        if (source.Request.Headers["X-Aegis-Action"] == "registration-cleanup")
            request.Headers.TryAddWithoutValidation("X-Aegis-Action", "registration-cleanup");

        bool cacheable = method == HttpMethod.Get;
        string cacheKey = method.Method + " " + url;

        try
        {
            using HttpResponseMessage response = await factory
                .CreateClient("ops")
                .SendAsync(request, ct);

            string content = await response.Content.ReadAsStringAsync(ct);
            string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

            if (response.IsSuccessStatusCode && !cacheable)
            {
                LastKnown.Clear();
                if (afterSuccess is not null) await afterSuccess(ct);
            }

            if (cacheable && response.IsSuccessStatusCode)
            {
                LastKnown[cacheKey] = new CachedAuthorityResponse(
                    content,
                    contentType,
                    (int)response.StatusCode,
                    DateTimeOffset.UtcNow);
            }

            return Results.Content(
                content,
                contentType,
                statusCode: (int)response.StatusCode);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            if (cacheable && LastKnown.TryGetValue(cacheKey, out CachedAuthorityResponse? cached))
            {
                source.Response.Headers["X-Aegis-Authority-Status"] = "Unavailable";
                source.Response.Headers["X-Aegis-Data-Status"] = "Last-Known";
                source.Response.Headers["X-Aegis-Last-Success-Utc"] = cached.CapturedAtUtc.ToString("O");

                return Results.Content(
                    cached.Content,
                    cached.ContentType,
                    statusCode: cached.StatusCode);
            }

            source.Response.Headers["X-Aegis-Authority-Status"] = "Unavailable";
            source.Response.Headers["X-Aegis-Data-Status"] = "Unknown";

            return Results.Json(
                new
                {
                    error = "RegistrationAuthorityUnavailable",
                    detail = "Configuration Registration authority is temporarily unavailable and no last-known snapshot is available.",
                    authorityStatus = "Unavailable",
                    dataStatus = "Unknown"
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool Has(HttpContext context, string permission)
        => context.Items.TryGetValue("SuiteIdentity", out object? value) &&
           value is SuiteIdentity identity &&
           identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private sealed record LifecycleDecision(string? Reason);
    private sealed record ApplicationCleanup(
        string ApplicationId, bool IncludeChildren, string? Reason, string? Confirmation,
        bool ApplicationsStopped, string[]? ExpectedRegistrationIds, string[]? ExpectedContracts);
}
