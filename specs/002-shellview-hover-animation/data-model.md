# Data Model: ShellView Hover Animation

No persistent data entities are introduced or changed.

## Transient UI State

### Shell visibility state

- **Hidden**: ShellView panel is offscreen; only the narrow left-edge trigger zone receives pointer hover.
- **Opening**: ShellView is animating from hidden to visible.
- **Visible**: ShellView is fully visible and remains open while pointer is inside it.
- **Closing**: ShellView is animating from visible to hidden after pointer leaves.

## State Transitions

| From | Trigger | To | Expected behavior |
|------|---------|----|-------------------|
| Hidden | Pointer enters left-edge trigger | Opening | Start reveal animation immediately |
| Opening | Animation completes | Visible | ShellView remains available for navigation |
| Opening | Pointer leaves and does not return | Closing | Reverse toward hidden without flicker |
| Visible | Pointer leaves ShellView | Closing | Start hide after short delay |
| Closing | Pointer returns to ShellView or trigger | Opening | Cancel close and reveal again |
| Closing | Animation completes | Hidden | Main content is fully usable |

## Validation Rules

- Hidden ShellView must not block main content except within the narrow trigger zone.
- Visible ShellView must preserve all existing menu items, selected state, and navigation commands.
- Keyboard navigation must remain possible without pointer hover.
- No database schema, migrations, seed data, repositories, or Application DTOs are required.
