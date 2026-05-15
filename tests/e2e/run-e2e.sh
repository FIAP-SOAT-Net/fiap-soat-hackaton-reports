#!/usr/bin/env bash
# Teste E2E do Report Service
# Pré-requisitos: curl, jq
# PDF opcional: pandoc (brew install pandoc / apt install pandoc)
#
# Uso:
#   chmod +x run-e2e.sh
#   ./run-e2e.sh [BASE_URL]
#   ./run-e2e.sh http://localhost:8080

set -e

BASE_URL="${1:-http://localhost:8080}"
PASS=0
FAIL=0

GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'

ok()   { echo -e "${GREEN}[OK]${NC} $1"; ((PASS++)); }
fail() { echo -e "${RED}[FAIL]${NC} $1"; ((FAIL++)); }

echo ""
echo "========================================="
echo " Report Service — Teste E2E"
echo " Base URL: $BASE_URL"
echo "========================================="

# ── 1. Health check ───────────────────────────────────────────────────────────
echo ""
echo "► 1. Health check"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/health")
[ "$STATUS" = "200" ] && ok "GET /health → 200" || fail "GET /health → $STATUS (esperado 200)"

# ── 2. Criar relatório ────────────────────────────────────────────────────────
echo ""
echo "► 2. Criar relatório (POST /api/reports)"
ANALYSIS_ID=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen | tr '[:upper:]' '[:lower:]')

RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/reports" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: e2e-test-$(date +%s)" \
  -d "{
    \"analysisProcessId\": \"$ANALYSIS_ID\",
    \"sourceFileName\": \"diagrama-arquitetura.png\",
    \"components\": [
      { \"name\": \"API Gateway\", \"type\": \"Gateway\", \"description\": \"Ponto de entrada unico para requisicoes externas\" },
      { \"name\": \"Servico de Pedidos\", \"type\": \"Microservice\", \"description\": \"Gerencia o ciclo de vida dos pedidos\" }
    ],
    \"risks\": [
      { \"title\": \"Ponto unico de falha\", \"severity\": \"High\", \"description\": \"O API Gateway nao possui redundancia\", \"recommendation\": \"Adicionar replicas com load balancer\" },
      { \"title\": \"Ausencia de circuit breaker\", \"severity\": \"Medium\", \"description\": \"Falhas em cascata possiveis\", \"recommendation\": \"Implementar Polly ou similar\" }
    ],
    \"recommendations\": [
      { \"title\": \"Observabilidade\", \"description\": \"Adicionar tracing distribuido com OpenTelemetry\" }
    ],
    \"aiModelInfo\": {
      \"provider\": \"OpenAI\",
      \"model\": \"gpt-4o\",
      \"promptVersion\": \"v1.0\",
      \"confidence\": 0.88
    }
  }")

HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY=$(echo "$RESPONSE" | head -n -1)

[ "$HTTP_CODE" = "201" ] && ok "POST /api/reports → 201 Created" || fail "POST /api/reports → $HTTP_CODE (esperado 201)"

REPORT_ID=$(echo "$BODY" | jq -r '.id')
echo "   Report ID: $REPORT_ID"

# ── 3. Duplicata deve retornar 409 ────────────────────────────────────────────
echo ""
echo "► 3. Duplicata (mesmo analysisProcessId deve retornar 409)"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/reports" \
  -H "Content-Type: application/json" \
  -d "{
    \"analysisProcessId\": \"$ANALYSIS_ID\",
    \"sourceFileName\": \"outro.png\",
    \"components\": [{ \"name\": \"X\", \"type\": \"Y\", \"description\": \"Z\" }],
    \"risks\": [],
    \"recommendations\": []
  }")
[ "$STATUS" = "409" ] && ok "POST duplicado → 409 Conflict" || fail "POST duplicado → $STATUS (esperado 409)"

