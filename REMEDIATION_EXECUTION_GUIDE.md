# Remediation Execution Guide - Step-by-Step

**Status**: READY FOR EXECUTION  
**Date**: February 5, 2026  
**Duration**: ~30 minutes  
**Risk Level**: LOW (fully reversible)

---

## Quick Start Summary

This guide walks through 6 parts to fix the directory structure and git history:

1. **Verify Current State** (5 min) - Document baseline
2. **Fix Directory Structure** (5 min) - Move .git to root
3. **Create Cleanup Branch** (5 min) - Prepare history work
4. **Clean Git History** (10 min) - Squash commits on separate branch
5. **Verification** (5 min) - Confirm remediation success
6. **Cleanup** (5 min) - Archive backups

**Total Time**: ~30 minutes  
**Risk**: LOW (fully reversible with backups)  
**Predev Status**: NEVER TOUCHED ✅

---

## Key Principles

✅ **Non-Destructive**: All changes backed up  
✅ **Separate Branch**: History cleanup on `history/cleanup-divergence` only  
✅ **Predev Untouched**: origin/predev never modified  
✅ **Verifiable**: Each step can be verified  
✅ **Reversible**: Rollback procedure available  

---

## PART 1: Verify Current State (5 minutes)

```bash
cd /workspace/workspace

# Check current branch
git branch --show-current
# Expected: 0.2.0-pre or similar

# Verify .git location
git rev-parse --git-dir
# Expected: .git

# Check unpushed commits
git log origin/predev..HEAD --oneline | wc -l
# Expected: 21+

# Document state
git status
git log --oneline | head -10
```

---

## PART 2: Fix Directory Structure (5 minutes)

### 2.1 Create Backup

```bash
cd /workspace

# Backup current .git
cp -r /workspace/workspace/.git /workspace/.git-backup

# Verify backup
git -C /workspace/.git-backup rev-parse --git-dir > /dev/null && echo "✅ Backup OK"
```

### 2.2 Create New .git at Root

```bash
cd /workspace

# Copy to new location
cp -r /workspace/workspace/.git /workspace/.git-new

# Verify new location
git -C /workspace/.git-new rev-parse --git-dir > /dev/null && echo "✅ New .git OK"
```

### 2.3 Swap .git Directories

```bash
cd /workspace

# Preserve old .git
mv /workspace/workspace/.git /workspace/workspace/.git-old

# Activate new .git
mv /workspace/.git-new /workspace/.git

# Verify from new location
git status
# Expected: shows Phase 1/2 files
```

### 2.4 Verify Project Files

```bash
cd /workspace

# Check all project files accessible
ls -la /workspace/src | head -3        # ✅
ls -la /workspace/tests | head -3      # ✅
ls -la /workspace/.github | head -3    # ✅
ls -la /workspace/tracker.sln          # ✅

git status  # ✅
```

### 2.5 Archive Old Structure

```bash
cd /workspace

# Move old nested directory to backup
mv /workspace/workspace /workspace/workspace-old-structure

echo "✅ Directory structure fixed"
```

---

## PART 3: Create Cleanup Branch (5 minutes)

```bash
cd /workspace

# Create branch from current HEAD
git checkout -b history/cleanup-divergence

# Verify branch
git branch --show-current
# Expected: history/cleanup-divergence

# Check commits ahead of main
git log origin/main..HEAD --oneline | wc -l
# Expected: 21+
```

---

## PART 4: Clean Git History (10 minutes)

### 4.1 Find Base Commit

```bash
cd /workspace

# Find last common commit with main
BASE=$(git merge-base origin/main HEAD)
echo "Base commit: $BASE"

# Show commits to be squashed
git log $BASE..HEAD --oneline
# Will show all Phase 1+2 commits
```

### 4.2 Interactive Rebase

```bash
cd /workspace

# Start interactive rebase
BASE=$(git merge-base origin/main HEAD)
git rebase -i $BASE
```

**In the editor that opens:**

```
# Keep Phase 1 first commit, squash rest
pick abc1234 feat: Phase 1 implementation...
squash def5678 feat: Phase 1 continuation...
squash ghi9012 feat: Phase 1 cleanup...

# Keep Phase 2 first commit, squash rest
pick jkl3456 feat: Phase 2 tests...
squash mno7890 feat: Phase 2 infrastructure...
squash pqr3456 docs: Phase 2 summary...
```

