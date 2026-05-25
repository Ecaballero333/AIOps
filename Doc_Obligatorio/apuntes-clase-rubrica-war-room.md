# Apuntes de clase sobre la letra del obligatorio

Este documento resume comentarios extraidos de la clase en la que el docente explico la letra del obligatorio. No reemplaza la letra oficial, pero ayuda a interpretar que evidencia conviene generar y como orientar el trabajo del equipo.

## Informacion general

| Tema | Comentario |
| --- | --- |
| Enfoque | El obligatorio no consiste en desarrollar funcionalidades nuevas, sino en operar y hacer resiliente una aplicacion existente. |
| Equipos | Grupos de hasta 3 integrantes. |
| Dedicacion estimada | Aproximadamente 2.5 horas por persona por semana, es decir unas 7.5 horas semanales por equipo. |
| Entrega | Se entrega por Gestion. Todos los productos de trabajo deben estar versionados en GitHub. |
| Evidencia | El docente corregira el informe y buscara evidencia directamente en el repositorio mediante enlaces a archivos concretos. |
| Informe final | Documento academico de hasta 10 paginas, estructurado con subtitulos alineados a cada item de la rubrica. |

## Interpretacion de la rubrica

La rubrica tiene 8 items de 5 puntos cada uno. Para cada item conviene dejar evidencia verificable en el repositorio y, cuando aplique, capturas o comandos reproducibles en el informe.

### 1. Implementacion de plataforma

Requisitos principales:

- El sistema debe correr obligatoriamente sobre Kubernetes.
- Cada microservicio debe estar en un `Deployment` independiente.
- Se debe incluir un diagrama de despliegue adaptado a Kubernetes.

Notas de clase:

- El diagrama original del proyecto estaba orientado a Docker Compose, por lo que hay que adaptarlo a la arquitectura Kubernetes.
- Se puede usar UML2, C4 u otro estandar razonable de arquitectura.
- El diagrama debe reflejar componentes principales: frontend, API Gateway, microservicios, base de datos y stack de observabilidad.

Evidencia sugerida:

- Manifiestos en `Implementacion K8S/Codigo/k8s/`.
- Salida de `kubectl get deploy -n pharmago`.
- Salida de `kubectl get svc -n pharmago`.
- Diagrama de despliegue versionado en el repo.

### 2. Alta disponibilidad y autocuracion

Requisitos principales:

- Los microservicios deben curarse solos ante excepciones en tiempo de ejecucion.
- Luego de fallar, deben volver al estado `READY`.
- El sistema debe mitigar ataques o altos volumenes de solicitudes fallidas.

Notas de clase:

- El docente menciono patrones como IP-based Rate Limitation.
- El foco no es evitar toda falla, sino reducir MTTR y demostrar recuperacion automatica.

Evidencia sugerida:

- `startupProbe`, `readinessProbe` y `livenessProbe` en deployments.
- Prueba de eliminacion/reinicio de pods y recuperacion automatica.
- Prueba de rate limiting con respuestas HTTP `429`.
- Metricas de errores, requests y latencia durante la prueba.

### 3. Despliegues seguros

Requisitos principales:

- Garantizar 100% de disponibilidad durante el reemplazo de componentes.
- Documentar y justificar la tecnica elegida.

Notas de clase:

- Tecnicas posibles: Blue-Green, Canary o Rolling Update bien configurado.
- No alcanza con nombrar el patron: hay que explicar como se ajustan sus parametros para evitar downtime.

Evidencia sugerida:

- Manifiestos o scripts de despliegue seguro.
- `kubectl rollout status`.
- Prueba con trafico activo mientras se reemplaza una version.
- Explicacion de parametros como replicas, `maxUnavailable`, `maxSurge`, readiness y health checks.

### 4. Telemetria: logs, trazas y metricas

Requisitos principales:

- Logs estructurados en JSON.
- Logs consultables en Kibana/Elasticsearch.
- Trazas de acuerdo a OTLP.
- Metricas de infraestructura, aplicacion y negocio.

Notas de clase:

- Para trazas, alcanza con propagar contexto y que los logs incluyan un `trace_id` o identificador equivalente.
- Se esperan al menos 15 metricas en total:
  - 5 de infraestructura, por ejemplo CPU, RAM, storage, red y estado de pods/nodos.
  - 5 de aplicacion, por ejemplo requests, latencia, errores, status codes y throughput.
  - 5 de negocio, por ejemplo medicamentos comprados u otros eventos propios del dominio.
- Se deben usar las interfaces ya definidas en el proyecto `Instrumentation` provisto en el codigo base.

Evidencia sugerida:

- Configuracion de logs JSON en servicios .NET.
- Logs visibles en Kibana.
- Propagacion de correlation id, trace id o contexto equivalente entre gateway y microservicios.
- Dashboards de Grafana con metricas de app, infra y negocio.
- Codigo que usa interfaces de `Instrumentation`.

### 5. Alertas

Requisitos principales:

- Configurar al menos 5 alertas en Grafana.
- Las alertas deben detectar anomalias sobre metricas definidas.

Notas de clase:

- Los umbrales deben tener justificacion.
- Antes de Machine Learning, se pueden usar tacticas estadisticas o heuristicas, por ejemplo intervalos de confianza, percentiles o umbrales basados en comportamiento esperado.

