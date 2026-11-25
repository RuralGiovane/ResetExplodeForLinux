# Paridade e próximas entregas

Estado em 25/11/2026. Não há percentual global de conclusão: presença de arquivo e número de telas não medem equivalência.

| Domínio | Linux atual | Lacuna |
| --- | --- | --- |
| Sistema/sensores | /proc, /sys, DMI, hwmon, cpufreq | RAM por módulo/SPD e cobertura ampliada |
| Dashboard | Resumo da máquina e leituras ao vivo | Gráficos/histórico visual e composição Windows completa |
| GPU | NVML NVIDIA somente leitura | Detalhamento e cobertura AMD/Intel equivalentes |
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
