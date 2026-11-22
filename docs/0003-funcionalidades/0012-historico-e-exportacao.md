# Histórico e exportação

**Estado Linux em 22/11/2026:** Implementado.

## Finalidade

Preservar execuções e compartilhar resultados com contexto.

## Comportamento atual

Histórico versionado mantém as últimas 200 execuções, configurações térmicas e parâmetros. A UI separa texto da sessão, histórico salvo e pendências de gravação. Exportação HTML/JSON usa snapshots por domínio; benchmark inclui stress CPU/RAM.

## Código e referência Windows

- [Linux/Core/Persistence/LocalPersistence.cs](../../Linux/Core/Persistence/LocalPersistence.cs)
- [Linux/Views/MainWindow.Persistence.cs](../../Linux/Views/MainWindow.Persistence.cs)
- [Linux/Views/MainWindow.Export.cs](../../Linux/Views/MainWindow.Export.cs)
- [Linux/Core/Reports/BenchmarkReportBuilder.cs](../../Linux/Core/Reports/BenchmarkReportBuilder.cs)

Referência Windows:

- [Windows/AppSettings.cs](../../Windows/AppSettings.cs)
- [Windows/ReportBuilder.cs](../../Windows/ReportBuilder.cs)

## Limites e diferenças

Não existe relatório único idêntico ao Windows nem perfis/credenciais de jogos implementados. O relatório de estabilidade não incorpora automaticamente o histórico de stress. Exportado não significa salvo no histórico local.

## Validação e continuidade

Testes verificam recuperação, versões desconhecidas, retenção, escaping, datas e cópias. Os contratos de persistência e relatórios ficam em 0004-contratos.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
