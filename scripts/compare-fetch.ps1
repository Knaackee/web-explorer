[CmdletBinding()]
param(
    [string[]]$Urls = @(
        'https://www.spiegel.de',
        'https://www.zeit.de',
        'https://www.heise.de',
        'https://www.tagesschau.de'
    ),
    [string]$OutputPath = '',
    [int]$TimeoutMs = 45000,
    [switch]$UseBuiltCli
)

$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function Get-OutputPath {
    param(
        [string]$RepoRoot,
        [string]$RequestedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return [System.IO.Path]::GetFullPath($RequestedPath)
    }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    return Join-Path $RepoRoot "fetch-comparison-$stamp.txt"
}

function Get-CliCommand {
    param(
        [string]$RepoRoot,
        [switch]$PreferBuiltCli
    )

    $cliProjectPath = Join-Path $RepoRoot 'src\WebExplorer.Cli\WebExplorer.Cli.csproj'
    $builtCliPath = Join-Path $RepoRoot 'src\WebExplorer.Cli\bin\Release\net10.0\win-x64\wxp.exe'

    if ($PreferBuiltCli -and (Test-Path $builtCliPath)) {
        return @{
            FileName = $builtCliPath
            PrefixArguments = @()
            Mode = 'built-cli'
        }
    }

    return @{
        FileName = 'dotnet'
        PrefixArguments = @('run', '--no-build', '-c', 'Release', '-f', 'net10.0', '--project', $cliProjectPath, '--')
        Mode = 'dotnet-run'
    }
}

function Join-ProcessArguments {
    param(
        [string[]]$Arguments
    )

    return ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

function Invoke-WebExplorerFetch {
    param(
        [hashtable]$Command,
        [string]$Url,
        [string]$Renderer,
        [int]$TimeoutMs
    )

    $allArguments = @()
    $allArguments += $Command.PrefixArguments
    $allArguments += @('fetch', '--renderer', $Renderer, '--timeout-ms', $TimeoutMs, $Url)

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Command.FileName
    $psi.WorkingDirectory = Get-RepoRoot
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Arguments = Join-ProcessArguments -Arguments $allArguments

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        [void]$process.Start()

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        if (-not $process.WaitForExit($TimeoutMs + 15000)) {
            try {
                $process.Kill($true)
            }
            catch {
            }

            return [ordered]@{
                Renderer = $Renderer
                ExitCode = -1
                DurationMs = $stopwatch.ElapsedMilliseconds
                TimedOut = $true
                StdOut = ''
                StdErr = 'Command timed out.'
            }
        }

        $stdoutTask.Wait()
        $stderrTask.Wait()

        return [ordered]@{
            Renderer = $Renderer
            ExitCode = $process.ExitCode
            DurationMs = $stopwatch.ElapsedMilliseconds
            TimedOut = $false
            StdOut = $stdoutTask.Result
            StdErr = $stderrTask.Result
        }
    }
    finally {
        $stopwatch.Stop()
        $process.Dispose()
    }
}

function Get-Excerpt {
    param(
        [string]$Text,
        [int]$Length = 1200
    )

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    if ($Text.Length -le $Length) {
        return $Text
    }

    return $Text.Substring(0, $Length) + "`r`n... [truncated]"
}

function Add-ResultBlock {
    param(
        [System.Text.StringBuilder]$Builder,
        [string]$Url,
        [hashtable]$HttpResult,
        [hashtable]$PlaywrightResult
    )

    [void]$Builder.AppendLine(('=' * 100))
    [void]$Builder.AppendLine("URL: $Url")
    [void]$Builder.AppendLine(('=' * 100))
    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine('Summary')
    [void]$Builder.AppendLine("- http: exit=$($HttpResult.ExitCode), durationMs=$($HttpResult.DurationMs), chars=$($HttpResult.StdOut.Length), timedOut=$($HttpResult.TimedOut)")
    [void]$Builder.AppendLine("- playwright: exit=$($PlaywrightResult.ExitCode), durationMs=$($PlaywrightResult.DurationMs), chars=$($PlaywrightResult.StdOut.Length), timedOut=$($PlaywrightResult.TimedOut)")
    [void]$Builder.AppendLine()

    [void]$Builder.AppendLine('HTTP Output')
    [void]$Builder.AppendLine(('-' * 100))
    [void]$Builder.AppendLine($HttpResult.StdOut)
    if (-not [string]::IsNullOrWhiteSpace($HttpResult.StdErr)) {
        [void]$Builder.AppendLine()
        [void]$Builder.AppendLine('HTTP stderr')
        [void]$Builder.AppendLine(('-' * 100))
        [void]$Builder.AppendLine($HttpResult.StdErr)
    }

    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine('Playwright Output')
    [void]$Builder.AppendLine(('-' * 100))
    [void]$Builder.AppendLine($PlaywrightResult.StdOut)
    if (-not [string]::IsNullOrWhiteSpace($PlaywrightResult.StdErr)) {
        [void]$Builder.AppendLine()
        [void]$Builder.AppendLine('Playwright stderr')
        [void]$Builder.AppendLine(('-' * 100))
        [void]$Builder.AppendLine($PlaywrightResult.StdErr)
    }

    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine('Short Comparison')
    [void]$Builder.AppendLine(('-' * 100))
    [void]$Builder.AppendLine('HTTP excerpt:')
    [void]$Builder.AppendLine((Get-Excerpt -Text $HttpResult.StdOut))
    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine('Playwright excerpt:')
    [void]$Builder.AppendLine((Get-Excerpt -Text $PlaywrightResult.StdOut))
    [void]$Builder.AppendLine()
}

$repoRoot = Get-RepoRoot
$resolvedOutputPath = Get-OutputPath -RepoRoot $repoRoot -RequestedPath $OutputPath
$command = Get-CliCommand -RepoRoot $repoRoot -PreferBuiltCli:$UseBuiltCli

$builder = [System.Text.StringBuilder]::new()
[void]$builder.AppendLine('web-explorer fetch comparison')
[void]$builder.AppendLine("GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
[void]$builder.AppendLine("RepoRoot: $repoRoot")
[void]$builder.AppendLine("CommandMode: $($command.Mode)")
[void]$builder.AppendLine("TimeoutMs: $TimeoutMs")
[void]$builder.AppendLine("URLs: $($Urls -join ', ')")
[void]$builder.AppendLine()

foreach ($url in $Urls) {
    Write-Host "Comparing fetchers for $url ..."
    $httpResult = Invoke-WebExplorerFetch -Command $command -Url $url -Renderer 'http' -TimeoutMs $TimeoutMs
    $playwrightResult = Invoke-WebExplorerFetch -Command $command -Url $url -Renderer 'playwright' -TimeoutMs $TimeoutMs
    Add-ResultBlock -Builder $builder -Url $url -HttpResult $httpResult -PlaywrightResult $playwrightResult
}

$outputDirectory = Split-Path -Path $resolvedOutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

[System.IO.File]::WriteAllText($resolvedOutputPath, $builder.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "Wrote comparison report to $resolvedOutputPath"
