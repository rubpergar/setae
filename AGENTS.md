# Setae Project Instructions

## Build and publish after source changes

After any change under `src/`, rebuild the runtime-specific executable before considering the task complete:

```text
dotnet build src\Setae.App\Setae.App.csproj -c Release -r win-x64
```

Also rebuild the portable release output:

```text
dotnet publish src\Setae.App\Setae.App.csproj -c Release -p:PublishProfile=portable
```

## Required validation

```text
dotnet build Setae.sln -c Release
dotnet test tests\Setae.App.Tests\Setae.App.Tests.csproj -c Release
```

The result must contain 0 errors and 0 warnings (`TreatWarningsAsErrors` is enabled).

## Startup smoke test

Launch the executable and verify that a visible `HwndWrapper...` window titled `Setae` exists and that no `#32770` dialog exists. A live process is not enough because a message box would also keep it alive.

## Scope source of truth

- `src/` is the behavioral source of truth.
- `README.md` and files under `docs/` describe the public behavior and must match `src/`.
- Do not claim features that are not implemented and manually validated.
