using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vilmo.Tests;

public class OAuthCallbackTests : IClassFixture<ApiFactory>
{
    readonly ApiFactory _factory;

    public OAuthCallbackTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Callback_without_state_or_companyId_is_400()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/oauth/MercadoLivre/callback?code=TG-test-auth-code");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("companyId required", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Callback_reads_company_id_from_state_and_keeps_demo_when_requested()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, companyId) = await AdminAsync(client);
        var res = await client.GetAsync(
            $"/oauth/MercadoLivre/callback?code=TG-test-auth-code&state={companyId:D}&demo=1");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var location = res.Headers.Location?.ToString() ?? "";
        Assert.Contains("#/marketplaces", location);
        Assert.Contains("oauth=ok", location);

        var list = await client.SendAsync(Authed(HttpMethod.Get, $"/companies/{companyId}/marketplaces", token, companyId));
        list.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var ml = doc.RootElement.EnumerateArray().First(x => x.GetProperty("code").GetString() == "MercadoLivre");
        Assert.Equal("Linked", ml.GetProperty("linkStatus").GetString());
        var steps = ml.GetProperty("connectLog").EnumerateArray().Select(x => x.GetProperty("stepCode").GetString()).ToList();
        Assert.Contains("callback", steps);
        Assert.Contains("applied", steps);
        var logsRes = await client.SendAsync(Authed(HttpMethod.Get, $"/companies/{companyId}/marketplaces/MercadoLivre/logs", token, companyId));
        logsRes.EnsureSuccessStatusCode();
        using var logsDoc = JsonDocument.Parse(await logsRes.Content.ReadAsStringAsync());
        Assert.True(logsDoc.RootElement.GetProperty("items").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Connect_without_client_id_still_returns_demo_callback()
    {
        var client = _factory.CreateClient();
        var (token, companyId) = await AdminAsync(client);
        var res = await client.SendAsync(Authed(HttpMethod.Post, "/marketplaces/Shopee/connect", token, new { }, companyId));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var url = doc.RootElement.GetProperty("authorizationUrl").GetString() ?? "";
        Assert.Contains("/oauth/Shopee/callback", url);
        Assert.Contains("demo=1", url);
        Assert.Contains(companyId.ToString("D"), url);
        var logs = doc.RootElement.GetProperty("logs").EnumerateArray().ToList();
        Assert.Contains(logs, x => x.GetProperty("stepCode").GetString() == "received");
        Assert.Contains(logs, x => x.GetProperty("stepCode").GetString() == "credentials");
        Assert.Contains(logs, x => x.GetProperty("stepCode").GetString() == "authorize");
        Assert.Contains(logs, x => x.GetProperty("stepCode").GetString() == "redirect");
        var authorize = logs.First(x => x.GetProperty("stepCode").GetString() == "authorize");
        Assert.Contains("request", authorize.GetProperty("technicalJson").GetString()!);
        Assert.Contains("demo", authorize.GetProperty("technicalJson").GetString()!);
    }

    static async Task<(string Token, Guid CompanyId)> AdminAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/auth/login", new { email = "admin@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        var me = await client.SendAsync(Authed(HttpMethod.Get, "/me", token));
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        return (token, meDoc.RootElement.GetProperty("memberships")[0].GetProperty("companyId").GetGuid());
    }

    static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null, Guid? companyId = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (companyId is { } cid) req.Headers.Add("X-Company-Id", cid.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }
        return req;
    }
}

