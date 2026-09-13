# Despliegue de Kapea en Azure

Los recursos se declaran en Terraform (`Deploy/infra`) y el camino a producción es
un único flujo de GitHub Actions (`.github/workflows/deploy.yml`) que compila,
prueba, propone el plan, **espera aprobación manual** y sólo entonces aplica,
migra la base de datos y publica las dos revisiones.

El entorno local no cambia: `docker compose up` sigue levantando la aplicación con
su base de datos en contenedor y su almacén de secretos en fichero, sin
credenciales de Azure y sin tocar nada de esto.

---

## 1. Pasos previos, una sola vez

Terraform no puede crear estas dos cosas, porque son las que le dan acceso.

### 1.1 El estado remoto

La cuenta de almacenamiento y el contenedor **ya existen**: son los mismos que usa
el otro proyecto. Lo único propio de Kapea es la ruta del blob, y tiene que ser
distinta. Coincidir significaría aplicar sobre el estado del otro proyecto y
empezar a destruir sus recursos, así que compruébalo antes del primer plan:

```bash
CUENTA=<cuenta-de-almacenamiento>
CONTENEDOR=<contenedor>

az storage blob list \
  --account-name "$CUENTA" --container-name "$CONTENEDOR" \
  --auth-mode login --query "[].name" -o tsv
```

La ruta de Kapea lleva el nombre del proyecto dentro, por ejemplo
`nacho/kapea/prod/terraform.tfstate`.

### 1.2 El registro de aplicación con credencial federada

Uno propio, no el del otro proyecto: la federación va atada al repositorio, y
compartirlo daría a un mismo sujeto permiso sobre los recursos de los dos.

```bash
REPO=Brainiac1703/Kapea
SUSCRIPCION=$(az account show --query id -o tsv)

APP_ID=$(az ad app create --display-name kapea-infra --query appId -o tsv)
az ad sp create --id "$APP_ID"

# Tres sujetos, y cada uno en dos formas.
#
# Tres porque cuando un trabajo se ata a un environment, GitHub cambia el sujeto
# del token: presenta "environment:production" en lugar de "ref:refs/heads/main".
# Planificar no usa entorno y aplicar sí, así que hacen falta las dos. Faltar una
# da AADSTS700213 sólo en el trabajo afectado, que es lo que lo confunde.
#
# Dos formas porque GitHub puede presentar el repositorio por nombre o con los
# identificadores inmutables del propietario y del repositorio, que sobreviven a
# un cambio de nombre. Este repositorio presenta la segunda, y sin ella el plan
# falla con el mismo AADSTS700213.
OWNER_ID=$(gh api "repos/${REPO}" -q .owner.id)
REPO_ID=$(gh api "repos/${REPO}" -q .id)
INMUTABLE="repo:${REPO%%/*}@${OWNER_ID}/${REPO##*/}@${REPO_ID}"

for BASE in "repo:${REPO}" "${INMUTABLE}"; do
  for SUFIJO in "ref:refs/heads/main" "pull_request" "environment:production"; do
    SUJETO="${BASE}:${SUFIJO}"
    az ad app federated-credential create --id "$APP_ID" --parameters "{
      \"name\": \"github-$(echo -n "${SUJETO}" | tr -c 'a-zA-Z0-9' '-' | cut -c1-120)\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"${SUJETO}\",
      \"audiences\": [\"api://AzureADTokenExchange\"]
    }"
  done
done
```

Las llaves en `${REPO}` no son decorativas: en zsh, `$REPO:r` y `$REPO:e` son
modificadores de ruta y estropean el sujeto sin dar ningún error.

Y los permisos, limitados al grupo de recursos porque la suscripción es
corporativa. El grupo se crea en la primera etapa, así que estos permisos se
conceden después de aplicarla. Colaborador para crear los recursos, y
administrador de control de acceso porque el propio Terraform asigna roles a la
identidad de la aplicación:

```bash
PRINCIPAL=$(az ad sp show --id "$APP_ID" --query id -o tsv)

for ROL in "Contributor" "Role Based Access Control Administrator"; do
  az role assignment create \
    --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal \
    --role "$ROL" --scope "/subscriptions/$SUSCRIPCION/resourceGroups/rg-kapea"
done
```

