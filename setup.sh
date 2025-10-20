#!/bin/bash
# =============================================================================
# Task Management API - Quick Setup Script (Linux/macOS)
# =============================================================================
# This script sets up the environment and starts all services with Docker Compose
# =============================================================================

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================"
echo -e "Task Management API - Quick Setup"
echo -e "========================================${NC}"
echo ""

# Check if Docker is running
echo -e "${YELLOW}Checking Docker...${NC}"
if ! docker ps &> /dev/null; then
    echo -e "${RED}✗ Docker is not running. Please start Docker first.${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Docker is running${NC}"

# Create .env file if it doesn't exist
echo ""
echo -e "${YELLOW}Setting up environment configuration...${NC}"

if [ -f ".env" ]; then
    echo -e "${GREEN}✓ .env file already exists${NC}"
    read -p "Do you want to overwrite it with default values? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        cp .env.example .env
        echo -e "${GREEN}✓ .env file updated with default values${NC}"
    else
        echo -e "${CYAN}Keeping existing .env file${NC}"
    fi
else
    cp .env.example .env
    echo -e "${GREEN}✓ .env file created from .env.example${NC}"
fi

# Show what will be started
echo ""
echo -e "${YELLOW}Starting services with Docker Compose...${NC}"
echo -e "  • MongoDB (port 27017)"
echo -e "  • Redis (port 6379)"
echo -e "  • Jaeger (port 16686)"
echo -e "  • API (port 5000)"
echo ""

# Start Docker Compose
docker-compose up -d

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}========================================"
    echo -e "✓ All services started successfully!"
    echo -e "========================================${NC}"
    echo ""
    echo -e "${YELLOW}Services are starting up (this takes ~30-40 seconds)...${NC}"
    echo ""
    echo -e "${CYAN}Once ready, you can access:${NC}"
    echo -e "  • API:         http://localhost:5000"
    echo -e "  • Swagger UI:  http://localhost:5000/swagger"
    echo -e "  • Jaeger UI:   http://localhost:16686"
    echo -e "  • Health:      http://localhost:5000/health"
    echo ""
    echo -e "${GRAY}View logs:       docker-compose logs -f"
    echo -e "Stop services:   docker-compose down${NC}"
    echo ""
    
    # Wait a moment and check health
    echo -e "${YELLOW}Waiting for services to be healthy...${NC}"
    sleep 5
    
    # Check if API is responding
    max_attempts=12
    attempt=0
    healthy=false
    
    while [ $attempt -lt $max_attempts ] && [ "$healthy" = false ]; do
        attempt=$((attempt + 1))
        if curl -s -f http://localhost:5000/health > /dev/null 2>&1; then
            healthy=true
            echo -e "${GREEN}✓ API is healthy and ready!${NC}"
            echo ""
            echo -e "${CYAN}Try it now:${NC}"
            echo -e "  Open browser: http://localhost:5000/swagger"
            echo ""
        else
            echo -n "."
            sleep 5
        fi
    done
    
    if [ "$healthy" = false ]; then
        echo ""
        echo -e "${YELLOW}⚠ Services are still starting up. Check logs with: docker-compose logs -f api${NC}"
    fi
    
else
    echo ""
    echo -e "${RED}✗ Failed to start services${NC}"
    echo -e "${YELLOW}Check Docker logs with: docker-compose logs${NC}"
    exit 1
fi
