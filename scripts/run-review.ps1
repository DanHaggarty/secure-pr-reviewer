<#
.SYNOPSIS
Prompts for the inputs SecurePrReviewer.App needs and runs a review.
#>

$repoPath = Read-Host "Repository path (Enter for current directory)"
if ([string]::IsNullOrWhiteSpace($repoPath)) {
    $repoPath = (Get-Location).Path
}

$diffSource = Read-Host "PR URL (https://github.com/owner/repo/pull/123) or path to a diff file"

if ($diffSource -match '^https://github\.com/' -and -not $env:GITHUB_TOKEN) {
    $secureToken = Read-Host "GitHub PAT" -AsSecureString
    $env:GITHUB_TOKEN = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    )
}

Write-Host "Starting SecurePrReviewer.App - this can take a minute or more; output prints as each step completes." -ForegroundColor Cyan
dotnet run --project src/SecurePrReviewer.App -- "$repoPath" "$diffSource"
