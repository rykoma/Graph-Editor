# This script provides functions to retrieve commit history and merged pull requests from GitHub repositories,
# targeting changes to Microsoft Graph API reference documentation files (api-reference/v1.0/api/).
# It resolves the table-of-contents (TOC) location of each changed file from the microsoft-graph-docs-contrib repository.
#
# Prerequisites:
#   - A GitHub personal access token with "repo" scopes.
#     SSO authorization for the MicrosoftGraph organization is required.
#     Obtain from: https://github.com/settings/tokens
#   - The "powershell-yaml" module (Install-Module -Name powershell-yaml -Scope CurrentUser)
#   - Call Connect-GitHubRepository before calling Get-CommitHistoryV2.
#
# Functions:
#   Connect-GitHubRepository  - Stores the target GitHub organization, repository, and access token in global variables.
#   Get-CommitHistoryV2       - Retrieves pull requests merged on the specified date and lists changed api-reference/v1.0/api/ files.
#                               Pull request-based processing avoids bulk-merge noise by design.
#   Get-YamlContent           - Fetches and parses a YAML file from a URL, with in-memory caching.
#   Search-FilenameInYaml     - Searches the Microsoft Graph docs TOC YAML tree for the given filename and returns its location path(s).
#   Test-FileHasRecentCommits - Returns $true if any commit for a file has an author date within the tolerance window of the target date.
#   Invoke-GitHubApi          - Calls the GitHub REST API with Bearer authentication and automatic exponential-backoff retry on rate-limit errors.
#
# Usage:
#   . .\MicrosoftGraphDocTools.ps1
#   Connect-GitHubRepository -AccessToken "<token>"
#   Get-CommitHistoryV2 -Date (Get-Date "2024-01-15")   # Recommended: PR-based

# Install required module if not available
if (-not (Get-Module -ListAvailable -Name powershell-yaml)) {
    Install-Module -Name powershell-yaml -Force -Scope CurrentUser
}
Import-Module powershell-yaml

# Define a global variable to store the cache
$global:YamlCache = @{} 

# Define a global variable to store the access token
$global:AccessTokenCache = ""

# Define target repositories for commit/PR history processing
$script:GitHubOrganization = "microsoftgraph"
$script:TargetRepositories = @("microsoft-graph-docs-contrib", "microsoft-graph-docs")

# Define a global variable to store file last commit date cache
$global:FileLastCommitCache = @{}

# Base URL for Microsoft Graph docs-contrib TOC processing
$baseUrl = "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-docs-contrib/main/api-reference/v1.0/"

# Counters for progress display in TOC export
$script:requestCount = 0
$script:subRequestCount = 0
$script:totalHttpRequests = 0
$script:startTime = Get-Date

# Progress state for Test-MicrosoftGraphV1ReferenceChanges flow
$script:V1ProgressEnabled = $false
$script:V1ProgressParentId = 7100
$script:V1ProgressChildId = 7101
$script:V1TopTocTotal = 0
$script:V1TopTocIndex = 0
$script:V1TopTocName = ""
$script:V1InnerTotal = 0
$script:V1InnerIndex = 0

function Update-V1ReferenceProgress {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Status,
        [double]$PercentComplete = -1,
        [switch]$Child
    )

    if (-not $script:V1ProgressEnabled) {
        return
    }

    $safePercent = if ($PercentComplete -lt 0) {
        0
    }
    else {
        [Math]::Max(0, [Math]::Min(100, [int][Math]::Round($PercentComplete)))
    }

    if ($Child) {
        Write-Progress -Id $script:V1ProgressChildId -ParentId $script:V1ProgressParentId -Activity "Exporting API v1.0 hierarchy" -Status $Status -PercentComplete $safePercent
    }
    else {
        Write-Progress -Id $script:V1ProgressParentId -Activity "Testing Microsoft Graph v1.0 reference changes" -Status $Status -PercentComplete $safePercent
    }
}

