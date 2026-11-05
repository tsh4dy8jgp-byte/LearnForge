# Layouts and colour schemes

Open **Appearance** in the top bar, or **Account → Change appearance**. Selection applies immediately to the whole application. Native radio controls support Tab and arrow keys, and the selected card shows a checkmark as well as a border.

| Layout | Use |
| --- | --- |
| Workspace (`Sidebar`) | Persistent side navigation on desktop |
| Campus (`Header`) | Full-width workspace with navigation across the top |
| Focus (`Focus`) | Centred workspace and single-column dashboard and practice setup |

All layouts keep navigation and account actions available on mobile. They share the same page components, learning data, and interaction controls.

| Colour scheme | Appearance |
| --- | --- |
| Site brand (`Brand`) | Botanical surfaces with the deployment’s primary colour |
| Ocean (`Ocean`) | Cool blue |
| Plum (`Plum`) | Lavender and violet |
| Terracotta (`Terracotta`) | Clay and cream |
| Midnight (`Midnight`) | Dark slate and mint, including native form controls |

## Set deployment defaults

Configure the public `Site` section in `apps/api/appsettings.json`, or set environment variables. For example:

```sh
Site__Layout=Header Site__ColorScheme=Ocean dotnet run --project apps/api
```

The API validates named enum values at startup and exposes them through `GET /api/site-settings`. Wire values use camelCase (`sidebar`, `header`, `focus`, `brand`, `ocean`, `plum`, `terracotta`, `midnight`). Existing configurations default to Workspace and Site brand. No database migration is needed.

`Site__PrimaryColor` customizes **Site brand** only. Other schemes retain their curated accents, including the light accent and dark button text in Midnight. Primary colours must be six-digit hex values and pass the existing contrast validation. Use `Site__Font=serif` for serif typography; `system` is the default. Existing logo, site name, copy, and navigation-label settings work in every layout. Locale settings format dates and numbers; they do not translate the interface.

## Personal preferences

Explicit choices are saved under `learnforge.appearance.v1` in localStorage for the current browser and origin. They are shared by people using that browser profile and do not sync with an account. A choice for just one setting leaves the other following deployment defaults. **Use site defaults** deletes the override. Invalid or obsolete saved values fall back independently; if writing browser storage fails, the current choice still applies and the page explains that it could not be saved.

## Extend the frontend

- `apps/web/src/_themes.scss`: surface, text, accent, focus, border and feedback tokens; scoped palette previews use the same tokens as real pages.
- `apps/web/src/_layouts.scss`: responsive shell variants. Navigation and the router outlet are shared.
- `apps/web/src/app/appearance/appearance-preferences.ts`: typed preset descriptions and preference persistence.
- `apps/web/src/app/appearance/appearance-page.ts` and `apps/web/src/_appearance.scss`: accessible live picker and CSS previews.

To add a preset, extend the corresponding API enum, run `make api-types`, then add its frontend metadata and styles. Avoid literal colours in page components; use the shared tokens. Keep success, warning and error meanings consistent across palettes.

The Playwright appearance suite checks all 15 combinations at 320, 768 and 1440 pixels, core text-token contrast (4.5:1), keyboard selection, reload persistence, configured defaults, and public page overflow. These checks support accessibility testing; they do not constitute a full accessibility audit.
