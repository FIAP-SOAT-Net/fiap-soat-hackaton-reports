# Teste E2E do Report Service — PowerShell
# Pré-requisito: curl disponível no PATH (já incluso no Windows 10+)
# PDF opcional: pandoc (https://pandoc.org/installing.html)
#
# Uso:
#   .\run-e2e.ps1
#   .\run-e2e.ps1 -BaseUrl http://localhost:8080

param([string]$BaseUrl = "http://localhost:8080")

$Pass = 0
$Fail = 0

function Ok($msg)   { Write-Host "[OK]  $msg" -ForegroundColor Green; $script:Pass++ }
function Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red;  $script:Fail++ }

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Report Service — Teste E2E"              -ForegroundColor Cyan
Write-Host " Base URL: $BaseUrl"                      -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# ── 1. Health check ───────────────────────────────────────────────────────────
Write-Host "`n► 1. Health check"
try {
    $r = Invoke-WebRequest "$BaseUrl/health" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Ok "GET /health → 200" } else { Fail "GET /health → $($r.StatusCode)" }
} catch { Fail "GET /health falhou: $_" }

# ── 2. Criar relatório ────────────────────────────────────────────────────────
Write-Host "`n► 2. Criar relatório (POST /api/reports)"
$AnalysisId = [Guid]::NewGuid().ToString()
$Payload = @{
    analysisProcessId = $AnalysisId
    sourceFileName    = "diagrama-arquitetura.png"
    components        = @(
        @{ name = "API Gateway";        type = "Gateway";      description = "Ponto de entrada unico para requisicoes externas" }
        @{ name = "Servico de Pedidos"; type = "Microservice"; description = "Gerencia o ciclo de vida dos pedidos" }
    )
    risks = @(
        @{ title = "Ponto unico de falha";       severity = "High";   description = "O API Gateway nao possui redundancia"; recommendation = "Adicionar replicas com load balancer" }
        @{ title = "Ausencia de circuit breaker"; severity = "Medium"; description = "Falhas em cascata possiveis";           recommendation = "Implementar Polly ou similar" }
    )
    recommendations = @(
        @{ title = "Observabilidade"; description = "Adicionar tracing distribuido com OpenTelemetry" }
    )
    aiModelInfo = @{ provider = "OpenAI"; model = "gpt-4o"; promptVersion = "v1.0"; confidence = 0.88 }
} | ConvertTo-Json -Depth 5

try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports" -Method POST `
        -ContentType "application/json" `
        -Headers @{ "X-Correlation-Id" = "e2e-test-$(Get-Date -Format 'yyyyMMddHHmmss')" } `
        -Body $Payload -UseBasicParsing
    if ($r.StatusCode -eq 201) { Ok "POST /api/reports → 201 Created" } else { Fail "POST → $($r.StatusCode)" }
    $ReportId = ($r.Content | ConvertFrom-Json).id
    Write-Host "   Report ID: $ReportId"
} catch { Fail "POST /api/reports falhou: $_"; exit 1 }

# ── 3. Duplicata ──────────────────────────────────────────────────────────────
Write-Host "`n► 3. Duplicata (mesmo analysisProcessId deve retornar 409)"
try {
    Invoke-WebRequest "$BaseUrl/api/reports" -Method POST `
        -ContentType "application/json" `
        -Body ($Payload) -UseBasicParsing | Out-Null
    Fail "POST duplicado → deveria ter retornado 409"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code -eq 409) { Ok "POST duplicado → 409 Conflict" } else { Fail "POST duplicado → $code (esperado 409)" }
}

# ── 4. Payload inválido ───────────────────────────────────────────────────────
Write-Host "`n► 4. Payload inválido (severity errada deve retornar 400)"
$BadPayload = @{
    analysisProcessId = [Guid]::NewGuid().ToString()
    sourceFileName    = "test.png"
    components        = @()
    risks             = @(@{ title = "T"; severity = "INVALIDA"; description = "D"; recommendation = "R" })
    recommendations   = @()
} | ConvertTo-Json -Depth 3

try {
    Invoke-WebRequest "$BaseUrl/api/reports" -Method POST `
        -ContentType "application/json" -Body $BadPayload -UseBasicParsing | Out-Null
    Fail "POST inválido → deveria ter retornado 400"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code -eq 400) { Ok "POST inválido → 400 Bad Request" } else { Fail "POST inválido → $code (esperado 400)" }
}

# ── 5. Buscar por ID ──────────────────────────────────────────────────────────
Write-Host "`n► 5. Buscar por ID (GET /api/reports/{id})"
try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports/$ReportId" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Ok "GET /api/reports/$ReportId → 200" } else { Fail "GET por ID → $($r.StatusCode)" }
} catch { Fail "GET por ID falhou: $_" }

# ── 6. Buscar por analysisProcessId ──────────────────────────────────────────
Write-Host "`n► 6. Buscar por analysisProcessId"
try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports/by-analysis/$AnalysisId" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Ok "GET by-analysis → 200" } else { Fail "GET by-analysis → $($r.StatusCode)" }
} catch { Fail "GET by-analysis falhou: $_" }

# ── 7. Listar relatórios ──────────────────────────────────────────────────────
Write-Host "`n► 7. Listar relatórios (GET /api/reports)"
try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Ok "GET /api/reports → 200" } else { Fail "GET lista → $($r.StatusCode)" }
} catch { Fail "GET lista falhou: $_" }

# ── 8. Atualizar status ───────────────────────────────────────────────────────
Write-Host "`n► 8. Atualizar status (PATCH /api/reports/{id}/status)"
try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports/$ReportId/status" -Method PATCH `
        -ContentType "application/json" -Body '{"status":"Generated"}' -UseBasicParsing
    if ($r.StatusCode -eq 200) { Ok "PATCH status → 200" } else { Fail "PATCH status → $($r.StatusCode)" }
} catch { Fail "PATCH status falhou: $_" }

# ── 9. Export JSON ────────────────────────────────────────────────────────────
Write-Host "`n► 9. Exportar como JSON"
try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports/$ReportId/export/json" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Ok "GET export/json → 200" } else { Fail "GET export/json → $($r.StatusCode)" }
} catch { Fail "GET export/json falhou: $_" }

# ── 10. Export Markdown e gerar PDF ──────────────────────────────────────────
Write-Host "`n► 10. Exportar Markdown e gerar PDF"
try {
    $r = Invoke-WebRequest "$BaseUrl/api/reports/$ReportId/export/markdown" -UseBasicParsing
    $MdFile  = "relatorio-$ReportId.md"
    $PdfFile = "relatorio-$ReportId.pdf"

    $r.Content | Out-File -FilePath $MdFile -Encoding utf8
    Ok "Markdown salvo em $MdFile"

    if (Get-Command pandoc -ErrorAction SilentlyContinue) {
        pandoc $MdFile -o $PdfFile 2>$null
        if (Test-Path $PdfFile) { Ok "PDF gerado em $PdfFile" } else { Fail "pandoc falhou ao gerar PDF" }
    } else {
        Write-Host "   ℹ️  pandoc não encontrado. Para gerar o PDF:" -ForegroundColor Yellow
        Write-Host "       1. Instale: https://pandoc.org/installing.html" -ForegroundColor Yellow
        Write-Host "       2. Execute: pandoc $MdFile -o $PdfFile" -ForegroundColor Yellow
    }
} catch { Fail "Export Markdown falhou: $_" }

# ── Resultado ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Resultado: $Pass OK  |  $Fail FALHAS"   -ForegroundColor $(if ($Fail -eq 0) { "Green" } else { "Red" })
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

exit $Fail
