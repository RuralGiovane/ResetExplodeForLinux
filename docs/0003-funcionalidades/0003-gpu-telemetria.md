# Telemetria de GPU

**Estado Linux em 22/11/2026:** Parcial.

## Finalidade

Acompanhar métricas de GPU disponíveis no Linux.

## Comportamento atual

Backend opcional NVIDIA via NVML fornece temperatura, clocks, uso, potência e memória quando suportados. Falha/ausência da biblioteca não deve impedir os outros coletores; indisponibilidade é por métrica.

## Código e referência Windows

- [Linux/Hardware/Interop/LinuxNvml.cs](../../Linux/Hardware/Interop/LinuxNvml.cs)
- [Linux/Hardware/Telemetry/LinuxSensorMonitor.cs](../../Linux/Hardware/Telemetry/LinuxSensorMonitor.cs)

Referência Windows:

- [Windows/GpuInfoCollector.cs](../../Windows/GpuInfoCollector.cs)
- [Windows/Nvml.cs](../../Windows/Nvml.cs)
- [Windows/GpuSpecDb.cs](../../Windows/GpuSpecDb.cs)

## Limites e diferenças

Somente leitura: não implementa ajuste de clocks ou potência. A identificação/métricas AMD e Intel não atingem a cobertura Windows. Telemetria não significa que benchmark GPU está portado.

## Validação e continuidade

Há aceite histórico NVIDIA; validar novamente ao alterar backend, driver ou mapeamento de sensores. GPU/Vulkan está em ficha separada.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
