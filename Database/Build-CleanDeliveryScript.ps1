<#
Builds a standalone SQL Server script for a new, empty ClinicDB installation.

The generated script combines the authoritative Schema.sql with the same
migration order used by DatabaseSession. It intentionally does not copy data
from a development database. The only inserts retained are application setup
defaults (the recovery admin and clinic settings) and idempotent legacy
migration statements, which have no effect on an empty database.
#>

$ErrorActionPreference = 'Stop'

$databaseDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDirectory = Split-Path -Parent $databaseDirectory
$outputPath = Join-Path $projectDirectory 'ClinicDB_CleanDelivery.sql'

$sections = [System.Collections.Generic.List[string]]::new()
$sections.Add(@"
/*
 Clinic Management System — Clean Delivery Database
 Generated from the current application schema and migrations.

 This script creates/updates ClinicDB without development business data.
 It preserves only application bootstrap defaults required for first login.
 Change the default administrator password after the first sign-in.
*/
IF DB_ID(N'ClinicDB') IS NULL
    CREATE DATABASE [ClinicDB];
GO
USE [ClinicDB];
GO
"@)

$sections.Add((Get-Content -LiteralPath (Join-Path $databaseDirectory 'Schema.sql') -Raw))
$sections.Add((Get-Content -LiteralPath (Join-Path $databaseDirectory 'Migration_AddDiscountRefunds.sql') -Raw))

Get-ChildItem -LiteralPath (Join-Path $databaseDirectory 'Migrations') -Filter '*.sql' |
    Sort-Object FullName |
    ForEach-Object {
        $sections.Add("`r`n/* Migration: $($_.Name) */`r`n")
        $sections.Add((Get-Content -LiteralPath $_.FullName -Raw))
    }

Set-Content -LiteralPath $outputPath -Value ($sections -join "`r`nGO`r`n") -Encoding utf8
Write-Host "Created clean delivery script: $outputPath"
