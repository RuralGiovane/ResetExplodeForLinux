# Biblioteca e perfis de jogos

**Estado Linux em 22/11/2026:** Pendente no Linux.

## Finalidade

Detectar jogos instalados, iniciar um jogo e aplicar seu perfil.

## Comportamento atual

Windows inclui descoberta de bibliotecas, lançamento, opções por jogo e integração de argumentos Steam. Não foi localizado equivalente Linux funcional no checkout.

## Código e referência Windows

- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/GameLibrary.cs](../../Windows/GameLibrary.cs)
- [Windows/GameLauncher.cs](../../Windows/GameLauncher.cs)
- [Windows/GameCatalog.cs](../../Windows/GameCatalog.cs)
- [Windows/Vdf.cs](../../Windows/Vdf.cs)
- [Windows/MainWindow.Launcher.cs](../../Windows/MainWindow.Launcher.cs)

## Limites e diferenças

Proton/Wine, caminhos de bibliotecas e argumentos precisam de regras próprias. Não copiar tweaks de registro Windows. Qualquer edição de configurações Steam deve tratar backup, concorrência e restauração.

## Validação e continuidade

Critérios futuros: descoberta sem alterar bibliotecas, perfil persistido, jogo ausente, falha de lançamento, restauração de mudanças temporárias e Steam aberta. Não há aceite Linux deste módulo.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
