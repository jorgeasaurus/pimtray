using System.Diagnostics;
using System.Reflection;

namespace PIMTray.UI;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About PIM Tray";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 260);
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 9F);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        using var appIcon = AppIcon.Load();
        var iconBox = new PictureBox
        {
            Image = appIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(16, 16)
        };

        var titleLabel = new Label
        {
            Text = "PIM Tray",
            Font = new Font("Segoe UI Semibold", 14F),
            AutoSize = true,
            Location = new Point(72, 16)
        };

        var versionLabel = new Label
        {
            Text = $"Version {version}",
            AutoSize = true,
            Location = new Point(72, 48),
            ForeColor = SystemColors.GrayText
        };

        var descLabel = new Label
        {
            Text = "Activate Entra ID PIM roles from the Windows tray.",
            AutoSize = false,
            Location = new Point(16, 90),
            Size = new Size(388, 32)
        };

        var authorLabel = new Label
        {
            Text = "Thomas Marcussen",
            Font = new Font("Segoe UI Semibold", 10F),
            AutoSize = true,
            Location = new Point(16, 130)
        };

        var titleSubLabel = new Label
        {
            Text = "Microsoft MVP, Technology Architect",
            AutoSize = true,
            Location = new Point(16, 152),
            ForeColor = SystemColors.GrayText
        };

        var siteLink = NewLink("ThomasMarcussen.com", "https://thomasmarcussen.com",
            new Point(16, 178));

        var blogLink = NewLink("blog.thomasmarcussen.com", "https://blog.thomasmarcussen.com",
            new Point(16, 198));

        var mailLink = NewLink("Thomas@ThomasMarcussen.com", "mailto:Thomas@ThomasMarcussen.com",
            new Point(16, 218));

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(326, 220),
            Size = new Size(78, 28),
            DialogResult = DialogResult.OK
        };
        AcceptButton = okButton;
        CancelButton = okButton;

        Controls.AddRange(new Control[]
        {
            iconBox, titleLabel, versionLabel, descLabel,
            authorLabel, titleSubLabel, siteLink, blogLink, mailLink, okButton
        });
    }

    private static LinkLabel NewLink(string text, string url, Point location)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            Location = location,
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // best effort - if no shell handler is registered, silently ignore
        }
    }
}
