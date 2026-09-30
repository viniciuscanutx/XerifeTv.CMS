using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace XerifeTv.CMS.Desktop;

internal sealed class MainForm : Form
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(90);

    private readonly CmsHost _cmsHost = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _statusLabel = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 12F),
        Text = "Iniciando o CMS..."
    };

    // Menor tamanho em que o layout do CMS (sidebar + grade de pôsteres) não quebra
    private static readonly Size LayoutMinimumSize = new(1800, 990);

    public MainForm()
    {
        Text = "XerifeTV CMS";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;

        // Limita à área útil da tela: em monitor menor que o mínimo, abre maximizado
        var workingArea = Screen.PrimaryScreen?.WorkingArea.Size ?? LayoutMinimumSize;
        MinimumSize = new Size(
            Math.Min(LayoutMinimumSize.Width, workingArea.Width),
            Math.Min(LayoutMinimumSize.Height, workingArea.Height));
        Size = MinimumSize;
        if (MinimumSize != LayoutMinimumSize)
            WindowState = FormWindowState.Maximized;

        Controls.Add(_webView);
        Controls.Add(_statusLabel);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            _cmsHost.Start();
            await _cmsHost.WaitUntilReadyAsync(StartupTimeout, _closing.Token);
            await InitializeWebViewAsync();
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
            // Janela fechada durante a inicialização
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowFatalError("O WebView2 Runtime não está instalado.\n\n" +
                "Baixe em: https://developer.microsoft.com/microsoft-edge/webview2/");
        }
        catch (Exception ex)
        {
            ShowFatalError(ex.Message);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing.Cancel();
        _webView.Dispose();
        _cmsHost.Dispose();
        base.OnFormClosing(e);
    }

    private async Task InitializeWebViewAsync()
    {
        // Pasta fixa: mantém login (cookies) e preferências entre execuções
        var environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(CmsHost.DataDirectory, "WebView2"));
        await _webView.EnsureCoreWebView2Async(environment);

        // O WebView2 vem com "Salvar senha?" desligado; liga pra lembrar login/senha
        _webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
        _webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;

        _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        _webView.CoreWebView2.DocumentTitleChanged += (_, _) =>
            Text = string.IsNullOrWhiteSpace(_webView.CoreWebView2.DocumentTitle)
                ? "XerifeTV CMS"
                : $"{_webView.CoreWebView2.DocumentTitle} - XerifeTV CMS";

        _webView.Source = _cmsHost.BaseUri;
        _statusLabel.Visible = false;
        _webView.Visible = true;
    }

    // Links externos (target=_blank pra fora do CMS) abrem no navegador padrão
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Host == _cmsHost.BaseUri.Host)
            return;

        e.Handled = true;
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    private void ShowFatalError(string message)
    {
        _statusLabel.Text = "Não foi possível abrir o CMS.";
        MessageBox.Show(this, message, "XerifeTV CMS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Close();
    }
}
