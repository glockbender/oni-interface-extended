$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../src/CenteredHud/Options.cs')
$options = [CenteredHud.Options]::new()
if (!$options.Enabled -or $options.WidthPixels -ne 2560 -or $options.ShowGuides -or $options.EnableShortcuts) {
    throw 'Unsafe or unexpected first-install defaults'
}
$options.WidthPixels = [int]::MaxValue
$options.CycleKey = $null
$options.SettingsKey = ' '
$options.Normalize()
if ($options.WidthPixels -ne 32768 -or $options.CycleKey -ne 'F8' -or $options.SettingsKey -ne 'F9') {
    throw 'Out-of-range width or missing keys were not repaired'
}
$options.WidthPixels = -123
$options.Normalize()
if ($options.WidthPixels -ne 0) { throw 'Negative width must become full width' }
$options.WidthPixels = 3360
$options.Enabled = $false
$options.SettingsKey = 'F10'
$options.Normalize()
if ($options.Enabled -or $options.WidthPixels -ne 3360 -or $options.SettingsKey -ne 'F10') {
    throw 'Normalization must preserve intentional settings'
}
Write-Output 'Options checks passed: defaults, invalid values and preservation of custom settings.'
