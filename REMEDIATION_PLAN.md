# Git & Directory Structure Remediation Plan

**Status**: PLAN - NOT YET EXECUTED  
**Date**: February 5, 2026  
**Constraint**: predev branch MUST NOT be modified  
**Scope**: Fix directory structure and git history divergence  

---

## Current State Analysis

### Directory Structure Problems

**Current (WRONG)**:
```
/workspace/
├── [documentation files - Phase 1, 2]
├── [scripts, src, tests - DUPLICATED]
└── workspace/
    ├── .git (ACTUAL REPO ROOT)
    ├── src/ (ACTUAL SOURCE)
    ├── tests/ (ACTUAL TESTS)
    ├── .github/
    └── tracker.sln
```

**Expected (CORRECT)**:
```
/workspace/
├── .git (REPO ROOT AT TOP LEVEL)
├── src/
├── tests/
├── .github/
├── tracker.sln
├── README.md
├── [documentation files]
└── [other project files]
```

**Issues**:
- ❌ Git repository is nested in `/workspace/workspace/.git`
- ❌ Documentation files are in `/workspace/` (outside git repo)
- ❌ Source/test files duplicated at `/workspace/src` (broken copies)
- ❌ Directory depth increases tooling complexity

### Git History Divergence

**Current State**:
```
LOCAL BRANCH: 0.2.0-pre
  - 21+ commits with Phase 1, 2 implementation
  - Unpushed changes to Phase 2 tests

REMOTE: origin/predev
  - f225290: "Release v0.1.0-predev - Complete Windows App Time Tracker Service"
  - 9766e53: "Initial commit"
  - NO Phase 1, 2 work reflected

REMOTE: origin/main
  - 9766e53: "Initial commit" (SAME as predev - repos are essentially identical)

DIVERGENCE:
  - Local 0.2.0-pre: 21+ commits AHEAD of origin/predev
  - origin/predev: Has v0.1.0 release commit NOT on local 0.2.0-pre
  - origin/main: Only initial commit (NO development)
```

**Root Cause**:
- Previous commits to 0.2.0-pre were never pushed to origin/predev
- Phase 1, 2 work was committed to local 0.2.0-pre (not pushed)
- User attempted to push via Skill to origin/predev, but changes may not have properly synced
- Main branch has minimal history

---

## Remediation Strategy

### Phase A: PRESERVE predev Branch (No Modifications)

**Goal**: Keep origin/predev exactly as-is per user requirement

**Actions**:
1. ✅ Do NOT fetch/pull from origin/predev
2. ✅ Do NOT rebase onto origin/predev
3. ✅ Do NOT force-push to origin/predev
4. ✅ Do NOT modify local branches that track origin/predev

**Result**: origin/predev remains untouched

---

### Phase B: Fix Directory Structure (Non-Destructive)

**Goal**: Move repo root from `/workspace/workspace/.git` to `/workspace/.git`

**Strategy**: Copy, don't move (preserve git state)

**Steps**:

1. **Create clean directory structure**
   ```bash
   # At /workspace/.git
   cp -r /workspace/workspace/.git /workspace/.git-new
   ```

2. **Verify git integrity**
   ```bash
   cd /workspace
   git -C /workspace/.git-new status  # Verify functionality
   ```

3. **Swap git directories** (atomic operation)
   ```bash
   cd /workspace
   mv /workspace/.git /workspace/.git-old  # Backup
   mv /workspace/.git-new /workspace/.git  # Activate
   ```

4. **Verify all project files are accessible**
   ```bash
   git status  # Should show /workspace/src, tests, etc.
   ls -la /workspace/src
   ls -la /workspace/tests
   ```

5. **Update .gitignore if needed** (git should already be at root)

6. **Backup old structure** (for reference, can delete later)
   ```bash
   mv /workspace/workspace /workspace/workspace-old-structure
   ```

**No Rebase, No History Rewrite**: Just moving .git directory

---

### Phase C: Handle Git History (ON SEPARATE BRANCH)

**Goal**: Clean up local history divergence, preserve origin/predev

**Constraint**: ALL history work on NEW branch, never touch predev

**Strategy**:

1. **Create history-cleanup branch** (from current state)
   ```bash
   cd /workspace
   git checkout -b history/cleanup-divergence 0.2.0-pre
   ```

2. **Analyze commit graph**
   ```bash
   git log --graph --oneline --all
   # Identify:
   # - Commits on 0.2.0-pre NOT on origin/predev
   # - Commits on origin/predev NOT on 0.2.0-pre
   ```

