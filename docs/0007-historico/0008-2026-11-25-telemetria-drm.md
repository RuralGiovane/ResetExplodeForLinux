# 0008 — 25/11/2026 — Telemetria DRM AMD/Intel

A tela de Sensores passa a incluir métricas DRM AMD/Intel: uso e VRAM em amdgpu, clocks medido/solicitado em i915 e Xe. A identificação utiliza PCI; não depende da ordem cardN nem associa temperatura da CPU à GPU. O backend NVML continua separado. Não há escrita em sysfs ou inicialização de carga gráfica.

A leitura real encontrou um detalhe de resolução dos links: device é relativo ao diretório físico do card. O coletor resolve primeiro o link da classe DRM; uma fixture reproduz essa estrutura para evitar regressão.

Build Avalonia/UiChecks sem avisos/erros; suíte net8.0 completa aprovada. Fixtures cobrem ausência, dados inválidos, unidades, cultura, múltiplas GPUs/GTs, renumeração, links, hotplug e cancelamento. Diagnóstico real Intel/i915 no PCI 0000:00:02.0 retornou GT0 medido/solicitado a 350 MHz na amostra. AMD/Xe permanecem validados apenas por fixtures; não houve novo teste de stress nem revisão gráfica nesta etapa de coleta.

Uso e limites em [Telemetria de GPU](../0003-funcionalidades/0003-gpu-telemetria.md). A implementação não equivale a uma página completa de especificações da GPU.
