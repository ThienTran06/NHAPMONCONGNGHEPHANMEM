# Implementation Plan: ShellView Hover Animation

**Branch**: `002-shellview-hover-animation` | **Date**: 2026-05-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-shellview-hover-animation/spec.md`

## Summary

Add global ShellView hover reveal/collapse behavior for the authenticated WPF shell. The shell starts hidden, an approximately 8-pixel left-edge trigger reveals it with Whallie-like slide animation, it stays open while the pointer is inside, and it hides after pointer leave without flicker. Scope is WPF shell UI only; no domain, application service, persistence, or schema changes.

## Technical Context

**Language/Version**: .NET 10 / C# with nullable reference types and implicit usings

**Primary Dependencies**: WPF MVVM, xUnit WPF tests; existing shared WPF resources and MVVM primitives

**Storage**: None for this feature. No SQL Server, EF Core, repositories, or migrations involved.

**Testing**: WPF xUnit tests for shell animation state/view-model-adjacent behavior where practical; manual WPF smoke validation for hover animation timing, flicker, pointer leave, keyboard access, and navigation regression.

**Target Platform**: Windows desktop WPF application

**Project Type**: Desktop app with layered Domain, Application, Infrastructure, and WPF projects

**Performance Goals**: ShellView begins reveal within 100ms of hover, completes open within 350ms, completes close within 300ms, and remains responsive during rapid pointer transitions.

**Constraints**: No ASP.NET Core API, REST controllers, JWT flow, persistence changes, authorization rule changes, or unrelated shell/navigation refactors. WPF ViewModels must not call `DormitoryDbContext`.

**Scale/Scope**: Global authenticated ShellView behavior in `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml` and `ShellView.xaml.cs`, preserving existing navigation/menu role behavior from `ShellViewModel`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layered MVVM boundaries**: PASS. WPF-only interaction; no Domain/Application/Infrastructure dependency changes.
- **Application authorization**: PASS. Existing service-enforced authorization remains unchanged; role-specific ShellView menu visibility preserved.
- **Security and secrets**: PASS. No secrets, password handling, config, or web auth changes.
- **Auditability**: PASS. No audited dormitory business event changes.
- **Test-first delivery**: PASS. Tasks require WPF tests/validation before implementation.
- **Data integrity**: PASS. No schema, transactions, money, or constraints touched.
- **WPF UX**: PASS. Uses existing shell view/resources; includes hover, keyboard accessibility, and smoke validation.
- **Documentation**: PASS. Feature plan/quickstart/contracts document behavior; no user docs required unless implementation changes demo checklist.

## Project Structure

### Documentation (this feature)

```text
specs/002-shellview-hover-animation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ui-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
└── DormitoryManagement.WPF/
    ├── Views/Shared/
    │   ├── ShellView.xaml
    │   └── ShellView.xaml.cs
    └── ViewModels/
        └── ShellViewModel.cs

tests/
└── DormitoryManagement.WPF.Tests/
    └── ShellViewAnimationTests.cs
```

**Structure Decision**: Implement animation and pointer handling in `ShellView.xaml` plus minimal view lifecycle code in `ShellView.xaml.cs`, because the behavior is view interaction/animation rather than dormitory business logic. Keep `ShellViewModel.cs` unchanged unless implementation needs bindable shell-open state for testability; do not move hover animation into Application services.

## Complexity Tracking

No constitution violations.

## Phase 0: Research

See [research.md](./research.md). Decisions resolved: Whallie-like timing/easing, view-owned pointer behavior, no persistence/model changes, WPF validation strategy.

## Phase 1: Design & Contracts

See [data-model.md](./data-model.md), [contracts/ui-contract.md](./contracts/ui-contract.md), and [quickstart.md](./quickstart.md).

## Phase 2: Task Planning Approach

Tasks must be generated in test-first order:

1. Add WPF test coverage for shell hover state transitions or extracted animation controller behavior.
2. Update `ShellView.xaml` layout to overlay hidden sidebar over content instead of fixed left column.
3. Add left-edge hover trigger, sidebar transform, open/close storyboards, and hit-test boundaries.
4. Add minimal `ShellView.xaml.cs` handlers/timer for show, delayed hide, cancellation, and keyboard-safe behavior.
5. Verify navigation/menu behavior still works for authenticated roles.
6. Run WPF tests/build and manual smoke from the WPF app.

## Post-Design Constitution Check

- **Layered MVVM boundaries**: PASS. Design remains WPF-only.
- **Application authorization**: PASS. No authorization changes; ShellView menu role filtering remains existing `ShellViewModel` responsibility.
- **Security and secrets**: PASS. No config/secrets/auth changes.
- **Auditability**: PASS. No audit-domain changes.
- **Test-first delivery**: PASS. Task plan starts with WPF tests/validation.
- **Data integrity**: PASS. No persistence changes.
- **WPF UX**: PASS. Design preserves navigation, keyboard access, content hit-testing, and flicker handling.
- **Documentation**: PASS. Spec-kit artifacts cover feature behavior.
