# Validação de hardware Linux

> Cópia versionada da checklist do diretório de trabalho, registrada no aceite desta entrega.

## Estado atual — aprovação do usuário

Fluxo administrativo SMART integralmente confirmado pelo usuário na conversa: aviso, Cancelar/Escape/fechamento do aviso, Prosseguir/autenticação, cancelamento da autenticação, consulta com dados reais, exportação e fechamento durante o fluxo. Os itens da nova checklist foram marcados a partir dessa confirmação. GPU/NVML também aprovada. Maker conectado permanece pendente por falta de dispositivo; exportação do inventário disponível aprovada.

As seções abaixo preservam o histórico do diagnóstico anterior, superado pela validação administrativa.

## Revisão dos resultados do usuário — 24/10/2026

- **GPU/NVML: aprovada pelo usuário.** Reconhecimento no terminal e coleta/apresentação com atualização no app confirmados. A falha anterior do agente não se reproduziu nessa validação.
- **Maker: exportação aprovada para o inventário disponível.** Identificação, conexão/desconexão e aliases com dispositivo real permanecem pendentes por falta de hardware; não são falhas confirmadas.
- **SMART: pendente de diagnóstico.** A tentativa do usuário retornou LEITURA INCOMPLETA por falha de abertura/identificação, sem avaliação válida. O aviso agregado do app não distingue permissão, nome de destino ou suporte. Consulta na UI e exportação com métricas reais continuam desmarcadas.

### Próximo diagnóstico SMART

Executar no terminal normal para identificar o destino e obter a mensagem original do smartctl:

```bash
lsblk -d -o NAME,TYPE,MODEL
ls -l /dev/nvme0 /dev/nvme0n1
smartctl -a /dev/nvme0n1
```

O último comando usa o disco `nvme0n1` observado anteriormente; adaptar ao nome de disco mostrado no lsblk se necessário. Para reproduzir o destino usado pelo app, escolher o disco inteiro, sem sufixo de partição como `p1` ou `p2`. Informar o comando usado e a mensagem de erro; não é necessário compartilhar números de série. Não alterar permissões antes de identificar a causa.

## Resultado do agente — 24/10/2026

Diagnósticos executados com o binário existente, sem recompilação ou alterações de código:

- CPU e sensores: leitura real funcionou (CPU 2,8%, temperatura 55 °C na amostra; hwmon e cpufreq apresentaram valores).
- SMART: `nvme0n1` aparece em `/sys/block`, mas `/dev/nvme0n1` não está disponível neste ambiente. O leitor retornou LEITURA INCOMPLETA, sem avaliação SMART válida. Saúde do disco não validada.
- Serial: nenhuma porta ttyUSB/ttyACM observada; associação a grupos parcialmente indisponível. Hardware conectado não validado.
- GPU: NVML retornou código 9; `nvidia-smi -L` também falhou ao comunicar com o driver. Nenhum `/dev/nvidia*` disponível neste ambiente. Isso não determina o funcionamento da GPU na sessão normal do usuário.
- Testes simulados executados pelo modo `--live-telemetry`: serial/USB/aliases, armazenamento/montagens, alimentação e telemetria passaram.

## Executar no terminal da sessão do usuário

Na pasta do projeto:

```bash
cd /home/ruralgiovane/Downloads/teste/Reset/ResetExplodeForLinux
```

### 1. Armazenamento / SMART

```bash
lsblk -d -o NAME,TYPE,MODEL
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-smart nvme0n1
```

Se o disco tiver outro nome, use o nome mostrado pelo lsblk, sem `/dev/`. O comando consulta informações; não inicia autotestes nem altera configurações. Pode acordar um disco em repouso. Não consulte zram como disco físico.

- [ ] O leitor apresenta avaliação/métricas reais, ou uma limitação explícita de permissão/suporte. Anotar qual ocorreu; uma limitação não aprova a leitura real.
- [ ] No app, selecionar o mesmo dispositivo em armazenamento e clicar em CONSULTAR SMART mostra resultado compatível com o terminal.
- [ ] Exportar o relatório de armazenamento apresenta os dados da consulta e sua data.

Resultado/mensagem: dá esse erro, mas eu corrigi o disk dispositivo, bate n o/dev/nvme0 e a partição da distro linux

"""
Estado: LEITURA INCOMPLETA
Dispositivo não pôde ser aberto/identificado. Verifique permissões e suporte do controlador.
O dispositivo não forneceu uma avaliação SMART válida.
Uma avaliação aprovada não garante ausência de falhas. Métricas ausentes não são tratadas como zero
"""

### 2. GPU NVIDIA (se disponível)

```bash
nvidia-smi -L
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-telemetry
```

- [X] NVIDIA reconhecida no terminal e métricas disponíveis apresentadas pelo coletor.
- [X] Na tela de sensores, os dados disponíveis da GPU aparecem e atualizam; métricas não suportadas são apresentadas como indisponíveis.

Se ambos falharem, registrar as mensagens. Se nvidia-smi funcionar e o coletor falhar, registrar essa diferença. Se não houver GPU NVIDIA, marcar como não aplicável.

Resultado/mensagem:

### 3. Maker / serial (se houver dispositivo USB serial)

Com um dispositivo conhecido conectado:

```bash
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-serial
```

- [ ] Porta ttyUSB/ttyACM aparece no terminal e no app após ATUALIZAR PORTAS.
- [ ] Identificação USB, driver e aliases aparecem quando fornecidos pelo sistema.
- [ ] Desconectar e atualizar remove a porta; reconectar e atualizar volta a apresentá-la.
- [X] HTML/JSON Maker corresponde ao inventário coletado.

O inventário não testa comunicação serial. Se não houver dispositivo para testar, registrar como pendente por falta de hardware.

Resultado/mensagem: Não tenho nenhum dispositivo para conectar

## Diagnóstico confirmado pelo usuário — permissão SMART

A consulta direta retornou `Smartctl open device: /dev/nvme0n1 failed: Permission denied`. Falta de permissão para abrir esse dispositivo confirmada nessa tentativa. Isso não é uma avaliação de saúde nem comprova erro de partição. O app executa smartctl sem elevação, portanto a leitura real permanece pendente no contexto atual.

Próxima verificação no terminal do usuário, elevando apenas a consulta:

```bash
sudo smartctl -a /dev/nvme0n1
```

Registrar se a consulta retorna dados ou outro erro. Sucesso com sudo valida o acesso privilegiado via smartctl, mas não aprova automaticamente a integração do app executado como usuário comum. Não executar a interface inteira como root nem alterar permissões do dispositivo para essa verificação.

## Consulta administrativa no app — nova validação

Usuário confirmou que `sudo smartctl -a /dev/nvme0n1` retorna dados SMART. Leitura privilegiada no terminal e novo fluxo gráfico abaixo aprovados pelo usuário.

- [X] CONSULTAR SMART mostra aviso com o dispositivo e os botões Cancelar e Prosseguir.
- [X] Cancelar, Escape ou fechar o aviso não abre autenticação nem consulta o disco; o botão fica disponível novamente.
- [X] Prosseguir abre a autenticação do sistema quando exigida; autorizar apresenta os dados SMART no app.
- [X] Cancelar a autenticação apresenta cancelamento e libera nova tentativa.
- [X] Exportar armazenamento após consulta bem-sucedida inclui métricas e data.
- [X] Fechar durante autorização/consulta encerra sem deixar a interface presa.

Iniciar a versão recompilada com o comando habitual. A sessão precisa de um agente gráfico polkit; não abrir o aplicativo como root. A janela do sistema pode citar `timeout`, responsável por limitar a duração da consulta SMART.
