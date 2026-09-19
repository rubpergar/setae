# Setae

Utilidad de escritorio para Windows que supervisa el nivel del micrófono y te avisa cuando elevas demasiado la voz mientras juegas con auriculares.

## Qué hace

- Muestra en tiempo real el nivel de entrada del micrófono seleccionado (0–100).
- Avisa cuando el nivel supera el umbral configurado durante un tiempo mínimo (pitido + color).
- Evita falsas alertas: los picos breves no activan el aviso y existe un enfriamiento entre avisos.
- Ventana compacta siempre visible, minimizable a la bandeja del sistema.

## Privacidad

Todo el procesamiento es local. No se reconoce, graba ni transmite audio; las muestras se descartan tras calcular el nivel. La aplicación funciona sin conexión a Internet.

## Stack

- C# / .NET 10 (WPF) para Windows 10 y 11.
- NAudio (WASAPI) para captura y enumeración de dispositivos.
- `System.Media.SoundPlayer` para el pitido de alerta (carillón generado en código).
- Pruebas con xUnit.
- Distribución: ejecutable `win-x64` self-contained de un solo archivo.

## Compilar y probar

```text
dotnet build Setae.sln -c Release
dotnet test tests/Setae.App.Tests/Setae.App.Tests.csproj -c Release
```

Publicación portable:

```text
dotnet publish src/Setae.App/Setae.App.csproj -c Release -p:PublishProfile=portable
```

## Arquitectura

Un único proyecto de producción. El código se organiza por carpetas:

```text
src/Setae.App/
  Audio/          Captura WASAPI, conversión de muestras y cálculo de nivel
  Monitoring/     Suavizado, umbral, detector de alertas y preferencias
  Infrastructure/ Persistencia, pitido e instancia única
  UI/             Ventana única (medidor + ajustes) y bandeja del sistema
```

Regla principal: `Audio` y `Monitoring` no dependen de la interfaz ni del sistema de archivos; la ventana solo lee el estado publicado por el monitor.

## Ajustes

Umbral, duración mínima sobre el umbral, enfriamiento entre alertas, micrófono, pitido y ventana siempre visible. Se guardan en `%AppData%\setae\settings.json`. La configuración se aplica al instante y se persiste al cerrar el panel de ajustes o la aplicación.

Detalles técnicos completos en [SPEC.md](SPEC.md).