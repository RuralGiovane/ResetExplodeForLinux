# 0003 — Stress limitado com proteção térmica obrigatória

Status: implementada em 22/11/2026 e registrada no commit 83f5d11; aceite manual pendente.

## Contexto

O usuário autorizou avançar nas lacunas do porte. Windows/StressTester já fornece carga CPU e padrões RAM; Linux tinha benchmarks, watchdog térmico, histórico e diagnóstico EDAC/journal.

## Escolha e evidência

Adicionar backend Linux derivado desses algoritmos e reutilizar BenchmarkRunner e histórico. A UI oferece 1–30 minutos, RAM explícita de até 1024 MiB, pré-checagem conservadora e monitoramento obrigatório da CPU. Todos os workers são aguardados antes de encerrar. [Implementação](../../Linux/Core/Benchmarks/StressTest.cs) e [integração](../../Linux/Views/MainWindow.Stress.cs).

## Por que

Reaproveitar o coordenador conserva exclusão e estados de cancelamento já usados pelos benchmarks. Prazo e quantidade de RAM explícitos tornam a carga escolhida visível ao usuário. Proteção obrigatória evita iniciar essa nova carga sem leitura térmica recente.

## Consequências e diferenças

Não replica a fração de RAM livre e a execução contínua da referência Windows. A memória observada pode mudar; a checagem não protege contra todo OOM externo ou limite compartilhado. O prazo inclui alocação. Não há stress GPU, score ou garantia de estabilidade.

EDAC/journal continuam consultas manuais, evitando atribuir um evento de sistema à carga sem evidência causal.

## Revisão

Reavaliar limites após validação real e demanda de paridade. Uma futura mudança deve documentar duração, alocação, cgroups, sensores, encerramento e classificação de resultados. Não retirar watchdog ou espera dos workers sem substituir suas garantias.
