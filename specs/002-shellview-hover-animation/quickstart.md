# Quickstart: ShellView Hover Animation

## Prerequisites

- From repo root, local app config exists if running the WPF app.
- Use demo login from `docs/DemoChecklist.md` if manual app login is needed.

## Verification Commands

Run from `src/`:

```powershell
dotnet build DormitoryManagement.sln
dotnet test ..\tests\DormitoryManagement.WPF.Tests\DormitoryManagement.WPF.Tests.csproj
```

## Manual Smoke Validation

1. Run WPF app:

```powershell
dotnet run --project DormitoryManagement.WPF
```

2. Log in with any demo account.
3. Confirm ShellView starts hidden or non-blocking for main content.
4. Move pointer to the far-left edge of the window.
5. Confirm ShellView slides into view smoothly within about 350ms.
6. Move pointer within ShellView menu area; confirm ShellView stays open.
7. Click at least one existing navigation item; confirm navigation still works.
8. Move pointer out of ShellView into main content; confirm ShellView hides within about 300ms.
9. Repeat rapid pointer movement between left edge, ShellView, and content 20 times; confirm no flicker or stuck half-open state.
10. With ShellView hidden, click and scroll main content near the left side; confirm only the narrow edge trigger blocks input.
11. Verify keyboard-only navigation path still reaches ShellView navigation.

## XAML Contract Checklist

- [ ] Left-edge reveal: hovering the far-left hotspot opens the ShellView without requiring a click.
- [ ] In-shell stay-open: moving across the header, menu, scroll area, and user card keeps the ShellView visible.
- [ ] Pointer-leave hide: moving from the ShellView into main content closes it after the short delay.
- [ ] Rapid re-entry cancellation: returning to the hotspot or ShellView while it is closing cancels the close without flicker.
- [ ] Hidden content click/scroll: when hidden, only the narrow hotspot blocks input; main content remains clickable and scrollable.
- [ ] Keyboard navigation: keyboard-only access can still reach visible ShellView navigation buttons and use existing navigation.

## Acceptance

- Build passes.
- WPF test project passes.
- Manual smoke validates reveal, stay-open, hide, flicker handling, main-content hit-testing, and keyboard access.
