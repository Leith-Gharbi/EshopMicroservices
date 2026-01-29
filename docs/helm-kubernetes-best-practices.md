# Helm & Kubernetes Best Practices

Ce document résume les bonnes pratiques et solutions appliquées pour résoudre les problèmes rencontrés lors du déploiement de l'application EshopMicroservices sur Kubernetes avec Helm.

## Table des matières

1. [Gestion des releases Helm bloquées](#1-gestion-des-releases-helm-bloquées)
2. [Probes Kubernetes (Health Checks)](#2-probes-kubernetes-health-checks)
3. [Services gRPC dans Kubernetes](#3-services-grpc-dans-kubernetes)
4. [SQL Server en mode non-root](#4-sql-server-en-mode-non-root)
5. [Forcer le redémarrage des pods](#5-forcer-le-redémarrage-des-pods)
6. [Configuration des ressources](#6-configuration-des-ressources)
7. [Helm Templates - Bonnes pratiques](#7-helm-templates---bonnes-pratiques)
8. [Health Checks UI Configuration](#8-health-checks-ui-configuration)
9. [ASP.NET Core - Ordre des Middlewares](#9-aspnet-core---ordre-des-middlewares)

---

## 1. Gestion des releases Helm bloquées

### Problème
```
Error: UPGRADE FAILED: another operation (install/upgrade/rollback) is in progress
```

Une release Helm peut rester bloquée en état `pending-install`, `pending-upgrade` ou `pending-rollback` après un échec ou une interruption.

### Solution
Ajouter une vérification automatique avant chaque déploiement dans le pipeline CI/CD :

```yaml
# .gitlab-ci.yml
script:
  # Install jq for JSON parsing
  - apt-get update -qq && apt-get install -qq -y jq > /dev/null 2>&1 || true

  # Check and fix stuck Helm releases
  - |
    RELEASE_STATUS=$(helm status $RELEASE_NAME -n $NAMESPACE -o json 2>/dev/null | jq -r '.info.status' || echo "not-found")
    if [ "$RELEASE_STATUS" = "pending-install" ] || [ "$RELEASE_STATUS" = "pending-upgrade" ] || [ "$RELEASE_STATUS" = "pending-rollback" ]; then
      echo "Release is stuck in $RELEASE_STATUS state. Attempting rollback..."
      helm rollback $RELEASE_NAME -n $NAMESPACE || helm uninstall $RELEASE_NAME -n $NAMESPACE --no-hooks || true
    fi

  # Proceed with deployment
  - helm upgrade --install $RELEASE_NAME $CHART_PATH ...
```

### Commandes utiles
```bash
# Vérifier l'état d'une release
helm status <release-name> -n <namespace>

# Rollback manuel
helm rollback <release-name> -n <namespace>

# Supprimer une release bloquée
helm uninstall <release-name> -n <namespace> --no-hooks
```

---

## 2. Probes Kubernetes (Health Checks)

### Les trois types de probes

| Probe | Rôle | Conséquence si échec |
|-------|------|---------------------|
| **startupProbe** | Vérifie que l'app a démarré | Bloque liveness/readiness jusqu'au succès |
| **livenessProbe** | Vérifie que l'app est vivante | Redémarre le container |
| **readinessProbe** | Vérifie que l'app peut recevoir du trafic | Retire le pod du Service (plus de trafic) |

### Problème : Container tué pendant le démarrage
```
Liveness probe failed: HTTP probe failed with statuscode: 404
Back-off restarting failed container
```

L'application n'a pas eu le temps de démarrer avant que le livenessProbe ne la tue.

### Solution : Utiliser startupProbe

```yaml
# deployment.yaml
containers:
- name: my-app
  # startupProbe - Donne du temps au démarrage
  startupProbe:
    httpGet:
      path: /health
      port: http
    initialDelaySeconds: 10    # Attendre 10s avant le premier check
    periodSeconds: 5           # Vérifier toutes les 5s
    timeoutSeconds: 3          # Timeout de 3s par requête
    failureThreshold: 30       # 30 échecs = 30 * 5s = 2.5 min max de démarrage

  # livenessProbe - Après que startupProbe réussisse
  livenessProbe:
    httpGet:
      path: /health
      port: http
    periodSeconds: 10
    timeoutSeconds: 5
    failureThreshold: 3

  # readinessProbe - Contrôle le trafic
  readinessProbe:
    httpGet:
      path: /health
      port: http
    periodSeconds: 5
    timeoutSeconds: 3
    failureThreshold: 3
```

### Diagramme de fonctionnement

```
Démarrage du container
        │
        ▼
┌───────────────────┐
│   startupProbe    │ ◄── Vérifie toutes les 5s pendant max 2.5min
│   (en cours...)   │
└───────────────────┘
        │
        │ Succès après ~30s
        ▼
┌───────────────────┐     ┌───────────────────┐
│   livenessProbe   │     │  readinessProbe   │
│   (actif)         │     │  (actif)          │
└───────────────────┘     └───────────────────┘
```

### Bonnes pratiques

1. **Toujours utiliser startupProbe** pour les applications lentes à démarrer
2. **Ne pas mettre `initialDelaySeconds`** sur liveness/readiness si startupProbe est configuré
3. **Endpoint `/health` léger** - ne pas faire de vérifications lourdes
4. **Différencier les endpoints** si nécessaire :
   - `/health/live` pour livenessProbe (vérifie que l'app répond)
   - `/health/ready` pour readinessProbe (vérifie les dépendances)

---

## 3. Services gRPC dans Kubernetes

### Problème 1 : grpc_health_probe manquant
```
exec: "/bin/grpc_health_probe": stat /bin/grpc_health_probe: no such file or directory
```

Le binaire `grpc_health_probe` n'est pas inclus dans l'image Docker.

### Solution : Utiliser HTTP health checks

Si votre service gRPC ASP.NET Core expose aussi des endpoints HTTP :

```yaml
# deployment.yaml - Utiliser HTTP au lieu de grpc_health_probe
livenessProbe:
  httpGet:
    path: /health
    port: grpc
  periodSeconds: 10
readinessProbe:
  httpGet:
    path: /health
    port: grpc
  periodSeconds: 5
```

### Problème 2 : HTTP probe retourne 400
```
Startup probe failed: HTTP probe failed with statuscode: 400
```

Le service gRPC est configuré pour HTTP/2 uniquement, mais les probes Kubernetes utilisent HTTP/1.1.

### Solution : Configurer Kestrel pour HTTP/1.1 et HTTP/2

```json
// appsettings.json
{
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http1AndHttp2"  // Pas juste "Http2"
    }
  }
}
```

### Configuration ASP.NET Core pour gRPC avec Health Checks

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<MyService>();
app.MapHealthChecks("/health");  // Endpoint HTTP pour les probes

app.Run();
```

---

## 4. SQL Server en mode non-root

### Problème
```
sqlservr: Error: The system directory [/.system] could not be created.
File: LinuxDirectory.cpp:420 [Status: 0xC0000022 Access Denied]
```

SQL Server 2022 s'exécute par défaut avec l'utilisateur `mssql` (UID 10001), pas root.

### Solution : SecurityContext et InitContainer

```yaml
# statefulset.yaml
spec:
  template:
    spec:
      # Définir le groupe pour les volumes
      securityContext:
        fsGroup: 10001

      # InitContainer pour fixer les permissions
      initContainers:
      - name: fix-permissions
        image: busybox:latest
        command:
        - sh
        - -c
        - |
          chown -R 10001:10001 /var/opt/mssql
          chmod -R 755 /var/opt/mssql
        volumeMounts:
        - name: data
          mountPath: /var/opt/mssql
        securityContext:
          runAsUser: 0  # Root pour chown

      containers:
      - name: sqlserver
        image: mcr.microsoft.com/mssql/server:2022-latest
        securityContext:
          runAsUser: 10001
          runAsGroup: 10001
          runAsNonRoot: true
```

### Explication de fsGroup

```
┌─────────────────────────────────────────────────────────────┐
│                     Pod avec fsGroup: 10001                  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   Container SQL Server          Volume PVC                   │
│   ┌─────────────────┐          ┌─────────────────┐          │
│   │ runAsUser: 10001│          │ Propriétaire:   │          │
│   │                 │ ◄──────► │ groupe 10001    │          │
│   │ Peut lire/      │          │                 │          │
│   │ écrire le volume│          │ /var/opt/mssql  │          │
│   └─────────────────┘          └─────────────────┘          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### SQL Server 2022 - Chemin sqlcmd

```yaml
# Le chemin a changé dans SQL Server 2022
livenessProbe:
  exec:
    command:
    - /bin/sh
    - -c
    - /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $MSSQL_SA_PASSWORD -Q "SELECT 1" -C
```

| Version | Chemin sqlcmd |
|---------|---------------|
| SQL Server 2019 | `/opt/mssql-tools/bin/sqlcmd` |
| SQL Server 2022 | `/opt/mssql-tools18/bin/sqlcmd` |

---

## 5. Forcer le redémarrage des pods

### Problème
Helm ne redémarre pas les pods si seul le tag d'image change (avec `imagePullPolicy: Always`).

### Solution : Annotation deploymentTimestamp

```yaml
# deployment.yaml
spec:
  template:
    metadata:
      annotations:
        # Change à chaque déploiement = force le rolling update
        deploymentTimestamp: {{ .Values.global.deploymentTimestamp | default "0" | quote }}
```

```yaml
# .gitlab-ci.yml
helm upgrade --install $RELEASE \
  --set global.deploymentTimestamp=$(date +%s) \
  ...
```

### Comment ça marche

```
Déploiement 1                    Déploiement 2
─────────────                    ─────────────
annotations:                     annotations:
  deploymentTimestamp: "1706000" → deploymentTimestamp: "1706100"
                                          │
                                          ▼
                                 Helm détecte un changement
                                          │
                                          ▼
                                 Rolling update déclenché
```

---

## 6. Configuration des ressources

### Kibana - Mémoire insuffisante

```
OOMKilled - Container killed due to memory limit
```

Kibana 8.x nécessite au minimum 1Gi de mémoire.

```yaml
# values.yaml
kibana:
  resources:
    requests:
      cpu: 250m
      memory: 1Gi    # Minimum pour Kibana 8.x
    limits:
      cpu: 1000m
      memory: 2Gi    # Recommandé
```

### Recommandations par service

| Service | Memory Request | Memory Limit | Notes |
|---------|---------------|--------------|-------|
| API (.NET) | 128-256Mi | 256-512Mi | Dépend de la charge |
| Elasticsearch | 1Gi | 2Gi | JVM heap = 50% de la limite |
| Kibana | 1Gi | 2Gi | Chargement de plugins |
| SQL Server | 1Gi | 2-4Gi | Dépend de la taille des DBs |
| Redis | 64-128Mi | 256Mi | Dépend du cache |

---

## 7. Helm Templates - Bonnes pratiques

### Problème : Tags d'image numériques

```
couldn't parse image name "registry/app:%!s(int64=29132002)"
```

Si le commit SHA ne contient que des chiffres, Helm l'interprète comme un nombre.

### Solution : Forcer la conversion en string

```go
{{/* _helpers.tpl */}}
{{- define "eshop.image" -}}
{{- $tag := .Values.global.imageTag | toString -}}  {{/* Toujours convertir en string */}}
{{- printf "%s/%s:%s" .Values.global.imageRegistry .repository $tag -}}
{{- end }}
```

### Bonnes pratiques pour les helpers

```go
{{/* Toujours utiliser | toString pour les valeurs qui pourraient être numériques */}}
{{- $tag := $imageConfig.tag | default $context.Values.global.imageTag | toString -}}

{{/* Utiliser | int pour les ports et valeurs numériques */}}
{{- printf "Server=%s:%d" .host (.port | int) -}}

{{/* Utiliser | quote pour les valeurs dans les manifests YAML */}}
value: {{ .Values.myValue | quote }}
```

---

## 8. Health Checks UI Configuration

### Problème : URLs localhost dans Kubernetes

```
Connection refused (localhost:7238)
```

Les URLs `localhost` dans `appsettings.json` ne fonctionnent pas dans Kubernetes.

### Solution : Override via variables d'environnement

```yaml
# deployment.yaml
env:
# Override les URLs de appsettings.json pour Kubernetes
- name: HealthChecksUI__HealthChecks__0__Name
  value: "Catalog API"
- name: HealthChecksUI__HealthChecks__0__Uri
  value: "http://{{ include "eshop.fullname" . }}-catalog-api/health"
- name: HealthChecksUI__HealthChecks__1__Name
  value: "Basket API"
- name: HealthChecksUI__HealthChecks__1__Uri
  value: "http://{{ include "eshop.fullname" . }}-basket-api/health"
# ... etc
```

### Convention de nommage des services Kubernetes

```
Format: <release-name>-<chart-name>-<component>

Exemple:
  Release: eshop-dev
  Chart: eshop-microservices
  Component: catalog-api

  Service Name: eshop-dev-eshop-microservices-catalog-api
  URL interne: http://eshop-dev-eshop-microservices-catalog-api/health
```

---

## 9. ASP.NET Core - Ordre des Middlewares

### Problème : Health Check retourne 404

```
Startup probe failed: HTTP probe failed with statuscode: 404
```

L'application démarre mais l'endpoint `/health` retourne 404. Pourtant les logs montrent "Application started".

### Cause

L'ordre des middlewares est crucial dans ASP.NET Core. Si `MapCarter()` ou d'autres handlers de routes sont enregistrés **avant** `UseHealthChecks()`, ils peuvent intercepter les requêtes avant que le middleware de health check ne les traite.

### Mauvais ordre (404)

```csharp
var app = builder.Build();

app.MapCarter();  // ❌ Carter intercepte /health
app.UseHealthChecks("/health", ...);  // Jamais atteint!

app.Run();
```

### Bon ordre (200 OK)

```csharp
var app = builder.Build();

// ✅ Health checks EN PREMIER
app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapCarter();  // Ensuite les autres routes

app.Run();
```

### Ordre recommandé des middlewares

```csharp
var app = builder.Build();

// 1. Logging/Telemetry (capture tout)
app.UseElasticsearchHttpLogging();

// 2. Health Checks (répond rapidement, avant tout le reste)
app.UseHealthChecks("/health", options);

// 3. Exception Handler
app.UseExceptionHandler(opt => { });

// 4. Routes de l'application
app.MapCarter();
// ou app.MapControllers();

app.Run();
```

### Pourquoi c'est important pour Kubernetes

```
┌─────────────────────────────────────────────────────────────┐
│                    Kubernetes Probe                          │
│                         │                                    │
│                         ▼                                    │
│              GET /health HTTP/1.1                           │
│                         │                                    │
├─────────────────────────┼───────────────────────────────────┤
│     MAUVAIS ORDRE       │        BON ORDRE                  │
│                         │                                    │
│  app.MapCarter()        │   app.UseHealthChecks()           │
│         │               │          │                         │
│         ▼               │          ▼                         │
│  "Route not found"      │   "200 OK - Healthy"              │
│         │               │                                    │
│         ▼               │                                    │
│  app.UseHealthChecks()  │   app.MapCarter()                 │
│  (jamais appelé)        │   (routes normales)               │
│                         │                                    │
│  RÉSULTAT: 404          │   RÉSULTAT: 200 ✅                │
└─────────────────────────────────────────────────────────────┘
```

---

## Checklist de déploiement

Avant chaque déploiement, vérifier :

- [ ] **Probes configurés** - startupProbe, livenessProbe, readinessProbe
- [ ] **Resources définies** - requests et limits pour CPU/mémoire
- [ ] **SecurityContext** - runAsNonRoot si possible
- [ ] **Variables d'environnement** - URLs internes Kubernetes (pas localhost)
- [ ] **PVC permissions** - fsGroup si volumes persistants
- [ ] **Image tags** - Utiliser | toString dans les templates
- [ ] **deploymentTimestamp** - Pour forcer les rolling updates
- [ ] **Ordre des middlewares** - UseHealthChecks AVANT MapCarter/MapControllers

---

## Commandes de diagnostic utiles

```bash
# État des pods
kubectl get pods -n <namespace>

# Logs d'un pod (container actuel)
kubectl logs -n <namespace> <pod-name>

# Logs d'un pod (container précédent après crash)
kubectl logs -n <namespace> <pod-name> --previous

# Détails d'un pod (events, état des probes)
kubectl describe pod -n <namespace> <pod-name>

# État d'une release Helm
helm status <release-name> -n <namespace>

# Historique des releases
helm history <release-name> -n <namespace>

# Valeurs utilisées par une release
helm get values <release-name> -n <namespace>

# Manifests générés (debug)
helm template <release-name> <chart-path> --values values.yaml
```

---

## Références

- [Kubernetes Probes Documentation](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Helm Best Practices](https://helm.sh/docs/chart_best_practices/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [gRPC Health Checking](https://github.com/grpc/grpc/blob/master/doc/health-checking.md)
- [SQL Server on Linux](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-overview)
