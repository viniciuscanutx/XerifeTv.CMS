# Gera o app desktop do CMS em .\publish:
#   publish\XerifeTV CMS.exe   -> janela (WebView2)
#   publish\cms\               -> o CMS em si, que a janela sobe em http://127.0.0.1:5093
#
# O CMS vai com o appsettings.Development.json (segredos do seu localhost), então
# NÃO distribua essa pasta pra outras pessoas.
#
# Uso: .\publish-desktop.ps1   (ou .\publish-desktop.ps1 -Output D:\Apps\XerifeTV-CMS)
param(
    [string]$Output = (Join-Path $PSScriptRoot 'publish')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$cmsProject = Join-Path $repoRoot 'XerifeTv.CMS\XerifeTv.CMS.csproj'
$desktopProject = Join-Path $PSScriptRoot 'XerifeTv.CMS.Desktop.csproj'

if (-not (Test-Path (Join-Path $repoRoot 'XerifeTv.CMS\appsettings.Development.json'))) {
    throw 'appsettings.Development.json não encontrado: o CMS local precisa dele (connection string, chaves).'
}

Write-Host '==> Publicando o CMS...' -ForegroundColor Cyan
dotnet publish $cmsProject -c Release -r win-x64 --self-contained true -o (Join-Path $Output 'cms')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o CMS.' }

Write-Host '==> Publicando a janela desktop...' -ForegroundColor Cyan
dotnet publish $desktopProject -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o app desktop.' }

Write-Host "==> Pronto: $(Join-Path $Output 'XerifeTV CMS.exe')" -ForegroundColor Green
