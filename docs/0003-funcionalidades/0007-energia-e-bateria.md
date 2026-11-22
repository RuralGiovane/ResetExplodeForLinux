# Energia e bateria

**Estado Linux em 22/11/2026:** Implementado parcialmente.

## Finalidade

Mostrar alimentação e informações da bateria reportadas pelo kernel.

## Comportamento atual

LinuxPowerCollector lê power_supply; a UI atualiza estado e apresenta capacidades, desgaste e outras métricas suportadas. Exportação preserva snapshot, unidades, datas e ausência de dados.

## Código e referência Windows

- [Linux/Hardware/Power/LinuxPowerCollector.cs](../../Linux/Hardware/Power/LinuxPowerCollector.cs)
- [Linux/Views/MainWindow.Power.cs](../../Linux/Views/MainWindow.Power.cs)
- [Linux/Hardware/Reports/PowerReportBuilder.cs](../../Linux/Hardware/Reports/PowerReportBuilder.cs)

Referência Windows:

- [Windows/BatteryInfo.cs](../../Windows/BatteryInfo.cs)
- [Windows/MainWindow.Net.cs](../../Windows/MainWindow.Net.cs)

## Limites e diferenças

Nem todo equipamento fornece ciclos, potência ou capacidade de projeto. Não converter campos ausentes em zero. O módulo não fornece todos os controles de plano de energia Windows.

## Validação e continuidade

Fixtures cobrem unidades, sinais, zero, escopo, remoção e cancelamento. Validar bateria real quando houver notebook disponível.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
