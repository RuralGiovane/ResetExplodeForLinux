# Maker e portas seriais

**Estado Linux em 22/11/2026:** Parcial.

## Finalidade

Identificar placas/conversores USB-seriais e condições de acesso.

## Comportamento atual

LinuxSerialCollector inventaria portas, identificação USB VID/PID, driver associado, aliases persistentes e grupos ativos. A UI permite atualizar e exportar snapshot. Ausência de portas e links inválidos são estados tratados.

## Código e referência Windows

- [Linux/Hardware/Maker/LinuxSerialCollector.cs](../../Linux/Hardware/Maker/LinuxSerialCollector.cs)
- [Linux/Views/MainWindow.Serial.cs](../../Linux/Views/MainWindow.Serial.cs)
- [Linux/Hardware/Reports/SerialReportBuilder.cs](../../Linux/Hardware/Reports/SerialReportBuilder.cs)

Referência Windows:

- [Windows/UsbCleaner.cs](../../Windows/UsbCleaner.cs)
- [Windows/MakerDrivers.cs](../../Windows/MakerDrivers.cs)
- [Windows/MainWindow.Maker.cs](../../Windows/MainWindow.Maker.cs)

## Limites e diferenças

Não instala os pacotes .inf Windows no Linux. Inventário/grupos não equivalem a alterar grupos, criar regras udev, liberar processos ou resolver ModemManager. Validação com placa conectada segue pendente.

## Validação e continuidade

Fixtures cobrem USB/ACM, drivers, aliases, symlinks, remoção e grupos. Verificar conexão/reconexão, permissões e aliases reais com dispositivo disponível.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
