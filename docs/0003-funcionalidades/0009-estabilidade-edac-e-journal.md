# Estabilidade: EDAC, journal e comparação

**Estado Linux em 22/11/2026:** Implementado; validação real parcial.

## Finalidade

Observar erros reportados e mudanças durante um intervalo.

## Comportamento atual

EDAC apresenta contadores corrigidos/não corrigidos e período de reset. Journal consulta até 500 avisos/erros do boot atual. Classificação textual indica CPU/memória, gráficos e PCIe, preservando mensagens. Referência e leitura posterior são comparadas pelo boot, relógio monotônico e continuidade observável dos contadores.

## Código e referência Windows

- [Linux/Hardware/Stability/LinuxEdacCollector.cs](../../Linux/Hardware/Stability/LinuxEdacCollector.cs)
- [Linux/Hardware/Stability/LinuxKernelLogReader.cs](../../Linux/Hardware/Stability/LinuxKernelLogReader.cs)
- [Linux/Hardware/Stability/KernelLogClassifier.cs](../../Linux/Hardware/Stability/KernelLogClassifier.cs)
- [Linux/Hardware/Stability/StabilitySession.cs](../../Linux/Hardware/Stability/StabilitySession.cs)
- [Linux/Views/MainWindow.Stability.cs](../../Linux/Views/MainWindow.Stability.cs)

Referência Windows:

- [Windows/HardwareErrors.cs](../../Windows/HardwareErrors.cs)
- [Windows/OcCheck.cs](../../Windows/OcCheck.cs)

## Limites e diferenças

Sem EDAC não significa zero erros. Classificação é indicativa, não diagnóstico causal. A amostra de logs pode não cobrir todo o intervalo; resets rápidos podem escapar. Referência vive apenas na sessão. Não atribui automaticamente ocorrências a benchmarks/stress.

## Validação e continuidade

Fixtures cobrem parsing, limites, boots, resets, remoção e relatórios. Logs/classificação/comparação reais ainda precisam da conferência registrada no roteiro.

[Roteiro relacionado](../../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
