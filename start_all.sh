#!/bin/bash

# Define temp directories to bypass permission issues
export TMP_BASE="$(pwd)/tmp"
mkdir -p "$TMP_BASE"
export TMPDIR="$TMP_BASE"
export HOME="$TMP_BASE"

# Backend Variables
export NUGET_SCRATCH="$TMP_BASE/nuget"
export DOTNET_CLI_HOME="$TMP_BASE/dotnet"

# Frontend Variables
export NPM_CONFIG_CACHE="$TMP_BASE/npm-cache"
export NPM_CONFIG_USERCONFIG="$TMP_BASE/npm-config/npmrc"

# Kill existing processes on ports (optional, to be safe)
lsof -ti:5033 | xargs kill -9 2>/dev/null
lsof -ti:4200 | xargs kill -9 2>/dev/null

echo "Starting Backend..."
cd Backend
nohup dotnet watch run /p:UseAppHost=false > ../backend_run.log 2>&1 &
BACKEND_PID=$!
echo "Backend started (PID: $BACKEND_PID). Logs in backend_run.log"

cd ..

echo "Starting Frontend..."
cd Frontend
nohup npm start > ../frontend_run.log 2>&1 &
FRONTEND_PID=$!
echo "Frontend started (PID: $FRONTEND_PID). Logs in frontend_run.log"

echo "Services are starting..."
echo "Backend will be at: http://localhost:5033"
echo "Frontend will be at: http://localhost:4200 (Give it ~30 seconds to compile)"
