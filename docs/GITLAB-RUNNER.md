# Guide Complet : GitLab Runner avec Docker sur Windows

Ce guide documente la configuration complète du GitLab Runner pour le projet EshopMicroservices, incluant les problèmes rencontrés et leurs solutions.

## Table des Matières

- [Architecture](#architecture)
- [Prérequis](#prérequis)
- [Installation du Runner](#installation-du-runner)
- [Configuration du Runner](#configuration-du-runner)
- [Configuration du Pipeline](#configuration-du-pipeline)
- [Concepts Clés](#concepts-clés)
- [Troubleshooting](#troubleshooting)
- [Commandes Utiles](#commandes-utiles)

---

## Architecture

### Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────────────────┐
│  WINDOWS HOST (votre machine)                                           │
│                                                                         │
│  ┌─────────────────────┐       ┌──────────────────────────────────┐    │
│  │   Docker Desktop    │       │   GitLab Runner                  │    │
│  │                     │       │                                  │    │
│  │  Docker Daemon      │◄─────►│   Exécute les jobs CI/CD        │    │
│  │  /var/run/          │       │   dans des containers            │    │
│  │  docker.sock        │       │                                  │    │
│  └─────────────────────┘       └──────────────────────────────────┘    │
│           ▲                                                             │
│           │ Socket monté dans le container                             │
│           ▼                                                             │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  Container Job (docker:24-cli ou docker:latest)                 │   │
│  │                                                                 │   │
│  │  /var/run/docker.sock ──────► Accès au Docker de l'hôte        │   │
│  │                                                                 │   │
│  │  $ docker build ...    ✓                                       │   │
│  │  $ docker push ...     ✓                                       │   │
│  │  $ docker info         ✓                                       │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### Types de Runners GitLab

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  ┌─────────────────────┐                                               │
│  │  INSTANCE RUNNERS   │  = Fournis par GitLab.com                     │
│  │  (Shared Runners)   │  = Partagés avec tous les utilisateurs        │
│  │                     │  = Peuvent interférer avec l'ordre des jobs   │
│  └─────────────────────┘                                               │
│                                                                         │
│  ┌─────────────────────┐                                               │
│  │   GROUP RUNNERS     │  = Créés par vous                             │
│  │                     │  = Partagés dans votre groupe GitLab          │
│  │                     │  = Recommandé pour les équipes                │
│  └─────────────────────┘                                               │
│                                                                         │
│  ┌─────────────────────┐                                               │
│  │  PROJECT RUNNERS    │  = Créés par vous                             │
│  │                     │  = Dédiés à un seul projet                    │
│  └─────────────────────┘                                               │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Prérequis

### Logiciels requis

- **Docker Desktop** pour Windows (avec WSL2 ou Hyper-V)
- **GitLab Runner** installé sur Windows
- Compte GitLab avec accès au projet/groupe

### Configuration Docker Desktop

1. Ouvrir Docker Desktop
2. Aller dans **Settings** → **General**
3. S'assurer que Docker est en cours d'exécution

---

## Installation du Runner

### 1. Télécharger GitLab Runner

```powershell
# Créer le dossier
mkdir C:\GitLab-Runner

# Télécharger le binaire
Invoke-WebRequest -Uri "https://gitlab-runner-downloads.s3.amazonaws.com/latest/binaries/gitlab-runner-windows-amd64.exe" -OutFile "C:\GitLab-Runner\gitlab-runner.exe"
```

### 2. Enregistrer le Runner

```powershell
cd C:\GitLab-Runner
.\gitlab-runner.exe register
```

Répondre aux questions :
- **GitLab URL** : `https://gitlab.com`
- **Registration token** : (obtenu depuis GitLab → Settings → CI/CD → Runners)
- **Description** : `docker-runner-windows`
- **Tags** : (laisser vide ou ajouter des tags)
- **Executor** : `docker`
- **Default Docker image** : `docker:24-cli`

### 3. Installer comme service Windows

```powershell
.\gitlab-runner.exe install
.\gitlab-runner.exe start
```

---

## Configuration du Runner

### Fichier config.toml

Emplacement : `C:\GitLab-Runner\config.toml`

```toml
concurrent = 1
check_interval = 0
connection_max_age = "15m0s"
shutdown_timeout = 0

[session_server]
  session_timeout = 1800

[[runners]]
  name = "docker-runner-windows"
  url = "https://gitlab.com"
  id = 51516979
  token = "votre-token-ici"
  token_obtained_at = 2026-01-29T07:15:49Z
  token_expires_at = 0001-01-01T00:00:00Z
  executor = "docker"

  [runners.cache]
    MaxUploadedArchiveSize = 0

  [runners.docker]
    tls_verify = false
    image = "docker:24-cli"
    privileged = false
    wait_for_services_timeout = 60
    disable_entrypoint_overwrite = false
    oom_kill_disable = false
    disable_cache = false
    shm_size = 0
    network_mtu = 0
    volumes = ["/var/run/docker.sock:/var/run/docker.sock", "/cache"]
```

### Explication des paramètres clés

| Paramètre | Valeur | Description |
|-----------|--------|-------------|
| `concurrent` | `1` | Nombre de jobs simultanés (1 = séquentiel) |
| `executor` | `docker` | Utilise Docker pour exécuter les jobs |
| `image` | `docker:24-cli` | Image Docker par défaut pour les jobs |
| `privileged` | `false` | Pas besoin de mode privilégié avec socket binding |
| `volumes` | `["/var/run/docker.sock:/var/run/docker.sock", "/cache"]` | Monte le socket Docker |

### Désactiver les Instance Runners

**Important** : Pour éviter que les runners partagés de GitLab n'interfèrent avec vos jobs :

1. Aller dans **Groupe** → **Settings** → **CI/CD** → **Runners**
2. Désactiver **"Enable instance runners for this group"**

```
┌─────────────────────────────────────────────────┐
│ Instance runners                                │
│                                                 │
│ Enable instance runners for this group          │
│ [  OFF  ]  ← Désactiver                        │
└─────────────────────────────────────────────────┘
```

---

## Configuration du Pipeline

### Fichier .gitlab-ci.yml

#### Structure de base

```yaml
stages:
  - build
  - test
  - push
  - deploy-dev
  - deploy-staging
  - deploy-prod
```

#### Template de Build (Socket Binding)

```yaml
.build_template: &build_template
  stage: build
  image: docker:latest
  # Pas de service dind - utilise le socket Docker de l'hôte
  before_script:
    - docker info
  rules:
    - if: '$CI_COMMIT_BRANCH =~ /^(feat|fix|chore)\//'
    - if: '$CI_COMMIT_BRANCH =~ /^(dev|develop)$/'
    - if: '$CI_COMMIT_BRANCH == "main"'
    - if: '$CI_COMMIT_TAG =~ /^v/'
```

#### Contrôle de l'ordre d'exécution avec `needs`

```yaml
# ❌ MAUVAIS : dependencies ne contrôle que les artifacts
push:catalog-api:
  dependencies:
    - build:catalog-api

# ✅ BON : needs contrôle l'ordre d'exécution
push:catalog-api:
  needs:
    - job: build:catalog-api
      artifacts: true    # Télécharge les artifacts
    - job: test:unit
      artifacts: false   # Attend le job sans télécharger
```

#### Exemple complet de job

```yaml
build:catalog-api:
  <<: *build_template
  script:
    - cd src/Services/Catalog/Catalog.API
    - docker build -t catalog-api:$CI_COMMIT_SHORT_SHA -f Dockerfile ../../..
    - docker save catalog-api:$CI_COMMIT_SHORT_SHA -o catalog-api.tar
  artifacts:
    paths:
      - src/Services/Catalog/Catalog.API/catalog-api.tar
    expire_in: 2 hours

push:catalog-api:
  stage: push
  image: docker:latest
  needs:
    - job: build:catalog-api
      artifacts: true
    - job: test:unit
      artifacts: false
  script:
    - docker load -i src/Services/Catalog/Catalog.API/catalog-api.tar
    - docker tag catalog-api:$CI_COMMIT_SHORT_SHA $REGISTRY/catalog-api:$CI_COMMIT_SHORT_SHA
    - docker push $REGISTRY/catalog-api:$CI_COMMIT_SHORT_SHA
```

---

## Concepts Clés

### Socket Binding vs Docker-in-Docker (dind)

#### Comparaison

| Aspect | Docker-in-Docker (dind) | Socket Binding |
|--------|-------------------------|----------------|
| **Fonctionnement** | Docker dans un container | Utilise Docker de l'hôte |
| **`privileged`** | Requis (`true`) | Non requis (`false`) |
| **Services** | `docker:24-dind` nécessaire | Aucun |
| **Networking** | Complexe | Simple |
| **Windows** | Problématique | Fonctionne bien |
| **Performance** | Plus lent | Plus rapide |
| **Isolation** | Totale | Partage le Docker hôte |

#### Pourquoi dind ne fonctionne pas bien sur Windows

1. **Problèmes de réseau** : Les containers ne peuvent pas se joindre via le réseau interne
2. **Health check timeout** : Le service dind prend trop de temps à démarrer
3. **Délai intentionnel** : Docker ajoute un délai de 15 secondes quand TLS est désactivé
4. **Permissions** : Le mode `privileged` a des comportements différents sur Windows

#### Solution : Socket Binding

```toml
# config.toml
[runners.docker]
  privileged = false
  volumes = ["/var/run/docker.sock:/var/run/docker.sock", "/cache"]
```

```yaml
# .gitlab-ci.yml - PAS de service dind
.build_template:
  image: docker:latest
  # Pas de "services:" avec dind
  before_script:
    - docker info
```

### `dependencies` vs `needs`

```
┌─────────────────────────────────────────────────────────────────────────┐
│  dependencies                                                           │
│  ─────────────                                                          │
│  • Contrôle UNIQUEMENT le téléchargement des artifacts                 │
│  • N'affecte PAS l'ordre d'exécution                                   │
│  • Les jobs attendent le stage précédent (comportement par défaut)     │
│  • Avec plusieurs runners, l'ordre peut être ignoré                    │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│  needs                                                                  │
│  ─────                                                                  │
│  • Contrôle l'ORDRE D'EXÉCUTION (DAG)                                  │
│  • Force le job à attendre les jobs spécifiés                          │
│  • Peut télécharger les artifacts (artifacts: true/false)              │
│  • Ordre TOUJOURS garanti, même avec plusieurs runners                 │
└─────────────────────────────────────────────────────────────────────────┘
```

#### Visualisation

```
AVEC dependencies (ordre non garanti avec plusieurs runners):

  Stage 1          Stage 2          Stage 3
  ───────          ───────          ───────
  build:a  ───────────────────────► push:a
  build:b  ──────► test ──────────► push:b
  build:c  ───────────────────────► push:c

  ⚠️ Avec plusieurs runners, push:a peut démarrer avant que build:a soit fini !


AVEC needs (ordre TOUJOURS garanti):

  build:a ─────────────────────────► push:a ──┐
                                              │
  build:b ──────► test ────────────► push:b ──┼──► deploy
                                              │
  build:c ─────────────────────────► push:c ──┘

  ✅ push:a attend TOUJOURS que build:a soit terminé
```

---

## Troubleshooting

### Problème : Jobs s'exécutent dans le désordre

**Symptôme** : `push` démarre avant `build`

**Cause** : Utilisation de `dependencies` au lieu de `needs`, ou Instance Runners activés

**Solution** :
1. Utiliser `needs` dans le pipeline
2. Désactiver les Instance Runners au niveau du groupe

### Problème : dind service timeout

**Symptôme** :
```
*** WARNING: Service ... probably didn't start properly.
Health check error: service "..." timeout
```

**Cause** : Docker-in-Docker ne fonctionne pas correctement sur Windows

**Solution** : Utiliser le socket binding (voir configuration ci-dessus)

### Problème : mount: permission denied

**Symptôme** :
```
mount: permission denied (are you root?)
Could not mount /sys/kernel/security.
```

**Cause** : dind nécessite `privileged: true` mais cela pose des problèmes sur Windows

**Solution** : Utiliser socket binding avec `privileged: false`

### Problème : Cannot connect to Docker daemon

**Symptôme** :
```
Cannot connect to the Docker daemon at tcp://...:2375
```

**Cause** : Le container ne peut pas joindre le daemon Docker

**Solution** :
1. Vérifier que Docker Desktop est lancé
2. Utiliser socket binding au lieu de TCP
3. Vérifier le volume dans config.toml :
   ```toml
   volumes = ["/var/run/docker.sock:/var/run/docker.sock", "/cache"]
   ```

### Problème : Error response from daemon (vide)

**Symptôme** :
```
Server:
ERROR: Error response from daemon:
errors pretty printing info
```

**Cause** : Incompatibilité de version entre client et serveur Docker

**Solution** : Utiliser `docker:latest` au lieu d'une version spécifique

---

## Commandes Utiles

### Gestion du Runner

```powershell
# Démarrer le runner
gitlab-runner start

# Arrêter le runner
gitlab-runner stop

# Redémarrer le runner (après modification de config.toml)
gitlab-runner restart

# Voir le statut
gitlab-runner status

# Lister les runners enregistrés
gitlab-runner list

# Vérifier la configuration
gitlab-runner verify

# Exécuter en mode debug (utile pour le troubleshooting)
gitlab-runner --debug run
```

### Gestion de Docker Desktop

```powershell
# Vérifier que Docker fonctionne
docker info

# Voir les containers en cours
docker ps

# Nettoyer les ressources inutilisées
docker system prune -a
```

### Tests de connectivité

```powershell
# Test Docker via socket (par défaut)
docker info

# Test Docker via TCP (si configuré)
$env:DOCKER_HOST="tcp://localhost:2375"
docker info

# Réinitialiser DOCKER_HOST
Remove-Item Env:\DOCKER_HOST
```

---

## Checklist de Configuration

```
□ Docker Desktop installé et en cours d'exécution
□ GitLab Runner installé et enregistré
□ Instance Runners désactivés au niveau du groupe
□ config.toml configuré avec socket binding :
    volumes = ["/var/run/docker.sock:/var/run/docker.sock", "/cache"]
    privileged = false
□ .gitlab-ci.yml sans service dind
□ .gitlab-ci.yml utilise `needs` pour l'ordre d'exécution
□ gitlab-runner restart exécuté après modifications
```

---

## Références

- [Documentation GitLab Runner](https://docs.gitlab.com/runner/)
- [Docker-in-Docker](https://docs.gitlab.com/ee/ci/docker/using_docker_build.html)
- [Utilisation de `needs`](https://docs.gitlab.com/ee/ci/yaml/#needs)
- [Configuration avancée du Runner](https://docs.gitlab.com/runner/configuration/advanced-configuration.html)