# ── 4. Payload inválido deve retornar 400 ─────────────────────────────────────
echo ""
echo "► 4. Payload inválido (severity errada deve retornar 400)"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/reports" \
  -H "Content-Type: application/json" \
  -d "{
    \"analysisProcessId\": \"$(uuidgen | tr '[:upper:]' '[:lower:]' 2>/dev/null || cat /proc/sys/kernel/random/uuid)\",
    \"sourceFileName\": \"test.png\",
    \"components\": [],
    \"risks\": [{ \"title\": \"T\", \"severity\": \"INVALIDA\", \"description\": \"D\", \"recommendation\": \"R\" }],
    \"recommendations\": []
  }")
[ "$STATUS" = "400" ] && ok "POST inválido → 400 Bad Request" || fail "POST inválido → $STATUS (esperado 400)"

# ── 5. Buscar por ID ──────────────────────────────────────────────────────────
echo ""
echo "► 5. Buscar relatório por ID (GET /api/reports/{id})"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/reports/$REPORT_ID")
[ "$STATUS" = "200" ] && ok "GET /api/reports/$REPORT_ID → 200" || fail "GET por ID → $STATUS (esperado 200)"

# ── 6. Buscar por analysisProcessId ──────────────────────────────────────────
echo ""
echo "► 6. Buscar por analysisProcessId"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/reports/by-analysis/$ANALYSIS_ID")
[ "$STATUS" = "200" ] && ok "GET /api/reports/by-analysis/$ANALYSIS_ID → 200" || fail "GET by-analysis → $STATUS (esperado 200)"

# ── 7. Listar relatórios ──────────────────────────────────────────────────────
echo ""
echo "► 7. Listar relatórios (GET /api/reports)"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/reports")
[ "$STATUS" = "200" ] && ok "GET /api/reports → 200" || fail "GET lista → $STATUS (esperado 200)"

# ── 8. Atualizar status ───────────────────────────────────────────────────────
echo ""
echo "► 8. Atualizar status (PATCH /api/reports/{id}/status)"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$BASE_URL/api/reports/$REPORT_ID/status" \
  -H "Content-Type: application/json" \
  -d '{"status": "Generated"}')
[ "$STATUS" = "200" ] && ok "PATCH status → 200" || fail "PATCH status → $STATUS (esperado 200)"

# ── 9. Export JSON ────────────────────────────────────────────────────────────
echo ""
echo "► 9. Exportar como JSON"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/reports/$REPORT_ID/export/json")
[ "$STATUS" = "200" ] && ok "GET export/json → 200" || fail "GET export/json → $STATUS (esperado 200)"

# ── 10. Export Markdown e gerar PDF ──────────────────────────────────────────
echo ""
echo "► 10. Exportar como Markdown e gerar PDF"
MARKDOWN=$(curl -s "$BASE_URL/api/reports/$REPORT_ID/export/markdown")
MARKDOWN_FILE="relatorio-$REPORT_ID.md"
PDF_FILE="relatorio-$REPORT_ID.pdf"

echo "$MARKDOWN" > "$MARKDOWN_FILE"
ok "Markdown salvo em $MARKDOWN_FILE"

if command -v pandoc &>/dev/null; then
  pandoc "$MARKDOWN_FILE" -o "$PDF_FILE" --pdf-engine=xelatex 2>/dev/null \
    || pandoc "$MARKDOWN_FILE" -o "$PDF_FILE" 2>/dev/null \
    && ok "PDF gerado em $PDF_FILE" \
    || fail "pandoc encontrado mas falhou ao gerar PDF"
else
  echo "   ℹ️  pandoc não encontrado. Para gerar o PDF instale: brew install pandoc (Mac) / apt install pandoc (Linux)"
  echo "   ℹ️  Depois execute: pandoc $MARKDOWN_FILE -o $PDF_FILE"
fi

# ── Resultado final ───────────────────────────────────────────────────────────
echo ""
echo "========================================="
echo " Resultado: $PASS OK  |  $FAIL FALHAS"
echo "========================================="
echo ""

[ "$FAIL" -eq 0 ] && exit 0 || exit 1
