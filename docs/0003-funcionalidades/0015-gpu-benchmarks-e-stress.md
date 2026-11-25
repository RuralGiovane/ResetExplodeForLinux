# GPU: benchmark, stress e ray tracing

**Estado Linux em 23/11/2026:** Backend offscreen e stress com seleção multi-GPU na interface implementados; prévia ao vivo implementada; benchmarks gráficos com pontuação pendentes.

## Finalidade

Portar as cargas gráficas e medições presentes na referência.

## Comportamento atual

Windows possui GpuBurn, GpuGameBench e GpuRtBench com Direct3D. O Linux possui agora um pipeline Vulkan offscreen inicial; não é equivalente integral à referência Windows. NVML atual fornece telemetria, não renderização.

`LinuxVulkanProbe` executa manualmente `vulkaninfo --summary`, sem shell ou elevação, com locale C, prazo de 10 s e limite de 256 Ki caracteres por canal. Aguarda encerramento do processo em cancelamento/timeout. Não instala Vulkan Tools automaticamente. Driver ausente, executável indisponível, formato inválido e saída com erro não produzem dispositivos utilizáveis. Avisos em stderr e blocos inválidos tornam explícita a possibilidade de inventário incompleto.

Retorna nome, versão Vulkan, tipo, driver e UUID quando disponível. Tipos CPU/software, virtual e desconhecido não são classificados como GPU física. Índices pertencem à consulta: não correspondem automaticamente a índices NVML. O inventário não verifica filas, extensões de dispositivo, criação de pipeline, apresentação ou sensores; detectar uma GPU não autoriza iniciar stress.

Diagnóstico: `dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-vulkan`. Ctrl+C cancela; indisponibilidade retorna código 3. Ainda não há ação gráfica ou persistência deste inventário.

## Código e referência Windows

- [docs/plano_porte_linux_reset_explode.md](../../docs/plano_porte_linux_reset_explode.md)
- [Linux/Hardware/Gpu/LinuxVulkanProbe.cs](../../Linux/Hardware/Gpu/LinuxVulkanProbe.cs)
- [Linux/SmokeTests/Gpu/VulkanChecks.cs](../../Linux/SmokeTests/Gpu/VulkanChecks.cs)

Referência Windows:

- [Windows/GpuBurn.cs](../../Windows/GpuBurn.cs)
- [Windows/GpuGameBench.cs](../../Windows/GpuGameBench.cs)
- [Windows/GpuRtBench.cs](../../Windows/GpuRtBench.cs)

## Limites e diferenças

Bindings, shaders e sincronização do pipeline offscreen estão implementados. Prévia integrada à interface usa cópia dos pixels da GPU. Apresentação direta via swapchain, suporte a ray tracing e semântica da pontuação permanecem pendentes. Não declarar equivalência de score antes de validar metodologia.

## Validação e continuidade

O backend verifica suporte ao pipeline inicial e proteção térmica; cancelamento real foi exercitado. Cobertura de device loss, validação visual, sensores adicionais e séries reproduzíveis permanece pendente.

Builds Avalonia/SmokeTests e suíte completa aprovados sem avisos/erros. `--vulkan-only` cobre múltiplos dispositivos, software, tipos virtuais, campos ausentes/inválidos, avisos, limites de saída, executável ausente, timeout e cancelamento. Consulta real do novo código fora do sandbox reconheceu Intel UHD Graphics (CML GT2) e NVIDIA GTX 1650. No sandbox, retornou indisponibilidade de driver corretamente. Essa primeira consulta apenas inventariou dispositivos; a carga gráfica foi validada posteriormente, conforme a seção abaixo.

