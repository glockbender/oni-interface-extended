#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$GamePath,
    [string]$ModPath = (Join-Path $PSScriptRoot '../artifacts/CenteredHud/CenteredHud.dll'),
    [string]$TestAgentPath
)
$ErrorActionPreference = 'Stop'

function Read-Metadata([string]$path, [scriptblock]$check) {
    $stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $path))
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        & $check $reader
    } finally {
        $pe.Dispose()
        $stream.Dispose()
    }
}

function Require-Members($reader, [string]$name, [string[]]$methods, [string[]]$fields) {
    foreach ($handle in $reader.TypeDefinitions) {
        $type = $reader.GetTypeDefinition($handle)
        if ($reader.GetString($type.Name) -ne $name) { continue }
        $methodNames = @($type.GetMethods() | ForEach-Object { $reader.GetString($reader.GetMethodDefinition($_).Name) })
        $fieldNames = @($type.GetFields() | ForEach-Object { $reader.GetString($reader.GetFieldDefinition($_).Name) })
        foreach ($member in $methods) {
            if ($member -notin $methodNames) { throw "Missing method: $name.$member" }
        }
        foreach ($member in $fields) {
            if ($member -notin $fieldNames) { throw "Missing field: $name.$member" }
        }
        return
    }
    throw "Missing type: $name"
}

Read-Metadata (Join-Path $GamePath 'OxygenNotIncluded_Data/Managed/Assembly-CSharp-firstpass.dll') {
    param($reader)
    Require-Members $reader 'GameScreenManager' @('OnSpawn') @('ssOverlayCanvas')
}
Read-Metadata (Join-Path $GamePath 'OxygenNotIncluded_Data/Managed/Assembly-CSharp.dll') {
    param($reader)
    Require-Members $reader 'OptionsMenuScreen' @('OnPrefabInit') @('graphicsOptionsScreenPrefab', 'metricsScreenPrefab')
    Require-Members $reader 'KButtonMenu' @() @('buttons')
    Require-Members $reader 'KModalButtonMenu' @() @('panelRoot')
    Require-Members $reader 'Localization' @('Initialize', 'RegisterForTranslation', 'GetLocale', 'LoadStringsFile', 'OverloadStrings') @()
}

Read-Metadata $ModPath {
    param($reader)
    foreach ($handle in $reader.AssemblyReferences) {
        $name = $reader.GetString($reader.GetAssemblyReference($handle).Name)
        if ($name -eq 'System.Private.CoreLib') { throw 'Mod references the host runtime instead of ONI framework assemblies' }
    }
    $version = [version]$reader.GetAssemblyDefinition().Version
    $info = Get-Content -Raw (Join-Path $PSScriptRoot '../mod/mod_info.yaml')
    $expected = [regex]::Match($info, 'version: "([\d.]+)"').Groups[1].Value
    if ($version.ToString(3) -ne $expected) { throw "DLL version $version differs from metadata $expected" }
}
if ($TestAgentPath) {
    Read-Metadata (Join-Path $GamePath 'OxygenNotIncluded_Data/Managed/Assembly-CSharp.dll') {
        param($reader)
        Require-Members $reader 'MainMenu' @('Options') @()
        Require-Members $reader 'LoadScreen' @('DoLoad') @()
        Require-Members $reader 'PauseScreen' @('OnOptions', 'OnResume') @()
        Require-Members $reader 'SpeedControlScreen' @('Pause', 'Unpause') @('pauseCount')
        Require-Members $reader 'ConfirmDialogScreen' @() @('titleText', 'popupMessage', 'confirmButton', 'cancelButton',
            'configurableButton', 'confirmAction', 'cancelAction', 'configurableAction', 'onDeactivateCB')
        Require-Members $reader 'Manager' @('MatchFootprint') @('events')
        Require-Members $reader 'KleiVersion' @() @('ChangeList')
    }
    Read-Metadata $TestAgentPath {
        param($reader)
        foreach ($handle in $reader.AssemblyReferences) {
            $name = $reader.GetString($reader.GetAssemblyReference($handle).Name)
            if ($name -eq 'System.Private.CoreLib') { throw 'Test agent references the host runtime instead of ONI assemblies' }
        }
    }
    Write-Output 'Test-agent game contract checks passed.'
}
Write-Output 'Game contract checks passed: patch targets, reflected fields, framework reference and assembly version.'
