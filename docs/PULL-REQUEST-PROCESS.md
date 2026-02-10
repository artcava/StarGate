# Pull Request Process - StarGate

## Overview

This document describes the Pull Request (PR) process for the StarGate project, including templates, guidelines, and best practices for creating, updating, and reviewing pull requests.

**Goals:**
- Maintain high code quality through structured review process
- Ensure clear communication between contributors and reviewers
- Document changes effectively for future reference
- Streamline the merge process while maintaining standards

---

## Table of Contents

1. [Before Creating a Pull Request](#before-creating-a-pull-request)
2. [Pull Request Template](#pull-request-template)
3. [Creating a Pull Request](#creating-a-pull-request)
4. [Updating an Open Pull Request](#updating-an-open-pull-request)
5. [Review Process](#review-process)
6. [Comment Guidelines](#comment-guidelines)
7. [Merge Requirements](#merge-requirements)
8. [After Merge](#after-merge)

---

## Before Creating a Pull Request

### Prerequisites Checklist

Before opening a PR, ensure you have:

- [ ] **Created a feature/bugfix branch** following [Git Flow conventions](./GIT-FLOW.md)
- [ ] **Written clear commit messages** following [Conventional Commits](./GIT-FLOW.md#commit-message-convention)
- [ ] **Followed coding conventions** as described in [CODING-CONVENTIONS.md](./CODING-CONVENTIONS.md)
- [ ] **Formatted your code** using `dotnet format`
- [ ] **Written or updated unit tests** with adequate coverage (≥70%)
- [ ] **Run tests locally** and verified they pass
- [ ] **Updated documentation** if introducing new features or changing behavior
- [ ] **Reviewed your own changes** to catch obvious issues

### Local Validation

```bash
# Format code
dotnet format

# Build project
dotnet build --configuration Release

# Run tests
dotnet test --no-build --verbosity normal

# Check code coverage
dotnet test --collect:"XPlat Code Coverage"

# Verify no warnings
dotnet build /p:TreatWarningsAsErrors=true
```

---

## Pull Request Template

When creating a pull request, use the following template structure. This template ensures all necessary information is provided for effective review.

### Template Structure

```markdown
## Description

### Summary
Brief description of what this PR does (2-3 sentences).

### Motivation and Context
Why is this change needed? What problem does it solve?
Link to related issue(s): Fixes #[issue_number]

### Type of Change
Select the type of change (delete options that don't apply):

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Code refactoring (no functional changes)
- [ ] Performance improvement
- [ ] Test additions/updates
- [ ] CI/CD changes

---

## Changes Made

### Key Changes
List the main changes in bullet points:

- Change 1: Brief description
- Change 2: Brief description
- Change 3: Brief description

### Technical Details
Provide more detailed technical information if needed:

- **Architecture changes**: Describe any architectural modifications
- **API changes**: Document new/modified endpoints or interfaces
- **Database changes**: Describe schema or data migrations
- **Configuration changes**: Note any new settings or environment variables

---

## Testing

### Test Coverage
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated (if applicable)
- [ ] All tests passing locally
- [ ] Code coverage maintained/improved (current: X%)

### Manual Testing
Describe manual testing performed:

1. Test scenario 1: Description and result
2. Test scenario 2: Description and result
3. Test scenario 3: Description and result

### Test Evidence
```
Paste relevant test output or screenshots here
```

---

## Checklist

Before requesting review, confirm:

- [ ] Code follows [coding conventions](./CODING-CONVENTIONS.md)
- [ ] Code has been formatted with `dotnet format`
- [ ] Self-review completed
- [ ] Comments added for complex logic
- [ ] XML documentation added for public APIs
- [ ] No compiler warnings
- [ ] All tests pass
- [ ] Documentation updated (if needed)
- [ ] CHANGELOG.md updated (for release branches)
- [ ] No sensitive data (passwords, tokens, etc.) in code or commits

---

## Reviewers

@mention specific reviewers if needed

### Review Focus Areas
Specific areas where you'd like focused review:

- Area 1: Reason
- Area 2: Reason

---

## Additional Notes

Any additional context, concerns, or discussion points:

- Note 1
- Note 2

---

## Screenshots/Recordings

If applicable, add screenshots or recordings to demonstrate changes:

<!-- Add images or links here -->
```

---

## Creating a Pull Request

### Step-by-Step Process

#### 1. Push Your Branch

```bash
# Ensure your branch is up to date with develop/main
git checkout develop
git pull origin develop

git checkout feature/your-feature
git rebase develop

# Push your branch
git push -u origin feature/your-feature
```

#### 2. Open Pull Request on GitHub

1. Navigate to the repository on GitHub
2. Click **"Pull requests"** tab
3. Click **"New pull request"**
4. Select base branch:
   - `develop` for features and bugfixes
   - `main` for releases (maintainers only)
5. Select compare branch: your feature/bugfix branch
6. Click **"Create pull request"**

#### 3. Fill Out the Template

Use the [PR template](#pull-request-template) provided above and fill in all sections:

**Title Format:**
```
<type>: <short description>
```

**Examples:**
- `feat: add process polling mechanism`
- `fix: resolve database connection timeout`
- `docs: update API documentation`
- `refactor: simplify authentication logic`

**Description Best Practices:**

✅ **DO:**
- Be specific and descriptive
- Explain the "why" behind changes, not just "what"
- Link to related issues or discussions
- Include test results or evidence
- Mention breaking changes prominently

❌ **DON'T:**
- Leave sections blank
- Use vague descriptions like "fixed stuff" or "updates"
- Forget to mention breaking changes
- Skip the testing section

#### 4. Initial PR Comment Example

```markdown
## 🚀 Ready for Review

This PR implements the adaptive polling mechanism for process status monitoring as discussed in issue #42.

### Overview
The polling strategy now uses adaptive intervals (30s → 60s) to balance responsiveness with resource efficiency. This reduces API calls by approximately 40% while maintaining good user experience.

### Key Implementation Details
- Added `PollingStrategyService` with configurable intervals
- Implemented exponential backoff for transient failures
- Added comprehensive unit tests (85% coverage)
- Updated configuration schema to support custom polling intervals

### Testing
All existing tests pass, and new tests cover:
- Normal polling flow
- Failure scenarios with retry logic
- Polling interval transitions
- Configuration validation

### Questions for Reviewers
1. Should we make the 2-minute threshold configurable?
2. Any concerns about the exponential backoff parameters?

Looking forward to your feedback! 🙏
```

---

## Updating an Open Pull Request

### Adding New Commits

When addressing feedback or adding improvements to an open PR:

#### 1. Make Changes Locally

```bash
# Ensure you're on the correct branch
git checkout feature/your-feature

# Make your changes
# ...

# Commit with clear message
git add .
git commit -m "refactor: simplify polling interval calculation"

# Push to update PR
git push origin feature/your-feature
```

#### 2. Comment on the Update

Always add a comment explaining what you changed:

**Update Comment Template:**

```markdown
## 📝 Update: [Brief description]

### Changes Made
In response to review feedback:

- ✅ [Reviewer Name]: Simplified polling interval calculation (commit abc123)
- ✅ [Reviewer Name]: Added validation for negative intervals (commit def456)
- ✅ Fixed code formatting issues (commit ghi789)

### New Commits
- `abc123`: refactor: simplify polling interval calculation
- `def456`: fix: add validation for negative intervals
- `ghi789`: style: fix code formatting

### Still TODO
- [ ] Add integration test for edge case (working on this)
- [ ] Update documentation (will do after approval)

### Re-Review Request
@reviewer1 @reviewer2 - Changes are ready for another look! 👀
```

#### 3. Best Practices for Updates

✅ **DO:**
- Make atomic commits (one logical change per commit)
- Reference the specific feedback you're addressing
- Mention which commits address which review comments
- Ask for re-review when ready
- Keep commit history clean and meaningful

❌ **DON'T:**
- Force push unless necessary (loses review context)
- Make unrelated changes in update commits
- Leave review comments unresolved without explanation
- Push "WIP" or "fix" commits without context

### Handling Conflicts

If your PR has conflicts with the base branch:

```bash
# Update your local base branch
git checkout develop
git pull origin develop

# Rebase your feature branch
git checkout feature/your-feature
git rebase develop

# Resolve conflicts
# Edit conflicted files...
git add .
git rebase --continue

# Force push (necessary after rebase)
git push --force-with-lease origin feature/your-feature
```

**Comment after resolving conflicts:**

```markdown
## 🔄 Rebased on latest develop

Rebased on `develop` branch (commit xyz789) to resolve conflicts.

### Conflict Resolution
- **File**: `src/Services/PollingService.cs`
  - **Conflict**: Both branches modified polling logic
  - **Resolution**: Kept new adaptive polling + preserved logging improvements from develop

- **File**: `tests/PollingServiceTests.cs`
  - **Conflict**: Test setup methods diverged
  - **Resolution**: Merged both test improvements

All tests still passing ✅
```

---

## Review Process

### For Authors

#### Responding to Review Comments

When reviewers leave comments, respond promptly and clearly:

**Response Template:**

```markdown
> **Reviewer Comment:** "Should we add validation here for null values?"

✅ **Fixed in commit abc123**

Added null validation and corresponding unit test. Good catch!

```csharp
if (value == null)
{
    throw new ArgumentNullException(nameof(value));
}
```
```

**Alternative Responses:**

```markdown
> **Reviewer Comment:** "Consider using StringBuilder here for better performance."

💭 **Question**

Good point! However, this loop typically runs <10 iterations. Would the StringBuilder overhead be worth it in this case? The current approach seems more readable.

Happy to change if you feel strongly about it!
```

```markdown
> **Reviewer Comment:** "Can we extract this into a separate method?"

📝 **TODO**

Agreed! I'll extract this into `ValidateProcessInput()` method. Will push the update shortly.
```

```markdown
> **Reviewer Comment:** "Missing XML documentation."

✅ **Fixed in commit def456**

Added comprehensive XML documentation for all public methods.
```

### For Reviewers

#### Effective Review Comments

Follow [GitHub's best practices for code review](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/reviewing-changes-in-pull-requests/about-pull-request-reviews).

**Comment Categories:**

1. **Blocking Issues** (must be fixed before merge)
2. **Suggestions** (nice to have, but not blocking)
3. **Questions** (asking for clarification)
4. **Praise** (acknowledging good work)

**Comment Template:**

```markdown
**[Category]: [Title]**

[Detailed explanation of the issue/suggestion/question]

[If applicable, provide example or suggestion]

```csharp
// Suggested change
[code example]
```

[Explain why this matters - performance, security, maintainability, etc.]
```

**Examples:**

**Blocking Issue:**
```markdown
**🚫 Blocking: Potential null reference exception**

`process.Data` could be null here if the database returns null. This would cause a `NullReferenceException`.

```csharp
// Suggested fix
if (process.Data == null)
{
    _logger.LogWarning("Process {Id} has no data", process.Id);
    return ProcessResult.Empty();
}
```

This needs to be fixed before merge to prevent runtime errors.
```

**Suggestion:**
```markdown
**💡 Suggestion: Extract magic number to constant**

The value `30` appears multiple times. Consider extracting to a named constant for better maintainability:

```csharp
private const int AggressivePollingIntervalSeconds = 30;
```

This makes the code more self-documenting and easier to adjust.

Not blocking, but would improve code quality.
```

**Question:**
```markdown
**❓ Question: Thread safety consideration**

Is `_pollingState` accessed from multiple threads? If so, should we add synchronization?

Just want to make sure we're not introducing race conditions.
```

**Praise:**
```markdown
**✨ Nice work!**

Excellent test coverage for the edge cases! The parameterized tests make it easy to see all scenarios.
```

#### Review Completion Comments

**Approval:**
```markdown
## ✅ LGTM (Looks Good To Me)

Excellent work! The adaptive polling implementation is clean and well-tested.

### Highlights
- Comprehensive test coverage (85%)
- Clear separation of concerns
- Good error handling

### Minor suggestions addressed
All my suggestions have been addressed or discussed satisfactorily.

Approved and ready to merge! 🚀
```

**Request Changes:**
```markdown
## 🔍 Changes Requested

Thanks for the PR! The approach is solid, but there are a few issues that need to be addressed before merging.

### Blocking Issues
1. **Null reference risk** (line 45): Needs null check - see inline comment
2. **Missing unit test**: No test for the failure scenario - see inline comment
3. **Code formatting**: Run `dotnet format` to fix formatting issues

### Suggestions (non-blocking)
1. Extract magic numbers to constants
2. Add XML documentation for public method

Let me know if you have any questions on the feedback!
```

**Comment (neither approve nor request changes):**
```markdown
## 💬 Review Comments

Left a few comments and questions inline. Overall looks good, just want to clarify a few things before approving.

No blocking issues, but please address the questions when you have a chance.
```

---

## Merge Requirements

### Automated Checks

Before a PR can be merged, the following must pass:

✅ **Build Job**: Code must compile without errors
✅ **Test Job**: All tests must pass with coverage ≥ 70%
✅ **Quality Job**: Code must pass formatting and analyzer checks

### Manual Requirements

For merging to `main`:
- ✅ At least **1 approval** from a code owner/maintainer
- ✅ All review comments **resolved** or **acknowledged**
- ✅ No **outstanding change requests**
- ✅ Branch is **up to date** with base branch
- ✅ **CHANGELOG.md** updated (for release branches)

For merging to `develop`:
- ✅ All **automated checks** pass
- ✅ Code **reviewed** (approval recommended but not strictly required)
- ✅ Branch is **up to date** with develop

### Merge Methods

**Squash and Merge** (Recommended for `develop`):
- Combines all commits into one
- Clean linear history
- Use when PR has many small commits

**Create a Merge Commit** (Recommended for `main`):
- Preserves all commits
- Maintains full history
- Use for release branches

**Rebase and Merge** (Use sparingly):
- Replays commits on top of base
- Linear history without merge commit
- Use only when commits are already clean and atomic

### Final Merge Comment Template

```markdown
## 🎉 Merged!

Thanks for the contribution! This PR has been merged into `develop`.

### What's Next
- This will be included in the next release (v0.X.X)
- Monitor CI/CD for any issues
- Close related issues: Closes #42

### Post-Merge Actions
- [ ] Delete feature branch
- [ ] Verify deployment to dev environment
- [ ] Update related documentation

Great work! 🚀
```

---

## After Merge

### Clean Up

```bash
# Delete local branch
git branch -d feature/your-feature

# Delete remote branch (if not done via GitHub)
git push origin --delete feature/your-feature

# Update local develop
git checkout develop
git pull origin develop
```

### Follow-Up Actions

- [ ] Verify the change in the target branch
- [ ] Close related issues with reference to the merged PR
- [ ] Update project board or task tracker
- [ ] Monitor for any issues in CI/CD or deployments
- [ ] Communicate changes to team if significant

---

## Common Scenarios

### Scenario 1: PR Needs Significant Changes

**Reviewer Comment:**
```markdown
## 🔄 Significant Refactoring Needed

After reviewing this PR, I think we need to take a different approach. The current implementation has architectural concerns:

1. Tight coupling between services
2. Difficult to test
3. Performance implications

### Suggested Approach
Instead of modifying the existing service, let's:
1. Create a new `IPollingStrategy` interface
2. Implement concrete strategies (Aggressive, Conservative)
3. Use dependency injection to select strategy

### Discussion
Let's discuss this approach before you start refactoring. We can schedule a quick call or discuss here.

What do you think? @author
```

**Author Response:**
```markdown
## 💭 Discussion: Alternative Approach

@reviewer Thanks for the thorough review!

I see your points about coupling and testability. The interface-based approach makes sense.

### Questions
1. Should we support runtime strategy switching, or is DI configuration sufficient?
2. Do we need backward compatibility with existing configuration?

### Proposal
I can create a draft implementation showing the new approach and we can review that before fully committing. Would that work?

Alternatively, should we close this PR and create a new one with the refactored approach?
```

### Scenario 2: PR Has Merge Conflicts

**GitHub Comment (automated):**
```
This branch has conflicts that must be resolved
```

**Author Comment:**
```markdown
## ⚠️ Merge Conflicts Detected

Working on resolving conflicts with `develop`. Will update shortly.

### Conflicting Files
- `src/Services/PollingService.cs`
- `tests/PollingServiceTests.cs`

Will rebase and force-push once resolved.
```

### Scenario 3: Long-Running PR Needs Updates

**Reminder Comment:**
```markdown
## 🔔 Status Update Needed

This PR has been open for 2 weeks. Current status:

### Outstanding Items
- [ ] Address reviewer feedback on line 45
- [ ] Add requested integration test
- [ ] Rebase on latest develop

@author - Are you still working on this? Let us know if you need any help or if we should close this PR.
```

---

## Best Practices Summary

### For All Participants

✅ **DO:**
- Be respectful and constructive in all communications
- Assume good intent from others
- Focus on the code, not the person
- Provide specific, actionable feedback
- Acknowledge good work and improvements
- Be responsive to comments and questions
- Keep discussions focused and relevant

❌ **DON'T:**
- Make personal attacks or be dismissive
- Approve PRs without actually reviewing them
- Leave comments unresolved without explanation
- Introduce unrelated changes in PR updates
- Merge PRs that don't meet quality standards
- Force your opinion without discussing alternatives

### Communication Guidelines

1. **Be Clear**: Write comments that others can understand without context
2. **Be Specific**: Reference exact lines, files, or commits
3. **Be Constructive**: Suggest improvements, don't just criticize
4. **Be Timely**: Respond to PR activity within 24-48 hours when possible
5. **Be Thorough**: Don't just approve without reading the code

---

## Additional Resources

### Internal Documentation
- [Git Flow Process](./GIT-FLOW.md) - Branch management and workflow
- [Coding Conventions](./CODING-CONVENTIONS.md) - Code style and standards
- [Release Process](./RELEASE-PROCESS.md) - Creating releases

### External Resources
- [GitHub Pull Request Documentation](https://docs.github.com/en/pull-requests)
- [About Pull Request Reviews](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/reviewing-changes-in-pull-requests/about-pull-request-reviews)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [How to Write a Git Commit Message](https://chris.beams.io/posts/git-commit/)

---

## Questions?

If you have questions about the PR process:

1. Check this document and linked resources
2. Ask in the team chat or discussion forum
3. Reach out to maintainers directly

We're here to help! 🙏
