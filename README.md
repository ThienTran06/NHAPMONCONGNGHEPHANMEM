# DormitoryManagement

WPF desktop application skeleton for student dormitory management, built with .NET 10, Layered MVVM, EF Core, and SQL Server.

## Quick Start

1. Restore packages:

   ```powershell
   cd src
   dotnet restore DormitoryManagement.sln
   ```

2. Create local configuration:

   ```powershell
   Copy-Item DormitoryManagement.WPF\appsettings.example.json DormitoryManagement.WPF\appsettings.Development.json
   ```

3. Update `ConnectionStrings:DormitoryDb` in `appsettings.Development.json`. Prefer Windows Authentication or a SQL user with limited permissions. Keep `Encrypt=True`.

4. Create migrations and database:

   ```powershell
   dotnet ef database update --project DormitoryManagement.Infrastructure --startup-project DormitoryManagement.WPF
   ```

5. Build and run:

   ```powershell
   dotnet build DormitoryManagement.sln
   dotnet test ..\tests\DormitoryManagement.Application.Tests\DormitoryManagement.Application.Tests.csproj
   dotnet test ..\tests\DormitoryManagement.Infrastructure.Tests\DormitoryManagement.Infrastructure.Tests.csproj
   dotnet run --project DormitoryManagement.WPF
   ```

This repository intentionally contains no ASP.NET Core API, no REST controllers, and no JWT flow. WPF ViewModels call Application Service interfaces only.

## Demo Login

All demo accounts use password `123456`; passwords are stored as PBKDF2 hashes, never plain text.

| Role | Login |
| --- | --- |
| Admin | `admin@ktx.local` or `admin` |
| Manager | `manager@ktx.local` or `manager` |
| BuildingManager | `building.manager@ktx.local` or `building.manager` |
| Staff | `staff@ktx.local` or `staff` |
| Student | `student01@ktx.local`, `student01`, or student code `SV001` |

## Demo Flow

Use [docs/DemoChecklist.md](docs/DemoChecklist.md) for the final demo setup, smoke-test checklist, and walkthrough script.
