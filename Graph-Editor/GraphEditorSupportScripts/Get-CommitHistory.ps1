# This script provides functions to retrieve commit history and merged pull requests from a GitHub repository,
# targeting changes to Microsoft Graph API reference documentation files (api-reference/v1.0/api/).
# It resolves the table-of-contents (TOC) location of each changed file from the microsoft-graph-docs-contrib repository.
#
# Prerequisites:
#   - A GitHub personal access token with "repo:status" and "public_repo" scopes.
#     SSO authorization for the MicrosoftDocs organization is required.
#     Obtain from: https://github.com/settings/tokens
#   - The "powershell-yaml" module (Install-Module -Name powershell-yaml -Scope CurrentUser)
#   - Call Connect-GitHubRepository before calling Get-CommitHistory or Get-CommitHistoryV2.
#
# Functions:
#   Connect-GitHubRepository  - Stores the target GitHub organization, repository, and access token in global variables.
#   Get-CommitHistory         - Retrieves commits pushed on the specified date and lists changed api-reference/v1.0/api/ files.
#                               Filters out noise caused by bulk-merged old commits using a date-tolerance check.
#   Get-CommitHistoryV2       - Retrieves pull requests merged on the specified date and lists changed api-reference/v1.0/api/ files.
#                               Recommended over Get-CommitHistory because it avoids bulk-merge noise by design.
#   Get-YamlContent           - Fetches and parses a YAML file from a URL, with in-memory caching.
#   Search-FilenameInYaml     - Searches the Microsoft Graph docs TOC YAML tree for the given filename and returns its location path(s).
#   Test-FileHasRecentCommits - Returns $true if any commit for a file has an author date within the tolerance window of the target date.
#   Invoke-GitHubApi          - Calls the GitHub REST API with Bearer authentication and automatic exponential-backoff retry on rate-limit errors.
#
# Usage:
#   . .\Get-CommitHistory.ps1
#   Connect-GitHubRepository -Organization "microsoftgraph" -Repository "microsoft-graph-docs-contrib" -AccessToken "<token>"
#   Get-CommitHistoryV2 -Date (Get-Date "2024-01-15")   # Recommended: PR-based
#   Get-CommitHistory   -Date (Get-Date "2024-01-15")   # Alternative: commit-based

# Define a global variable to store the cache
$global:YamlCache = @{} 

# Define a global variable to store the access token
$global:AccessTokenCache = ""

# Define global variables to store repository information
$global:GitHubOrganization = ""
$global:GitHubRepository = ""

# Define a global variable to store file last commit date cache
$global:FileLastCommitCache = @{}

