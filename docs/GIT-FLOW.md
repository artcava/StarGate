# Git Flow - StarGate Development Model

## Overview

StarGate uses **Git Flow** branching model for version control and release management.

```
┌──────────────────────────────────────────┐
│        StarGate Git Flow Model          │
├──────────────────────────────────────────┤
│                                          │
│  main (production)  ──────────────────── │
│       ↑                                  │
│       │ (tag v0.1.0, v0.2.0, ...)        │
│       │ (PR + status checks required)    │
│       │                                  │
│  develop (integration)  ──────────────── │
│       ↑                                  │
│       │ (PR + status checks required)    │
│    ┌──┴──┐                               │
│    │     │                               │
│  feature/*  bugfix/*  docs/*  release/*  │
│  (feature development)                   │
│                                          │
└──────────────────────────────────────────┘
```

---

## Branch Types

### 1. `main` - Production Branch
- **Purpose:** Stable, production-ready code
- **Protected:** ✅ Yes
- **Merge from:** `release/*` branches only
- **Requires:** 
  - Pull Request ✅
  - Status checks (build + test) ✅
  - At least 1 approval ❌ (skipped for docs)
- **Tagging:** Every merge gets a version tag (v0.1.0, v0.2.0, etc.)
- **Release:** Automatic release workflow on tag push

**Protected Rule on GitHub:**
```
✅ Require pull request reviews
✅ Require status checks to pass
✅ Require branches up to date
✅ Require code owner reviews
✅ Allow force push: NO
```

---

### 2. `develop` - Integration Branch
- **Purpose:** Integration branch for feature development
- **Protected:** ✅ Yes
- **Merge from:** `feature/*`, `bugfix/*`, `release/*` branches
- **Requires:**
  - Pull Request ✅
  - Status checks (build + test) ✅
- **Release:** Automatic test/build on each merge

**Protected Rule on GitHub:**
```
✅ Require pull request reviews
✅ Require status checks to pass
✅ Require branches up to date
✅ Allow force push: NO
```

---

### 3. `feature/*` - Feature Branches
- **Naming:** `feature/short-description` or `feature/ISSUE-123-description`
- **Branch from:** `develop`
- **Merge back to:** `develop` (via Pull Request)
- **Naming examples:**
  - `feature/user-authentication`
  - `feature/database-integration`
  - `feature/ISSUE-45-api-validation`

**Workflow:**
```bash
# 1. Create feature branch from develop
git checkout develop
git pull origin develop
git checkout -b feature/my-feature

# 2. Make changes and commit
git add .
git commit -m "feat: description of feature"

# 3. Push to GitHub
git push -u origin feature/my-feature

# 4. Create Pull Request on GitHub
#    - Base: develop
#    - Compare: feature/my-feature
#    - Wait for status checks (build + test) to pass
#    - Get approvals if required
#    - Merge

# 5. Delete branch after merge
git branch -d feature/my-feature
git push origin --delete feature/my-feature
```

---

### 4. `bugfix/*` - Bugfix Branches
- **Naming:** `bugfix/short-description` or `bugfix/ISSUE-123-description`
- **Branch from:** `develop`
- **Merge back to:** `develop` (via Pull Request)
- **Priority:** Higher than regular features

**Example:**
```bash
git checkout develop
git checkout -b bugfix/api-timeout-issue
# Fix bug
git commit -m "fix: resolve API timeout issue"
git push -u origin bugfix/api-timeout-issue
# Create PR to develop
```

---

### 5. `release/*` - Release Branches
- **Naming:** `release/X.X.X` (e.g., `release/0.1.0`, `release/0.2.0`)
- **Branch from:** `develop`
- **Merge to:** `main` AND `develop`
- **Purpose:** Release preparation, version bumping, final testing
- **Automatic merge:** NO (manual review required)

**Release Workflow (Maintainer Only):**
```bash
# 1. Create release branch
git checkout develop
git checkout -b release/0.2.0

# 2. Update version in CSPROJ
# Edit src/StarGate.App/StarGate.App.csproj
# Set <Version>0.2.0</Version>

git commit -m "chore: bump version to 0.2.0"
git push -u origin release/0.2.0

# 3. Create PR to main
# Base: main
# Compare: release/0.2.0
# Wait for status checks
# Merge to main

# 4. Create PR back to develop
# This ensures develop gets version bump too

# 5. Tag on main
git checkout main
git pull origin main
git tag -a v0.2.0 -m "Release v0.2.0"
git push origin v0.2.0

# 6. Workflow automatically creates release
# GitHub Actions ci.yml triggers
# Publishes release
```

