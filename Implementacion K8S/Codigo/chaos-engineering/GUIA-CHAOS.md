# Guia breve de scripts de chaos engineering

Estos scripts permiten validar como responde PharmaGo ante fallas controladas en Kubernetes. Se recomienda ejecutarlos primero en staging o prueba; en produccion solo deberian usarse con monitoreo activo, ventana acordada, limites de impacto y plan de rollback. Para que una alerta se dispare, la condicion debe sostenerse durante su ventana de evaluacion, por lo que una prueba corta puede verse en dashboards sin generar alerta.

## `load-requests.sh`

Genera muchas requests `POST` concurrentes contra `/api/login` o contra la URL indicada. Sirve para probar inyeccion de requests, aumento de throughput, latencia, errores HTTP y posibles respuestas `429` si actua el rate limit. El impacto deberia verse principalmente en las metricas de peticiones por segundo, throughput total, latencia promedio, tasa de error, status codes e intentos de login.

Revisar los dashboards `PharmaGo - Overview`, `PharmaGo - Endpoint Analysis` y `PharmaGo - Business`. Las alertas esperadas son `PharmaGo - Rate limit HTTP 429 > 10 en 5m`, `PharmaGo - Alta Tasa de Error > 1%`, `PharmaGo - Latencia promedio > 1000ms` y, si los login fallidos generan eventos de negocio, `PharmaGo - Eventos de negocio fallidos > 5 en 5m`. Se resuelve esperando a que termine el script o reduciendo la cantidad de requests; si algun pod queda degradado, se puede recrear con `kubectl delete pod -n pharmago <pod>`.

## `app-latency-ramp.sh`

Activa una latencia artificial dentro de la aplicacion escribiendo archivos de control en `/tmp` dentro de los pods del deployment elegido. Como el delay ocurre dentro del middleware de metricas, se ve en `pharmago_http_request_duration_milliseconds_ms_*` y en el panel `Latencia Promedio por Endpoint`.

El perfil default sube y baja la latencia en pasos de 15 segundos y termina dejando una degradacion fuerte de 5000 ms: `500,1000,1500,2000,3000,4000,2500,1500,3000,1000,5000`. Por defecto no afecta `/health` para que Kubernetes no saque los pods del Service y Prometheus pueda seguir recolectando metricas durante toda la demo. Si se quiere mostrar tambien el efecto en readiness, ejecutar con `include_health=true`; al superar el timeout del probe, los pods pueden pasar a `NotReady`.

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

## `pharmacy-network-latency.sh`

Agrega latencia con `tc netem` sobre `eth0` en un solo pod de `pharmago-pharmacy-service`. Sirve para demostrar una falla temporal donde `/health` tarda mas que el `timeoutSeconds` del readiness probe, Kubernetes marca ese pod como `NotReady` y el Service deja de incluirlo en sus endpoints mientras el otro pod sano sigue atendiendo.

Requiere que la imagen de Pharmacy tenga `iproute2` instalado y que el contenedor tenga la capability `NET_ADMIN`. El script valida ambas cosas antes de aplicar el caos. Al final pregunta si se quiere restituir la red; si se responde que no, la latencia queda activa hasta ejecutar el comando de restauracion que imprime el script o hasta recrear el pod.

## `disconnect-component.sh`

Escala temporalmente un deployment a `0` replicas para simular la caida de un componente interno. Si cae `pharmago-users-service`, pueden fallar login y usuarios; si cae `pharmago-pharmacy-service`, pueden fallar operaciones de farmacia; si cae `pharmago-db`, puede degradarse gran parte del backend. Las metricas principales son errores HTTP, latencia, availability y eventos de negocio fallidos.

Revisar `PharmaGo - Overview`, `PharmaGo - Endpoint Analysis`, `PharmaGo - Business` y `PharmaGo - SLIs SLOs`. Las alertas esperadas son `PharmaGo - Alta Tasa de Error > 1%`, `PharmaGo - Latencia promedio > 1000ms` y `PharmaGo - Eventos de negocio fallidos > 5 en 5m`. El script restaura las replicas originales al terminar; si se corta antes, restaurar manualmente con `kubectl scale deployment -n pharmago <deployment> --replicas=1`.

## `disconnect-db-service.sh`

Cambia temporalmente el selector del `Service` de base de datos para que `pharmago-db` quede sin endpoints, sin apagar el pod de SQL Server. Sirve para demostrar una falla de descubrimiento/conectividad interna: `pharmago-users-service` y `pharmago-pharmacy-service` siguen corriendo, pero las conexiones a `Server=pharmago-db` fallan porque el Service no apunta a ningun pod.

Uso recomendado para demo: primero hacer un login exitoso con la base sana y guardar el token. Luego ejecutar `./disconnect-db-service.sh pharmago-db pharmago`, intentar otro login para generar `login_db_lookup_fail`, y despues llamar un endpoint protegido con el token anterior para generar `auth_db_lookup_fail` en el filtro de autorizacion. Confirmar el estado con `kubectl get endpoints -n pharmago pharmago-db`. Cuando termine la demostracion, presionar Enter en la terminal del script para restaurar el selector original.

Revisar Kibana buscando `pharma_biz=login_db_lookup_fail`, `pharma_biz=login_fail` y `pharma_biz=auth_db_lookup_fail`. En Grafana revisar `PharmaGo - Overview`, `PharmaGo - Endpoint Analysis`, `PharmaGo - Business` y `PharmaGo - SLIs SLOs`. Las alertas esperadas son `PharmaGo - Alta Tasa de Error > 1%` y `PharmaGo - Eventos de negocio fallidos > 5 en 5m`, siempre que haya suficientes requests durante la ventana. El script restaura el selector original al terminar; si se interrumpe y no restaura, ejecutar `kubectl patch service -n pharmago pharmago-db --type=merge -p '{"spec":{"selector":{"app":"pharmago-db"}}}'`.