function Complete-V1ReferenceProgress {
    if (-not $script:V1ProgressEnabled) {
        return
    }

    Write-Progress -Id $script:V1ProgressChildId -ParentId $script:V1ProgressParentId -Activity "Exporting API v1.0 hierarchy" -Completed
    Write-Progress -Id $script:V1ProgressParentId -Activity "Testing Microsoft Graph v1.0 reference changes" -Completed
}

function Get-V1ApiChildStatus {
    param (
        [string]$Detail = ""
    )

    if ($script:V1TopTocTotal -le 0 -or $script:V1TopTocIndex -le 0) {
        return $Detail
    }

    $topName = if ([string]::IsNullOrWhiteSpace($script:V1TopTocName)) { "(unknown)" } else { $script:V1TopTocName }

    if ($script:V1InnerTotal -gt 0 -and $script:V1InnerIndex -gt 0) {
        $status = "API TOC item $($script:V1TopTocIndex)/$($script:V1TopTocTotal), sub $($script:V1InnerIndex)/$($script:V1InnerTotal): $topName"
    }
    else {
        $status = "API TOC item $($script:V1TopTocIndex)/$($script:V1TopTocTotal): $topName"
    }

    if (-not [string]::IsNullOrWhiteSpace($Detail)) {
        $status += " ($Detail)"
    }

    return $status
}

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
                        $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Error: $errorMsg`n"
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

# Store the GitHub access token for all functions in this script.
function Connect-GitHubRepository {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$AccessToken
    )

    $global:AccessTokenCache = $AccessToken

    Write-Host "GitHub access token configured for: $($script:GitHubOrganization)/$($script:TargetRepositories -join ', ')" -ForegroundColor Green
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

# Retrieve merged pull requests from a specified date and list changed files.
# This version filters by PR merge date, avoiding noise from bulk-merged old commits.
# For merged PRs, get a list of files whose names start with "api-reference/v1.0/api/".
# Access token can be obtained from https://github.com/settings/tokens with "repo" scopes, and SSO to MicrosoftGraph is required.
function Get-CommitHistoryV2 {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [DateTime]$Date,
        [string]$LogPath = [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Graph Editor', 'GraphEditorSupportScripts.log')
    )

    # Check if access token is configured
    if ([string]::IsNullOrEmpty($global:AccessTokenCache)) {
        Write-Error "GitHub access token is not configured. Please run Connect-GitHubRepository first."
        return
    }

    $Organization = $script:GitHubOrganization

    # Get current time with timezone for logging
    $now = Get-Date
    $timezone = [System.TimeZoneInfo]::Local
    $timezoneOffset = $timezone.GetUtcOffset($now).ToString("hh\:mm")
    $timezoneSign = if ($timezone.GetUtcOffset($now).TotalMinutes -ge 0) { "+" } else { "-" }
    $timestampWithTz = $now.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC$timezoneSign$timezoneOffset)"

    # Convert local date to UTC
    $utcStart = ($Date.Date).ToUniversalTime()
    $utcEnd = ($Date.Date.AddDays(1).AddSeconds(-1)).ToUniversalTime()
    
    foreach ($Repository in $script:TargetRepositories) {
        Write-Host ""
        Write-Host "====================================================" -ForegroundColor DarkGray
        Write-Host "Repository: $Organization/$Repository" -ForegroundColor Cyan
        Write-Host "====================================================" -ForegroundColor DarkGray

        $tocMainYamlUrl = "https://raw.githubusercontent.com/$Organization/$Repository/refs/heads/main/api-reference/v1.0/toc.yml"

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
                $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Repo: $Organization/$Repository, Date: $($Date.ToString('yyyy-MM-dd')), No merged PRs`n"
                Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
            }
            catch {
                Write-Warning "Failed to write log: $_"
            }
            continue
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

            Write-Progress -Activity "Processing pull requests ($Repository)" -Status "PR #$prNumber ($prIndex of $($allPRs.Count))" -PercentComplete (($prIndex / $allPRs.Count) * 100)

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
        Write-Progress -Activity "Processing pull requests ($Repository)" -Completed

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
                $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Repo: $Organization/$Repository, Date: $($Date.ToString('yyyy-MM-dd')), Files: $($fileCommitMap.Count), PRs: $($allPRs.Count)`n"
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
                $yamlLocations = Search-FilenameInYaml -filename:($filename.Split("/") | Select-Object -Last 1) -mainYamlUrl $tocMainYamlUrl -returnOnly
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
                $logEntry = "[$timestampWithTz] Get-CommitHistoryV2 - Repo: $Organization/$Repository, Date: $($Date.ToString('yyyy-MM-dd')), No matching files`n"
                Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
            }
            catch {
                Write-Warning "Failed to write log: $_"
            }
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
        Write-Error "Access token is not set. Please run Connect-GitHubRepository first."
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
        Write-Error "Access token is not set. Please run Connect-GitHubRepository first."
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
function Invoke-RestMethodWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [int]$MaxRetries = 3,
        [int]$RetryDelaySeconds = 2,
        [int]$PoliteDelayMilliseconds = 100,
        [ValidateSet('Primary', 'Secondary')]
        [string]$RequestType = 'Primary'
    )
    
    for ($i = 0; $i -lt $MaxRetries; $i++) {
        try {
            $headers = @{ "User-Agent" = "PowerShell" }
            if (-not [string]::IsNullOrEmpty($global:AccessTokenCache)) {
                $headers["Authorization"] = "Bearer $global:AccessTokenCache"
            }
            $result = Invoke-RestMethod -Uri $Uri -Headers $headers -ErrorAction Stop
            
            # Display progress (only on first success)
            if ($i -eq 0) {
                $script:totalHttpRequests++  # Count total number of requests
                
                if ($RequestType -eq 'Primary') {
                    # Primary request (YAML files, etc.)
                    $script:requestCount++
                    $script:subRequestCount = 0
                    Write-Verbose "[$script:requestCount] $Uri"
                }

                if ($script:V1TopTocTotal -gt 0 -and $script:V1TopTocIndex -gt 0) {
                    $currentTopPercent = ($script:V1TopTocIndex / $script:V1TopTocTotal) * 100
                    $status = Get-V1ApiChildStatus -Detail "HTTP $($script:totalHttpRequests)"
                    Update-V1ReferenceProgress -Child -Status $status -PercentComplete $currentTopPercent
                }
                # No progress display for secondary requests (Markdown files, etc.)
                # Display control is handled by the caller
            }
            
            # Polite delay on success (not needed on retry)
            if ($PoliteDelayMilliseconds -gt 0 -and $i -eq 0) {
                Start-Sleep -Milliseconds $PoliteDelayMilliseconds
            }
            
            return $result
        }
        catch {
            $isLastRetry = ($i -eq ($MaxRetries - 1))
            
            # Get status code (if exists)
            $statusCode = $null
            if ($_.Exception.Response) {
                $statusCode = $_.Exception.Response.StatusCode.value__
            }
            
            if ($isLastRetry) {
                Write-Warning "Maximum retry attempts reached: $Uri"
                throw
            }
            
            # Wait longer for 429 (Too Many Requests)
            $delay = if ($statusCode -eq 429) { 
                $RetryDelaySeconds * 3 
            }
            else { 
                $RetryDelaySeconds * [Math]::Pow(2, $i)  # Exponential backoff
            }
            
            $statusInfo = if ($statusCode) { "(HTTP $statusCode)" } else { "" }
            if ($script:V1TopTocTotal -gt 0 -and $script:V1TopTocIndex -gt 0) {
                $currentTopPercent = ($script:V1TopTocIndex / $script:V1TopTocTotal) * 100
                $status = Get-V1ApiChildStatus -Detail "waiting ${delay}s"
                Update-V1ReferenceProgress -Child -Status $status -PercentComplete $currentTopPercent
            }
            Write-Warning "Retry $($i + 1)/$MaxRetries $statusInfo : $Uri - Waiting ${delay} seconds"
            Start-Sleep -Seconds $delay
        }
    }
}

