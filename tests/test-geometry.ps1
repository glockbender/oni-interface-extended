$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '..\src\CenteredHud\HudGeometry.cs')
function Assert-Near([double]$actual, [double]$expected, [string]$message) {
    if ([double]::IsNaN($actual) -or [double]::IsInfinity($actual) -or [Math]::Abs($actual - $expected) -gt 0.001) {
        throw "$message Expected $expected; got $actual"
    }
}
Assert-Near ([CenteredHud.HudGeometry]::Width(5120, 2560, 1.221)) 2560 '16:9 width at 110% UI scale'
Assert-Near ([CenteredHud.HudGeometry]::Width(5120, 3360, 1.221)) 3360 '21:9 width'
Assert-Near ([CenteredHud.HudGeometry]::Width(5120, 0, 1.221)) 5120 'Full screen'
Assert-Near ([CenteredHud.HudGeometry]::Width(1920, 2560, 1)) 1920 'Narrower monitor'
Assert-Near ([CenteredHud.HudGeometry]::Width(5120, 100, 1.5)) 1920 'Minimum usable logical width'
Assert-Near ([CenteredHud.HudGeometry]::Width(800, 100, 2)) 800 'Minimum never exceeds screen'
Assert-Near ([CenteredHud.HudGeometry]::Width(5120, 2560, [double]::NaN)) 2560 'Invalid scale'
Assert-Near ([CenteredHud.HudGeometry]::Width(5120, -1, 1)) 5120 'Invalid width'
Assert-Near ([CenteredHud.HudGeometry]::Width(0, 2560, 1)) 0 'No initialized screen'
Assert-Near ([CenteredHud.HudGeometry]::Width([double]::NaN, 2560, 1)) 0 'NaN viewport'
Assert-Near ([CenteredHud.HudGeometry]::Width([double]::PositiveInfinity, 2560, 1)) 0 'Infinite viewport'
Assert-Near ([CenteredHud.HudGeometry]::StepWidth(5120, 0, 1.221, -160)) 4960 'Decrease from full width'
Assert-Near ([CenteredHud.HudGeometry]::StepWidth(5120, 0, 1.221, 160)) 5120 'Increase at full width'
Assert-Near ([CenteredHud.HudGeometry]::StepWidth(5120, 2560, 1.221, -160)) 2400 'Decrease custom width'
Assert-Near ([CenteredHud.HudGeometry]::StepWidth(5120, 100, 1.5, -160)) 1920 'Step respects scaled minimum'
Assert-Near ([CenteredHud.HudGeometry]::StepWidth(800, 0, 2, -160)) 800 'Step on narrow screen'
Assert-Near ([CenteredHud.HudGeometry]::StepWidth(5120, 2560, [double]::NaN, 160)) 2720 'Step with invalid scale'
foreach ($fraction in @(1, 0.5, (3360.0/5120))) {
    $left = [CenteredHud.HudGeometry]::MapAnchor(0, $fraction)
    $right = [CenteredHud.HudGeometry]::MapAnchor(1, $fraction)
    Assert-Near ($left + $right) 1 'Symmetric margins'
    Assert-Near ([CenteredHud.HudGeometry]::MapAnchor(0.5, $fraction)) 0.5 'Center stays centered'
    Assert-Near (($right - $left) * 5120) ($fraction * 5120) 'Stretch container physical width'
    foreach ($anchor in @(0, 0.2, 0.5, 1)) {
        # A point anchored panel keeps its physical inset at any canvas scale.
        $mapped = [CenteredHud.HudGeometry]::MapAnchor($anchor, $fraction)
        foreach ($scale in @(1, 1.1, 1.221, 2)) {
            $position = $mapped * (5120/$scale) + 20
            Assert-Near ($position * $scale) (($left + $anchor*$fraction)*5120 + 20*$scale) 'Anchor mapping with UI scaling'
        }
    }
}
Assert-Near ([CenteredHud.HudGeometry]::MapAnchor(0, 1)) 0 'Restore left anchor'
Assert-Near ([CenteredHud.HudGeometry]::MapAnchor(1, 1)) 1 'Restore right anchor'
Write-Output 'Geometry checks passed: widths, limits, stepping, symmetry, center, stretching, UI scales and restoration.'
