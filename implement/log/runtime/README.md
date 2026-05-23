# runtime logs

`log/runtime/` is the output directory for local development runtime logs.

## Purpose

- Store stdout/stderr captured from local frontend and backend startup
- Keep runtime artifacts out of Git history
- Leave one tracked document in place so the directory purpose stays explicit

## Naming

- `frontend-live-YYYYMMDD-HHMMSS.out.log`
- `frontend-live-YYYYMMDD-HHMMSS.err.log`
- `backend-live-YYYYMMDD-HHMMSS.out.log`
- `backend-live-YYYYMMDD-HHMMSS.err.log`

## How to generate logs

Run the startup script from `implement/`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-dev-with-runtime-logs.ps1
```

The script starts:

- frontend: `npm run dev`
- backend: `dotnet run --project src\backend\CobolAnalyzer.API\CobolAnalyzer.API.csproj`

and writes their stdout/stderr into this directory.

## Git policy

- Log files under this directory are ignored by `implement/.gitignore`
- This `README.md` remains tracked

## Cleanup

Delete old `*.log` files when they are no longer needed.
