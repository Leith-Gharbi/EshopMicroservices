# Guide des Ressources Kubernetes - EshopMicroservices

Ce document explique chaque ressource Kubernetes utilisée dans le cluster EshopMicroservices, son rôle et ses avantages.

---

## Table des Matières

1. [Namespace](#1-namespace)
2. [Pod](#2-pod)
3. [Deployment](#3-deployment)
4. [Service](#4-service)
5. [Ingress](#5-ingress)
6. [ConfigMap](#6-configmap)
7. [Secret](#7-secret)
8. [ImagePullSecret](#8-imagepullsecret)
9. [ServiceAccount](#9-serviceaccount)
10. [PersistentVolumeClaim (PVC)](#10-persistentvolumeclaim-pvc)
11. [StatefulSet](#11-statefulset)
12. [Horizontal Pod Autoscaler (HPA)](#12-horizontal-pod-autoscaler-hpa)
13. [PodDisruptionBudget (PDB)](#13-poddisruptionbudget-pdb)
14. [NetworkPolicy](#14-networkpolicy)
15. [ResourceQuota](#15-resourcequota)
16. [LimitRange](#16-limitrange)

---

## 1. Namespace

### Description
Un **Namespace** est un espace de noms virtuel qui permet d'isoler les ressources dans un cluster Kubernetes.

### Schéma
```
┌─────────────────────────────────────────────────────────────┐
│                    CLUSTER KUBERNETES                        │
│  ┌─────────────────┐ ┌─────────────────┐ ┌───────────────┐  │
│  │   eshop-dev     │ │  eshop-staging  │ │  eshop-prod   │  │
│  │  (Namespace)    │ │   (Namespace)   │ │  (Namespace)  │  │
│  │                 │ │                 │ │               │  │
│  │ - catalog-api   │ │ - catalog-api   │ │ - catalog-api │  │
│  │ - basket-api    │ │ - basket-api    │ │ - basket-api  │  │
│  │ - postgres      │ │ - postgres      │ │ - postgres    │  │
│  └─────────────────┘ └─────────────────┘ └───────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# values-dev.yaml
global:
  namespace: eshop-dev

# values-staging.yaml
global:
  namespace: eshop-staging

# values-prod.yaml
global:
  namespace: eshop-prod
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Isolation** | Sépare les environnements (dev/staging/prod) |
| **Sécurité** | Limite l'accès aux ressources par équipe |
| **Organisation** | Regroupe logiquement les ressources |
| **Quotas** | Permet de limiter les ressources par namespace |
| **Nommage** | Évite les conflits de noms entre environnements |

---

## 2. Pod

### Description
Un **Pod** est la plus petite unité déployable dans Kubernetes. Il contient un ou plusieurs conteneurs qui partagent le même réseau et stockage.

### Schéma
```
┌─────────────────────────────────────────┐
│                   POD                    │
│  ┌─────────────┐    ┌─────────────────┐ │
│  │  Container  │    │    Container    │ │
│  │ catalog-api │    │  sidecar-logs   │ │
│  │   :8080     │    │     :9090       │ │
│  └─────────────┘    └─────────────────┘ │
│                                         │
│  Shared: Network (localhost)            │
│          Storage (volumes)              │
│          IP Address: 10.244.0.15        │
└─────────────────────────────────────────┘
```

### Cycle de vie d'un Pod
```
Pending ──► Running ──► Succeeded
                │
                └──► Failed
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Colocation** | Conteneurs liés s'exécutent ensemble |
| **Réseau partagé** | Communication via localhost |
| **Stockage partagé** | Volumes accessibles par tous les conteneurs |
| **Scheduling** | Placés ensemble sur le même nœud |

---

## 3. Deployment

### Description
Un **Deployment** gère le déploiement et la mise à jour des Pods de manière déclarative. Il assure que le nombre souhaité de réplicas est toujours en cours d'exécution.

### Schéma
```
┌─────────────────────────────────────────────────────────────┐
│                      DEPLOYMENT                              │
│                    catalog-api                               │
│                   replicas: 3                                │
│                                                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │ ReplicaSet  │  │ ReplicaSet  │  │ ReplicaSet  │         │
│  │   (v1)      │  │   (v2)      │  │   (v3)      │         │
│  │  outdated   │  │  previous   │  │  current    │         │
│  └─────────────┘  └─────────────┘  └──────┬──────┘         │
│                                           │                 │
│                          ┌────────────────┼────────────────┐│
│                          ▼                ▼                ▼││
│                     ┌────────┐       ┌────────┐       ┌────────┐
│                     │  Pod   │       │  Pod   │       │  Pod   │
│                     │  (1)   │       │  (2)   │       │  (3)   │
│                     └────────┘       └────────┘       └────────┘
└─────────────────────────────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/catalog-api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ .Release.Name }}-catalog-api
spec:
  replicas: {{ .Values.replicaCount.catalogApi }}
  selector:
    matchLabels:
      app.kubernetes.io/component: catalog-api
  template:
    spec:
      containers:
        - name: catalog-api
          image: {{ .Values.global.imageRegistry }}/catalog-api:{{ .Values.global.imageTag }}
          ports:
            - containerPort: 8080
```

### Stratégies de déploiement
```
RollingUpdate (par défaut):
┌─────┐ ┌─────┐ ┌─────┐        ┌─────┐ ┌─────┐ ┌─────┐
│ v1  │ │ v1  │ │ v1  │   ──►  │ v2  │ │ v2  │ │ v2  │
└─────┘ └─────┘ └─────┘        └─────┘ └─────┘ └─────┘
   ▲        │       │             │        │       ▲
   │        │       │             │        │       │
   └────────┴───────┴─────────────┴────────┴───────┘
              Mise à jour progressive

Recreate:
┌─────┐ ┌─────┐ ┌─────┐        ┌─────┐ ┌─────┐ ┌─────┐
│ v1  │ │ v1  │ │ v1  │   ──►  │     │ │     │ │     │  ──►  │ v2  │ │ v2  │ │ v2  │
└─────┘ └─────┘ └─────┘        └─────┘ └─────┘ └─────┘       └─────┘ └─────┘ └─────┘
              Suppression totale puis recréation
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Rolling Updates** | Mise à jour sans interruption |
| **Rollback** | Retour à une version précédente |
| **Scaling** | Ajustement du nombre de réplicas |
| **Self-healing** | Recréation automatique des Pods défaillants |
| **Declarative** | État souhaité décrit dans le YAML |

---

## 4. Service

### Description
Un **Service** expose un ensemble de Pods comme un service réseau avec une IP stable et un nom DNS.

### Types de Services
```
┌─────────────────────────────────────────────────────────────────┐
│                        TYPES DE SERVICES                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ClusterIP (par défaut)         NodePort                        │
│  ┌─────────────────────┐        ┌─────────────────────┐        │
│  │ IP interne: 10.0.0.5│        │ IP: 10.0.0.5        │        │
│  │ Port: 80            │        │ NodePort: 30080     │        │
│  │ Accessible: Cluster │        │ Accessible: Externe │        │
│  └─────────────────────┘        └─────────────────────┘        │
│                                                                  │
│  LoadBalancer                   ExternalName                    │
│  ┌─────────────────────┐        ┌─────────────────────┐        │
│  │ IP externe: 169.x.x │        │ CNAME: external.com │        │
│  │ Cloud Provider      │        │ Pas de proxy        │        │
│  │ Accessible: Internet│        │ Redirection DNS     │        │
│  └─────────────────────┘        └─────────────────────┘        │
└─────────────────────────────────────────────────────────────────┘
```

### Schéma ClusterIP (utilisé dans le projet)
```
                    ┌──────────────────────────────┐
                    │      Service (ClusterIP)      │
                    │   catalog-api-service         │
                    │   IP: 10.96.45.123            │
                    │   Port: 80                    │
                    └──────────────┬───────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              │                    │                    │
              ▼                    ▼                    ▼
        ┌──────────┐         ┌──────────┐         ┌──────────┐
        │   Pod    │         │   Pod    │         │   Pod    │
        │ :8080    │         │ :8080    │         │ :8080    │
        │10.244.0.5│         │10.244.0.6│         │10.244.0.7│
        └──────────┘         └──────────┘         └──────────┘
```

### Utilisation dans le projet
```yaml
# templates/catalog-api-service.yaml
apiVersion: v1
kind: Service
metadata:
  name: {{ .Release.Name }}-catalog-api
spec:
  type: ClusterIP
  selector:
    app.kubernetes.io/component: catalog-api
  ports:
    - port: 80           # Port du service
      targetPort: 8080   # Port du conteneur
      protocol: TCP
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **IP Stable** | L'IP du service ne change pas même si les Pods changent |
| **DNS** | Résolution automatique (catalog-api.eshop-dev.svc.cluster.local) |
| **Load Balancing** | Distribution du trafic entre les Pods |
| **Service Discovery** | Les Pods se trouvent via le nom du service |
| **Abstraction** | Découple les clients des Pods |

---

## 5. Ingress

### Description
Un **Ingress** gère l'accès externe aux services dans le cluster, typiquement HTTP/HTTPS. Il fournit le routage basé sur les URL, la terminaison SSL et l'hébergement virtuel basé sur les noms.

### Schéma complet
```
                              INTERNET
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                     INGRESS CONTROLLER                           │
│              (nginx - IBM Cloud ALB)                             │
│         lgharbi-eshop-cluster-k8s-...appdomain.cloud            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    INGRESS RULES                         │    │
│  ├──────────────────┬────────────────┬─────────────────────┤    │
│  │     Host         │     Path       │      Backend        │    │
│  ├──────────────────┼────────────────┼─────────────────────┤    │
│  │ eshop-dev.*      │ /              │ shopping-web:80     │    │
│  │ api-dev.*        │ /              │ yarp-gateway:80     │    │
│  │ api-dev.*        │ /catalog       │ catalog-api:80      │    │
│  │ health-dev.*     │ /              │ healthdeck-web:80   │    │
│  │ kibana-dev.*     │ /              │ kibana:5601         │    │
│  └──────────────────┴────────────────┴─────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                                  │
         ┌────────────────────────┼────────────────────────┐
         ▼                        ▼                        ▼
   ┌───────────┐           ┌───────────┐           ┌───────────┐
   │  Service  │           │  Service  │           │  Service  │
   │shopping-web           │yarp-gateway           │  kibana   │
   └─────┬─────┘           └─────┬─────┘           └─────┬─────┘
         ▼                       ▼                       ▼
   ┌───────────┐           ┌───────────┐           ┌───────────┐
   │   Pods    │           │   Pods    │           │   Pods    │
   └───────────┘           └───────────┘           └───────────┘
```

### Utilisation dans le projet
```yaml
# templates/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: {{ .Release.Name }}-ingress
  annotations:
    kubernetes.io/ingress.class: "public-iks-k8s-nginx"
spec:
  rules:
    {{- range .Values.ingress.hosts }}
    - host: {{ .host }}
      http:
        paths:
          {{- range .paths }}
          - path: {{ .path }}
            pathType: {{ .pathType }}
            backend:
              service:
                name: {{ $.Release.Name }}-{{ .service }}
                port:
                  number: {{ .port }}
          {{- end }}
    {{- end }}
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Point d'entrée unique** | Un seul LoadBalancer pour tous les services |
| **Routage intelligent** | Basé sur hostname et path |
| **TLS/SSL** | Terminaison SSL centralisée |
| **Économie** | Un LoadBalancer au lieu de plusieurs |
| **Annotations** | Configuration avancée (rate limiting, redirects, etc.) |

---

## 6. ConfigMap

### Description
Un **ConfigMap** stocke des données de configuration non-confidentielles sous forme de paires clé-valeur. Il permet de découpler la configuration de l'image du conteneur.

### Schéma
```
┌─────────────────────────────────────────────────────────────┐
│                       CONFIGMAP                              │
│                   catalog-api-config                         │
├─────────────────────────────────────────────────────────────┤
│  data:                                                       │
│    ConnectionStrings__Database: "Host=postgres;DB=catalog"  │
│    ASPNETCORE_ENVIRONMENT: "Production"                     │
│    Logging__LogLevel__Default: "Information"                │
│                                                              │
│  OU fichier complet:                                        │
│    appsettings.json: |                                      │
│      {                                                      │
│        "Logging": { "LogLevel": "Info" }                    │
│      }                                                      │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│   Injection comme       │     │   Injection comme       │
│   Variables d'env       │     │   Volume/Fichier        │
│                         │     │                         │
│   env:                  │     │   volumeMounts:         │
│   - name: DB_HOST       │     │   - name: config        │
│     valueFrom:          │     │     mountPath: /config  │
│       configMapKeyRef:  │     │                         │
│         name: config    │     │   volumes:              │
│         key: DB_HOST    │     │   - name: config        │
└─────────────────────────┘     │     configMap:          │
                                │       name: config      │
                                └─────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/catalog-api-configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ .Release.Name }}-catalog-api-config
data:
  ConnectionStrings__Database: "Host={{ .Release.Name }}-postgresql-catalogdb;Database=CatalogDb;Username=postgres;Password=postgres"
  ASPNETCORE_ENVIRONMENT: "{{ .Values.global.environment }}"
  ASPNETCORE_URLS: "http://+:8080"
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Séparation** | Configuration séparée du code |
| **Réutilisabilité** | Même image, différentes configs |
| **Mise à jour** | Changement sans rebuild d'image |
| **Environnements** | Config différente par environnement |
| **Centralisation** | Gestion centralisée de la configuration |

---

## 7. Secret

### Description
Un **Secret** stocke des données sensibles comme des mots de passe, tokens ou clés. Les données sont encodées en base64 et peuvent être chiffrées au repos.

### Schéma
```
┌─────────────────────────────────────────────────────────────┐
│                         SECRET                               │
│                    database-credentials                      │
├─────────────────────────────────────────────────────────────┤
│  type: Opaque                                                │
│                                                              │
│  data:                                                       │
│    username: cG9zdGdyZXM=        # postgres (base64)        │
│    password: c2VjcmV0MTIz        # secret123 (base64)       │
│    connection-string: SG9zdD1...  # (base64)                │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│   Variable d'env        │     │   Fichier monté         │
│                         │     │                         │
│   env:                  │     │   volumeMounts:         │
│   - name: DB_PASSWORD   │     │   - name: creds         │
│     valueFrom:          │     │     mountPath: /secrets │
│       secretKeyRef:     │     │     readOnly: true      │
│         name: db-creds  │     │                         │
│         key: password   │     │   # Fichier créé:       │
│                         │     │   # /secrets/password   │
└─────────────────────────┘     └─────────────────────────┘
```

### Types de Secrets
```
┌────────────────────────────────────────────────────────────────┐
│                      TYPES DE SECRETS                           │
├─────────────────────┬──────────────────────────────────────────┤
│ Opaque              │ Données arbitraires (défaut)             │
│ kubernetes.io/tls   │ Certificats TLS (tls.crt, tls.key)       │
│ kubernetes.io/      │ Credentials Docker Registry              │
│ dockerconfigjson    │                                          │
│ kubernetes.io/      │ Token ServiceAccount                     │
│ service-account-    │                                          │
│ token               │                                          │
└─────────────────────┴──────────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/postgresql-secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: {{ .Release.Name }}-postgresql-credentials
type: Opaque
stringData:  # Kubernetes encode automatiquement en base64
  POSTGRES_USER: "postgres"
  POSTGRES_PASSWORD: "{{ .Values.postgresql.password }}"
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Sécurité** | Données sensibles séparées du code |
| **Encodage** | Base64 par défaut, chiffrement possible |
| **RBAC** | Contrôle d'accès granulaire |
| **Rotation** | Mise à jour sans rebuild |
| **Audit** | Traçabilité des accès |

---

## 8. ImagePullSecret

### Description
Un **ImagePullSecret** est un Secret spécial contenant les credentials pour accéder à un registre de conteneurs privé (comme IBM Container Registry).

### Schéma
```
┌─────────────────────────────────────────────────────────────┐
│                    IMAGE PULL SECRET                         │
│                       icr-secret                             │
├─────────────────────────────────────────────────────────────┤
│  type: kubernetes.io/dockerconfigjson                        │
│                                                              │
│  data:                                                       │
│    .dockerconfigjson: {                                     │
│      "auths": {                                             │
│        "de.icr.io": {                                       │
│          "username": "iamapikey",                           │
│          "password": "xxx-api-key-xxx",                     │
│          "auth": "base64(user:pass)"                        │
│        }                                                    │
│      }                                                      │
│    }                                                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        POD SPEC                              │
├─────────────────────────────────────────────────────────────┤
│  spec:                                                       │
│    imagePullSecrets:                                        │
│      - name: icr-secret     ◄── Référence au secret         │
│    containers:                                               │
│      - name: catalog-api                                    │
│        image: de.icr.io/eshop-images/catalog-api:v1         │
│               ▲                                              │
│               │                                              │
│               └── Registre privé IBM                        │
└─────────────────────────────────────────────────────────────┘
```

### Création du secret
```bash
# Création manuelle
kubectl create secret docker-registry icr-secret \
  --docker-server=de.icr.io \
  --docker-username=iamapikey \
  --docker-password=$IBM_CLOUD_API_KEY \
  --docker-email=your@email.com \
  -n eshop-dev

# Ou via GitLab CI/CD (dans .gitlab-ci.yml)
kubectl create secret docker-registry icr-secret \
  --docker-server=$ICR_REGISTRY \
  --docker-username=iamapikey \
  --docker-password=$IBM_CLOUD_API_KEY \
  -n $NAMESPACE --dry-run=client -o yaml | kubectl apply -f -
```

### Utilisation dans le projet
```yaml
# templates/catalog-api-deployment.yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: icr-secret
      containers:
        - name: catalog-api
          image: de.icr.io/eshop-images/catalog-api:{{ .Values.global.imageTag }}
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Registres privés** | Accès aux images non-publiques |
| **Sécurité** | Credentials non exposés dans les manifests |
| **Multi-registres** | Support de plusieurs registres |
| **Automatisation** | Création via CI/CD |

---

## 9. ServiceAccount

### Description
Un **ServiceAccount** fournit une identité aux Pods pour interagir avec l'API Kubernetes. Il détermine ce que le Pod peut faire dans le cluster.

### Schéma
```
┌─────────────────────────────────────────────────────────────┐
│                    SERVICE ACCOUNT                           │
│                   catalog-api-sa                             │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                     TOKEN                            │    │
│  │  (JWT auto-généré et monté dans le Pod)             │    │
│  │  /var/run/secrets/kubernetes.io/serviceaccount/     │    │
│  │    - token                                          │    │
│  │    - ca.crt                                         │    │
│  │    - namespace                                      │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                ImagePullSecrets                      │    │
│  │  - icr-secret (hérité par les Pods)                 │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                         RBAC                                 │
│                                                              │
│  Role/ClusterRole ◄──── RoleBinding ────► ServiceAccount    │
│                                                              │
│  Exemple:                                                    │
│  - Lire les ConfigMaps                                      │
│  - Lister les Pods                                          │
│  - Accéder aux Secrets spécifiques                          │
└─────────────────────────────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/serviceaccount.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: {{ .Release.Name }}-service-account
imagePullSecrets:
  - name: icr-secret   # Tous les Pods utilisant ce SA auront accès au registre

---
# templates/catalog-api-deployment.yaml
spec:
  template:
    spec:
      serviceAccountName: {{ .Release.Name }}-service-account
      containers:
        - name: catalog-api
          image: de.icr.io/eshop-images/catalog-api:v1
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Identité** | Chaque Pod a une identité propre |
| **RBAC** | Contrôle fin des permissions |
| **ImagePullSecrets** | Héritage automatique des credentials |
| **Audit** | Traçabilité des actions |
| **Principe du moindre privilège** | Permissions minimales nécessaires |

---

## 10. PersistentVolumeClaim (PVC)

### Description
Un **PersistentVolumeClaim (PVC)** est une demande de stockage persistant. Il abstrait les détails du stockage sous-jacent et garantit que les données survivent aux redémarrages des Pods.

### Schéma
```
┌─────────────────────────────────────────────────────────────────┐
│                    STORAGE HIERARCHY                             │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              StorageClass (IBM Cloud)                    │    │
│  │         ibmc-vpc-block-10iops-tier                       │    │
│  │  - Provisioner: vpc.block.csi.ibm.io                    │    │
│  │  - IOPS: 10 per GB                                      │    │
│  │  - Reclaim: Delete                                      │    │
│  └─────────────────────────────────────────────────────────┘    │
│                              │                                   │
│                              ▼ (Provisionne dynamiquement)      │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              PersistentVolume (PV)                       │    │
│  │         pvc-abc123-def456-...                           │    │
│  │  - Capacity: 10Gi                                       │    │
│  │  - AccessModes: ReadWriteOnce                           │    │
│  │  - IBM Block Storage Volume ID                          │    │
│  └─────────────────────────────────────────────────────────┘    │
│                              ▲                                   │
│                              │ (Bound)                          │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │         PersistentVolumeClaim (PVC)                      │    │
│  │              postgresql-data                             │    │
│  │  - Request: 10Gi                                        │    │
│  │  - StorageClass: ibmc-vpc-block-10iops-tier            │    │
│  │  - AccessModes: ReadWriteOnce                           │    │
│  └─────────────────────────────────────────────────────────┘    │
│                              │                                   │
│                              ▼                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                        POD                               │    │
│  │  volumeMounts:                                          │    │
│  │    - name: data                                         │    │
│  │      mountPath: /var/lib/postgresql/data               │    │
│  │  volumes:                                               │    │
│  │    - name: data                                         │    │
│  │      persistentVolumeClaim:                             │    │
│  │        claimName: postgresql-data                       │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### Access Modes
```
┌─────────────────────────────────────────────────────────────┐
│                     ACCESS MODES                             │
├─────────────────────┬───────────────────────────────────────┤
│ ReadWriteOnce (RWO) │ Un seul nœud peut monter en R/W       │
│                     │ → Utilisé pour PostgreSQL, SQL Server │
├─────────────────────┼───────────────────────────────────────┤
│ ReadOnlyMany (ROX)  │ Plusieurs nœuds peuvent lire          │
│                     │ → Fichiers de config partagés         │
├─────────────────────┼───────────────────────────────────────┤
│ ReadWriteMany (RWX) │ Plusieurs nœuds peuvent R/W           │
│                     │ → NFS, stockage partagé               │
└─────────────────────┴───────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/postgresql-statefulset.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: {{ .Release.Name }}-postgresql-catalogdb
spec:
  volumeClaimTemplates:
    - metadata:
        name: data
      spec:
        accessModes: ["ReadWriteOnce"]
        storageClassName: {{ .Values.postgresql.catalogDb.storageClass }}
        resources:
          requests:
            storage: {{ .Values.postgresql.catalogDb.storageSize }}
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Persistance** | Données survivent aux redémarrages |
| **Abstraction** | Indépendant du provider de stockage |
| **Provisionnement dynamique** | Création automatique des volumes |
| **Portabilité** | Même manifest, différents clouds |
| **Backup** | Snapshots possibles via StorageClass |

---

## 11. StatefulSet

### Description
Un **StatefulSet** gère le déploiement de Pods avec état (stateful). Contrairement au Deployment, il garantit l'ordre, l'unicité et la stabilité des identités des Pods.

### Comparaison Deployment vs StatefulSet
```
┌─────────────────────────────────────────────────────────────────┐
│                    DEPLOYMENT (Stateless)                        │
├─────────────────────────────────────────────────────────────────┤
│  Pods: catalog-api-7d9b4-abc12, catalog-api-7d9b4-xyz89         │
│        (noms aléatoires)                                         │
│                                                                  │
│  Stockage: Partagé ou éphémère                                  │
│  Ordre de démarrage: Parallèle (tous en même temps)             │
│  Réseau: IP aléatoire à chaque redémarrage                      │
│                                                                  │
│  Utilisé pour: APIs, frontends, workers sans état               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    STATEFULSET (Stateful)                        │
├─────────────────────────────────────────────────────────────────┤
│  Pods: postgresql-0, postgresql-1, postgresql-2                  │
│        (noms ordonnés et stables)                               │
│                                                                  │
│  Stockage: PVC dédié par Pod (postgresql-0 → pvc-postgresql-0)  │
│  Ordre de démarrage: Séquentiel (0, puis 1, puis 2)             │
│  Réseau: DNS stable (postgresql-0.postgresql.eshop-dev.svc)     │
│                                                                  │
│  Utilisé pour: Bases de données, caches, message brokers        │
└─────────────────────────────────────────────────────────────────┘
```

### Schéma StatefulSet
```
┌─────────────────────────────────────────────────────────────────┐
│                      STATEFULSET                                 │
│                     postgresql                                   │
│                    replicas: 3                                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐        │
│  │ postgresql-0 │   │ postgresql-1 │   │ postgresql-2 │        │
│  │   (master)   │   │   (replica)  │   │   (replica)  │        │
│  └──────┬───────┘   └──────┬───────┘   └──────┬───────┘        │
│         │                  │                  │                 │
│         ▼                  ▼                  ▼                 │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐        │
│  │   PVC-0      │   │   PVC-1      │   │   PVC-2      │        │
│  │   10Gi       │   │   10Gi       │   │   10Gi       │        │
│  └──────────────┘   └──────────────┘   └──────────────┘        │
│                                                                  │
│  DNS Entries (Headless Service):                                │
│  - postgresql-0.postgresql.eshop-dev.svc.cluster.local         │
│  - postgresql-1.postgresql.eshop-dev.svc.cluster.local         │
│  - postgresql-2.postgresql.eshop-dev.svc.cluster.local         │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/postgresql-statefulset.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: {{ .Release.Name }}-postgresql-catalogdb
spec:
  serviceName: {{ .Release.Name }}-postgresql-catalogdb  # Headless service
  replicas: 1
  selector:
    matchLabels:
      app.kubernetes.io/component: postgresql-catalogdb
  template:
    spec:
      containers:
        - name: postgresql
          image: postgres:15
          volumeMounts:
            - name: data
              mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
    - metadata:
        name: data
      spec:
        accessModes: ["ReadWriteOnce"]
        resources:
          requests:
            storage: 10Gi
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Identité stable** | Noms prévisibles (pod-0, pod-1, ...) |
| **Stockage dédié** | Chaque Pod a son propre PVC |
| **Ordre garanti** | Démarrage/arrêt séquentiel |
| **DNS stable** | Chaque Pod a un DNS unique |
| **Scaling ordonné** | Scale up/down dans l'ordre |

---

## 12. Horizontal Pod Autoscaler (HPA)

### Description
Un **HPA** ajuste automatiquement le nombre de réplicas d'un Deployment/StatefulSet basé sur l'utilisation CPU/mémoire ou des métriques personnalisées.

### Schéma
```
┌─────────────────────────────────────────────────────────────────┐
│                 HORIZONTAL POD AUTOSCALER                        │
│                      catalog-api-hpa                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Target: Deployment/catalog-api                                 │
│  Min Replicas: 2                                                │
│  Max Replicas: 10                                               │
│  Target CPU: 70%                                                │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    METRICS SERVER                        │    │
│  │              (collecte les métriques)                    │    │
│  └─────────────────────────────────────────────────────────┘    │
│                              │                                   │
│                              ▼                                   │
│  Current CPU: 85%  ──► Decision: SCALE UP                       │
│                                                                  │
│  Avant:                          Après:                         │
│  ┌─────┐ ┌─────┐                ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐│
│  │Pod 1│ │Pod 2│     ──►        │Pod 1│ │Pod 2│ │Pod 3│ │Pod 4││
│  │ 85% │ │ 85% │                │ 42% │ │ 42% │ │ 42% │ │ 42% ││
│  └─────┘ └─────┘                └─────┘ └─────┘ └─────┘ └─────┘│
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Algorithme de scaling
```
desiredReplicas = ceil(currentReplicas × (currentMetric / targetMetric))

Exemple:
- currentReplicas = 2
- currentCPU = 85%
- targetCPU = 70%

desiredReplicas = ceil(2 × (85 / 70)) = ceil(2.43) = 3
```

### Utilisation dans le projet
```yaml
# templates/catalog-api-hpa.yaml
{{- if .Values.autoscaling.enabled }}
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: {{ .Release.Name }}-catalog-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: {{ .Release.Name }}-catalog-api
  minReplicas: {{ .Values.autoscaling.catalogApi.minReplicas }}
  maxReplicas: {{ .Values.autoscaling.catalogApi.maxReplicas }}
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: {{ .Values.autoscaling.catalogApi.targetCPUUtilizationPercentage }}
{{- end }}
```

### Configuration par environnement
```yaml
# values-dev.yaml (désactivé)
autoscaling:
  enabled: false

# values-staging.yaml
autoscaling:
  enabled: true
  catalogApi:
    minReplicas: 2
    maxReplicas: 4
    targetCPUUtilizationPercentage: 70

# values-prod.yaml
autoscaling:
  enabled: true
  catalogApi:
    minReplicas: 3
    maxReplicas: 10
    targetCPUUtilizationPercentage: 70
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Élasticité** | S'adapte à la charge automatiquement |
| **Économie** | Réduit les ressources en période creuse |
| **Disponibilité** | Scale up lors des pics de trafic |
| **Automatisation** | Pas d'intervention manuelle |
| **Métriques custom** | Peut utiliser des métriques métier |

---

## 13. PodDisruptionBudget (PDB)

### Description
Un **PDB** limite le nombre de Pods qui peuvent être indisponibles simultanément lors d'une opération volontaire (maintenance, mise à jour).

### Schéma
```
┌─────────────────────────────────────────────────────────────────┐
│                    POD DISRUPTION BUDGET                         │
│                       catalog-api-pdb                            │
├─────────────────────────────────────────────────────────────────┤
│  minAvailable: 2  (ou maxUnavailable: 1)                        │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │            DEPLOYMENT: catalog-api (3 replicas)           │   │
│  ├──────────────────────────────────────────────────────────┤   │
│  │                                                           │   │
│  │  Sans PDB (drain node):                                  │   │
│  │  ┌─────┐ ┌─────┐ ┌─────┐                                │   │
│  │  │Pod 1│ │Pod 2│ │Pod 3│  ──► Tous évacués en même temps│   │
│  │  │ ❌  │ │ ❌  │ │ ❌  │      = DOWNTIME!               │   │
│  │  └─────┘ └─────┘ └─────┘                                │   │
│  │                                                           │   │
│  │  Avec PDB (minAvailable: 2):                             │   │
│  │  ┌─────┐ ┌─────┐ ┌─────┐                                │   │
│  │  │Pod 1│ │Pod 2│ │Pod 3│                                │   │
│  │  │ ✅  │ │ ✅  │ │ ❌  │  ──► Un seul évacué à la fois │   │
│  │  └─────┘ └─────┘ └─────┘      = Service maintenu        │   │
│  │                                                           │   │
│  │  Étape 1: Évacuer Pod 3, attendre nouveau Pod           │   │
│  │  Étape 2: Évacuer Pod 2, attendre nouveau Pod           │   │
│  │  Étape 3: Évacuer Pod 1                                 │   │
│  │                                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Utilisation dans le projet
```yaml
# templates/catalog-api-pdb.yaml
{{- if .Values.podDisruptionBudget.enabled }}
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: {{ .Release.Name }}-catalog-api-pdb
spec:
  minAvailable: {{ .Values.podDisruptionBudget.minAvailable }}
  selector:
    matchLabels:
      app.kubernetes.io/component: catalog-api
{{- end }}
```

### Configuration par environnement
```yaml
# values-dev.yaml (désactivé - on veut pouvoir tout casser)
podDisruptionBudget:
  enabled: false

# values-staging.yaml
podDisruptionBudget:
  enabled: true
  minAvailable: 1

# values-prod.yaml (plus strict)
podDisruptionBudget:
  enabled: true
  minAvailable: 2
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Haute disponibilité** | Garantit un minimum de Pods disponibles |
| **Maintenance safe** | Permet les mises à jour sans downtime |
| **Protection** | Empêche les évacuations accidentelles |
| **Rolling updates** | Contrôle le rythme des mises à jour |

---

## 14. NetworkPolicy

### Description
Une **NetworkPolicy** définit les règles de communication réseau entre les Pods. Par défaut, tous les Pods peuvent communiquer entre eux. Les NetworkPolicies permettent de restreindre ce trafic.

### Schéma
```
┌─────────────────────────────────────────────────────────────────┐
│                      SANS NETWORK POLICY                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Tous les Pods peuvent communiquer avec tous:                   │
│                                                                  │
│    catalog-api ◄──────► basket-api ◄──────► ordering-api        │
│         ▲                    ▲                    ▲              │
│         │                    │                    │              │
│         ▼                    ▼                    ▼              │
│    postgresql ◄──────────► redis ◄────────────► rabbitmq        │
│                                                                  │
│  PROBLÈME: Un Pod compromis peut accéder à tout!               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      AVEC NETWORK POLICY                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Communication contrôlée:                                       │
│                                                                  │
│    catalog-api ──────────────────────────► postgresql           │
│         ▲           (seul autorisé)              ▲              │
│         │                                        │ ❌            │
│    basket-api ─────────────────────────────► redis              │
│         │           (seul autorisé)                             │
│         │                                                       │
│    ordering-api ─────────────────────────► rabbitmq             │
│                     (seul autorisé)                             │
│                                                                  │
│  SÉCURITÉ: Principe du moindre privilège                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Exemple de règles
```yaml
# templates/catalog-api-networkpolicy.yaml
{{- if .Values.networkPolicies.enabled }}
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: {{ .Release.Name }}-catalog-api-netpol
spec:
  podSelector:
    matchLabels:
      app.kubernetes.io/component: catalog-api
  policyTypes:
    - Ingress
    - Egress
  ingress:
    # Autoriser le trafic depuis l'Ingress Controller
    - from:
        - namespaceSelector:
            matchLabels:
              name: kube-system
      ports:
        - port: 8080
    # Autoriser le trafic depuis yarp-gateway
    - from:
        - podSelector:
            matchLabels:
              app.kubernetes.io/component: yarp-gateway
      ports:
        - port: 8080
  egress:
    # Autoriser la connexion à PostgreSQL
    - to:
        - podSelector:
            matchLabels:
              app.kubernetes.io/component: postgresql-catalogdb
      ports:
        - port: 5432
    # Autoriser DNS
    - to:
        - namespaceSelector: {}
      ports:
        - port: 53
          protocol: UDP
{{- end }}
```

### Configuration par environnement
```yaml
# values-dev.yaml (désactivé pour faciliter le debug)
networkPolicies:
  enabled: false

# values-staging.yaml
networkPolicies:
  enabled: true

# values-prod.yaml
networkPolicies:
  enabled: true
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Sécurité** | Limite la surface d'attaque |
| **Isolation** | Segmente le réseau par application |
| **Conformité** | Répond aux exigences de sécurité |
| **Moindre privilège** | Seules les communications nécessaires |
| **Microsegmentation** | Contrôle fin du trafic |

---

## 15. ResourceQuota

### Description
Un **ResourceQuota** limite la quantité totale de ressources (CPU, mémoire, nombre d'objets) qu'un namespace peut consommer.

### Schéma
```
┌─────────────────────────────────────────────────────────────────┐
│                      RESOURCE QUOTA                              │
│                    eshop-dev-quota                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                  COMPUTE RESOURCES                       │    │
│  ├──────────────────────┬──────────────────────────────────┤    │
│  │ requests.cpu         │ 10 cores (total pour le NS)      │    │
│  │ requests.memory      │ 20Gi                             │    │
│  │ limits.cpu           │ 20 cores                         │    │
│  │ limits.memory        │ 40Gi                             │    │
│  └──────────────────────┴──────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   OBJECT COUNT                          │    │
│  ├──────────────────────┬──────────────────────────────────┤    │
│  │ pods                 │ 50                               │    │
│  │ services             │ 20                               │    │
│  │ secrets              │ 30                               │    │
│  │ configmaps           │ 30                               │    │
│  │ persistentvolumeclaims │ 10                             │    │
│  └──────────────────────┴──────────────────────────────────┘    │
│                                                                  │
│  Usage actuel:                                                  │
│  CPU: 4/10 cores (40%)  ████████░░░░░░░░░░░░░░                 │
│  Memory: 8/20Gi (40%)   ████████░░░░░░░░░░░░░░                 │
│  Pods: 14/50 (28%)      ██████░░░░░░░░░░░░░░░░                 │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Utilisation
```yaml
# templates/resourcequota.yaml
apiVersion: v1
kind: ResourceQuota
metadata:
  name: {{ .Release.Name }}-quota
spec:
  hard:
    requests.cpu: "10"
    requests.memory: 20Gi
    limits.cpu: "20"
    limits.memory: 40Gi
    pods: "50"
    services: "20"
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Équité** | Empêche un namespace de monopoliser les ressources |
| **Prévisibilité** | Garantit des ressources disponibles |
| **Coût** | Contrôle les coûts cloud |
| **Multi-tenant** | Isolation entre équipes/projets |

---

## 16. LimitRange

### Description
Un **LimitRange** définit les valeurs par défaut et les limites min/max pour les conteneurs dans un namespace.

### Schéma
```
┌─────────────────────────────────────────────────────────────────┐
│                       LIMIT RANGE                                │
│                    eshop-dev-limits                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Type: Container                                                │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                        CPU                                │   │
│  │  Min: 50m ──────────────────────────────────── Max: 2    │   │
│  │            │              │                               │   │
│  │         Default       DefaultRequest                     │   │
│  │          500m            100m                            │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                      MEMORY                               │   │
│  │  Min: 64Mi ─────────────────────────────────── Max: 4Gi  │   │
│  │             │              │                              │   │
│  │          Default       DefaultRequest                    │   │
│  │           512Mi          128Mi                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  Si un Pod ne spécifie pas de resources:                       │
│  → Applique automatiquement les defaults                       │
│                                                                  │
│  Si un Pod dépasse les limites:                                │
│  → Rejeté à la création                                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Utilisation
```yaml
# templates/limitrange.yaml
apiVersion: v1
kind: LimitRange
metadata:
  name: {{ .Release.Name }}-limits
spec:
  limits:
    - type: Container
      default:
        cpu: 500m
        memory: 512Mi
      defaultRequest:
        cpu: 100m
        memory: 128Mi
      min:
        cpu: 50m
        memory: 64Mi
      max:
        cpu: 2
        memory: 4Gi
```

### Avantages
| Avantage | Description |
|----------|-------------|
| **Defaults** | Valeurs automatiques si non spécifiées |
| **Protection** | Empêche les requêtes excessives |
| **Standardisation** | Garantit des limites cohérentes |
| **Scheduling** | Aide le scheduler à placer les Pods |

---

## Récapitulatif visuel complet

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CLUSTER KUBERNETES                                 │
│                                                                              │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                         NAMESPACE: eshop-dev                           │  │
│  │  ┌──────────────────────────────────────────────────────────────────┐ │  │
│  │  │ ResourceQuota │ LimitRange │ NetworkPolicy (disabled in dev)     │ │  │
│  │  └──────────────────────────────────────────────────────────────────┘ │  │
│  │                                                                        │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                   │  │
│  │  │ ConfigMap   │  │   Secret    │  │ImagePullSec │                   │  │
│  │  │ (config)    │  │ (passwords) │  │ (icr-secret)│                   │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                   │  │
│  │         │                │                │                           │  │
│  │         └────────────────┼────────────────┘                           │  │
│  │                          ▼                                            │  │
│  │  ┌──────────────────────────────────────────────────────────────┐    │  │
│  │  │                    ServiceAccount                             │    │  │
│  │  │               (identité + imagePullSecrets)                   │    │  │
│  │  └──────────────────────────────────────────────────────────────┘    │  │
│  │                          │                                            │  │
│  │         ┌────────────────┼────────────────┐                          │  │
│  │         ▼                ▼                ▼                          │  │
│  │  ┌────────────┐   ┌────────────┐   ┌────────────┐                   │  │
│  │  │ Deployment │   │ Deployment │   │StatefulSet │                   │  │
│  │  │ catalog-api│   │ basket-api │   │ postgresql │                   │  │
│  │  │  + HPA     │   │  + HPA     │   │            │                   │  │
│  │  │  + PDB     │   │  + PDB     │   │            │                   │  │
│  │  └──────┬─────┘   └──────┬─────┘   └──────┬─────┘                   │  │
│  │         │                │                │                          │  │
│  │         ▼                ▼                ▼                          │  │
│  │  ┌────────────┐   ┌────────────┐   ┌────────────┐                   │  │
│  │  │    Pod     │   │    Pod     │   │    Pod     │                   │  │
│  │  │  :8080     │   │  :8080     │   │  :5432     │                   │  │
│  │  └──────┬─────┘   └──────┬─────┘   └──────┬─────┘                   │  │
│  │         │                │                │                          │  │
│  │         │                │                ▼                          │  │
│  │         │                │         ┌────────────┐                   │  │
│  │         │                │         │    PVC     │                   │  │
│  │         │                │         │   10Gi    │                   │  │
│  │         │                │         └────────────┘                   │  │
│  │         │                │                                           │  │
│  │         ▼                ▼                                           │  │
│  │  ┌────────────┐   ┌────────────┐                                    │  │
│  │  │  Service   │   │  Service   │                                    │  │
│  │  │ ClusterIP  │   │ ClusterIP  │                                    │  │
│  │  │   :80      │   │   :80      │                                    │  │
│  │  └──────┬─────┘   └──────┬─────┘                                    │  │
│  │         │                │                                           │  │
│  │         └────────┬───────┘                                           │  │
│  │                  ▼                                                   │  │
│  │  ┌──────────────────────────────────────────────────────────────┐   │  │
│  │  │                        INGRESS                                │   │  │
│  │  │  eshop-dev.* → shopping-web:80                               │   │  │
│  │  │  api-dev.*   → yarp-gateway:80                               │   │  │
│  │  │  health-dev.* → healthdeck-web:80                            │   │  │
│  │  └──────────────────────────────────────────────────────────────┘   │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                    │                                         │
│                                    ▼                                         │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                    INGRESS CONTROLLER (nginx)                          │  │
│  │              lgharbi-eshop-cluster-k8s-...appdomain.cloud             │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                    │                                         │
└────────────────────────────────────┼─────────────────────────────────────────┘
                                     │
                                     ▼
                                 INTERNET
```

---

## Ressources additionnelles

- [Documentation officielle Kubernetes](https://kubernetes.io/docs/home/)
- [IBM Cloud Kubernetes Service](https://cloud.ibm.com/docs/containers)
- [Helm Documentation](https://helm.sh/docs/)

---

*Document créé pour le projet EshopMicroservices - Janvier 2026*
