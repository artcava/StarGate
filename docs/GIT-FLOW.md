# Git Flow - PTRP Development Model

## Overview

PTRP uses **Git Flow** branching model for version control and release management.

```
┌─────────────────────────────────────────────┐
│          PTRP Git Flow Model                │
├─────────────────────────────────────────────┤
│                                             │
│  main (production)  ─────────────────────   │
│       ↑                                     │
│       │ (tag v0.1.0, v0.2.0, ...)          │
│       │ (PR + status checks required)      │
│       │                                    │
│  develop (integration)  ──────────────────  │
│       ↑                                     │
│       │ (PR + status checks required)      │
│    ┌──┴──┐                                 │
│    │     │                                 │
│  feature/*  bugfix/*  docs/*  release/*    │
│  (feature development)                     │
│                                             │
└─────────────────────────────────────────────┘
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
  - `feature/patient-list-ui`
  - `feature/database-integration`
  - `feature/ISSUE-45-project-validation`

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
git checkout -b bugfix/patient-search-crash
# Fix bug
git commit -m "fix: prevent crash in patient search"
git push -u origin bugfix/patient-search-crash
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
# Edit src/PTRP.App/PTRP.App.csproj
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
# GitHub Actions release.yml triggers
# Creates installer + publishes release
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
git commit -m "feat: add patient search by name"
git commit -m "fix: prevent crash when deleting patient"
git commit -m "docs: add installation guide"
git commit -m "chore: bump version to 0.2.0"
git commit -m "test: add unit tests for PatientService"
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
git tag -a v0.1.0 -m "Initial MVVM WPF release"

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

### Triggers

**Build Workflow** (`build.yml`):
- Triggers on: push to `main`/`develop`, PR to `main`/`develop`
- Action: Compile project, run basic checks
- Status: Must pass before merge

**Test Workflow** (`test.yml`):
- Triggers on: push to `main`/`develop`, PR to `main`/`develop`
- Action: Run unit tests, measure code coverage (70% minimum)
- Status: Must pass before merge

**Release Workflow** (`release.yml`):
- Triggers on: push of tag `v*` to `main`
- Action: Build release, create Windows installer (Velopack), publish to GitHub Releases
- Status: Automatic, no manual intervention

---

## Quick Reference

```bash
# Clone repository
git clone https://github.com/artcava/PTRP.git
cd PTRP

# Check branches
git branch -a

# Create feature
git checkout develop
git pull origin develop
git checkout -b feature/my-feature
# ... make changes ...
git commit -m "feat: my feature"
git push -u origin feature/my-feature
# Create PR on GitHub

# Update develop locally
git checkout develop
git pull origin develop

# Delete local branch after merge
git branch -d feature/my-feature
git push origin --delete feature/my-feature

# Create release (maintainer only)
git checkout develop
git checkout -b release/0.2.0
# ... update version ...
git commit -m "chore: bump to 0.2.0"
git push -u origin release/0.2.0
# Create PR to main
# After merge: git tag -a v0.2.0 && git push origin v0.2.0
```

---

## Useful Commands

```bash
# See all branches
git branch -a

# Delete local branch
git branch -d branch-name

# Delete remote branch
git push origin --delete branch-name

# Fetch latest from remote
git fetch origin

# Pull latest develop
git checkout develop && git pull origin develop

# Check status
git status

# See recent commits
git log --oneline -10

# See commits on this branch vs main
git log main..HEAD --oneline
```

---

For more info, see:
- [RELEASE-PROCESS.md](./RELEASE-PROCESS.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)
