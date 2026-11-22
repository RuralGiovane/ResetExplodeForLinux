# FPS estimado, gargalo e overlay

**Estado Linux em 22/11/2026:** Pendente no Linux.

## Finalidade

Portar estimativas de desempenho e acompanhamento durante jogos.

## Comportamento atual

Referência Windows reúne estimativa de FPS, análise de gargalo, eventos ETW e janela de overlay. A simulação CPU Linux não substitui essas funções.

## Código e referência Windows

- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/HowManyFpsClient.cs](../../Windows/HowManyFpsClient.cs)
- [Windows/BottleneckAnalyzer.cs](../../Windows/BottleneckAnalyzer.cs)
- [Windows/FrameMonitor.cs](../../Windows/FrameMonitor.cs)
- [Windows/OverlayWindow.xaml.cs](../../Windows/OverlayWindow.xaml.cs)
- [Windows/OverlayMetrics.cs](../../Windows/OverlayMetrics.cs)

## Limites e diferenças

ETW não é backend Linux. Captura de frametimes, overlay, atalhos e X11/Wayland requerem avaliação específica. Estimativa não deve ser apresentada como medição do jogo.

## Validação e continuidade

Critérios futuros: fonte/proveniência da métrica, unidade, jogo sem suporte, impacto da coleta, foco/entrada, atalhos e diferenças entre sessões gráficas.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
