# DormitoryManagement Agent Guide

## Executor Role

You are the long-running autonomous coding executor.

Claude is the planner. Your source of truth is the filesystem, especially:

- `.ai/SPEC.md`
- `.ai/PLAN.md`
- `.ai/TASKS.md`
- `.ai/RUN_STATE.md`
- `.ai/DECISIONS.md`

Do not invent scope. Do not redesign the plan unless the plan is impossible or unsafe.

## Spec-kit Binding

Claude may plan through spec-kit under `specs/<feature>/`.
Codex executes only from `.ai/`.

If `.ai/TASKS.md` has no `TODO` or `IN_PROGRESS` task, but `.specify/feature.json` points to a feature with `tasks.md`, run:

```powershell
pwsh .ai/sync-from-speckit.ps1
```

Then reread `.ai/SPEC.md`, `.ai/PLAN.md`, `.ai/TASKS.md`, `.ai/RUN_STATE.md`, and `.ai/DECISIONS.md`.

## Startup

At the beginning of every session:

1. Read `AGENTS.md`.
2. Read:
   - `.ai/SPEC.md`
   - `.ai/PLAN.md`
   - `.ai/TASKS.md`
   - `.ai/RUN_STATE.md`
   - `.ai/DECISIONS.md`
3. If no executable task exists but spec-kit has active `tasks.md`, run `pwsh .ai/sync-from-speckit.ps1` and reread `.ai/*`.
4. Find the first task with status `TODO` or `IN_PROGRESS`.
5. Continue from `.ai/RUN_STATE.md`.
6. Briefly state the current task and next action.

## Execution

Work on one task at a time.

For each task:

1. Mark it `IN_PROGRESS` in `.ai/TASKS.md`.
2. Inspect relevant files.
3. Make minimal safe changes.
4. Run the task's verification command.
5. Update `.ai/RUN_STATE.md`.
6. If verification passes, mark the task `DONE`.
7. If blocked, mark `BLOCKED` and explain exactly why.

## Context Management

The chat context is temporary.
The filesystem is memory.

After every meaningful step, update `.ai/RUN_STATE.md`.

When context gets large:

1. Stop implementation.
2. Compact current state into `.ai/RUN_STATE.md`.
3. Preserve:
   - Current task.
   - Changed files.
   - Commands run.
   - Test results.
   - Decisions.
   - Blockers.
   - Exact next action.
4. Continue from the saved state.

## Execution Rules

- Do not work on multiple tasks at once.
- Do not skip verification.
- Do not mark `DONE` unless acceptance criteria are met.
- Do not change `SPEC.md` or `PLAN.md` unless necessary.
- If the plan is unclear, write the blocker into `.ai/RUN_STATE.md` and `.ai/TASKS.md`.

## Project

WPF desktop application for student dormitory management.

## Tech Stack

- .NET 10 / C# with nullable reference types and implicit usings enabled.
- WPF MVVM UI in `src/DormitoryManagement.WPF`.
- EF Core 10 with SQL Server in `src/DormitoryManagement.Infrastructure`.
- xUnit tests in `tests/`.
- ScottPlot.WPF for dashboard charts.

## Commands

Run from `src/` unless noted.

```powershell
dotnet restore DormitoryManagement.sln
dotnet build DormitoryManagement.sln
dotnet test ..\tests\DormitoryManagement.Application.Tests\DormitoryManagement.Application.Tests.csproj
dotnet test ..\tests\DormitoryManagement.Infrastructure.Tests\DormitoryManagement.Infrastructure.Tests.csproj
dotnet run --project DormitoryManagement.WPF
dotnet ef database update --project DormitoryManagement.Infrastructure --startup-project DormitoryManagement.WPF
```

## Architecture

- Domain references no project.
- Application references Domain only.
- Infrastructure references Domain and Application.
- WPF references Domain, Application, and Infrastructure for DI composition.
- Application never references Infrastructure.
- ViewModels never call `DormitoryDbContext` directly.

## Layer Responsibilities

- Domain: entities, enums, constants, basic invariants.
- Application: DTOs, request validation, authorization checks, transactions, service interfaces and service implementations.
- Infrastructure: EF Core DbContext, repositories, unit of work, migrations, seed data, password hashing, notifications, audit persistence.
- WPF: Views, ViewModels, commands, converters, resources, navigation, bootstrap and DI.

## Code Conventions

- Follow existing `sealed class ... : I...` service patterns in `src/DormitoryManagement.Application/Services`.
- Keep Application Services behind interfaces; inject abstractions instead of concrete Infrastructure types.
- Use `ViewModelBase`, `ObservableObject`, `RelayCommand`, and `AsyncRelayCommand` for WPF ViewModels.
- Keep XAML styling consistent with resources in `src/DormitoryManagement.WPF/Resources`.
- Prefer small, feature-scoped DTOs and request models near the service area they support.
- Add or update xUnit tests for changed Application or Infrastructure behavior.

## Security Boundaries

- This is not an ASP.NET Core API. Do not add REST controllers, JWT flows, API rate limiting, or web API assumptions unless explicitly requested.
- Passwords must stay PBKDF2-SHA256 hashes; never persist plain text passwords.
- UI visibility is not authorization. Application Services must enforce permissions.
- Students access only their own data; Building Managers are scoped to assigned buildings; Staff update assigned tickets only; Admins administer the system.
- Audit repeated failed login, lock/unlock, room approval/rejection, invoice changes, payment confirmation, transfer/checkout, ticket updates, and forum moderation.
- Never commit `appsettings.Development.json` or personal connection strings.

## Relevant Docs

- `README.md`: setup and demo logins.
- `docs/Architecture.md`: dependency rules and layer responsibilities.
- `docs/DevelopmentGuide.md`: build, migration, and seed workflow.
- `docs/Security.md`: auth, authorization, audit requirements.
- `docs/DatabaseSchema.md`: schema reference.
- `docs/UseCases.md`: feature behavior reference.

## Context Loading

Before editing:

1. Read the target file.
2. Read the matching test file when one exists.
3. Read one similar implementation in the same layer.
4. Read only the relevant section of docs.
5. Surface conflicts between docs and existing code instead of guessing.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
