#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$source = @('../src/CenteredHud/Options.cs', '../src/CenteredHud/ConfigStore.cs', 'ConfigStoreChecks.cs') |
    ForEach-Object { Join-Path $PSScriptRoot $_ }
# PowerShell ships Json.NET; no ONI installation is needed for these filesystem tests.
$references = @([Newtonsoft.Json.JsonConvert].Assembly.Location,
    (Join-Path $PSHOME 'ref/System.Runtime.dll'), (Join-Path $PSHOME 'ref/System.Linq.dll'),
    (Join-Path $PSHOME 'ref/System.Text.Encoding.Extensions.dll'))
# Bundled Json.NET can target an older System.Runtime than the current PowerShell host.
Add-Type -Path $source -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('InterfaceExtended-config-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    [ConfigStoreChecks]::Run($testDirectory)
    Write-Output 'Config checks passed: migration, precedence, malformed data, atomic writes and update persistence.'
} finally {
    $resolved = [IO.Path]::GetFullPath($testDirectory)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\', '/') -ne $expectedParent.TrimEnd('\', '/') -or
        [IO.Path]::GetFileName($resolved) -notmatch '^InterfaceExtended-config-[a-f0-9]{32}$') {
        throw "Refusing to remove unexpected test directory: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
