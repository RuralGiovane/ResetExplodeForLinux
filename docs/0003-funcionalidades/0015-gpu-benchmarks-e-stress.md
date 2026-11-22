# GPU: benchmark, stress e ray tracing

**Estado Linux em 22/11/2026:** Pendente no Linux.

## Finalidade

Portar as cargas gráficas e medições presentes na referência.

## Comportamento atual

Windows possui GpuBurn, GpuGameBench e GpuRtBench com Direct3D. O plano cita Vulkan para Linux, mas não existe backend equivalente implementado neste checkout. NVML atual fornece telemetria, não renderização.

## Código e referência Windows

- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/GpuBurn.cs](../../Windows/GpuBurn.cs)
- [Windows/GpuGameBench.cs](../../Windows/GpuGameBench.cs)
- [Windows/GpuRtBench.cs](../../Windows/GpuRtBench.cs)

## Limites e diferenças

Escolha de bindings, compilação de shaders, sincronização, suporte a ray tracing e semântica da pontuação ainda precisam ser definidas. Não declarar equivalência de score antes de validar metodologia.

## Validação e continuidade

Critérios futuros: detecção de capacidades, GPU não suportada, cancelamento, liberação de recursos, proteção térmica GPU e séries reproduzíveis. Estes critérios são proposta para implementação, não testes já existentes.

[Roteiro relacionado](../../docs/VALIDACAO_BENCHMARKS.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
