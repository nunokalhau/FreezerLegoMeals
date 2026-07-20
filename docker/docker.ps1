[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('up', 'down', 'restart', 'pull', 'clean', 'reset')]
    [string]$Command = 'up'
)

$ErrorActionPreference = 'Stop'

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepositoryRoot = Resolve-Path (Join-Path $ScriptDirectory '..')
$DataDirectories = @(
    Join-Path $RepositoryRoot 'data/chromadb'
    Join-Path $RepositoryRoot 'data/redis'
)

function Invoke-DockerCompose {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    Push-Location $ScriptDirectory
    try {
        & docker compose @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Ensure-DataDirectories {
    foreach ($Directory in $DataDirectories) {
        if (-not (Test-Path $Directory)) {
            New-Item -ItemType Directory -Path $Directory -Force | Out-Null
        }
    }
}

switch ($Command) {
    'up' {
        Ensure-DataDirectories
        Invoke-DockerCompose up -d
    }
    'down' {
        Invoke-DockerCompose down
    }
    'restart' {
        Ensure-DataDirectories
        Invoke-DockerCompose down
        Invoke-DockerCompose up -d
    }
    'pull' {
        Invoke-DockerCompose pull
    }
    'clean' {
        Invoke-DockerCompose down --remove-orphans
    }
    'reset' {
        $Confirmation = Read-Host "This will delete data/chromadb and data/redis. Type RESET to continue"
        if ($Confirmation -ne 'RESET') {
            Write-Host 'Reset cancelled.'
            exit 0
        }

        Invoke-DockerCompose down --remove-orphans
        foreach ($Directory in $DataDirectories) {
            if (Test-Path $Directory) {
                Remove-Item -Recurse -Force $Directory
            }
        }
        Ensure-DataDirectories
        Write-Host 'Infrastructure data has been reset.'
    }
}
