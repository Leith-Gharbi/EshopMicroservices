# Git Commit Generator Skill

## Purpose
Generate high-quality Git commit messages following Conventional Commits specification for a .NET 8 eShop microservices monorepo.

## Trigger
This skill activates when:
- User asks to generate a commit message
- User types `/commit` or asks to commit changes
- Called from git prepare-commit-msg hook

## Project Structure
```
├── ApiGateways/YarpApiGateway/     → gateway
├── BuildingBlocks/                  → building-blocks
│   ├── BuildingBlocks/
│   └── BuildingBlocks.Messaging/
├── Services/
│   ├── Basket/                      → basket
│   ├── Catalog/                     → catalog
│   ├── Discount/                    → discount
│   └── Ordering/                    → ordering
│       ├── Ordering.API/
│       ├── Ordering.Application/
│       ├── Ordering.Domain/
│       └── Ordering.Infrastructure/
├── WebApps/                         → webapp
└── docker-compose                   → infra
```

## Scope Detection Rules
| Path Pattern | Scope |
|--------------|-------|
| `ApiGateways/` | `gateway` |
| `BuildingBlocks/` | `building-blocks` |
| `Services/Basket/` | `basket` |
| `Services/Catalog/` | `catalog` |
| `Services/Discount/` | `discount` |
| `Services/Ordering/` | `ordering` |
| `WebApps/` | `webapp` |
| `docker-compose*`, `.github/`, `*.yml` (root) | `infra` |
| `.sln`, `Directory.Build.props`, `global.json` | `repo` |
| Multiple services equally | `repo` or ASK user |

## Conventional Commits Format
```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

### Types
| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Code style (formatting) |
| `refactor` | Code refactoring |
| `perf` | Performance improvement |
| `test` | Adding/updating tests |
| `build` | Build system or dependencies |
| `ci` | CI/CD configuration |
| `chore` | Maintenance tasks |
| `revert` | Revert a previous commit |

### Breaking Changes
- Add `!` after scope: `feat(basket)!: change cart structure`
- Add `BREAKING CHANGE:` footer with migration details

## Workflow

### Step 1: Analyze Staged Changes
Run these commands and analyze output:
```bash
git diff --cached --stat
git diff --cached --name-only
git diff --cached
```

### Step 2: Determine Scope
1. List all changed file paths
2. Match paths to scope rules above
3. Single area → use that scope
4. Multiple areas → use `repo` or ASK user

### Step 3: Determine Type
- New files with features → `feat`
- Bug fixes → `fix`
- Only `*.md` files → `docs`
- Only `*Tests.cs` → `test`
- Only `.csproj` changes → `build`
- If ambiguous → ASK user

### Step 4: Generate Description
- Imperative mood: "add" not "added"
- Max 50 characters
- Lowercase first letter
- No period at end

### Step 5: Generate Body (if needed)
Add body for:
- Complex changes
- Multiple related modifications
- Breaking changes

### Step 6: Present to User
```
📝 Proposed commit message:
═══════════════════════════════════════════════════

feat(catalog): add product search endpoint

Add full-text search capability for products.

═══════════════════════════════════════════════════

📁 Files changed:
   • Services/Catalog/Catalog.API/Endpoints/SearchEndpoint.cs (new)

🎯 Detected: scope=catalog, type=feat

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[1] ✅ Use this message
[2] ✏️  Edit message  
[3] 🔄 Regenerate (change type/scope)
[4] ❌ Cancel
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

## Interactive Mode - When to Ask
1. Changes in multiple service areas equally
2. Type could be `feat` or `refactor`
3. More than 10 files changed
4. Breaking change detected

## Examples

### Simple Feature
```
+ Services/Catalog/Catalog.API/Endpoints/GetProductById.cs
```
→ `feat(catalog): add get product by id endpoint`

### Bug Fix
```
M Services/Ordering/Ordering.Application/Orders/Commands/CreateOrderHandler.cs
```
→ `fix(ordering): handle null shipping address in order creation`

### Shared Code
```
M BuildingBlocks/BuildingBlocks.Messaging/Events/IntegrationEvent.cs
```
→ `refactor(building-blocks): add correlation id to integration events`

### Infrastructure
```
M docker-compose.yml
+ .github/workflows/ci.yml
```
→ `ci(infra): add GitHub Actions workflow`

### Breaking Change
```
- public string? CouponCode { get; set; }
```
→ 
```
refactor(basket)!: remove coupon code from shopping cart

BREAKING CHANGE: CouponCode property removed.
Migration: Use Discount service for coupons.
```

## Hook Output Mode
When called for hook (non-interactive), output ONLY the raw message:
```
feat(catalog): add product search endpoint
```