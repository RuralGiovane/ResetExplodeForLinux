# Contratos com o sistema e serviços externos

## Interfaces locais

| Interface | Uso | Falha esperada |
| --- | --- | --- |
| /proc, /sys, /etc/os-release | Sistema, sensores e inventários | Ausência, permissão, parsing, remoção |
| libnvidia-ml/NVML | Métricas NVIDIA somente leitura | Biblioteca/dispositivo/métrica indisponível |
| libvulkan / Vulkan 1.1 | Pipeline gráfico offscreen via Silk.NET | Driver, fila, formato ou identidade PCI indisponível |
| vulkaninfo --summary | Inventário textual opcional | Ausência, prazo de 10 s, saída inválida/limitada |
| libc e statx | I/O direto Linux x64 | ABI/alinhamento/sistema de arquivos não suportado |
| smartctl | SMART JSON de dispositivo escolhido | Permissão, timeout, bits de alerta, saída inválida |
| journalctl | Avisos/erros do kernel na inicialização atual | Sem systemd/acesso, timeout ou amostra limitada |

Fontes: [LinuxNvml](../../Linux/Hardware/Interop/LinuxNvml.cs), [LinuxDirectFile](../../Linux/Core/Storage/LinuxDirectFile.cs), [LinuxSmartReader](../../Linux/Hardware/Storage/LinuxSmartReader.cs), [LinuxKernelLogReader](../../Linux/Hardware/Stability/LinuxKernelLogReader.cs).

## Processos auxiliares

Argumentos são passados separadamente, sem shell. SMART recebe nome de dispositivo validado, não uma linha de comando arbitrária. A consulta normal tem prazo de 15 s. O fluxo administrativo usa pkexec, timeout de 15 s com kill-after de 2 s e janela de espera externa de até 2 min, incluindo autenticação.

Journal usa kernel da inicialização atual, prioridades 0–4, até 500 entradas recentes, JSON e no-pager; prazo de 10 s. Há limites de saída e de texto por mensagem. Cancelamento/finalização aguardam ou encerram o processo conforme o leitor.

Não executar toda a interface como root para substituir a autorização pontual do SMART.

## Rede externa

ICMP usa destino explícito; DNS usa o resolvedor do sistema; throughput usa HTTP com Cloudflare (10 MB recebidos + 5 MB sintéticos enviados). São operações ativas, canceláveis e separadas da coleta local.

O app não controla disponibilidade/capacidade do servidor externo. Uma resposta ausente de ICMP não comprova falha da internet. Não há backend do projeto nem contrato de autenticação/conta.

## Associação térmica da GPU

O backend exige PCI Vulkan único e físico, e consulta o mesmo endereço via NVML; nunca associa por índice/nome. Leituras com mais de 2 s, erro, ausência ou temperatura no limite impedem novas submissões. Frames em execução são aguardados antes de liberar recursos. Ver [decisão Vulkan](../0006-decisoes/0004-backend-vulkan.md).