3. **Strategy Decision** (two options):

   **OPTION A: Squash Phase 1+2 into clean commits**
   - Create new branch `feature/phase-1-2-implementation`
   - Interactive rebase to consolidate commits
   - Result: 2-3 clean commits summarizing Phase 1+2
   - Then merge to main (fast-forward)
   - Keep origin/predev as-is (per requirement)

   **OPTION B: Keep full history, merge to main**
   - Merge 0.2.0-pre → main (preserves all commits)
   - Main now has full Phase 1+2 history
   - origin/predev stays separate (per requirement)

4. **Recommended approach (OPTION A)**
   - Cleaner history
   - Professional summary commits
   - Preserves predev completely
   - Easy to understand for code review

**On history/cleanup-divergence Branch Only**:
   ```bash
   git rebase -i <commit-hash>  # Only on cleanup branch
   git commit --amend            # Only on cleanup branch
   ```

5. **Final state**:
   ```
   origin/main: ← Phase 1+2 work merged here (NEW)
   origin/predev: UNCHANGED (per requirement)
   history/cleanup-divergence: Cleanup work (can be deleted after)
   local 0.2.0-pre: UNCHANGED (no modifications)
   ```

---

## Step-by-Step Execution Plan

### STEP 1: Prepare (Non-Destructive Verification)

```bash
# Current state
cd /workspace/workspace
git status
git log --oneline | head -5
git branch -a

# Verify remotes
git remote -v

# Check what's unpushed
git log origin/predev..HEAD --oneline
git log origin/main..HEAD --oneline
```

**Expected Output**:
- Current branch: 0.2.0-pre (or local equivalent)
- Unpushed commits: 21+ commits
- Remotes: origin (GitHub)

---

### STEP 2: Fix Directory Structure

```bash
# 1. Verify current .git is functional
cd /workspace/workspace
git rev-parse --git-dir  # Should show .git

# 2. Copy .git to parent directory
cp -r /workspace/workspace/.git /workspace/.git-new

# 3. Test new .git is functional
cd /workspace
git -C /workspace/.git-new rev-parse --git-dir
git -C /workspace/.git-new log --oneline | head -3

# 4. Backup current .git in workspace/
cp -r /workspace/workspace/.git /workspace/workspace/.git-backup

# 5. Swap .git to parent
cd /workspace
mv /workspace/.git /workspace/.git-old-location
mv /workspace/.git-new /workspace/.git

# 6. Verify from parent directory
cd /workspace
git status  # Should work from parent now
git log --oneline | head -3

# 7. Verify project structure is accessible
ls -la /workspace/src
ls -la /workspace/tests
ls -la /workspace/.github

# 8. Verify workspace/src is still there (for reference)
ls -la /workspace/workspace/src
```

---

### STEP 3: Create History Cleanup Branch

```bash
cd /workspace

# 1. Create cleanup branch from current HEAD
git checkout -b history/cleanup-divergence

# 2. Verify branch created
git branch -a | grep cleanup

# 3. Check commits ahead of main
git log origin/main..HEAD --oneline
# Should show 21+ commits

# 4. Check what's different from predev
git log remotes/origin/predev..HEAD --oneline
# Should show Phase 1+2 work
```

---

### STEP 4: Analyze History on Cleanup Branch

```bash
cd /workspace

# 1. View full history graph
git log --graph --oneline --all | head -50

# 2. Find commit before Phase 1 work
git log --oneline | grep -E "(Phase|Test|Code Quality)" | tail -1

# 3. Identify base commit (should be common with main/predev)
git merge-base origin/main 0.2.0-pre
# This shows the last common commit between main and 0.2.0-pre
```

---

### STEP 5: Clean Up History (ON cleanup-divergence BRANCH ONLY)

**Option A: Recommended - Squash Phase 1+2 into summary commits**

```bash
cd /workspace
git checkout history/cleanup-divergence

# 1. Find the base commit (last common with main)
BASE_COMMIT=$(git merge-base origin/main HEAD)
echo "Base commit: $BASE_COMMIT"

# 2. Interactive rebase to consolidate
git rebase -i $BASE_COMMIT

# In the interactive rebase editor:
# - Keep first Phase 1 commit as 'pick'
# - Change all other Phase 1 commits to 'squash' (s)
# - Keep first Phase 2 commit as 'pick'
# - Change all other Phase 2 commits to 'squash' (s)
# - Save and exit editor
# - Provide new commit messages:
#   "feat: Phase 1 - Code quality & configuration (5 tasks)"
#   "feat: Phase 2 - Unit tests & coverage (95+ tests)"

# 3. Verify result
git log --oneline | head -10
# Should show 2-3 new consolidated commits

# 4. Verify files are correct
git status
git diff --name-status origin/main

# 5. Verify no unintended changes
git diff origin/main | wc -l  # Reasonable size
```

