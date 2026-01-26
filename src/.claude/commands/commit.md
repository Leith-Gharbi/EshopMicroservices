# /commit - Generate Git Commit Message

Generate a Conventional Commits message for staged changes.

## Instructions

1. **Check staged changes:**
```bash
git diff --cached --name-only
```

If empty, tell user: "No staged changes. Use `git add <files>` first."

2. **Analyze the diff:**
```bash
git diff --cached
```

3. **Apply the git-commit skill** from `.claude/skills/git-commit/SKILL.md`:
   - Detect scope from file paths
   - Detect type from changes
   - Generate description (imperative, max 50 chars)

4. **Present the message** with formatted output:
```
📝 Proposed commit message:
═══════════════════════════════════════════════════

<generated message here>

═══════════════════════════════════════════════════

📁 Changed files:
   • file1.cs
   • file2.cs

🎯 Detected: scope=<scope>, type=<type>

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[1] ✅ Use this message
[2] ✏️  Edit - tell me what to change
[3] 🔄 Regenerate with different type/scope
[4] ❌ Cancel
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

5. **Handle user choice:**
   - **1**: Execute `git commit -m "<message>"`
   - **2**: Ask what to change, regenerate
   - **3**: Ask for preferred type/scope, regenerate
   - **4**: Cancel, do nothing

6. **For multi-line commits** (with body):
```bash
git commit -m "<title>" -m "<body>"
```