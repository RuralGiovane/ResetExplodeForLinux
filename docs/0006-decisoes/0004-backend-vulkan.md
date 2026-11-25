# 0004 — Vulkan offscreen e sensor associado por PCI

Status: implementada; integração e prévia gráfica concluídas em 23/11/2026. As seções iniciais registram a decisão de 22/11; veja a evolução abaixo.

## Contexto

A referência Windows usa Direct3D 11 e uma janela Win32. O Linux tem Intel e NVIDIA simultaneamente; os índices Vulkan e NVML não representam necessariamente o mesmo dispositivo.

## Escolha e evidência

Backend Vulkan 1.1 com Silk.NET.Vulkan 2.22.0 em Hardware/Gpu. Enumeração e renderização usam a API nativa, sem depender do formato textual de vulkaninfo. Shaders GLSL e SPIR-V compilados são versionados e os binários são recursos do assembly, sem compilador de shaders em runtime.

Primeiro pipeline offscreen: torus procedural 160×64, 32 camadas, 32 iterações ALU, RGBA8 640×360, alpha blending, sem depth buffer/MSAA/swapchain. Não replica integralmente GpuBurn Windows nem permite comparar FPS. Uma submissão por vez, contada somente após a fence; leitura do último frame confirma pixels não pretos.

A associação utiliza VK_EXT_pci_bus_info e nvmlDeviceGetHandleByPciBusId_v2. Nome, posição na lista e índice NVML não são alternativas de associação. Sem identificação PCI única, GPU física NVIDIA e sensor válido, o teste não começa. Intel/AMD/software não têm fallback térmico nesta etapa.

Limite obrigatório: 60–90 °C na API, padrão 80 °C no CLI; amostra com idade máxima de 2 s pelo relógio monotônico. NVML observa apenas temperatura do núcleo, não VRAM/hotspot. Checagem antes da carga e de cada submissão, após conclusão e durante espera da fence. Erro, perda de leitura ou temperatura no limite impedem novas submissões.

## Consequências

Core continua sem dependência Vulkan; Hardware passa a exigir o pacote NuGet e dependências transitivas. Builds offline precisam deles no cache. Sem chamar o backend, a suíte simulada não precisa de loader, driver ou GPU real. O caminho suplementar net10 inclui pacote e recursos, mas não foi executado neste ambiente sem SDK 10.

Cancelamento aguarda o trabalho já enviado antes de destruir recursos. Uma GPU/driver travado pode bloquear chamadas nativas ou a fence: não há garantia de prazo máximo de encerramento. Não aquecer deliberadamente hardware para testar o limite. Duração cobre o loop de renderização, exclui inicialização e leitura final; pode ultrapassar o prazo por um frame em execução.

Ainda faltam janela/preview, controles da aplicação, integração com exclusão mútua, histórico/exportações e sensores Intel/AMD. A API síncrona é destinada ao worker/CLI e não deve ser chamada na thread da UI.

## Referências

- [Silk.NET](https://dotnet.github.io/Silk.NET/docs/)
- [Propriedades PCI Vulkan](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDevicePCIBusInfoPropertiesEXT.html)
- [Consulta de dispositivo/temperatura NVML](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html)
- [Sincronização Vulkan para leitura pelo host](https://github.com/KhronosGroup/Vulkan-Docs/wiki/Synchronization-Examples-%28Legacy-synchronization-APIs%29)

## Evolução em 23/11/2026

Seleção explícita multi-GPU, controles, exclusão mútua e histórico/exportação foram integrados. Intel/AMD podem usar hwmon sob o próprio dispositivo PCI quando disponível; sensor CPU não é alternativa. O limite continua obrigatório e a detecção não inicia carga. A prévia passou a exibir quadros reais a até 5 atualizações/s, com slot único e imagem parada ao encerrar. A metodologia exportada identifica o custo da prévia. Validação real de AMD/Intel permanece pendente.
