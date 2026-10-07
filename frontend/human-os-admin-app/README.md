# HumanOS Admin App

Scaffold inicial del panel administrativo de HumanOS para FlatBit.

## Desarrollo

```bash
npm install
npm run dev
```

La app utiliza el puerto `6200` y deja `/api` preparado para el backend local de HumanOS en `http://localhost:7071`.

## Inicio de sesión

La consola usa Microsoft Entra External ID mediante MSAL con el registro
`HumanOSAdmin` (`118e8295-1e5b-4570-ae14-ee8a2ad8a156`) y el flujo
`humanosflow`. El URI de redirección configurado para desarrollo es
`http://localhost:6200`; debe coincidir exactamente con el URI de tipo SPA
registrado en Entra.

## Estadisticas del engram

Los tokens de entrada y salida se miden desde la perspectiva del modelo.
En "Sesiones de voz (Realtime)", la entrada incluye instrucciones, contenido,
historial y audio del estudiante cuando lo hay; la salida corresponde a lo
generado por el agente. Estos totales no distinguen audio, texto ni cache.

El tiempo de cada paso termina como maximo al iniciar el siguiente paso de
la misma sesion de curso, incluso si pertenece a otro engram. Este limite
tambien se aplica a registros historicos sin cierre o con un cierre tardio,
sin modificar los timestamps originales.

Las visitas nuevas miden tiempo visible y activo con un reloj monotonico:
se pausa al ocultar la pestana o perder el foco y se suma al regresar,
cambiar de paso o volver a un paso anterior. Se guarda un acumulado cada
10 segundos y al salir; los reintentos no duplican el tiempo. Completar
una actividad no detiene el reloj mientras el paso siga visible.
Los intervalos no aumentan el numero de visitas: se cuentan sesiones de
curso distintas y el promedio divide el tiempo acumulado entre ellas.
Los totales de sesion usan la misma suma de pasos, no el tiempo de pared.

Un cierre abrupto sin evento final conserva el ultimo acumulado recibido
(pueden faltar hasta 10 segundos en condiciones normales de conectividad).
Las visitas anteriores se identifican como estimaciones: no se inventa
cuanto tiempo permanecieron visibles.

Si no existen registros de tiempo para un paso, el panel muestra "Sin registro"
en lugar de "0s". Si hay tokens, aclara que hubo actividad sin captura de tiempo
y no presenta "0 visitas" como si el paso no se hubiera utilizado. Un cero
solo representa duracion cero cuando existe al menos un registro de tiempo.

## Consumo de tokens

Las llamadas nuevas guardan eventos independientes por persona, curso, engram,
sesion, paso, canal y modelo; no actualizan un acumulado compartido.
El observador Realtime suma input_tokens y output_tokens de response.done
una sola vez por response.id. El navegador reporta duracion, no tokens.
Text Professor guarda el Usage real de cada respuesta, incluyendo el contexto
y el historial enviados al modelo. Production, Recall y Assessment tambien
registran sus llamadas de texto; no son conversaciones con Text Professor.

La suma por paso es voz + texto. La suma de los seis pasos mas el consumo
historico sin paso asignado concilia con el total del engram. Los datos
historicos mezclados por el antiguo acumulado no pueden redistribuirse sin
eventos originales. Los costos siguen siendo estimaciones: el ledger no
separa audio/texto ni cache de Realtime, por lo que no verifica una factura.
Los tokens de una llamada de voz se guardan al cerrarse el observador, no
en vivo; un reinicio del backend durante la llamada puede perder ese consumo.
