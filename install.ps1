# Copilot Companion installer for Windows (Rider plugin + Visual Studio VSIX).
#   irm https://raw.githubusercontent.com/maisa92/copilot-companion/main/install.ps1 | iex
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'  # much faster Invoke-WebRequest downloads

$repo = 'maisa92/copilot-companion'
$pluginDirName = 'copilot-companion'

Write-Host 'Copilot Companion installer'
Write-Host ''

# Stable redirects to the newest release; no GitHub API call (avoids rate limits).
$zipUrl  = "https://github.com/$repo/releases/latest/download/copilot-companion.zip"
$vsixUrl = "https://github.com/$repo/releases/latest/download/CopilotCompanion.vsix"

$tmp = Join-Path $env:TEMP "copilot-companion-install-$([guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Path $tmp | Out-Null

try {
    # --- JetBrains Rider ------------------------------------------------------
    $riderDirs = @(Get-ChildItem -Path (Join-Path $env:APPDATA 'JetBrains') -Directory -Filter 'Rider*' -ErrorAction SilentlyContinue)
    if ($riderDirs.Count -gt 0) {
        $zipPath = Join-Path $tmp 'copilot-companion.zip'
        Write-Host 'Downloading the Rider plugin...'
        Invoke-WebRequest $zipUrl -OutFile $zipPath
        foreach ($riderDir in $riderDirs) {
            $plugins = Join-Path $riderDir.FullName 'plugins'
            New-Item -ItemType Directory -Path $plugins -Force | Out-Null
            $target = Join-Path $plugins $pluginDirName
            if (Test-Path $target) { Remove-Item $target -Recurse -Force }
            Expand-Archive -Path $zipPath -DestinationPath $plugins -Force
            Write-Host "Rider plugin installed into $plugins"
        }
        Write-Host 'Restart Rider to finish.'
    } else {
        Write-Host 'No Rider installation found (skipped). Manual install: Settings | Plugins | Install Plugin from Disk...'
    }

    # --- Visual Studio --------------------------------------------------------
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsixInstaller = & $vswhere -latest -prerelease -products * -find '**\VSIXInstaller.exe' | Select-Object -First 1
        if ($vsixInstaller) {
            $vsixPath = Join-Path $tmp 'CopilotCompanion.vsix'
            Write-Host 'Downloading the Visual Studio extension...'
            Invoke-WebRequest $vsixUrl -OutFile $vsixPath
            Write-Host ''
            Write-Host 'Opening the VSIX Installer window - click "Install" there.'
            Write-Host '(If Visual Studio is running, close it first; the installer waits for it.)'
            $p = Start-Process -FilePath $vsixInstaller -ArgumentList "`"$vsixPath`"" -Wait -PassThru
            if ($p.ExitCode -eq 0) {
                Write-Host 'Visual Studio extension installed.'
            } else {
                Write-Host "VSIX Installer exited with code $($p.ExitCode) (1002 = cancelled/already installed)."
                Write-Host "You can also install by double-clicking the .vsix from https://github.com/$repo/releases"
            }
        } else {
            Write-Host 'VSIXInstaller.exe not found (skipped). Double-click the .vsix from the Releases page instead.'
        }
    } else {
        Write-Host 'Visual Studio not found (skipped).'
    }

    Write-Host ''
    Write-Host 'Done.'
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
