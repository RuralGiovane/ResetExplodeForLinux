# Telemetria de GPU

**Estado Linux em 25/11/2026:** Parcial.

## Finalidade

Acompanhar métricas de GPU disponíveis no Linux.

## Comportamento atual

Backend opcional NVIDIA via NVML fornece temperatura, clocks, uso, potência e memória quando suportados. Falha/ausência da biblioteca não deve impedir os outros coletores; indisponibilidade é por métrica.

As leituras AMD/Intel agora entram em **Sistema e sensores → Sensores**, com filtro por fabricante, PCI ou nome da métrica:

- AMD com driver amdgpu: uso da GPU, atividade da memória e VRAM total/usada.
- Intel i915: clocks medido e solicitado por GT; interface legada quando não há diretórios de GT.
- Intel Xe: clocks medido e solicitado por tile/GT, quando esses diretórios existem.
- hwmon de i915, xe e radeon também é classificado como GPU. Temperatura da CPU não substitui a da GPU.

A identidade DRM usa o dispositivo PCI, independentemente do número cardN. Conectores são ignorados e PCI duplicado não duplica as leituras. Os links da classe DRM são resolvidos antes dos links relativos internos. O coletor não inicia Vulkan, carga gráfica ou comandos externos e não escreve em sysfs. NVIDIA continua no backend NVML existente.

## Código e referência Windows

- [Linux/Hardware/Gpu/LinuxDrmSensors.cs](../../Linux/Hardware/Gpu/LinuxDrmSensors.cs)
- [Linux/Hardware/Interop/LinuxNvml.cs](../../Linux/Hardware/Interop/LinuxNvml.cs)
- [Linux/Hardware/Telemetry/LinuxSensorMonitor.cs](../../Linux/Hardware/Telemetry/LinuxSensorMonitor.cs)

Referência Windows:

- [Windows/GpuInfoCollector.cs](../../Windows/GpuInfoCollector.cs)
- [Windows/Nvml.cs](../../Windows/Nvml.cs)
- [Windows/GpuSpecDb.cs](../../Windows/GpuSpecDb.cs)

## Limites e diferenças

Somente leitura: não implementa ajuste de clocks ou potência. A identificação/métricas AMD e Intel não atingem a cobertura Windows. A cobertura depende de arquivos e permissões do driver; dados ausentes/inválidos aparecem como indisponíveis, e zero válido é preservado. Clock solicitado não é clock medido nem percentual de uso. Não estima VRAM de uma integrada a partir da RAM do sistema. Os rótulos DRM mostram fabricante, PCI e driver; não são um banco de especificações/modelos comerciais.

## Validação e continuidade

Diagnóstico somente de leitura após build:

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --drm-sensors
```

Fixtures cobrem múltiplos dispositivos, PCI duplicado, renumeração, links relativos em cards simbólicos, unidades/cultura, zero/ausência, percentuais e memória inválidos, overflow, i915 legado/multi-GT, Xe, remoção, cancelamento e integração com telemetria. Não substituem validação em hardware AMD/Xe real. Em 25/11/2026, o diagnóstico real identificou Intel/i915 no PCI 0000:00:02.0 e leu clocks medido/solicitado de GT0 (350 MHz na amostra). Build Avalonia/UiChecks e suíte completa net8.0 aprovados. Não foi iniciada carga de stress.

Fontes: [AMDGPU — memória](https://kernel.org/doc/html/latest/gpu/amdgpu/driver-misc.html), [AMDGPU — métricas](https://docs.kernel.org/gpu/amdgpu/thermal.html), [i915 — sysfs GT](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/i915/gt/intel_gt_sysfs_pm.c), [Xe — frequência GT](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/xe/xe_gt_freq.c).

Há aceite histórico NVIDIA; validar novamente ao alterar backend, driver ou mapeamento de sensores. GPU/Vulkan está em ficha separada.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