function Get-DeterministicGuid {
    param (
        [Parameter(Mandatory = $true)]
        [string]$InputString,
        
        [string]$Namespace = "00000000-0000-0000-0000-000000000000"
    )
    
    # Convert namespace GUID to byte array
    $namespaceGuid = [System.Guid]::Parse($Namespace)
    $namespaceBytes = $namespaceGuid.ToByteArray()
    
    # Convert input string to UTF-8 bytes
    $stringBytes = [System.Text.Encoding]::UTF8.GetBytes($InputString)
    
    # Combine and calculate SHA1 hash
    $combinedBytes = $namespaceBytes + $stringBytes
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    $hashBytes = $sha1.ComputeHash($combinedBytes)
    
    # Use first 16 bytes of hash to construct GUID
    $guidBytes = New-Object byte[] 16
    [Array]::Copy($hashBytes, 0, $guidBytes, 0, 16)
    
    # Set as Version 5 (SHA-1 based) GUID according to RFC 4122
    $guidBytes[6] = ($guidBytes[6] -band 0x0F) -bor 0x50  # Version 5
    $guidBytes[8] = ($guidBytes[8] -band 0x3F) -bor 0x80  # Variant
    
    # Construct GUID string from byte array (in correct format)
    $guidString = "{0:x2}{1:x2}{2:x2}{3:x2}-{4:x2}{5:x2}-{6:x2}{7:x2}-{8:x2}{9:x2}-{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}" -f `
        $guidBytes[0], $guidBytes[1], $guidBytes[2], $guidBytes[3], `
        $guidBytes[4], $guidBytes[5], $guidBytes[6], $guidBytes[7], `
        $guidBytes[8], $guidBytes[9], $guidBytes[10], $guidBytes[11], `
        $guidBytes[12], $guidBytes[13], $guidBytes[14], $guidBytes[15]
    
    return [System.Guid]::Parse($guidString)
}

