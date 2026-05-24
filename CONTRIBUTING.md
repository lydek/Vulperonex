# Contributing

## Plugin Scaffolds

- New overlay importer plugins should depend on `Vulperonex.Application` contracts and `Vulperonex.Plugins.Abstractions`.
- Keep importer contracts stream-based. Do not require callers to hand over filesystem paths.
- For OneComme-style imports, map external fields into Vulperonex overlay DTO names and return warnings for lossy conversions.
