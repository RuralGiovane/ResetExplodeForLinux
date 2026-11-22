# Benchmarks de CPU e RAM

**Estado Linux em 22/11/2026:** Implementado; equivalência de medição pendente.

## Finalidade

Medir renderização, simulação CPU e comportamento de acesso à RAM.

## Comportamento atual

Render usa fontes compartilhadas, cena 960×540 e 1024 amostras/pixel, com uma ou todas as threads. Simulação usa 30 mil entidades, 2 s de aquecimento e 10 s de medição. Latência e banda RAM têm metodologia e parâmetros registrados. Cancelamento descarta resultado parcial.

## Código e referência Windows

- [Shared/Benchmarks/BenchRenderer.cs](../../Shared/Benchmarks/BenchRenderer.cs)
- [Shared/Benchmarks/GameSimBench.cs](../../Shared/Benchmarks/GameSimBench.cs)
- [Linux/Core/Benchmarks/MemoryBench.cs](../../Linux/Core/Benchmarks/MemoryBench.cs)
- [Linux/Core/Benchmarks/BenchmarkMethodology.cs](../../Linux/Core/Benchmarks/BenchmarkMethodology.cs)

Referência Windows:

- [Windows/MemoryBench.cs](../../Windows/MemoryBench.cs)

## Limites e diferenças

Banda usa cerca de 2 GiB; latência cerca de 272 MiB. Testes curtos não validam cargas completas. FPS de simulação não é FPS renderizado de um jogo. Correções Linux de partições/caudas RAM tornam necessária avaliação explícita entre plataformas.

## Validação e continuidade

Suíte cobre algoritmos reduzidos, cancelamento e exclusão. Repetibilidade e comparação Windows/Linux precisam de séries de medições.

[Roteiro relacionado](../../docs/VALIDACAO_BENCHMARKS.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
