# Feature Specification: ShellView Hover Animation

**Feature Branch**: `002-shellview-hover-animation`

**Created**: 2026-05-18

**Status**: Draft

**Input**: User description: "minh muon build feature animation cho toan bo shellview cua app Dormitory. trigger khi minh hover chuot vao sat trai man hinh, shellview se day ra. khi chuot con dang o trong shellview thi shellview luon hien thi, khi keo chuot ra khoi shellview thi shellview trigger animation va day vao. ban hay tham khao theo sidebar cua project whallie nay, minh muon animation giong nhu the D:\\Whallie Project\\Whallie\\Whallie"

**Project Boundary**: This specification targets the DormitoryManagement WPF desktop
application. Do not introduce ASP.NET Core APIs, REST controllers, JWT flows, or web
assumptions unless the user explicitly requests that scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reveal ShellView from left edge hover (Priority: P1)

An authenticated user can move the pointer to the far-left edge of the app window and see the full ShellView slide into view with the same feel as the referenced Whallie sidebar.

**Primary Actor**: Authenticated user

**Why this priority**: This is the core interaction requested and determines whether the ShellView feels discoverable and fast during daily navigation.

**Independent Test**: From any main app screen, place the pointer inside the left-edge trigger zone and confirm the ShellView becomes fully visible with smooth slide-out animation.

**Acceptance Scenarios**:

1. **Given** the ShellView is hidden and the app window is active, **When** the user hovers at the far-left edge, **Then** the ShellView slides into view and remains visible.
2. **Given** the ShellView is hidden and the pointer is not near the far-left edge, **When** the user continues working in the main content area, **Then** the ShellView remains hidden and does not block content.

---

### User Story 2 - Keep ShellView visible while pointer is inside it (Priority: P2)

An authenticated user can move within the visible ShellView without it closing unexpectedly while choosing navigation items or reading menu labels.

**Primary Actor**: Authenticated user

**Why this priority**: Users must be able to interact with the ShellView once it appears; premature closing would make navigation frustrating.

**Independent Test**: Reveal the ShellView, move the pointer across its visible area, and confirm it remains open until the pointer leaves the ShellView.

**Acceptance Scenarios**:

1. **Given** the ShellView is visible, **When** the user moves the pointer between ShellView menu items, **Then** the ShellView remains visible.
2. **Given** the ShellView is visible and the user pauses over it, **When** no click occurs, **Then** the ShellView remains visible.

---

### User Story 3 - Hide ShellView after pointer leaves it (Priority: P3)

An authenticated user can move the pointer out of the ShellView and have the ShellView slide back out of the way, restoring focus to the main content.

**Primary Actor**: Authenticated user

**Why this priority**: The ShellView must not permanently cover content after navigation or inspection is complete.

**Independent Test**: Reveal the ShellView, move the pointer outside the ShellView, and confirm the ShellView hides with smooth slide-in animation.

**Acceptance Scenarios**:

1. **Given** the ShellView is visible, **When** the user moves the pointer outside the ShellView, **Then** the ShellView slides back into its hidden position.
2. **Given** the ShellView is closing, **When** the user quickly returns the pointer to the ShellView or far-left edge, **Then** the ShellView stays visible or reopens without flicker.

### Edge Cases

- Pointer briefly touches the left-edge trigger zone and leaves before the reveal animation completes.
- Pointer moves rapidly from ShellView to main content and back again.
- Pointer leaves ShellView through the top, bottom, right edge, or over layered content.
- ShellView is already visible when the user hovers the left edge again.
- App window is resized while the ShellView is hidden or visible.
- Current screen has scrollable or clickable content near the far-left edge.
- User uses keyboard navigation while the ShellView is hidden.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a far-left-edge hover trigger that reveals the ShellView from its hidden state.
- **FR-002**: The left-edge trigger MUST be narrow enough that normal main-content interactions are not accidentally blocked.
- **FR-003**: The ShellView MUST slide into view when the pointer enters the far-left trigger area.
- **FR-004**: The ShellView MUST remain visible while the pointer is inside the ShellView.
- **FR-005**: The ShellView MUST slide back into its hidden state after the pointer leaves the ShellView.
- **FR-006**: The reveal animation MUST feel like the referenced Whallie sidebar: quick, smooth, left-to-right, and ease-out while opening.
- **FR-007**: The hide animation MUST feel like the referenced Whallie sidebar: quick, smooth, right-to-left, and ease-in while closing.
- **FR-008**: The animation MUST avoid visible flicker when the pointer rapidly crosses between the trigger zone, ShellView, and main content.
- **FR-009**: The hidden ShellView MUST not prevent users from interacting with visible main content except within the narrow left-edge trigger zone.
- **FR-010**: The visible ShellView MUST preserve existing navigation behavior, menu availability, role-specific content, and current selected state.
- **FR-011**: The feature MUST apply to the whole app shell, not only one feature screen.
- **FR-012**: The feature MUST preserve keyboard access to ShellView navigation for users who do not use pointer hover.
- **FR-013**: The app MUST remain usable if the window is resized while the ShellView is visible or hidden.
- **FR-014**: The ShellView MUST start in the hidden state after normal app startup unless existing user/session state already defines a different shell visibility state.

### Authorization and Security Requirements

- **SR-001**: Existing role-based ShellView navigation visibility MUST remain unchanged.
- **SR-002**: UI visibility MUST NOT be the only authorization control for any screen reachable from ShellView navigation.
- **SR-003**: The feature MUST NOT add ASP.NET Core API, REST, JWT, or web-auth behavior.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of trial users can reveal the ShellView by moving the pointer to the far-left edge within 3 seconds after being told the gesture.
- **SC-002**: The ShellView begins revealing within 100 milliseconds of pointer entry into the left-edge trigger zone during normal app use.
- **SC-003**: The ShellView completes reveal in no more than 350 milliseconds and completes hide in no more than 300 milliseconds.
- **SC-004**: During 20 rapid pointer transitions between trigger zone, ShellView, and main content, the ShellView shows no visible flicker or stuck half-open state.
- **SC-005**: Existing ShellView navigation actions remain successful for all roles that could use them before this feature.
- **SC-006**: Main content remains clickable and scrollable when the ShellView is hidden, except for the narrow left-edge trigger zone.
- **SC-007**: Keyboard-only users can still access ShellView navigation without relying on pointer hover.

## Assumptions

- The referenced Whallie sidebar behavior means an approximately 8-pixel left-edge hover zone, a full ShellView slide from offscreen to onscreen, fast open/close timing, ease-out on open, ease-in on close, and a short leave delay to avoid flicker.
- The feature changes ShellView behavior globally for the WPF shell and does not change dormitory business rules or persisted data.
- No new data entities, migrations, audit logs, or billing/notification behavior are required.
- Existing role-based navigation contents remain authoritative; this feature changes how the ShellView appears, not what users are allowed to open.
