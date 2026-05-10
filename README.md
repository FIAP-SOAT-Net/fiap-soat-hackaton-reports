# Report Service (Serviço de Relatórios)

Microsserviço independente responsável por transformar resultados de análise arquitetural (vindos de outro serviço) em relatórios técnicos persistidos, consultáveis e exportáveis.

## Arquitetura
- `src/ReportService.Api`: API REST, Swagger, health check, tratamento de erro.
- `src/ReportService.Application`: casos de uso, validações, contratos e abstrações.
- `src/ReportService.Domain`: entidades e enums de negócio.
- `src/ReportService.Infrastructure`: persistência EF Core (SQLite no MVP).
- `tests/ReportService.Tests`: testes unitários iniciais com xUnit.

## Responsabilidades
- Criar relatório a partir de análise processada.
- Consultar por ID e por `analysisProcessId`.
- Listar com filtros.
- Exportar em Markdown e JSON.
- Atualizar status (`Pending`, `Generated`, `Failed`).
- Preparar consumo assíncrono via `IAnalysisCompletedConsumer`.

## Endpoints
- `POST /api/reports`
- `GET /api/reports/{id}`
- `GET /api/reports/by-analysis/{analysisProcessId}`
- `GET /api/reports?status=&analysisProcessId=&createdAtFrom=&createdAtTo=`
- `GET /api/reports/{id}/export/markdown`
- `GET /api/reports/{id}/export/json`
- `PATCH /api/reports/{id}/status`
- `GET /health`

## Validações e segurança
- IDs obrigatórios, `sourceFileName` obrigatório.
- Pelo menos uma lista preenchida: componentes/riscos/recomendações.
- Severity permitida: `Low|Medium|High|Critical`.
- Campos textuais: sem vazio, sem HTML/script, limite de 500 chars.
- Não expõe stack trace na resposta.
- O serviço confia em dados validados do serviço de processamento/IA, mas revalida no boundary HTTP.

## Executar localmente
```bash
dotnet restore ReportService.sln
dotnet run --project src/ReportService.Api
```
Swagger: `http://localhost:5000/swagger` (ou porta configurada).

## Docker
```bash
docker compose up --build
```
API: `http://localhost:8080/swagger`

## Testes
```bash
dotnet test ReportService.sln
```

## CI
Workflow em `.github/workflows/ci.yml` com restore, build e test.

## Limitações atuais
- Sem broker real (RabbitMQ/Kafka) no MVP; apenas abstração e implementação inicial.
- SQLite local por simplicidade; PostgreSQL recomendado para ambiente compartilhado.
- Migrações EF podem ser adicionadas conforme evolução.

## Próximos passos
- Adicionar autenticação/autorização no gateway e trust boundary.
- Implementar consumer com broker real.
- Expandir testes de integração de API e contratos entre serviços.
