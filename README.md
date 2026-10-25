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
- Esses comandos rodam na **máquina host**. O healthcheck que roda dentro do container é configurado no Docker Compose (veja a última seção).

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

Declara staging e production em um arquivo `compose.yaml`, no lugar de vários `docker run`. Ambos os serviços usam a imagem `devops-webapi:1.1.0` e têm um `healthcheck`.

```bash
docker compose up -d     # sobe os ambientes em segundo plano
docker compose ps        # lista os serviços e o estado de saúde
docker compose down      # encerra e remove containers e rede
```

### Desafio: healthcheck no Compose

O exemplo do enunciado usa o `curl` dentro do container, mas a imagem base `aspnet` **não possui o curl**. Sem ele, o healthcheck falhava com `executable file not found in $PATH` e o container ficava `unhealthy`.

**Solução adotada:** o healthcheck foi implementado no próprio aplicativo. Ao ser executado com o argumento `--healthcheck`, o programa **não inicia a API**: ele faz uma requisição HTTP para `localhost:8080/health` e encerra com código `0` (saudável) ou `1` (falha).

| Como é executado                          | O que acontece                                          |
|-------------------------------------------|---------------------------------------------------------|
| `dotnet DevOpsWebApi.dll`                 | sobe a API normalmente (`ENTRYPOINT` do Dockerfile)     |
| `dotnet DevOpsWebApi.dll --healthcheck`   | consulta o `/health` e sai com código 0 ou 1            |

**1. Program.cs** — bloco no topo do arquivo, antes do `WebApplication.CreateBuilder`:

```csharp
if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var response = await client.GetAsync("http://localhost:8080/health");
        Environment.Exit(response.IsSuccessStatusCode ? 0 : 1);
    }
    catch
    {
        Environment.Exit(1);
    }
}
```

O bloco precisa vir **antes** de criar o servidor. Se viesse depois, o processo do healthcheck tentaria subir outra API na porta 8080, que já está em uso.

**2. compose.yaml** — o `healthcheck` fica no mesmo nível de `image`, `ports` e `environment`, em cada serviço:

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
      test: ["CMD", "dotnet", "DevOpsWebApi.dll", "--healthcheck"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 15s

  production:
    image: devops-webapi:1.1.0
    container_name: devops-api-prod
    ports:
      - "8082:8080"
    environment:
      APP_ENVIRONMENT: production
      APP_VERSION: "1.1.0"
    healthcheck:
      test: ["CMD", "dotnet", "DevOpsWebApi.dll", "--healthcheck"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 15s
```

| Campo          | Função                                                          |
|----------------|-----------------------------------------------------------------|
| `interval`     | de quanto em quanto tempo o teste roda                          |
| `timeout`      | tempo máximo para o teste responder                             |
| `retries`      | falhas seguidas até marcar o container como `unhealthy`         |
| `start_period` | tempo de carência para a API iniciar antes de contar falhas     |

**3. Dockerfile** — não precisa de nenhuma ferramenta extra, então permanece sem instalar o curl.

**Resultado:** após reconstruir a imagem (`docker build -t devops-webapi:1.1.0 .`) e subir com `docker compose up -d`, os dois containers passam para `healthy`:

```bash
docker ps
```

Teste manual do healthcheck (deve imprimir `0`):

```powershell
docker exec devops-api-staging dotnet DevOpsWebApi.dll --healthcheck
echo $LASTEXITCODE
```

**Alternativa considerada:** instalar o `curl` no estágio `runtime` do Dockerfile. É mais simples, mas adiciona uma ferramenta à imagem final. O problema é conhecido e as mesmas duas alternativas aparecem em projetos reais, como na [issue #469 do Lumenqraph](https://github.com/Lumen-Scribe/Lumenqraph/issues/469).