public sealed class MercadoLivreTokenHandler : HttpMessageHandler
{
    public string? LastBody { get; private set; }
    public Uri? LastUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastUri = request.RequestUri;
        LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        if (request.RequestUri!.AbsolutePath.Contains("/oauth/token", StringComparison.OrdinalIgnoreCase))
        {
            const string json = """{"access_token":"APP_USR-test-access","token_type":"bearer","expires_in":21600,"refresh_token":"TG-test-refresh","user_id":3632941126}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

public sealed class OAuthApiFactory : ApiFactory
{
    public MercadoLivreTokenHandler TokenHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PUBLIC_BASE_URL"] = "https://vilmomkt.com"
            });
        });
        var handler = TokenHandler;
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient("marketplace")
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        });
    }
}

public class MercadoLivreOAuthExchangeTests : IClassFixture<OAuthApiFactory>
{
    readonly OAuthApiFactory _factory;

    public MercadoLivreOAuthExchangeTests(OAuthApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Connect_with_client_id_returns_mercadolivre_authorize_url()
    {
        var client = _factory.CreateClient();
        var (token, companyId) = await AdminAsync(client);
        await SaveAppAsync(client, token, companyId);

        var res = await client.SendAsync(Authed(HttpMethod.Post, "/marketplaces/MercadoLivre/connect", token, new { }, companyId));
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var url = doc.RootElement.GetProperty("authorizationUrl").GetString() ?? "";
        var callback = doc.RootElement.GetProperty("callback").GetString() ?? "";
        Assert.Equal("https://vilmomkt.com/oauth/MercadoLivre/callback", callback);
        Assert.StartsWith("https://auth.mercadolivre.com.br/authorization?", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("client_id=1027027955417592", url);
        Assert.Contains($"state={companyId:D}", url);
        Assert.Contains(Uri.EscapeDataString("https://vilmomkt.com/oauth/MercadoLivre/callback"), url);
        Assert.DoesNotContain("demo=1", url);
    }

    [Fact]
    public async Task Callback_with_state_and_code_exchanges_token()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, companyId) = await AdminAsync(client);
        await SaveAppAsync(client, token, companyId);

        var res = await client.GetAsync($"/oauth/MercadoLivre/callback?code=TG-test-auth-code&state={companyId:D}");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var location = res.Headers.Location?.ToString() ?? "";
        Assert.Contains("oauth=ok", location);
        Assert.Contains("connected=MercadoLivre", location);

        Assert.NotNull(_factory.TokenHandler.LastUri);
        Assert.Equal("/oauth/token", _factory.TokenHandler.LastUri!.AbsolutePath);
        Assert.Contains("grant_type=authorization_code", _factory.TokenHandler.LastBody);
        Assert.Contains("code=TG-test-auth-code", _factory.TokenHandler.LastBody);
        Assert.Contains(Uri.EscapeDataString("https://vilmomkt.com/oauth/MercadoLivre/callback"), _factory.TokenHandler.LastBody);
        Assert.Contains("client_id=1027027955417592", _factory.TokenHandler.LastBody);

        var list = await client.SendAsync(Authed(HttpMethod.Get, $"/companies/{companyId}/marketplaces", token, companyId));
        list.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var ml = doc.RootElement.EnumerateArray().First(x => x.GetProperty("code").GetString() == "MercadoLivre");
        Assert.Equal("Linked", ml.GetProperty("linkStatus").GetString());
        var fields = ml.GetProperty("fields").EnumerateArray().ToDictionary(
            x => x.GetProperty("parameterKey").GetString()!,
            x => x.GetProperty("value").GetString());
        Assert.Equal("3632941126", fields["UserId"]);
        Assert.DoesNotContain("APP_USR-test-access", fields["AccessToken"]);
        Assert.Contains("••", fields["AccessToken"]);

        var connectLog = ml.GetProperty("connectLog").EnumerateArray().ToList();
        Assert.Contains(connectLog, x => x.GetProperty("stepCode").GetString() == "calling");
        Assert.Contains(connectLog, x => x.GetProperty("stepCode").GetString() == "applied");
        var calling = connectLog.First(x => x.GetProperty("stepCode").GetString() == "calling");
        var callingTech = calling.GetProperty("technicalJson").GetString()!;
        Assert.Contains("request", callingTech);
        Assert.Contains("oauth/token", callingTech);
        Assert.DoesNotContain("test-client-secret", callingTech);
        Assert.DoesNotContain("APP_USR-test-access", callingTech);
        var tokenCallback = connectLog.First(x =>
            x.GetProperty("stepCode").GetString() == "callback"
            && x.GetProperty("technicalJson").GetString()!.Contains("response"));
        var tokenTech = tokenCallback.GetProperty("technicalJson").GetString()!;
        Assert.Contains("response", tokenTech);
        Assert.Contains("3632941126", tokenTech);
        Assert.DoesNotContain("APP_USR-test-access", tokenTech);
        Assert.DoesNotContain("TG-test-refresh", tokenTech);
    }

    static async Task SaveAppAsync(HttpClient client, string token, Guid companyId)
    {
        var res = await client.SendAsync(Authed(HttpMethod.Put, $"/companies/{companyId}/marketplaces/MercadoLivre", token, new
        {
            isEnabled = true,
            fields = new Dictionary<string, string>
            {
                ["ClientId"] = "1027027955417592",
                ["ClientSecret"] = "test-client-secret",
                ["SiteId"] = "MLB"
            }
        }, companyId));
        res.EnsureSuccessStatusCode();
    }

    static async Task<(string Token, Guid CompanyId)> AdminAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/auth/login", new { email = "admin@vilmomkt.com", password = "VilmoAdmin!2026" });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        var me = await client.SendAsync(Authed(HttpMethod.Get, "/me", token));
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        return (token, meDoc.RootElement.GetProperty("memberships")[0].GetProperty("companyId").GetGuid());
    }

    static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null, Guid? companyId = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (companyId is { } cid) req.Headers.Add("X-Company-Id", cid.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }
        return req;
    }
}
