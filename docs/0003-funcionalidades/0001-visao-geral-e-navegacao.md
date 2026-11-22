# Visão geral e navegação

**Estado Linux em 22/11/2026:** Implementado parcialmente.

## Finalidade

Identificar rapidamente a máquina e acessar os diagnósticos.

## Comportamento atual

A navegação principal separa Sistema e sensores de Benchmark. Sistema tem oito abas: Visão geral, Computador, Sensores, Rede, Armazenamento, Alimentação, Estabilidade e Maker. O resumo reúne distribuição, kernel, arquitetura, CPU, placa-mãe/BIOS, RAM, GPU e uso da raiz a partir das coletas existentes; não executa fastfetch.

## Código e referência Windows

- [Linux/Views/MainWindow.axaml](../../Linux/Views/MainWindow.axaml)
- [Linux/Views/MainWindow.Overview.cs](../../Linux/Views/MainWindow.Overview.cs)
- [Linux/Views/MainWindow.Telemetry.cs](../../Linux/Views/MainWindow.Telemetry.cs)

Referência Windows:

- [Windows/MainWindow.Dash.cs](../../Windows/MainWindow.Dash.cs)
- [Windows/MainWindow.xaml](../../Windows/MainWindow.xaml)

## Limites e diferenças

O dashboard Windows tem gráficos, anéis e eventos mais amplos. O Linux não reproduz toda essa composição nem o tema claro. Ausência de dado deve permanecer visível.

## Validação e continuidade

Conferir navegação, rolagem, estados de coleta e janela mínima. Aceites anteriores não validam automaticamente os controles de stress adicionados depois.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
