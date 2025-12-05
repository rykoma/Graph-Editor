# PowerShell script: Retrieve the hierarchical structure of Microsoft Graph API documentation and output it as JSON
# Install required modules if not available
if (-not (Get-Module -ListAvailable -Name powershell-yaml)) {
    Install-Module -Name powershell-yaml -Force -Scope CurrentUser
}
Import-Module powershell-yaml

$baseUrl = "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-docs-contrib/main/api-reference/v1.0/"

# Counters for progress display
$script:requestCount = 0
$script:subRequestCount = 0
$script:totalHttpRequests = 0  # Total number of HTTP requests
$script:startTime = Get-Date

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
            $result = Invoke-RestMethod -Uri $Uri -ErrorAction Stop
            
            # Display progress (only on first success)
            if ($i -eq 0) {
                $script:totalHttpRequests++  # Count total number of requests
                
                if ($RequestType -eq 'Primary') {
                    # Primary request (YAML files, etc.)
                    $script:requestCount++
                    $script:subRequestCount = 0
                    Write-Host "[$script:requestCount] $Uri" -ForegroundColor Gray
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
        [string]$parentPath = ""
    )

    $result = @()
    foreach ($item in $items) {
        if ($item.name -eq "API v1.0 reference") {
            $item.name = "Custom query"
        }
        
        $href = $item.href
        $children = @()

        # Build full path to current item
        $currentPath = if ($parentPath) { "$parentPath/$($item.name)" } else { $item.name }

        # If href is toc.yml, load recursively
        if ($href -and $href -like "*.yml") {
            $children = Get-TocHierarchy -tocPath $href -parentPath $currentPath
        }
        # If items exist, process recursively
        elseif ($item.items) {
            $children = ConvertFrom-TocItems -items $item.items -parentPath $currentPath
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
            Write-Host "  [$script:requestCount-$script:subRequestCount] Getting Examples: $($item.name) : $referenceFullPath" -ForegroundColor DarkGray
            $exampleTitles = Get-ExampleTitle -examplePath $referenceFullPath

            if ($exampleTitles.Count -eq 0) {
                Write-Host "  [$script:requestCount-$script:subRequestCount] No Examples: $($item.name)" -ForegroundColor DarkGray

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
        [string]$parentPath = ""
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

    return ConvertFrom-TocItems -items $toc.items -parentPath $parentPath
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
    param (
        [string]$graphEditorFolder = [System.IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Graph Editor')
    )

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

    # Execute export to create new latest file
    Export-MicrosoftGraphV1ReferenceTocToJson -outputPath $latestFile

    # Always create timestamped backup
    Copy-Item -Path $latestFile -Destination $backupFile -Force
    Write-Host "Backup saved: $backupFile" -ForegroundColor Gray

    # Clean up old backups (older than 30 days)
    $cutoffDate = (Get-Date).AddDays(-30)
    Get-ChildItem -Path $graphEditorFolder -Filter "graph-api-hierarchy_*.json" | 
    Where-Object { $_.Name -match '^graph-api-hierarchy_\d{8}_\d{6}\.json$' -and $_.LastWriteTime -lt $cutoffDate } | 
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "Removed old backup: $($_.Name)" -ForegroundColor DarkGray
    }

    # If first run, display message and exit
    if ($isFirstRun) {
        Write-Host "`nFirst run. Comparison will be performed on next execution." -ForegroundColor Cyan
        Write-Host "Saved to: $latestFile" -ForegroundColor Cyan
        
        # Write log entry
        $logEntry = "[$timestampWithTz] Test-MicrosoftGraphV1ReferenceChanges - First run - Initial data collected`n"
        Add-Content -Path $logFile -Value $logEntry -Encoding UTF8
        
        return
    }

    # Compare previous and latest files
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
}

# Default execution
Test-MicrosoftGraphV1ReferenceChanges
