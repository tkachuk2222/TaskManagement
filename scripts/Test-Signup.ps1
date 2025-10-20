#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test user registration flow: Signup -> Login -> Get Profile

.DESCRIPTION
    Tests the user registration and authentication flow:
    1. Register a new user (signup)
    2. Login with the new credentials
    3. Get user profile information
    
    Email and Password are required parameters.

.PARAMETER BaseUrl
    The base URL of the API. Default: http://localhost:5000 (Docker) or https://localhost:7101 (local development)

.PARAMETER Email
    Email address for registration. REQUIRED.

.PARAMETER Password
    Password for registration. REQUIRED.

.PARAMETER FullName
    Full name for the user profile. Default: "Test User"

.EXAMPLE
    .\Test-Signup.ps1 -Email "newuser@example.com" -Password "SecurePass123!"
    Register a new user with default name

.EXAMPLE
    .\Test-Signup.ps1 -Email "john@example.com" -Password "Pass123!" -FullName "John Doe"
    Register a new user with custom name

.EXAMPLE
    .\Test-Signup.ps1 -BaseUrl "https://api.example.com" -Email "test@test.com" -Password "Test123"
    Register against a different API server
#>

param(
    [Parameter(Mandatory=$false, HelpMessage="API base URL")]
    [string]$BaseUrl = "http://localhost:5000",
    
    [Parameter(Mandatory=$true, HelpMessage="Email address for registration")]
    [string]$Email,
    
    [Parameter(Mandatory=$true, HelpMessage="Password for registration")]
    [string]$Password,
    
    [Parameter(Mandatory=$false, HelpMessage="Full name for user profile")]
    [string]$FullName = "Test User"
)

# Colors for output
$script:Green = if ($PSVersionTable.PSVersion.Major -ge 6) { "`e[32m" } else { "" }
$script:Red = if ($PSVersionTable.PSVersion.Major -ge 6) { "`e[31m" } else { "" }
$script:Yellow = if ($PSVersionTable.PSVersion.Major -ge 6) { "`e[33m" } else { "" }
$script:Blue = if ($PSVersionTable.PSVersion.Major -ge 6) { "`e[34m" } else { "" }
$script:Reset = if ($PSVersionTable.PSVersion.Major -ge 6) { "`e[0m" } else { "" }

function Write-Success {
    param([string]$Message)
    Write-Host "$Green✓ $Message$Reset"
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "$Red✗ $Message$Reset"
}

function Write-Info {
    param([string]$Message)
    Write-Host "$Blue➜ $Message$Reset"
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "$Yellow⚠ $Message$Reset"
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "$Blue═══════════════════════════════════════════════════$Reset"
    Write-Host "$Blue  $Title$Reset"
    Write-Host "$Blue═══════════════════════════════════════════════════$Reset"
}

function Get-TruncatedToken {
    param([string]$Token, [int]$Length = 20)
    if ([string]::IsNullOrEmpty($Token)) {
        return "[empty]"
    }
    if ($Token.Length -le $Length) {
        return $Token
    }
    return $Token.Substring(0, $Length) + "..."
}

# Global variables
$script:ApiVersion = "v1"
$script:ApiBaseUrl = "$BaseUrl/api/$ApiVersion"
$script:TestsPassed = 0
$script:TestsFailed = 0
$script:AccessToken = $null
$script:RefreshToken = $null
$script:UserId = $null

# Trust self-signed certificates (for localhost testing)
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
    $PSDefaultParameterValues['Invoke-WebRequest:SkipCertificateCheck'] = $true
}

function Test-ApiEndpoint {
    param(
        [string]$TestName,
        [scriptblock]$TestBlock
    )
    
    Write-Info $TestName
    try {
        & $TestBlock
        $script:TestsPassed++
        Write-Success "PASSED"
    }
    catch {
        $script:TestsFailed++
        Write-Error-Custom "FAILED: $_"
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response: $responseBody"
        }
    }
}

# ============================================================================
# MAIN TEST FLOW
# ============================================================================

Write-Host ""
Write-Host "$Blue╔════════════════════════════════════════════════════╗$Reset"
Write-Host "$Blue║     Task Management API - User Signup Test        ║$Reset"
Write-Host "$Blue╚════════════════════════════════════════════════════╝$Reset"
Write-Host ""
Write-Host "API Base URL: $Blue$ApiBaseUrl$Reset"
Write-Host "Email: $Blue$Email$Reset"
Write-Host "Full Name: $Blue$FullName$Reset"
Write-Host ""

# ============================================================================
# 1. REGISTER NEW USER (SIGNUP)
# ============================================================================