function ConvertFrom-TocItems {
    param (
        [array]$items,
        [string]$parentPath = "",
        [int]$ApiSectionDepth = -1
    )

    $isRootLevel = [string]::IsNullOrEmpty($parentPath)
    $isApiTopLevel = ($ApiSectionDepth -eq 0)
    $result = @()
    $totalItems = if ($items) { $items.Count } else { 0 }

    if ($isApiTopLevel) {
        $script:V1TopTocTotal = $totalItems
    }

    $itemIndex = 0
    foreach ($item in $items) {
        $itemIndex++
        $originalName = $item.name
        if ($item.name -eq "API v1.0 reference") {
            $item.name = "Custom query"
        }

        $nextApiSectionDepth = -1
        if ($ApiSectionDepth -ge 0) {
            $nextApiSectionDepth = $ApiSectionDepth + 1
        }
        elseif ($originalName -eq "API v1.0 reference" -or $item.name -eq "Custom query") {
            $nextApiSectionDepth = 0
        }
        
        $href = $item.href
        $children = @()

        # Build full path to current item
        $currentPath = if ($parentPath) { "$parentPath/$($item.name)" } else { $item.name }
        if ($isApiTopLevel) {
            $script:V1TopTocIndex = $itemIndex
            $script:V1TopTocName = $item.name
            $script:V1InnerTotal = 0
            $script:V1InnerIndex = 0
            $itemPercent = if ($totalItems -gt 0) { ($itemIndex / $totalItems) * 100 } else { 0 }
            Update-V1ReferenceProgress -Child -Status "API TOC item $itemIndex/${totalItems}: $($item.name)" -PercentComplete $itemPercent
        }
        elseif ($ApiSectionDepth -eq 1 -and $script:V1TopTocTotal -gt 0 -and $script:V1TopTocIndex -gt 0) {
            $script:V1InnerTotal = $totalItems
            $script:V1InnerIndex = $itemIndex
            $currentTopPercent = ($script:V1TopTocIndex / $script:V1TopTocTotal) * 100
            $status = Get-V1ApiChildStatus -Detail "processing $($item.name)"
            Update-V1ReferenceProgress -Child -Status $status -PercentComplete $currentTopPercent
        }
        elseif ($isRootLevel) {
            $rootPercent = if ($totalItems -gt 0) { ($itemIndex / $totalItems) * 100 } else { 0 }
            Update-V1ReferenceProgress -Child -Status "Root TOC item $itemIndex/${totalItems}: $($item.name)" -PercentComplete $rootPercent
        }
        elseif ($script:V1TopTocTotal -gt 0 -and $script:V1TopTocIndex -gt 0) {
            $currentTopPercent = ($script:V1TopTocIndex / $script:V1TopTocTotal) * 100
            $status = Get-V1ApiChildStatus -Detail "processing $($item.name)"
            Update-V1ReferenceProgress -Child -Status $status -PercentComplete $currentTopPercent
        }

        # If href is toc.yml, load recursively
        if ($href -and $href -like "*.yml") {
            $children = Get-TocHierarchy -tocPath $href -parentPath $currentPath -ApiSectionDepth $nextApiSectionDepth
        }
        # If items exist, process recursively
        elseif ($item.items) {
            $children = ConvertFrom-TocItems -items $item.items -parentPath $currentPath -ApiSectionDepth $nextApiSectionDepth
        }
        # If href doesn't start with ../../api, skip as it's some overview page
        elseif ($href -and -not ($href -like "../../api*")) {
            continue
        }

        # Generate GUID from full path
        $guid = Get-DeterministicGuid -InputString $currentPath

        # Reaching here means the item being checked is a reference page containing Examples, or its parent
        $exampleTitles = @()
        if ($href) {
            # Get list of Example titles
            $referenceFullPath = "$baseUrl$($href.Trim("../../"))"
            $script:subRequestCount++
            Write-Verbose "  [$script:requestCount-$script:subRequestCount] Getting Examples: $($item.name) : $referenceFullPath"
            $exampleTitles = Get-ExampleTitle -examplePath $referenceFullPath

            if ($exampleTitles.Count -eq 0) {
                Write-Verbose "  [$script:requestCount-$script:subRequestCount] No Examples: $($item.name)"

                $result += [PSCustomObject]@{
                    Id       = $guid.ToString()
                    Type     = 0
                    Name     = $item.name
                    Children = $children
                }
            }
            elseif ($exampleTitles.Count -eq 1) {
                # If there's only one Example, add it directly as a child
                $result += [PSCustomObject]@{
                    Id        = $guid.ToString()
                    Type      = 1
                    Name      = $item.name
                    Method    = ""
                    Url       = ""
                    Headers   = @{}
                    Body      = ""
                    Children  = @()
                    IsBuiltIn = $false
                }
            }
            else {
                # If there are multiple Examples, add child nodes for each Example
                $exampleChildren = @()
                foreach ($title in $exampleTitles) {
                    $exampleGuid = Get-DeterministicGuid -InputString "$currentPath/$title"
                    $exampleChildren += [PSCustomObject]@{
                        Id        = $exampleGuid.ToString()
                        Type      = 1
                        Name      = $title
                        Method    = ""
                        Url       = ""
                        Headers   = @{}
                        Body      = ""
                        Children  = @()
                        IsBuiltIn = $false
                    }
                }

                $result += [PSCustomObject]@{
                    Id       = $guid.ToString()
                    Type     = 0
                    Name     = $item.name
                    Children = $exampleChildren + $children
                }
            }
        }
        else {
            $result += [PSCustomObject]@{
                Id       = $guid.ToString()
                Type     = 0
                Name     = $item.name
                Children = $children
            }
        }
        
    }
    return $result
}

