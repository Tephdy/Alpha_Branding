# Native WPF Migration Plan

## Evidence
- Original stack: static HTML/CSS/JavaScript browser application (index.html, styles.css, script.js) with JSZip loaded from a CDN.
- Workflow: select multiple property photos, stretch each to 1200x1000, overlay img/alpha_branding.png, encode WebP at 80% quality, preview results, rename sequentially, download individually or as ZIP.
- Branding: dark charcoal and gold visual language; img/1.png is the header logo; all four PNG assets are local and contain no credentials.
- Configuration/integrations: no package manifest, environment file, backend, API, database, test, lint, CI, release, or agent-instruction setup exists. README.md is empty.

## Contract
- Target: `net8.0-windows`, WPF/XAML, native dialogs and controls, one application project and one focused test project.
- Preserve output behavior and assets. Use ImageSharp only because WPF/WIC does not provide a supported built-in WebP encoder; use System.IO.Compression for ZIP.
- Keep architecture minimal: one view model, one image service, one filename helper, one result model, two views (main window and image preview).
- No local persistence is required by the original workflow.

## Validation
- Restore, Release build, tests, format verification, framework-dependent win-x64 publish, process startup smoke test, web-shell dependency scan, diff review, push, and PR.
