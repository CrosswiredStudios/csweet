# C-Sweet Application Design System

## Status and purpose

This document is the normative design guide for all C-Sweet product UI. It applies to new features, maintenance work, shared components, web views, and the MAUI Blazor Hybrid host.

The keywords **must**, **should**, and **may** describe required, recommended, and optional behavior. A deliberate exception must be documented in the pull request and must not silently introduce a second visual language.

The design goal is **friendly productivity**: C-Sweet should feel capable and trustworthy without becoming cold, dense, or enterprise-generic. The product is an operating environment for a mixed human and agent workforce, so hierarchy, state, responsibility, and next actions must be immediately understandable.

## Sources of truth

- Shared product UI belongs in `src/CSweet.UI`.
- Global tokens and cross-component rules live in `src/CSweet.UI/wwwroot/css/app.css`.
- Layout-specific styles live beside their Razor layouts when CSS isolation is effective.
- MudBlazor theme palettes are configured by the layouts in `src/CSweet.UI/Layout`.
- Light-surface logo: `src/CSweet.App/wwwroot/images/icon.svg`.
- Dark-surface logo: `src/CSweet.App/wwwroot/images/icon_dark.svg`.

Do not duplicate a token as an unexplained literal in new CSS. If a value is repeated or expresses brand meaning, add or reuse a `--cs-*` variable.

## Brand principles

Every product surface must support these principles:

1. **Warm, not ornamental.** Use ivory, white, restrained shadows, and rounded geometry. Decoration must not compete with work.
2. **Confident, not severe.** Navy communicates authority; generous space and friendly language keep it approachable.
3. **Clear status at a glance.** State uses consistent color, text, and where useful an icon. Color alone is never sufficient.
4. **One mixed workforce.** Human and agent employees share the same structural language. Type is indicated with a badge, avatar treatment, or label rather than an entirely separate UI.
5. **Progressive disclosure.** Show the decision and primary action first. Put diagnostics, metadata, and destructive actions behind secondary controls.
6. **Responsive by design.** Web and MAUI render the same shared experience; mobile is not a reduced afterthought.

## Color system

The canonical palette is:

| Token | Value | Role |
| --- | --- | --- |
| `--cs-navy` | `#18233F` | Primary text, filled primary actions, selected controls |
| `--cs-navy-deep` | `#0B2E53` | Navigation rails and large dark brand surfaces |
| `--cs-ivory` | `#FFFCF7` | Application canvas and warm page background |
| `--cs-surface` | `#FFFFFF` | Cards, forms, dialogs, and elevated panels |
| `--cs-mint` | `#98E7C2` | Agent identity accents and soft positive emphasis |
| `--cs-mint-strong` | `#23845C` | Success and online status text/borders |
| `--cs-amber` | `#F2B84B` | Busy, waiting, and caution states |
| `--cs-coral` | `#E85D75` | Brand emphasis, destructive/error states, focal actions |
| `--cs-border` | `#E2DDD5` | Default borders and separators |
| `--cs-muted` | `#68738A` | Secondary text and metadata |

### Color rules

- Page backgrounds must use ivory; primary content surfaces must use white.
- Default body and heading text must use navy. Secondary text must use muted navy-gray.
- Coral must be used sparingly. It is appropriate for a singular focal action, brand overline, error, destructive action, or active-navigation marker.
- Mint means positive, online, available, or agent-associated. Amber means busy, waiting, or caution. Gray means offline or inactive.
- Do not use success, warning, or error colors as large decorative backgrounds.
- Text and interactive controls must meet WCAG 2.2 AA contrast. If a soft color cannot support readable text, use it as a border or background with navy text.

## Typography

The font stack is `Inter, "Avenir Next", Roboto, "Helvetica Neue", Arial, sans-serif`.

- Page titles use `Typo.h3` unless the page is a marketing or empty-state hero.
- Section titles use `Typo.h6`.
- Card titles use `Typo.subtitle1`.
- Supporting copy uses body text with `Color.Secondary`.
- Eyebrows use `Typo.overline`; they are coral, uppercase, bold, and letter-spaced.
- Headings use navy, a strong weight, tight letter spacing, and short line lengths.
- Labels must use sentence case. Avoid title case on every control.
- Do not communicate hierarchy by size alone; use spacing, weight, and placement.

Recommended content widths:

- Explanatory paragraphs: no more than 65 characters per line.
- Form help and empty-state copy: no more than 55 characters per line when centered.
- Dense tables and logs may use the available width.

## Spacing, geometry, and elevation

Use a 4-pixel base rhythm. Preferred increments are `0.25rem`, `0.5rem`, `0.75rem`, `1rem`, `1.5rem`, `2rem`, and `3rem`.

- Standard panel radius: `--cs-radius` (`12px`).
- Standard control radius: `9px`.
- Chips and status pills: fully rounded.
- Standard panel shadow: `--cs-shadow`.
- Use one border and one restrained shadow for elevated surfaces. Do not stack multiple strong shadows.
- Related controls should be 8–12px apart; sections should be 16–24px apart; major page regions should be 24–48px apart.
- Avoid arbitrary per-page spacing when an existing utility or component pattern is suitable.

