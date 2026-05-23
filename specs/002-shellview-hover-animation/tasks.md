# Tasks: ShellView Hover Animation

**Input**: Design documents from `/specs/002-shellview-hover-animation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md, contracts/ui-contract.md

**Tests**: Tests are mandatory for this WPF behavior change. Write focused failing tests before implementation, then make them pass. Manual WPF smoke validation is also required because visual animation and pointer hit-testing cannot be fully proven by unit tests.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency.
- **[Story]**: User story label such as US1, US2, or US3.
- Include exact repository paths in every task description.
- Keep tasks small enough to complete and verify in one step.

## Phase 1: Setup (Shared Context)

**Purpose**: Load exact shell context and confirm WPF-only scope before tests/code.

- [ ] T001 Read `specs/002-shellview-hover-animation/spec.md`, `specs/002-shellview-hover-animation/plan.md`, `specs/002-shellview-hover-animation/contracts/ui-contract.md`, and `specs/002-shellview-hover-animation/quickstart.md`; objective: understand requested hover reveal/collapse behavior; verification: confirm FR-001 through FR-014 are covered in later tasks; acceptance: no requirement is left unmapped.
- [ ] T002 Read `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`, `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs`, `src/DormitoryManagement.WPF/ViewModels/ShellViewModel.cs`, and `src/DormitoryManagement.WPF/Views/Shared/MainWindow.xaml`; objective: identify current fixed 312px sidebar layout and existing navigation bindings; verification: note current authenticated shell grid columns and menu command bindings; acceptance: executor knows which elements must be preserved.
- [ ] T003 [P] Read existing WPF test patterns in `tests/DormitoryManagement.WPF.Tests/RoomRegistrationViewModelTests.cs`, `tests/DormitoryManagement.WPF.Tests/PaymentViewModelTests.cs`, and `tests/DormitoryManagement.WPF.Tests/VehicleRegistrationViewModelTests.cs`; objective: match test project style; verification: identify test naming, fixture, and assertion style; acceptance: new tests use same conventions.
- [ ] T004 [P] Read WPF resource files `src/DormitoryManagement.WPF/Resources/Colors.xaml` and `src/DormitoryManagement.WPF/Resources/Buttons.xaml`; objective: preserve existing ShellView colors/buttons; verification: identify `SidebarBrush`, `SidebarButtonStyle`, and text brushes; acceptance: implementation does not introduce unrelated style systems.

---

## Phase 2: Tests First (Blocking Prerequisites)

**Purpose**: Establish failing tests/checks for changed shell behavior before implementation.

**CRITICAL**: Implementation tasks MUST NOT start until the required tests fail for the intended reason or a manual-test-only limitation is documented in `specs/002-shellview-hover-animation/quickstart.md`.

- [ ] T005 [P] Create `tests/DormitoryManagement.WPF.Tests/ShellViewAnimationTests.cs`; objective: add tests for shell animation constants/state helper if extracted; implementation steps: define tests for hidden offset, open offset, open duration <= 350ms, close duration <= 300ms, hover trigger width near 8px, and close delay behavior; verification: `dotnet test ..\tests\DormitoryManagement.WPF.Tests\DormitoryManagement.WPF.Tests.csproj` from `src/`; acceptance: tests compile and fail because production constants/helper do not exist yet.
- [ ] T006 [P] Add XAML contract checklist section to `specs/002-shellview-hover-animation/quickstart.md`; objective: record manual checks for hit-testing, flicker, keyboard access, and navigation; implementation steps: add checkboxes for left-edge reveal, in-shell stay-open, pointer-leave hide, rapid re-entry cancellation, hidden content click/scroll, and keyboard navigation; verification: inspect quickstart; acceptance: manual validation can be executed without chat history.
- [ ] T007 Run WPF test command from `src/`: `dotnet test ..\tests\DormitoryManagement.WPF.Tests\DormitoryManagement.WPF.Tests.csproj`; objective: confirm new tests are red before implementation; verification: command output shows failures tied to missing shell animation behavior/helper; acceptance: failures are expected and not unrelated build errors.

**Checkpoint**: Required tests/checklists are red/ready and meaningful.

---

## Phase 3: Foundation (Shared Implementation)

**Purpose**: Add shared WPF shell animation primitives that all user stories use.

- [ ] T008 Create or update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs`; objective: define reusable constants/helper state for `SidebarWidth = 312`, `HiddenOffset = -312` or a Whallie-like offscreen offset, `HotspotWidth = 8`, `OpenDuration <= 350ms`, `CloseDuration <= 300ms`, and short close delay; implementation steps: keep code-behind limited to view interaction/animation; verification: `dotnet test ..\tests\DormitoryManagement.WPF.Tests\DormitoryManagement.WPF.Tests.csproj` from `src/`; acceptance: tests from T005 can reference or validate deterministic values.
- [ ] T009 Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: restructure authenticated shell layout so main content occupies full width while sidebar can overlay/slide; implementation steps: remove fixed left content dependency from main layout, keep `TopBar`, `ContentControl`, menu `ItemsControl`, user card, and existing bindings intact; verification: XAML compiles in WPF project; acceptance: authenticated main content no longer depends on permanent 312px left column.
- [ ] T010 Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: name sidebar panel and add transform-ready structure; implementation steps: add stable element names for sidebar panel and left-edge hotspot, attach `TranslateTransform` to sidebar, set initial hidden position, preserve `SidebarBrush` and existing `SidebarButtonStyle`; verification: XAML compiles; acceptance: sidebar can be animated without changing `ShellViewModel` navigation bindings.
- [ ] T011 Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs`; objective: add minimal show/hide methods and close-delay timer; implementation steps: implement `ShowSidebar`, `ScheduleCloseSidebar`, `CloseSidebar`, cancel pending close on re-entry, guard repeated animation starts, and keep handler names matched to XAML; verification: WPF tests from T005 pass deterministic state checks; acceptance: no Domain/Application/Infrastructure files are changed.

**Checkpoint**: Shared animation plumbing compiles and deterministic tests pass.

---

## Phase 4: User Story 1 - Reveal ShellView from left edge hover (Priority: P1)

**Goal**: User hovers far-left edge and ShellView slides into view smoothly.

**Independent Test**: From any authenticated screen, hover the far-left edge; ShellView starts reveal within 100ms and finishes open within 350ms.

- [ ] T012 [US1] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: add left-edge hotspot over authenticated shell; implementation steps: create narrow transparent hit-testable element aligned to far-left edge, visible only when authenticated shell is visible, bind no business data, avoid blocking main content beyond hotspot width; verification: build WPF project; acceptance: hidden state leaves main content usable except hotspot.
- [ ] T013 [US1] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: add open storyboard or equivalent animation; implementation steps: animate sidebar transform from hidden-left to visible position using quick ease-out timing matching `contracts/ui-contract.md`; verification: run WPF app manual smoke steps 3-5 in `quickstart.md`; acceptance: ShellView reveal visually matches Whallie-like slide feel.
- [ ] T014 [US1] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs`; objective: wire hotspot hover to reveal behavior; implementation steps: handle hotspot pointer/mouse enter, cancel pending close, call `ShowSidebar`, ignore duplicate show calls while open/opening; verification: run WPF test project and manual smoke step 4; acceptance: hover at far-left edge reveals ShellView reliably.
- [ ] T015 [US1] Validate User Story 1 with `specs/002-shellview-hover-animation/quickstart.md`; objective: prove edge-hover reveal independently; implementation steps: check reveal trigger, open timing, and hidden content hit-testing; verification: `dotnet build DormitoryManagement.sln` and quickstart steps 3-5 from `src/`; acceptance: US1 acceptance scenarios pass.

