# Smoke checklist for TicTacToe.
# Runs each scripted scenario, checks key output substrings, prints a pass/fail summary.
# Usage from repo root: pwsh -ExecutionPolicy Bypass -File tests/smoke.ps1

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

# Deterministic RNG so AI random moves are reproducible across runs.
# Seed 13 is chosen so the Captain test reaches both block scenarios:
# AI's random opener is C2 (a square that shares no winning line with A3),
# so Captain's later block at A3 doesn't accidentally create a winning threat —
# meaning the next forced move is the row-1 block at C1.
$env:TICTACTOE_SEED = '13'

dotnet build --nologo | Out-Null

function Run-Case {
  param(
    [string]$Name,
    [string]$InputFile,
    [hashtable]$Expect  # keys: pattern, count
  )
  $out = Get-Content $InputFile | dotnet run --project . --no-build 2>&1 | Out-String
  $allPass = $true
  $details = @()
  foreach ($pattern in $Expect.Keys) {
    $expected = $Expect[$pattern]
    $actual = ([regex]::Matches($out, $pattern)).Count
    if ($actual -ne $expected) { $allPass = $false }
    $details += "  '$pattern' = $actual (expected $expected)"
  }
  $status = if ($allPass) { 'PASS' } else { 'FAIL' }
  Write-Host "[$status] $Name"
  if (-not $allPass) { $details | ForEach-Object { Write-Host $_ } }
  return $allPass
}

$results = @()
$results += Run-Case 'row win'      'tests/inputs/test-input-rowwin.txt'  @{
  'The Ascendant holds the line' = 1
  'Stalemates 0' = 1
}
$results += Run-Case 'column win'   'tests/inputs/test-input-colwin.txt'  @{
  'The Ascendant holds the line' = 1
}
$results += Run-Case 'diagonal win' 'tests/inputs/test-input-diagwin.txt' @{
  'The Ascendant holds the line' = 1
}
$results += Run-Case 'draw'         'tests/inputs/test-input-draw.txt'    @{
  'Stalemate\.' = 1
  'Stalemates 1' = 1
}
$results += Run-Case 'invalid input recovery' 'tests/inputs/test-input-invalid.txt' @{
  'Invalid coordinates' = 3
  'already claimed' = 1
  'holds the line' = 1
}
# With seed=2 the AI opens at B3, so both blocks (A3 column threat, C1 row threat)
# are forced and observable.
$results += Run-Case 'Captain blocks both threats' 'tests/inputs/test-input-captain.txt' @{
  'The Crimson Vanguard claims sector A3' = 1
  'The Crimson Vanguard claims sector C1' = 1
}
$results += Run-Case 'Warlord (column-A attack) never loses' 'tests/inputs/test-input-warlord.txt' @{
  'The Ascendant holds the line' = 0
}
$results += Run-Case 'Warlord (center attack) never loses' 'tests/inputs/test-input-warlord-center.txt' @{
  'The Ascendant holds the line' = 0
}
$results += Run-Case 'Warlord (corner attack) never loses' 'tests/inputs/test-input-warlord-corner.txt' @{
  'The Ascendant holds the line' = 0
}
$results += Run-Case 'quit from menu (no board, exit 0)' 'tests/inputs/test-input-quit.txt' @{
  '┌───┬───┬───┐' = 0
  'ORDER VS REBELLION' = 1
}

$passed = ($results | Where-Object { $_ }).Count
$total = $results.Count
Write-Host ""
Write-Host "============================================="
Write-Host " $passed / $total scenarios passed."
Write-Host "============================================="

if ($passed -ne $total) { exit 1 }
