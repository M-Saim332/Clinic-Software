# ============================================================
#  build_installers.ps1
#  One-click script to publish the app and compile both
#  Inno Setup installers (Server + Client).
#
#  Usage:  .\build_installers.ps1
#  Output: Installer\ClinicSetup_Server.exe
#          Installer\ClinicSetup_Client.exe
# ============================================================

$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot
$ISCC    = "C:\Program Files\Inno Setup 7\ISCC.exe"
$Publish = Join-Path $Root "publish"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Clinic Management System — Build Tool " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Verify Inno Setup ────────────────────────────────────────────────
if (-not (Test-Path $ISCC)) {
    Write-Error "Inno Setup 7 not found at: $ISCC`nPlease install from https://jrsoftware.org/isdl.php"
    exit 1
}
Write-Host "[OK] Inno Setup 7 found." -ForegroundColor Green

# ── Step 2: dotnet publish (self-contained, Release) ─────────────────────────
Write-Host ""
Write-Host "[1/3] Publishing application (self-contained, win-x64, Release)..." -ForegroundColor Yellow
Write-Host "      This bundles .NET 10 runtime — no installation needed on target PCs." -ForegroundColor Gray

# Remove old publish folder for a clean output
if (Test-Path $Publish) {
    Remove-Item $Publish -Recurse -Force
    Write-Host "      Removed old publish folder." -ForegroundColor Gray
}

$publishArgs = @(
    "publish", "ClinicSystem.UI.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", $Publish,
    "-p:PublishSingleFile=false",
    "-p:PublishReadyToRun=true",
    "--nologo"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed. Fix build errors before creating installers."
    exit 1
}

# Remove appsettings.local.json from publish output (machine-specific, must not ship)
$localJson = Join-Path $Publish "appsettings.local.json"
if (Test-Path $localJson) {
    Remove-Item $localJson -Force
    Write-Host "      Removed appsettings.local.json from publish output." -ForegroundColor Gray
}

Write-Host "[OK] Publish succeeded. Output: $Publish" -ForegroundColor Green

# ── Step 3: Compile Server installer ─────────────────────────────────────────
Write-Host ""
Write-Host "[2/3] Compiling Doctor PC (Server) installer..." -ForegroundColor Yellow
& $ISCC (Join-Path $Root "ClinicSetup_Server.iss")
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup failed for Server installer."
    exit 1
}
Write-Host "[OK] ClinicSetup_Server.exe created." -ForegroundColor Green

# ── Step 4: Compile Client installer ─────────────────────────────────────────
Write-Host ""
Write-Host "[3/3] Compiling Reception PC (Client) installer..." -ForegroundColor Yellow
& $ISCC (Join-Path $Root "ClinicSetup_Client.iss")
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup failed for Client installer."
    exit 1
}
Write-Host "[OK] ClinicSetup_Client.exe created." -ForegroundColor Green

# ── Done ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  BUILD COMPLETE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$serverExe = Join-Path $Root "Installer\ClinicSetup_Server.exe"
$clientExe = Join-Path $Root "Installer\ClinicSetup_Client.exe"

if (Test-Path $serverExe) {
    $size = [math]::Round((Get-Item $serverExe).Length / 1MB, 1)
    Write-Host "  Doctor PC  (Server): Installer\ClinicSetup_Server.exe  ($size MB)" -ForegroundColor White
}
if (Test-Path $clientExe) {
    $size = [math]::Round((Get-Item $clientExe).Length / 1MB, 1)
    Write-Host "  Reception  (Client): Installer\ClinicSetup_Client.exe  ($size MB)" -ForegroundColor White
}

Write-Host ""
Write-Host "  Deployment steps:" -ForegroundColor Gray
Write-Host "  1. Copy both .exe files to a USB drive." -ForegroundColor Gray
Write-Host "  2. On Doctor PC: Run ClinicSetup_Server.exe" -ForegroundColor Gray
Write-Host "     Then open SSMS and run: Database\Schema.sql (in the install folder)" -ForegroundColor Gray
Write-Host "  3. On Reception PC: Run ClinicSetup_Client.exe" -ForegroundColor Gray
Write-Host "     Enter Doctor PC IP in the DB Setup screen." -ForegroundColor Gray
Write-Host ""
