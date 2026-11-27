# Telemetria de GPU

**Estado Linux em 26/11/2026:** Parcial; inventário ampliado e telemetria por driver.

## Finalidade

Acompanhar métricas de GPU disponíveis no Linux.

## Comportamento atual

Backend opcional NVIDIA via NVML fornece temperatura, clocks, uso, potência e memória quando suportados. Falha/ausência da biblioteca não deve impedir os outros coletores; indisponibilidade é por métrica.

As leituras AMD/Intel agora entram em **Sistema e sensores → Sensores**, com filtro por fabricante, PCI ou nome da métrica:

- AMD com driver amdgpu: uso da GPU, atividade da memória e VRAM total/usada.
- Intel i915: clocks medido e solicitado por GT; interface legada quando não há diretórios de GT.
- Intel Xe: clocks medido e solicitado por tile/GT, quando esses diretórios existem.
- hwmon de i915, xe e radeon também é classificado como GPU. Temperatura da CPU não substitui a da GPU.

A identidade DRM usa o dispositivo PCI, independentemente do número cardN. Conectores são ignorados e PCI duplicado não duplica as leituras. Os links da classe DRM são resolvidos antes dos links relativos internos. O coletor não inicia Vulkan, carga gráfica ou comandos externos e não escreve em sysfs. NVIDIA continua no backend NVML existente.

## Código e referência Windows

- [Linux/Hardware/Gpu/LinuxGpuInventory.cs](../../Linux/Hardware/Gpu/LinuxGpuInventory.cs)

- [Linux/Hardware/Gpu/LinuxDrmSensors.cs](../../Linux/Hardware/Gpu/LinuxDrmSensors.cs)
- [Linux/Hardware/Interop/LinuxNvml.cs](../../Linux/Hardware/Interop/LinuxNvml.cs)
- [Linux/Hardware/Telemetry/LinuxSensorMonitor.cs](../../Linux/Hardware/Telemetry/LinuxSensorMonitor.cs)

Referência Windows:

- [Windows/GpuInfoCollector.cs](../../Windows/GpuInfoCollector.cs)
- [Windows/Nvml.cs](../../Windows/Nvml.cs)
- [Windows/GpuSpecDb.cs](../../Windows/GpuSpecDb.cs)

## Limites e diferenças

Somente leitura: não implementa ajuste de clocks ou potência. A identificação/métricas AMD e Intel não atingem a cobertura Windows. A cobertura depende de arquivos e permissões do driver; dados ausentes/inválidos aparecem como indisponíveis, e zero válido é preservado. Clock solicitado não é clock medido nem percentual de uso. Não estima VRAM de uma integrada a partir da RAM do sistema. Os rótulos DRM mostram fabricante, PCI e driver; não são um banco de especificações/modelos comerciais.

## Validação e continuidade

