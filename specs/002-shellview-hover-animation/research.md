# Research: ShellView Hover Animation

## Decision: Match Whallie sidebar interaction profile

**Rationale**: User explicitly requested animation like Whallie. Observed reference behavior: narrow left-edge hotspot, panel hidden offscreen, slide in about 300ms with ease-out, slide out about 250ms with ease-in, and short leave delay to avoid flicker.

**Alternatives considered**:
- Permanent collapsed sidebar: rejected because user requested hover-triggered push/slide behavior.
- Instant show/hide: rejected because user requested animation.
- Long delay before hide: rejected because it would cover main content longer than needed.

## Decision: Keep behavior in WPF shell view layer

**Rationale**: Hover zones, storyboards, transforms, and pointer leave timing are view interaction concerns. They do not affect dormitory business rules, persistence, authorization, or Application services.

**Alternatives considered**:
- Application service state: rejected because no business workflow or data access exists.
- Domain model state: rejected because shell open/closed state is transient UI state.
- Persisted user preference: rejected because spec does not ask for remembered shell state.

## Decision: Preserve existing ShellViewModel navigation and role filtering

**Rationale**: Feature changes how ShellView appears, not which menu items exist or where they navigate. Existing role-based menu construction remains authoritative.

**Alternatives considered**:
- Rebuild navigation menu: rejected as unrelated refactor.
- Add new authorization checks in UI: rejected because service-layer authorization remains separate and existing menu visibility already handles shell display.

## Decision: Use WPF tests plus manual smoke validation

**Rationale**: Animation timing and pointer behavior are difficult to prove fully in headless unit tests. Test deterministic state/handler behavior where possible, then require manual WPF smoke validation for visual timing, flicker, hit-testing, and keyboard access.

**Alternatives considered**:
- Only manual testing: rejected because task plan should start with repeatable tests when practical.
- Full automated UI animation testing: rejected as too heavy for current project patterns.
