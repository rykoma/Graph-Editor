# Retrieve the commit history of a specified GitHub organization repository.
# Filter the commits to include only those from a specified date.
# For the filtered commits, get a list of files whose names start with "api-reference/v1.0/api/".
# Finally, output the commits and the corresponding list of files.
function Get-CommitHistory {
    param (
        [string]$Organization = "microsoftgraph",
        [string]$Repository = "microsoft-graph-docs-contrib",
        [DateTime]$Date
    )

    # Convert JST to UTC
    $utcStart = ($Date.Date).ToUniversalTime()
    $utcEnd = ($Date.Date.AddDays(1).AddSeconds(-1)).ToUniversalTime()

    # GitHub API URL
    $url = "https://api.github.com/repos/$Organization/$Repository/commits?since=$($utcStart.ToString('yyyy-MM-ddTHH:mm:ssZ'))&until=$($utcEnd.ToString('yyyy-MM-ddTHH:mm:ssZ'))&sha=main"

    # Retrieve commit history from the GitHub API
    $commits = Invoke-RestMethod -Uri $url -Method Get -Headers @{ "User-Agent" = "PowerShell" }

    # Get the list of files included in each commit
    foreach ($commit in $commits) {
        $commitSha = $commit.sha
        $commitMessage = $commit.commit.message -split "`n" | Select-Object -First 1
        $commitDateUtc = $commit.commit.author.date
        $commitDateLocal = [DateTime]::Parse($commitDateUtc).ToLocalTime()
        $commitUrl = "https://github.com/$Organization/$Repository/commit/$commitSha"
        $response = Invoke-RestMethod -Uri "https://api.github.com/repos/$Organization/$Repository/commits/$commitSha" -Method Get -Headers @{ "User-Agent" = "PowerShell" }

        # Filter files whose names start with "api-reference/v1.0/api/"
        $filteredFiles = $response.files | Where-Object { $_.filename -like "api-reference/v1.0/api/*" }

        # Output the results
        Write-Host "Commit URL: $commitUrl" -ForegroundColor Green
        Write-Host "Message: $commitMessage" -ForegroundColor Cyan
        Write-Host "Date (Local): $commitDateLocal" -ForegroundColor Yellow

        if ($filteredFiles) {
            Write-Host "Files:" -ForegroundColor Magenta
            $filteredFiles | ForEach-Object {
            Write-Host "  - $($_.filename) (Status: $($_.status))" -ForegroundColor White
            }
        } else {
            Write-Host "No matching files found." -ForegroundColor Red
        }
        Write-Host ""
    }
}