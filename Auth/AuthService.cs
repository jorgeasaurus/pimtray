using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace PIMTray.Auth;

public sealed class AuthService
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private IAccount? _account;

    private AuthService(IPublicClientApplication app, string[] scopes)
    {
        _app = app;
        _scopes = scopes;
    }

    public static async Task<AuthService> CreateAsync(AzureAdConfig cfg)
    {
        var cloud = AzureCloudSettingsResolver.Resolve(cfg);

        var app = PublicClientApplicationBuilder
            .Create(cfg.ClientId)
            .WithAuthority(cloud.AuthorityCloud, cfg.TenantId)
            .WithRedirectUri(cfg.RedirectUri)
            .WithClientName("PIMTray")
            .WithClientVersion("1.0.0")
            .Build();

        await AttachTokenCacheAsync(app);
        return new AuthService(app, BuildScopes(cloud.GraphResource));
    }

    private static async Task AttachTokenCacheAsync(IPublicClientApplication app)
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PIMTray");
        Directory.CreateDirectory(cacheDir);

        var props = new StorageCreationPropertiesBuilder("msal_cache.bin", cacheDir).Build();
        var helper = await MsalCacheHelper.CreateAsync(props);
        helper.RegisterCache(app.UserTokenCache);
    }

    public async Task<AuthResult> SignInAsync(CancellationToken ct = default)
    {
        var accounts = await _app.GetAccountsAsync();
        _account = accounts.FirstOrDefault();

        AuthenticationResult result;
        if (_account is not null)
        {
            try
            {
                result = await _app.AcquireTokenSilent(_scopes, _account).ExecuteAsync(ct);
            }
            catch (MsalUiRequiredException)
            {
                result = await AcquireInteractiveAsync(ct);
            }
        }
        else
        {
            result = await AcquireInteractiveAsync(ct);
        }

        _account = result.Account;
        return new AuthResult(result.AccessToken, result.Account.Username, GetUserObjectId(result));
    }

    public async Task<AuthResult?> TryGetTokenSilentAsync(CancellationToken ct = default)
    {
        var accounts = await _app.GetAccountsAsync();
        _account = accounts.FirstOrDefault();
        if (_account is null) return null;

        try
        {
            var result = await _app.AcquireTokenSilent(_scopes, _account).ExecuteAsync(ct);
            return new AuthResult(result.AccessToken, result.Account.Username, GetUserObjectId(result));
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        var accounts = await _app.GetAccountsAsync();
        foreach (var a in accounts)
            await _app.RemoveAsync(a);
        _account = null;
    }

    private Task<AuthenticationResult> AcquireInteractiveAsync(CancellationToken ct)
    {
        return _app.AcquireTokenInteractive(_scopes)
            .WithUseEmbeddedWebView(false)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(ct);
    }

    private static string[] BuildScopes(string graphResource) =>
    {
        $"{graphResource}/RoleEligibilitySchedule.Read.Directory",
        $"{graphResource}/RoleAssignmentSchedule.ReadWrite.Directory",
        $"{graphResource}/User.Read"
    };

    private static string GetUserObjectId(AuthenticationResult r)
    {
        return r.UniqueId;
    }
}

public sealed record AuthResult(string AccessToken, string Username, string UserObjectId);