Con permisos sólo sobre el grupo, Terraform no puede registrar proveedores de
recursos. Comprueba una vez que están registrados en la suscripción:

```bash
for NS in Microsoft.App Microsoft.ContainerRegistry Microsoft.Sql Microsoft.KeyVault \
  Microsoft.Storage Microsoft.OperationalInsights Microsoft.ManagedIdentity \
  Microsoft.CognitiveServices; do
  echo "$NS $(az provider show -n $NS --query registrationState -o tsv)"
done
```

Y el acceso de datos al contenedor del estado, que está en otra suscripción:

```bash
SUSCRIPCION_ESTADO=<suscripción de la cuenta del estado>

CONTENEDOR_ID=$(az storage account show --name "$CUENTA" --subscription "$SUSCRIPCION_ESTADO" --query id -o tsv)/blobServices/default/containers/$CONTENEDOR

az role assignment create \
  --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" --scope "$CONTENEDOR_ID"
```

Apunta lo que imprime esto, que es lo que necesita el flujo:

```bash
echo "AZURE_INFRA_CLIENT_ID = $APP_ID"
echo "AZURE_TENANT_ID       = $(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID = $SUSCRIPCION"
echo "SQL_ADMIN_LOGIN       = kapea-infra"
echo "SQL_ADMIN_OBJECT_ID   = $PRINCIPAL"
```

`SQL_ADMIN_OBJECT_ID` es el del *service principal*, no el del registro de
aplicación. Son distintos, y usar el equivocado hace que Azure rechace el
administrador sin explicar por qué.

### 1.3 El entorno y las variables de GitHub

En **Settings → Environments**, crea `production` y añádete como revisor
requerido. Esa es la parada de aprobación: sin ella el flujo aplicaría solo.

En **Settings → Variables**, da de alta estas siete. Ninguna es un secreto:

| Variable | Valor |
| --- | --- |
| `AZURE_INFRA_CLIENT_ID` | el `APP_ID` de arriba |
| `AZURE_TENANT_ID` | el inquilino |
| `AZURE_SUBSCRIPTION_ID` | la suscripción |
| `TFSTATE_STORAGE_ACCOUNT` | la cuenta compartida |
| `TFSTATE_CONTAINER` | el contenedor compartido |
| `TFSTATE_KEY` | la ruta de Kapea, distinta de la del otro proyecto |
| `SQL_ADMIN_LOGIN` | `kapea-infra` |
| `SQL_ADMIN_OBJECT_ID` | el `PRINCIPAL` de arriba |

Si falta cualquiera de ellas, los trabajos que tocan Azure se omiten en lugar de
fallar, para no dejar el repositorio en rojo permanente.

---

## 2. Primera etapa: sólo el servicio de modelos

Es lo que hace falta para desarrollar en local, y se crea sin desplegar nada más.

```bash
cd Deploy/infra
cp backend.hcl.example backend.hcl      # y ajusta cuenta, contenedor y ruta
cp terraform.tfvars.example terraform.tfvars

terraform init -backend-config=backend.hcl
terraform apply                          # deploy_app queda en false
```

Se crean tres recursos: el grupo, la cuenta de servicios cognitivos y el
despliegue del modelo. Ni registro de contenedores, ni base de datos, ni
aplicaciones.

Lo que hace falta para configurarlo en local sale de aquí:

```bash
terraform output openai_endpoint
terraform output openai_deployment
terraform output -raw openai_key
```

Y va al fichero `.env` de la raíz del repositorio:

```dotenv
AZURE_OPENAI_ENDPOINT=https://oai-kapea.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT=gpt-4.1-mini
AZURE_OPENAI_KEY=<la clave>
```

Con eso, la traducción de un método a reglas y la extracción de ideas de un texto
dejan de decir que no hay servicio configurado. La aplicación desplegada no lleva
esa clave: llama con su identidad.

---

## 3. Segunda etapa: la aplicación

Pon `deploy_app = true` en `terraform.tfvars` (o deja que lo haga el flujo, que
siempre lo pasa activado) y lanza el flujo **Deploy**. Se detendrá esperando tu
aprobación del plan.

