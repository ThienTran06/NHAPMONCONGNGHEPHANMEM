# UI Contract: ShellView Hover Animation

## Scope

Applies to authenticated app shell screens rendered inside `ShellView`.

## Interaction Contract

### Reveal

- When pointer enters the far-left trigger zone, ShellView begins opening within 100ms.
- ShellView slides from hidden-left to visible position.
- Opening motion feels quick and smooth, matching Whallie-style ease-out behavior.
- Re-entering the trigger while ShellView is already visible does not restart animation unnecessarily.

### Stay visible

- While pointer remains inside ShellView bounds, ShellView stays visible.
- Users can click existing navigation items without ShellView hiding before the click completes.
- Existing current-page selected state remains visible.

### Hide

- When pointer leaves ShellView bounds, ShellView begins closing after a short anti-flicker delay.
- ShellView slides back to hidden-left position.
- Closing motion feels quick and smooth, matching Whallie-style ease-in behavior.
- If pointer returns to ShellView or trigger before close completes, closing is canceled and ShellView remains visible/reopens.

### Main content hit-testing

- Hidden ShellView does not block main content except in the left-edge trigger zone.
- Main content remains scrollable/clickable when ShellView is hidden.
- Visible ShellView may overlay main content while open.

### Accessibility

- Keyboard-only users can still access ShellView navigation without requiring pointer hover.
- Existing focus behavior for navigation buttons is preserved.

## Non-Goals

- No new screens.
- No menu content changes.
- No authorization changes.
- No persistence of shell open/closed state.
