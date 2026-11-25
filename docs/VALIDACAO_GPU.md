# GPU Vulkan — atualizado em 25/11/2026

## Pela interface

Em Benchmark → GPU → Stress de GPU, aguarde a detecção, selecione uma GPU, ajuste duração/limite e inicie. Nenhum dispositivo é selecionado automaticamente. Atualizar mantém a escolha por PCI ou limpa a seleção se o dispositivo desaparecer. GPU sem sensor próprio permanece na lista e explica por que não pode iniciar.

- [x] Detecção real de Intel UHD e GTX 1650, seleção explícita e atualização preservando PCI.
- [x] GTX habilitada; Intel sem sensor próprio bloqueada com motivo legível.
- [x] Carga curta e cancelamento pela UI, bloqueios recíprocos com benchmarks e controles liberados ao terminar.
- [x] Histórico temporário: GPU/PCI, proteção térmica GPU, cancelled e ausência de métricas parciais.
- [x] Conteúdo visual conferido em 945×993, sem corte horizontal.
- [ ] Conferir teclado, dropdown e layout no tamanho mínimo real (o gerenciador de janelas impediu validar 820×620).
- [x] Fechamento da janela durante carga GPU com prévia ativa.
- [ ] Conferir reabertura após fechamento sob carga.
- [ ] Concluir teste prolongado e conferir relatórios HTML/JSON pelo diálogo.
- [ ] Validar carga real AMD/Intel com sensor hwmon próprio; por enquanto testado com fixtures.

## Executar

Após compilar SmokeTests em Release, liste os dispositivos e escolha explicitamente o PCI NVIDIA reportado:

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --gpu-devices
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --gpu-render 0000:01:00.0 2
```

O endereço acima é o observado nesta máquina, não um padrão universal. A execução gera carga real de GPU, offscreen, por 1–300 s. Limite obrigatório de 80 °C no CLI. Ctrl+C solicita cancelamento e aguarda o frame em execução; não há janela de preview nem resultado parcial exportado. Não executar simultaneamente com outros benchmarks. Exige Vulkan 1.1, driver, GPU física identificável por PCI e sensor correspondente (NVML NVIDIA ou hwmon próprio Intel/AMD). Não precisa de vulkaninfo para renderizar.

## Evidências

- [x] Enumeração Vulkan nativa identificou Intel e NVIDIA com endereços PCI distintos.
- [x] Carga curta na GTX 1650 produziu frames completos e pixels não pretos.
- [x] Cancelamento real por Ctrl+C retornou 130, sem publicar resultado parcial; nova execução posterior concluiu normalmente.
- [x] Testes simulados recusam sensor de outra GPU, ausente, obsoleto, inválido ou no limite térmico.
- [x] SPIR-V passou por spirv-val com alvo Vulkan 1.1.
- [x] Validação visual do torus na prévia ao vivo da GTX 1650.
- [ ] Validação com camada Khronos de diagnóstico Vulkan (não disponível neste ambiente).
- [x] Cancelamento e fechamento durante carga exercitados pela interface com a prévia ativa.

Sem certificação de estabilidade, saturação máxima ou equivalência com Windows. Não provocar superaquecimento para validar proteção. Driver travado pode atrasar indefinidamente o retorno das funções nativas/fences; o backend não destrói recursos que ainda estejam em uso.

## Recompilar shaders

SPIR-V já está versionado; glslc e spirv-val são necessários apenas para modificar/verificar shaders:

```bash
glslc --target-env=vulkan1.1 -O Linux/Hardware/Gpu/Shaders/burn.vert -o Linux/Hardware/Gpu/Shaders/burn.vert.spv
glslc --target-env=vulkan1.1 -O Linux/Hardware/Gpu/Shaders/burn.frag -o Linux/Hardware/Gpu/Shaders/burn.frag.spv
spirv-val --target-env vulkan1.1 Linux/Hardware/Gpu/Shaders/burn.vert.spv
spirv-val --target-env vulkan1.1 Linux/Hardware/Gpu/Shaders/burn.frag.spv
```

## Prévia ao vivo — validação em 23/11/2026

- Builds Avalonia e SmokeTests sem avisos/erros; suíte completa aprovada.
- Fixtures verificam snapshots independentes, canais RGBA e ordem das linhas, stride com padding, dimensões inválidas, slot sem fila, concorrência e pré-cancelamento.
- Diagnóstico `dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --gpu-preview 0000:01:00.0`: executa 3 s com limite de 80 °C, verifica quadros visíveis e mutáveis no tempo, ordem e quadro final; depois inicia nova carga e cancela no primeiro callback. Nesta máquina: GTX 1650, 531 frames e 16 atualizações no primeiro teste, seguido de cancelamento aprovado. Esses números são evidência de funcionamento, não pontuação.
- Pela interface, com configurações/histórico temporários: carga de aproximadamente 3 s, captura da cena, cancelamento, última imagem identificada como parada, troca para Intel sem sensor limpando a imagem e bloqueando início, retorno à GTX, reinício e fechamento durante carga após aproximadamente 2 s. Janela encerrou normalmente. Captura observada em 944×620; o gerenciador de janelas não respeitou a largura solicitada, portanto não confirma o tamanho mínimo de 820 px.
- Não houve validação prolongada, de carga real AMD/Intel nem com camada Khronos. A prévia tem custo de leitura da GPU; exportações identificam metodologia com prévia. Nenhuma dessas verificações certifica estabilidade do hardware.

## Responsividade — 25/11/2026

O aplicativo agora aceita alocações positivas menores que seu mínimo solicitado, evitando alternância entre 589 e 620 px de altura útil. Títulos e valores longos podem quebrar linha. O projeto UiChecks verifica as 14 abas em quatro tamanhos de conteúdo (56 combinações), incluindo 820×589 e 820×620; build e execução aprovados. O botão Cancelar desabilitado permanece legível no tema escuro.

A primeira execução registrou mudança de tamanho nativo na aba Maker; a repetição completa passou sem alteração da correção de resize. Os tamanhos de conteúdo são impostos pelo teste, não garantias de tamanho nativo: o gerenciador de janelas pode determinar outra área. Capturas revisadas das abas de sistema e Benchmark em 820 px; teclado e dropdown no tamanho mínimo nativo continuam pendentes. Nenhuma carga real de GPU foi iniciada nesta rodada.
