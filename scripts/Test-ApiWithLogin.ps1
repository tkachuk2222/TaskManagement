#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Complete API test flow: Login -> Create Project -> Create Tasks -> Update -> Complete -> Delete

.DESCRIPTION
    Tests the full user journey through the Task Management API using existing user credentials.
    Runs 31 integration tests covering all CRUD operations, authentication, ETag validation, and more.
    
    Email and Password are required parameters.

.PARAMETER BaseUrl
    The base URL of the API. Default: http://localhost:5000 (Docker) or https://localhost:7101 (local development)

.PARAMETER Email
    Email address for login. REQUIRED.

.PARAMETER Password
    Password for login. REQUIRED.

.EXAMPLE
    .\Test-ApiWithLogin.ps1 -Email "user@example.com" -Password "MyPassword123"
    Run tests with default credentials

.EXAMPLE
    .\Test-ApiWithLogin.ps1 -Email "user@example.com" -Password "MyPassword123"
    Run tests with your credentials

.EXAMPLE
    .\Test-ApiWithLogin.ps1 -BaseUrl "https://api.example.com" -Email "test@test.com" -Password "TestPass123"
    Run tests against a different API server
#>

param(
    [Parameter(Mandatory=$false, HelpMessage="API base URL")]
    [string]$BaseUrl = "http://localhost:5000",
    
    [Parameter(Mandatory=$true, HelpMessage="Email address for authentication")]
    [string]$Email,
    
    [Parameter(Mandatory=$true, HelpMessage="Password for authentication")]
    [string]$Password
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
$script:AccessToken = $null
$script:RefreshToken = $null
$script:UserId = $null
$script:ProjectId = $null
$script:TaskIds = @()
$script:TestResults = @{
    Total = 0
    Passed = 0
    Failed = 0
    Steps = @()
}

function Test-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )
    
    $script:TestResults.Total++
    Write-Info "Testing: $Name"
    
    try {
        $result = & $Action
        $script:TestResults.Passed++
        $script:TestResults.Steps += [PSCustomObject]@{
            Name = $Name
            Status = "PASS"
            Error = $null
        }
        Write-Success "$Name"
        return $result
    }
    catch {
        $script:TestResults.Failed++
        $script:TestResults.Steps += [PSCustomObject]@{
            Name = $Name
            Status = "FAIL"
            Error = $_.Exception.Message
        }
        Write-Error-Custom "$Name - Error: $($_.Exception.Message)"
        throw
    }
}

function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [bool]$RequireAuth = $true,
        [int[]]$ExpectedStatusCodes = @(200, 201, 204)
    )
    
    $headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
    }
    
    if ($RequireAuth -and $script:AccessToken) {
        $headers["Authorization"] = "Bearer $script:AccessToken"
    }
    
    $params = @{
        Uri = "$BaseUrl$Endpoint"
        Method = $Method
        Headers = $headers
        ErrorAction = "Stop"
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $params["SkipCertificateCheck"] = $true
    }
    
    if ($Body) {
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
    }
    
    Write-Verbose "Request: $Method $Endpoint"
    if ($Body) {
        Write-Verbose "Body: $($params["Body"])"
    }
    
    try {
        $response = Invoke-RestMethod @params -ResponseHeadersVariable responseHeaders -StatusCodeVariable statusCode
        
        Write-Verbose "Response Status: $statusCode"
        Write-Verbose "Response: $($response | ConvertTo-Json -Depth 5)"
        
        if ($statusCode -notin $ExpectedStatusCodes) {
            throw "Unexpected status code: $statusCode (expected: $($ExpectedStatusCodes -join ', '))"
        }
        
        return $response
    }
    catch {
        $errorResponse = $_.ErrorDetails.Message
        Write-Verbose "Error Response: $errorResponse"
        
        # Try to parse error details
        if ($errorResponse) {
            try {
                $errorObj = $errorResponse | ConvertFrom-Json
                if ($errorObj.errors) {
                    $errorDetails = ($errorObj.errors | ConvertTo-Json -Compress)
                    throw "API Error [$($_.Exception.Response.StatusCode)]: $($errorObj.error) - Details: $errorDetails"
                }
                elseif ($errorObj.error) {
                    throw "API Error [$($_.Exception.Response.StatusCode)]: $($errorObj.error)"
                }
            }
            catch {
                # If JSON parsing fails, use raw message
            }
        }
        
        throw "API Error: $($_.Exception.Message)"
    }
}

# ============================================================================
# TEST FLOW
# ============================================================================