**When prompted for commit message, use:**

```
Phase 1: Code Quality & Configuration

- Extract FormatDuration to DurationFormatter utility (DRY)
- Externalize MinSessionDurationSeconds to SessionTrackingSettings
- Remove redundant transaction wrapping in UsageRepository
- Add composite index (SessionDate, ProcessName) for optimization
- Add input validation for topCount parameter

Phase 2: Unit Tests & Code Coverage

- Implement 95+ unit tests across 4 test files
- DurationFormatter: 26 tests (format validation, edge cases)
- UsageRepository: 24 tests (CRUD, aggregation, queries)
- SessionTrackingSettings: 21 tests (configuration binding, DI)
- DailyReportService: 24 tests (report generation, validation)
- Setup NUnit, Moq, Coverlet with CI/CD integration
```

### 4.3 Verify History Cleaned

```bash
cd /workspace

# Check final history
git log --oneline | head -5
# Expected: 2-3 squashed commits, then origin/main

# Check diff size
git diff --stat origin/main | tail -1
# Should show reasonable number of files

# Verify clean working tree
git status
# Expected: working tree clean
```

---

## PART 5: Verification (5 minutes)

```bash
cd /workspace

echo "=== VERIFICATION CHECKLIST ==="

# 1. Directory structure
[ -f /workspace/.git/HEAD ] && echo "✅ .git at root" || echo "❌ .git missing"

# 2. Project files
[ -d /workspace/src ] && echo "✅ src/ present" || echo "❌ src/ missing"
[ -d /workspace/tests ] && echo "✅ tests/ present" || echo "❌ tests/ missing"
[ -f /workspace/tracker.sln ] && echo "✅ tracker.sln present" || echo "❌ sln missing"

# 3. Git status
git status | grep -q "working tree clean" && echo "✅ Working tree clean" || echo "⚠️ Uncommitted changes"

# 4. Branch
[ "$(git branch --show-current)" = "history/cleanup-divergence" ] && echo "✅ On cleanup branch" || echo "❌ Wrong branch"

# 5. Commits
COUNT=$(git log origin/main..HEAD --oneline | wc -l)
[ $COUNT -le 3 ] && echo "✅ History cleaned ($COUNT commits)" || echo "⚠️ Still multiple commits"

# 6. Predev untouched
git log origin/predev --oneline | head -1 | grep -q "f225290" && echo "✅ Predev untouched" || echo "❌ Predev changed"

echo "=== VERIFICATION COMPLETE ==="
```

---

## PART 6: Cleanup (5 minutes)

```bash
cd /workspace

# Optional: Remove backup files (ONLY after full verification)
# 
# rm -rf /workspace/.git-backup
# rm -rf /workspace/workspace-old-structure
#
# Or keep for safety - they can be deleted later anytime

echo "✅ Remediation Complete"
```

---

## If Problems Occur - Rollback

```bash
cd /workspace

# Delete problem branch
git branch -D history/cleanup-divergence

# Restore original .git
rm -rf /workspace/.git
mv /workspace/.git-backup /workspace/.git

# Restore nested structure
rm -rf /workspace/workspace
mv /workspace/workspace-old-structure /workspace/workspace

cd /workspace/workspace
mv .git-old .git
git status

echo "✅ Rollback complete"
```

---

## Next Steps After Remediation

Once remediation is complete and verified:

```bash
# 1. Switch to main branch
git checkout main

# 2. Merge cleanup branch
git merge history/cleanup-divergence

# 3. Push to origin/main
git push origin main

# 4. Delete cleanup branch (optional, but clean)
git branch -d history/cleanup-divergence

# 5. Clean up old structure (optional, after final verification)
rm -rf /workspace/.git-backup
rm -rf /workspace/workspace-old-structure
```

---

## Support

- See `REMEDIATION_PLAN.md` for detailed rationale
- See `REMEDIATION_COMPLETION_SUMMARY.md` for results
- All backups preserved for safety
- Rollback procedure available anytime

**Status**: ✅ Ready to Execute
