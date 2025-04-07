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
        }
        else {
            Write-Host "No matching files found." -ForegroundColor Red
        }
        Write-Host ""
    }
}

# Define a global variable to store the cache
$global:YamlCache = @{} 

# Function to fetch and parse the YAML file from the provided URL
function Get-YamlContent {
    param (
        [string]$url
    )

    # Check if the URL is already cached
    if ($global:YamlCache.ContainsKey($url)) {
        return $global:YamlCache[$url]
    }

    try {
        # Fetch the YAML content from the URL
        $yamlContent = Invoke-RestMethod -Uri $url -Method Get -Headers @{ "User-Agent" = "PowerShell" }

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
        [string]$mainYamlUrl = "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-docs-contrib/refs/heads/main/api-reference/v1.0/toc.yml"
    )

    # Get the parsed main YAML content
    $mainYamlData = Get-YamlContent -url $mainYamlUrl

    if (-not $mainYamlData) {
        Write-Host "Failed to retrieve or parse the main YAML file." -ForegroundColor Red
        return
    }

    # Find the "API v1.0 reference" node
    $apiReferenceNode = $mainYamlData.items | Where-Object { $_.name -eq "API v1.0 reference" }

    if (-not $apiReferenceNode) {
        Write-Host "The 'API v1.0 reference' node is not found in the main YAML data." -ForegroundColor Red
        return
    }

    # Initialize a String List to store the locations
    $locations = New-Object System.Collections.Generic.List[System.String]

    # Function to search for the filename in a single YAML file
    function Search-InYamlFile {
        param (
            [string]$yamlUrl,
            [string]$filename,
            [System.Collections.Generic.List[System.String]]$locations
        )

        # Get the parsed YAML content
        $yamlData = Get-YamlContent -url $yamlUrl

        if (-not $yamlData) {
            Write-Host "Failed to retrieve or parse the YAML file: $yamlUrl" -ForegroundColor Red
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
                    write-Host "Found file: $($node.href)" -ForegroundColor Green
                    $locations.Add($path + "/" + $node.name)
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
            Search-InYamlFile -yamlUrl $yamlUrl -filename $filename -locations $locations
        }
        else {
            # If the item is not a toc.yml file, write the item name
            Write-Host "Skipping non-toc.yml file: $($item.href)" -ForegroundColor Yellow
        }
    }

    # Output the locations
    if ($locations.Count -gt 0) {
        Write-Host "The file '$filename' is found at the following locations:" -ForegroundColor Green
        $locations | ForEach-Object { Write-Host $_ -ForegroundColor White }
    }
    else {
        Write-Host "The file '$filename' is not found in the YAML data." -ForegroundColor Red
    }
}