Write-Section "1. User Registration (Signup)"

Test-ApiEndpoint "Register new user" {
    $registerBody = @{
        email = $Email
        password = $Password
        fullName = $FullName
    } | ConvertTo-Json

    Write-Host "Request Body: $registerBody"

    $response = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/auth/register" `
        -Method Post `
        -Body $registerBody `
        -ContentType "application/json"

    if (-not $response.accessToken) {
        throw "No access token in registration response"
    }

    $script:AccessToken = $response.accessToken
    $script:RefreshToken = $response.refreshToken
    $script:UserId = $response.userId

    Write-Host "User ID: $($response.userId)"
    Write-Host "Access Token: $(Get-TruncatedToken $response.accessToken)"
    Write-Host "Refresh Token: $(Get-TruncatedToken $response.refreshToken)"
}

# ============================================================================
# 2. LOGIN WITH NEW CREDENTIALS
# ============================================================================

Write-Section "2. User Login"

Test-ApiEndpoint "Login with new credentials" {
    $loginBody = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json

    $response = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json"

    if (-not $response.accessToken) {
        throw "No access token in login response"
    }

    # Update tokens with login response
    $script:AccessToken = $response.accessToken
    $script:RefreshToken = $response.refreshToken

    Write-Host "Login successful"
    Write-Host "New Access Token: $(Get-TruncatedToken $response.accessToken)"
}

# ============================================================================
# 3. GET USER PROFILE
# ============================================================================

Write-Section "3. Get User Profile"

Test-ApiEndpoint "Get user profile (me)" {
    $headers = @{
        "Authorization" = "Bearer $script:AccessToken"
    }

    $response = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/auth/me" `
        -Method Get `
        -Headers $headers

    if (-not $response.id) {
        throw "No user ID in profile response"
    }

    Write-Host "User Profile:"
    Write-Host "  ID: $($response.id)"
    Write-Host "  Email: $($response.email)"
    Write-Host "  Full Name: $($response.fullName)"
    Write-Host "  Created At: $($response.createdAt)"

    if ($response.id -ne $script:UserId) {
        throw "User ID mismatch: expected $script:UserId, got $($response.id)"
    }

    if ($response.email -ne $Email) {
        throw "Email mismatch: expected $Email, got $($response.email)"
    }

    if ($response.fullName -ne $FullName) {
        throw "Full name mismatch: expected $FullName, got $($response.fullName)"
    }
}

# ============================================================================
# 4. TEST REFRESH TOKEN
# ============================================================================

Write-Section "4. Test Token Refresh"

Test-ApiEndpoint "Refresh access token" {
    $refreshBody = @{
        refreshToken = $script:RefreshToken
    } | ConvertTo-Json

    $response = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/auth/refresh" `
        -Method Post `
        -Body $refreshBody `
        -ContentType "application/json"

    if (-not $response.accessToken) {
        throw "No access token in refresh response"
    }

    Write-Host "Token refreshed successfully"
    Write-Host "New Access Token: $(Get-TruncatedToken $response.accessToken)"
    
    # Update token for future requests
    $script:AccessToken = $response.accessToken
}

# ============================================================================
# 5. VERIFY NEW TOKEN WORKS
# ============================================================================

Write-Section "5. Verify New Token"

Test-ApiEndpoint "Get profile with refreshed token" {
    $headers = @{
        "Authorization" = "Bearer $script:AccessToken"
    }

    $response = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/auth/me" `
        -Method Get `
        -Headers $headers

    Write-Host "Profile retrieved successfully with new token"
    Write-Host "  Email: $($response.email)"
}

# ============================================================================
# SUMMARY
# ============================================================================

Write-Host ""
Write-Host "$Blue═══════════════════════════════════════════════════$Reset"
Write-Host "$Blue  TEST SUMMARY$Reset"
Write-Host "$Blue═══════════════════════════════════════════════════$Reset"
Write-Host ""
Write-Host "Total Tests: $($script:TestsPassed + $script:TestsFailed)"
Write-Host "$Green✓ Passed: $script:TestsPassed$Reset"
if ($script:TestsFailed -gt 0) {
    Write-Host "$Red✗ Failed: $script:TestsFailed$Reset"
}
Write-Host ""

if ($script:TestsFailed -eq 0) {
    Write-Success "All tests passed! ✨"
    Write-Host ""
    Write-Host "User Details:"
    Write-Host "  Email: $Email"
    Write-Host "  User ID: $script:UserId"
    Write-Host "  Access Token: Available for use"
    Write-Host ""
    exit 0
} else {
    Write-Error-Custom "Some tests failed!"
    exit 1
}
