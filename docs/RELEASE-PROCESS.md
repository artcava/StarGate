# Release Process - StarGate

## Overview

This document describes the process for creating and publishing releases of StarGate.

**Frequency:** When features are stable and ready for testing/production  
**Versioning:** Semantic Versioning (e.g., 0.1.0, 0.2.0, 1.0.0)  
**Channels:** Stable releases + Pre-release (alpha/beta)  

---

## Release Types

### 1. Feature Release (Minor Version)
**When:** Multiple features completed and tested  
**Example:** 0.1.0 → 0.2.0  
**Timeline:** ~1-2 weeks of development

```
0.2.0 includes:
  ✓ New API endpoints
  ✓ Enhanced data processing
  ✓ Improved algorithms
  ✓ Additional test coverage
```

---

### 2. Bugfix Release (Patch Version)
**When:** Critical bug fixes need immediate release  
**Example:** 0.1.0 → 0.1.1  
**Timeline:** Same day or next day

```
0.1.1 includes:
  ✓ Fixed data processing issue
  ✓ Fixed API endpoint error
```

---

### 3. Major Release
**When:** Significant changes, API changes, or production milestone  
**Example:** 0.X.X → 1.0.0  
**Requirement:** Team discussion + planning

---

## Pre-Release Process (Before Versioning)

### Step 1: Ensure Main/Develop are Clean
```bash
# Switch to main
git checkout main
git pull origin main

# Should have no uncommitted changes
git status  # Should show "nothing to commit, working tree clean"
```

### Step 2: Create Release Branch
```bash
# Create release branch from main (for patch) or develop (for feature)
git checkout develop
git pull origin develop

# Create release branch
git checkout -b release/0.2.0
```

### Step 3: Update Version in Code
Edit version files in the project (e.g., `pyproject.toml`, `package.json`, or version configuration files):

```toml
# Example for Python project (pyproject.toml)
[project]
version = "0.2.0"
```

```json
// Example for Node.js project (package.json)
{
  "version": "0.2.0"
}
```

### Step 4: Update CHANGELOG
Create/update `CHANGELOG.md`:

```markdown
## [0.2.0] - 2026-02-10

### Added
- New data processing pipeline
- Enhanced API endpoints
- Improved algorithm performance
- Comprehensive unit tests (80% coverage)

### Fixed
- Data processing performance issue

### Changed
- Updated dependencies to latest versions

### Dependencies
- Updated core libraries
- Added new optimization tools
```

### Step 5: Commit Changes
```bash
git add .
git commit -m "chore: release v0.2.0

- Update version in configuration files
- Update CHANGELOG.md
- Ready for release"

git push -u origin release/0.2.0
```

---

## GitHub Release Process (Maintainer Only)

### Step 1: Create Pull Request to Main
1. Go to GitHub
2. Create Pull Request:
   - **Base:** `main`
   - **Compare:** `release/0.2.0`
   - **Title:** `Release v0.2.0`
   - **Description:** Copy from CHANGELOG

3. Wait for status checks (build + test) to pass
4. Get approval if required
5. **Merge** Pull Request (Squash or Create merge commit)

### Step 2: Create Pull Request Back to Develop
1. Create another Pull Request:
   - **Base:** `develop`
   - **Compare:** `release/0.2.0`
   - **Title:** `Merge release/0.2.0 back to develop`
   - **Description:** Ensures develop gets version bump

2. Merge to develop

### Step 3: Tag Release on Main
```bash
# Switch to main and pull latest
git checkout main
git pull origin main

# Create annotated tag
git tag -a v0.2.0 -m "Release v0.2.0

See CHANGELOG.md for details.

Features:
- New data processing pipeline
- Enhanced API endpoints
- Improved algorithm performance

Breaking changes: None"

# Push tag to GitHub
git push origin v0.2.0

# Verify tag
git tag -l
git show v0.2.0
```

### Step 4: GitHub Actions Release Workflow
When you push the tag `v0.2.0`:

1. **GitHub Actions Triggered:** Release workflow starts
2. **Build & Test:** 
   - Runs full test suite
   - Builds release artifacts
   - Generates documentation
3. **Create GitHub Release:**
   - Automatically creates release on GitHub
   - Attaches release artifacts
   - Uses tag message as release notes
4. **Publish to GitHub Releases Page:**
   - Available at: https://github.com/artcava/StarGate/releases

---

## Post-Release Verification

### 1. Check GitHub Release
- Go to [Releases](https://github.com/artcava/StarGate/releases)
- Verify v0.2.0 is listed
- Verify release artifacts are attached
- Verify release notes are correct

### 2. Test Release Artifacts
```bash
# Clone or pull latest release
git clone --branch v0.2.0 https://github.com/artcava/StarGate.git

# Run tests
# Verify functionality
# Verify version information
```

### 3. Announce Release
- Post to team/stakeholders
- Update documentation if needed
- Link to release notes

---

## Command Reference

### Create Release Branch
```bash
git checkout develop
git pull origin develop
git checkout -b release/0.2.0
```

### Push Release Branch
```bash
git add .
git commit -m "chore: release v0.2.0"
git push -u origin release/0.2.0
```

### Create Tag
```bash
git tag -a v0.2.0 -m "Release v0.2.0"
git push origin v0.2.0
```

### List Tags
```bash
git tag -l
git show v0.2.0  # Show tag details
```

### Delete Tag (if needed)
```bash
# Delete locally
git tag -d v0.2.0

# Delete remote
git push origin --delete v0.2.0
```

---

## Troubleshooting

### Release workflow failed
**Symptom:** Tag pushed but no release created  
**Solution:**
1. Check GitHub Actions tab for error logs
2. Verify workflow configuration is correct
3. Check that all required files are present
4. Verify permissions for GitHub Actions

### Status checks failing on PR
**Symptom:** Can't merge release PR due to failed build/test  
**Solution:**
1. Fix failing tests locally
2. Commit and push to release branch
3. PR automatically updates
4. Status checks re-run

### Accidentally pushed to wrong branch
**Solution:**
```bash
# Find the commit SHA
git log --oneline

# Revert on wrong branch
git revert <commit-sha>
git push origin branch-name

# Cherry-pick on correct branch
git checkout correct-branch
git cherry-pick <commit-sha>
git push
```

---

## Release Checklist

- [ ] Features merged to `develop`
- [ ] All tests passing locally
- [ ] Code reviewed and approved
- [ ] Create `release/X.X.X` branch
- [ ] Update version in configuration files
- [ ] Update CHANGELOG.md
- [ ] Commit and push release branch
- [ ] Create PR to `main`
- [ ] Status checks pass
- [ ] Merge PR to `main`
- [ ] Create PR back to `develop`
- [ ] Merge back to `develop`
- [ ] Create and push tag `vX.X.X`
- [ ] Verify GitHub Release created
- [ ] Test release artifacts
- [ ] Announce release

---

For questions, see [GIT-FLOW.md](./GIT-FLOW.md) or [CONTRIBUTING.md](./CONTRIBUTING.md).
