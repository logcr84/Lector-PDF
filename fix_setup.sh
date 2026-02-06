#!/bin/bash
echo "Fixing Permissions..."

# Fix npm cache permissions
sudo chown -R $(whoami) ~/.npm 2>/dev/null || echo "Could not fix ~/.npm (might not exist or sudo failed)"

# Fix Angular config permissions
sudo chown $(whoami) ~/.angular-config.json 2>/dev/null

# Attempt to fix temp dir permissions (careful here, but usually safe for user's own files)
# sudo chown -R $(whoami) /var/folders/sd/z400ksc902j5s_ln70r5w5n00000gn/T/ 2>/dev/null

echo "Restoring Backend..."
cd /Users/jalfaro/Lector\ PDF/Backend
dotnet restore
dotnet build

echo "Creating Frontend..."
cd /Users/jalfaro/Lector\ PDF
rm -rf Frontend
npx -y @angular/cli new Frontend --minimal --style css --routing false --skip-git

echo "Done! If this fails, please run commands manually with sudo."
