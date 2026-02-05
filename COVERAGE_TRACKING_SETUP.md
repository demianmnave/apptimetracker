# Code Coverage Tracking Setup

**Date**: February 4, 2026  
**Purpose**: Establish coverage infrastructure before Phase 2 unit test ramp  
**Implementation Timeline**: Before Phase 2 begins (Day 1-2 transition)  
**Responsible**: 1 senior developer + 1 DevOps engineer  
**Status**: SCHEDULED FOR SETUP

---

## Overview

Code coverage tracking must be operational before Phase 2 (unit test implementation) begins to ensure ≥70% coverage threshold is met and maintained throughout test development.

**Coverage Goals**:
- ✅ Baseline measurement (current state)
- ✅ Real-time reporting (per test)
- ✅ CI/CD integration (automated enforcement)
- ✅ Trend tracking (over time)
- ✅ Target threshold: ≥70% code coverage

---

## Setup Components

### 1. Coverage Tool: Coverlet

**Selection**: Coverlet (open-source, .NET native, integrates with xUnit/NUnit)

**Installation**:

```bash
cd /workspace/workspace

# Add Coverlet NuGet package
dotnet add src/AppTimeTracker.csproj package coverlet.collector

# Add Coverlet.Console for command-line reporting
dotnet add src/AppTimeTracker.csproj package coverlet.console --version 6.0.0
```

**Alternative**: ReportGenerator for visual reports

```bash
dotnet add src/AppTimeTracker.csproj package ReportGenerator --version 5.2.0
```

---

### 2. Test Framework Configuration

**NUnit Test Project Setup**:

```bash
# Create test project (if not exists)
dotnet new nunit -n AppTimeTracker.Tests -o tests/AppTimeTracker.Tests

# Add to solution
dotnet sln /workspace/workspace/AppTimeTracker.sln add tests/AppTimeTracker.Tests/AppTimeTracker.Tests.csproj

# Add project reference to main app
dotnet add tests/AppTimeTracker.Tests/AppTimeTracker.Tests.csproj reference src/AppTimeTracker.csproj
```

