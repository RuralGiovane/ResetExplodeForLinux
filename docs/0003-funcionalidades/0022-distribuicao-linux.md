# Distribuição e instalação Linux

**Estado Linux em 22/11/2026:** Pendente.

## Finalidade

Entregar o aplicativo ao usuário sem exigir ambiente de desenvolvimento.

## Comportamento atual

O CI compila/valida Linux. Publicação automatizada atual gera distribuição Windows. O plano registra pacote Linux x64 portátil, runtime incluído, comando, entrada desktop e instalação por usuário como trabalho futuro.

## Código e referência Windows

- [.github/workflows/linux.yml](../../.github/workflows/linux.yml)
- [.github/workflows/publicar.yml](../../.github/workflows/publicar.yml)
- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)

Referência Windows:

- [Windows/publicar.bat](../../Windows/publicar.bat)

## Limites e diferenças

Empacotamento permanece depois das funcionalidades obrigatórias conforme orientação histórica. AppImage/deb/rpm são possibilidades, não formatos já adotados. Abrir pelo terminal não transforma o app em CLI sem GUI.

## Validação e continuidade

Critérios futuros: dependências nativas, instalação/atualização/remoção, preservação de dados XDG, caminhos/ícones e execução em distribuições alvo.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