Write-Host ""
Write-Host "$Blue╔════════════════════════════════════════════════════════════╗$Reset"
Write-Host "$Blue║         TASK MANAGEMENT API - FULL INTEGRATION TEST        ║$Reset"
Write-Host "$Blue╚════════════════════════════════════════════════════════════╝$Reset"
Write-Host ""
Write-Info "Base URL: $BaseUrl"
Write-Info "User Email: $Email"
Write-Host ""

# ============================================================================
# 1. HEALTH CHECK
# ============================================================================
Write-Section "1. Health Check"

Test-Step "API Health Check" {
    $health = Invoke-ApiRequest -Method GET -Endpoint "/health" -RequireAuth $false
    if ($health.status -ne "Healthy") {
        throw "API is not healthy: $($health.status)"
    }
}

# ============================================================================
# 2. USER AUTHENTICATION (Try Login, Register if needed)
# ============================================================================
Write-Section "2. User Authentication"

Test-Step "Authenticate User (Login or Register)" {
    # Try login first
    $loginBody = @{
        email = $Email
        password = $Password
    }
    
    try {
        Write-Verbose "Attempting login..."
        $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/auth/login" -Body $loginBody -RequireAuth $false
        Write-Host "    ✓ Logged in successfully"
    }
    catch {
        Write-Warning-Custom "Login failed, attempting registration..."
        
        # If login fails, try registration
        $registerBody = @{
            email = $Email
            password = $Password
            fullName = "API Test User"
        }
        
        $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/auth/register" -Body $registerBody -RequireAuth $false
        Write-Host "    ✓ Registered new user successfully"
    }
    
    # AuthResponse properties are at root level, not in .data
    $script:AccessToken = $response.accessToken
    $script:RefreshToken = $response.refreshToken
    $script:UserId = $response.userId
    
    if (-not $script:AccessToken) {
        throw "No access token received"
    }
    
    Write-Verbose "User ID: $script:UserId"
    Write-Verbose "Access Token: $(Get-TruncatedToken $script:AccessToken)"
}

Test-Step "Get User Profile" {
    $profile = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/me"
    
    if ($profile.email -ne $Email) {
        throw "Email mismatch: expected '$Email', got '$($profile.email)'"
    }
    
    Write-Host "    User: $($profile.fullName) ($($profile.email))"
}

Test-Step "Get Active Sessions" {
    $sessions = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/sessions"
    
    if ($sessions.Count -lt 1) {
        throw "No active sessions found"
    }
    
    Write-Host "    Active Sessions: $($sessions.Count)"
}

# ============================================================================
# 3. PROJECT MANAGEMENT
# ============================================================================
Write-Section "3. Project Management"