---

### 6. `docs/*` - Documentation Branches
- **Naming:** `docs/topic-name` (e.g., `docs/database-setup`, `docs/deployment`)
- **Branch from:** `main`
- **Merge back to:** `main` (via Pull Request)
- **Status checks:** ❌ SKIPPED (no build/test required)
- **Review required:** ❌ NO (auto-merge if OK)
- **Purpose:** Documentation updates without affecting code

**Documentation Workflow:**
```bash
# 1. Create docs branch from main
git checkout main
git pull origin main
git checkout -b docs/database-guide

# 2. Edit markdown files
# Add/update docs/ files

git add .
git commit -m "docs: add database setup guide"
git push -u origin docs/database-guide

# 3. Create PR to main
# No status checks needed!
# Can merge immediately if content is OK
```

---

## Commit Message Convention

Follow **Conventional Commits** format:

```
<type>: <subject>
<blank line>
<body>
<blank line>
<footer>
```

### Types
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `ci`: CI/CD pipeline changes
- `chore`: Build, dependencies, version bumping
- `refactor`: Code refactoring without feature change
- `test`: Test additions or changes
- `perf`: Performance improvements

### Examples
```bash
git commit -m "feat: add user authentication module"
git commit -m "fix: resolve database connection timeout"
git commit -m "docs: add installation guide"
git commit -m "chore: bump version to 0.2.0"
git commit -m "test: add unit tests for AuthService"
```

---

## Version Tagging

Versions follow **Semantic Versioning (SemVer)**: `MAJOR.MINOR.PATCH`

- `0.1.0` - Initial MVP
- `0.1.1` - Bugfix
- `0.2.0` - New features
- `1.0.0` - First stable release

**Tagged on:** `main` branch only, after release merge

```bash
# Create tag
git tag -a v0.1.0 -m "Initial release"

# Push tag
git push origin v0.1.0

# Release workflow automatically triggers
```

---

## Protected Branch Rules Summary

| Branch | Require PR | Status Checks | Reviews | Force Push |
|--------|-----------|---------------|---------|------------|
| `main` | ✅ Yes | ✅ Yes (build+test) | ✅ Yes | ❌ No |
| `develop` | ✅ Yes | ✅ Yes (build+test) | ❌ No | ❌ No |
| `feature/*` | ❌ No (local) | ✅ Yes (on PR) | ❌ No | ✅ Yes |
| `bugfix/*` | ❌ No (local) | ✅ Yes (on PR) | ❌ No | ✅ Yes |
| `docs/*` | ✅ Yes | ❌ No | ❌ No | ❌ No |
| `release/*` | ✅ Yes | ✅ Yes (on PR) | ✅ Yes | ❌ No |

---

## CI/CD Pipeline

The project uses a **unified GitHub Actions workflow** (`ci.yml`) that handles all CI/CD aspects: build, test, and release.

### Unified Workflow: `ci.yml`

**Location:** `.github/workflows/ci.yml`

**Features:**
- **Build & Test**: Automatic compilation and testing on every push/PR
- **Code Quality**: Formatting and analyzer verification
- **Release**: Automatic release creation on tag push

### Trigger Events

| Event | Branches | Jobs Executed |
|-------|----------|---------------|
| **Push** | `main`, `develop` | Build → Test → Quality |
| **Pull Request** | `main`, `develop` | Build → Test → Quality |
| **Push Tag** | `v*` (e.g., `v0.1.0`) | Build → Test → Release |

### Workflow Jobs

#### 1. **Build Job**
```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - Checkout code
      - Setup .NET SDK
      - Restore dependencies
      - Build project
      - Upload artifacts
```

**Goal:** Verify that code compiles correctly.

**Output:** Artifacts ready for testing and release.

---

#### 2. **Test Job**
```yaml
  test:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - Checkout code
      - Setup .NET SDK
      - Restore dependencies
      - Run unit tests
      - Generate code coverage report
      - Verify minimum threshold (70%)
```

