#!/bin/bash
# machine1-setup.sh - Analytics Server Setup Script

# Color codes for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Default values
BUILD_ONLY=false
PROJECT_DIR="/opt/fall-detection-analytics"
PROJECT_NAME="FallDetection.Analytics"
SERVICE_NAME="fall-detection-analytics"
PORT=5000
DISTRO="auto"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_PROJECT_DIR="$SCRIPT_DIR/$PROJECT_NAME"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --build-only)
      BUILD_ONLY=true
      shift
      ;;
    --project-dir)
      PROJECT_DIR="$2"
      shift 2
      ;;
    --distro)
      DISTRO="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--build-only] [--project-dir /path/to/project] [--distro ubuntu|debian]"
      exit 1
      ;;
  esac
done

if [[ "$DISTRO" == "auto" ]]; then
  if [[ -r /etc/os-release ]]; then
    . /etc/os-release
    DISTRO="${ID:-auto}"
  fi
fi

DOTNET_CMD="$(command -v dotnet || true)"
if [[ -z "$DOTNET_CMD" ]]; then
  DOTNET_CMD="/usr/bin/dotnet"
fi

if [[ "$DISTRO" == "debian" ]]; then
  DOTNET_REPO_URL="https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb"
elif [[ "$DISTRO" == "ubuntu" ]]; then
  DOTNET_REPO_URL="https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb"
else
  echo -e "${RED}Error: Unsupported distro '$DISTRO'. Use --distro ubuntu or --distro debian.${NC}"
  exit 1
fi

echo -e "${GREEN}=== Fall Detection Analytics Server Setup ===${NC}"

if [ "$BUILD_ONLY" = false ]; then
  echo -e "${YELLOW}Step 1: Installing system dependencies...${NC}"
  
  # Update system
  sudo apt-get update
  sudo apt-get upgrade -y
  
  # Install .NET 8 SDK
  echo -e "${YELLOW}Installing .NET 8 SDK...${NC}"
  wget "$DOTNET_REPO_URL" -O packages-microsoft-prod.deb
  sudo dpkg -i packages-microsoft-prod.deb
  rm packages-microsoft-prod.deb
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-8.0
  
  # Install additional dependencies
  sudo apt-get install -y nginx certbot python3-certbot-nginx
  
  echo -e "${YELLOW}Step 2: Creating project structure...${NC}"
  
  # Create project directory
  sudo mkdir -p $PROJECT_DIR
  sudo chown -R $USER:$USER $PROJECT_DIR
  
  if [ ! -f "$SOURCE_PROJECT_DIR/FallDetection.Analytics.csproj" ]; then
    echo -e "${RED}Error: Source project not found at $SOURCE_PROJECT_DIR${NC}"
    exit 1
  fi

  echo -e "${YELLOW}Copying application source into $PROJECT_DIR...${NC}"
  rm -rf "$PROJECT_DIR/$PROJECT_NAME"
  cp -a "$SOURCE_PROJECT_DIR" "$PROJECT_DIR/"

  # Navigate into project
  cd "$PROJECT_DIR/$PROJECT_NAME"
  
  echo -e "${YELLOW}Step 3: Configuring firewall...${NC}"
  sudo ufw allow $PORT/tcp
  sudo ufw allow ssh
  sudo ufw --force enable
  
  echo -e "${YELLOW}Step 4: Creating data directories...${NC}"
  sudo mkdir -p /var/lib/fall-detection/analytics
  sudo mkdir -p /var/log/fall-detection/analytics
  sudo chown -R $USER:$USER /var/lib/fall-detection /var/log/fall-detection
  
  echo -e "${YELLOW}Step 5: Creating systemd service...${NC}"
  sudo tee /etc/systemd/system/$SERVICE_NAME.service > /dev/null <<EOF
[Unit]
Description=Fall Detection Analytics Server
After=network.target

[Service]
Type=exec
User=$USER
WorkingDirectory=$PROJECT_DIR/$PROJECT_NAME
ExecStart=$DOTNET_CMD run --urls http://0.0.0.0:$PORT
Restart=always
RestartSec=10
KillSignal=SIGINT
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="DOTNET_PRINT_TELEMETRY_MESSAGE=false"

