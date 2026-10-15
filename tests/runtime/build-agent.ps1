#Requires -Version 7.0
param([string]$GamePath = $env:ONI_GAME_PATH)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$GamePath) { throw 'Set ONI_GAME_PATH or pass -GamePath.' }
$managed = Join-Path $GamePath 'OxygenNotIncluded_Data/Managed'
$compiler = Join-Path $root '.tools/roslyn/tasks/netcore/bincore/csc.dll'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Run make build first to obtain the pinned Roslyn compiler.' }
$output = Join-Path $root 'artifacts/InterfaceExtendedTestAgent'
# Never clean or install here: building the agent must not touch a running game or bridge requests.
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @('/nostdlib+', '/target:library', '/langversion:9.0', '/optimize+', '/deterministic+', '/warnaserror+',
    ('/out:"' + (Join-Path $output 'InterfaceExtended.TestAgent.dll') + '"'))
$references = @('mscorlib','netstandard','System','System.Core','Assembly-CSharp','Assembly-CSharp-firstpass',
    '0Harmony','Newtonsoft.Json','UnityEngine.CoreModule','UnityEngine.UI','UnityEngine.UIModule',
    'UnityEngine.InputLegacyModule','UnityEngine.TextRenderingModule','Unity.TextMeshPro',
    'UnityEngine.ScreenCaptureModule','UnityEngine.ImageConversionModule')
foreach ($name in $references) { $arguments += '/reference:"' + (Join-Path $managed ($name + '.dll')) + '"' }
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'agent') -Filter '*.cs' | Sort-Object Name |
    ForEach-Object { $arguments += '"' + $_.FullName + '"' }
$response = Join-Path $root '.tools/build-test-agent.rsp'
Set-Content -LiteralPath $response -Value $arguments -Encoding utf8
& dotnet $compiler /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw "Test agent compilation failed ($LASTEXITCODE)." }
& (Join-Path $root 'tests/test-game-contract.ps1') -GamePath $GamePath -TestAgentPath (Join-Path $output 'InterfaceExtended.TestAgent.dll')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'mod/mod.yaml'),(Join-Path $PSScriptRoot 'mod/mod_info.yaml') -Destination $output -Force
Write-Output "Prepared development agent: $output (not installed)"