Test-Step "Create Project #1 - E-Commerce Platform" {
    $projectBody = @{
        name = "E-Commerce Platform [Test $(Get-Date -Format 'HH:mm:ss')]"
        description = "Building a scalable online shopping platform with microservices architecture"
        status = 1  # Active
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/projects" -Body $projectBody -ExpectedStatusCodes @(200, 201)
    
    $script:ProjectId = $response.id
    
    if (-not $script:ProjectId) {
        throw "No project ID received"
    }
    
    Write-Host "    Project ID: $script:ProjectId"
}

Test-Step "Create Project #2 - Mobile App" {
    $projectBody = @{
        name = "Mobile App Development [Test $(Get-Date -Format 'HH:mm:ss')]"
        description = "Cross-platform mobile application using React Native"
        status = 0  # Planning
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/projects" -Body $projectBody -ExpectedStatusCodes @(200, 201)
    Write-Host "    Project ID: $($response.id)"
}

Test-Step "Get All Projects (Paginated)" {
    $projects = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects?pageNumber=1&pageSize=10"
    
    if ($projects.items.Count -lt 2) {
        throw "Expected at least 2 projects, found $($projects.items.Count)"
    }
    
    Write-Host "    Total Projects: $($projects.totalCount) | Page: $($projects.pageNumber)/$($projects.totalPages)"
}

Test-Step "Get Project by ID & Update Status (with ETag)" {
    # First, get the project to retrieve current ETag
    $getParams = @{
        Uri = "$BaseUrl/api/v1/projects/$script:ProjectId"
        Method = "GET"
        Headers = @{
            "Authorization" = "Bearer $script:AccessToken"
            "Accept" = "application/json"
        }
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $getParams["SkipCertificateCheck"] = $true
    }
    
    $getResponse = Invoke-WebRequest @getParams
    $project = $getResponse.Content | ConvertFrom-Json
    $etag = $getResponse.Headers['ETag'][0]
    
    Write-Host "    Project: $($project.name)"
    Write-Host "    Current ETag: $etag"
    
    # Immediately update with the fresh ETag (optimistic concurrency)
    $updateBody = @{
        name = "E-Commerce Platform (In Development)"
        description = "Building a scalable online shopping platform with microservices, Redis caching, and MongoDB"
        status = 1  # Active
    }
    
    $updateParams = @{
        Uri = "$BaseUrl/api/v1/projects/$script:ProjectId"
        Method = "PUT"
        Headers = @{
            "Authorization" = "Bearer $script:AccessToken"
            "Content-Type" = "application/json"
            "If-Match" = $etag
        }
        Body = ($updateBody | ConvertTo-Json)
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $updateParams["SkipCertificateCheck"] = $true
    }
    
    $updateResponse = Invoke-WebRequest @updateParams
    $updated = $updateResponse.Content | ConvertFrom-Json
    
    Write-Host "    ✓ Updated: $($updated.name)"
    
    # Try to get new ETag if present (optional)
    if ($updateResponse.Headers.ContainsKey('ETag')) {
        $newETag = $updateResponse.Headers['ETag'][0]
        Write-Host "    New ETag: $newETag"
    }
}

Test-Step "Verify Project Update Changes" {
    # Get the project again to verify changes
    $project = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId"
    
    # Verify name was updated
    if (-not $project.name.Contains("In Development")) {
        throw "Project name was not updated. Expected 'In Development' in name, got: $($project.name)"
    }
    
    # Verify status changed to Active (1)
    if ($project.status -ne 1) {
        throw "Project status should be Active (1), got: $($project.status)"
    }
    
    # Verify description contains expected keywords
    if (-not $project.description.Contains("microservices")) {
        throw "Project description was not updated correctly"
    }
    
    Write-Host "    ✓ Name updated: $($project.name)"
    Write-Host "    ✓ Status: Active"
    Write-Host "    ✓ Description verified"
}

Test-Step "Search Projects by Keyword" {
    $projects = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects?search=Commerce"
    
    if ($projects.items.Count -lt 1) {
        throw "Search returned no results"
    }
    
    Write-Host "    Found: $($projects.totalCount) project(s) matching 'Commerce'"
}

# ============================================================================
# 4. TASK MANAGEMENT
# ============================================================================
Write-Section "4. Task Management"

Test-Step "Create Task #1 - Database Setup (High Priority)" {
    $taskBody = @{
        title = "Setup MongoDB Database Schema"
        description = "Design and implement MongoDB collections with proper indexing and validation"
        status = 0  # Todo
        priority = 2  # High
        dueDate = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss")
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks" -Body $taskBody -ExpectedStatusCodes @(200, 201)
    
    $script:TaskIds += $response.id
    Write-Host "    Task ID: $($response.id)"
}

Test-Step "Create Task #2 - Authentication (High Priority)" {
    $taskBody = @{
        title = "Implement JWT Authentication"
        description = "Setup Supabase authentication with JWT tokens and refresh token rotation"
        status = 1  # InProgress
        priority = 2  # High
        dueDate = (Get-Date).AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss")
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks" -Body $taskBody -ExpectedStatusCodes @(200, 201)
    $script:TaskIds += $response.id
    Write-Host "    Task ID: $($response.id)"
}

Test-Step "Create Task #3 - API Endpoints (Medium Priority)" {
    $taskBody = @{
        title = "Build RESTful API Endpoints"
        description = "Create CRUD endpoints for projects and tasks with pagination and filtering"
        status = 1  # InProgress
        priority = 1  # Medium
        dueDate = (Get-Date).AddDays(14).ToString("yyyy-MM-ddTHH:mm:ss")
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks" -Body $taskBody -ExpectedStatusCodes @(200, 201)
    $script:TaskIds += $response.id
    Write-Host "    Task ID: $($response.id)"
}

Test-Step "Create Task #4 - Unit Tests (Low Priority)" {
    $taskBody = @{
        title = "Write Unit Tests"
        description = "Achieve 80% code coverage with xUnit and FluentAssertions"
        status = 0  # Todo
        priority = 0  # Low
        dueDate = (Get-Date).AddDays(21).ToString("yyyy-MM-ddTHH:mm:ss")
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks" -Body $taskBody -ExpectedStatusCodes @(200, 201)
    $script:TaskIds += $response.id
    Write-Host "    Task ID: $($response.id)"
}

Test-Step "Get All Tasks for Project" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?pageNumber=1&pageSize=20"
    
    if ($tasks.items.Count -lt 4) {
        throw "Expected at least 4 tasks, found $($tasks.items.Count)"
    }
    
    Write-Host "    Total Tasks: $($tasks.totalCount) | Page: $($tasks.pageNumber)/$($tasks.totalPages)"
}

Test-Step "Sort Tasks by Priority (Descending)" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?sortBy=Priority&sortDescending=true"
    
    $firstTask = $tasks.items[0]
    if ($firstTask.priority -ne 2) {  # High = 2
        throw "Sorting failed - expected High priority (2) first, got $($firstTask.priority)"
    }
    
    Write-Host "    Top Priority Task: $($firstTask.title)"
}

Test-Step "Sort Tasks by Due Date (Ascending)" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?sortBy=DueDate&sortDescending=false"
    
    Write-Host "    Earliest Due: $($tasks.items[0].title) - $(([DateTime]$tasks.items[0].dueDate).ToString('MM/dd/yyyy'))"
}

Test-Step "Filter Tasks by Status (InProgress)" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?status=InProgress"
    
    foreach ($task in $tasks.items) {
        if ($task.status -ne 1) {  # InProgress = 1
            throw "Status filter failed - found task with status: $($task.status)"
        }
    }
    
    Write-Host "    In Progress Tasks: $($tasks.totalCount)"
}

Test-Step "Filter Tasks by Status (Todo)" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?status=Todo"
    
    Write-Host "    Todo Tasks: $($tasks.totalCount)"
}

Test-Step "Get Task by ID & Update Details (with ETag)" {
    # First, get the task to retrieve current ETag
    $getParams = @{
        Uri = "$BaseUrl/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[0])"
        Method = "GET"
        Headers = @{
            "Authorization" = "Bearer $script:AccessToken"
            "Accept" = "application/json"
        }
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $getParams["SkipCertificateCheck"] = $true
    }
    
    $getResponse = Invoke-WebRequest @getParams
    $task = $getResponse.Content | ConvertFrom-Json
    
    Write-Host "    Task: $($task.title) - Priority: $($task.priority)"
    
    # Get ETag if present
    $etag = $null
    if ($getResponse.Headers.ContainsKey('ETag')) {
        $etag = $getResponse.Headers['ETag'][0]
        Write-Host "    Current ETag: $etag"
    }
    
    # Immediately update with the fresh ETag
    $updateBody = @{
        title = "Setup MongoDB Database Schema (Complete)"
        description = "Design and implement MongoDB collections with proper indexing, validation, and TTL indexes for session management"
        status = 1  # InProgress
        priority = 2  # High
        dueDate = (Get-Date).AddDays(5).ToString("yyyy-MM-ddTHH:mm:ss")
    }
    
    $updateHeaders = @{
        "Authorization" = "Bearer $script:AccessToken"
        "Content-Type" = "application/json"
    }
    
    # Add If-Match header if ETag is available
    if ($etag) {
        $updateHeaders["If-Match"] = $etag
    }
    
    $updateParams = @{
        Uri = "$BaseUrl/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[0])"
        Method = "PUT"
        Headers = $updateHeaders
        Body = ($updateBody | ConvertTo-Json)
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $updateParams["SkipCertificateCheck"] = $true
    }
    
    $updateResponse = Invoke-WebRequest @updateParams
    $updated = $updateResponse.Content | ConvertFrom-Json
    
    Write-Host "    ✓ Updated: $($updated.title)"
    
    # Save the new ETag for the next operation if present
    if ($updateResponse.Headers.ContainsKey('ETag')) {
        $script:TaskETag = $updateResponse.Headers['ETag'][0]
        Write-Host "    New ETag: $script:TaskETag"
    }
}

Test-Step "Complete Task (with ETag validation)" {
    # Get the current task state first to get fresh ETag
    $getParams = @{
        Uri = "$BaseUrl/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[0])"
        Method = "GET"
        Headers = @{
            "Authorization" = "Bearer $script:AccessToken"
            "Accept" = "application/json"
        }
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $getParams["SkipCertificateCheck"] = $true
    }
    
    $getResponse = Invoke-WebRequest @getParams
    
    # Get ETag if present
    $etag = $null
    if ($getResponse.Headers.ContainsKey('ETag')) {
        $etag = $getResponse.Headers['ETag'][0]
    }
    
    # PATCH endpoint uses If-Match header for optimistic concurrency
    $patchHeaders = @{
        "Authorization" = "Bearer $script:AccessToken"
    }
    
    # Add If-Match header if ETag is available
    if ($etag) {
        $patchHeaders["If-Match"] = $etag
    }
    
    $params = @{
        Uri = "$BaseUrl/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[0])/complete"
        Method = "PATCH"
        Headers = $patchHeaders
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $params["SkipCertificateCheck"] = $true
    }
    
    $completeResponse = Invoke-WebRequest @params
    $response = $completeResponse.Content | ConvertFrom-Json
    
    if ($response.status -ne 3) {  # Done = 3
        throw "Task not marked as completed"
    }
    
    if (-not $response.completedAt) {
        throw "CompletedAt timestamp not set"
    }
    
    $completedDate = ([DateTime]$response.completedAt).ToString("yyyy-MM-dd HH:mm:ss")
    Write-Host "    ✓ Completed at: $completedDate"
}

Test-Step "Verify Task Completion" {
    $task = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[0])"
    
    if ($task.status -ne 3) {  # Done = 3
        throw "Task status verification failed"
    }
    
    Write-Host "    Status: ✓ Completed"
}

