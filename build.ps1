#Requires -Version 7.0
param(
    [string]$GamePath = $env:ONI_GAME_PATH,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
if (!$GamePath) { throw 'Set ONI_GAME_PATH or pass -GamePath pointing to your OxygenNotIncluded installation.' }
$managed = Join-Path $GamePath 'OxygenNotIncluded_Data\Managed'
if (!(Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw "ONI assemblies not found: $managed" }
$compiler = Join-Path $projectRoot '.tools\roslyn\tasks\netcore\bincore\csc.dll'
if (!(Test-Path -LiteralPath $compiler)) {
    $toolDirectory = Join-Path $projectRoot '.tools'
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
    $archive = Join-Path $toolDirectory 'microsoft.net.compilers.toolset.zip'
    Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.14.0/microsoft.net.compilers.toolset.4.14.0.nupkg' -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $toolDirectory 'roslyn') -Force
}
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (!$dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (!(Test-Path -LiteralPath $dotnet)) { throw '.NET 9+ runtime is required to run Roslyn.' }
$output = Join-Path $projectRoot 'artifacts\CenteredHud'
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$output = [IO.Path]::GetFullPath($output)
if (!$output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Build output must stay inside artifacts.'
}
foreach ($directory in @($artifactRoot, $output)) {
    if ((Test-Path -LiteralPath $directory) -and
        ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to clean a linked build directory: $directory"
    }
}
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @('/nostdlib+', '/target:library', '/langversion:9.0', '/optimize+', '/deterministic+', '/warnaserror+', ('/out:"' + (Join-Path $output 'CenteredHud.dll') + '"'))
# Use only ONI's reference assemblies; never compile against the host's System.Private.CoreLib.
$references = @('mscorlib','netstandard','System','System.Core','Assembly-CSharp','Assembly-CSharp-firstpass','0Harmony','Newtonsoft.Json','UnityEngine.CoreModule','UnityEngine.UI','UnityEngine.UIModule','UnityEngine.InputLegacyModule','UnityEngine.TextRenderingModule','Unity.TextMeshPro')
foreach ($reference in $references) { $arguments += '/reference:"' + (Join-Path $managed ($reference + '.dll')) + '"' }
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\CenteredHud') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $arguments += '"' + $_.FullName + '"' }
$response = Join-Path $projectRoot '.tools\build.rsp'
Set-Content -LiteralPath $response -Value $arguments -Encoding utf8
& $dotnet $compiler /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed ($LASTEXITCODE)." }
Copy-Item -LiteralPath (Join-Path $projectRoot 'mod\mod.yaml'),(Join-Path $projectRoot 'mod\mod_info.yaml'),(Join-Path $projectRoot 'LICENSE') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'mod\translations') -Destination $output -Recurse -Force
& (Join-Path $projectRoot 'tests/test-game-contract.ps1') -GamePath $GamePath
Write-Output "Built: $output"
if ($Install) {
    if (Get-Process OxygenNotIncluded -ErrorAction SilentlyContinue) { throw 'Close ONI before installing the new DLL. The build has been saved under artifacts.' }
    $destination = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Klei\OxygenNotIncluded\mods\Local\CenteredHud'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($file in @('CenteredHud.dll','mod.yaml','mod_info.yaml')) { Copy-Item -LiteralPath (Join-Path $output $file) -Destination $destination -Force }
    Copy-Item -LiteralPath (Join-Path $output 'translations') -Destination $destination -Recurse -Force
    Write-Output "Installed: $destination"
}