function Get-TocHierarchy {
    param (
        [string]$tocPath,
        [string]$parentPath = "",
        [int]$ApiSectionDepth = -1
    )

    $url = if ($tocPath -like "http*") { $tocPath } else { "$baseUrl$tocPath" }
    try {
        $yamlContent = Invoke-RestMethodWithRetry -Uri $url
        $toc = ConvertFrom-Yaml $yamlContent
    }
    catch {
        Write-Warning "Load failed: $url"
        return @()
    }

    return ConvertFrom-TocItems -items $toc.items -parentPath $parentPath -ApiSectionDepth $ApiSectionDepth
}

function Get-ExampleTitle {
    param (
        [string]$examplePath
    )

    try {
        # Get Markdown file
        $markdownContent = Invoke-RestMethodWithRetry -Uri $examplePath -RequestType Secondary
        $lines = $markdownContent -split "`n"
        
        $inExamplesSection = $false
        $examplesWithPrefix = [System.Collections.ArrayList]@()  # "Example n:" format
        $genericHeadings = [System.Collections.ArrayList]@()      # Generic ### headings
        $foundRequestResponse = $false
        $numberedRequests = [System.Collections.ArrayList]@()
        
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i].Trim()
            
            # Find ## Examples or ## Example section
            if ($line -match '^##\s+Examples?\s*$') {
                $inExamplesSection = $true
                continue
            }
            
            # Process within Examples section
            if ($inExamplesSection) {
                # Exit when next ## section (## only) is found
                if ($line -match '^##\s+[^#]') {
                    break
                }
                
                # Detect headings starting with ### or higher (###, ####, #####, etc.)
                if ($line -match '^#{3,}\s+(.+)$') {
                    $title = $matches[1].Trim()
                    
                    # Pattern 1: Collect headings in "Example X:" format
                    if ($title -match '^Example\s+\d+:\s*(.+)$') {
                        # Trim "Example n: " and add only the description
                        [void]$examplesWithPrefix.Add($matches[1].Trim())
                    }
                    # Pattern 2: Detect "Request n" or "Response n" format
                    elseif ($title -match '^Request\s+(\d+)$') {
                        $requestNum = $matches[1]
                        [void]$numberedRequests.Add("Request $requestNum")
                    }
                    # Pattern 3: ### headings without "Example n:" prefix (excluding Request/Response)
                    elseif ($title -ne 'Request' -and $title -ne 'Response' -and 
                        -not ($title -match '^Response\s+\d+$')) {
                        # Collect as generic heading
                        [void]$genericHeadings.Add($title)
                    }
                    # Found normal Request or Response (for detection when Example X: is absent)
                    elseif ($title -eq 'Request' -or $title -eq 'Response') {
                        $foundRequestResponse = $true
                    }
                }
            }
        }
        
        # Priority: 1. Numbered Request > 2. "Example n:" format > 3. Generic headings > 4. Request/Response only
        
        # Prioritize numbered Requests if found
        if ($numberedRequests.Count -gt 0) {
            return , [string[]]$numberedRequests.ToArray()
        }
        
        # Prioritize "Example n:" format if found
        if ($examplesWithPrefix.Count -gt 0) {
            return , [string[]]$examplesWithPrefix.ToArray()
        }
        
        # If generic headings are found
        if ($genericHeadings.Count -gt 0) {
            return , [string[]]$genericHeadings.ToArray()
        }
        
        # If Example X: is not found and only normal Request/Response is found
        if ($foundRequestResponse) {
            return , [string[]]@("Example")
        }
        
        # If nothing is found
        return , [string[]]@()
    }
    catch {
        Write-Warning "Failed to read Markdown file: $examplePath - $($_.Exception.Message)"
        return , [string[]]@()
    }
}

