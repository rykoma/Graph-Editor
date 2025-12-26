# This script connects to Microsoft Graph, retrieves the OAuth2 permission scopes for the Microsoft Graph API using Microsoft Graph PowerShell SDK, formats them, and copies them to the clipboard.
# The copied output is intended to be pasted as the code for the WellKnownScopes property section in Data\MicrosoftGraphScope\MicrosoftGraphScope.cs.
function Copy-MicrosoftGraphScopes {
    Write-Host "Connecting to Microsoft Graph..."
    Connect-MgGraph -Scopes Application.Read.All -UseDeviceCode -NoWelcome -ContextScope Process

    $DefaultScopes = @(
        "Calendars.ReadWrite",
        "Contacts.ReadWrite",
        "Mail.ReadWrite",
        "Mail.Send",
        "MailboxSettings.ReadWrite",
        "Tasks.ReadWrite",
        "User.ReadWrite"
    )

    $Scopes = (Invoke-MgGraphRequest -Uri "https://graph.microsoft.com/v1.0/servicePrincipals(appId='00000003-0000-0000-c000-000000000000')?`$select=oauth2PermissionScopes").oauth2PermissionScopes

    $FormattedScopes = $Scopes | Select-Object @{
        Name       = "Scope"; 
        Expression = { $_["value"] }
    }, @{
        Name       = "Identifier"; 
        Expression = { $_["id"] }
    }, @{
        Name       = "Description"; 
        Expression = { $_["adminConsentDescription"].Trim() }
    }, @{
        Name       = "AdminConsentRequired"; 
        Expression = { ($_["type"] -eq "Admin") }
    } | Sort-Object Scope | ForEach-Object {
        "            new MicrosoftGraphScope(`"$($_.Scope)`", $($DefaultScopes.Contains($_.Scope).ToString().ToLower()))"
    }

    [string]::Join(",$([Environment]::NewLine)", $FormattedScopes) | clip

    Disconnect-MgGraph | Out-Null
    Write-Host "Disconnected from Microsoft Graph."
    Write-Host "The scopes have been formatted and copied to the clipboard."
}