# Helper function to invoke GitHub API with rate limit handling
function Invoke-GitHubApi {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [string]$Method = "Get"
    )

    $maxRetries = 3
    $retryCount = 0

    while ($retryCount -lt $maxRetries) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -Method $Method -Headers @{
                "User-Agent"    = "PowerShell"
                "Authorization" = "Bearer $global:AccessTokenCache"
            }

            # Check rate limit from response headers (get first element if array)
            $rateLimit = if ($response.Headers['X-RateLimit-Limit'] -is [array]) { 
                $response.Headers['X-RateLimit-Limit'][0] 
            }
            else { 
                $response.Headers['X-RateLimit-Limit'] 
            }
            $rateRemaining = if ($response.Headers['X-RateLimit-Remaining'] -is [array]) { 
                $response.Headers['X-RateLimit-Remaining'][0] 
            }
            else { 
                $response.Headers['X-RateLimit-Remaining'] 
            }
            $rateReset = if ($response.Headers['X-RateLimit-Reset'] -is [array]) { 
                $response.Headers['X-RateLimit-Reset'][0] 
            }
            else { 
                $response.Headers['X-RateLimit-Reset'] 
            }

            # Warn if rate limit is low
            if ($rateRemaining -and [int]$rateRemaining -lt 100) {
                $resetTime = [DateTimeOffset]::FromUnixTimeSeconds([long]$rateReset).ToLocalTime()
                Write-Warning "GitHub API rate limit low: $rateRemaining/$rateLimit remaining (resets at $resetTime)"
            }

            # Return parsed content
            return ($response.Content | ConvertFrom-Json)
        }
        catch {
            $statusCode = $_.Exception.Response.StatusCode.value__

            # Handle rate limit exceeded (403 or 429)
            if ($statusCode -eq 403 -or $statusCode -eq 429) {
                $retryCount++
                
                if ($retryCount -lt $maxRetries) {
                    $waitSeconds = [Math]::Pow(2, $retryCount) * 30  # Exponential backoff: 60, 120, 240 seconds
                    Write-Warning "Rate limit exceeded. Waiting $waitSeconds seconds before retry ($retryCount/$maxRetries)..."
                    Start-Sleep -Seconds $waitSeconds
                }
                else {
                    $errorMsg = "Rate limit exceeded. Max retries reached. Please try again later."
                    Write-Error $errorMsg
                    
                    # Log rate limit error
                    try {
                        $now = Get-Date
                        $timezone = [System.TimeZoneInfo]::Local
                        $timezoneOffset = $timezone.GetUtcOffset($now).ToString("hh\:mm")
                        $timezoneSign = if ($timezone.GetUtcOffset($now).TotalMinutes -ge 0) { "+" } else { "-" }
                        $timestampWithTz = $now.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC$timezoneSign$timezoneOffset)"
                        $logPath = [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Graph Editor', 'GraphEditorSupportScripts.log')
                        $logDir = Split-Path -Path $logPath -Parent
                        if (-not (Test-Path $logDir)) {
                            New-Item -Path $logDir -ItemType Directory -Force | Out-Null
                        }
                        $logEntry = "[$timestampWithTz] Get-CommitHistory - Error: $errorMsg`n"
                        Add-Content -Path $logPath -Value $logEntry -Encoding UTF8
                    }
                    catch {
                        # Silently ignore log write errors during error handling
                    }
                    
                    throw
                }
            }
            else {
                Write-Error "API request failed: $_"
                throw
            }
        }
    }
}

# Connect to a GitHub repository and store the connection information
function Connect-GitHubRepository {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Organization,
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$AccessToken
    )

    # Store the connection information in global variables
    $global:GitHubOrganization = $Organization
    $global:GitHubRepository = $Repository
    $global:AccessTokenCache = $AccessToken

    Write-Host "Connected to GitHub repository: $Organization/$Repository" -ForegroundColor Green
}

# Helper function to check if a file has recent commits (not old commits from bulk merges)
function Test-FileHasRecentCommits {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [hashtable]$FileInfo,
        [Parameter(Mandatory = $true)]
        [DateTime]$TargetDate,
        [int]$ToleranceDays = 7
    )

    # Check if any commit's author date is within tolerance days of the target date
    # This filters out old commits that were bulk-merged from forks
    $targetDateUtc = $TargetDate.ToUniversalTime()
    $minDate = $targetDateUtc.AddDays(-$ToleranceDays)
    $maxDate = $targetDateUtc.AddDays($ToleranceDays)

    foreach ($commit in $FileInfo.Commits) {
        $commitDateUtc = $commit.Date.ToUniversalTime()
        if ($commitDateUtc -ge $minDate -and $commitDateUtc -le $maxDate) {
            # Found at least one commit within tolerance period
            return $true
        }
    }

    # All commits are too old (likely from bulk merge), filter out as noise
    return $false
}

