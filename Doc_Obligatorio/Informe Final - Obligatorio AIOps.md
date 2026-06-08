# Universidad ORT Uruguay

## Facultad de Ingeniería

## Informe Final: Resiliencia, Observabilidad y

## Respuesta a Incidentes en PharmaGo

## Obligatorio AIOps

## Eric Poplawski - 258327

## Santiago Martorell - 218845

## Eduardo Caballero - 175016

## Año 2026


## 1. Implementación de plataforma

**Puntaje de la rúbrica:** 5 puntos
**_Comentario de la rúbrica (Resultados esperados):_** _El sistema debe correr sobre
Kubernetes. Cada microservicio debe ejecutar en un deployment independiente. Se debe
implementar un diagrama de despliegue de los componentes principales de la plataforma._

### Desafíos enfrentados

Describir los principales desafíos técnicos y operacionales encontrados al implementar este
punto, incluyendo decisiones, restricciones del stack, problemas de integración y trade-offs
relevantes.

### Implementación realizada

Explicar qué se implementó, dónde se encuentra en el repositorio y cómo se integra con el
resto de la plataforma. Incluir comandos, configuración o fragmentos relevantes cuando ayuden
a reproducir el resultado.

### Resultados obtenidos

Reportar el comportamiento observado, mediciones obtenidas y evidencia de cumplimiento
frente a lo esperado por la rúbrica. Cuando corresponda, relacionar los resultados con MTTR,
disponibilidad, mantenibilidad, observabilidad o desplegabilidad.

### Evidencia a incluir

● Manifiestos Kubernetes aplicados.
● Listado de deployments y servicios por microservicio.
● Diagrama de despliegue de la plataforma.
● Explicación de namespace, servicios, configmaps, secrets y persistencia si aplica.


## 2. Alta disponibilidad

**Puntaje de la rúbrica:** 5 puntos
**_Comentario de la rúbrica (Resultados esperados):_** _Los microservicios deben curarse
automáticamente en casos de errores en tiempo de ejecución. Una vez que las réplicas de la
aplicación llegan a un estado READY, deben poder volver al mismo estado luego de
excepciones. El sistema debe ser resiliente a ataques que inciten fallas reiteradas; se debe
implementar algún patrón que mitigue la eventualidad de tener un alto volumen de solicitudes
fallidas._

### Desafíos enfrentados

Describir los principales desafíos técnicos y operacionales encontrados al implementar este
punto, incluyendo decisiones, restricciones del stack, problemas de integración y trade-offs
relevantes.

### Implementación realizada

Explicar qué se implementó, dónde se encuentra en el repositorio y cómo se integra con el
resto de la plataforma. Incluir comandos, configuración o fragmentos relevantes cuando ayuden
a reproducir el resultado.

### Resultados obtenidos

Reportar el comportamiento observado, mediciones obtenidas y evidencia de cumplimiento
frente a lo esperado por la rúbrica. Cuando corresponda, relacionar los resultados con MTTR,
disponibilidad, mantenibilidad, observabilidad o desplegabilidad.

### Evidencia a incluir

● Pruebas de recuperación automática ante excepciones o reinicios.
● Configuración de probes y réplicas.
● Patrón de mitigación elegido, por ejemplo rate limiting, circuit breaker o backoff.
● Mediciones que evidencien reducción del MTTRestore.


## 3. Despliegues seguros

**Puntaje de la rúbrica:** 5 puntos
**_Comentario de la rúbrica (Resultados esperados):_** _El sistema debe poder reemplazar sus
componentes garantizando 100% de disponibilidad. Se necesita documentar y explicar la
técnica elegida._

### Desafíos enfrentados

Describir los principales desafíos técnicos y operacionales encontrados al implementar este
punto, incluyendo decisiones, restricciones del stack, problemas de integración y trade-offs
relevantes.

### Implementación realizada

Explicar qué se implementó, dónde se encuentra en el repositorio y cómo se integra con el
resto de la plataforma. Incluir comandos, configuración o fragmentos relevantes cuando ayuden
a reproducir el resultado.

### Resultados obtenidos

Reportar el comportamiento observado, mediciones obtenidas y evidencia de cumplimiento
frente a lo esperado por la rúbrica. Cuando corresponda, relacionar los resultados con MTTR,
disponibilidad, mantenibilidad, observabilidad o desplegabilidad.

### Evidencia a incluir

● Técnica seleccionada y justificación.
● Pasos de ejecución del despliegue.
● Evidencia de disponibilidad durante el reemplazo.
● Procedimiento de rollback o mitigación ante falla.


## 4. Telemetría

Se incorporó una capa de telemetría para observar el comportamiento técnico y funcional de PharmaGo sobre Kubernetes. La instrumentación se implementó con OpenTelemetry en los servicios .NET, publicando métricas bajo el meter `PharmaGo.CustomMetrics` y enviándolas al OTLP Collector. Desde allí se exponen a Prometheus y se visualizan en Grafana. Los logs estructurados se emiten en JSON con información de contexto y se recolectan con Fluent Bit hacia ElasticSearch, quedando disponibles para consulta en Kibana.

Las métricas agregadas cubren tres niveles. A nivel de aplicación se registran intentos de login por resultado (`pharmago_login_attempts_total`), eventos de negocio por tipo y resultado (`pharmago_business_events_total`), compras creadas (`pharmago_purchases_created_total`), monto acumulado de compras (`pharmago_purchase_amount_total`), cantidad de items comprados (`pharmago_purchase_items_total`) y cambios de estado de compras (`pharmago_purchase_status_changes_total`). A nivel HTTP se registran requests por endpoint, método y código de estado (`pharmago_http_requests_total`), errores por endpoint y tipo (`pharmago_http_errors_total`) y duración por endpoint (`pharmago_http_request_duration_milliseconds`). A nivel infraestructura se recolectan métricas de pods, contenedores y nodo mediante Prometheus, kube-state/cAdvisor y node-exporter, incluyendo CPU, memoria, red, filesystem/storage y estado de workloads.

