# Benchmarks de disco e carregamento

**Estado Linux em 22/11/2026:** Implementado em Linux x64.

## Finalidade

Medir I/O e um cenário sintético de carregamento na pasta escolhida.

## Comportamento atual

Cria arquivos temporários exclusivos. Disco usa arquivo de 512 MiB, operações sequenciais e aleatórias; carregamento usa pacote de 512 MiB e mil assets de 64 KiB. Backend exige I/O direto confirmado, buffers alinhados e escritas O_DSYNC. Limpeza remove os arquivos do próprio workspace.

## Código e referência Windows

- [Linux/Core/Benchmarks/DiskBenchmarks.cs](../../Linux/Core/Benchmarks/DiskBenchmarks.cs)
- [Linux/Core/Storage/LinuxDirectFile.cs](../../Linux/Core/Storage/LinuxDirectFile.cs)
- [Linux/Core/Storage/BenchmarkWorkspace.cs](../../Linux/Core/Storage/BenchmarkWorkspace.cs)
- [Linux/Views/MainWindow.DiskBench.cs](../../Linux/Views/MainWindow.DiskBench.cs)

Referência Windows:

- [Windows/DiskBench.cs](../../Windows/DiskBench.cs)
- [Windows/MemoryBench.cs](../../Windows/MemoryBench.cs)

## Limites e diferenças

No Btrfs, arquivos temporários usam NoCoW/NoCompress, diferentes do padrão. Até oito workers síncronos não significam QD32. Cache de dispositivo/metadados ainda influencia. Não acessa disco bruto nem modifica caches globais.

## Validação e continuidade

Fixtures verificam alinhamento, I/O curto, falhas, cancelamento e limpeza. Existe validação histórica reduzida em Btrfs e aceite de roteiro; comparação de metodologia segue pendente.

[Roteiro relacionado](../../docs/VALIDACAO_DISCO.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)
