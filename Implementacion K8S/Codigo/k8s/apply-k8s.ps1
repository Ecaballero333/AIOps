# Script de despliegue para Kubernetes (Minikube)
# Uso: .\apply-k8s.ps1

$originalLocation = Get-Location
Set-Location $PSScriptRoot

Write-Host "=== Desplegando PharmaGo en Kubernetes ===" -ForegroundColor Green

function Wait-DeploymentAvailable {
    param(
        [Parameter(Mandatory = $true)][string]$DeploymentName,
        [Parameter(Mandatory = $true)][string]$Namespace,
        [Parameter(Mandatory = $true)][string]$ReadyMessage,
        [Parameter(Mandatory = $true)][string]$TimeoutMessage,
        [int]$MaxTimeoutSeconds = 300,
        [int]$PollSeconds = 5
    )

    $timeout = 0
    do {
        $desired = kubectl get deployment $DeploymentName -n $Namespace -o jsonpath='{.spec.replicas}' 2>$null
        $available = kubectl get deployment $DeploymentName -n $Namespace -o jsonpath='{.status.availableReplicas}' 2>$null
        $updated = kubectl get deployment $DeploymentName -n $Namespace -o jsonpath='{.status.updatedReplicas}' 2>$null

        if ([string]::IsNullOrEmpty($desired)) { $desired = "0" }
        if ([string]::IsNullOrEmpty($available)) { $available = "0" }
        if ([string]::IsNullOrEmpty($updated)) { $updated = "0" }

        if ([int]$desired -gt 0 -and [int]$available -ge [int]$desired) {
            Write-Host "   $ReadyMessage" -ForegroundColor Green
            return
        }

        Start-Sleep -Seconds $PollSeconds
        $timeout += $PollSeconds

        if ($timeout -ge $MaxTimeoutSeconds) {
            Write-Host "   $TimeoutMessage" -ForegroundColor Yellow
            return
        }

        Write-Host "   Esperando... disponibles $available/$desired, actualizadas $updated/$desired ($timeout/$MaxTimeoutSeconds segundos)" -ForegroundColor Cyan
    } while ($true)
}

# Verificar que kubectl está disponible
if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
    Write-Host "Error: kubectl no está instalado o no está en el PATH" -ForegroundColor Red
    exit 1
}

# Verificar que el cluster Kubernetes está accesible
$nodes = kubectl get nodes -o jsonpath='{.items[*].metadata.name}' 2>&1
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrEmpty($nodes)) {
    Write-Host "Error: No hay cluster Kubernetes accesible. Si usas Minikube: minikube start (o minikube start --nodes 3)" -ForegroundColor Red
    exit 1
}

# Etiquetar nodos (requerido para que los pods de telemetría se programen)
Write-Host "`n1. Etiquetando nodos..." -ForegroundColor Yellow
$nodeList = $nodes.Trim().Split(" ", [StringSplitOptions]::RemoveEmptyEntries)
if ($nodeList.Count -eq 1) {
    $singleNode = $nodeList[0]
    Write-Host "   Cluster de 1 nodo detectado. Etiquetando $singleNode con node-type=all" -ForegroundColor Cyan
    kubectl label nodes $singleNode node-type=all --overwrite 2>$null
} elseif ($nodeList.Count -ge 3) {
    Write-Host "   Cluster multi-nodo detectado. Etiquetando nodos..." -ForegroundColor Cyan
    $frontendNode = $nodeList[0]
    $backendNode = $nodeList[1]
    $opsNode = $nodeList[2]
    kubectl label nodes $frontendNode node-type=frontend --overwrite 2>$null
    kubectl label nodes $backendNode node-type=backend --overwrite 2>$null
    kubectl label nodes $opsNode node-type=ops --overwrite 2>$null
    if ($nodeList.Count -gt 3) {
        foreach ($node in $nodeList[3..($nodeList.Count - 1)]) {
            kubectl label nodes $node node-type=all --overwrite 2>$null
        }
    }
} else {
    $firstNode = $nodeList[0]
    Write-Host "   Etiquetando nodo $firstNode con node-type=all (compatibilidad)" -ForegroundColor Cyan
    kubectl label nodes $firstNode node-type=all --overwrite 2>$null
}
Write-Host "   Verificar: kubectl get nodes --show-labels" -ForegroundColor Gray

Write-Host "`n2. Creando namespace..." -ForegroundColor Yellow
kubectl apply -f namespace.yaml
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n3. Creando secrets..." -ForegroundColor Yellow
kubectl apply -f secrets\db-secret.yaml
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n4. Creando configmaps..." -ForegroundColor Yellow
kubectl apply -f configmaps\prometheus-config.yaml
kubectl apply -f configmaps\otel-collector-config.yaml
kubectl apply -f configmaps\grafana-provisioning.yaml
kubectl apply -f configmaps\grafana-dashboards.yaml
kubectl apply -f configmaps\grafana-dashboard-infra.yaml
kubectl apply -f configmaps\grafana-dashboard-business.yaml
kubectl apply -f configmaps\grafana-dashboard-endpoints.yaml
kubectl apply -f configmaps\grafana-dashboard-slo.yaml
kubectl apply -f configmaps\grafana-dashboard-storage.yaml
kubectl apply -f configmaps\grafana-alerts-business.yaml
kubectl apply -f configmaps\grafana-alerts-infra.yaml
kubectl apply -f configmaps\fluent-bit-config.yaml
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n5. Creando StorageClass y PersistentVolumes..." -ForegroundColor Yellow
kubectl apply -f persistent-volumes\storage-class.yaml
if ($LASTEXITCODE -ne 0) { exit 1 }