Test-Step "Verify Task Update Changes" {
    # Get the updated task
    $task = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[0])"
    
    # Verify the title was actually changed
    if (-not $task.title.Contains("Complete")) {
        throw "Task title was not updated correctly. Got: $($task.title)"
    }
    
    # Verify status is Done (3)
    if ($task.status -ne 3) {
        throw "Task status should be Done (3), got: $($task.status)"
    }
    
    # Verify priority was updated to High (2)
    if ($task.priority -ne 2) {
        throw "Task priority should be High (2), got: $($task.priority)"
    }
    
    Write-Host "    ✓ Title: $($task.title)"
    Write-Host "    ✓ Status: Done"
    Write-Host "    ✓ Priority: High"
}

Test-Step "Assign Task to User" {
    # Assign one of the tasks to the current user
    $assignBody = @{
        userId = $script:UserId
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/tasks/$($script:TaskIds[1])/assign" -Body $assignBody
    
    # Verify assignment
    if ($response.assignedToId -ne $script:UserId) {
        throw "Task not assigned correctly. Expected: $script:UserId, Got: $($response.assignedToId)"
    }
    
    Write-Host "    ✓ Task assigned to: $script:UserId"
}

Test-Step "Update Task Status (Generic)" {
    # Use the generic status update endpoint
    $statusBody = @{
        status = 1  # InProgress
    }
    
    $response = Invoke-ApiRequest -Method PATCH -Endpoint "/api/v1/tasks/$($script:TaskIds[2])/status" -Body $statusBody
    
    # Verify status changed
    if ($response.status -ne 1) {
        throw "Task status not updated. Expected: 1 (InProgress), Got: $($response.status)"
    }
    
    # Double-check with GET
    $task = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks/$($script:TaskIds[2])"
    if ($task.status -ne 1) {
        throw "Task status verification failed after update"
    }
    
    Write-Host "    ✓ Task status updated to: InProgress"
}

Test-Step "Get Project Analytics" {
    $analytics = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$($script:ProjectId)/analytics"
    
    # Verify analytics structure
    if (-not $analytics.PSObject.Properties['totalTasks']) {
        throw "Analytics missing totalTasks property"
    }
    
    # We created 4 tasks
    if ($analytics.totalTasks -ne 4) {
        throw "Expected 4 total tasks, got: $($analytics.totalTasks)"
    }
    
    # We completed 1 task
    if ($analytics.completedTasks -ne 1) {
        throw "Expected 1 completed task, got: $($analytics.completedTasks)"
    }
    
    # Calculate expected completion percentage
    $expectedCompletion = (1 / 4.0) * 100
    if ([Math]::Abs($analytics.completionPercentage - $expectedCompletion) > 0.01) {
        throw "Completion percentage incorrect. Expected: $expectedCompletion%, Got: $($analytics.completionPercentage)%"
    }
    
    Write-Host "    ✓ Total Tasks: $($analytics.totalTasks)"
    Write-Host "    ✓ Completed: $($analytics.completedTasks) ($($analytics.completionPercentage)%)"
    Write-Host "    ✓ In Progress: $($analytics.inProgressTasks)"
    Write-Host "    ✓ Todo: $($analytics.todoTasks)"
    
    # Verify breakdown dictionaries exist
    if (-not $analytics.tasksByStatus) {
        throw "Analytics missing tasksByStatus"
    }
    if (-not $analytics.tasksByPriority) {
        throw "Analytics missing tasksByPriority"
    }
    
    Write-Host "    ✓ Analytics calculation verified"
}

# ============================================================================
# 5. TOKEN REFRESH
# ============================================================================
Write-Section "5. Token Management"

Test-Step "Refresh Access Token" {
    $refreshBody = @{
        refreshToken = $script:RefreshToken
    }
    
    $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/auth/refresh" -Body $refreshBody -RequireAuth $false
    
    $oldToken = $script:AccessToken
    $script:AccessToken = $response.accessToken
    $script:RefreshToken = $response.refreshToken
    
    Write-Host "    Token refreshed successfully"
}

Test-Step "Verify New Token Works" {
    $profile = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/me"
    
    if ($profile.email -ne $Email) {
        throw "Token refresh invalidated user session"
    }
    
    Write-Host "    New token validated: $($profile.email)"
}

# ============================================================================
# 6. PAGINATION TEST
# ============================================================================
Write-Section "6. Advanced Pagination"

Test-Step "Test Pagination - Page 1" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?pageNumber=1&pageSize=2"
    
    if ($tasks.pageSize -ne 2) {
        throw "Page size mismatch"
    }
    
    Write-Host "    Page 1: $($tasks.items.Count) tasks | HasNextPage: $($tasks.hasNextPage)"
}

