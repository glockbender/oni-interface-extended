#Requires -Version 7.0
param(
    [ValidateSet('check', 'build', 'package', 'install', 'translations', 'test-agent', 'test-runner', 'workshop')]
    [string]$Task = 'check',
    [string]$GamePath = $env:ONI_GAME_PATH,
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
switch ($Task) {
    'check' {
        & (Join-Path $PSScriptRoot 'tests/test-geometry.ps1')
        & (Join-Path $PSScriptRoot 'tests/test-options.ps1')
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'tests/test-config.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Config checks failed' }
        & $Python (Join-Path $PSScriptRoot 'scripts/localization.py')
        if ($LASTEXITCODE -ne 0) { throw 'Localization check failed' }
        & $PSCommandPath -Task test-runner -Python $Python
        & $Python -m unittest discover -s (Join-Path $PSScriptRoot 'tests') -p test_workshop.py
        if ($LASTEXITCODE -ne 0) { throw 'Workshop exporter checks failed' }
        & $Python (Join-Path $PSScriptRoot 'scripts/workshop.py') --check
        if ($LASTEXITCODE -ne 0) { throw 'Workshop README source check failed' }
    }
    'test-runner' {
        & $Python -m unittest discover -s (Join-Path $PSScriptRoot 'tests/runtime') -p 'test_*.py'
        if ($LASTEXITCODE -ne 0) { throw 'Runtime runner checks failed' }
    }
    'test-agent' {
        & (Join-Path $PSScriptRoot 'tests/runtime/build-agent.ps1') -GamePath $GamePath
    }
    'workshop' {
        & $Python (Join-Path $PSScriptRoot 'scripts/workshop.py')
        if ($LASTEXITCODE -ne 0) { throw 'Workshop export failed' }
    }
    'translations' {
        & $Python (Join-Path $PSScriptRoot 'scripts/localization.py') --update-template
        if ($LASTEXITCODE -ne 0) { throw 'Translation template update failed' }
    }
    default {
        if ($Task -eq 'package') { & $PSCommandPath -Task check -Python $Python }
        & (Join-Path $PSScriptRoot 'build.ps1') -GamePath $GamePath -Install:($Task -eq 'install')
        if ($Task -eq 'package') {
            $output = Join-Path $PSScriptRoot 'artifacts/CenteredHud'
            $info = Get-Content -Raw (Join-Path $output 'mod_info.yaml')
            $version = [regex]::Match($info, 'version: "([\d.]+)"').Groups[1].Value
            $files = @('CenteredHud.dll', 'mod.yaml', 'mod_info.yaml', 'LICENSE', 'translations') |
                ForEach-Object { Join-Path $output $_ }
            $archive = Join-Path $PSScriptRoot "artifacts/InterfaceExtended-$version.zip"
            Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
            Write-Output "Packaged: $archive"
        }
    }
}
