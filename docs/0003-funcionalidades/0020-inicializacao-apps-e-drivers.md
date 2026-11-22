# Inicialização, instalação de apps e drivers

**Estado Linux em 22/11/2026:** Pendente no Linux.

## Finalidade

Portar ferramentas de manutenção que a referência Windows reúne no aplicativo.

## Comportamento atual

Windows gerencia inicialização, instala aplicativos por winget e fornece inventário/fluxos de drivers. No Linux não há implementação equivalente dessas três ferramentas. O inventário serial Maker é outro escopo.

## Código e referência Windows

- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/StartupManager.cs](../../Windows/StartupManager.cs)
- [Windows/AppInstaller.cs](../../Windows/AppInstaller.cs)
- [Windows/DriverManager.cs](../../Windows/DriverManager.cs)
- [Windows/MainWindow.Startup.cs](../../Windows/MainWindow.Startup.cs)
- [Windows/MainWindow.Apps.cs](../../Windows/MainWindow.Apps.cs)
- [Windows/MainWindow.Driver.cs](../../Windows/MainWindow.Driver.cs)

## Limites e diferenças

Autostart .desktop/systemd, gerenciadores de pacotes e drivers variam por distribuição. Catálogo, suporte e operações privilegiadas ainda precisam ser definidos. Não há backend remoto de instalação do projeto.

## Validação e continuidade

Critérios futuros: detectar estado sem mutação, distinguir instalação/atualização/remoção, confirmar destino, apresentar falhas e oferecer restauração onde houver backup.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
