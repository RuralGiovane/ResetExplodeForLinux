# Armazenamento e SMART

**Estado Linux em 22/11/2026:** Implementado.

## Finalidade

Identificar discos/partições, montagens, espaço e indicadores de saúde.

## Comportamento atual

Coletores separam dispositivos de bloco e montagens. SMART é consulta manual por dispositivo com smartctl JSON; a UI oferece fluxo administrativo pontual quando necessário. Relatório contém inventário e última consulta SMART, com datas próprias.

## Código e referência Windows

- [Linux/Hardware/Storage/LinuxStorageCollector.cs](../../Linux/Hardware/Storage/LinuxStorageCollector.cs)
- [Linux/Hardware/Storage/LinuxMountCollector.cs](../../Linux/Hardware/Storage/LinuxMountCollector.cs)
- [Linux/Hardware/Storage/LinuxSmartReader.cs](../../Linux/Hardware/Storage/LinuxSmartReader.cs)
- [Linux/Views/MainWindow.Smart.cs](../../Linux/Views/MainWindow.Smart.cs)

Referência Windows:

- [Windows/DriveMap.cs](../../Windows/DriveMap.cs)
- [Windows/DiskHealth.cs](../../Windows/DiskHealth.cs)
- [Windows/MainWindow.Disks.cs](../../Windows/MainWindow.Disks.cs)

## Limites e diferenças

Estado depende de controlador, suporte SMART, ferramentas e permissões. Código de saída smartctl contém bits de alerta, não apenas sucesso/falha genéricos. Evitar somar montagens que compartilham capacidade como discos independentes.

## Validação e continuidade

Testes cobrem JSON, bits, unidades/valores, processos, timeout e cancelamento. Consulta administrativa tem aceite histórico, limitado ao equipamento testado.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