function ConvertTo-ChildrenArrays {
    param (
        $obj
    )
    
    if ($obj -is [array]) {
        # Process each element of array recursively
        return @($obj | ForEach-Object { ConvertTo-ChildrenArrays $_ })
    }
    elseif ($obj -is [PSCustomObject]) {
        # Process each property of PSCustomObject
        # Use ordered hashtable to control property order
        $newObj = [ordered]@{}
        
        # Add properties in desired order
        $propertyOrder = @('Id', 'Type', 'Name', 'Method', 'Url', 'Headers', 'Body', 'Children', 'IsBuiltIn')
        
        foreach ($propName in $propertyOrder) {
            $prop = $obj.PSObject.Properties[$propName]
            if ($null -ne $prop) {
                if ($propName -eq 'Children') {
                    # Always make Children property an array
                    if ($null -eq $prop.Value) {
                        $newObj[$propName] = @()
                    }
                    elseif ($prop.Value -is [array]) {
                        # If already an array, process its elements recursively
                        $newObj[$propName] = @($prop.Value | ForEach-Object { ConvertTo-ChildrenArrays $_ })
                    }
                    else {
                        # If not an array, convert to array and process recursively
                        $newObj[$propName] = @(ConvertTo-ChildrenArrays $prop.Value)
                    }
                }
                else {
                    # Process properties other than Children recursively
                    $newObj[$propName] = ConvertTo-ChildrenArrays $prop.Value
                }
            }
        }
        
        # Add additional properties not in the order list to the end
        foreach ($prop in $obj.PSObject.Properties) {
            if ($prop.Name -notin $propertyOrder) {
                $newObj[$prop.Name] = ConvertTo-ChildrenArrays $prop.Value
            }
        }
        
        return [PSCustomObject]$newObj
    }
    else {
        # Return primitive types and other types as is
        return $obj
    }
}