**Checkpoint**: User Story 1 works independently and is MVP-ready.

---

## Phase 5: User Story 2 - Keep ShellView visible while pointer is inside it (Priority: P2)

**Goal**: ShellView remains visible while user moves within it and selects navigation items.

**Independent Test**: Reveal ShellView, move pointer across menu/user card areas, pause, and click a navigation item; ShellView stays open until pointer leaves.

- [ ] T016 [US2] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: attach pointer enter/leave boundaries to the full sidebar surface; implementation steps: ensure menu scroll area, header, user card, and all sidebar content count as inside ShellView; verification: manual smoke steps 6-7; acceptance: pointer movement within sidebar never triggers premature close.
- [ ] T017 [US2] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs`; objective: keep ShellView visible while pointer is inside; implementation steps: cancel close timer on sidebar enter, track open state, avoid closing during navigation button click, keep event handlers UI-only; verification: run WPF test project; acceptance: sidebar remains visible during in-panel hover/pause.
- [ ] T018 [US2] Verify `src/DormitoryManagement.WPF/ViewModels/ShellViewModel.cs` remains behavior-compatible; objective: preserve role-specific menu items and `NavigateCommand`; implementation steps: inspect no command binding regression, avoid changing role menu construction unless compile requires it; verification: manual click one menu item after reveal; acceptance: navigation works exactly as before.
- [ ] T019 [US2] Validate User Story 2 with `specs/002-shellview-hover-animation/quickstart.md`; objective: prove stay-open behavior independently; implementation steps: move across all sidebar regions, pause, click navigation; verification: quickstart steps 6-7; acceptance: US2 acceptance scenarios pass.

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 6: User Story 3 - Hide ShellView after pointer leaves it (Priority: P3)

**Goal**: ShellView hides smoothly after pointer leaves and does not flicker or get stuck.

**Independent Test**: Reveal ShellView, move pointer out, confirm smooth close within 300ms; rapidly re-enter during close and confirm it stays visible/reopens.

- [ ] T020 [US3] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: add close storyboard or equivalent animation; implementation steps: animate sidebar transform from visible position to hidden-left using quick ease-in timing matching `contracts/ui-contract.md`; verification: manual smoke step 8; acceptance: ShellView hides smoothly without layout jump.
- [ ] T021 [US3] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs`; objective: implement delayed close and re-entry cancellation; implementation steps: start short timer on sidebar leave, close only if pointer is outside sidebar/hotspot when timer fires, cancel close on hotspot/sidebar enter, handle rapid transitions without duplicate timers; verification: WPF tests and manual smoke step 9; acceptance: no flicker, no stuck half-open state.
- [ ] T022 [US3] Update `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml`; objective: ensure hidden sidebar hit-testing does not block main content; implementation steps: disable hit-test or visibility for hidden sidebar surface while leaving hotspot active; verification: manual smoke step 10; acceptance: main content remains clickable/scrollable while ShellView hidden.
- [ ] T023 [US3] Validate keyboard access in `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml` and `ShellView.xaml.cs`; objective: preserve non-hover access path; implementation steps: ensure focusable navigation remains reachable by keyboard when shell is visible and no hover-only trap prevents access; verification: manual smoke step 11; acceptance: keyboard-only user can still reach ShellView navigation.
- [ ] T024 [US3] Validate User Story 3 with `specs/002-shellview-hover-animation/quickstart.md`; objective: prove hide and rapid re-entry behavior independently; implementation steps: run close, rapid transition, hidden hit-test, and keyboard checks; verification: quickstart steps 8-11; acceptance: US3 acceptance scenarios pass.

