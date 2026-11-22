# Instância única e ativação

**Estado Linux em 22/11/2026:** Implementado.

## Finalidade

Evitar janelas concorrentes da mesma aplicação e levar o usuário à instância existente.

## Comportamento atual

Program estabelece identidade por usuário/aplicação; SingleInstance mantém lock e mensagens de ativação em arquivos privados XDG. A janela existente pode ser restaurada/ativada, respeitando diálogo modal.

## Código e referência Windows

- [Linux/Program.cs](../../Linux/Program.cs)
- [Linux/Core/Lifecycle/SingleInstance.cs](../../Linux/Core/Lifecycle/SingleInstance.cs)
- [Linux/Views/MainWindow.axaml.cs](../../Linux/Views/MainWindow.axaml.cs)

Referência Windows:

- [Windows/App.xaml.cs](../../Windows/App.xaml.cs)

## Limites e diferenças

Pedidos não contêm comandos de shell. Falha na confirmação é comunicada; não prova que não existe janela. Ativação depende das políticas do ambiente gráfico.

## Validação e continuidade

Suíte entre processos cobre exclusão, ativação repetida, isolamento, reabertura e encerramento abrupto. Há aceite gráfico histórico.

[Roteiro relacionado](../../docs/VALIDACAO_GRAFICA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