## Application shells

### Primary authenticated shell

- Desktop uses a persistent deep-navy left navigation drawer.
- The brand appears at the top with the dark-surface logo, product name, and short product phrase.
- Navigation uses icon plus label, with a darkened selected background and coral left marker.
- The desktop top app bar is hidden; account and organization controls should live in an intentional header or sidebar region.
- The first destination is Enterprise, which represents the complete portfolio of businesses.
- A focused-business selector follows Enterprise. Business-scoped destinations, including Overview and Employees, are visually nested beneath it.
- Platform-wide Settings and the signed-in user menu remain separated from business navigation at the bottom of the drawer.
- Changing the focused business preserves a safe peer destination when possible. Entity-specific destinations return to their owning section instead of carrying invalid entity identifiers into another business.
- Tablet and phone use a compact warm-ivory app bar and a collapsible drawer.
- All authenticated portfolio, business, settings, and account pages use this same shell. Do not introduce a separate command-center navigation rail.

### Authentication shell

- Desktop uses a dark brand panel paired with a white form card on ivory.
- Mobile stacks the brand region above the form and reduces the logo size.
- Authentication pages must remain calm and focused; do not add product dashboards or promotional carousels.

### Setup shell

- Setup uses a lightweight ivory header with the light-surface logo and explicit setup context.
- Setup steps must indicate current, complete, warning, and error states with both text/icon and color.

## Page composition

Product pages should use this order:

1. `PageTitle` for browser history and accessibility.
2. A page wrapper such as `setup-shell` or `command-center-shell`.
3. A `page-header` containing an optional overline, one `h3`, one short description, and primary actions.
4. Loading, error, or empty state when applicable.
5. Main content organized in cards, a table/list, graph, or task-focused workspace.

Rules:

- There must be only one page-level `h1`/visual title concept. Current MudBlazor pages use `h3` styling, but the rendered accessibility hierarchy must remain logical.
- The primary action belongs at the upper right on desktop and becomes full-width or wraps predictably on mobile.
- Do not put a border under every page header. Separation should usually come from spacing.
- Use `surface-panel` for generic content panels; create a named component class when behavior or structure is reusable.
- Empty states must explain what is missing and provide the next valid action when one exists.

## Component rules

### Buttons

- One filled primary button per local decision area.
- Use outlined buttons for secondary actions and text buttons for low-emphasis actions.
- Destructive actions use coral/error styling and must not sit between safe primary actions.
- Button labels begin with a verb: “Hire employee,” “Open chat,” “Save changes.”
- Icon-only buttons require an accessible name and a tooltip when the action is not universally obvious.
- Touch targets must be at least 44 by 44 CSS pixels on mobile.

### Forms

- Use outlined fields with persistent labels. Placeholder text supplements a label; it never replaces one.
- Group related fields under a section heading and short explanation.
- Prefer one column on phone and no more than two columns for ordinary desktop forms.
- Place validation next to the affected field. The final action area may also contain a concise summary.
- Disable submission during processing and change the label when helpful, such as “Saving…”.
- Preserve user input after recoverable errors.

### Cards and panels

- Cards use a white surface, warm border, 12px radius, and restrained shadow.
- A card should represent one entity, decision, or summary. Do not use cards merely to box every paragraph.
- Clickable cards need hover and focus treatment and must use a semantic interactive element or equivalent keyboard behavior.
- Metadata labels use small muted uppercase text; values use navy body text.

### Tables and directories

- Use tables for repeated records with comparable fields; use cards for browsing a small collection or when each record has diverse actions.
- Keep the primary identifier in the first column with avatar, name, and role.
- Status is always a labeled chip.
- Row actions live at the end and should not make every row visually noisy.
- On mobile, allow horizontal scrolling only for genuinely tabular data; otherwise transform the content into stacked records.

### Tabs and segmented controls

- Use a segmented control to switch between peer representations of the same data, such as Graph and Directory.
- The selected segment is navy-filled with white text.
- Switching tabs must preserve relevant filters and selection where practical.

### Status and badges

Canonical employee/runtime states:

| State | Treatment |
| --- | --- |
| Online / success | Mint/green outline or dot plus text |
| Busy / waiting | Amber outline or dot plus text |
| Offline / inactive | Gray outline or dot plus text |
| Out of office / error | Coral outline or dot plus text |
| Agent | Mint identity badge; do not imply online status solely from agent type |
| Human | Navy identity treatment; usually no extra badge unless needed for comparison |

### Dialogs

- Dialogs use the same ivory/white surface language and 14px radius.
- Titles state the decision. Body copy explains consequences before destructive confirmation.
- Primary and cancel actions remain visible without scrolling when practical.
- Long workflows should use steps or tabs rather than one very tall form.

### Notifications and errors

- Success messages should be concise and disappear when no longer useful.
- Warnings explain the consequence and the recovery path.
- Error messages must describe what the user can do next. Do not expose raw exception text.
- Loading states should preserve the eventual layout where possible to reduce visual movement.

