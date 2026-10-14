# Windows PowerShell 5.1 and PowerShell 7. Arguments mirror install.sh.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InstallerArguments = @($args)
$TaskRoot = Split-Path -Parent $PSScriptRoot
$TaskExitCode = 2
Set-Location -LiteralPath $TaskRoot

function Write-Help {
    Write-Host @'
LearnForge: install missing requirements, start the local servers, and open your browser.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/install.ps1 [options]
  --check                 Read-only requirement report
  --install-only          Prepare the application without starting it
  --no-install            Use existing tools/packages; build and start
  --no-browser            Print the URL without opening a browser
  --api-port PORT         Require this API port (default: find one near 5080)
  --web-port PORT         Require this web port (default: find one near 4300)
  --startup-timeout SEC   Readiness timeout (default: 180 seconds)
  --data-dir DIRECTORY    Separate database and session keys (default: apps/api/data)
Tools/cache/logs: .learnforge/   Stop both servers: Ctrl+C
'@
}

function Get-Download([string]$Url, [string]$Destination) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    for ($Attempt = 1; $Attempt -le 3; $Attempt++) {
        try {
            Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing -TimeoutSec 600
            return
        } catch {
            if ($Attempt -eq 3) { throw "Download failed from $(([Uri]$Url).Host). Check internet/proxy settings." }
            Start-Sleep -Seconds 2
        }
    }
}

