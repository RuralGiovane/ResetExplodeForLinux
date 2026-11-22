# Paridade e próximas entregas

Estado em 22/11/2026. Não há percentual global de conclusão: presença de arquivo e número de telas não medem equivalência.

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
| Estabilidade | EDAC, journal, classificação, deltas, relatórios | Validação real das últimas etapas |
| Stress | CPU/RAM com prazo e proteção térmica | Carga prolongada/interação; GPU |
| Overclock | Sem escrita de clocks/potência | Fluxo OC e restauração |
| Maker | Identificação, grupos e aliases | Placa conectada e ferramentas corretivas |
| Jogos | Sem launcher Linux equivalente | Biblioteca, perfis, FPS, gargalo e overlay |
| Ferramentas | Limpeza de memória do próprio processo | Otimizador global, inicialização, apps, drivers e RGB |
| Distribuição | Build Linux em CI | Pacotes, instalação e releases Linux |

## Ordem de continuidade

O histórico registra a orientação de concluir funcionalidades obrigatórias antes da distribuição. A entrega mais recente adicionou stress CPU/RAM; o [roteiro de estabilidade](../VALIDACAO_ESTABILIDADE_MEMORIA.md) mantém suas verificações reais abertas.

Próximos blocos possíveis: GPU/stress gráfico, completar rede, jogos/overlay e ferramentas de sistema. Esta lista descreve lacunas, não uma escolha já aprovada de biblioteca, backend ou cronograma.

Ao iniciar uma frente: localizar a referência Windows, definir o comportamento Linux esperado, registrar diferenças inevitáveis de plataforma e escrever critérios de aceite. Não considerar uma substituição funcional automaticamente equivalente em desempenho.
