# EdgeChronicles - minimal JSON accessor (PowerShell fallback).
#
# This file is intentionally ASCII-only. Windows PowerShell 5.1 decodes .ps1
# files without a BOM as ANSI, which corrupts non-ASCII characters and breaks
# string terminators. Keeping this file pure ASCII makes it encoding-proof.
# User-facing messages live in the bash layer (Tools/build/lib/common.sh).
#
# Usage:
#   powershell -NoProfile -File jsonq.ps1 file.json payload.sha256
#   powershell -NoProfile -File jsonq.ps1 --validate file.json
#   powershell -NoProfile -File jsonq.ps1 --flatten file.json
#
# Exit codes: 0 ok / 2 unreadable or invalid JSON / 3 path not found / 4 usage

param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$Path,

  [Parameter(Position = 1)]
  [string]$Key
)

$ErrorActionPreference = 'Stop'

function Exit-With([int]$code, [string]$message) {
  if ($message) { [Console]::Error.WriteLine($message) }
  exit $code
}

function Get-Node($node, [string]$key) {
  foreach ($seg in $key.Split('.')) {
    if ($null -eq $node) { Exit-With 3 ("value not found: " + $key) }
    $isList = ($node -is [System.Collections.IEnumerable]) -and
              (-not ($node -is [string])) -and
              (-not ($node -is [System.Management.Automation.PSCustomObject]))
    if ($isList) {
      $idx = 0
      if (-not [int]::TryParse($seg, [ref]$idx)) { Exit-With 3 ("array index expected: " + $key) }
      $arr = @($node)
      if ($idx -ge $arr.Count) { Exit-With 3 ("index out of range: " + $key) }
      $node = $arr[$idx]
    } else {
      $prop = $node.PSObject.Properties[$seg]
      if ($null -eq $prop) { Exit-With 3 ("field not found: " + $key) }
      $node = $prop.Value
    }
  }
  return $node
}

if ($Path -eq '--validate') {
  if (-not $Key) { Exit-With 4 'usage: jsonq.ps1 --validate <file.json>' }
  if (-not (Test-Path -LiteralPath $Key)) { Exit-With 2 ("file not found: " + $Key) }
  try {
    $null = Get-Content -Raw -Encoding UTF8 -LiteralPath $Key | ConvertFrom-Json
  } catch {
    Exit-With 2 ("invalid JSON: " + $Key + ": " + $_.Exception.Message)
  }
  exit 0
}

if ($Path -eq '--flatten') {
  if (-not $Key) { Exit-With 4 'usage: jsonq.ps1 --flatten <file.json>' }
  if (-not (Test-Path -LiteralPath $Key)) { Exit-With 2 ("file not found: " + $Key) }
  try {
    $doc = Get-Content -Raw -Encoding UTF8 -LiteralPath $Key | ConvertFrom-Json
  } catch {
    Exit-With 2 ("invalid JSON: " + $Key + ": " + $_.Exception.Message)
  }

  function Walk($node, $prefix) {
    if ($null -eq $node) { return }
    if ($node -is [System.Management.Automation.PSCustomObject]) {
      foreach ($prop in $node.PSObject.Properties) {
        $child = if ($prefix) { $prefix + '.' + $prop.Name } else { $prop.Name }
        Walk $prop.Value $child
      }
    } elseif (($node -is [System.Collections.IEnumerable]) -and -not ($node -is [string])) {
      $i = 0
      foreach ($item in $node) {
        Walk $item ("$prefix.$i")
        $i++
      }
    } elseif ($node -is [bool]) {
      Write-Output ("$prefix=" + $(if ($node) { 'true' } else { 'false' }))
    } elseif ($node -is [double] -or $node -is [decimal] -or $node -is [single]) {
      Write-Output ("$prefix=" + $node)
    } else {
      Write-Output ("$prefix=" + $node)
    }
  }

  Walk $doc ''
  exit 0
}

if (-not $Key) { Exit-With 4 'usage: jsonq.ps1 <file.json> <dotted.path>' }
if (-not (Test-Path -LiteralPath $Path)) { Exit-With 2 ("file not found: " + $Path) }
try {
  $doc = Get-Content -Raw -Encoding UTF8 -LiteralPath $Path | ConvertFrom-Json
} catch {
  Exit-With 2 ("invalid JSON: " + $Path + ": " + $_.Exception.Message)
}

$node = Get-Node $doc $Key

if ($null -eq $node) {
  Write-Output ''
} elseif ($node -is [bool]) {
  Write-Output $(if ($node) { 'true' } else { 'false' })
} elseif (($node -is [System.Collections.IEnumerable]) -and -not ($node -is [string]) -and -not ($node -is [System.Management.Automation.PSCustomObject])) {
  Write-Output (@($node) -join ',')
} else {
  Write-Output $node
}
exit 0
