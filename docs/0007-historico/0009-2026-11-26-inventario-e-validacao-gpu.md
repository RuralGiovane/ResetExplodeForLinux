# 0009 — 26/11/2026 — Inventário e validação GPU

## Pedido e mudanças

Avançar nas etapas de validação GPU e ampliação das especificações/cobertura. Inventário somente leitura de controladores PCI, com enriquecimento NVML por PCI e modelo/tipo/API Vulkan na interface. Dados ausentes continuam explícitos. A tela existente ganhou detalhes expansíveis, sem substituir seleção nem proteção térmica.

LinuxGpuInventory e ReadSpecifications do NVML coletam identidade, driver, revisão, VBIOS, PCIe e memória. Corrigida a resolução do link do driver relativo ao dispositivo PCI físico. SmokeTests ganhou fixtures e `--gpu-inventory`. UiChecks ganhou `--gpu-lifecycle <PCI> <diretório>` opt-in, com carga real, histórico temporário e relatórios, além de expandir as especificações na regressão gráfica.

## Validação

- Build Release Avalonia/UiChecks sem avisos/erros e suíte net8.0 completa aprovados.
- Inventário real Intel/i915 e NVIDIA GTX 1650; dedicada reporta 4 GiB.
- Carga CLI solicitada de 300 s na GTX atingiu 80 °C e foi interrompida (código 3). Evidência de proteção térmica, não de conclusão prolongada.
- Fechamento sob carga/prévia, reabertura e novo cancelamento aprovados pelo harness Avalonia. Histórico e HTML/JSON preservam dois cancelamentos sem métricas/score parciais.
- 14 abas × quatro tamanhos aprovados com inventário expandido; captura de GPU em 820×620 revisada. Compositor impôs 945×993 no teste de ciclo de vida.

## Pendências

Diálogo nativo de salvar, teclado/dropdown no tamanho nativo mínimo, carga prolongada concluída, camada Khronos e hardware AMD/Xe. Intel/i915 desta máquina não oferece temperatura própria, portanto não foi submetida a stress. Especificações comerciais adicionais (núcleos, litografia, barramento/tipo de memória) não foram inferidas. Sequência de continuidade salva na seção de retomada do [roadmap](../0001-projeto/0002-paridade-e-roadmap.md).

[Roteiro detalhado](../VALIDACAO_GPU.md) · [Funcionalidade](../0003-funcionalidades/0003-gpu-telemetria.md)
