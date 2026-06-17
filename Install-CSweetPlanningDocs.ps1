param(
    [Parameter(Mandatory = $false)]
    [string]$Destination = "."
)

$Source = Split-Path -Parent $MyInvocation.MyCommand.Path

Copy-Item -Path (Join-Path $Source "README.md") -Destination (Join-Path $Destination "README.md") -Force
New-Item -ItemType Directory -Path (Join-Path $Destination "docs") -Force | Out-Null
Copy-Item -Path (Join-Path $Source "docs\*") -Destination (Join-Path $Destination "docs") -Recurse -Force
Copy-Item -Path (Join-Path $Source "COMMIT_SEQUENCE.md") -Destination (Join-Path $Destination "COMMIT_SEQUENCE.md") -Force

Write-Host "CSweet planning documents copied to $Destination"
