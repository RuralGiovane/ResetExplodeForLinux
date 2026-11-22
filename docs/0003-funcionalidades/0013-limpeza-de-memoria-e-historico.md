# Limpeza de memória e histórico

**Estado Linux em 22/11/2026:** Implementado no escopo do app.

## Finalidade

Recuperar buffers temporários e controlar os registros guardados pelo aplicativo.

## Comportamento atual

LIBERAR MEMÓRIA DOS TESTES solicita coleta/compactação do heap gerenciado fora de medições. Limpar registro da sessão apaga apenas texto; limpar histórico salvo exige confirmação e gravação. A UI bloqueia ações incompatíveis e aguarda tarefas ao fechar.

## Código e referência Windows

- [Linux/Core/Benchmarks/BenchmarkMemoryCleanup.cs](../../Linux/Core/Benchmarks/BenchmarkMemoryCleanup.cs)
- [Linux/Views/MainWindow.MemoryCleanup.cs](../../Linux/Views/MainWindow.MemoryCleanup.cs)
- [Linux/Views/MainWindow.HistoryCleanup.cs](../../Linux/Views/MainWindow.HistoryCleanup.cs)

Referência Windows:

- [Windows/Optimizer.cs](../../Windows/Optimizer.cs)

## Limites e diferenças

Não limpa caches globais Linux, não esvazia RAM de outros processos e não garante queda equivalente de RSS. Pode causar pausa; memória gerenciada e memória física do processo são medidas diferentes. Não equivale ao otimizador Windows.

## Validação e continuidade

Testes verificam recuperação de buffers e preservação de objetos vivos/histórico. Há aceite manual histórico das ações anteriores ao stress.

[Roteiro relacionado](../../docs/VALIDACAO_ESTABILIDADE_MEMORIA.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
