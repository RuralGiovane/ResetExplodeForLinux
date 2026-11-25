# Stress de CPU e RAM

**Estado Linux em 22/11/2026:** Implementado; aceite manual do usuário registrado em 22/11/2026.

## Finalidade

Aplicar carga por prazo definido e verificar padrões escritos na RAM.

## Comportamento atual

Em Benchmark → CPU ou Memória RAM, iniciar o stress pelo card do componente. Cada card tem duração de 1–30 minutos e limite CPU de 60–100 °C independentes; o card RAM permite 16–1024 MiB. A interface executa CPU e RAM separadamente. Padrões: 5 minutos, 256 MiB e 90 °C. A proteção térmica é obrigatória e independente da configuração dos benchmarks. Workers dedicados encerram por prazo, cancelamento ou erro; uma divergência RAM interrompe a carga.

## Código e referência Windows

- [Linux/Core/Benchmarks/StressTest.cs](../../Linux/Core/Benchmarks/StressTest.cs)
- [Linux/Views/MainWindow.Stress.cs](../../Linux/Views/MainWindow.Stress.cs)
- [Linux/SmokeTests/Stability/StressChecks.cs](../../Linux/SmokeTests/Stability/StressChecks.cs)

Referência Windows:

- [Windows/StressTester.cs](../../Windows/StressTester.cs)

## Limites e diferenças

Pré-checagem limita RAM a um quarto do menor valor observado entre MemAvailable e orçamento estimado do runtime; não reserva RAM nem garante proteção contra OOM. Prazo inclui alocação. Zero passadas é inconclusivo; sem divergência não prova estabilidade. Não há carga GPU nem temperatura DIMM.

## Validação e continuidade

Suíte completa passou após implementação. Testes pequenos incluem corrupção, caudas, prazo, cancelamento e watchdog simulado. Histórico/exportação ficam em Benchmark; EDAC/journal continuam manuais. Usuário confirmou as validações de stress/estabilidade em 22/11/2026; não informou medições individuais.

[Roteiro relacionado](../../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)

## Organização por componente — 23/11/2026

Benchmark usa sub-abas Visão geral, CPU, Memória RAM, GPU, Disco e Configurações. Progresso e cancelamento permanecem acima das abas. Cada componente mostra seu histórico e um expansor com todos os testes; exportações e limpeza ficam em Visão geral. Configurações contém a proteção opcional dos benchmarks; os stresses mantêm seus limites obrigatórios. Novos registros usam `cpu-stress` e `ram-stress`; registros legados `cpu-ram-stress` aparecem em ambos os componentes, identificados pelo nome original. EDAC, logs e comparação de estabilidade permanecem em Sistema e sensores.
