# 22/11/2026 — Backend gráfico Vulkan e proteção térmica por PCI

## Pedido e contexto

Usuário autorizou implementar o backend de renderização e associar a GPU selecionada ao sensor correto, após a entrega do inventário Vulkan.

## Entrega

Pipeline offscreen em VulkanGpuRenderer, GLSL/SPIR-V versionados, associação por PCI via NVML e GpuThermalGuard. Diagnósticos --gpu-devices e --gpu-render; testes de política térmica incluídos em --vulkan-only e na suíte completa. Dependência Silk.NET.Vulkan 2.22.0. Checkout sem commit nesta etapa.

## Validação

Builds Avalonia/SmokeTests Release sem avisos/erros; suíte completa aprovada, incluindo sensor incorreto, ausente, obsoleto, com erro, temperatura acima do limite, limites de parâmetros e pré-cancelamento. SPIR-V validado por spirv-val para Vulkan 1.1.

Enumeração nativa fora do sandbox encontrou Intel em 0000:00:02.0 e NVIDIA GTX 1650 em 0000:01:00.0. Execução real inicial de 2 s concluiu 329 frames e confirmou 109785 pixels não pretos no último frame. Ctrl+C durante outra execução retornou código 130, sem resultado parcial. A execução não busca nem comprova saturação máxima e esses números não são score de benchmark.

Após o cancelamento e o ajuste final de sincronização, nova execução de 1 s concluiu 161 frames e confirmou 107446 pixels não pretos, sem erro.

A leitura final usa barreiras color attachment → transferência → host e memória host-coherent. A verificação de pixels não substitui validação visual da cena. Não foram provocados aquecimento até o limite, device loss ou travamento do driver; testes de política térmica são simulados.

## Próximas etapas

Integrar controles/preview à aplicação, coordenação com outros testes, histórico/exportação e sensores adicionais. Ver [decisão do backend](../0006-decisoes/0004-backend-vulkan.md) e [roteiro GPU](../VALIDACAO_GPU.md).