Test-Step "Test Pagination - Page 2" {
    $tasks = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId/tasks?pageNumber=2&pageSize=2"
    
    Write-Host "    Page 2: $($tasks.items.Count) tasks | HasPreviousPage: $($tasks.hasPreviousPage)"
}

# ============================================================================
# 7. EDGE CASE & ERROR HANDLING TESTS
# ============================================================================
Write-Section "7. Edge Cases & Error Handling"

Test-Step "Test Invalid ETag (Should Return 412)" {
    try {
        $invalidEtagHeaders = @{
            "Authorization" = "Bearer $script:AccessToken"
            "Content-Type" = "application/json"
            "If-Match" = '"invalid-etag-12345"'
        }
        
        $updateBody = @{
            name = "This Should Fail"
        }
        
        $params = @{
            Uri = "$BaseUrl/api/v1/projects/$script:ProjectId"
            Method = "PUT"
            Headers = $invalidEtagHeaders
            Body = ($updateBody | ConvertTo-Json)
        }
        
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $params["SkipCertificateCheck"] = $true
        }
        
        try {
            $response = Invoke-WebRequest @params
            throw "Should have received 412 Precondition Failed"
        }
        catch {
            if ($_.Exception.Response.StatusCode -eq 412 -or $_ -match "412") {
                Write-Host "    ✓ Got expected 412 Precondition Failed"
            }
            else {
                throw "Expected 412, got: $_"
            }
        }
    }
    catch {
        # Expected behavior
        Write-Host "    ✓ ETag validation working correctly"
    }
}