**Checkpoint**: All planned user stories work independently.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final checks across all stories.

- [ ] T025 [P] Review `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml` for binding errors, lost resources, broken `DataTemplate` mappings, and unchanged `TopBar`/content rendering; verification: run WPF app and navigate through at least Login, Dashboard, Billing or Vehicles; acceptance: no visible binding errors or missing views.
- [ ] T026 [P] Review `src/DormitoryManagement.WPF/Views/Shared/ShellView.xaml.cs` for UI-only code-behind scope; verification: confirm no `DormitoryDbContext`, Application service calls, or business authorization logic added; acceptance: code-behind only controls animation/pointer state.
- [ ] T027 Verify no ASP.NET Core API, REST controller, JWT, config secret, migration, or database change was added; files to inspect: `src/`, `tests/`, `docs/`, `.gitignore`; verification: `git diff --name-only`; acceptance: changes are limited to WPF/tests/spec artifacts unless justified.
- [ ] T028 Run from `src/`: `dotnet build DormitoryManagement.sln`; objective: compile all projects; acceptance: build passes or blocker is documented with exact error.
- [ ] T029 Run from `src/`: `dotnet test ..\tests\DormitoryManagement.WPF.Tests\DormitoryManagement.WPF.Tests.csproj`; objective: verify WPF test coverage; acceptance: tests pass or blocker is documented with exact error.
- [ ] T030 Run manual smoke validation from `specs/002-shellview-hover-animation/quickstart.md`; objective: verify visual animation and pointer behavior; acceptance: all quickstart manual steps pass.
- [ ] T031 Update `specs/002-shellview-hover-animation/quickstart.md` with final validation notes if any manual step needs environment-specific caveat; verification: quickstart remains executable without chat history; acceptance: no hidden assumptions remain.
- [ ] T032 Run `pwsh .ai/sync-from-speckit.ps1` from repo root; objective: mirror final `tasks.md` into `.ai/TASKS.md` for Codex; acceptance: `.ai/TASKS.md` references `specs/002-shellview-hover-animation` and contains TODO tasks.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Tests First (Phase 2)**: Depends on Setup; blocks implementation.
- **Foundation (Phase 3)**: Depends on failing tests; blocks user stories.
- **US1 Reveal (Phase 4)**: Depends on Foundation.
- **US2 Stay Visible (Phase 5)**: Depends on US1 reveal plumbing.
- **US3 Hide (Phase 6)**: Depends on US1/US2 open-state and pointer-boundary behavior.
- **Polish (Phase 7)**: Depends on selected user stories.

