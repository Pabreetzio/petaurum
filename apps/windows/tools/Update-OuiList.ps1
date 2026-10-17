<#
.SYNOPSIS
    Regenerates src/Petaurum.Windows/Discovery/oui.txt from the IEEE MAC address registries.

.DESCRIPTION
    Downloads MA-L (24-bit), MA-M (28-bit) and MA-S (36-bit) assignments and writes one
    "<hex prefix><TAB><vendor>" line per assignment, with legal suffixes dropped so names stay short.
    See specs/domain/device.md, "Vendor". Run it now and then; the output is checked in.

    Pass -SourceDir to read oui.csv, mam.csv and oui36.csv from a folder instead of downloading.
#>
param([string] $SourceDir)

$ErrorActionPreference = 'Stop'
$registries = [ordered]@{ 'oui.csv' = 'oui/oui.csv'; 'mam.csv' = 'oui28/mam.csv'; 'oui36.csv' = 'oui36/oui36.csv' }
$output = Join-Path $PSScriptRoot '..\src\Petaurum.Windows\Discovery\oui.txt'

# IEEE's firewall rejects requests that don't look like a browser.
$userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36'

# Applied repeatedly to the end of a name, so "Foo Co., Ltd." loses both parts.
$suffix = '(?i)[\s,]+(inc|incorporated|corp|corporate|corporation|co|company|ltd|limited|llc|l\.l\.c|plc|gmbh|ag|kg|' +
          'se|sa|s\.a|sas|s\.a\.s|sarl|srl|s\.r\.l|spa|s\.p\.a|bv|b\.v|nv|n\.v|ab|a/s|as|oy|oyj|kk|k\.k|pty|pte|' +
          's\.l|sl|sdn\s*bhd|bhd|pvt|private|sp\.\s*z\s*o\.o|zrt|kft|&)\.?$'

function Get-ShortName([string] $name) {
    $name = ($name -replace '\s+', ' ').Trim()
    do {
        $before = $name
        $name = ($name -replace $suffix, '').Trim(' ', ',', '.', '-')
    } while ($name -ne $before -and $name.Length -gt 0)
    if ($name.Length -eq 0) { return $before }
    return $name
}

$temp = $null
if (-not $SourceDir) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) "petaurum-oui-$([guid]::NewGuid())"
    New-Item -ItemType Directory $temp | Out-Null
    foreach ($file in $registries.Keys) {
        Write-Host "Downloading $file…"
        Invoke-WebRequest "https://standards-oui.ieee.org/$($registries[$file])" -OutFile (Join-Path $temp $file) -UserAgent $userAgent
    }
    $SourceDir = $temp
}

try {
    $lines = foreach ($file in $registries.Keys) {
        foreach ($row in Import-Csv (Join-Path $SourceDir $file)) {
            $vendor = Get-ShortName $row.'Organization Name'
            # "Private" assignments hide the owner; MA-L blocks sold on as MA-M/MA-S name the IEEE itself.
            if ($vendor -eq '' -or $vendor -eq 'Private' -or $vendor -eq 'IEEE Registration Authority') { continue }
            "$($row.Assignment.ToUpperInvariant())`t$vendor"
        }
    }
    $sorted = [Collections.Generic.List[string]]::new([string[]] $lines)
    $sorted.Sort([StringComparer]::Ordinal)
    [IO.File]::WriteAllLines($output, $sorted, [Text.UTF8Encoding]::new($false))
    Write-Host "Wrote $($sorted.Count) prefixes to $output"
}
finally {
    if ($temp) { Remove-Item -Recurse -Force $temp }
}
