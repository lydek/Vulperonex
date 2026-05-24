# OneComme Bridge

Phase 7C only ships the contract and plugin scaffold.

## Goal

Import a OneComme overlay package stream into a Vulperonex custom overlay preset slug without coupling the contract to local file paths.

## Contract

- Interface: `Vulperonex.Application.Overlay.Extensions.IOverlayTemplateImporter`
- Entry point: `ImportAsync(Stream package, string targetSlug, CancellationToken ct)`
- Result: `OverlayTemplateImportResult`

## Mapping Notes

- `comment.name` maps to overlay `displayName`
- `comment.message` maps to chat segment text
- Imported HTML packages should target `/overlay/custom/{slug}/index.html`
- Warnings should carry lossy field mappings or unsupported OneComme options

## Phase Boundary

- No importer implementation in Phase 7C
- No marketplace packaging in Phase 7C
- No automatic HTML rewrite in Phase 7C