# Retrieve the commit history of a specified GitHub organization repository with authentication.
# Filter the commits to include only those from a specified date.
# For the filtered commits, get a list of files whose names start with "api-reference/v1.0/api/".
# Finally, output the commits and the corresponding list of files.
# Access token can be obtained from https://github.com/settings/tokens with "repo:status" and "public_repo" scopes, and SSO to MicrosoftDocs is required.
function Get-CommitHistory {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [DateTime]$Date,
        [string]$LogPath = [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Graph Editor', 'GraphEditorSupportScripts.log')
    )

    # Check if the repository connection is established
    if ([string]::IsNullOrEmpty($global:GitHubOrganization) -or 
        [string]::IsNullOrEmpty($global:GitHubRepository) -or 
        [string]::IsNullOrEmpty($global:AccessTokenCache)) {
        Write-Error "GitHub repository connection is not established. Please run Connect-GitHubRepository first."
        return
    }

    $Organization = $global:GitHubOrganization
    $Repository = $global:GitHubRepository

    # Get current time with timezone for logging
    $now = Get-Date
    $timezone = [System.TimeZoneInfo]::Local
    $timezoneOffset = $timezone.GetUtcOffset($now).ToString("hh\:mm")
    $timezoneSign = if ($timezone.GetUtcOffset($now).TotalMinutes -ge 0) { "+" } else { "-" }
    $timestampWithTz = $now.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC$timezoneSign$timezoneOffset)"

    # Convert JST to UTC
    $utcStart = ($Date.Date).ToUniversalTime()
    $utcEnd = ($Date.Date.AddDays(1).AddSeconds(-1)).ToUniversalTime()
    
    # Retrieve commit history from the GitHub API with authentication (with pagination)
    Write-Host "Fetching commits..." -ForegroundColor Cyan
    $commits = @()
    $page = 1
    $perPage = 100  # Maximum allowed by GitHub API
    
    do {
        $url = "https://api.github.com/repos/$Organization/$Repository/commits?since=$($utcStart.ToString('yyyy-MM-ddTHH:mm:ssZ'))&until=$($utcEnd.ToString('yyyy-MM-ddTHH:mm:ssZ'))&sha=main&per_page=$perPage&page=$page"
        Write-Host "  Fetching page $page..." -ForegroundColor DarkGray
        $response = Invoke-GitHubApi -Uri $url -Method Get
        
        if ($response -and $response.Count -gt 0) {
            $commits += $response
            $page++
        }
        else {
            break
        }
        
        # If we got fewer results than per_page, we've reached the end
        if ($response.Count -lt $perPage) {
            break
        }
    } while ($true)
    
    Write-Host "Fetched $($commits.Count) commits in total" -ForegroundColor Cyan

    # Create a hashtable to store files and their commits
    $fileCommitMap = @{}

    # Get the list of files included in each commit
    Write-Host "Processing $($commits.Count) commits..." -ForegroundColor Cyan
    $commitIndex = 0
    foreach ($commit in $commits) {
        $commitIndex++
        $commitSha = $commit.sha
        $commitMessage = $commit.commit.message -split "`n" | Select-Object -First 1
        $commitDateUtc = $commit.commit.author.date
        $commitDateLocal = [DateTime]::Parse($commitDateUtc).ToLocalTime()
        $commitUrl = "https://github.com/$Organization/$Repository/commit/$commitSha"
        Write-Progress -Activity "Processing commits" -Status "Commit $commitIndex of $($commits.Count)" -PercentComplete (($commitIndex / $commits.Count) * 100)
        $response = Invoke-GitHubApi -Uri "https://api.github.com/repos/$Organization/$Repository/commits/$commitSha" -Method Get

        # Filter files whose names start with "api-reference/v1.0/api/"
        $filteredFiles = $response.files | Where-Object { $_.filename -like "api-reference/v1.0/api/*" }

        # Group files by filename
        foreach ($file in $filteredFiles) {
            if (-not $fileCommitMap.ContainsKey($file.filename)) {
                $fileCommitMap[$file.filename] = @{
                    Status  = $file.status
                    Commits = @()
                }
            }
            $fileCommitMap[$file.filename].Commits += @{
                Sha     = $commitSha
                Message = $commitMessage
                Date    = $commitDateLocal
                Url     = $commitUrl
            }
        }
    }

    # Complete progress bar
    Write-Progress -Activity "Processing commits" -Completed

    # Filter files: only include files with commits whose author date is recent (within tolerance)
    # This filters out old commits that were bulk-merged from forks
    Write-Host "Filtering files by commit recency (excluding old bulk-merged commits)..." -ForegroundColor Cyan
    $filteredFileCommitMap = @{}
    $skippedFilesCount = 0
    $fileIndex = 0
    $totalFiles = $fileCommitMap.Count
    $toleranceDays = 7  # Only include files with commits within 7 days of target date

    foreach ($filename in $fileCommitMap.Keys) {
        $fileIndex++
        Write-Progress -Activity "Filtering files" -Status "File $fileIndex of $totalFiles" -PercentComplete (($fileIndex / $totalFiles) * 100)
        
        # Check if this file has any commits with recent author dates
        $hasRecentCommits = Test-FileHasRecentCommits -FileInfo $fileCommitMap[$filename] -TargetDate $Date -ToleranceDays $toleranceDays
        
        if ($hasRecentCommits) {
            # File has recent commits, include it
            $filteredFileCommitMap[$filename] = $fileCommitMap[$filename]
        }
        else {
            # All commits are too old (likely from bulk merge), skip it (noise)
            $skippedFilesCount++
        }
    }

    # Complete progress bar
    Write-Progress -Activity "Filtering files" -Completed

    if ($skippedFilesCount -gt 0) {
        Write-Host "Filtered out $skippedFilesCount file(s) with only old commits (bulk merge noise)" -ForegroundColor Yellow
    }

    # Output the results grouped by file
    if ($filteredFileCommitMap.Count -gt 0) {
        Write-Host ""
        Write-Host "Summary of changes on $($Date.ToString('yyyy-MM-dd')): $($filteredFileCommitMap.Count) files" -ForegroundColor Green
        Write-Host ""

        # Write log entry
        try {
            $logDir = Split-Path -Path $LogPath -Parent
            if (-not (Test-Path $logDir)) {
                New-Item -Path $logDir -ItemType Directory -Force | Out-Null
            }
            $logEntry = "[$timestampWithTz] Get-CommitHistory - Date: $($Date.ToString('yyyy-MM-dd')), Files: $($filteredFileCommitMap.Count) (filtered from $($fileCommitMap.Count)), Commits: $($commits.Count), Skipped: $skippedFilesCount`n"
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        catch {
            Write-Warning "Failed to write log: $_"
        }

        foreach ($filename in $filteredFileCommitMap.Keys | Sort-Object) {
            $fileInfo = $filteredFileCommitMap[$filename]
            Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
            Write-Host "File: $filename" -ForegroundColor Magenta
            
            # Generate GitHub diff URL to show changes for this file on the specified date
            # The commits are in reverse chronological order (newest first)
            $latestCommit = $fileInfo.Commits[0].Sha
            $oldestCommit = $fileInfo.Commits[-1].Sha
            
            if ($fileInfo.Commits.Count -eq 1) {
                # Single commit: show the commit diff
                $diffUrl = "https://github.com/$Organization/$Repository/commit/$latestCommit#diff-" + [System.BitConverter]::ToString([System.Text.Encoding]::UTF8.GetBytes($filename)).Replace("-", "").ToLower()
            }
            else {
                # Multiple commits: show compare view from oldest to latest
                $diffUrl = "https://github.com/$Organization/$Repository/compare/$oldestCommit^..$latestCommit#diff-" + [System.BitConverter]::ToString([System.Text.Encoding]::UTF8.GetBytes($filename)).Replace("-", "").ToLower()
            }
            Write-Host "Diff: $diffUrl" -ForegroundColor Cyan
            
            # Search the file name in YAML structure (inline output)
            $yamlLocations = Search-FilenameInYaml -filename:($filename.Split("/") | Select-Object -Last 1) -returnOnly
            if ($yamlLocations -and $yamlLocations.Count -gt 0) {
                $isFirst = $true
                foreach ($location in $yamlLocations) {
                    if ($isFirst) {
                        Write-Host "TOC:  $location" -ForegroundColor Green
                        $isFirst = $false
                    }
                    else {
                        Write-Host "      $location" -ForegroundColor Green
                    }
                }
            }
            
            # Show commits count and URLs only
            if ($fileInfo.Commits.Count -eq 1) {
                Write-Host "Commit: $($fileInfo.Commits[0].Url)" -ForegroundColor DarkGray
            }
            else {
                Write-Host "Commits ($($fileInfo.Commits.Count)): $($fileInfo.Commits[0].Url)" -ForegroundColor DarkGray
                for ($i = 1; $i -lt $fileInfo.Commits.Count; $i++) {
                    Write-Host "           $($fileInfo.Commits[$i].Url)" -ForegroundColor DarkGray
                }
            }
            Write-Host ""
        }
    }
    else {
        Write-Host "No matching files found." -ForegroundColor Red

        # Write log entry
        try {
            $logDir = Split-Path -Path $LogPath -Parent
            if (-not (Test-Path $logDir)) {
                New-Item -Path $logDir -ItemType Directory -Force | Out-Null
            }
            $logEntry = "[$timestampWithTz] Get-CommitHistory - Date: $($Date.ToString('yyyy-MM-dd')), No changes`n"
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        catch {
            Write-Warning "Failed to write log: $_"
        }
    }
}

# Retrieve merged pull requests from a specified date and list changed files.
# This version filters by PR merge date, avoiding noise from bulk-merged old commits.
# For merged PRs, get a list of files whose names start with "api-reference/v1.0/api/".
# Access token can be obtained from https://github.com/settings/tokens with "repo:status" and "public_repo" scopes, and SSO to MicrosoftDocs is required.
function Get-CommitHistoryV2 {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [DateTime]$Date,
        [string]$LogPath = [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Graph Editor', 'GraphEditorSupportScripts.log')
    )

    # Check if the repository connection is established
    if ([string]::IsNullOrEmpty($global:GitHubOrganization) -or 
        [string]::IsNullOrEmpty($global:GitHubRepository) -or 
        [string]::IsNullOrEmpty($global:AccessTokenCache)) {
        Write-Error "GitHub repository connection is not established. Please run Connect-GitHubRepository first."
        return
    }

    $Organization = $global:GitHubOrganization
    $Repository = $global:GitHubRepository

    # Get current time with timezone for logging
    $now = Get-Date
    $timezone = [System.TimeZoneInfo]::Local
    $timezoneOffset = $timezone.GetUtcOffset($now).ToString("hh\:mm")
    $timezoneSign = if ($timezone.GetUtcOffset($now).TotalMinutes -ge 0) { "+" } else { "-" }
    $timestampWithTz = $now.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC$timezoneSign$timezoneOffset)"

    # Convert local date to UTC
    $utcStart = ($Date.Date).ToUniversalTime()
    $utcEnd = ($Date.Date.AddDays(1).AddSeconds(-1)).ToUniversalTime()
    
    # Retrieve merged pull requests from the GitHub API (with pagination)
    Write-Host "Fetching merged pull requests..." -ForegroundColor Cyan
    $allPRs = @()
    $page = 1
    $perPage = 100  # Maximum allowed by GitHub API
    
    # We need to search for PRs that were updated around the target date
    # GitHub doesn't have a direct "merged_at" filter, so we use updated and filter later
    # Get PRs updated within a wider range to ensure we don't miss any
    $searchStart = $utcStart.AddDays(-1)  # Include PRs updated 1 day before
    
    do {
        # Get closed PRs sorted by updated date
        $url = "https://api.github.com/repos/$Organization/$Repository/pulls?state=closed&sort=updated&direction=desc&per_page=$perPage&page=$page"
        Write-Host "  Fetching PR page $page..." -ForegroundColor DarkGray
        $response = Invoke-GitHubApi -Uri $url -Method Get
        
        if ($response -and $response.Count -gt 0) {
            # Filter to only merged PRs
            $mergedPRs = $response | Where-Object { 
                $_.merged_at -and 
                [DateTime]::Parse($_.merged_at) -ge $utcStart -and 
                [DateTime]::Parse($_.merged_at) -le $utcEnd
            }
            
            if ($mergedPRs) {
                $allPRs += $mergedPRs
            }
            
            # Check if the last PR in this page was updated before our search start
            # If so, we can stop searching
            $lastUpdated = [DateTime]::Parse($response[-1].updated_at)
            if ($lastUpdated -lt $searchStart) {
                Write-Host "  Reached PRs older than search range, stopping..." -ForegroundColor DarkGray
                break
            }
            
            $page++
            
            # If we got fewer results than per_page, we've reached the end
            if ($response.Count -lt $perPage) {
                break
            }
        }
        else {
            break
        }
    } while ($true)
    
    Write-Host "Found $($allPRs.Count) merged PR(s) on $($Date.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan

    if ($allPRs.Count -eq 0) {
        Write-Host "No merged pull requests found on $($Date.ToString('yyyy-MM-dd'))" -ForegroundColor Red
        
        # Write log entry
        try {
            $logDir = Split-Path -Path $LogPath -Parent
            if (-not (Test-Path $logDir)) {
                New-Item -Path $logDir -ItemType Directory -Force | Out-Null
            }
            $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Date: $($Date.ToString('yyyy-MM-dd')), No merged PRs`n"
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        catch {
            Write-Warning "Failed to write log: $_"
        }
        return
    }

    # Create a hashtable to store files and their PRs
    $fileCommitMap = @{}

    # Get the list of files changed in each PR
    Write-Host "Processing $($allPRs.Count) pull request(s)..." -ForegroundColor Cyan
    $prIndex = 0
    foreach ($pr in $allPRs) {
        $prIndex++
        $prNumber = $pr.number
        $prTitle = $pr.title
        $prMergedAt = [DateTime]::Parse($pr.merged_at).ToLocalTime()
        $prUrl = $pr.html_url
        
        Write-Progress -Activity "Processing pull requests" -Status "PR #$prNumber ($prIndex of $($allPRs.Count))" -PercentComplete (($prIndex / $allPRs.Count) * 100)
        
        # Get files changed in this PR (with pagination)
        $prFiles = @()
        $filePage = 1
        $filePerPage = 100
        
        do {
            $filesUrl = "https://api.github.com/repos/$Organization/$Repository/pulls/$prNumber/files?per_page=$filePerPage&page=$filePage"
            $filesResponse = Invoke-GitHubApi -Uri $filesUrl -Method Get
            
            if ($filesResponse -and $filesResponse.Count -gt 0) {
                $prFiles += $filesResponse
                $filePage++
                
                if ($filesResponse.Count -lt $filePerPage) {
                    break
                }
            }
            else {
                break
            }
        } while ($true)

        # Filter files whose names start with "api-reference/v1.0/api/"
        $filteredFiles = $prFiles | Where-Object { $_.filename -like "api-reference/v1.0/api/*" }

        # Group files by filename
        foreach ($file in $filteredFiles) {
            if (-not $fileCommitMap.ContainsKey($file.filename)) {
                $fileCommitMap[$file.filename] = @{
                    Status = $file.status
                    PRs    = @()
                }
            }
            $fileCommitMap[$file.filename].PRs += @{
                Number   = $prNumber
                Title    = $prTitle
                MergedAt = $prMergedAt
                Url      = $prUrl
            }
        }
    }

    # Complete progress bar
    Write-Progress -Activity "Processing pull requests" -Completed

    # Output the results grouped by file
    if ($fileCommitMap.Count -gt 0) {
        Write-Host ""
        Write-Host "Summary of changes on $($Date.ToString('yyyy-MM-dd')): $($fileCommitMap.Count) files in $($allPRs.Count) PR(s)" -ForegroundColor Green
        Write-Host ""

        # Write log entry
        try {
            $logDir = Split-Path -Path $LogPath -Parent
            if (-not (Test-Path $logDir)) {
                New-Item -Path $logDir -ItemType Directory -Force | Out-Null
            }
            $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Date: $($Date.ToString('yyyy-MM-dd')), Files: $($fileCommitMap.Count), PRs: $($allPRs.Count)`n"
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        catch {
            Write-Warning "Failed to write log: $_"
        }

        foreach ($filename in $fileCommitMap.Keys | Sort-Object) {
            $fileInfo = $fileCommitMap[$filename]
            Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
            Write-Host "File: $filename" -ForegroundColor Magenta
            
            # Generate GitHub URL to view the file
            $fileUrl = "https://github.com/$Organization/$Repository/blob/main/$filename"
            Write-Host "View: $fileUrl" -ForegroundColor Cyan
            
            # Search the file name in YAML structure (inline output)
            $yamlLocations = Search-FilenameInYaml -filename:($filename.Split("/") | Select-Object -Last 1) -returnOnly
            if ($yamlLocations -and $yamlLocations.Count -gt 0) {
                $isFirst = $true
                foreach ($location in $yamlLocations) {
                    if ($isFirst) {
                        Write-Host "TOC:  $location" -ForegroundColor Green
                        $isFirst = $false
                    }
                    else {
                        Write-Host "      $location" -ForegroundColor Green
                    }
                }
            }
            
            # Generate file hash for diff anchor
            $fileHash = [System.BitConverter]::ToString([System.Text.Encoding]::UTF8.GetBytes($filename)).Replace("-", "").ToLower()
            
            # Show PR count and URLs with file-specific diff links
            if ($fileInfo.PRs.Count -eq 1) {
                $pr = $fileInfo.PRs[0]
                $prDiffUrl = "https://github.com/$Organization/$Repository/pull/$($pr.Number)/files#diff-$fileHash"
                Write-Host "PR:    #$($pr.Number) - $($pr.Title)" -ForegroundColor DarkGray
                Write-Host "       $prDiffUrl" -ForegroundColor DarkGray
            }
            else {
                Write-Host "PRs ($($fileInfo.PRs.Count)):" -ForegroundColor DarkGray
                foreach ($pr in $fileInfo.PRs) {
                    $prDiffUrl = "https://github.com/$Organization/$Repository/pull/$($pr.Number)/files#diff-$fileHash"
                    Write-Host "       #$($pr.Number) - $($pr.Title)" -ForegroundColor DarkGray
                    Write-Host "       $prDiffUrl" -ForegroundColor DarkGray
                }
            }
            Write-Host ""
        }
    }
    else {
        Write-Host "No matching files found in merged pull requests." -ForegroundColor Red

        # Write log entry
        try {
            $logDir = Split-Path -Path $LogPath -Parent
            if (-not (Test-Path $logDir)) {
                New-Item -Path $logDir -ItemType Directory -Force | Out-Null
            }
            $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Date: $($Date.ToString('yyyy-MM-dd')), No matching files`n"
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        catch {
            Write-Warning "Failed to write log: $_"
        }
    }
}

# Function to fetch and parse the YAML file from the provided URL
function Get-YamlContent {
    param (
        [string]$url
    )

    # Check if the URL is already cached
    if ($global:YamlCache.ContainsKey($url)) {
        return $global:YamlCache[$url]
    }

    # Check if the access token is set
    if ([string]::IsNullOrEmpty($global:AccessTokenCache)) {
        Write-Error "Access token is not set. Please run Get-CommitHistory first."
        return
    }

    try {
        # Fetch the YAML content from the URL with authentication (raw text, not JSON)
        $response = Invoke-WebRequest -Uri $url -Method Get -Headers @{
            "User-Agent"    = "PowerShell"
            "Authorization" = "Bearer $global:AccessTokenCache"
        }
        $yamlContent = $response.Content

        # Parse the YAML content
        $parsedYaml = $yamlContent | ConvertFrom-Yaml

        # Store the parsed YAML content in the cache
        $global:YamlCache[$url] = $parsedYaml

        return $parsedYaml
    }
    catch {
        Write-Host "Failed to fetch YAML content from URL: $url" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
        return $null
    }
}

# Function to search for a given filename in the parsed YAML data
function Search-FilenameInYaml {
    param (
        [string]$filename,
        [string]$mainYamlUrl = "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-docs-contrib/refs/heads/main/api-reference/v1.0/toc.yml",
        [switch]$returnOnly
    )

    # Check if the access token is set
    if ([string]::IsNullOrEmpty($global:AccessTokenCache)) {
        Write-Error "Access token is not set. Please run Get-CommitHistory first."
        return
    }

    # Get the parsed main YAML content
    $mainYamlData = Get-YamlContent -url $mainYamlUrl

    if (-not $mainYamlData) {
        if (-not $returnOnly) {
            Write-Host "Failed to retrieve or parse the main YAML file." -ForegroundColor Red
        }
        return
    }

    # Find the "API v1.0 reference" node
    $apiReferenceNode = $mainYamlData.items | Where-Object { $_.name -eq "API v1.0 reference" }

    if (-not $apiReferenceNode) {
        if (-not $returnOnly) {
            Write-Host "The 'API v1.0 reference' node is not found in the main YAML data." -ForegroundColor Red
        }
        return
    }

    # Initialize a String List to store the locations
    $locations = New-Object System.Collections.Generic.List[System.String]

    # Function to search for the filename in a single YAML file
    function Search-InYamlFile {
        param (
            [string]$yamlUrl,
            [string]$filename,
            [string]$topParentName,
            [System.Collections.Generic.List[System.String]]$locations
        )

        # Get the parsed YAML content
        $yamlData = Get-YamlContent -url $yamlUrl

        if (-not $yamlData) {
            if (-not $returnOnly) {
                Write-Host "Failed to retrieve or parse the YAML file: $yamlUrl" -ForegroundColor Red
            }
            return
        }

        # Recursive function to search for the filename in the YAML data
        function Search-Recursive {
            param (
                [array]$nodes,
                [string]$path = ""
            )

            foreach ($node in $nodes) {
                if (!([string]::IsNullOrEmpty($node.href)) -and $node.href.EndsWith($filename)) {
                    # File is found. Add the location to the list.
                    $locations.Add($topParentName + $path + "/" + $node.name)
                }

                if ($node.items) {
                    Search-Recursive -nodes $node.items -path ($path + "/" + $node.name)
                }
            }
        }

        # Start the recursive search
        Search-Recursive -nodes $yamlData.items
    }

    # Iterate through each YAML file under the "API v1.0 reference" node
    foreach ($item in $apiReferenceNode.items) {
        $yamlUrl = "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-docs-contrib/refs/heads/main/api-reference/v1.0/$($item.href)"
        if ($yamlUrl.EndsWith("/toc.yml")) {
            Search-InYamlFile -yamlUrl $yamlUrl -filename $filename -topParentName $item.name -locations $locations
        }
        else {
            # If the item is not a toc.yml file, skip it
        }
    }

    # Output the locations or return them
    if ($returnOnly) {
        return $locations
    }
    else {
        if ($locations.Count -gt 0) {
            Write-Host "    The file '$filename' is found at the following locations:" -ForegroundColor Green
            $locations | ForEach-Object { Write-Host "      $_" -ForegroundColor White }
        }
        else {
            Write-Host "    The file '$filename' is not found in the YAML data." -ForegroundColor Red
        }
    }
}
