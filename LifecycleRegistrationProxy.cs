using Common.Security;

internal static class LifecycleRegistrationProxy
{
    internal static void MapLifecycleRegistrationProxy(this WebApplication app, string configurationUrl)
    {
        app.MapGet("/api/lifecycle-registrations", async (
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

        app.MapGet("/registration-lifecycle", () => Results.Redirect("/registration/lifecycle", permanent: true));
    }

    private static async Task<IResult> ForwardAsync(
        HttpContext source,
        SuiteSecurity suiteSecurity,
        IHttpClientFactory factory,
        string url,
        HttpMethod method,
        object? body,
        CancellationToken ct)
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

        using HttpResponseMessage response = await factory
            .CreateClient("ops")
            .SendAsync(request, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        return Results.Content(
            content,
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    }

    private static bool Has(HttpContext context, string permission)
        => context.Items.TryGetValue("SuiteIdentity", out object? value) &&
           value is SuiteIdentity identity &&
           identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private sealed record LifecycleDecision(string? Reason);
}
