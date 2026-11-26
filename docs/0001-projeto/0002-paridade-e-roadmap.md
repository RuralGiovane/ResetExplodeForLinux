# Paridade e próximas entregas

Estado em 26/11/2026. Não há percentual global de conclusão: presença de arquivo e número de telas não medem equivalência.

| Domínio | Linux atual | Lacuna |
| --- | --- | --- |
| Sistema/sensores | /proc, /sys, DMI, hwmon, cpufreq | RAM por módulo/SPD e cobertura ampliada |
| Dashboard | Resumo da máquina e leituras ao vivo | Gráficos/histórico visual e composição Windows completa |
| GPU | NVML/DRM/hwmon e inventário PCI, driver, VBIOS, memória, PCIe e API Vulkan | Especificações comerciais adicionais e validação real AMD/Xe |
| CPU/RAM benchmark | Render/simulação compartilhados, latência/banda | Repetibilidade e comparação Windows/Linux |
| Disco/carregamento | I/O direto Linux x64 | Metodologia/filas/tamanhos diferentes do Windows |
| Armazenamento/SMART | Inventário, montagens, smartctl, exportação | Validar por dispositivo/controlador |
| Energia | Baterias/fontes e exportação | Controles/planos equivalentes |
| Rede | Inventário, ICMP, DNS e HTTP limitado | Bufferbloat e capacidade máxima |
| Estabilidade | EDAC, journal, classificação, deltas, relatórios | Aceite manual registrado; cobertura depende do hardware |
| Stress | CPU/RAM com prazo e proteção térmica | CPU/RAM com aceite; GPU Vulkan com seletor e prévia ao vivo |
| Overclock | Sem escrita de clocks/potência | Fluxo OC e restauração |
| Maker | Identificação, grupos e aliases | Placa conectada e ferramentas corretivas |
| Jogos | Sem launcher Linux equivalente | Biblioteca, perfis, FPS, gargalo e overlay |
| Ferramentas | Limpeza de memória do próprio processo | Otimizador global, inicialização, apps, drivers e RGB |
| Distribuição | Build Linux em CI | Pacotes, instalação e releases Linux |

## Ordem de continuidade

O histórico registra a orientação de concluir funcionalidades obrigatórias antes da distribuição. Stress CPU/RAM e estabilidade receberam aceite manual do usuário em 22/11/2026, conforme o [roteiro de estabilidade](../VALIDACAO_ESTABILIDADE_MEMORIA.md). A frente GPU iniciou com inventário Vulkan e diagnóstico CLI; pipeline gráfico offscreen e sensor NVML associado por PCI implementados, integrados à interface com seleção explícita, histórico e exportações.

Frente iniciada: GPU/stress gráfico. Prévia gráfica e correção de oscilação em janelas menores implementadas, com regressão gráfica das 14 abas em quatro tamanhos de conteúdo. Continua pendente a ampliação da validação real de GPU (cargas prolongadas e sensores adicionais). Outros blocos possíveis: completar rede, jogos/overlay e ferramentas de sistema. Esta lista descreve lacunas, não uma escolha já aprovada de biblioteca, backend ou cronograma.

Ao iniciar uma frente: localizar a referência Windows, definir o comportamento Linux esperado, registrar diferenças inevitáveis de plataforma e escrever critérios de aceite. Não considerar uma substituição funcional automaticamente equivalente em desempenho.

Em 26/11, reabertura após fechamento sob carga e novo cancelamento foram verificados na GTX 1650, com histórico isolado e relatórios pelo builder. O teste solicitado de 300 s atingiu o limite de 80 °C e foi interrompido corretamente; não recebeu aceite de conclusão prolongada. Inventário ampliado e regressão das 56 combinações de layout aprovados. Permanecem validação nativa de teclado/dropdown e diálogo de salvar, carga prolongada concluída, hardware AMD/Xe e camada Khronos. Detalhes no [roteiro GPU](../VALIDACAO_GPU.md).

## Retomada combinada — 26/11/2026

O usuário escolheu trabalhar por etapas, primeiro nos tópicos 1 (validação GPU) e 2 (cobertura e especificações). Continuar nessas frentes antes de iniciar os demais blocos do roadmap.

1. Concluir o salvamento de relatórios HTML/JSON pelo diálogo e conferir os arquivos. Teclado, foco e dropdown passaram em 945×1029, com aceite de legibilidade do usuário; cancelamento do diálogo também passou. O usuário confirmou a abertura das opções de salvar, mas não salvou os arquivos. O novo `UiChecks --gpu-interaction` permite concluir essa checagem guiada com histórico sintético isolado e sem carga. O mínimo nativo de 820×620 continua pendente. Ver [registro da validação](../0007-historico/0010-2026-11-26-interacao-gpu-e-dialogos.md).
2. Retomar a validação prolongada apenas com condições térmicas adequadas, preservando o limite de 80 °C. A tentativa de 300 s foi interrompida nesse limite; não registrar como concluída nem elevar o limite para forçar aprovação. Conferir histórico e relatórios de uma execução concluída e de interrupções.
3. Validar em hardware AMD e Intel/Xe quando houver acesso. Esta máquina tem GTX 1650 e Intel UHD/i915; esta última não expõe temperatura própria e deve continuar bloqueada para stress. Fixtures não substituem essa validação física. Conferir também a camada Khronos quando disponível.
4. Avaliar as especificações comerciais ainda ausentes (núcleos, litografia e tipo/barramento de memória), com fontes verificáveis e associação inequívoca ao modelo. O inventário atual já apresenta PCI, driver, revisão, VBIOS, memória reportada, PCIe e modelo/tipo/API Vulkan; não inferir dados ausentes.

Ponto de partida: [registro desta entrega](../0007-historico/0009-2026-11-26-inventario-e-validacao-gpu.md) e [checklist de validação](../VALIDACAO_GPU.md). Builds e suíte net8.0 aprovados; 56 combinações de layout e fechamento/reabertura sob carga aprovados. Evidências temporárias desta sessão ficaram em `/tmp/reset-gpu-validation-20261126` e `/tmp/reset-gpu-layout-20261126`; esses diretórios podem desaparecer, portanto o registro versionado é a referência de continuidade.
