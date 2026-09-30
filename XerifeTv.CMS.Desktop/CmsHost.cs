using System.Diagnostics;
using System.Net.Sockets;

namespace XerifeTv.CMS.Desktop;

/// <summary>
/// Sobe o CMS (publicado na pasta "cms" ao lado do app) como processo filho em localhost.
/// Rodando na máquina de quem usa, todas as requisições do servidor (catálogo, redirect,
/// StreamMedia) saem do IP residencial - o mesmo cenário do "localhost que funciona".
/// </summary>
internal sealed class CmsHost : IDisposable
{
    // Porta fixa (e não a 5003 do dev) pra não brigar com o CMS rodando pelo Visual Studio.
    // Fixa também mantém o localStorage/cookies do WebView2 entre execuções.
    private const int Port = 5093;
    private const string CmsExecutableName = "XerifeTv.CMS.exe";
    private static readonly TimeSpan StartupPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly string _cmsDirectory = Path.Combine(AppContext.BaseDirectory, "cms");
    private readonly object _logLock = new();
    private Process? _process;
    private StreamWriter? _log;

    public Uri BaseUri { get; } = new($"http://127.0.0.1:{Port}/");

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XerifeTV.CMS");

    public string LogPath { get; } = Path.Combine(DataDirectory, "cms.log");

    public void Start()
    {
        var executablePath = Path.Combine(_cmsDirectory, CmsExecutableName);
        if (!File.Exists(executablePath))
            throw new FileNotFoundException(
                $"CMS não encontrado em \"{executablePath}\". Gere o app com o publish-desktop.ps1.");

        KillOrphanInstances(executablePath);
        EnsurePortIsFree();

        Directory.CreateDirectory(DataDirectory);
        _log = new StreamWriter(LogPath, append: false) { AutoFlush = true };

        var startInfo = new ProcessStartInfo(executablePath)
        {
            // Content root do ASP.NET = diretório atual: precisa ser a pasta do CMS (wwwroot, appsettings)
            WorkingDirectory = _cmsDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Development = mesmo appsettings (com os segredos) do localhost; em Production o
        // Program.cs do CMS escuta em *:80, o que não serve pra app local.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = BaseUri.ToString().TrimEnd('/');

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Não foi possível iniciar o CMS.");
        _process.OutputDataReceived += (_, e) => WriteLog(e.Data);
        _process.ErrorDataReceived += (_, e) => WriteLog(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>Espera o Kestrel responder (qualquer status HTTP serve).</summary>
    public async Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_process is null || _process.HasExited)
                throw new InvalidOperationException($"O CMS fechou ao iniciar. Veja o log em \"{LogPath}\".");

            try
            {
                using var response = await client.GetAsync(BaseUri, cancellationToken);
                return;
            }
            catch (HttpRequestException)
            {
                // Kestrel ainda subindo
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout de uma tentativa: tenta de novo
            }

            await Task.Delay(StartupPollInterval, cancellationToken);
        }

        throw new TimeoutException($"O CMS não respondeu a tempo. Veja o log em \"{LogPath}\".");
    }

    public void Dispose()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Já tinha fechado
        }

        _process?.Dispose();
        _process = null;

        lock (_logLock)
        {
            _log?.Dispose();
            _log = null;
        }
    }

    private void WriteLog(string? line)
    {
        if (line is null) return;
        lock (_logLock)
            _log?.WriteLine(line);
    }

    // Se o app fechou de forma inesperada da última vez, o CMS filho pode ter ficado vivo
    // segurando a porta. Só mata processos do MESMO executável (não o CMS do Visual Studio).
    private static void KillOrphanInstances(string executablePath)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(CmsExecutableName)))
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Sem acesso ou já encerrado: ignora
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void EnsurePortIsFree()
    {
        try
        {
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, Port);
            listener.Start();
            listener.Stop();
        }
        catch (SocketException)
        {
            throw new InvalidOperationException(
                $"A porta {Port} já está em uso por outro programa. Feche-o e abra o XerifeTV CMS de novo.");
        }
    }
}
