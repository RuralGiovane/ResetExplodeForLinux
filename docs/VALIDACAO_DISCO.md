# Benchmark de disco e carregamento Linux

## Entrega de 25/10/2026

Implementados na página Benchmark, com pasta absoluta escolhida pelo usuário, progresso, cancelamento, histórico e exportações HTML/JSON existentes. O backend usa Linux x64; outras arquiteturas são rejeitadas explicitamente. Usuário aprovou o roteiro gráfico e de cargas completas abaixo; valores individuais não foram fornecidos. O reposicionamento posterior dos controles de execução ainda requer conferência visual.

## I/O e arquivos temporários

- Arquivos regulares exclusivos em `.reset-explode-bench-<identificador>` dentro da pasta selecionada. Diretório 0700, arquivos 0600, criação exclusiva, sem seguir symlink no arquivo final. Não abre discos brutos nem altera arquivos existentes.
- `O_DIRECT`, buffers nativos alinhados a 4096 bytes, tamanhos/offsets múltiplos de 4096; escritas com `O_DSYNC`. Transferência curta/EOF é erro e descarta a medição. Repetição de syscall somente em `EINTR`; sem reabertura com I/O em cache.
- Alinhamento confirmado por `statx/STATX_DIOALIGN`. Ausência, falta de suporte ou alinhamento incompatível impede a medição.
- Btrfs tem caminho explícito: `fstatfs` identifica o sistema de arquivos e `BTRFS_IOC_FS_INFO` informa seu setor. Antes de qualquer escrita, apenas o arquivo temporário novo recebe NoCoW/NoCompress via ioctl; flags são relidas e verificadas. NoCoW implica ausência de checksum dos dados nesses arquivos. Isso evita o fallback de escrita direta com checksum descrito pelo Btrfs, mas **não representa o desempenho de arquivos Btrfs comuns com CoW/checksum/compressão**. Pastas e configurações do volume permanecem intactas.
- Cache do controlador/dispositivo, metadados, escalonamento e características do sistema de arquivos ainda podem afetar os tempos. Não há limpeza global de caches. Não é uma medição de disco físico isolado.
- Verificação inicial de espaço disponível no caminho selecionado: carga mais margem de 512 MiB. Espaço pode mudar durante o teste; ENOSPC e demais falhas interrompem sem publicar taxas parciais.
- Cancelamento observado entre operações. Syscall em andamento precisa retornar; não há prazo garantido se dispositivo/driver travar. Workers são aguardados antes de fechar handles, liberar buffers e limpar arquivos.
- Limpeza remove somente nomes rastreados, sem exclusão recursiva. Falha informa a pasta residual e impede publicação de sucesso. Encerramento abrupto/queda de energia pode deixar a pasta temporária; não há varredura automática para removê-la.