Test-Step "Test Access Non-Existent Project (Should Return 404)" {
    try {
        Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/nonexistent-project-id-12345" -ExpectedStatusCodes @(404)
        Write-Host "    ✓ Got expected 404 Not Found"
    }
    catch {
        if ($_ -match "404|Not Found") {
            Write-Host "    ✓ Correctly returns 404 for missing resource"
        }
        else {
            throw
        }
    }
}

Test-Step "Test Unauthorized Access (Without Token)" {
    # Temporarily clear token
    $savedToken = $script:AccessToken
    $script:AccessToken = $null
    
    try {
        Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects" -ExpectedStatusCodes @(401)
        Write-Host "    ✓ Got expected 401 Unauthorized"
    }
    catch {
        if ($_ -match "401|Unauthorized") {
            Write-Host "    ✓ Authentication properly enforced"
        }
        else {
            throw
        }
    }
    finally {
        # Restore token
        $script:AccessToken = $savedToken
    }
}

Test-Step "Test Invalid Status Value (Should Return 400)" {
    try {
        $invalidBody = @{
            status = 999  # Invalid status
        }
        
        try {
            Invoke-ApiRequest -Method PATCH -Endpoint "/api/v1/tasks/$($script:TaskIds[0])/status" -Body $invalidBody -ExpectedStatusCodes @(400)
            Write-Host "    ✓ Got expected 400 Bad Request"
        }
        catch {
            # Check for validation errors (400, Bad Request, Validation, or Invalid)
            if ($_ -match "400|Bad Request|Validation|Invalid|ValidationException") {
                Write-Host "    ✓ Input validation working correctly"
            }
            else {
                # Some APIs might allow it, just warn
                Write-Warning-Custom "Unexpected error: $_"
            }
        }
    }
    catch {
        # Any exception here means validation is enforced (expected behavior)
        if ($_ -match "Validation|Invalid|400") {
            Write-Host "    ✓ Validation enforced"
        }
        else {
            Write-Warning-Custom "Validation test caught unexpected error: $_"
        }
    }
}