# Eliminar solo PVs huerfanos. No borrar PVs Bound porque puede colgar el despliegue
# o eliminar volumenes que todavia estan en uso.
Write-Host "   Limpiando PVs huerfanos (Released/Failed)..." -ForegroundColor Cyan
foreach ($pv in @("sql-pv", "elasticsearch-pv", "prometheus-pv", "grafana-pv")) {
    $status = kubectl get pv $pv -o jsonpath='{.status.phase}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        continue
    }

    if ($status -eq "Released" -or $status -eq "Failed") {
        kubectl delete pv $pv --ignore-not-found=true 2>$null
    } else {
        Write-Host "   PV $pv en estado $status; se conserva." -ForegroundColor Gray
    }
}
Start-Sleep -Seconds 2

kubectl apply -f persistent-volumes\sql-pv.yaml
kubectl apply -f persistent-volumes\elasticsearch-pv.yaml
kubectl apply -f persistent-volumes\prometheus-pv.yaml
kubectl apply -f persistent-volumes\grafana-pv.yaml
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n6. Desplegando base de datos..." -ForegroundColor Yellow
kubectl apply -f services\ops\db-service.yaml
kubectl apply -f deployments\ops\db-deployment.yaml
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n   Esperando a que la base de datos esté lista..." -ForegroundColor Yellow
# Esperar a que el pod esté Ready
Wait-DeploymentAvailable -DeploymentName "pharmago-db" -Namespace "pharmago" -ReadyMessage "Base de datos lista!" -TimeoutMessage "Timeout esperando la base de datos. Continuando..."

Write-Host "`n7. Desplegando servicios de observabilidad..." -ForegroundColor Yellow
# Elasticsearch primero
kubectl apply -f services\ops\elasticsearch-service.yaml
kubectl apply -f deployments\ops\elasticsearch-deployment.yaml

# Esperar a que Elasticsearch esté listo
Write-Host "   Esperando a que Elasticsearch esté listo..." -ForegroundColor Yellow
Wait-DeploymentAvailable -DeploymentName "elasticsearch" -Namespace "pharmago" -ReadyMessage "Elasticsearch listo!" -TimeoutMessage "Timeout esperando Elasticsearch. Continuando..."

# Resto de servicios ops
kubectl apply -f services\ops\otel-collector-service.yaml
kubectl apply -f deployments\ops\otel-collector-deployment.yaml

kubectl apply -f deployments\ops\prometheus-serviceaccount.yaml
kubectl apply -f deployments\ops\prometheus-clusterrole.yaml
kubectl apply -f deployments\ops\prometheus-clusterrolebinding.yaml
kubectl apply -f services\ops\prometheus-service.yaml
kubectl apply -f deployments\ops\prometheus-deployment.yaml
kubectl apply -f services\ops\node-exporter-service.yaml
kubectl apply -f deployments\ops\node-exporter-daemonset.yaml

kubectl apply -f deployments\ops\fluent-bit-serviceaccount.yaml
kubectl apply -f deployments\ops\fluent-bit-clusterrole.yaml
kubectl apply -f deployments\ops\fluent-bit-clusterrolebinding.yaml
kubectl apply -f deployments\ops\fluent-bit-daemonset.yaml

kubectl apply -f services\ops\grafana-service.yaml
kubectl apply -f deployments\ops\grafana-deployment.yaml

kubectl apply -f services\ops\kibana-service.yaml
kubectl apply -f deployments\ops\kibana-deployment.yaml

if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n8. Desplegando servicios backend..." -ForegroundColor Yellow
kubectl apply -f services\backend\users-service-service.yaml
kubectl apply -f deployments\backend\users-service-deployment.yaml

kubectl apply -f services\backend\pharmacy-service-service.yaml
kubectl apply -f deployments\backend\pharmacy-service-deployment.yaml

kubectl apply -f services\backend\api-gateway-service.yaml
kubectl apply -f deployments\backend\api-gateway-deployment.yaml

if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n9. Desplegando frontend..." -ForegroundColor Yellow
kubectl apply -f services\frontend\ui-service.yaml
kubectl apply -f deployments\frontend\ui-deployment.yaml

if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`n=== Despliegue completado ===" -ForegroundColor Green
Write-Host "`nVerificando estado de los pods..." -ForegroundColor Yellow
kubectl get pods -n pharmago -o wide

Write-Host "`nPara ver los servicios expuestos:" -ForegroundColor Cyan
Write-Host "  Frontend:     minikube service pharmago-ui -n pharmago --url" -ForegroundColor White
Write-Host "  Grafana:      minikube service grafana -n pharmago --url" -ForegroundColor White
Write-Host "  Kibana:       minikube service kibana -n pharmago --url" -ForegroundColor White
Write-Host "  Prometheus:   minikube service prometheus -n pharmago --url" -ForegroundColor White

Set-Location $originalLocation

