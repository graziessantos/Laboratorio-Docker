# 🐳 Laboratório Docker

Laboratório da disciplina **Integração DevOps**. A atividade empacota uma Web API em **ASP.NET Core (.NET 10)** como uma **imagem Docker versionada** e percorre o ciclo completo de entrega:

```
Código → Imagem → Container → Staging → Validação → Production → Falha → Rollback
```

A API expõe três endpoints e lê sua configuração de variáveis de ambiente:

| Endpoint   | Retorna                                        |
|------------|------------------------------------------------|
| `/`        | nome da aplicação, versão e ambiente           |
| `/health`  | status de saúde, versão, ambiente e horário    |
| `/version` | versão, ambiente e mensagem (a partir da 1.1.0)|

| Variável          | Função                                | Valor padrão  |
|-------------------|---------------------------------------|---------------|
| `APP_ENVIRONMENT` | ambiente em que a API está executando | `development` |
| `APP_VERSION`     | versão implantada                     | `local`       |

> 📎 Os prints de cada etapa estão no **documento de evidências** entregue junto com este repositório.

---

## 🔨 Construção

Constrói a imagem `devops-webapi` com a tag `1.0.0`, a partir do `Dockerfile` da pasta atual.

```bash
docker build -t devops-webapi:1.0.0 .
```

- `-t devops-webapi:1.0.0` define o nome e a tag (versão) da imagem.
- O `.` indica que o contexto do build é a pasta atual.
- O Dockerfile usa **multi-stage build**: um estágio com o SDK compila e publica a aplicação, e o estágio final (somente runtime ASP.NET) recebe apenas o resultado publicado. Isso deixa a imagem menor.
- O arquivo `.dockerignore` evita enviar `bin/`, `obj/`, `.git/` e outros arquivos desnecessários ao build.

Para conferir: `docker images`

---

## ▶️ Execução

Cria e executa o primeiro container a partir da imagem.

```bash
docker run -d --name devops-api -p 8080:8080 devops-webapi:1.0.0
```

| Opção                  | Significado                                              |
|------------------------|----------------------------------------------------------|
| `-d`                   | executa em segundo plano                                 |
| `--name devops-api`    | dá um nome ao container                                  |
| `-p 8080:8080`         | mapeia `porta-do-host:porta-do-container`                |

Acesse `http://localhost:8080/` para ver a resposta da API. Comandos úteis para inspecionar: `docker ps`, `docker logs devops-api` e `docker inspect devops-api`.

---

## 🧪 Staging

Cria o ambiente de **staging** com a mesma imagem, mudando só a configuração e a porta externa (`8081`).

```bash
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

- `-e` passa variáveis de ambiente para o container.
- Resultado esperado em `http://localhost:8081/`: `"environment": "staging"` e `"version": "1.0.0"`.

---

## 🚀 Production

Cria o ambiente de **production** a partir da **mesma imagem**, na porta `8082`.

```bash
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

| Propriedade     | Staging              | Production           |
|-----------------|----------------------|----------------------|
| Imagem          | `devops-webapi:1.0.0`| `devops-webapi:1.0.0`|
| Porta externa   | 8081                 | 8082                 |
| APP_ENVIRONMENT | staging              | production           |

Conceito aplicado: **build once, deploy many**. A imagem é construída uma vez e reutilizada em vários ambientes.

---

## ❤️ Health Check

Valida se a aplicação está saudável em cada ambiente.

```bash
curl.exe -f http://localhost:8081/health
curl.exe -f http://localhost:8082/health
```

- O `-f` faz o curl **falhar** (código de saída diferente de zero) quando a resposta HTTP é de erro, o que o torna um *quality gate*: se falhar, a release **não deve ser promovida**.
- Resultado esperado: `{"status":"healthy","version":"...","environment":"..."}`.
- No Linux/macOS, use `curl` no lugar de `curl.exe`.

---

## 🏷️ Nova versão

Após adicionar o endpoint `/version` ao `Program.cs`, gera-se uma nova imagem com a tag `1.1.0`. A `1.0.0` continua disponível.

```bash
docker build -t devops-webapi:1.1.0 .
```

Fluxo de implantação seguido:

1. **Staging primeiro:** recria-se o container de staging com a `1.1.0` e roda-se o health check.
2. **Promoção:** com a validação aprovada, recria-se o container de production com a `1.1.0`.

```bash
docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.1.0 devops-webapi:1.1.0
```

---

## 💥 Falha controlada

A versão `1.2.0` foi criada **de propósito** com o endpoint `/health` retornando erro 500, para simular uma release defeituosa (uso exclusivamente didático).

```bash
docker build -t devops-webapi:1.2.0 .
```

Ao implantar a `1.2.0` em staging, o health check falha:

```
curl: (22) The requested URL returned error: 500
```

Em um fluxo real, essa falha **bloquearia a promoção** para production. Para demonstrar o rollback, a `1.2.0` foi colocada em production mesmo assim, simulando um incidente.

---

## ⏪ Rollback

Volta o ambiente de production para a última versão conhecida como funcional (`1.1.0`).

```bash
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0
```

Depois, `curl.exe -f http://localhost:8082/health` volta a responder `healthy`. O rollback só é possível porque as imagens são **versionadas** e a `1.1.0` continua disponível.

---

## 🧩 Docker Compose

Declara staging e production em um arquivo `compose.yaml`, no lugar de vários `docker run`. Ambos os serviços usam a imagem `devops-webapi:1.1.0` e têm um `healthcheck` que chama `/health`.

```bash
docker compose up -d     # sobe os ambientes em segundo plano
docker compose ps        # lista os serviços e o estado de saúde
docker compose down      # encerra e remove containers e rede
```

**Atenção ao healthcheck:** ele usa o `curl`, que **não vem** na imagem base `aspnet`. Por isso o `Dockerfile` instala o curl no estágio `runtime`:

```dockerfile
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
```

Com isso, os containers passam de `starting` para `healthy` em poucos segundos (confira com `docker ps`).