Evidencia sugerida:

- Alertas versionadas o documentadas.
- Capturas o export de reglas de Grafana.
- Explicacion del umbral de cada alerta.
- Prueba que dispare al menos alguna alerta durante chaos engineering.

### 6. Tecnicas de deteccion de anomalias

Requisitos principales:

- Entregar notebooks con Isolation Forest.
- Entregar notebooks con SVM, Support Vector Machines.
- Usar datasets que el docente proveera.

Notas de clase:

- Esta parte esta separada de las alertas heuristicas.
- Las notebooks deben reportar resultados, no solo ejecutar codigo.

Evidencia sugerida:

- Notebooks `.ipynb` versionadas.
- Dataset o referencia al dataset usado.
- Comparacion de resultados.
- Breve interpretacion de falsos positivos/falsos negativos si aplica.

### 7. Plan de contencion de incidentes

Requisitos principales:

- Disenar un plan de mitigacion de incidentes operacionales.
- Cubrir todas las fases del framework de Atlassian:
  - Prepare
  - Detect
  - Respond
  - Recover
  - Learn

Notas de clase:

- El plan debe poder usarse durante la defensa, no ser solo teorico.
- Debe definir roles, canales de comunicacion, criterios de severidad, pasos de mitigacion y aprendizaje posterior.

Evidencia sugerida:

- Runbook o seccion de informe con fases Atlassian.
- Roles del war room.
- Tabla de severidades.
- Procedimientos de mitigacion.
- Template de postmortem.

### 8. Scripts de caos

Requisitos principales:

- Implementar scripts Bash para simular:
  - Inyeccion de requests.
  - Sobrecarga de CPU.
  - Saturacion de RAM.
  - Llenado de storage.
  - Interrupcion de trafico de red.
  - Desconexion de componentes internos de la aplicacion.

Notas de clase:

- Los scripts deben permitir observar efectos en dashboards y logs.
- Deben tener alcance controlado y una forma clara de revertir o detener el experimento.

Evidencia sugerida:

- Scripts en `chaos-engineering/`.
- README con uso, hipotesis, blast radius y rollback.
- Capturas o metricas observadas en Grafana/Kibana.
- Relacion entre cada script y una alerta o metrica.

## Defensa: War Room

La defensa sera virtual y funcionara como un simulacro en vivo.

Formato esperado:

- El docente o el equipo ejecutara un script de ingenieria de caos.
- El equipo debera actuar como en un War Room.
- Se deberan usar dashboards, logs y metricas para diagnosticar.
- Se debera aplicar el plan de mitigacion disenado.
- Se deberan explicar los mecanismos implementados y por que garantizan el comportamiento observado.

Preparacion recomendada:

1. Tener Minikube levantado y todos los pods `Running`.
2. Tener `port-forward.sh` activo.
3. Tener abiertas las herramientas:
   - Frontend.
   - Grafana.
   - Prometheus.
   - Kibana.
4. Tener una lista corta de comandos de diagnostico:

```bash
kubectl get pods -n pharmago
kubectl get deploy -n pharmago
kubectl logs -n pharmago deploy/pharmago-api-gateway --tail=100
kubectl logs -n pharmago deploy/pharmago-users-service --tail=100
kubectl logs -n pharmago deploy/pharmago-pharmacy-service --tail=100
```

5. Tener definido quien cumple cada rol:
   - Coordinacion del incidente.
   - Observabilidad y diagnostico.
   - Ejecucion de mitigacion.
   - Comunicacion y registro de hallazgos.

## Implicancias para organizar el equipo

Una division razonable para 3 integrantes:

| Integrante | Foco | Entregables |
| --- | --- | --- |
| Integrante 1 | Despliegues seguros y alta disponibilidad | Estrategia de deploy, replicas/probes, pruebas de cero downtime, evidencia de rollout. |
| Integrante 2 | Telemetria y chaos engineering | Logs, trazas/contexto, metricas, dashboards, alertas y scripts de caos. |
| Integrante 3 | ML, plan de incidentes e informe | Notebooks IsolationForest/SVM, plan Atlassian, diagrama y consolidacion del informe. |

## Checklist inicial derivado de la clase

| Tarea | Estado sugerido |
| --- | --- |
| Verificar despliegue completo en Kubernetes | Pendiente/En progreso |
| Adaptar diagrama a Kubernetes | Pendiente |
| Validar microservicios en deployments independientes | En progreso |
| Probar autocuracion ante reinicio/falla | Pendiente |
| Validar rate limiting | Pendiente |
| Definir tecnica de despliegue seguro | Pendiente |
| Completar trazas o propagacion de contexto | Pendiente |
| Confirmar logs JSON en todos los servicios | Pendiente |
| Definir 15 metricas: 5 infra, 5 app, 5 negocio | Pendiente |
| Crear 5 alertas Grafana con umbrales justificados | Pendiente |
| Crear notebooks IsolationForest y SVM | Pendiente |
| Redactar plan Atlassian | Pendiente |
| Completar scripts de caos faltantes | Pendiente |
| Preparar guion de defensa War Room | Pendiente |

