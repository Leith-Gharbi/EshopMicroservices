#!/usr/bin/env pwsh
# Git prepare-commit-msg hook for Windows
# Generates commit message using Claude Code

param(
    [Parameter(Position=0)]
    [string]$CommitMsgFile,
    
    [Parameter(Position=1)]
    [string]$CommitSource
)

# Skip if commit message already provided (amend, merge, squash, etc.)
if ($CommitSource -ne "") {
    exit 0
}

# Skip if not interactive terminal
if (-not [Environment]::UserInteractive) {
    exit 0
}

# Check if Claude Code CLI is available
$claudeCmd = Get-Command claude -ErrorAction SilentlyContinue
if (-not $claudeCmd) {
    Write-Host "⚠️  Claude Code CLI not found. Install it or write commit manually." -ForegroundColor Yellow
    exit 0
}

# Check for staged changes
$stagedFiles = git diff --cached --name-only 2>$null

if ([string]::IsNullOrWhiteSpace($stagedFiles)) {
    Write-Host "❌ No staged changes. Use 'git add <files>' first." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🤖 Generating commit message with Claude Code..." -ForegroundColor Cyan
Write-Host ""

# Build the prompt
$prompt = @"
You are a git commit message generator. Analyze the staged changes and generate a Conventional Commits message.

First, run this command to see staged changes:
git diff --cached

Then follow these rules:
1. Detect scope from file paths:
   - ApiGateways/ → gateway
   - BuildingBlocks/ → building-blocks
   - Services/Basket/ → basket
   - Services/Catalog/ → catalog
   - Services/Discount/ → discount
   - Services/Ordering/ → ordering
   - WebApps/ → webapp
   - docker-compose*, .github/ → infra
   - Multiple areas → repo

2. Detect type from changes:
   - New features → feat
   - Bug fixes → fix
   - Only docs → docs
   - Only tests → test
   - Dependencies → build
   - Refactoring → refactor

3. Format: type(scope): description (max 50 chars, imperative mood, lowercase)

Output ONLY the commit message, nothing else. No explanations, no markdown, just the message.
"@

try {
    # Call Claude Code
    $generatedMsg = claude --print $prompt 2>$null
    
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($generatedMsg)) {
        # Clean up the message (remove any extra whitespace/newlines)
        $generatedMsg = $generatedMsg.Trim()
        
        # Write to commit message file
        Set-Content -Path $CommitMsgFile -Value $generatedMsg -NoNewline -Encoding UTF8
        
        Write-Host "📝 Generated commit message:" -ForegroundColor Green
        Write-Host "─────────────────────────────────────────────────" -ForegroundColor DarkGray
        Write-Host $generatedMsg -ForegroundColor White
        Write-Host "─────────────────────────────────────────────────" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "📋 Your editor will open to review/edit the message..." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "⚠️  Could not generate message. Write manually." -ForegroundColor Yellow
    }
}
catch {
    Write-Host "⚠️  Error calling Claude: $_" -ForegroundColor Yellow
}

exit 0