Referências: [open/O_DIRECT](https://man7.org/linux/man-pages/man2/open.2.html), [statx e alinhamento](https://man7.org/linux/man-pages/man2/statx.2.html), [flags de inode](https://man7.org/linux/man-pages/man2/FS_IOC_SETFLAGS.2const.html) e [checksums e escrita direta no Btrfs](https://btrfs.readthedocs.io/en/latest/Checksumming.html). Layouts/constantes conferidos nos headers Linux x64 locais.

## Metodologia

**Disco — `linux-disk-direct-v1`:** arquivo de 512 MiB, escrita sequencial seguida de leitura em blocos de 1 MiB; leitura e escrita aleatórias de 4 KiB, uma operação síncrona por vez, por 4 s cada; leitura aleatória com até oito workers síncronos por 4 s. Não promete profundidade de fila 8 no dispositivo. Escrita sequencial inclui alocação do arquivo. Média de duração por operação inclui coordenação/sorteio/progresso e não é latência física isolada. Volume adicional de escrita aleatória varia com o desempenho; não há volume fixo por fase.

**Carregamento — `linux-disk-loading-v1`:** pacote de 512 MiB e 1000 assets de 64 KiB, preparados fora da medição. Pacote lido em blocos de 1 MiB; assets abertos/lidos/fechados por até oito workers, incluindo consultas de suporte. Uma passada de assets não medida antecede a passada medida. Tempo total = tempo de leitura do pacote + tempo medido dos assets; não inclui preparação nem aquecimento e não representa o carregamento de um jogo real.

Taxas em MiB/s (1.048.576 bytes), IOPS, microssegundos médios e arquivos/s. Uma execução, sem melhor-de-N nem pontuação comparável ao Windows. Buffer pseudoaleatório de 1 MiB reutilizado; não é garantia contra deduplicação. Métodos/cargas diferem da implementação Windows, preservada. Contexto inclui pasta de destino e metodologia: o caminho pode conter informação pessoal e aparece no relatório.

A política térmica existente aplica-se à CPU; não monitora a temperatura do disco para interromper esses testes. Exclusão com outros benchmarks, testes de rede e exportações usa o coordenador existente.

## Validações automatizadas

```bash
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --disk-only
# Opt-in: cria/escreve/lê arquivos pequenos na pasta indicada.
dotnet run --project Linux/SmokeTests/ResetExplode.SmokeTests.csproj -c Release --no-build -- --live-disk-smoke /caminho/absoluto
```

A suíte padrão usa backend simulado e arquivos marcadores vazios; não executa benchmark real de disco. Cobre alinhamento, transferência curta, contagens, taxas, progresso, pré-cancelamento, cancelamento em preparação/leitura medida, erros sequenciais/paralelos, falta de espaço, exclusividade e limpeza sem remoção de arquivos desconhecidos. O diagnóstico opt-in verifica round-trip de bytes, EOF, colisão de nome e cargas reduzidas de 4 MiB/8 assets/20 ms por fase aleatória. Código 3 indica suporte indisponível, 130 cancelamento, 1 falha, 2 uso inválido.

Builds Avalonia/SmokeTests e suíte completa aprovados em .NET 8, sem avisos/erros; `git diff --check` aprovado.

Resultado local: diagnóstico real aprovado em Btrfs após configuração dos arquivos temporários; tmpfs em `/tmp` rejeitado por ausência de suporte confirmado. Esses resultados não validam todos os sistemas de arquivos, cargas padrão ou interação gráfica.

## Roteiro gráfico — aprovado pelo usuário

- [X] Abrir Benchmark: campos e avisos legíveis; informar pasta absoluta existente em um volume com pelo menos 1,1 GiB livres.
- [X] TESTAR DISCO até concluir: progresso sequencial/aleatório, taxas positivas e unidades corretas; arquivo temporário removido.
- [X] TESTAR CARREGAMENTO até concluir: preparação, pacote, passada não medida e leitura dos assets; resultado com taxas/tempo, limpeza concluída.
- [X] Pasta inexistente/relativa ou sem permissão: erro explícito, sem resultado parcial e sem alteração de arquivos existentes. Corrigir a pasta permite tentar novamente.
- [X] Quando disponível, testar sistema de arquivos sem suporte (por exemplo tmpfs): indisponibilidade explícita, sem fallback silencioso.
- [X] Cancelar durante escrita/preparação e durante leitura: sem métricas parciais, limpeza e nova execução funcionando.
- [X] Fechar durante teste: aguardar cancelamento, verificar limpeza e reabrir sem conclusão tardia.
- [X] Enquanto executa, outros benchmarks, testes de rede, exportações e campo de pasta ficam bloqueados; navegação continua utilizável.
- [X] Exportar histórico HTML/JSON e reabrir app: estados, métricas e metodologia preservados. Caminho de destino aparece no contexto.
- [X] Btrfs: confirmar execução com arquivos temporários NoCoW; arquivos existentes na pasta continuam intactos.

Valores individuais, modelo do dispositivo, sistema de arquivos e condições de execução ainda não informados pelo usuário. Comparação entre plataformas permanece pendente.
