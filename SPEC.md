# Setae

## Especificación técnica y de alcance

Estado: MVP implementado; queda validación manual prolongada con hardware adicional.

## 1. Objetivo

Setae será una utilidad de escritorio para Windows que ayude a detectar cuándo el usuario está elevando demasiado la voz mientras utiliza auriculares.

La aplicación analizará únicamente el nivel de la señal del micrófono seleccionado. No interpretará el contenido del audio, no reconocerá voz y no almacenará grabaciones.

## 2. Alcance del MVP

El MVP incluirá:

- Selección y recuerdo del micrófono de entrada.
- Inicio y detención manual de la monitorización.
- Medidor de nivel en tiempo real en escala relativa de 0 a 100.
- Umbral configurable manualmente.
- Duración mínima configurable antes de activar una alerta.
- Periodo configurable entre alertas.
- Cambio visual verde, ámbar y rojo.
- Pitido breve opcional por evento de alerta.
- Ventana compacta oscura, siempre visible y con posición y tamaño recordados.
- Minimización a la bandeja del sistema.
- Gestión de desconexiones, permisos bloqueados y ausencia de micrófono.
- Persistencia local de preferencias.

## 3. Fuera del MVP

No formarán parte de la primera versión:

- Reconocimiento o transcripción de voz.
- Grabación, reproducción o exportación de audio.
- Filtros avanzados de ruido o separación de voz.
- Overlay específico sobre juegos en pantalla completa exclusiva.
- Atajos de teclado globales.
- Inicio automático con Windows.
- Perfiles por juego.
- Soporte para macOS o Linux.
- Telemetría, cuentas, backend, actualizaciones automáticas o funciones online.

## 4. Plataforma y stack

- Sistema operativo: Windows 10 y Windows 11.
- Arquitectura de distribución: `win-x64`.
- Lenguaje: C#.
- Runtime: versión LTS actual al iniciar la implementación, fijada en `global.json`. La referencia prevista es .NET 10 LTS.
- Interfaz: WPF.
- Bandeja del sistema: `System.Windows.Forms.NotifyIcon`, habilitando Windows Forms junto con WPF.
- Captura: NAudio mediante WASAPI en modo compartido.
- Enumeración de dispositivos: endpoints de captura activos de Windows.
- Persistencia: `System.Text.Json` en un archivo dentro de `%AppData%\setae`.
- Pruebas: xUnit para el núcleo independiente de la interfaz.
- Distribución: ejecutable self-contained portable, sin instalador en el MVP.

No se utilizarán frameworks de UI web, Electron, Tauri, una base de datos, un backend ni un contenedor de dependencias salvo que aparezca una necesidad concreta durante la implementación.

La aplicación no requerirá privilegios de administrador.

## 5. Comportamiento funcional

### 5.1 Inicio

- La aplicación se abrirá detenida.
- No accederá al micrófono hasta pulsar el botón de inicio.
- Se preseleccionará el dispositivo de entrada predeterminado de Windows si no existe una preferencia previa.
- Se ejecutará una única instancia.
- Una segunda apertura activará o mostrará la instancia existente.

### 5.2 Monitorización

- El usuario iniciará y detendrá la captura desde la ventana principal o la bandeja.
- La captura compartida no bloqueará Discord, el juego ni otras aplicaciones.
- Setae no cambiará el volumen, el silencio ni el dispositivo predeterminado de Windows.
- Al ocultar o minimizar la ventana, la monitorización continuará activa.
- Al pulsar Salir desde la bandeja, se detendrá la captura y se liberarán los recursos.

### 5.3 Dispositivos

- La aplicación mostrará micrófonos de entrada activos.
- La lista se actualizará al abrir los ajustes y mediante un botón Refrescar.
- El micrófono elegido se recordará mediante su identificador de dispositivo.
- El cambio de micrófono podrá hacerse durante la monitorización.
- El dispositivo anterior se liberará antes de activar el nuevo.
- Si el dispositivo seleccionado desaparece, la monitorización se detendrá y se mostrará un error.
- Si no existe ningún dispositivo, la aplicación permanecerá abierta y detenida con una explicación.

### 5.4 Ventana y bandeja

- La interfaz estará en español.
- La apariencia será oscura y fija.
- La ventana compacta mostrará barra, nivel actual, marca del umbral y estado textual.
- La ventana podrá mantenerse siempre visible.
- Se recordarán posición y tamaño.
- Cerrar la ventana la ocultará en la bandeja en lugar de finalizar la aplicación.
- El menú de la bandeja incluirá mostrar, iniciar o detener, abrir ajustes y salir.

## 6. Medición del nivel

El nivel de alerta no representará dB SPL reales. Será una escala relativa sobre la señal digital capturada.

### 6.1 Procesamiento

