#Requires -Version 5.1
<#
.SYNOPSIS
    Adds an EF Core migration to /db/migrations and regenerates /db/scripts/001_create_schema.sql.

.DESCRIPTION
    Migrations live outside the Infrastructure project (in /db/migrations) and are compiled into it via a
    <Compile Include> link. `dotnet ef` only searches inside the project directory for the existing model
    snapshot, so it writes the updated snapshot to a namespace-derived folder inside the project. This script
    moves it back next to the migrations and regenerates the idempotent schema script.

.EXAMPLE
    ./db/add-migration.ps1 -Name AddDeliveryReceipts
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string] $Name
)

$ErrorActionPreference = 'Stop'

$root           = Split-Path -Parent $PSScriptRoot
$project        = Join-Path $root 'src/SmsManagement.Infrastructure'
$migrationsDir  = Join-Path $root 'db/migrations'
$schemaScript   = Join-Path $root 'db/scripts/001_create_schema.sql'
$namespace      = 'SmsManagement.Infrastructure.Persistence.Migrations'
$snapshotName   = 'ApplicationDbContextModelSnapshot.cs'

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet ef migrations add $Name `
        --project $project --startup-project $project `
        --output-dir ../../db/migrations --namespace $namespace
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations add failed.' }

    # Relocate any snapshot that dotnet ef wrote inside the project directory. The list is materialized
    # first, because the loop deletes folders that a streaming enumeration would still be walking.
    $projectPath = (Resolve-Path $project).Path
    $strays = @(Get-ChildItem -Path $project -Filter $snapshotName -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })

    foreach ($stray in $strays) {
        Move-Item -Path $stray.FullName -Destination (Join-Path $migrationsDir $snapshotName) -Force

        # Remove the now-empty namespace folders that dotnet ef created.
        $dir = $stray.Directory
        while ($dir.FullName -ne $projectPath -and -not (Get-ChildItem $dir.FullName -Force)) {
            $parent = $dir.Parent
            Remove-Item $dir.FullName
            $dir = $parent
        }
    }

    dotnet ef migrations script --idempotent `
        --project $project --startup-project $project --output $schemaScript
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations script failed.' }

    Write-Host "Migration '$Name' added. Review db/migrations and db/scripts/001_create_schema.sql." -ForegroundColor Green
}
finally {
    Pop-Location
}