Cuando termine, quedan dos cosas por hacer a mano, y en este orden.

### 3.1 Registrar la URL de vuelta en Google

Google exige registrarla antes de que exista, así que hasta aquí no se puede.

```bash
terraform output -raw api_url
```

En la consola de Google Cloud, en las credenciales del cliente OAuth, añade como
URI de redirección autorizado:

```
https://<lo-que-devuelva>/signin-google
```

### 3.2 Dar de alta el secreto de Google en el almacén

No pasa por variables de GitHub a propósito: es un secreto, y el punto del
despliegue es que no haya ninguno guardado ahí.

```bash
VAULT=$(terraform output -raw key_vault_uri)

az keyvault secret set --vault-base-url "$VAULT" \
  --name Authentication--Google--ClientId --value '<identificador>'

az keyvault secret set --vault-base-url "$VAULT" \
  --name Authentication--Google--ClientSecret --value '<secreto>'
```

Los dos guiones son la jerarquía: un nombre de secreto no admite dos puntos, así
que `Authentication--Google--ClientId` se lee como
`Authentication:Google:ClientId`. La API sólo carga los secretos con ese prefijo;
las credenciales de los brókeres viven en el mismo almacén y se leen una a una
cuando hacen falta.

Reinicia la revisión de la API para que los recoja:

```bash
az containerapp revision restart --name ca-kapea-api --resource-group rg-kapea \
  --revision "$(az containerapp revision list --name ca-kapea-api \
    --resource-group rg-kapea --query '[?properties.active].name | [0]' -o tsv)"
```

---

## 4. Qué cuesta y qué se puede apagar

Estimación mensual para una cartera personal, en la región de despliegue y con
los tamaños por omisión:

| Recurso | Aproximado | Nota |
| --- | --- | --- |
| SQL Database serverless | 5-15 € | Cobra por segundo de cómputo y se pausa tras una hora sin uso. Con uso a ráfagas, la mayor parte es almacenamiento. |
| Container App de la API | 0-3 € | Escala a cero cuando nadie entra. |
| Container App del trabajador | 3-6 € | Una réplica siempre encendida, porque si escalara a cero nadie despertaría al temporizador. |
| Azure OpenAI | por uso | Sólo se paga lo que se pide. Los tres trabajos son de una pasada. |
| Key Vault, Storage, Log Analytics, registro | 1-3 € | Céntimos cada uno; el registro en Basic. |

**Qué se puede apagar sin perder datos:**

- El trabajador. `az containerapp update --name ca-kapea-sync --resource-group
  rg-kapea --min-replicas 0 --max-replicas 0`. La cartera sigue consultable y las
  importaciones por fichero siguen funcionando; sólo deja de sincronizarse sola.
- La retención de registros, bajándola en `azurerm_log_analytics_workspace`.
- Todo, con `terraform destroy`. Se pierde la base de datos, así que exporta
  antes si te importa lo importado. Los datos se pueden reconstruir volviendo a
  leer el histórico de las plataformas.

Lo que **no** conviene apagar es el almacén de secretos: al borrarlo se van las
credenciales de los brókeres, y hay que volver a darlas de alta.

---

## 5. Un bloqueo de estado que se quedó tomado

El backend bloquea con un *lease* sobre el blob del estado. Dos ejecuciones a la
vez esperan, y eso está resuelto. Lo que no se resuelve esperando es un proceso
muerto a media ejecución: su *lease* no caduca, y cualquier plan posterior falla
diciendo que el estado está bloqueado.

Primero comprueba que de verdad no hay nadie aplicando. Después:

```bash
# Con el identificador que imprime el error de Terraform:
terraform force-unlock <ID-del-bloqueo>

# Si ni eso funciona, se rompe el lease directamente sobre el blob:
az storage blob lease break \
  --account-name "$CUENTA" --container-name "$CONTENEDOR" \
  --blob-name "<la-ruta-de-kapea>" --auth-mode login
```

Romper el *lease* de una ejecución que sigue viva deja el estado a medias, que es
mucho peor que esperar. Compruébalo antes.