## Workforce and graph views

- Employee graphs use calm blue-gray connectors and white employee nodes.
- Human avatars use navy. Agent avatars use mint/green. Special identities may use amber when the meaning is explained.
- Selection uses border/shadow emphasis, not a completely different card layout.
- Detail panels may appear at the right on desktop or as a bottom sheet/stacked panel on mobile.
- Graph controls must include a directory/list alternative for accessibility and small screens.
- Never rely on spatial position alone to communicate reporting relationships; include textual “reports to” and subordinate information.

## Chat views

- User messages are navy-filled and right-aligned.
- Assistant messages use a white surface with navy text and are left-aligned with an avatar.
- Conversation history is secondary to the active transcript.
- The composer remains easy to reach, supports multiline text, and has a clear send action.
- Execution traces, tool details, and diagnostics use progressive disclosure.
- Markdown content must remain readable for lists, tables, code, and long unbroken values.

## Responsive behavior

Design and test at minimum:

- Phone: 390px wide.
- Tablet: 768px wide.
- Desktop: 1280px wide.
- Wide desktop: 1440px or greater.

Required behavior:

- No unintended horizontal page overflow at any target width.
- Multi-column forms, summaries, and card grids collapse to one column on phone.
- Headers and actions wrap without overlap or clipped labels.
- Navigation changes form at the defined breakpoint rather than merely shrinking.
- Graphs and wide tables use an intentional scroll container or alternate representation.
- Hover is an enhancement; every action works with touch and keyboard.
- Shared UI must render from `CSweet.UI` in both web and MAUI hosts. Do not fork ordinary product pages by host.

## Accessibility

All new and changed UI must:

- Meet WCAG 2.2 AA color contrast.
- Be operable with keyboard alone.
- Show a visible focus state.
- Use semantic headings, landmarks, lists, tables, and controls.
- Provide accessible names for icon-only controls.
- Associate labels, help text, and validation messages with form fields.
- Announce asynchronous loading, errors, and streamed chat updates appropriately.
- Respect `prefers-reduced-motion`; motion must not be required to understand state.
- Avoid flashing, excessive parallax, and auto-playing decorative motion.

## Motion

- Use motion only to explain cause and effect: opening a drawer, selecting a card, changing state, or revealing details.
- Standard transitions should be 120–200ms with an ease-out curve.
- Hover elevation should move no more than 2px.
- Do not animate large layout changes repeatedly.

## Content and voice

C-Sweet copy is direct, warm, and action-oriented.

- Prefer “Hire employee” over “Initiate workforce resource creation.”
- Prefer “No agents have been installed” over “0 results.”
- Explain specialized terms the first time they appear.
- Avoid jokes in errors, security decisions, billing, or destructive confirmations.
- Use “business” in user-facing organization-management language unless the domain distinction matters.
- Use “employee” as the shared presentation term when humans and agents are shown together; use explicit type labels where ambiguity matters.

## Implementation rules

1. Reuse MudBlazor primitives and existing C-Sweet classes before creating bespoke controls.
2. Add reusable product components to `src/CSweet.UI/Components`.
3. Keep host-specific code out of shared Razor components.
4. Use `--cs-*` variables for brand values and MudBlazor palette values for semantic component state.
5. CSS attached directly to the rendered root of a MudBlazor component may not receive a Razor CSS-isolation attribute. Put those cross-component selectors in `app.css` or use a verified `::deep` selector.
6. Do not use `!important` unless overriding third-party component output that cannot be controlled through the theme or component API. Document the reason nearby.
7. Do not introduce a new font, color, radius, shadow, breakpoint, or icon family without updating this document and the shared tokens.
8. New pages must implement loading, empty, error, and populated states.
9. UI must not display raw backend exceptions, secrets, internal identifiers without purpose, or untrusted HTML.
10. Visual changes require desktop and phone verification.

## Pull request design checklist

- [ ] Uses the shared palette, typography, spacing, radius, and elevation rules.
- [ ] Has one clear page title and one clear primary action per decision area.
- [ ] Includes loading, empty, error, disabled, and success behavior where applicable.
- [ ] Works at 390px, 768px, 1280px, and a wide desktop width.
- [ ] Has no unintended horizontal overflow.
- [ ] Supports keyboard, visible focus, semantic structure, and accessible names.
- [ ] Does not rely on color, hover, or spatial layout alone.
- [ ] Reuses shared components and tokens instead of adding one-off literals.
- [ ] Preserves web/MAUI shared UI boundaries.
- [ ] Includes screenshots or recorded visual evidence for meaningful UI changes.
- [ ] Builds with no new warnings and passes relevant tests.

## Changing the design system

A design-system change must update all affected sources of truth in the same change:

1. This document.
2. The `--cs-*` variables and shared selectors in `app.css`.
3. Every layout palette that exposes the changed semantic color.
4. Any affected reusable component.
5. Visual verification at phone and desktop widths.

Prefer evolving an existing rule over adding a page-specific exception. If a pattern appears three times, promote it to a shared class or Razor component.
