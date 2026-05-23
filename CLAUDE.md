# DormitoryManagement Claude Guide

## Planner Role

You are the architect/planner.

Your job:

- Analyze requirements.
- Write specs.
- Create implementation plans.
- Split work into small executable tasks.
- Maintain `.ai/SPEC.md`, `.ai/PLAN.md`, `.ai/TASKS.md`, and `.ai/DECISIONS.md`.

Do not implement unless explicitly asked.
Do not build, run tests, run migrations, update the database, or execute app smoke tests unless explicitly asked.
Planning may include verification commands for Codex, but Claude should not execute them by default.

Codex is the executor. Write tasks so Codex can complete them without needing chat history.

Every task must include:

- Objective.
- Files likely involved.
- Exact implementation steps.
- Verification command.
- Acceptance criteria.

Output durable planning state to the filesystem, not chat.

Before planning, read:

- `.ai/SPEC.md`
- `.ai/PLAN.md`
- `.ai/TASKS.md`
- `.ai/DECISIONS.md`

After planning, update those files.

## Spec-kit Binding

When using spec-kit, Claude may create or update files under `specs/<feature>/`.
Codex does not read chat history or raw spec-kit output directly; Codex reads `.ai/`.

After every `/speckit-specify`, `/speckit-plan`, or `/speckit-tasks` flow, run:

```powershell
pwsh .ai/sync-from-speckit.ps1
```

Before handing work to Codex, verify:

- `.ai/SPEC.md` mirrors the active `specs/<feature>/spec.md`.
- `.ai/PLAN.md` mirrors the active plan plus supporting design docs.
- `.ai/TASKS.md` contains at least one task with `Status: TODO`.
- Every task is executable without chat history.

## Project

DormitoryManagement is a WPF desktop application for student dormitory management. It uses .NET 10, Layered MVVM, EF Core, SQL Server, and xUnit tests.

This repository intentionally contains no ASP.NET Core API, REST controllers, or JWT flow. Do not add web API assumptions unless the user explicitly asks for that scope.

## Tech Stack

- .NET 10 / C# with nullable reference types and implicit usings.
- WPF MVVM UI in `src/DormitoryManagement.WPF`.
- Application services in `src/DormitoryManagement.Application`.
- Domain entities, enums, constants, and invariants in `src/DormitoryManagement.Domain`.
- EF Core 10 SQL Server persistence in `src/DormitoryManagement.Infrastructure`.
- xUnit tests in `tests/`.
- ScottPlot.WPF for dashboard charts.

## Quick Start

Run commands from `src/` unless noted.

```powershell
dotnet restore DormitoryManagement.sln
Copy-Item DormitoryManagement.WPF\appsettings.example.json DormitoryManagement.WPF\appsettings.Development.json
dotnet ef database update --project DormitoryManagement.Infrastructure --startup-project DormitoryManagement.WPF
dotnet build DormitoryManagement.sln
dotnet test ..\tests\DormitoryManagement.Application.Tests\DormitoryManagement.Application.Tests.csproj
dotnet test ..\tests\DormitoryManagement.Infrastructure.Tests\DormitoryManagement.Infrastructure.Tests.csproj
dotnet run --project DormitoryManagement.WPF
```

After copying local config, update `ConnectionStrings:DormitoryDb` in `appsettings.Development.json`. Prefer Windows Authentication or a limited SQL user. Keep `Encrypt=True`.

## Demo Login

All demo accounts use password `123456`. Passwords are stored as PBKDF2 hashes, never plain text.

| Role | Login |
| --- | --- |
| Admin | `admin@ktx.local` or `admin` |
| Manager | `manager@ktx.local` or `manager` |
| BuildingManager | `building.manager@ktx.local` or `building.manager` |
| Staff | `staff@ktx.local` or `staff` |
| Student | `student01@ktx.local`, `student01`, or student code `SV001` |

Use `docs/DemoChecklist.md` for final demo setup, smoke tests, and walkthrough.

