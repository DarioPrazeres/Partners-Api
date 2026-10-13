# API de Clientes e Fornecedores — Teste técnico (Fase 2)

API ASP.NET Core (C#) com EF Core e SQL Server para gerir clientes e fornecedores de uma única empresa.
Uma entidade pode ser `CUSTOMER`, `SUPPLIER` ou `BOTH`, com um único registo por NIF.

## Versões usadas

| Componente | Versão |
|---|---|
| .NET SDK / runtime | **.NET 10** (LTS) — `net10.0` |
| EF Core + provider SQL Server | 10.0.0 |
| SQL Server | 2022 (imagem Docker, edição Developer — gratuita) |
| Testes | xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing 10.0.0, Testcontainers.MsSql 4.16.0 |

## Estrutura

```
src/Partners.Core            Regras: entidade, validação/normalização, serviço, contratos, interface do repositório (sem EF)
src/Partners.Infrastructure  Persistência: DbContext, mapeamento, repositório EF, migrações, dados de exemplo
src/Partners.Api             API: controller, formato de erros, arranque
tests/Partners.Tests         Testes unitários (regras) e de integração (API + SQL Server real)
http/partners.http           Exemplos de pedidos HTTP
```

## Preparação

1. Instalar o [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. SQL Server (uma das opções):
   - **Docker** (recomendado): `docker compose up -d` — expõe `localhost,1433`, utilizador `sa`, palavra-passe `Your_strong_Passw0rd` (só para desenvolvimento). A connection string por defeito em `src/Partners.Api/appsettings.json` já aponta para este servidor.
   - **SQL Server Developer/Express** instalado localmente: alterar a connection string `ConnectionStrings:DefaultConnection` em `src/Partners.Api/appsettings.json` (ou via variável de ambiente `ConnectionStrings__DefaultConnection`). Exemplo LocalDB/Windows: `Server=(localdb)\MSSQLLocalDB;Database=PartnersDb;Integrated Security=true;TrustServerCertificate=True`.
3. Restaurar e compilar:
   ```
   dotnet restore
   dotnet build
   ```

## Execução

```
dotnet run --project src/Partners.Api
```

Em ambiente **Development** (perfil por defeito em `launchSettings.json`) a aplicação **aplica as migrações e insere dados de exemplo** (8 registos, só se a tabela estiver vazia) ao arrancar. A API fica em `http://localhost:5080`.

Alternativa explícita, sem arranque automático:

```
dotnet tool install --global dotnet-ef        # uma vez
dotnet ef database update --project src/Partners.Infrastructure --startup-project src/Partners.Api
```

Em outros ambientes, `Database:MigrateOnStartup=true` e `Database:Seed=true` ativam o mesmo comportamento.

Exemplos de pedidos: `http/partners.http` (VS Code REST Client, Visual Studio, Rider).

## Testes

Os testes de integração correm contra **SQL Server real** (sem InMemory/SQLite). Para isso precisam de **Docker em execução** (por exemplo, Docker Desktop): ao arrancar, `dotnet test` sobe sozinho um contentor SQL Server 2022 (Testcontainers), que é removido no fim. **Não é necessário** correr `docker compose up` para os testes; o `docker-compose.yml` serve apenas para executar a API (ver Preparação). Na primeira execução a imagem do SQL Server é descarregada, por isso pode demorar alguns minutos.

```
dotnet test
```

Cada classe de testes cria uma base de dados própria (`PartnersTests_<guid>`), aplica as migrações e apaga-a no fim.

Sem Docker, defina `PARTNERS_TEST_CONNECTION` com uma connection string **sem** `Database`/`Initial Catalog`, por exemplo:

```
# PowerShell
$env:PARTNERS_TEST_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true"
# bash
export PARTNERS_TEST_CONNECTION="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True"
```

Os 7 cenários do enunciado estão cobertos em `tests/Partners.Tests`:
`PartnersApiTests` (1, 2, 3, 4, 6, 7) e `ConcurrencyTests` (5: 16 criações simultâneas do mesmo NIF em formatos diferentes → um 201 e 15 × 409; 10 atualizações simultâneas com a mesma `Version` → um 200 e 9 × 409; verificação final com contexto independente). `PartnerValidatorTests` cobre as regras sem base de dados.

## Contrato da API

Respostas e pedidos em JSON com nomes **PascalCase** (`Items`, `TotalCount`, `Version`...); a leitura de pedidos não distingue maiúsculas.

| Endpoint | Sucesso | Erros |
|---|---|---|
| `POST /api/partners` | 201 + `Location` + dados (incl. `Version`) | 400, 409 (NIF duplicado) |
| `GET /api/partners/{id}` | 200 (também inativas) | 400 (Id malformado), 404 |
| `GET /api/partners?Search&Type&IncludeInactive&Page&PageSize` | 200 `{ Items, TotalCount, Page, PageSize }` | 400 |
| `PUT /api/partners/{id}` | 200 + nova `Version` | 400, 404, 409 (NIF duplicado ou `Version` desatualizada) |

Todos os erros usam `application/problem+json` com `status`, `title`, `detail`, `code` (`validation_error`, `malformed_request`, `not_found`, `duplicate_tax_number`, `version_conflict`, `internal_error`), `traceId` e, quando aplicável, `errors: [{ field, message }]`. Nunca há stack traces.

`POST` e `PUT` exigem todos os campos (`CreditLimit` incluído); `Email` pode ser `null`. No `PUT`, `IsActive` e `Version` (Base64, no corpo) são obrigatórios. `Id`, `CreatedAt` e `UpdatedAt` enviados pelo cliente são ignorados.

## Decisões de desenho

- **Unicidade do NIF**: índice único `IX_Partners_TaxNumber` na base de dados (inclui inativas). A API insere/atualiza e traduz a violação (erros SQL 2601/2627) em 409; não há locks em memória nem "verificar antes de gravar". O NIF é guardado normalizado em `char(9)`, com `CHECK` de nove dígitos, preservando zeros à esquerda.
- **Concorrência otimista**: coluna `rowversion` exposta como `Version` (Base64 de 8 bytes). No `PUT`, a versão do cliente é definida como valor original do token e o EF gera `UPDATE ... WHERE Id = @id AND RowVersion = @versao`; a comparação é feita pelo SQL Server, e zero linhas afetadas → 409. `UpdatedAt` muda sempre, por isso o `UPDATE` (e a verificação) nunca é omitido, mesmo com dados idênticos.
- **Atomicidade**: cada atualização é um único `SaveChanges`/instrução; um pedido inválido ou em conflito não altera nada.
- **CreditLimit**: `decimal(18,2)` com `CHECK` 0–1 000 000; precisão superior a duas casas é rejeitada (400), nunca arredondada.
- **Listagem**: filtros, `COUNT` e paginação executados no SQL Server; `COUNT` antes de `Skip/Take`; ordenação `Name, Id`. `Search` usa `LIKE` com escape de `% _ [ \`; o ramo do NIF só existe se o termo normalizado não for vazio. O `Name` usa a colação explícita `Latin1_General_100_CI_AS` (insensível a maiúsculas, independente da base de dados).
- **Datas**: `CreatedAt`/`UpdatedAt` em UTC, definidas pelo servidor (`TimeProvider`).
- **Ordem por Id**: o SQL Server ordena `uniqueidentifier` de forma diferente da comparação de `Guid` em .NET; a ordem de desempate é a da base de dados.

## Notas

- As migrações em `src/Partners.Infrastructure/Migrations` foram geradas com o EF Core. Para as regenerar, apague a pasta `Migrations` e execute:
  ```
  dotnet ef migrations add InitialCreate --project src/Partners.Infrastructure --startup-project src/Partners.Api
  ```
- Fora do âmbito, conforme o enunciado: frontend, autenticação, gestão de utilizadores e integrações externas.