Diagnóstico somente de leitura após build:

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --drm-sensors
```

Fixtures cobrem múltiplos dispositivos, PCI duplicado, renumeração, links relativos em cards simbólicos, unidades/cultura, zero/ausência, percentuais e memória inválidos, overflow, i915 legado/multi-GT, Xe, remoção, cancelamento e integração com telemetria. Não substituem validação em hardware AMD/Xe real. Em 25/11/2026, o diagnóstico real identificou Intel/i915 no PCI 0000:00:02.0 e leu clocks medido/solicitado de GT0 (350 MHz na amostra). Build Avalonia/UiChecks e suíte completa net8.0 aprovados. Não foi iniciada carga de stress.

Fontes: [AMDGPU — memória](https://kernel.org/doc/html/latest/gpu/amdgpu/driver-misc.html), [AMDGPU — métricas](https://docs.kernel.org/gpu/amdgpu/thermal.html), [i915 — sysfs GT](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/i915/gt/intel_gt_sysfs_pm.c), [Xe — frequência GT](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/xe/xe_gt_freq.c).

### Inventário de especificações

Em **Benchmark → GPU → Identificação e especificações das GPUs**, a detecção consulta controladores de vídeo PCI independentemente de Vulkan. Exibe fabricante, endereço e IDs PCI/subsistema, revisão, driver, versão do módulo, VBIOS, link PCIe atual/máximo reportado e memória quando disponíveis. NVML associa nome, VBIOS e memória por PCI; amdgpu fornece memória pelo sysfs. A enumeração Vulkan complementa modelo, tipo e versão da API somente com PCI não ambíguo. Atualizar GPUs renova os dados.

NVML também fornece núcleos GPU, barramento de memória em bits e clocks máximos gráfico/de memória em MHz, quando suportados. Cada consulta é opcional e independente: símbolo ausente, erro, zero ou sentinela uint.MaxValue resultam em indisponibilidade apenas daquele campo. Os rótulos identificam NVML como fonte e os dados são associados pelo mesmo endereço PCI do dispositivo.

Não há inferência de VRAM integrada a partir da RAM. Um catálogo local limitado complementa tipo de memória e processo de fabricação (litografia) como **referência**, com URLs das fontes e modelo documentado. GPUs fora da cobertura continuam com esses campos indisponíveis. Clocks máximos não são clocks medidos, base/boost de referência ou taxa efetiva; não são usados para estimar GDDR, largura de banda ou TFLOPS. Núcleos reportados não são convertidos em SMs, unidades RT ou Tensor. PCIe atual pode diminuir em repouso; máximo reportado não mede throughput. Ausências permanecem explícitas. O inventário não é persistido nem incluído no relatório de benchmarks, cujo esquema permanece igual.

Diagnóstico somente leitura: `dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --gpu-inventory`. Esse comando consulta PCI/sysfs/NVML; os campos Vulkan são complementados pela interface. Fixtures cobrem IDs/links/unidades, ausência, negativos, associação NVML por PCI, ambiguidade Vulkan, remoção e cancelamento. Leitura real aprovada para Intel/i915 e GTX 1650; AMD/Xe continuam sem hardware real disponível. Referência dos atributos: [ABI PCI do kernel](https://www.kernel.org/doc/Documentation/ABI/testing/sysfs-bus-pci).

Há aceite histórico NVIDIA; validar novamente ao alterar backend, driver ou mapeamento de sensores. GPU/Vulkan está em ficha separada.

[Roteiro relacionado](../../docs/VALIDACAO_HARDWARE.md). Para funções pendentes, o roteiro é referência geral; não representa uma checklist de implementação já cumprida.

[Índice de funcionalidades](README.md) · [Paridade do porte](../0001-projeto/0002-paridade-e-roadmap.md)

### Ampliação NVML — 26/11/2026

Fonte das consultas: [documentação oficial NVML](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html), funções `nvmlDeviceGetNumGpuCores`, `nvmlDeviceGetMemoryBusWidth` e `nvmlDeviceGetMaxClockInfo` (domínios gráfico e memória). A biblioteca continua opcional e carregada dinamicamente.

Leitura real na GTX 1650: 896 núcleos, barramento de 128 bits, máximos reportados de 1785 MHz (gráfico) e 6001 MHz (memória). Intel/i915 permanece sem esses campos NVML. Build Release de SmokeTests/Avalonia/UiChecks sem avisos ou erros e suíte completa net8.0 aprovada, incluindo fixtures de dados parciais/inválidos e isolamento entre fabricantes. Nenhum stress GPU iniciado. Detalhes no [registro da ampliação](../0007-historico/0011-2026-11-26-especificacoes-nvml.md).

### Catálogo de referência — 26/11/2026

[GpuReferenceCatalog.cs](../../Linux/Hardware/Gpu/GpuReferenceCatalog.cs) cruza fabricante/dispositivo PCI exatos. A cobertura inicial é GTX 1080 Ti (10de:1b06) e RTX 2080 Ti (10de:1e04/1e07). A [lista oficial NVIDIA](https://download.nvidia.com/XFree86/Linux-x86_64/590.48.01/README/supportedchips.html) fundamenta a identidade; a tabela 1 do [artigo técnico NVIDIA](https://developer.nvidia.com/blog/nvidia-turing-architecture-in-depth/) fundamenta memória e processo. Não é detecção física do chip de memória, nem cobertura geral da família GeForce.

A consulta funciona sem NVML. Quando o coletor já tem nome reportado, ele precisa corresponder exatamente ao modelo documentado, ignorando caixa e o prefixo NVIDIA; não há correspondência por substring nem primeira variante como fallback. Capacidade, clocks, núcleos e demais leituras do driver não são substituídos pelo catálogo. Os modelos iniciais foram exercitados por fixtures, não por hardware real.

A GTX 1650 desta máquina (10de:1f9d) não foi cadastrada: o nome DMI informa apenas Nitro AN515-55, sem SKU útil. Fichas Acer para outras variantes não comprovam a configuração instalada. O usuário não pôde conferir o sufixo nesta sessão. Não deduzir GDDR6 pelo clock de 6001 MHz nem litografia pelo nome genérico Turing.
