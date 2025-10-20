#!/usr/bin/env pwsh
# =============================================================================
# Task Management API - Quick Setup Script
# =============================================================================
# This script sets up the environment and starts all services with Docker Compose
# =============================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Task Management API - Quick Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
Write-Host "Checking Docker..." -ForegroundColor Yellow
try {
    $null = docker ps 2>&1
    Write-Host "✓ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Create .env file if it doesn't exist
Write-Host ""
Write-Host "Setting up environment configuration..." -ForegroundColor Yellow

if (Test-Path ".env") {
    Write-Host "✓ .env file already exists" -ForegroundColor Green
    $response = Read-Host "Do you want to overwrite it with default values? (y/N)"
    if ($response -ne "y" -and $response -ne "Y") {
        Write-Host "Keeping existing .env file" -ForegroundColor Cyan
    } else {
        Copy-Item ".env.example" ".env" -Force
        Write-Host "✓ .env file updated with default values" -ForegroundColor Green
    }
} else {
    Copy-Item ".env.example" ".env"
    Write-Host "✓ .env file created from .env.example" -ForegroundColor Green
}

# Show what will be started
Write-Host ""
Write-Host "Starting services with Docker Compose..." -ForegroundColor Yellow
Write-Host "  • MongoDB (port 27017)" -ForegroundColor White
Write-Host "  • Redis (port 6379)" -ForegroundColor White
Write-Host "  • Jaeger (port 16686)" -ForegroundColor White
Write-Host "  • API (port 5000)" -ForegroundColor White
Write-Host ""

# Start Docker Compose
docker-compose up -d

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✓ All services started successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Services are starting up (this takes ~30-40 seconds)..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Once ready, you can access:" -ForegroundColor Cyan
    Write-Host "  • API:         http://localhost:5000" -ForegroundColor White
    Write-Host "  • Swagger UI:  http://localhost:5000/swagger" -ForegroundColor White
    Write-Host "  • Jaeger UI:   http://localhost:16686" -ForegroundColor White
    Write-Host "  • Health:      http://localhost:5000/health" -ForegroundColor White
    Write-Host ""
    Write-Host "View logs:       docker-compose logs -f" -ForegroundColor Gray
    Write-Host "Stop services:   docker-compose down" -ForegroundColor Gray
    Write-Host ""
    
    # Wait a moment and check health
    Write-Host "Waiting for services to be healthy..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    # Check if API is responding
    $maxAttempts = 12
    $attempt = 0
    $healthy = $false
    
    while ($attempt -lt $maxAttempts -and -not $healthy) {
        $attempt++
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $healthy = $true
                Write-Host "✓ API is healthy and ready!" -ForegroundColor Green
                Write-Host ""
                Write-Host "Try it now:" -ForegroundColor Cyan
                Write-Host "  Open browser: http://localhost:5000/swagger" -ForegroundColor White
                Write-Host ""
            }
        }
        catch {
            Write-Host "." -NoNewline -ForegroundColor Gray
            Start-Sleep -Seconds 5
        }
    }
    
    if (-not $healthy) {
        Write-Host ""
        Write-Host "⚠ Services are still starting up. Check logs with: docker-compose logs -f api" -ForegroundColor Yellow
    }
    
}
else {
    Write-Host ""
    Write-Host "✗ Failed to start services" -ForegroundColor Red
    Write-Host "Check Docker logs with: docker-compose logs" -ForegroundColor Yellow
    exit 1
}