En Grafana se definieron dashboards separados para operación general, infraestructura y negocio. El dashboard de aplicación permite observar throughput, latencia promedio, tasa de error, distribución de status codes y errores 429. El dashboard de negocio muestra logins por resultado, eventos fallidos, eventos por tipo, compras creadas, monto total vendido, items promedio por compra y cambios de estado. Las consultas usan PromQL con `rate`, `increase` y agregaciones por etiquetas para analizar tanto tasas como cantidades acumuladas según el caso.

También se configuraron alertas orientadas a anomalías operativas: incremento de errores HTTP, exceso de respuestas 429, latencia elevada, caída o falta de disponibilidad de pods, y presión de recursos de infraestructura. Con esta telemetría es posible detectar degradaciones, relacionarlas con endpoints o eventos de negocio concretos y reducir el tiempo de diagnóstico durante incidentes.

## 5. Técnicas de detección de anomalías

**Puntaje de la rúbrica:** 5 puntos
**_Comentario de la rúbrica (Resultados esperados):_** _Deben implementarse los algoritmos
IsolationForest y Support Vector Machine para datasets provistos por los docentes. Se deben
entregar las notebooks de cada experimento y reportar los resultados._

### Desafíos enfrentados

Describir los principales desafíos técnicos y operacionales encontrados al implementar este
punto, incluyendo decisiones, restricciones del stack, problemas de integración y trade-offs
relevantes.

### Implementación realizada

Explicar qué se implementó, dónde se encuentra en el repositorio y cómo se integra con el
resto de la plataforma. Incluir comandos, configuración o fragmentos relevantes cuando ayuden
a reproducir el resultado.

### Resultados obtenidos

Reportar el comportamiento observado, mediciones obtenidas y evidencia de cumplimiento
frente a lo esperado por la rúbrica. Cuando corresponda, relacionar los resultados con MTTR,
disponibilidad, mantenibilidad, observabilidad o desplegabilidad.

### Evidencia a incluir

● Notebook de IsolationForest.
● Notebook de Support Vector Machine.
● Descripción del dataset utilizado.
● Variables/features seleccionadas.
● Resultados, métricas, gráficos y comparación entre técnicas.
● Conclusiones sobre utilidad para operación del sistema.


## 6. Plan de contención de incidentes operacionales

**Puntaje de la rúbrica:** 5 puntos
**_Comentario de la rúbrica (Resultados esperados):_** _Se debe realizar un plan de mitigación
de incidentes operacionales que implemente todas las fases del framework de Atlassian._

### Desafíos enfrentados

Describir los principales desafíos técnicos y operacionales encontrados al implementar este
punto, incluyendo decisiones, restricciones del stack, problemas de integración y trade-offs
relevantes.

### Implementación realizada

Explicar qué se implementó, dónde se encuentra en el repositorio y cómo se integra con el
resto de la plataforma. Incluir comandos, configuración o fragmentos relevantes cuando ayuden
a reproducir el resultado.

### Resultados obtenidos

Reportar el comportamiento observado, mediciones obtenidas y evidencia de cumplimiento
frente a lo esperado por la rúbrica. Cuando corresponda, relacionar los resultados con MTTR,
disponibilidad, mantenibilidad, observabilidad o desplegabilidad.

### Evidencia a incluir

● Descripción de fases del framework de Atlassian aplicadas al contexto del sistema.
● Roles y responsabilidades durante el incidente.
● Canales de comunicación y escalamiento.
● Criterios de severidad y priorización.
● Runbooks o pasos de mitigación.
● Formato de postmortem y acciones correctivas.


## 7. Scripts de caos

**Puntaje de la rúbrica:** 5 puntos
**_Comentario de la rúbrica (Resultados esperados):_** _Se deben implementar scripts de caos
que prueben inyección de requests, sobrecarga de CPU, sobrecarga de memoria, sobrecarga
de storage, interrupción de tráfico de red y desconexión de componentes internos de la
aplicación._

### Desafíos enfrentados

Describir los principales desafíos técnicos y operacionales encontrados al implementar este
punto, incluyendo decisiones, restricciones del stack, problemas de integración y trade-offs
relevantes.

### Implementación realizada

Explicar qué se implementó, dónde se encuentra en el repositorio y cómo se integra con el
resto de la plataforma. Incluir comandos, configuración o fragmentos relevantes cuando ayuden
a reproducir el resultado.

### Resultados obtenidos

Reportar el comportamiento observado, mediciones obtenidas y evidencia de cumplimiento
frente a lo esperado por la rúbrica. Cuando corresponda, relacionar los resultados con MTTR,
disponibilidad, mantenibilidad, observabilidad o desplegabilidad.

### Evidencia a incluir

● Listado de scripts implementados y ubicación en el repositorio.
● Hipótesis de cada experimento.
● Alcance o blast radius definido.
● Comandos de ejecución y reversión.
● Métricas y alertas observadas durante cada experimento.
● Resultados y aprendizajes obtenidos.

## Cierre del informe

Sintetizar el impacto global de las prácticas implementadas sobre la respuesta a incidentes, la
disponibilidad, la desplegabilidad y la mantenibilidad del sistema. Incluir una reflexión breve
sobre qué funcionó bien, qué limitaciones quedaron y qué mejoras futuras serían prioritarias.


