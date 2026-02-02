# Repository Guidelines

## Project Structure & Module Organization
- `KsefSheetsSync.sln` is the solution entry point.
- `src/KsefSheetsSync/` contains the .NET 8 console app.
- Key files: `Program.cs` (orchestration), `AppConfig.cs` (env loading), `KsefApiClient.cs` (KSeF API), `GoogleSheetsWriter.cs` (Sheets I/O), `Models.cs` (DTOs).
- There is no `tests/` directory yet.

## Build, Test, and Development Commands
- `dotnet build` builds the solution from the repo root.
- `dotnet run --project src/KsefSheetsSync` runs the sync; requires environment variables to be set.
- `dotnet test` currently does nothing (no test projects), but use it once tests are added.

## Coding Style & Naming Conventions
- C#/.NET conventions: PascalCase for types/public members, camelCase for locals/parameters.
- 4-space indentation; keep brace style consistent with existing files (Allman).
- Keep file names aligned with primary types (`GoogleSheetsWriter.cs`, `KsefApiClient.cs`).
- Nullable reference types are enabled; avoid `null` when possible and annotate explicitly when needed.

## Testing Guidelines
- No automated tests exist yet.
- If adding tests, prefer xUnit with a `tests/KsefSheetsSync.Tests` project and `*Tests` class names; run via `dotnet test`.

## Commit & Pull Request Guidelines
- No commit history exists yet, so there is no established convention.
- Use concise, imperative commit subjects (e.g., "Add KSeF invoice filter").
- PRs should describe behavior changes, list any new/changed env vars, and include a brief validation note.

## Configuration & Secrets
- The app is driven by environment variables (see `README.md`).
- Required examples:

```bash
export SPREADSHEET_ID=...
export GOOGLE_SERVICE_ACCOUNT_JSON_BASE64=...
export COMPANY1_NAME=...   # plus COMPANY1_NIP and a token
```

- Do not commit secrets; use GitHub Actions secrets for CI.
