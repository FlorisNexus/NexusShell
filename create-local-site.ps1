# --- Gemini CLI: Local Site Scaffolder ---
# This script automates the creation of a new FlorisNexus local project.

$reposDir = "C:\Users\flori\source\repos"
$siteName = Read-Host "Enter the name of the new local site"
$sitePath = Join-Path $reposDir $siteName

# 1. Create Vite Project (React + TS)
Write-Host "🚀 Creating Vite project for $siteName..." -ForegroundColor Cyan
cd $reposDir
npm create vite@latest $siteName -- --template react-ts

# 2. Install Tailwind & Base Deps
cd $sitePath
Write-Host "📦 Installing Tailwind CSS and dependencies..." -ForegroundColor Cyan
npm install tailwindcss postcss autoprefixer
npx tailwindcss init -p

# 3. Create Project Dashboard (index.md)
Write-Host "📝 Creating Project Dashboard (index.md)..." -ForegroundColor Cyan
$indexContent = @"
# Project Index: $siteName

## 🎯 Project Overview
- **Client:** [Enter Client Name]
- **Type:** Showcase Site (SPA)
- **Status:** Initial Scaffolding

## 🛠️ Tech Stack
- Frontend: Vite + React + TS
- Styling: Tailwind CSS
- Hosting: Azure Static Web Apps

## 📅 Roadmap
- [ ] Requirements gathering
- [ ] UI Design / Wireframe
- [ ] Development
- [ ] Deployment to Azure
"@
Set-Content -Path "index.md" -Value $indexContent -Encoding UTF8

# 4. Create Project Context (GEMINI.md)
Write-Host "📝 Creating Project Context (GEMINI.md)..." -ForegroundColor Cyan
$geminiMd = @"
# 🏢 Project: $siteName

This is a **Local Showcase Site** for FlorisNexus.
Focus on modern, clean, mobile-first design using Tailwind.
Host on Azure Static Web Apps.
"@
Set-Content -Path "GEMINI.md" -Value $geminiMd -Encoding UTF8

# 5. Initialize Git
Write-Host "🐙 Initializing Git repository..." -ForegroundColor Cyan
git init
git add .
git commit -m "Initial scaffold: Vite + Tailwind + Conductor Dashboard"

Write-Host "✅ Site $siteName created successfully at $sitePath!" -ForegroundColor Green
Write-Host "Next steps: cd $siteName; npm install; npm run dev"