**Test Project File** (`tests/AppTimeTracker.Tests/AppTimeTracker.Tests.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NUnit" Version="4.0.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Moq" Version="4.20.70" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/AppTimeTracker.csproj" />
  </ItemGroup>

</Project>
```

---

### 3. Coverage Collection Configuration

**Create** `runsettings.xml` in repository root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Exclude>[AppTimeTracker.Tests*]*,[NUnit*]*</Exclude>
          <IncludeTestAssembly>false</IncludeTestAssembly>
          <SingleHit>false</SingleHit>
          <UseSourceLink>true</UseSourceLink>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
  <ThresholdLimit>70</ThresholdLimit>
  <ThresholdType>line</ThresholdType>
</RunSettings>
```

---

### 4. CI/CD Integration

**GitHub Actions Workflow** (`/.github/workflows/coverage.yml`):

```yaml
name: Code Coverage

on:
  push:
    branches: [ predev, main ]
  pull_request:
    branches: [ predev, main ]

jobs:
  coverage:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run tests with coverage
      run: dotnet test --no-build --verbosity normal \
        --logger:"console;verbosity=normal" \
        /p:CollectCoverage=true \
        /p:CoverletOutputFormat=cobertura \
        /p:CoverletOutput=./coverage/

    - name: Generate coverage report
      run: |
        dotnet tool install -g reportgenerator
        reportgenerator -reports:"**/coverage.cobertura.xml" \
          -targetdir:"coverage/report" \
          -reporttypes:"Html;HtmlSummary"

    - name: Upload coverage to Codecov
      uses: codecov/codecov-action@v3
      with:
        file: ./coverage/coverage.cobertura.xml
        flags: unittests
        fail_ci_if_error: true
        threshold: 70

    - name: Comment PR with coverage
      if: github.event_name == 'pull_request'
      uses: romeovs/lcov-reporter-action@v0.3.1
      with:
        lcov-file: ./coverage/coverage.cobertura.xml
        github-token: ${{ secrets.GITHUB_TOKEN }}
```

**Local Test Command**:

```bash
# Run tests with coverage collection
dotnet test --settings runsettings.xml \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/

# Generate HTML report
reportgenerator -reports:"coverage/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:"Html;HtmlSummary"

# View report in browser
open coverage/report/index.html
```

---

### 5. Coverage Metrics Baseline

**Baseline Measurement** (before Phase 2):

Create `COVERAGE_BASELINE.md` after running initial coverage:

```markdown
# Code Coverage Baseline

**Date**: February 4, 2026  
**Measurement Point**: Before Phase 2 unit tests

## Metrics

| Metric | Value | Target |
|--------|-------|--------|
| Line Coverage | 0% | ≥70% |
| Branch Coverage | 0% | ≥70% |
| Method Coverage | 0% | ≥70% |

## Coverage by Component

| Component | Coverage | Tests |
|-----------|----------|-------|
| DailyReportService | 0% | 0 |
| UsageRepository | 0% | 0 |
| Worker | 0% | 0 |
| AppDbContext | 0% | 0 |
| Models | N/A | 0 |

## Notes

- Initial measurement taken before Phase 2 unit test implementation
- Target: ≥70% coverage achieved by end of Phase 2
- Coverage tracked per test addition
- Reports generated after each Phase 2 task completion
```

---

## Setup Checklist

### Pre-Phase-2 Activities (Day 1-2)

**Developer Tasks**:
- [ ] Create test project structure
- [ ] Configure Coverlet in test projects
- [ ] Set up runsettings.xml
- [ ] Create test utility base classes
- [ ] Configure mock frameworks (Moq)

**DevOps Tasks**:
- [ ] Create GitHub Actions coverage workflow
- [ ] Set up Codecov integration (if using)
- [ ] Configure branch protection rules:
  - [ ] Require 70% coverage on PRs
  - [ ] Require PR review before merge
  - [ ] Require build to pass
- [ ] Create coverage report dashboard

**Documentation Tasks**:
- [ ] Create test naming conventions guide
- [ ] Document test template structure
- [ ] Create coverage gap analysis process
- [ ] Document Moq usage patterns

---

## Coverage Target Progression

| Phase | Target | Cumulative Tests |
|-------|--------|------------------|
| **Baseline** | 0% | 0 |
| **Phase 2 - 25%** | 25% | ~14 tests |
| **Phase 2 - 50%** | 50% | ~28 tests |
| **Phase 2 - 70%** | ≥70% | 57+ tests |
| **Phase 2 - 100%** | 70%+ | 57 tests |
| **Staging (Phase 4)** | ≥70% maintained | All tests |
| **Production** | ≥70% maintained | All tests |

---

## Coverage Gates

### Phase 2 Entry Gate

✅ **Prerequisite**: Coverage infrastructure operational

- [ ] Test project created and configured
- [ ] Coverlet installed and working
- [ ] Local test execution working
- [ ] CI/CD pipeline configured
- [ ] Baseline metrics recorded
- [ ] Team trained on coverage tools

### Phase 2 Exit Gate

✅ **Requirement**: ≥70% coverage achieved

- [ ] 57 unit tests implemented
- [ ] Coverage report shows ≥70%
- [ ] All coverage gaps identified
- [ ] CI/CD enforces 70% threshold
- [ ] Team commits to maintaining coverage

---

## Monitoring & Reporting

### Daily Metrics (Phase 2)

Track per development day:

```
Day 1: 
  - Tests Added: 8 (14% progress)
  - Coverage: 5%
  - Status: On track

Day 2:
  - Tests Added: 16 (cumulative 24)
  - Coverage: 18%
  - Status: On track

Day 2.5:
  - Tests Added: 57 (complete)
  - Coverage: 72%
  - Status: ✅ Target met
```

### Coverage Report Template

**File**: `PHASE_2_COVERAGE_REPORT.md` (created during Phase 2)

```markdown
# Phase 2 Coverage Report

**Date**: [Date]
**Status**: In Progress / Complete

## Summary

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Test Cases | 57 | [X] | ✅/❌ |
| Line Coverage | 70% | [Y]% | ✅/❌ |
| Branch Coverage | 65% | [Z]% | ✅/❌ |

## Coverage by Component

| Component | Line Coverage | Branch Coverage | Tests |
|-----------|---|---|---|
| DailyReportService | X% | Y% | 21 |
| UsageRepository | X% | Y% | 22 |
| Worker | X% | Y% | 14 |

## Coverage Gaps

[List any uncovered code paths]

## Recommendations

[Notes on coverage improvement opportunities]
```

---

## Tools & Commands Reference

### Build & Test

```bash
# Clean build
dotnet clean && dotnet build

# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test class
dotnet test --filter "Class=AppTimeTracker.Tests.DailyReportServiceTests"

# Verbose output
dotnet test --verbosity normal
```

### Coverage Reporting

```bash
# Generate Cobertura XML
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML report
reportgenerator -reports:"coverage/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:"Html"

# View coverage summary
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"coverage/coverage.cobertura.xml" \
  -targetdir:"coverage/summary" \
  -reporttypes:"HtmlSummary"
```

### Threshold Enforcement

```bash
# .NET 8+ built-in coverage threshold (in runsettings.xml)
<ThresholdLimit>70</ThresholdLimit>
<ThresholdType>line</ThresholdType>

# CI/CD enforcement (GitHub Actions example)
codecov/codecov-action with threshold: 70
```

---

## Timeline: Coverage Setup

**Day 1 Afternoon (after Phase 1)**: Setup infrastructure
- 30 min: Create test project structure
- 30 min: Configure Coverlet and runsettings
- 30 min: Set up GitHub Actions workflow
- 30 min: Create test base classes and utilities
- **Total**: 2 hours

**Day 1 Evening**: Verification
- 30 min: Run test on simple test case
- 30 min: Verify coverage reporting works
- 30 min: Configure Codecov (if using cloud-based)
- **Total**: 1.5 hours

**Day 2 Morning (before Phase 2 starts)**: Team training
- 30 min: Coverage tool walkthrough
- 30 min: Test naming conventions and structure
- 30 min: Mock framework (Moq) basics
- **Total**: 1.5 hours

**Total Setup Time**: 5 hours

---

## Success Criteria

### Coverage Infrastructure ✅

- [ ] Test project created with NUnit/xUnit
- [ ] Coverlet configured and collecting metrics
- [ ] Local coverage reports generate successfully
- [ ] GitHub Actions workflow triggers on PRs
- [ ] CI/CD enforces 70% threshold
- [ ] Team can run coverage locally

### Baseline Documentation ✅

- [ ] `COVERAGE_BASELINE.md` created
- [ ] `PHASE_2_COVERAGE_REPORT.md` template ready
- [ ] Coverage gap analysis process documented
- [ ] Codecov dashboard configured (if used)

### Team Readiness ✅

- [ ] All Phase 2 developers trained on coverage tools
- [ ] Test naming conventions established
- [ ] Mock framework patterns documented
- [ ] Coverage goals communicated

---

## Handoff to Phase 2

**Coverage Infrastructure Ready**: Day 2, 10:00 UTC

**Phase 2 Kickoff**: Day 2, 10:30 UTC

**Phase 2 Team Briefing**:
- Coverage tools overview
- Coverage goals and targets
- How to measure progress
- Q&A session

**Phase 2 Resources**:
- Coverage tracking spreadsheet
- Test template examples
- Mock framework documentation
- Debugging coverage gaps guide

---

## Escalation & Support

**Coverage Questions**: Slack #m4-coverage-tracking
**Tool Issues**: GitHub Issues (tag @devops)
**Architecture Help**: Team lead review
**Blocker**: Escalate to tech lead immediately

---

## Appendix: Configuration Files

### runsettings.xml (Full Version)

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <!-- Code Coverage Configuration -->
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <!-- Cobertura format for CI/CD compatibility -->
          <Format>cobertura</Format>
          
          <!-- Exclude test projects from coverage -->
          <Exclude>[AppTimeTracker.Tests*]*,[NUnit*]*,[Moq*]*</Exclude>
          
          <!-- Don't measure test assembly itself -->
          <IncludeTestAssembly>false</IncludeTestAssembly>
          
          <!-- Count each line only once (not per hit) -->
          <SingleHit>false</SingleHit>
          
          <!-- Use source link for better reports -->
          <UseSourceLink>true</UseSourceLink>
          
          <!-- Include generated files -->
          <IncludeDirectories>src</IncludeDirectories>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
  
  <!-- Coverage Threshold -->
  <ThresholdLimit>70</ThresholdLimit>
  <ThresholdType>line</ThresholdType>
</RunSettings>
```

---

**Coverage Tracking Status**: SCHEDULED FOR SETUP

**Setup Start**: February 4, 2026 (after Phase 1)  
**Setup Complete**: February 5, 2026, 10:00 UTC (before Phase 2)  
**Duration**: 5 hours

**Next Gate**: Phase 2 kickoff with operational coverage tracking
