{{/*
Expand the name of the chart.
*/}}
{{- define "eshop.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "eshop.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "eshop.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "eshop.labels" -}}
helm.sh/chart: {{ include "eshop.chart" . }}
{{ include "eshop.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/environment: {{ .Values.global.environment }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "eshop.selectorLabels" -}}
app.kubernetes.io/name: {{ include "eshop.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "eshop.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "eshop.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Get image tag for a service
*/}}
{{- define "eshop.imageTag" -}}
{{- $imageConfig := index .Values.images . -}}
{{- if $imageConfig.tag }}
{{- $imageConfig.tag }}
{{- else }}
{{- .Values.global.imageTag }}
{{- end }}
{{- end }}

{{/*
Build full image name for a service
*/}}
{{- define "eshop.image" -}}
{{- $service := index . 0 -}}
{{- $context := index . 1 -}}
{{- $imageConfig := index $context.Values.images $service -}}
{{- $tag := $imageConfig.tag | default $context.Values.global.imageTag | toString -}}
{{- printf "%s/%s:%s" $context.Values.global.imageRegistry $imageConfig.repository $tag -}}
{{- end }}

{{/*
PostgreSQL connection string for CatalogDb
*/}}
{{- define "eshop.catalogDbConnectionString" -}}
{{- printf "Server=%s-postgresql-catalogdb;Port=%d;Database=%s;User Id=%s;Password=%s;Include Error Detail=true"
    (include "eshop.fullname" .)
    (.Values.postgresql.catalogDb.port | int)
    .Values.postgresql.catalogDb.name
    .Values.postgresql.catalogDb.user
    .Values.postgresql.catalogDb.password -}}
{{- end }}

{{/*
PostgreSQL connection string for BasketDb
*/}}
{{- define "eshop.basketDbConnectionString" -}}
{{- printf "Server=%s-postgresql-basketdb;Port=%d;Database=%s;User Id=%s;Password=%s;Include Error Detail=true"
    (include "eshop.fullname" .)
    (.Values.postgresql.basketDb.port | int)
    .Values.postgresql.basketDb.name
    .Values.postgresql.basketDb.user
    .Values.postgresql.basketDb.password -}}
{{- end }}

{{/*
SQL Server connection string for OrderDb
*/}}
{{- define "eshop.orderDbConnectionString" -}}
{{- printf "Server=%s-sqlserver,%d;Database=%s;User Id=sa;Password=%s;TrustServerCertificate=true;Encrypt=false"
    (include "eshop.fullname" .)
    (.Values.sqlserver.port | int)
    .Values.sqlserver.database
    .Values.sqlserver.saPassword -}}
{{- end }}

{{/*
Redis connection string
*/}}
{{- define "eshop.redisConnectionString" -}}
{{- printf "%s-redis:%d" (include "eshop.fullname" .) (.Values.redis.port | int) -}}
{{- end }}

{{/*
RabbitMQ connection string
*/}}
{{- define "eshop.rabbitmqConnectionString" -}}
{{- printf "amqp://%s:%s@%s-rabbitmq:%d"
    .Values.rabbitmq.username
    .Values.rabbitmq.password
    (include "eshop.fullname" .)
    (.Values.rabbitmq.port | int) -}}
{{- end }}

{{/*
Elasticsearch URL
*/}}
{{- define "eshop.elasticsearchUrl" -}}
{{- printf "http://%s-elasticsearch:%d" (include "eshop.fullname" .) (.Values.elasticsearch.port | int) -}}
{{- end }}

{{/*
Discount gRPC URL
*/}}
{{- define "eshop.discountGrpcUrl" -}}
{{- printf "http://%s-discount-grpc:%d" (include "eshop.fullname" .) (.Values.ports.discountGrpc | int) -}}
{{- end }}