Referências: [Vulkan Tools — resumo de dispositivos](https://github.com/KhronosGroup/Vulkan-Tools/blob/main/vulkaninfo/vulkaninfo.md) e [tipos e propriedades dos dispositivos Vulkan](https://docs.vulkan.org/spec/latest/chapters/devsandqueues.html). `--summary` é texto, não contrato estruturado estável; mudanças de formato devem resultar em indisponibilidade/avisos. O backend nativo usa Silk.NET.Vulkan 2.22.0; o inventário textual continua disponível separadamente.

[Roteiro relacionado](../../docs/VALIDACAO_BENCHMARKS.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

## Backend gráfico e proteção térmica

Implementado em `Linux/Hardware/Gpu/VulkanGpuRenderer.cs`, com shaders em `Gpu/Shaders`. Enumera diretamente a API Vulkan e seleciona GPU física por endereço PCI único. A temperatura é consultada por `LinuxNvml.ReadTemperature` usando esse mesmo PCI; `GpuThermalGuard` exige amostra recente e abaixo do limite. NVIDIA usa NVML; Intel/AMD podem usar hwmon sob o mesmo PCI (drivers i915/xe/amdgpu/radeon), sem substituição por sensor CPU.

Diagnósticos `--gpu-devices` e `--gpu-render <PCI> <segundos>` em SmokeTests. Renderiza torus procedural offscreen 640×360, 32 camadas e 32 iterações ALU, sem janela/depth/MSAA; uma submissão por vez. Retorna frames concluídos, duração e contagem de pixels não pretos, sem score. Ctrl+C impede novas submissões e aguarda recursos em uso. Biblioteca/API/drivers indisponíveis geram erro explícito.

Builds e suíte aprovados, carga curta e cancelamento verificados na GTX 1650. Interface, histórico e exportação estão integrados. Carga real AMD/Intel e validação prolongada continuam pendentes. A prévia da cena já está integrada à interface. Consulte [decisão e limites](../0006-decisoes/0004-backend-vulkan.md) e [roteiro GPU](../VALIDACAO_GPU.md).

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)

## Seleção automática de dispositivos e escolha do usuário

Ao abrir o app, o card Stress de GPU em Benchmark → GPU detecta dispositivos Vulkan e consulta sensores individualmente. A lista inclui integrada, dedicada e dispositivos não elegíveis, com nome/tipo/PCI. Não há seleção ou carga automática. ATUALIZAR GPUs refaz a detecção e preserva o PCI escolhido quando ainda existe; remoção limpa a seleção, sem fallback para outra placa. Antes de submeter carga, o backend verifica novamente a identidade observada.

INICIAR STRESS DA GPU fica disponível somente para opção com sensor válido. Duração 1–5 min, limite GPU 60–90 °C (80 padrão), progresso, cancelamento e exclusão mútua seguem o fluxo de benchmarks. Histórico/exportações registram gpu-stress, GPU/tipo/PCI, duração, metodologia e limite GPU; não há métricas parciais em cancelamento/erro. Sem sensor próprio, a GPU permanece visível com motivo do bloqueio. A Intel UHD desta máquina está nesse caso; GTX 1650 validada em carga curta e cancelamento pela UI.

Fontes: [seleção e sensores](../../Linux/Hardware/Gpu/GpuStressDiscovery.cs), [integração UI](../../Linux/Views/MainWindow.GpuStress.cs), [regressões](../../Linux/SmokeTests/Gpu/GpuSelectionChecks.cs). Referência de hwmon: [kernel Linux](https://www.kernel.org/doc/html/v5.9/hwmon/sysfs-interface.html).

## Prévia da carga real — 23/11/2026

Benchmark → GPU exibe os pixels RGBA8 do torus renderizado pela placa selecionada. O backend reutiliza um buffer de leitura Vulkan coerente, aguarda a fence e publica um snapshot próprio até a cada 200 ms, mais o quadro final. A UI consome somente o snapshot mais recente, respeita o stride do bitmap e não bloqueia o worker em callbacks do dispatcher. Temperatura e cancelamento são verificados também enquanto a cópia aguarda a GPU; recursos em uso só são liberados após terminar a submissão.

Ao iniciar ou trocar a GPU, a imagem anterior é limpa. Após sucesso, cancelamento ou falha, a última imagem recebida fica marcada como parada; sem quadro recebido, o estado é explícito. Fechar cancela a carga e descarta a imagem após o worker encerrar.

A prévia não mede FPS de jogos. O readback periódico tem custo e integra o tempo do loop; histórico/exportações da UI identificam `linux-gpu-stress-preview-v2`, distinguindo execuções antigas sem prévia. São preservados resolução, shaders, 32 camadas, proteção térmica e ausência de score. A cópia final e inicialização ficam fora da duração reportada.

Sincronização segue [exemplos oficiais Vulkan de leitura pela CPU](https://docs.vulkan.org/guide/latest/synchronization_examples.html): barreira para leitura host, fence e memória coerente; a imagem retorna ao layout de color attachment após a cópia.