[Install]
WantedBy=multi-user.target
EOF
  
  sudo systemctl daemon-reload
else
  echo -e "${YELLOW}[BUILD-ONLY MODE] Skipping system setup, going directly to build...${NC}"
  
  # Navigate to project directory
  echo "Navigating to project directory: $PROJECT_DIR/$PROJECT_NAME"
  cd "$PROJECT_DIR/$PROJECT_NAME" || {
    echo -e "${RED}Error: Could not navigate to $PROJECT_DIR/$PROJECT_NAME${NC}"
    echo "Make sure the project exists or specify --project-dir with the correct path"
    exit 1
  }
  
  # Verify we're in the project directory
  if [ ! -f "FallDetection.Analytics.csproj" ]; then
    echo -e "${RED}Error: Not in project directory. FallDetection.Analytics.csproj not found${NC}"
    echo "Current directory: $(pwd)"
    exit 1
  fi
fi

echo -e "${YELLOW}Step 6: Building and publishing the project...${NC}"
echo "Current directory: $(pwd)"

# Restore NuGet packages
echo "Restoring NuGet packages..."
dotnet restore

# Build the project
echo "Building project..."
dotnet build --configuration Release --no-restore

echo "Backing up ./publish/Data/ directory to ./backup/Data/..."
if [ -d "./Data" ]; then
  mkdir -p ./backup/Data/
  cp -r ./publish/Data/* ./backup/Data/
  echo "Data directory backed up"
fi

# Clean and publish the project
echo "Cleaning existing publish directory..."
rm -rf ./publish

echo "Publishing project..."
dotnet publish --configuration Release --no-build --output ./publish

echo "Restoring ./backup/Data/ to ./publish/Data/..."
if [ -d "./Data" ]; then
  mkdir -p ./backup/Data/
  cp -r ./backup/Data/* ./publish/Data/
  echo -e "${GREEN}Data directory restored"
fi

echo -e "${YELLOW}Step 7: Updating systemd service for published app...${NC}"
sudo tee /etc/systemd/system/$SERVICE_NAME.service > /dev/null <<EOF
[Unit]
Description=Fall Detection Analytics Server
After=network.target

[Service]
Type=exec
User=$USER
WorkingDirectory=$PROJECT_DIR/$PROJECT_NAME/publish
ExecStart=$DOTNET_CMD FallDetection.Analytics.dll --urls http://0.0.0.0:$PORT
Restart=always
RestartSec=10
KillSignal=SIGINT
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="DOTNET_PRINT_TELEMETRY_MESSAGE=false"

[Install]
WantedBy=multi-user.target
EOF

echo -e "${YELLOW}Step 8: Reloading systemd and restarting service...${NC}"
sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME

if sudo systemctl is-active --quiet $SERVICE_NAME; then
  echo "Service is running. Restarting $SERVICE_NAME..."
  sudo systemctl restart $SERVICE_NAME
else
  echo "Service is not running. Starting $SERVICE_NAME..."
  sudo systemctl start $SERVICE_NAME
fi

if [ "$BUILD_ONLY" = false ]; then
  echo -e "${YELLOW}Step 9: Verifying service status...${NC}"
  sudo systemctl status $SERVICE_NAME --no-pager
  
  # Test the API
  echo -e "${YELLOW}Step 10: Testing API endpoint...${NC}"
  sleep 5
  curl -f http://localhost:$PORT/api/analytics/health || echo -e "${RED}API health check failed${NC}"
  
  echo -e "${GREEN}=== Setup Complete! ===${NC}"
  echo -e "Analytics Server is running on: http://$(hostname -I | awk '{print $1}'):$PORT"
  echo -e "Service status: sudo systemctl status $SERVICE_NAME"
  echo -e "Logs: sudo journalctl -u $SERVICE_NAME -f"
else
  echo -e "${GREEN}=== Build Complete! ===${NC}"
  echo -e "Project published to: $PROJECT_DIR/$PROJECT_NAME/publish"
  echo -e "Current directory: $(pwd)"
  echo -e "Service reloaded and started: $SERVICE_NAME"
  echo -e "Service status: sudo systemctl status $SERVICE_NAME"
fi