- NAudio entregará buffers temporales mediante el callback de captura.
- Se convertirán las muestras PCM o float a valores normalizados.
- Se calculará RMS sobre las muestras disponibles, incluyendo todos los canales.
- Las muestras se descartarán después del cálculo; no se copiarán a una cola persistente.
- El callback no realizará operaciones de interfaz, disco, red ni bloqueo prolongado.
- Se utilizará un nivel mínimo para evitar problemas matemáticos con el silencio.

### 6.2 Conversión a escala relativa

La implementación inicial utilizará dBFS internamente y un rango fijo de referencia:

```text
dBFS = 20 * log10(max(RMS, epsilon))
relative = clamp(((dBFS + 60) / 60) * 100, 0, 100)
```

Por tanto, 0 representa aproximadamente -60 dBFS o menos y 100 representa 0 dBFS. Esta escala es de uso interno y no pretende ser una medición acústica científica.

No se normalizará automáticamente según los máximos observados. El valor del mismo nivel digital debe permanecer estable durante la sesión.

### 6.3 Suavizado y actualización

- El RMS se calculará en ventanas cortas determinadas por el buffer de captura.
- El nivel visual tendrá una subida rápida y una bajada más lenta para evitar parpadeos.
- El detector usará el nivel suavizado, no un pico instantáneo.
- La interfaz se actualizará como máximo entre 20 y 30 veces por segundo.
- El suavizado y la histéresis serán internos en el MVP y no aparecerán como ajustes técnicos.

## 7. Umbral y estados

- Umbral inicial: 70 sobre 100.
- El usuario podrá modificarlo manualmente.
- El estado verde indicará nivel normal.
- El estado ámbar comenzará automáticamente 10 puntos por debajo del umbral.
- El estado rojo indicará un nivel igual o superior al umbral.
- El estado textual acompañará siempre al color para no depender solo de la percepción cromática.

El hecho de que un sonido active el detector no implica que Setae haya identificado una voz. Cualquier sonido captado con suficiente intensidad podrá activar la alerta.

## 8. Máquina de alertas

La lógica de alerta tendrá estos estados conceptuales:

- Normal: el nivel está por debajo de la zona de alerta.
- Sobre umbral pendiente: el nivel ha superado el umbral, pero aún no durante el tiempo necesario.
- Alertado: se ha emitido el pitido para el evento actual.
- Enfriamiento: se bloquean nuevos pitidos durante el periodo configurado.

Reglas:

- Duración mínima inicial: 500 ms.
- Rango configurable de duración: 100 ms a 3 s.
- Enfriamiento inicial: 3 s.
- Rango configurable de enfriamiento: 0 a 30 s.
- Si el nivel baja antes de completar la duración mínima, se reinicia el contador pendiente.
- Al completar la duración mínima, se emite un único pitido si el sonido está activado.
- El enfriamiento no provocará pitidos adicionales mientras permanezca activo.
- La alerta se rearmará cuando el nivel baje al menos 3 puntos por debajo del umbral.
- Cambiar y guardar ajustes reiniciará el estado temporal del detector sin reiniciar necesariamente la captura.

El pitido utilizará la salida predeterminada de Windows y podrá activarse o desactivarse. No habrá selector de sonidos ni de dispositivo de salida en el MVP.

## 9. Configuración y persistencia

Los ajustes se modificarán en una pantalla separada y se aplicarán al pulsar Guardar.

Se conservarán:

- Identificador del micrófono.
- Umbral relativo.
- Duración mínima sobre umbral.
- Periodo de enfriamiento.
- Activación del pitido.
- Posición, tamaño y estado siempre visible de la ventana.

El archivo de preferencias será un JSON local en `%AppData%\setae\settings.json`.

El archivo no contendrá muestras, audio, transcripciones ni datos de uso.

La pantalla de ajustes incluirá Restaurar valores iniciales. Si el archivo está ausente, corrupto o contiene valores inválidos, se cargarán los valores iniciales y se mostrará un aviso visible.

## 10. Privacidad y seguridad

- Todo el procesamiento se ejecutará en el equipo local.
- No se realizarán peticiones de red.
- No habrá telemetría ni comprobación automática de actualizaciones.
- No se crearán archivos de audio.
- No se guardarán buffers de captura después de procesarlos.
- No se escribirán logs persistentes en el MVP.
- Los errores se mostrarán en la interfaz.
- La aplicación funcionará con el acceso a Internet bloqueado por el firewall.
- La captura solo estará activa después de la acción explícita de inicio.
- La aplicación no solicitará permisos de administrador.

## 11. Gestión de errores

Los errores no deben provocar reintentos infinitos ni cerrar la aplicación de forma inesperada.

Casos mínimos:

- Micrófono inexistente: estado detenido y explicación.
- Permiso de Windows bloqueado: estado detenido y guía breve para revisar la privacidad del micrófono.
- Dispositivo desconectado: detener captura y pedir reconexión o selección de otro dispositivo.
- Fallo al inicializar NAudio: mostrar error y permitir reintentar manualmente.
- Error al guardar ajustes: mostrar el problema y conservar la configuración válida anterior.
- Segunda instancia: mostrar o activar la instancia ya abierta.

## 12. Arquitectura

La solución se dividirá en un núcleo testeable y una capa de aplicación Windows.

### Núcleo

Contendrá:

- Conversión de muestras.
- Cálculo RMS.
- Conversión dBFS a escala relativa.
- Suavizado.
- Máquina de estados de alertas.
- Modelo de preferencias y validación de rangos.

El núcleo no dependerá de WPF, NAudio, Windows Forms ni del sistema de archivos.

### Infraestructura

Contendrá:

- Enumeración de dispositivos.
- Adaptador de captura NAudio/WASAPI.
- Gestión de salida del pitido.
- Lectura y escritura del JSON.
- Detección de instancia única.

### Interfaz

Contendrá:

- Ventana principal.
- Ventana de ajustes.
- Bandeja del sistema.
- Representación del medidor y estados.
- Mensajes de error y guía de permisos.

No se introducirá MVVM completo ni inyección de dependencias generalizada. Se utilizará una UI sencilla con un ViewModel pequeño o code-behind limitado, manteniendo el procesamiento fuera de la vista.

## 13. Pruebas

### Automatizadas

El proyecto de pruebas cubrirá:

- Silencio y muestras con valores conocidos.
- Conversión PCM y float.
- RMS con uno y varios canales.
- Conversión a escala 0-100 y saturación de límites.
- Suavizado de subida y bajada.
- Pico inferior a la duración mínima sin alerta.
- Exceso sostenido que genera una alerta.
- Enfriamiento que evita alertas repetidas.
- Histéresis y rearme.
- Validación y restauración de preferencias.

### Manuales

Se validará con micrófono integrado, USB, auriculares y Bluetooth.

También se comprobarán:

- Convivencia con Discord y un juego.
- Permiso de micrófono bloqueado.
- Desconexión y reconexión del dispositivo.
- Cambio de dispositivo durante la monitorización.
- Minimización, bandeja, cierre y salida completa.
- Escalado de pantalla de Windows.
- Segunda apertura de la aplicación.
- Firewall bloqueando cualquier acceso de red.
- Sesión prolongada de al menos 8 horas.

## 14. Objetivos de rendimiento

Se medirá el comportamiento en un PC de referencia antes de cerrar el MVP.

Objetivos iniciales:

- No bloquear el callback de audio con UI, disco o red.
- Actualización visual estable entre 20 y 30 Hz.
- Latencia de alerta cercana a la duración configurada, con una desviación objetivo inferior a 150 ms.
- CPU media inferior al 1% durante monitorización en el equipo de referencia.
- Memoria estable y preferiblemente inferior a 100 MB de uso privado.
- Sin crecimiento sostenido de memoria durante una sesión de 8 horas.

Los objetivos de CPU y memoria son referencias de aceptación, no una garantía independiente del hardware, controladores o versión de Windows.

## 15. Fases de implementación

1. Crear la solución WPF portable y fijar la versión del SDK.
2. Implementar un prototipo de enumeración y captura WASAPI sin persistir audio.
3. Implementar el núcleo de medición y la máquina de alertas con pruebas automatizadas.
4. Integrar ciclo de vida de dispositivos, cambio en caliente y gestión de errores.
5. Implementar ventana compacta, medidor, ajustes y bandeja.
6. Añadir persistencia JSON, restauración de valores y única instancia.
7. Ejecutar pruebas manuales, pruebas prolongadas y mediciones de consumo.
8. Publicar el ejecutable `win-x64` self-contained.

## 16. Criterios de finalización

El MVP se considerará terminado cuando:

- El usuario pueda elegir un micrófono e iniciar la monitorización manualmente.
- El nivel se muestre de forma estable y comprensible.
- Los picos breves no produzcan alertas.
- Un exceso sostenido produzca un único pitido según los tiempos configurados.
- El sistema se rearme al bajar claramente del umbral.
- La aplicación no interfiera con otras aplicaciones que usan el micrófono.
- Las preferencias sobrevivan a un reinicio de la aplicación.
- Los errores de permisos y dispositivos sean recuperables desde la interfaz.
- No exista persistencia ni transmisión de audio.
- Las pruebas automatizadas y manuales definidas en este documento estén superadas.

## 17. Referencias técnicas

- Microsoft, configuración de proyectos de escritorio WPF: https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props-desktop
- NAudio, ejemplo de medidor de nivel de grabación: https://github.com/naudio/NAudio/blob/master/Docs/RecordingLevelMeter.md