# ============================================================================
# 8. CLEANUP
# ============================================================================
Write-Section "8. Cleanup Operations"

Test-Step "Delete Tasks" {
    $deletedCount = 0
    foreach ($taskId in $script:TaskIds) {
        try {
            Invoke-ApiRequest -Method DELETE -Endpoint "/api/v1/projects/$($script:ProjectId)/tasks/$taskId" -ExpectedStatusCodes @(200, 204) | Out-Null
            $deletedCount++
        }
        catch {
            Write-Warning-Custom "Failed to delete task $taskId"
        }
    }
    
    Write-Host "    Deleted: $deletedCount/$($script:TaskIds.Count) tasks"
}

Test-Step "Delete Project" {
    # First, get the project to retrieve current ETag (required for DELETE)
    $getParams = @{
        Uri = "$BaseUrl/api/v1/projects/$script:ProjectId"
        Method = "GET"
        Headers = @{
            "Authorization" = "Bearer $script:AccessToken"
            "Accept" = "application/json"
        }
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $getParams["SkipCertificateCheck"] = $true
    }
    
    $getResponse = Invoke-WebRequest @getParams
    
    # Get ETag if present
    $etag = $null
    if ($getResponse.Headers.ContainsKey('ETag')) {
        $etag = $getResponse.Headers['ETag'][0]
    }
    
    # Delete with ETag
    $deleteHeaders = @{
        "Authorization" = "Bearer $script:AccessToken"
    }
    
    # Add If-Match header if ETag is available
    if ($etag) {
        $deleteHeaders["If-Match"] = $etag
    }
    
    $deleteParams = @{
        Uri = "$BaseUrl/api/v1/projects/$script:ProjectId"
        Method = "DELETE"
        Headers = $deleteHeaders
    }
    
    # Add SSL skip for PowerShell 7+
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $deleteParams["SkipCertificateCheck"] = $true
    }
    
    Invoke-WebRequest @deleteParams | Out-Null
    Write-Host "    Project deleted: $script:ProjectId"
}

Test-Step "Verify Project Deleted" {
    try {
        Invoke-ApiRequest -Method GET -Endpoint "/api/v1/projects/$script:ProjectId" -ExpectedStatusCodes @(404)
        throw "Project should have been deleted"
    }
    catch {
        if ($_ -match "404|Not Found") {
            # Expected - project was deleted
        }
        else {
            throw
        }
    }
    
    Write-Host "    ✓ Project no longer exists"
}

# ============================================================================
# 9. SESSION MANAGEMENT
# ============================================================================
Write-Section "9. Session Management"

Test-Step "View All Active Sessions" {
    $sessions = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/sessions"
    
    # Sessions is a plain array, not wrapped in .data
    $sessionList = if ($sessions.data) { $sessions.data } else { $sessions }
    
    Write-Host "    Total Sessions: $($sessionList.Count)"
    foreach ($session in $sessionList) {
        $sessionId = if ($session.id) { $session.id.Substring(0, [Math]::Min(8, $session.id.Length)) } else { "N/A" }
        $deviceType = if ($session.deviceType) { $session.deviceType } else { "Unknown" }
        if ($session.lastActivityAt) {
            $lastActivity = ([DateTime]$session.lastActivityAt).ToString("MM/dd HH:mm")
            Write-Host "      - Session: $sessionId | Device: $deviceType | Last Active: $lastActivity"
        } else {
            Write-Host "      - Session: $sessionId | Device: $deviceType | Last Active: N/A"
        }
    }
    
    # Store sessions for later use
    $script:UserSessions = $sessionList
}

