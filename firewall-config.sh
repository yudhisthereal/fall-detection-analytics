#!/bin/bash

# firewall-config.sh - Configure firewalls

echo "Configuring firewalls..."

sudo ufw allow ssh
sudo ufw allow 22

# Machine 1 (Analytics Server - run on 103.127.136.213)
echo "=== Machine 1 (Analytics Server) ==="
echo "Opening port 5000 for analytics API"
sudo ufw allow 5000/tcp
sudo ufw --force enable
sudo ufw status

echo "Firewall configuration complete!"