**Goal:** Execute all unit tests and verify code coverage.

**Requirements:**
- ✅ All tests must pass
- ✅ Code coverage ≥ 70%

**Failure:** PR blocked if tests fail or coverage < 70%.

---

#### 3. **Quality Job**
```yaml
  quality:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - Checkout code
      - Setup .NET SDK
      - Restore dependencies
      - Verify formatting (dotnet format)
      - Run analyzers
      - Check warnings
```

**Goal:** Ensure code quality and consistency.

**Checks:**
- ✅ Code properly formatted (`.editorconfig`)
- ✅ No analyzer violations
- ✅ No critical warnings

**Failure:** PR blocked if there are formatting or analyzer violations.

---

#### 4. **Release Job** (Tag Only)
```yaml
  release:
    needs: [build, test, quality]
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    steps:
      - Checkout code
      - Setup .NET SDK
      - Build Release configuration
      - Publish GitHub Release
      - Upload release artifacts
```

**Goal:** Automatically create and publish a new release.

**Trigger:** Push of a tag with format `v*` (e.g., `v0.1.0`, `v1.2.3`).

**Output:**
- 📦 GitHub Release with build artifacts
- 📝 Automatic release notes
- 🔗 Download links for users

---

### Status Checks for Pull Requests

Before merging a PR to `main` or `develop`, the following must pass:

✅ **Build Job** - Compilation without errors  
✅ **Test Job** - All tests pass + coverage ≥ 70%  
✅ **Quality Job** - Code formatted + no analyzer violations

**Example PR status:**
```
✅ ci / build (pull_request)           — Passed in 2m 34s
✅ ci / test (pull_request)            — Passed in 1m 45s
✅ ci / quality (pull_request)         — Passed in 1m 12s
```

---

### Complete Release Workflow

**Step 1:** Merge `release/X.X.X` into `main`
```bash
# After completing PR and merge
git checkout main
git pull origin main
```

**Step 2:** Create and push tag
```bash
git tag -a v0.2.0 -m "Release v0.2.0: Feature X, Fix Y"
git push origin v0.2.0
```

**Step 3:** Automatic workflow
```
ci.yml triggered by tag push v0.2.0
  ↓
✅ Build job completes
  ↓
✅ Test job completes (coverage OK)
  ↓
✅ Quality job completes (no violations)
  ↓
✅ Release job starts
  ↓
🚀 Publishes GitHub Release
  ↓
✅ Release v0.2.0 published!
```

**Step 4:** Verify on GitHub
- Go to: `https://github.com/[user]/[repo]/releases`
- Find release `v0.2.0`
- Release artifacts available for download

---

### Branch Protection Configuration

To enable automatic checks, configure on GitHub:

**Settings → Branches → Branch protection rules → `main` / `develop`**

```
☑️ Require status checks to pass before merging
  ☑️ Require branches to be up to date before merging
  
  Status checks that are required:
    ☑️ ci / build
    ☑️ ci / test
    ☑️ ci / quality

☑️ Require pull request reviews before merging
  • Required approvals: 1 (for main)
  • Required approvals: 0 (for develop)

☑️ Do not allow bypassing the above settings
```

---

### Workflow Debugging

If the workflow fails:

**1. Check logs on GitHub Actions:**
```
Repository → Actions → ci → Click on failed run → Expand job
```

**2. Test locally:**
```bash
# Simulate build job
dotnet restore
dotnet build --configuration Release

# Simulate test job
dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

# Simulate quality job
dotnet format --verify-no-changes
dotnet build /p:TreatWarningsAsErrors=true
```

**3. Common errors:**

| Error | Cause | Solution |
|--------|-------|----------|
| Build fails | Compilation errors | Fix code errors |
| Test fails | Unit tests failed | Fix tests or code |
| Coverage < 70% | Insufficient code coverage | Add more tests |
| Format check fails | Code not formatted | Run `dotnet format` |
| Analyzer warnings | Analyzer rule violations | Fix violations or suppress if justified |

---

### Benefits of Unified Workflow

✅ **Simplicity:** Single file to maintain instead of 3  
✅ **Consistency:** All jobs share the same configuration  
✅ **Efficiency:** Artifact reuse between jobs (caching)  
✅ **Visibility:** Clear and centralized status checks  
✅ **Maintainability:** Changes in a single place  
