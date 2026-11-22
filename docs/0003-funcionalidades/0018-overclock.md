# Overclock e restauração

**Estado Linux em 22/11/2026:** Pendente no Linux.

## Finalidade

Oferecer controles e verificação de ajustes suportados pelo hardware.

## Comportamento atual

Windows possui limites de potência e clocks NVIDIA, restauração e fluxo de verificação OC. Linux possui telemetria NVML e diagnóstico/stress CPU/RAM, mas não escreve limites ou clocks.

## Código e referência Windows

- [Linux/Hardware/Interop/LinuxNvml.cs](../../Linux/Hardware/Interop/LinuxNvml.cs)
- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/OcCheck.cs](../../Windows/OcCheck.cs)
- [Windows/MainWindow.Oc.cs](../../Windows/MainWindow.Oc.cs)
- [Windows/Nvml.cs](../../Windows/Nvml.cs)

## Limites e diferenças

Diagnóstico de estabilidade não equivale a autorização ou suporte para modificar hardware. O futuro backend precisa informar capacidades, permissões, valores originais e como restaurá-los após falha.

## Validação e continuidade

Critérios futuros: recusar valores não suportados, registrar originais, cancelar, restaurar e explicar sensores ausentes. Não há validação de escrita OC Linux.

[Roteiro relacionado](../../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
