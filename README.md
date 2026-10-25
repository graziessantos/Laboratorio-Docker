# Laboratório Docker — Integração DevOps (Unidade 3)

**Centro Universitário de Brasília – CEUB**
Faculdade de Ciência da Computação
Disciplina: Integração DevOps
Professor: Daniel Linhares Lim Apo
Aluna: Grazielle Santana (RA 22510450)

Repositório (fork): https://github.com/graziessantos/CSharp-Unit-Testing-Tutorial

---

## Sobre o projeto

API simples em **ASP.NET Core (.NET 10)** empacotada em uma imagem Docker e executada em containers para diferentes ambientes (staging e production). O laboratório cobre:

- build de imagem com **multi-stage build**;
- execução de containers com **port mapping** e **variáveis de ambiente**;
- **versionamento** de imagens por tag (`1.0.0`, `1.1.0`, `1.2.0`);
- promoção de versão de **staging** para **production**;
- **health check** e quality gate manual;
- **falha controlada** e **rollback**;
- orquestração local com **Docker Compose**, incluindo `healthcheck`.

Fluxo geral:

```
Código C# → Dockerfile → docker build → Imagem versionada
          → Container (staging) → Validação → Production → Rollback
```

## Endpoints da API

| Endpoint   | Descrição                                              |
|------------|--------------------------------------------------------|
| `/`        | Retorna nome da aplicação, versão e ambiente           |
| `/health`  | Retorna `status`, `version`, `environment`, `timestamp`|
| `/version` | Adicionado na versão 1.1.0                             |

A versão e o ambiente vêm das variáveis `APP_VERSION` e `APP_ENVIRONMENT`.

## Pré-requisitos

```bash
docker --version
dotnet --version
git --version
```

Ambiente utilizado: Docker 24.0.6, .NET SDK 10.0.401, Git 2.53.0 (Windows).

Teste do Docker (com o Docker Desktop aberto):

```bash
docker run --rm hello-world
```

## Executando localmente (sem Docker)

```bash
dotnet new webapi -n DevOpsWebApi
cd DevOpsWebApi
dotnet run
```

Neste laboratório a aplicação subiu em `http://localhost:5299`. Teste `/` e `/health`.

## Estrutura de arquivos

```
DevOpsWebApi/
├── DevOpsWebApi.csproj
├── Program.cs
├── Dockerfile
├── .dockerignore
└── compose.yaml
```

## Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY DevOpsWebApi.csproj .
RUN dotnet restore

COPY . .

RUN dotnet publish \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "DevOpsWebApi.dll"]
```

- **Stage `build`:** usa a imagem com o .NET SDK para restaurar e publicar o projeto.
- **Stage `runtime`:** usa a imagem `aspnet`, menor, e copia somente o resultado publicado.
- O `RUN` que instala o `curl` está no stage **`runtime`**, pois é essa a imagem que roda o container e onde o healthcheck procura o executável. Isso foi acrescentado no desafio de healthcheck (ver seção abaixo).

## .dockerignore

```
bin/
obj/
.git/
.github/
.vscode/
*.user
*.suo
```

## Construção das imagens

```bash
docker build -t devops-webapi:1.0.0 .
docker images
```

## Execução

Primeiro container (versão 1.0.0):

```bash
docker run -d --name devops-api -p 8080:8080 devops-webapi:1.0.0
```

| Opção                  | Função                                         |
|------------------------|------------------------------------------------|
| `-d`                   | executa em background                          |
| `--name devops-api`    | nome do container                              |
| `-p 8080:8080`         | mapeia `porta-host:porta-container`            |
| `devops-webapi:1.0.0`  | imagem utilizada                               |

Inspeção e ciclo de vida:

```bash
docker ps
docker logs devops-api
docker logs -f devops-api
docker inspect devops-api
docker stop devops-api
docker ps -a
docker rm devops-api
docker images          # a imagem continua disponível
```

## Staging

```bash
docker run -d --name devops-api-staging -p 8081:8080 \
  -e APP_ENVIRONMENT=staging -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

Acesso: http://localhost:8081/

## Production

```bash
docker run -d --name devops-api-prod -p 8082:8080 \
  -e APP_ENVIRONMENT=production -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

Acesso: http://localhost:8082/

Mesma imagem, ambientes diferentes (*build once, deploy many*):

| Propriedade      | Staging              | Production           |
|------------------|----------------------|----------------------|
| Imagem           | `devops-webapi:1.0.0`| `devops-webapi:1.0.0`|
| Porta externa    | 8081                 | 8082                 |
| APP_ENVIRONMENT  | staging              | production           |
| APP_VERSION      | 1.0.0                | 1.0.0                |

## Health Check

Linux/macOS:

```bash
curl -f http://localhost:8081/health
```

Windows (PowerShell):

```powershell
curl.exe -f http://localhost:8081/health
```

Resposta esperada (HTTP 200):

```json
{"status":"healthy","version":"1.1.0","environment":"staging","timestamp":"..."}
```

O parâmetro `-f` faz o curl falhar em respostas HTTP de erro (como 500), o que permite usá-lo como quality gate.

## Nova versão (1.1.0)

Adicionado ao `Program.cs`, antes de `app.Run();`:

```csharp
app.MapGet("/version", () => new
{
    version,
    environment,
    message = "Nova funcionalidade da versão 1.1"
});
```

Build e deploy **primeiro em staging**:

```bash
docker build -t devops-webapi:1.1.0 .

docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 \
  -e APP_ENVIRONMENT=staging -e APP_VERSION=1.1.0 devops-webapi:1.1.0

curl.exe -f http://localhost:8081/health
```

Após a validação, promoção para production:

```bash
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 \
  -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0

curl.exe -f http://localhost:8082/health
```

## Falha controlada (1.2.0)

Para fins didáticos, o endpoint `/health` foi alterado para retornar erro 500:

```csharp
app.MapGet("/health", () =>
    Results.StatusCode(
        StatusCodes.Status500InternalServerError
    )
);
```

```bash
docker build -t devops-webapi:1.2.0 .

docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 \
  -e APP_ENVIRONMENT=staging -e APP_VERSION=1.2.0 devops-webapi:1.2.0

curl.exe -f http://localhost:8081/health
# curl: (22) The requested URL returned error: 500
```

O health check falhou em staging, então a versão **não deveria ser promovida**. Para demonstrar o rollback, a versão foi implantada em production (simulação de incidente):

```bash
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 \
  -e APP_ENVIRONMENT=production -e APP_VERSION=1.2.0 devops-webapi:1.2.0

curl.exe -f http://localhost:8082/health
# curl: (22) The requested URL returned error: 500
```

## Rollback

Retorno à última versão estável (1.1.0):

```bash
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 \
  -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0

curl.exe -f http://localhost:8082/health
```

O rollback só foi possível porque a imagem `1.1.0` continuou disponível e versionada por tag.

## Docker Compose

Antes de subir pelo Compose, os containers manuais foram removidos:

```bash
docker rm -f devops-api-staging devops-api-prod
```

`compose.yaml` (já com healthcheck):

```yaml
services:

  staging:
    image: devops-webapi:1.1.0
    container_name: devops-api-staging
    ports:
      - "8081:8080"
    environment:
      APP_ENVIRONMENT: staging
      APP_VERSION: "1.1.0"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3

  production:
    image: devops-webapi:1.1.0
    container_name: devops-api-prod
    ports:
      - "8082:8080"
    environment:
      APP_ENVIRONMENT: production
      APP_VERSION: "1.1.0"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3
```

Comandos:

```bash
docker compose up -d
docker compose ps
docker compose logs
docker compose logs -f
docker compose config
docker compose down
```

## Desafio avançado — Healthcheck no Compose

O enunciado alerta que o healthcheck com `curl` exige que o executável exista dentro da imagem.

**Problema encontrado:** a imagem `mcr.microsoft.com/dotnet/aspnet` não traz o `curl`. Com o healthcheck configurado no Compose, os containers ficaram `unhealthy`. O resultado de `docker inspect` mostrou a causa:

```
OCI runtime exec failed: exec failed: unable to start container process:
exec: "curl": executable file not found in $PATH
```

Ou seja, o Docker nem chegou a testar a API: o comando de teste não existia na imagem.

**Solução adotada:** instalar o `curl` no stage `runtime` do Dockerfile, mantendo apenas as dependências obrigatórias:

```dockerfile
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
```

| Trecho                          | Função                                                             |
|---------------------------------|--------------------------------------------------------------------|
| `apt-get update`                | baixa a lista de pacotes disponíveis                               |
| `apt-get install -y curl`       | instala o curl sem pedir confirmação                               |
| `--no-install-recommends`       | evita pacotes opcionais, deixando a imagem menor                   |
| `rm -rf /var/lib/apt/lists/*`   | remove a lista de pacotes para reduzir o tamanho final da imagem   |

A instalação fica no mesmo `RUN` para que a limpeza realmente reduza o tamanho da camada.

Depois de reconstruir a imagem `1.1.0` e recriar os containers, o `docker ps` passou a mostrar os dois ambientes como `(healthy)`. A imagem `1.1.0` ficou com 238 MB, contra 231 MB das imagens sem o curl.

**Alternativas consideradas** (sem alterar o Dockerfile): healthcheck com `bash` via `/dev/tcp`, ou implementar um argumento `--healthcheck` no próprio app .NET.

Comandos úteis para diagnóstico:

```bash
docker ps
docker inspect --format "{{json .State.Health}}" devops-api-staging
docker exec devops-api-staging curl -f http://localhost:8080/health
```

## Limpeza do laboratório

```bash
docker compose down
docker ps -a
docker images

# opcional
docker rmi devops-webapi:1.0.0
docker rmi devops-webapi:1.1.0
docker rmi devops-webapi:1.2.0
```

## Segurança

As variáveis `APP_ENVIRONMENT` e `APP_VERSION` são apenas configuração. Credenciais reais (senhas, tokens, certificados) nunca devem ficar no Dockerfile, no código ou em arquivos versionados.

## Evidências

As evidências em captura de tela de cada etapa (imagens 1.0.0 e 1.1.0, containers de staging e production, health check bem-sucedido, versão 1.2.0 com falha, rollback para 1.1.0, Compose e healthcheck) estão no relatório de entrega da atividade.
