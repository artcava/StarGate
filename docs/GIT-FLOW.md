# Git Flow - PTRP Development Model

## Overview

PTRP uses **Git Flow** branching model for version control and release management.

```
┌──────────────────────────────────────────┐
│          PTRP Git Flow Model             │
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
# GitHub Actions ci.yml triggers
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

Il progetto utilizza un **unico workflow GitHub Actions** (`ci.yml`) che gestisce tutti gli aspetti del CI/CD: build, test e release.

### Workflow Unificato: `ci.yml`

**Posizione:** `.github/workflows/ci.yml`

**Funzionalità:**
- **Build & Test**: Compilazione e testing automatico su ogni push/PR
- **Code Quality**: Verifica formattazione e analyzer
- **Release**: Creazione automatica di release su push di tag

### Trigger Events

| Event | Branches | Jobs Eseguiti |
|-------|----------|---------------|
| **Push** | `main`, `develop` | Build → Test → Quality |
| **Pull Request** | `main`, `develop` | Build → Test → Quality |
| **Push Tag** | `v*` (es. `v0.1.0`) | Build → Test → Release |

### Jobs del Workflow

#### 1. **Build Job**
```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - Checkout del codice
      - Setup .NET SDK
      - Restore delle dipendenze
      - Build del progetto
      - Upload degli artifacts
```

**Obiettivo:** Verificare che il codice compili correttamente.

**Output:** Artifacts pronti per testing e release.

---

#### 2. **Test Job**
```yaml
  test:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - Checkout del codice
      - Setup .NET SDK
      - Restore delle dipendenze
      - Esecuzione unit tests
      - Generazione code coverage report
      - Verifica soglia minima (70%)
```

**Obiettivo:** Eseguire tutti gli unit test e verificare la code coverage.

**Requisiti:**
- ✅ Tutti i test devono passare
- ✅ Code coverage ≥ 70%

**Failure:** PR bloccata se i test falliscono o coverage < 70%.

---

#### 3. **Quality Job**
```yaml
  quality:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - Checkout del codice
      - Setup .NET SDK
      - Restore delle dipendenze
      - Verifica formattazione (dotnet format)
      - Esecuzione analyzers
      - Controllo warnings
```

**Obiettivo:** Garantire qualità e consistenza del codice.

**Verifica:**
- ✅ Codice formattato correttamente (`.editorconfig`)
- ✅ Nessuna violazione degli analyzer
- ✅ Nessun warning critico

**Failure:** PR bloccata se ci sono violazioni di formattazione o analyzer.

---

#### 4. **Release Job** (Solo su Tag)
```yaml
  release:
    needs: [build, test, quality]
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: windows-latest
    steps:
      - Checkout del codice
      - Setup .NET SDK
      - Build Release configuration
      - Creazione installer (Velopack/MSIX)
      - Pubblicazione GitHub Release
      - Upload artifacts della release
```

**Obiettivo:** Creare e pubblicare automaticamente una nuova release.

**Trigger:** Push di un tag con formato `v*` (es. `v0.1.0`, `v1.2.3`).

**Output:**
- 📦 GitHub Release con installer
- 📝 Release notes automatiche
- 🔗 Download links per gli utenti

---

### Status Checks per Pull Request

Prima di poter mergeare una PR verso `main` o `develop`, devono passare:

✅ **Build Job** - Compilazione senza errori  
✅ **Test Job** - Tutti i test passano + coverage ≥ 70%  
✅ **Quality Job** - Codice formattato + nessuna violazione analyzer

**Esempio di stato PR:**
```
✅ ci / build (pull_request)           — Passed in 2m 34s
✅ ci / test (pull_request)            — Passed in 1m 45s
✅ ci / quality (pull_request)         — Passed in 1m 12s
```

---

### Workflow di Release Completo

**Step 1:** Merge di `release/X.X.X` in `main`
```bash
# Dopo aver completato la PR e merge
git checkout main
git pull origin main
```

**Step 2:** Creazione e push del tag
```bash
git tag -a v0.2.0 -m "Release v0.2.0: Feature X, Fix Y"
git push origin v0.2.0
```

**Step 3:** Workflow automatico
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
📦 Creates Windows installer
  ↓
🚀 Publishes GitHub Release
  ↓
✅ Release v0.2.0 published!
```

**Step 4:** Verifica su GitHub
- Vai a: `https://github.com/[user]/[repo]/releases`
- Trova la release `v0.2.0`
- Download dell'installer disponibile

---

### Configurazione Branch Protection

Per abilitare i controlli automatici, configura su GitHub:

**Settings → Branches → Branch protection rules → `main` / `develop`**

```
☑️ Require status checks to pass before merging
  ☑️ Require branches to be up to date before merging
  
  Status checks that are required:
    ☑️ ci / build
    ☑️ ci / test
    ☑️ ci / quality

☑️ Require pull request reviews before merging
  • Required approvals: 1 (per main)
  • Required approvals: 0 (per develop)

☑️ Do not allow bypassing the above settings
```

---

### Debugging del Workflow

Se il workflow fallisce:

**1. Verifica logs su GitHub Actions:**
```
Repository → Actions → ci → Click sul run fallito → Espandi il job
```

**2. Test in locale:**
```bash
# Simula il build job
dotnet restore
dotnet build --configuration Release

# Simula il test job
dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

# Simula il quality job
dotnet format --verify-no-changes
dotnet build /p:TreatWarningsAsErrors=true
```

**3. Errori comuni:**

| Errore | Causa | Soluzione |
|--------|-------|----------|
| Build fails | Errori di compilazione | Fissa gli errori nel codice |
| Test fails | Test unitari falliti | Correggi i test o il codice |
| Coverage < 70% | Code coverage insufficiente | Aggiungi più test |
| Format check fails | Codice non formattato | Esegui `dotnet format` |
| Analyzer warnings | Violazioni regole analyzer | Correggi le violazioni o sopprimi se giustificato |

---

### Vantaggi del Workflow Unificato

✅ **Semplicità:** Un solo file da mantenere invece di 3  
✅ **Consistenza:** Tutti i job condividono la stessa configurazione  
✅ **Efficienza:** Riuso di artifacts tra job (caching)  
✅ **Visibilità:** Status checks chiari e centralizzati  
✅ **Manutenibilità:** Modifiche in un unico punto  

---

For more info, see:
- [RELEASE-PROCESS.md](./RELEASE-PROCESS.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)
