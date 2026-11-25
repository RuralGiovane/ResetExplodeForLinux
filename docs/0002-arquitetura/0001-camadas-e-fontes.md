# Camadas e localização do código

Estado observado em 22/11/2026.

| Camada | Responsabilidade | Dependências |
| --- | --- | --- |
| [Linux/Views](../../Linux/Views/) | Layout, eventos, apresentação e coordenação da janela | Core, Hardware, Avalonia |
| [Linux/Core](../../Linux/Core/) | Benchmarks/stress, política térmica, persistência, relatórios e instância única | .NET; fontes CPU de Shared |
| [Linux/Hardware](../../Linux/Hardware/) | Leitura Linux, NVML, backend gráfico Vulkan, processos e modelos/relatórios | .NET, APIs Linux e Silk.NET.Vulkan |
| [Linux/SmokeTests](../../Linux/SmokeTests/) | Fixtures, regressões e diagnósticos opt-in | Core e Hardware |
| [Shared/Benchmarks](../../Shared/Benchmarks/) | Render e simulação CPU usados pelos dois sistemas | Sem WPF/Avalonia |

A UI usa MainWindow dividida em arquivos parciais por domínio. Não é uma arquitetura MVVM completa: eventos chamam serviços e atualizam controles diretamente. Os modelos de hardware são independentes da UI.

## Fluxo típico

A ação do usuário inicia uma operação assíncrona; o serviço lê arquivos ou executa trabalho fora da UI; um snapshot/result retorna à janela; a janela apresenta estado e, conforme o domínio, permite exportar ou persistir.

LinuxFiles aceita uma raiz alternativa para fixtures. IGpuSensorSource separa o monitor da implementação NVML. As duas abstrações permitem simular ausência, erro e remoção sem depender de hardware.

## Onde modificar

- Algoritmo CPU comum: Shared; verificar impacto nos dois destinos.
- Algoritmo exclusivo Linux: Core/Benchmarks ou Core/Storage.
- Coletor e modelo do dispositivo: Hardware na pasta do domínio.
- Comando/progresso/tela: Views/MainWindow.<domínio>.cs e MainWindow.axaml.
- Persistência: Core/Persistence; exportação: Core/Reports ou Hardware/Reports.
- Regressão: SmokeTests na área correspondente.

O [mapa histórico de caminhos](../ESTRUTURA.md) explica a reorganização anterior. Não copiar APIs Windows para Shared.