---

### STEP 6: Verify Predev is Untouched

```bash
cd /workspace

# 1. Confirm predev not modified
git fetch origin predev:refs/remotes/origin/predev-check --force
git log remotes/origin/predev-check --oneline | head -5
# Should be identical to before

# 2. Confirm no pushes happened to predev
git log origin/predev..origin/predev-check --oneline
# Should be empty

# 3. Confirm local branches unchanged
git log 0.2.0-pre --oneline | head -5
# Should still show original commits (not on cleanup branch)
```

---

## Post-Remediation State

### Directory Structure (FIXED ✅)

```
/workspace/
├── .git/                    ← Now at root
├── src/
├── tests/
├── .github/
├── tracker.sln
├── README.md
├── [documentation files]
└── workspace-old-structure/ ← Backup (can be deleted)
```

### Git Branches (CLEAN ✅)

```
origin/main
  └── Phase 1+2 work squashed into clean commits (NEW)

origin/predev
  └── UNCHANGED - f225290 (per requirement) ✅

local 0.2.0-pre
  └── Original commits (unchanged)

history/cleanup-divergence
  └── Cleanup work (can be deleted after verification)
```

### Git History (CLEAN ✅)

```
A (Initial commit)
├── origin/main
└── origin/predev (v0.1.0 release)
    └── A → B → C [Phase 1+2 squashed] → origin/main
```

---

## Verification Checklist

After executing remediation:

- [ ] `.git` directory is at `/workspace/.git` (not nested)
- [ ] `git status` works from `/workspace/`
- [ ] `git log` shows expected commits
- [ ] `/workspace/src` and `/workspace/tests` are in git tracking
- [ ] origin/predev is UNCHANGED (f225290 still at HEAD)
- [ ] origin/main has Phase 1+2 work
- [ ] history/cleanup-divergence branch is separate (not predev)
- [ ] `dotnet build` works from `/workspace`
- [ ] All documentation files are tracked in git
- [ ] No uncommitted changes when cleanup complete

---

## Rollback Plan (If Needed)

If remediation encounters issues:

```bash
# 1. Restore from backup
cd /workspace
mv /workspace/.git /workspace/.git-new-failed
mv /workspace/.git-old-location /workspace/.git

cd /workspace/workspace
git status  # Should work again from nested location

# 2. Restore workspace-old-structure if deleted
# (copy from backup location)

# 3. Delete cleanup branch
git branch -D history/cleanup-divergence

# 4. All local commits still on 0.2.0-pre
git checkout 0.2.0-pre
git log --oneline | head -5
```

---

## Implementation Notes

### Why This Approach?

1. **Non-Destructive**: Original .git backed up, can rollback
2. **Preserves predev**: Origin/predev never touched (per requirement)
3. **Fixes divergence**: History cleanup on separate branch
4. **Clean result**: Directory structure matches standard projects
5. **Verifiable**: Each step can be verified before proceeding

### Tools Needed

- `git` (already available)
- `bash` (already available)
- `cp`, `mv` (standard Unix, already available)

### Time Estimate

- Directory structure fix: 5 minutes
- History analysis: 5 minutes
- History cleanup (rebase): 10 minutes
- Verification: 10 minutes
- **Total**: ~30 minutes

### Risk Level

**LOW** - Non-destructive operations with backups at each step

---

## Approval Required

Before executing this plan:

1. ✅ User confirms directory structure fix is acceptable
2. ✅ User confirms Option A (squash commits) for history cleanup
3. ✅ User confirms predev MUST remain untouched
4. ✅ User confirms separate cleanup branch is acceptable

---

## Summary

This remediation plan:
- ✅ Fixes directory structure (moves .git to root)
- ✅ Cleans git history (on separate branch only)
- ✅ Preserves origin/predev completely (per requirement)
- ✅ Consolidates Phase 1+2 work into clean summary commits
- ✅ Enables pushing to origin/main with clean history
- ✅ Maintains full rollback capability
- ✅ Non-destructive and verifiable at each step

**Status**: READY FOR EXECUTION (awaiting user approval)

---

**Document Created**: 2026-02-05  
**Scope**: Directory structure + Git history remediation  
**Constraint**: predev branch untouched