function Export-MicrosoftGraphV1ReferenceTocToJson {
    param (
        [string]$outputPath = "c:\temp\graph-api-hierarchy-tree_Preview10.json"
    )

    # Start from root toc.yml
    $rootToc = "toc.yml"
    $hierarchy = Get-TocHierarchy -tocPath $rootToc

    # Ensure all Children properties are arrays
    $processed = ConvertTo-ChildrenArrays -obj $hierarchy

    # Output to JSON (hierarchical structure)
    $processed | ConvertTo-Json -Depth 20 | Out-File -Encoding utf8 $outputPath

    $elapsed = (Get-Date) - $script:startTime
    Write-Host "✅ Hierarchical structure output to $outputPath. (Processed: $script:totalHttpRequests items, Elapsed time: $($elapsed.ToString('mm\:ss')))" -ForegroundColor Green
}

function Test-MicrosoftGraphV1ReferenceChanges {
    [CmdletBinding()]
    param (
        [string]$graphEditorFolder = [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Graph Editor'),
        [switch]$NoProgress
    )

    $script:V1ProgressEnabled = -not $NoProgress
    Update-V1ReferenceProgress -Status "Preparing execution" -PercentComplete 5

    try {

        # Ensure Graph Editor folder exists
        if (-not (Test-Path $graphEditorFolder)) {
            New-Item -Path $graphEditorFolder -ItemType Directory -Force | Out-Null
            Write-Host "Created folder: $graphEditorFolder" -ForegroundColor Gray
        }

        $latestFile = Join-Path $graphEditorFolder "graph-api-hierarchy_latest.json"
        $previousFile = Join-Path $graphEditorFolder "graph-api-hierarchy_previous.json"
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $backupFile = Join-Path $graphEditorFolder "graph-api-hierarchy_$timestamp.json"
        $logFile = Join-Path $graphEditorFolder "GraphEditorSupportScripts.log"
    
        # Get current time with timezone
        $now = Get-Date
        $timezone = [System.TimeZoneInfo]::Local
        $timezoneOffset = $timezone.GetUtcOffset($now).ToString("hh\:mm")
        $timezoneSign = if ($timezone.GetUtcOffset($now).TotalMinutes -ge 0) { "+" } else { "-" }
        $timestampWithTz = $now.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC$timezoneSign$timezoneOffset)"

        # Check if this is the first run
        $isFirstRun = -not (Test-Path $latestFile)

        # If latest file exists, copy it to previous
        if (-not $isFirstRun) {
            Copy-Item -Path $latestFile -Destination $previousFile -Force
        }

        # Reset counters for new execution
        $script:requestCount = 0
        $script:subRequestCount = 0
        $script:totalHttpRequests = 0
        $script:startTime = Get-Date
        $script:V1TopTocTotal = 0
        $script:V1TopTocIndex = 0
        $script:V1TopTocName = ""
        $script:V1InnerTotal = 0
        $script:V1InnerIndex = 0
        Update-V1ReferenceProgress -Status "Exporting latest hierarchy" -PercentComplete 20
        Update-V1ReferenceProgress -Child -Status "Starting TOC traversal" -PercentComplete 0

        # Execute export to create new latest file
        Export-MicrosoftGraphV1ReferenceTocToJson -outputPath $latestFile
        Update-V1ReferenceProgress -Status "Saving backup and rotating old files" -PercentComplete 80

        # Always create timestamped backup
        Copy-Item -Path $latestFile -Destination $backupFile -Force
        Write-Host "Backup saved: $backupFile" -ForegroundColor Gray

        # Clean up old backups (older than 30 days)
        $cutoffDate = (Get-Date).AddDays(-30)
        Get-ChildItem -Path $graphEditorFolder -Filter "graph-api-hierarchy_*.json" | 
        Where-Object { $_.Name -match '^graph-api-hierarchy_\d{8}_\d{6}\.json$' -and $_.LastWriteTime -lt $cutoffDate } | 
        ForEach-Object {
            Remove-Item $_.FullName -Force
            Write-Verbose "Removed old backup: $($_.Name)"
        }

        # If first run, display message and exit
        if ($isFirstRun) {
            Update-V1ReferenceProgress -Status "First run complete" -PercentComplete 100
            Write-Host "`nFirst run. Comparison will be performed on next execution." -ForegroundColor Cyan
            Write-Host "Saved to: $latestFile" -ForegroundColor Cyan
        
            # Write log entry
            $logEntry = "[$timestampWithTz] Test-MicrosoftGraphV1ReferenceChanges - First run - Initial data collected`n"
            Add-Content -Path $logFile -Value $logEntry -Encoding UTF8
        
            return
        }

        # Compare previous and latest files
        Update-V1ReferenceProgress -Status "Comparing previous and latest snapshots" -PercentComplete 92
        $previousContent = Get-Content -Path $previousFile -Raw -Encoding UTF8
        $latestContent = Get-Content -Path $latestFile -Raw -Encoding UTF8

        if ($previousContent -eq $latestContent) {
            Write-Host "`n✅ No changes detected since last execution." -ForegroundColor Green
        
            # Write log entry
            $logEntry = "[$timestampWithTz] Test-MicrosoftGraphV1ReferenceChanges - No changes detected`n"
            Add-Content -Path $logFile -Value $logEntry -Encoding UTF8
        }
        else {
            Write-Host "`n⚠️ Changes detected since last execution." -ForegroundColor Yellow
            Write-Host "Previous: $previousFile" -ForegroundColor Yellow
            Write-Host "Latest:   $latestFile" -ForegroundColor Yellow
            Write-Host "Backup:   $backupFile" -ForegroundColor Yellow
        
            # Write log entry
            $logEntry = "[$timestampWithTz] Test-MicrosoftGraphV1ReferenceChanges - Changes detected - Backup: $backupFile`n"
            Add-Content -Path $logFile -Value $logEntry -Encoding UTF8
        }

        Update-V1ReferenceProgress -Status "Completed" -PercentComplete 100
    }
    finally {
        Complete-V1ReferenceProgress
        $script:V1ProgressEnabled = $false
        $script:V1TopTocTotal = 0
        $script:V1TopTocIndex = 0
        $script:V1TopTocName = ""
        $script:V1InnerTotal = 0
        $script:V1InnerIndex = 0
    }
}
