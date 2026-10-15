# Static Unity asset integrity checks for the main/fix integration. Run in PowerShell 7.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
function Check($condition, $message) {
    if (-not $condition) { throw $message }
    Write-Output "PASS: $message"
}
function Read-Asset($path) { [IO.File]::ReadAllText((Join-Path $projectRoot $path)) }

foreach ($path in @('Assets/SceneBase.unity', 'Assets/Scenes/CroustyMainMenu.unity',
    'Assets/Jojo/SceneBaseJojo.unity', 'Assets/SceneBaseversiontest.unity',
    'Assets/Jojo/SceneBaseversiontest.unity', 'Assets/Menu/RetroBlendy SDF.asset')) {
    $content = Read-Asset $path
    Check (-not [regex]::IsMatch($content, '(?m)^(<<<<<<<|=======|>>>>>>>)')) "No conflict markers: $path"
    $ids = [Collections.Generic.HashSet[string]]::new()
    foreach ($match in [regex]::Matches($content, '(?m)^--- !u!\d+ &(-?\d+)')) {
        if (-not $ids.Add($match.Groups[1].Value)) { throw "Duplicate Unity object $($match.Groups[1].Value) in $path" }
    }
    Check ($ids.Count -gt 0) "Unique Unity object IDs: $path"
    $missing = [Collections.Generic.HashSet[string]]::new()
    foreach ($match in [regex]::Matches($content, '\{fileID: (-?\d+)\}')) {
        $id = $match.Groups[1].Value
        if ($id -ne '0' -and -not $ids.Contains($id)) { [void]$missing.Add($id) }
    }
    Check ($missing.Count -eq 0) "Internal references resolve: $path ($($missing -join ', '))"
    Check (-not $content.Contains('8cc7c5d0dbfd47f28d70103b5d89be94')) "No incoming inertia component: $path"
}

$scene = Read-Asset 'Assets/SceneBase.unity'
Check ($scene.Contains('modelRotation: {x: 0, y: 0, z: 0}') -and
    $scene -match 'alongBus: 0.1\r?\n  height: 0\r?\n') 'Local wing placement survives scene merge'
Check ($scene.Contains('randomStartingPower: 1') -and $scene.Contains('randomPrices: 1') -and
    $scene.Contains('flightLast: 1')) 'Incoming varied power progression enabled'
foreach ($component in @('BusAirTricks','BusBoostDisplay','BusWings','BusFlightController','PowerUnlockProgression')) {
    Check ($scene.Contains("Assembly-CSharp::$component")) "Gameplay component retained: $component"
}

# Glyph, character and atlas tables must belong to the same TMP asset revision.
$font = Read-Asset 'Assets/Menu/RetroBlendy SDF.asset'
$unicode = [Collections.Generic.HashSet[int]]::new()
foreach ($match in [regex]::Matches($font, 'm_Unicode: (\d+)')) { [void]$unicode.Add([int]$match.Groups[1].Value) }
foreach ($character in 'CREDITS0123456789$GPS'.ToCharArray()) {
    Check ($unicode.Contains([int][char]$character)) "RetroBlendy contains '$character'"
}
$glyphs = [Collections.Generic.HashSet[int]]::new()
foreach ($match in [regex]::Matches($font, '(?m)^  - m_Index: (\d+)')) { [void]$glyphs.Add([int]$match.Groups[1].Value) }
foreach ($match in [regex]::Matches($font, '(?m)^    m_GlyphIndex: (\d+)')) {
    Check ($glyphs.Contains([int]$match.Groups[1].Value)) "Character refers to existing glyph $($match.Groups[1].Value)"
}

Check ((Read-Asset 'Assets/Resources/Crousty/GarageRepairIcon.png.meta') -match 'spriteMode: 1') 'Garage icon imported as a sprite'
Check ((Read-Asset 'Assets/Scripts/Map/GarageGPSMarker.cs.meta') -match 'guid: [0-9a-f]{32}') 'Garage marker has a stable Unity GUID'
Write-Output 'INTEGRATION_ASSETS_CHECKS_PASS'
