# Setae — Instrucciones del proyecto

## Ejecutable que usa el acceso directo

El enlace `setae.lnk` de la raíz apunta a:

```text
src\Setae.App\bin\Release\net10.0-windows\win-x64\Setae.App.exe
```

Tras CUALQUIER cambio en `src/`, regenerar ese ejecutable antes de dar la tarea por terminada:

```text
dotnet build src\Setae.App\Setae.App.csproj -c Release -r win-x64
```

Regenerar también el portable:

```text
dotnet publish src\Setae.App\Setae.App.csproj -c Release -p:PublishProfile=portable
```

## Validación obligatoria

```text
dotnet build Setae.sln -c Release
dotnet test tests\Setae.App.Tests\Setae.App.Tests.csproj -c Release
```

Debe quedar en 0 errores y 0 warnings (`TreatWarningsAsErrors` activo).

## Smoke test de arranque

Lanzar el exe y comprobar que existe una ventana visible de clase `HwndWrapper...` con título `Setae` y que NO hay ningún diálogo de clase `#32770`. Que el proceso siga vivo no basta (un MessageBox también lo mantiene vivo).

## Fuente de verdad del alcance

- `context.md` / `CONTEXT.md`: premisa y filosofía (referencia de verdad).
- `SPEC.md` y `README.md` pueden estar desactualizados respecto al código; verificar contra `src/` antes de asumir.
