# Contributing

> **Language / 語言**: [English](CONTRIBUTING.md) | [繁體中文](docs/zh-TW/CONTRIBUTING.md)

## Documentation Locale Policy

- English is the default documentation language and keeps the original filename.
- Localized Markdown files live under `docs/<locale>/` with the same relative path and clean filename as the English source.
- Traditional Chinese documentation uses the `docs/zh-TW/` tree.
- Do not use locale suffix naming such as `*.zh-TW.md`; use the locale directory strategy instead.

## Plugin Scaffolds

- New overlay importer plugins should depend on `Vulperonex.Application` contracts and `Vulperonex.Plugins.Abstractions`.
- Keep importer contracts stream-based. Do not require callers to hand over filesystem paths.
- For OneComme-style imports, map external fields into Vulperonex overlay DTO names and return warnings for lossy conversions.
