using System.Net;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Petaurum.Windows.Router;

public sealed class AccessCodeRequiredException() : Exception("This page needs the router's device access code.");

public sealed class AccessCodeRejectedException() : Exception("The router didn't accept the device access code.");

/// <summary>
/// Reads status pages from an AT&amp;T BGW gateway, one request at a time, and signs in with the device access
/// code when a page asks for it. Never submits any other form. See specs/features/router.md.
/// </summary>
public sealed class BgwRouterClient : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(120);
    private static readonly Encoding PageEncoding;

    private readonly HttpClient _http;
    private readonly HtmlParser _parser = new();
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    static BgwRouterClient()
    {
        // BGW pages are windows-1252, which .NET only decodes once code pages are registered.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        PageEncoding = Encoding.GetEncoding(1252);
    }

    public BgwRouterClient(IPAddress address)
    {
        Address = address;
        _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri($"http://{address}/cgi-bin/"),
            Timeout = RequestTimeout,
        };
        _http.DefaultRequestHeaders.Referrer = new Uri($"http://{address}/cgi-bin/home.ha");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Petaurum");
    }

    public IPAddress Address { get; }

    /// <summary>Whether the gateway at this address is a BGW: its front page sends you to /cgi-bin/home.ha.</summary>
    public static async Task<bool> IsBgwAsync(IPAddress address, CancellationToken cancellationToken)
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        try
        {
            using var response = await http.GetAsync($"http://{address}/", cancellationToken);
            if (response.Headers.Location?.OriginalString.Contains("/cgi-bin/home.ha") == true) return true;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Contains("/cgi-bin/home.ha", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a page such as "devices.ha". If it needs the access code, signs in with <paramref name="accessCode"/>,
    /// or throws <see cref="AccessCodeRequiredException"/> when there is none.
    /// </summary>
    public async Task<RouterPageData> ReadAsync(string path, Func<string?> accessCode, CancellationToken cancellationToken)
    {
        await _oneAtATime.WaitAsync(cancellationToken);
        try
        {
            var document = await GetAsync(path, cancellationToken);
            if (BgwPageParser.IsAccessCodePage(document) && !BgwPageParser.NeedsAccessCode(document))
                document = await GetAsync(path, cancellationToken);
            if (!BgwPageParser.NeedsAccessCode(document)) return BgwPageParser.Parse(document);

            var code = accessCode() ?? throw new AccessCodeRequiredException();
            await SignInAsync(document, code, cancellationToken);
            document = await GetAsync(path, cancellationToken);
            if (BgwPageParser.NeedsAccessCode(document)) throw new AccessCodeRejectedException();
            return BgwPageParser.Parse(document);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private async Task<IDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = PageEncoding.GetString(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        return await _parser.ParseDocumentAsync(html, cancellationToken);
    }

    /// <summary>
    /// Posts the access code form the way the router's own page does: the code never leaves this computer,
    /// only MD5(code + nonce) and a row of asterisks.
    /// </summary>
    private async Task SignInAsync(IDocument loginPage, string code, CancellationToken cancellationToken)
    {
        var form = BgwPageParser.LoginForm(loginPage)!;
        var nonce = form.QuerySelector("input[name=nonce]")?.GetAttribute("value") ?? "";
        var hash = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(code + nonce)));
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["nonce"] = nonce,
            ["password"] = new string('*', code.Length),
            ["hashpassword"] = hash,
            ["Continue"] = "Continue",
        });
        using var response = await _http.PostAsync("login.ha", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        _http.Dispose();
        _oneAtATime.Dispose();
    }
}
