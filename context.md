## Contexto y problemática

La aplicación nace de una situación habitual al jugar a videojuegos utilizando auriculares: al tener los oídos parcialmente aislados del entorno y escuchar el propio sonido del juego o de una conversación por voz, resulta difícil percibir con precisión el volumen real al que se está hablando.

Como consecuencia, el usuario puede elevar progresivamente la voz sin darse cuenta, especialmente durante momentos de concentración, tensión o emoción dentro del juego. Esto puede generar molestias a otras personas que se encuentren en la misma vivienda, aunque el usuario no tenga intención de hablar en voz alta.

El objetivo es desarrollar una aplicación ligera para escritorio que permita supervisar en tiempo real el nivel de entrada del micrófono y proporcionar una señal clara cuando la intensidad de la voz supere un determinado umbral.

La aplicación no necesita interpretar, reconocer ni almacenar lo que el usuario dice. Únicamente debe analizar el nivel de la señal de audio procedente del micrófono para determinar si se está hablando demasiado fuerte. El funcionamiento debe realizarse completamente en local.

## Objetivo principal

Crear una herramienta discreta y de bajo consumo que ayude al usuario a regular de forma inconsciente su volumen de voz mientras utiliza auriculares.

La aplicación deberá:

* Obtener en tiempo real el nivel de entrada del micrófono seleccionado.
* Mostrar visualmente la intensidad de la voz mediante un medidor de volumen.
* Permitir configurar un umbral a partir del cual se considere que el usuario está hablando demasiado alto.
* Generar una alerta cuando dicho umbral se supere durante un periodo configurable.
* Evitar falsas alertas provocadas por picos de audio muy breves.
* Mantener un consumo mínimo de CPU, memoria y recursos del sistema.

## Sistemas de aviso

La aplicación podrá utilizar uno o varios mecanismos de feedback:

* Cambio de color del indicador de volumen.
* Pequeño aviso visual superpuesto en pantalla.
* Ventana flotante configurable como «siempre visible».
* Sonido breve reproducido cuando se supera el umbral.
* Indicador en la bandeja del sistema.
* Periodo de espera entre alertas para evitar avisos continuos.

El sistema de aviso debe ser suficientemente perceptible durante una partida, pero no tan intrusivo como para interferir con el juego.

## Medición del volumen

No es necesario medir decibelios acústicos reales en dB SPL, ya que el micrófono y el sistema operativo normalmente proporcionan el nivel de la señal digital.

La aplicación puede trabajar con valores relativos de amplitud, RMS, nivel de pico o dBFS.

El objetivo no es determinar científicamente cuántos decibelios produce la voz en la habitación, sino detectar cuándo el nivel captado por el micrófono supera el nivel habitual correspondiente a una conversación de volumen aceptable.

Por este motivo, deberá existir algún mecanismo sencillo de calibración o ajuste manual del umbral.

## Privacidad y seguridad

La privacidad es un requisito fundamental debido a que la aplicación tendrá acceso permanente al micrófono.

El diseño deberá seguir los siguientes principios:

* Todo el procesamiento del audio debe realizarse localmente.
* El audio no debe enviarse a servidores externos.
* No debe existir reconocimiento de voz ni transcripción.
* No deben almacenarse grabaciones.
* Las muestras de audio utilizadas para calcular el nivel deben descartarse inmediatamente después de procesarlas.
* La aplicación no debe requerir cuentas de usuario.
* No debe incluir telemetría innecesaria.
* No debe necesitar conexión a Internet para funcionar.

Idealmente, la aplicación podrá funcionar correctamente incluso aunque se bloquee completamente su acceso a Internet mediante el firewall del sistema operativo.

Los únicos datos persistentes que deberían almacenarse son preferencias de configuración, como el micrófono seleccionado, el nivel del umbral, el tipo de aviso, el tiempo mínimo necesario para activar una alerta y el periodo de espera entre avisos.

## Filosofía de diseño

La aplicación debe tener un propósito deliberadamente limitado: ayudar al usuario a detectar cuándo está elevando demasiado la voz.

No pretende convertirse en una herramienta de grabación, edición, comunicación o procesamiento avanzado de audio.

Las prioridades del desarrollo son:

1. Privacidad.
2. Bajo consumo de recursos.
3. Respuesta en tiempo real.
4. Facilidad de configuración.
5. Interfaz mínima y poco intrusiva.
6. Funcionamiento fiable durante sesiones prolongadas de juego.

El resultado esperado es una pequeña utilidad que pueda permanecer ejecutándose durante horas en segundo plano y actuar como una referencia externa del volumen de voz cuando el propio usuario, debido al uso de auriculares, pierde la percepción de cuánto está elevando la voz.
