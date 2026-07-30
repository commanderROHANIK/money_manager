# Money Manager Design System

Source of truth for the app's visual language: color tokens, type scale, spacing,
component specs (buttons, inputs, badges, stat/chart cards, list rows) and the
priority screen layouts (Login/Register, Dashboard, Rental Properties).

Exported from the [Claude Design](https://claude.ai/design) project
"Money Manager design system" (id `da647a01-c3ab-435a-8331-1ab9baac9dfc`).

- `Money Manager Design System.dc.html` — the design doc itself (Design Capsule format).
- `support.js` — the Claude Design runtime that renders `.dc.html` files. Generated,
  do not hand-edit; re-export from the source project if it needs to change.

## Viewing it

Serve this folder and open the `.dc.html` file in a browser, e.g.:

```
npx serve design-system
```

The design tokens (colors, radii, spacing) are the reference for anything we build
in `money-manager-ui` — CSS custom properties should track the `--bg`, `--surface`,
`--primary`, `--danger`, etc. values defined in the doc's light/dark theme objects.
