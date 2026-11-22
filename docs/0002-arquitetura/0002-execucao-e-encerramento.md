# Execução, cancelamento e encerramento

## Inicialização

[Program.cs](../../Linux/Program.cs) obtém a instância única antes de iniciar Avalonia. [App.axaml.cs](../../Linux/App.axaml.cs) cria MainWindow. A abertura inicia telemetria, energia e inventários; a janela não precisa esperar a conclusão de toda a coleta para existir.

[SingleInstance](../../Linux/Core/Lifecycle/SingleInstance.cs) usa arquivos privados no estado XDG, um lock e pedidos de ativação com confirmação. Os pedidos não carregam comandos. Instâncias com a mesma identidade solicitam ativação da janela existente.

## Carga e exclusão

[BenchmarkRunner](../../Linux/Core/Benchmarks/BenchmarkRunner.cs) admite uma carga por vez. O trabalho roda fora da thread da interface. Após cancelamento, um retorno parcial do algoritmo não vira resultado concluído.

MainWindow mantém bloqueios adicionais entre cargas, testes ativos de rede, exportação e limpeza. Coletores de leitura têm seus próprios ciclos/tarefas; não se deve interpretar o bloqueio de benchmarks como pausa global de telemetria.

## Proteção térmica

[CpuThermalPolicy](../../Linux/Core/Benchmarks/CpuThermalPolicy.cs) exige temperatura válida, recente (menos de 5 s) e abaixo do limite. Um watchdog em thread dedicada observa o snapshot, sem depender da fila de trabalho dos benchmarks.

Benchmarks permitem configurar/desativar a proteção. Stress CPU/RAM exige proteção e possui limite próprio. O sensor observado é CPU, não disco, DIMM ou GPU.

## Fechamento

MainWindow intercepta o fechamento, solicita cancelamento, aguarda carga e tarefas de coleta/gravação/exportação, libera recursos e então fecha. StressTest espera todos os seus workers. Cancelamento é cooperativo, não instantâneo.

Critérios de regressão: nova carga recusada enquanto outra está ativa; estado incompleto sem score; ausência de worker restante após retorno; preferências e histórico preservados; recuperação de instância após encerramento abrupto. Consulte [validação](../0005-desenvolvimento/0002-validacao.md).