### User Story Dependencies

- **User Story 1 (P1)**: MVP. Can ship only reveal if hide is not yet implemented, but ShellView may remain open until later stories.
- **User Story 2 (P2)**: Requires US1 visible sidebar; independently validates in-panel hover/navigation.
- **User Story 3 (P3)**: Requires US1/US2 state tracking; independently validates close/flicker/hit-testing.

### Parallel Opportunities

- T003 and T004 can run parallel with T001/T002 after feature docs are available.
- T005 and T006 can run parallel; different files.
- T025 and T026 can run parallel during polish; different files.
- Manual smoke and code review should not run before T028/T029 pass.

## Parallel Execution Examples

### US1

```text
Task: T012 update hotspot in ShellView.xaml
Task: T014 wire hotspot hover in ShellView.xaml.cs
```

These can be drafted in parallel after T008-T011 if both agents coordinate element names first.

### US2

```text
Task: T016 attach sidebar pointer boundaries in ShellView.xaml
Task: T018 verify ShellViewModel navigation compatibility
```

These are parallelizable because one edits XAML and the other inspects ViewModel compatibility.

### US3

```text
Task: T020 add close animation in ShellView.xaml
Task: T023 validate keyboard access in ShellView.xaml and ShellView.xaml.cs
```

These can run in parallel if animation element names are already stable.

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete T001-T011.
2. Complete T012-T015.
3. Verify ShellView reveals from left edge and main content remains usable when hidden.

### Incremental Delivery

1. Add US1 reveal behavior.
2. Add US2 stay-open behavior and navigation regression validation.
3. Add US3 hide/re-entry/hit-test behavior.
4. Finish polish checks and sync `.ai/`.

### Review Gate

Before handoff, verify:

- No Domain/Application/Infrastructure files changed unless a documented compile necessity exists.
- `ShellViewModel` role menu logic remains unchanged or behavior-equivalent.
- No web/API/JWT assumptions added.
- Build and WPF tests pass.
- Manual quickstart smoke passes.