try {
    if (($InstallerArguments -contains '--help') -or ($InstallerArguments -contains '-h')) { Write-Help; exit 0 }
    for ($TaskIndex = 0; $TaskIndex -lt $InstallerArguments.Count; $TaskIndex++) {
        $TaskArgument = $InstallerArguments[$TaskIndex]
        if ($TaskArgument -in @('--check', '--no-install', '--install-only', '--no-browser', '--repair-native')) { continue }
        if ($TaskArgument -notin @('--api-port', '--web-port', '--startup-timeout', '--data-dir')) { throw "Unknown option: $TaskArgument" }
        $TaskIndex++
        if ($TaskIndex -ge $InstallerArguments.Count -or $InstallerArguments[$TaskIndex].StartsWith('--') -or -not $InstallerArguments[$TaskIndex]) { throw "Missing value for $TaskArgument" }
        if ($TaskArgument -ne '--data-dir') {
            $TaskValue = $InstallerArguments[$TaskIndex]
            $TaskLimit = if ($TaskArgument -eq '--startup-timeout') { 3600 } else { 65535 }
            if ($TaskValue -notmatch '^\d{1,5}$' -or [int]$TaskValue -lt 1 -or [int]$TaskValue -gt $TaskLimit) { throw "Invalid value for $TaskArgument" }
        }
    }
    if (($InstallerArguments -contains '--install-only') -and (($InstallerArguments -contains '--check') -or ($InstallerArguments -contains '--no-install'))) { throw '--install-only cannot be combined with --check or --no-install.' }
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or [Environment]::OSVersion.Version.Build -lt 22000) {
        throw 'This installer targets Windows 11. On macOS/Linux use scripts/install.sh.'
    }
    $TaskArchitecture = $env:PROCESSOR_ARCHITECTURE
    if ($env:PROCESSOR_ARCHITEW6432) { $TaskArchitecture = $env:PROCESSOR_ARCHITEW6432 }
    switch ($TaskArchitecture) {
        'ARM64' { $TaskArch = 'arm64' }
        'AMD64' { $TaskArch = 'x64' }
        default { throw "Unsupported native architecture: $TaskArchitecture" }
    }

    # Invoked only if a newly acquired SDK reports a missing Windows C runtime.
    if ($InstallerArguments -contains '--repair-native') {
        $TaskExitCode = 3
        $TaskPrerequisite = Join-Path $TaskRoot '.learnforge\cache\vc_redist.exe'
        New-Item -ItemType Directory -Path (Split-Path $TaskPrerequisite) -Force | Out-Null
        Get-Download "https://aka.ms/vs/17/release/vc_redist.$TaskArch.exe" $TaskPrerequisite
        $TaskSignature = Get-AuthenticodeSignature -LiteralPath $TaskPrerequisite
        if ($TaskSignature.Status -ne 'Valid' -or $TaskSignature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
            throw 'Microsoft prerequisite signature validation failed.'
        }
        Write-Host 'Installing the missing Microsoft C runtime. Windows may request administrator access.'
        $TaskProcess = Start-Process -FilePath $TaskPrerequisite -ArgumentList '/install', '/passive', '/norestart' -Verb RunAs -Wait -PassThru
        if ($TaskProcess.ExitCode -eq 3010) { throw 'The prerequisite requires a Windows restart. Restart, then run this script again.' }
        if ($TaskProcess.ExitCode -notin @(0, 1638)) { throw "Prerequisite installer failed: $($TaskProcess.ExitCode)" }
        exit 0
    }

    $TaskReadOnly = ($InstallerArguments -contains '--check') -or ($InstallerArguments -contains '--no-install')
    $TaskVersion = (Get-Content -LiteralPath (Join-Path $TaskRoot '.node-version') -Raw).Trim()
    if ($TaskVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid .node-version.' }
    $TaskNpmVersion = ((Get-Content -LiteralPath (Join-Path $TaskRoot 'apps\web\package.json') -Raw | ConvertFrom-Json).packageManager -replace '^npm@', '')
    $TaskManaged = Join-Path $TaskRoot ".learnforge\tools\node\v$TaskVersion"
    $TaskCandidates = @((Join-Path $TaskManaged 'node.exe'))
    $TaskCommand = Get-Command node.exe -ErrorAction SilentlyContinue
    if ($TaskCommand) { $TaskCandidates += $TaskCommand.Source }
    $TaskNode = $null
    foreach ($TaskCandidate in $TaskCandidates) {
        if (-not (Test-Path -LiteralPath $TaskCandidate)) { continue }
        $TaskNpmCli = Join-Path (Split-Path $TaskCandidate) 'node_modules\npm\bin\npm-cli.js'
        if (-not (Test-Path -LiteralPath $TaskNpmCli)) { continue }
        $TaskFound = & $TaskCandidate --version 2>$null
        $TaskFoundArch = & $TaskCandidate -p 'process.arch' 2>$null
        if ($LASTEXITCODE -eq 0 -and $TaskFound -eq "v$TaskVersion" -and $TaskFoundArch -eq $TaskArch) {
            $TaskFoundNpm = & $TaskCandidate $TaskNpmCli --version 2>$null
            if ($LASTEXITCODE -eq 0 -and $TaskFoundNpm -eq $TaskNpmVersion) { $TaskNode = $TaskCandidate; break }
        }
    }
    if (-not $TaskNode -and $TaskReadOnly) {
        Write-Host "Required: Node $TaskVersion and npm $TaskNpmVersion."
        Write-Host 'Run scripts/install.ps1 without --check/--no-install to acquire a private copy.'
        $TaskDotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
        if ($TaskDotnet) { Write-Host "Installed dotnet host: $($TaskDotnet.Source)" }
        exit 2
    }
    if (-not $TaskNode) {
        $TaskExitCode = 3
        $TaskCache = Join-Path $TaskRoot '.learnforge\cache'
        $TaskTools = Join-Path $TaskRoot '.learnforge\tools\node'
        New-Item -ItemType Directory -Path $TaskCache, $TaskTools -Force | Out-Null
        $TaskLock = Join-Path $TaskRoot '.learnforge\node-bootstrap.lock'
        if (Test-Path -LiteralPath $TaskLock) {
            $TaskPidFile = Join-Path $TaskLock 'pid'
            $TaskOwner = if (Test-Path -LiteralPath $TaskPidFile) { (Get-Content -LiteralPath $TaskPidFile -Raw).Trim() } else { '' }
            if ($TaskOwner -match '^\d+$' -and -not (Get-Process -Id ([int]$TaskOwner) -ErrorAction SilentlyContinue)) {
                Remove-Item -LiteralPath $TaskLock -Recurse
            } else { throw 'Another Node installation is running; retry when it finishes.' }
        }
        New-Item -ItemType Directory -Path $TaskLock | Out-Null
        Set-Content -LiteralPath (Join-Path $TaskLock 'pid') -Value $PID
        $TaskStage = Join-Path $TaskTools ("staging-" + [Guid]::NewGuid().ToString('N'))
        $TaskPartial = $null
        try {
            New-Item -ItemType Directory -Path $TaskStage | Out-Null
            $TaskArchive = "node-v$TaskVersion-win-$TaskArch.zip"
            $TaskArchivePath = Join-Path $TaskCache $TaskArchive
            $TaskBase = "https://nodejs.org/dist/v$TaskVersion"
            $TaskSums = Join-Path $TaskStage 'SHASUMS256.txt'
            Get-Download "$TaskBase/SHASUMS256.txt" $TaskSums
            $TaskHash = $null
            foreach ($TaskLine in Get-Content -LiteralPath $TaskSums) {
                if ($TaskLine -match ('^([a-fA-F0-9]{64})\s+' + [Regex]::Escape($TaskArchive) + '$')) { $TaskHash = $Matches[1].ToLowerInvariant() }
            }
            if (-not $TaskHash) { throw 'Official Node checksum was not found.' }
            $TaskValid = (Test-Path -LiteralPath $TaskArchivePath) -and ((Get-FileHash -LiteralPath $TaskArchivePath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $TaskHash)
            if (-not $TaskValid) {
                Write-Host "Installing Node $TaskVersion (Windows/$TaskArch)..."
                $TaskPartial = "$TaskArchivePath.part.$PID"
                Get-Download "$TaskBase/$TaskArchive" $TaskPartial
                if ((Get-FileHash -LiteralPath $TaskPartial -Algorithm SHA256).Hash.ToLowerInvariant() -ne $TaskHash) { throw 'Node archive checksum mismatch.' }
                Move-Item -LiteralPath $TaskPartial -Destination $TaskArchivePath -Force
            }
            Expand-Archive -LiteralPath $TaskArchivePath -DestinationPath $TaskStage
            $TaskExtracted = Join-Path $TaskStage "node-v$TaskVersion-win-$TaskArch"
            $TaskStagedNode = Join-Path $TaskExtracted 'node.exe'
            if ((& $TaskStagedNode --version) -ne "v$TaskVersion" -or $LASTEXITCODE -ne 0) { throw 'Downloaded Node failed its executable check.' }
            $TaskStagedNpm = Join-Path $TaskExtracted 'node_modules\npm\bin\npm-cli.js'
            if ((& $TaskStagedNode $TaskStagedNpm --version) -ne $TaskNpmVersion -or $LASTEXITCODE -ne 0) { throw 'Downloaded npm does not match packageManager.' }
            if (Test-Path -LiteralPath $TaskManaged) { Move-Item -LiteralPath $TaskManaged -Destination "$TaskManaged.previous.$PID" }
            Move-Item -LiteralPath $TaskExtracted -Destination $TaskManaged
            $TaskNode = Join-Path $TaskManaged 'node.exe'
        } finally {
            if ($TaskPartial -and (Test-Path -LiteralPath $TaskPartial)) { Remove-Item -LiteralPath $TaskPartial -Force }
            if (Test-Path -LiteralPath $TaskStage) { Remove-Item -LiteralPath $TaskStage -Recurse -Force }
            Remove-Item -LiteralPath $TaskLock -Recurse -Force
        }
    }
    $env:PATH = (Split-Path $TaskNode) + [IO.Path]::PathSeparator + $env:PATH
    & $TaskNode (Join-Path $TaskRoot 'scripts\install.mjs') @InstallerArguments
    exit $LASTEXITCODE
} catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit $TaskExitCode
}
