# Otimização do sistema

**Estado Linux em 22/11/2026:** Pendente, além da limpeza local do app.

## Finalidade

Portar as ferramentas Windows de otimização para comportamentos apropriados ao Linux.

## Comportamento atual

Windows inclui gerenciamento de processos e ajustes do sistema. O Linux atual oferece somente limpeza de memória gerenciada do próprio app. GameMode, governors e outros ajustes aparecem como possibilidades do plano, não recursos concluídos.

## Código e referência Windows

- [Linux/Core/Benchmarks/BenchmarkMemoryCleanup.cs](../../Linux/Core/Benchmarks/BenchmarkMemoryCleanup.cs)
- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/Optimizer.cs](../../Windows/Optimizer.cs)
- [Windows/WindowsTweaks.cs](../../Windows/WindowsTweaks.cs)

## Limites e diferenças

Não executar drop_caches, alterar swappiness/governor ou encerrar programas como consequência da limpeza local. Cada alteração de sistema exige escopo, observação do valor original e restauração definidos.

## Validação e continuidade

Critérios futuros: suporte/permissões, efeito medido, restauração e falha parcial. Não transformar uma sugestão do plano em política de ajuste automática.

[Roteiro relacionado](../../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
