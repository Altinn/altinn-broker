[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0, HelpMessage = "File size, e.g. 64MiB, 1GiB, 512MB, or bytes like 1073741824")]
    [string] $Size,

    [Parameter(HelpMessage = "Output file path. Defaults to a file in %TEMP%.")]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ByteCount {
    param([string] $Value)

    $normalized = $Value.Trim()
    if ($normalized -match '^\d+$') {
        return [long]::Parse($normalized)
    }

    if ($normalized -match '^(\d+(?:\.\d+)?)\s*([KMGT]?i?B?)$') {
        $amount = [double]::Parse($matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
        $unit = $matches[2].ToUpperInvariant()

        $multiplier = switch ($unit) {
            '' { 1 }
            'B' { 1 }
            'K' { 1000 }
            'KB' { 1000 }
            'M' { 1000 * 1000 }
            'MB' { 1000 * 1000 }
            'G' { 1000 * 1000 * 1000 }
            'GB' { 1000 * 1000 * 1000 }
            'T' { 1000 * 1000 * 1000 * 1000 }
            'TB' { 1000 * 1000 * 1000 * 1000 }
            'KIB' { 1024 }
            'MIB' { 1024 * 1024 }
            'GIB' { 1024 * 1024 * 1024 }
            'TIB' { 1024 * 1024 * 1024 * 1024 }
            default { throw "Unsupported size unit '$unit' in '$Value'." }
        }

        $bytes = [math]::Round($amount * $multiplier)
        if ($bytes -lt 1) {
            throw "Size must be at least 1 byte."
        }
        return [long]$bytes
    }

    throw "Invalid size '$Value'. Examples: 64MiB, 1GiB, 512MB, 1073741824"
}

function Format-ByteCount {
    param([long] $Bytes)

    if ($Bytes -ge 1GB) {
        return '{0:N2} GiB' -f ($Bytes / 1GB)
    }
    if ($Bytes -ge 1MB) {
        return '{0:N2} MiB' -f ($Bytes / 1MB)
    }
    return "$Bytes bytes"
}

$byteCount = ConvertTo-ByteCount -Value $Size
if (-not $OutputPath) {
    $timestamp = Get-Date -Format 'yyyyMMddHHmmss'
    $OutputPath = Join-Path $env:TEMP "altinn-broker-upload-$timestamp.bin"
}

$outputFile = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFile
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$chunkSize = 8MB
$remaining = $byteCount
$written = 0L
$buffer = New-Object byte[] $chunkSize
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

$formattedSize = Format-ByteCount -Bytes $byteCount
Write-Host "Creating $outputFile ($formattedSize)"

$fileStream = [System.IO.File]::Open(
    $outputFile,
    [System.IO.FileMode]::CreateNew,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None
)

try {
    while ($remaining -gt 0) {
        $writeSize = [Math]::Min($chunkSize, $remaining)
        if ($writeSize -eq $chunkSize) {
            $rng.GetBytes($buffer)
            $fileStream.Write($buffer, 0, $chunkSize)
        }
        else {
            $tail = New-Object byte[] $writeSize
            $rng.GetBytes($tail)
            $fileStream.Write($tail, 0, $writeSize)
        }

        $remaining -= $writeSize
        $written += $writeSize
    }
}
finally {
    $fileStream.Dispose()
    $rng.Dispose()
}

$formattedWritten = Format-ByteCount -Bytes $written
Write-Host "Done. Wrote $formattedWritten to:"
Write-Host $outputFile

Write-Output $outputFile
