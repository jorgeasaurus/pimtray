using PIMTray.Auth;
using PIMTray.Pim;

namespace PIMTray.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _signInItem;
    private readonly ToolStripMenuItem _signOutItem;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly ToolStripMenuItem _activateRoot;
    private readonly ToolStripMenuItem _openWindowItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly ToolStripMenuItem _aboutItem;
    private readonly HttpClient _http = new();
    private readonly MainForm _mainForm;

    private AppConfig _cfg = null!;
    private AuthService _auth = null!;
    private PimService _pim = null!;
    private AuthResult? _session;
    private IReadOnlyList<EligibleRole> _roles = Array.Empty<EligibleRole>();

    public TrayApplicationContext()
    {
        _menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("Not signed in") { Enabled = false };
        _openWindowItem = new ToolStripMenuItem("Open PIM Tray", null, (_, _) => ShowMainWindow());
        _signInItem = new ToolStripMenuItem("Sign in...", null, async (_, _) => await SignInAsync());
        _signOutItem = new ToolStripMenuItem("Sign out", null, async (_, _) => await SignOutAsync()) { Visible = false };
        _refreshItem = new ToolStripMenuItem("Refresh roles", null, async (_, _) => await RefreshRolesAsync()) { Visible = false };
        _activateRoot = new ToolStripMenuItem("Eligible roles") { Visible = false };
        _exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApp());
        _aboutItem = new ToolStripMenuItem("About...", null, (_, _) => ShowAbout());

        _menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            _openWindowItem,
            new ToolStripSeparator(),
            _signInItem,
            _activateRoot,
            _refreshItem,
            _signOutItem,
            new ToolStripSeparator(),
            _exitItem,
            _aboutItem
        });

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = "PIM Tray - not signed in",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _icon.MouseClick += OnIconClick;
        _icon.MouseDoubleClick += (_, _) => ShowMainWindow();

        _mainForm = new MainForm();
        _mainForm.SignInRequested += () => _ = SignInAsync();
        _mainForm.SignOutRequested += () => _ = SignOutAsync();
        _mainForm.RefreshRequested += () => _ = RefreshRolesAsync();
        _mainForm.ActivateRolesRequested += roles => _ = ActivateRolesAsync(roles);
        _mainForm.AboutRequested += ShowAbout;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _cfg = AppConfig.Load();
            _auth = await AuthService.CreateAsync(_cfg.AzureAd);
            _pim = new PimService(_http);

            var silent = await _auth.TryGetTokenSilentAsync();
            if (silent is not null)
            {
                _session = silent;
                _pim.SetAccessToken(silent.AccessToken);
                ApplySignedInState();
                await RefreshRolesAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError("PIM Tray failed to start", ex);
        }
    }

    private async Task SignInAsync()
    {
        try
        {
            _session = await _auth.SignInAsync();
            _pim.SetAccessToken(_session.AccessToken);
            ApplySignedInState();
            await RefreshRolesAsync();
            ShowInfo("Signed in", $"Signed in as {_session.Username}");
        }
        catch (Exception ex)
        {
            ShowError("Sign-in failed", ex);
        }
    }

    private async Task SignOutAsync()
    {
        try
        {
            await _auth.SignOutAsync();
            _session = null;
            _pim.SetAccessToken("");
            _roles = Array.Empty<EligibleRole>();
            ApplySignedOutState();
        }
        catch (Exception ex)
        {
            ShowError("Sign-out failed", ex);
        }
    }

    private async Task RefreshRolesAsync()
    {
        if (_session is null) return;
        try
        {
            _activateRoot.DropDownItems.Clear();
            _activateRoot.DropDownItems.Add(new ToolStripMenuItem("Loading...") { Enabled = false });

            var fresh = await _auth.TryGetTokenSilentAsync() ?? _session;
            _session = fresh;
            _pim.SetAccessToken(fresh.AccessToken);

            _roles = await _pim.GetEligibleRolesAsync(fresh.UserObjectId);
            RebuildTrayRoleMenu();
            _mainForm.SetRoles(_roles);
        }
        catch (Exception ex)
        {
            _activateRoot.DropDownItems.Clear();
            _activateRoot.DropDownItems.Add(new ToolStripMenuItem("Error - see notification") { Enabled = false });
            ShowError("Failed to load eligible roles", ex);
        }
    }

    private void RebuildTrayRoleMenu()
    {
        _activateRoot.DropDownItems.Clear();
        if (_roles.Count == 0)
        {
            _activateRoot.DropDownItems.Add(new ToolStripMenuItem("No eligible roles") { Enabled = false });
            return;
        }
        foreach (var r in _roles)
        {
            var label = r.ScopeDescription == "Directory"
                ? r.RoleDisplayName
                : $"{r.RoleDisplayName}  ({r.ScopeDescription})";
            var captured = r;
            _activateRoot.DropDownItems.Add(new ToolStripMenuItem(label, null,
                async (_, _) => await ActivateRolesAsync(new[] { captured })));
        }
    }

    private async Task ActivateRolesAsync(IReadOnlyList<EligibleRole> roles)
    {
        if (_session is null || roles.Count == 0) return;

        using var form = new ActivateRoleForm(roles, _cfg.Pim.DurationOptionsHours, _cfg.Pim.DefaultDurationHours);
        if (form.ShowDialog() != DialogResult.OK) return;

        var ok = new List<string>();
        var failures = new List<(string Role, string Error)>();

        foreach (var role in roles)
        {
            try
            {
                await _pim.ActivateRoleAsync(_session.UserObjectId, role, form.Justification, form.Duration);
                ok.Add(role.RoleDisplayName);
            }
            catch (Exception ex)
            {
                failures.Add((role.RoleDisplayName, ex.Message));
            }
        }

        ReportActivationResult(ok, failures, form.Duration);
    }

    private void ReportActivationResult(
        IReadOnlyList<string> succeeded,
        IReadOnlyList<(string Role, string Error)> failed,
        TimeSpan duration)
    {
        var hours = $"{duration.TotalHours:0} hour(s)";

        if (failed.Count == 0)
        {
            var body = succeeded.Count == 1
                ? $"{succeeded[0]} for {hours}."
                : $"{succeeded.Count} roles activated for {hours}:\n - " + string.Join("\n - ", succeeded);
            ShowInfo("PIM activation requested", body);
            return;
        }

        if (succeeded.Count == 0)
        {
            var body = failed.Count == 1
                ? $"{failed[0].Role}: {failed[0].Error}"
                : string.Join("\n", failed.Select(f => $"{f.Role}: {f.Error}"));
            ShowError("PIM activation failed", new InvalidOperationException(body));
            return;
        }

        var mixedBody =
            $"OK ({succeeded.Count}): " + string.Join(", ", succeeded) + "\n" +
            $"Failed ({failed.Count}): " + string.Join("; ", failed.Select(f => $"{f.Role} - {f.Error}"));
        _icon.ShowBalloonTip(8000, "PIM activation - partial success", mixedBody, ToolTipIcon.Warning);
    }

    private void ApplySignedInState()
    {
        _statusItem.Text = $"Signed in: {_session?.Username}";
        _signInItem.Visible = false;
        _signOutItem.Visible = true;
        _refreshItem.Visible = true;
        _activateRoot.Visible = true;
        _icon.Text = $"PIM Tray - {_session?.Username}".Trim();
        _mainForm.SetSignedIn(_session?.Username ?? "");
    }

    private void ApplySignedOutState()
    {
        _statusItem.Text = "Not signed in";
        _signInItem.Visible = true;
        _signOutItem.Visible = false;
        _refreshItem.Visible = false;
        _activateRoot.Visible = false;
        _activateRoot.DropDownItems.Clear();
        _icon.Text = "PIM Tray - not signed in";
        _mainForm.SetSignedOut();
    }

    private void ShowMainWindow() => _mainForm.ShowAndFocus();

    private void OnIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ShowMainWindow();
    }

    private static void ShowAbout()
    {
        using var dlg = new AboutForm();
        dlg.ShowDialog();
    }

    private void ShowInfo(string title, string message)
        => _icon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);

    private void ShowError(string title, Exception ex)
        => _icon.ShowBalloonTip(8000, title, ex.Message, ToolTipIcon.Error);

    private void ExitApp()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _mainForm.RequestRealExit();
        _http.Dispose();
        ExitThread();
    }
}
