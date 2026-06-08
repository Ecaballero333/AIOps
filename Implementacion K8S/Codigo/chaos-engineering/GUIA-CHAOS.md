# Guia breve de scripts de chaos engineering

Estos scripts permiten validar como responde PharmaGo ante fallas controladas en Kubernetes. Se recomienda ejecutarlos primero en staging o prueba; en produccion solo deberian usarse con monitoreo activo, ventana acordada, limites de impacto y plan de rollback. Para que una alerta se dispare, la condicion debe sostenerse durante su ventana de evaluacion, por lo que una prueba corta puede verse en dashboards sin generar alerta.

## `load-requests.sh`

Genera muchas requests `POST` concurrentes contra `/api/login` o contra la URL indicada. Sirve para probar inyeccion de requests, aumento de throughput, latencia, errores HTTP y posibles respuestas `429` si actua el rate limit. El impacto deberia verse principalmente en las metricas de peticiones por segundo, throughput total, latencia promedio, tasa de error, status codes e intentos de login.

Revisar los dashboards `PharmaGo - Overview`, `PharmaGo - Endpoint Analysis` y `PharmaGo - Business`. Las alertas esperadas son `PharmaGo - Rate limit HTTP 429 > 10 en 5m`, `PharmaGo - Alta Tasa de Error > 1%`, `PharmaGo - Latencia promedio > 1000ms` y, si los login fallidos generan eventos de negocio, `PharmaGo - Eventos de negocio fallidos > 5 en 5m`. Se resuelve esperando a que termine el script o reduciendo la cantidad de requests; si algun pod queda degradado, se puede recrear con `kubectl delete pod -n pharmago <pod>`.

## `cpu-spike.sh`

Ejecuta loops dentro de un pod para consumir CPU durante una cantidad de segundos. Sirve para probar sobrecarga de CPU en un deployment, por ejemplo `pharmago-api-gateway`, y observar si aparecen throttling, aumento de latencia o timeouts. Las metricas principales son CPU por pod, CPU del nodo, latencia promedio y tasa de error.

Revisar `PharmaGo - Infra`, `PharmaGo - Overview` y `PharmaGo - SLIs SLOs`. Las alertas esperadas son `PharmaGo - CPU alta > 80%`, `PharmaGo - Latencia promedio > 1000ms` y `PharmaGo - Alta Tasa de Error > 1%`. Se resuelve esperando a que finalice; si quedan procesos consumiendo CPU, borrar el pod para que Kubernetes lo recree.

## `ram-spike.sh`

Escribe datos en `/dev/shm/fill` o `/tmp/fill` para consumir memoria dentro del pod. Sirve para probar sobrecarga de memoria y puede terminar en reinicio del contenedor si supera los limites configurados. Las metricas principales son memoria por pod y memoria usada del nodo.

Revisar `PharmaGo - Infra`. La alerta esperada es `PharmaGo - Memoria alta > 85%` si el consumo afecta la memoria total del nodo. Se resuelve limpiando los archivos generados (`/dev/shm/fill` o `/tmp/fill`) o recreando el pod si queda inestable.

## `volume-spike.sh`

Escribe un archivo grande en `/tmp/chaos_fill` para consumir storage efimero. Sirve para probar sobrecarga de storage y puede provocar fallos de escritura, degradacion de la aplicacion o eviccion del pod si hay presion de disco. La metrica principal es el espacio disponible en disco.

Revisar `PharmaGo - Infra`, especialmente `Disco disponible %`. Se resuelve eliminando `/tmp/chaos_fill` dentro del pod o recreando el pod si queda inconsistente.

## `network-interruption.sh`

Aplica una `NetworkPolicy` temporal que bloquea ingreso y salida de red del deployment indicado. Sirve para probar interrupcion de trafico de red: el componente queda aislado, no recibe requests y tampoco puede llamar a otros servicios. Si se aplica al API Gateway, la aplicacion puede quedar inaccesible durante la prueba.

Revisar `PharmaGo - Overview`, `PharmaGo - Endpoint Analysis` y `PharmaGo - SLIs SLOs`, observando throughput, tasa de error, latencia, availability y status codes. Las alertas esperadas son `PharmaGo - Alta Tasa de Error > 1%` y `PharmaGo - Latencia promedio > 1000ms`, siempre que haya trafico durante el corte. Se resuelve automaticamente al finalizar; si se interrumpe el script, borrar la policy con `kubectl delete networkpolicy -n pharmago chaos-deny-network-<deployment>`.

## `disconnect-component.sh`

Escala temporalmente un deployment a `0` replicas para simular la caida de un componente interno. Si cae `pharmago-users-service`, pueden fallar login y usuarios; si cae `pharmago-pharmacy-service`, pueden fallar operaciones de farmacia; si cae `pharmago-db`, puede degradarse gran parte del backend. Las metricas principales son errores HTTP, latencia, availability y eventos de negocio fallidos.

Revisar `PharmaGo - Overview`, `PharmaGo - Endpoint Analysis`, `PharmaGo - Business` y `PharmaGo - SLIs SLOs`. Las alertas esperadas son `PharmaGo - Alta Tasa de Error > 1%`, `PharmaGo - Latencia promedio > 1000ms` y `PharmaGo - Eventos de negocio fallidos > 5 en 5m`. El script restaura las replicas originales al terminar; si se corta antes, restaurar manualmente con `kubectl scale deployment -n pharmago <deployment> --replicas=1`.