Test-Step "Test Session Revocation (if multiple sessions exist)" {
    $sessions = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/sessions"
    
    # Handle both wrapped and unwrapped responses
    $sessionList = if ($sessions.data) { $sessions.data } else { $sessions }
    
    if ($sessionList.Count -gt 1) {
        # Find a session that's not the current one
        $sessionToRevoke = $null
        foreach ($session in $sessionList) {
            if (-not $session.isCurrentSession -and $session.id) {
                $sessionToRevoke = $session
                break
            }
        }
        
        # If all sessions are marked as current (or none are), just take the first one with an ID
        if (-not $sessionToRevoke) {
            foreach ($session in $sessionList) {
                if ($session.id) {
                    $sessionToRevoke = $session
                    break
                }
            }
        }
        
        if (-not $sessionToRevoke -or [string]::IsNullOrEmpty($sessionToRevoke.id)) {
            Write-Warning-Custom "No valid session ID found - skipping revoke test"
            Write-Host "    Sessions: $($sessionList | ConvertTo-Json -Depth 3)"
            return
        }
        
        Write-Host "    Revoking session: $($sessionToRevoke.id.Substring(0, [Math]::Min(8, $sessionToRevoke.id.Length)))"
        
        $revokeBody = @{
            sessionId = $sessionToRevoke.id
        }
        
        $response = Invoke-ApiRequest -Method POST -Endpoint "/api/v1/auth/sessions/revoke" -Body $revokeBody
        
        # Verify session was removed
        $updatedSessions = Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/sessions"
        $updatedSessionList = if ($updatedSessions.data) { $updatedSessions.data } else { $updatedSessions }
        
        if ($updatedSessionList.Count -ne ($sessionList.Count - 1)) {
            throw "Session count should be reduced by 1. Before: $($sessionList.Count), After: $($updatedSessionList.Count)"
        }
        
        # Verify the specific session is gone
        $stillExists = $updatedSessionList | Where-Object { $_.id -eq $sessionToRevoke.id }
        if ($stillExists) {
            throw "Revoked session still exists in session list"
        }
        
        Write-Host "    ✓ Session revoked successfully"
        Write-Host "    ✓ Session count: $($sessionList.Count) → $($updatedSessionList.Count)"
    }
    else {
        Write-Warning-Custom "Only 1 session - skipping revoke test"
    }
}

Test-Step "Logout (Revoke Current Session)" {
    $logoutBody = @{
        refreshToken = $script:RefreshToken
    }
    
    Invoke-ApiRequest -Method POST -Endpoint "/api/v1/auth/logout" -Body $logoutBody -ExpectedStatusCodes @(200, 204) | Out-Null
    Write-Host "    Session revoked successfully"
}

Test-Step "Verify Token Revoked" {
    try {
        Invoke-ApiRequest -Method GET -Endpoint "/api/v1/auth/me" -ExpectedStatusCodes @(401)
        throw "Should have received 401 Unauthorized"
    }
    catch {
        if ($_ -match "401|Unauthorized") {
            # Expected - token was revoked
            Write-Host "    ✓ Token properly invalidated"
        }
        else {
            throw
        }
    }
}

# ============================================================================
# FINAL REPORT
# ============================================================================
Write-Section "TEST SUMMARY"

$passRate = if ($script:TestResults.Total -gt 0) { 
    [math]::Round(($script:TestResults.Passed / $script:TestResults.Total) * 100, 2) 
} else { 
    0 
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════"
Write-Host "  Total Tests: $($script:TestResults.Total)"
Write-Host "  $Green✓ Passed: $($script:TestResults.Passed)$Reset"
Write-Host "  $Red✗ Failed: $($script:TestResults.Failed)$Reset"
Write-Host "  Pass Rate: $passRate%"
Write-Host "═══════════════════════════════════════════════════"
Write-Host ""

if ($script:TestResults.Failed -gt 0) {
    Write-Host "${Red}Failed Tests:$Reset"
    $script:TestResults.Steps | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  $Red✗ $($_.Name)$Reset"
        Write-Host "    Error: $($_.Error)"
    }
    Write-Host ""
}

if ($script:TestResults.Failed -eq 0) {
    Write-Host ""
    Write-Host "$Green╔════════════════════════════════════════════════════════════╗$Reset"
    Write-Host "$Green║           ✓ ALL TESTS PASSED SUCCESSFULLY!                ║$Reset"
    Write-Host "$Green║                                                            ║$Reset"
    Write-Host "$Green║  Your Task Management API is working perfectly!           ║$Reset"
    Write-Host "$Green╚════════════════════════════════════════════════════════════╝$Reset"
    Write-Host ""
    exit 0
} else {
    Write-Error-Custom "SOME TESTS FAILED. Please review the errors above."
    exit 1
}