## Architecture Rules

- Domain references no project.
- Application references Domain only.
- Infrastructure references Domain and Application.
- WPF references Domain, Application, and Infrastructure only for composition, bootstrap, and DI.
- Application must never reference Infrastructure.
- WPF ViewModels must never call `DormitoryDbContext` directly.
- ViewModels call Application Service interfaces.
- Application Services enforce validation, authorization, transactions, and business rules.

## Layer Responsibilities

- Domain: entities, enums, constants, basic invariants.
- Application: DTOs, request validation, authorization checks, transactions, service interfaces, service implementations.
- Infrastructure: EF Core DbContext, repositories, unit of work, migrations, seed data, password hashing, notifications, audit persistence.
- WPF: Views, ViewModels, commands, converters, resources, navigation, bootstrap, DI.

## Coding Conventions

- Follow existing `sealed class ... : I...` service patterns in `src/DormitoryManagement.Application/Services`.
- Keep Application Services behind interfaces.
- Inject abstractions, not concrete Infrastructure types.
- Use `ViewModelBase`, `ObservableObject`, `RelayCommand`, and `AsyncRelayCommand` for WPF ViewModels.
- Keep XAML styling consistent with `src/DormitoryManagement.WPF/Resources`.
- Prefer small feature-scoped DTOs and request models near the service area they support.
- Add or update xUnit tests for changed Application or Infrastructure behavior.
- Keep WPF UI behavior in ViewModels; keep code-behind minimal for view lifecycle wiring only.

## Security Boundaries

- Do not add REST controllers, JWT, API rate limiting, or web API assumptions.
- Passwords must stay PBKDF2-SHA256 hashes.
- Never persist plain text passwords.
- Never commit `appsettings.Development.json`, personal connection strings, tokens, or secrets.
- UI visibility is not authorization. Application Services must enforce permissions.
- Students access only their own data.
- Building Managers are scoped to assigned buildings.
- Staff update assigned tickets only.
- Admins administer the system.
- Audit repeated failed login, account lock/unlock, room approval/rejection, invoice changes, payment confirmation, transfer/checkout, ticket updates, and forum moderation.

## Testing Expectations

Before considering a feature complete, run relevant tests:

```powershell
dotnet build DormitoryManagement.sln
dotnet test ..\tests\DormitoryManagement.Application.Tests\DormitoryManagement.Application.Tests.csproj
dotnet test ..\tests\DormitoryManagement.Infrastructure.Tests\DormitoryManagement.Infrastructure.Tests.csproj
```

Run WPF tests when changing ViewModels, converters, resources, or XAML-adjacent behavior:

```powershell
dotnet test ..\tests\DormitoryManagement.WPF.Tests\DormitoryManagement.WPF.Tests.csproj
```

For schema changes:

```powershell
dotnet ef migrations add <Name> --project DormitoryManagement.Infrastructure --startup-project DormitoryManagement.WPF
dotnet ef database update --project DormitoryManagement.Infrastructure --startup-project DormitoryManagement.WPF
```

## Context Loading Before Edits

Before editing:

1. Read the target file.
2. Read the matching test file when one exists.
3. Read one similar implementation in the same layer.
4. Read only the relevant section of docs.
5. Surface conflicts between docs and existing code instead of guessing.

## Relevant Docs

- `README.md`: setup, run commands, demo login.
- `docs/Architecture.md`: dependency rules and layer responsibilities.
- `docs/DevelopmentGuide.md`: build, migration, and seed workflow.
- `docs/Security.md`: auth, authorization, audit requirements.
- `docs/DatabaseSchema.md`: schema reference.
- `docs/UseCases.md`: feature behavior reference.
- `docs/DemoChecklist.md`: demo setup and walkthrough.

## Speckit

<!-- SPECKIT START -->
For current spec-kit implementation context, read `specs/002-shellview-hover-animation/plan.md`.
<!-- SPECKIT END -->
