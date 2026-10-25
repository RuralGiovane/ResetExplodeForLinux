# Validação gráfica Linux

> Cópia versionada da checklist do diretório de trabalho, registrada no aceite desta entrega.

Estado final: todas as etapas deste roteiro foram aprovadas pelo usuário, incluindo instância única, inventário de rede e endereços/gateways/DNS. As notas de cada etapa abaixo preservam o histórico.

Estado em 24/10/2026: validação gráfica básica aprovada pelo usuário, incluindo bloqueio de início de benchmarks/exportações durante exportação e substituição de JSON existente. Build Avalonia e suíte completa aprovados em 23/10. Usuário confirmou também os três cenários de benchmark: cancelamento, bloqueio na pré-checagem térmica e fechamento durante execução. Roteiro gráfico concluído em 24/10/2026. Testes automatizados térmicos reexecutados em 24/10 com sucesso (binário existente, `--thermal-only`).

O ambiente do agente apresentou bloqueio de acesso a X11/Wayland na tentativa anterior. Execute os cenários gráficos em um terminal da sessão do usuário.

## Abrir a aplicação

```bash
cd /home/ruralgiovane/Downloads/teste/Reset/ResetExplodeForLinux
dotnet run --project Linux/ResetExplode.csproj -c Release --no-build
```

## Roteiro e resultados esperados

Marque somente após observar o resultado; registre mensagens e diferenças encontradas.

- [X] A janela abre com logo e navegação entre Sistema e sensores e Benchmark. Redimensionar e rolar permite acessar todos os cartões e botões.
- [X] CPU/RAM e sensores deixam o estado inicial e atualizam. Recursos indisponíveis apresentam ausência/erro sem travar a navegação.
- [X] ATUALIZAR PORTAS, ATUALIZAR ALIMENTAÇÃO e ATUALIZAR DISPOSITIVOS concluem e permitem nova atualização. Inventário vazio é aceitável quando não há dispositivos correspondentes.
- [X] EXPORTAR MAKER HTML abre o diálogo. Cancelar informa “Exportação cancelada.” e libera os botões. Repetir e salvar em arquivo novo informa sucesso; abrir o HTML no navegador mostra os dados e a data da coleta.
- [X] EXPORTAR MAKER JSON salva JSON válido com `reportType: linux-serial`. Ausência de portas é apresentada como inventário vazio, sem inventar identificação USB.
- [X] Exportações HTML/JSON de alimentação, armazenamento e benchmarks também concluem. Histórico vazio é aceitável. Os botões de benchmark/exportação ficam indisponíveis enquanto uma exportação está em andamento e voltam após salvar ou cancelar.
- [X] Substituir um arquivo de teste maior pelo relatório JSON deixa um JSON válido, sem conteúdo residual. Usar somente arquivo descartável criado para este teste. Confirmado posteriormente pelo usuário ao testar o mesmo destino; o nome sugerido com data e hora gera arquivos distintos.
- [X] Fechar a janela em repouso encerra o processo e devolve o terminal. Reabrir funciona.
- [X] Iniciar atualização de inventário e fechar a janela encerra sem exceção ou processo preso.

Para conferir um JSON exportado, substitua o caminho pelo arquivo escolhido:

```bash
python3 -m json.tool /caminho/relatorio.json > /dev/null
```

## Execução e cancelamento de benchmarks — aprovados pelo usuário em 24/10/2026

- [X] Com proteção térmica habilitada e leitura válida abaixo do limite, iniciar um benchmark e cancelar. A interface volta ao estado disponível; o histórico registra cancelamento sem pontuação parcial.
- [X] Quando a pré-checagem térmica bloquear o teste, a interface apresenta o motivo e permanece utilizável.
- [X] Fechar durante um benchmark solicita cancelamento e aguarda o encerramento, sem exceção.

Essas verificações não substituem validação de hardware serial conectado, SMART real, NVML ou metodologia dos benchmarks completos.

### Como executar esta etapa

1. Abra a aba Benchmark, mantenha “Monitorar durante os benchmarks” habilitado e anote o limite original. Para os testes de execução, aguarde uma leitura térmica válida abaixo desse limite.
2. Clique em **1 THREAD** e, com o teste em execução, em **CANCELAR**. Espere “Cancelado pelo usuário. Sem pontuação.”, confira o histórico e confirme que os botões voltam a ficar disponíveis.
3. Para a pré-checagem, se a temperatura atual permitir, escolha temporariamente um limite igual ou inferior à leitura (o campo aceita 60–100 °C) e tente iniciar **1 THREAD**. Espere “NÃO INICIADO”, o motivo e “Sem pontuação.”. Restaure o limite original ao terminar. Se a CPU estiver abaixo de 60 °C, registre este cenário como não exercitado; não provoque aquecimento para alcançar o limite. Os testes automatizados cobrem essa condição, mas não confirmam sua apresentação gráfica.
4. Com o limite original restaurado e leitura válida abaixo dele, inicie **1 THREAD** novamente e feche a janela durante a execução. Confirme que o terminal fica disponível e que reabrir funciona. Confira no histórico o cancelamento ao fechar, sem pontuação.

Registre os resultados nos três itens acima. Se houver bloqueio térmico antes de iniciar, isso não valida o cancelamento de um benchmark em execução; anote a mensagem observada.

## Instância única — aprovada pelo usuário

Usar a versão recompilada. Em dois terminais, a partir de `ResetExplodeForLinux/`, execute o mesmo comando:

```bash
dotnet Linux/bin/Release/net8.0/ResetExplode.dll
```

- [X] Com o app aberto no primeiro terminal, abrir no segundo não cria outra janela; o segundo comando retorna e informa que solicitou ativação.
- [X] Minimizar o app e abrir novamente restaura a janela. Anotar caso o desktop impeça trazer a janela à frente.
- [X] Com o aviso SMART aberto, abrir novamente não fecha nem confirma o aviso e não inicia consulta administrativa.
- [X] Durante um benchmark, abrir novamente mantém o teste e histórico na mesma instância, sem iniciar outro teste.
- [X] Fechar normalmente e abrir novamente funciona, sem bloqueio residual.

Resultado/mensagem:

## Inventário de rede — aprovado pelo usuário

Reabrir a versão recompilada e acessar **Sistema e sensores → REDE · INTERFACES**.

- [X] O cartão mostra interfaces locais, incluindo `lo`, com estado, link, MTU e contadores. Campos não fornecidos pelo sistema aparecem como Indisponível.
- [X] ATUALIZAR REDE atualiza o horário e os dados, libera uma nova atualização ao terminar e mantém a navegação utilizável.
- [X] Contadores de bytes/pacotes refletem nova leitura após uso normal da rede. Eles são acumulados; não representam velocidade de download/upload.
- [X] Redimensionar e rolar permite acessar os dados de todas as interfaces e os cartões seguintes.
- [X] Fechar após solicitar atualização de rede encerra normalmente; reabrir funciona.

Conferência opcional no terminal, sem enviar tráfego:

```bash
cd /home/ruralgiovane/Downloads/teste/Reset/ResetExplodeForLinux
dotnet Linux/SmokeTests/bin/Release/net8.0/ResetExplode.SmokeTests.dll --live-network
```

Leituras feitas em momentos diferentes podem ter contadores diferentes. Velocidade do link indisponível em Wi-Fi/loopback é aceitável quando o driver não fornece o atributo.

Resultado/mensagem:

## Endereços, gateways e DNS — aprovados pelo usuário

Reabrir a versão recompilada e acessar **Sistema e sensores → REDE · INTERFACES**.

- [X] Cada interface mostra seus endereços IPv4/IPv6 e prefixos (`/24`, `/64`, etc.) quando disponíveis. `lo` pode mostrar `127.0.0.1/8` e `::1/128`.
- [X] Gateways aparecem nas interfaces correspondentes quando reportados. Interfaces sem gateway mostram Nenhum informado; falhas de leitura mostram Indisponível.
- [X] DNS · CONFIGURAÇÃO LOCAL corresponde aos servidores explícitos do arquivo resolv.conf. Um endereço local como `127.0.0.53` é válido e pode representar o resolvedor do sistema.
- [X] ATUALIZAR REDE atualiza endereços, gateways, DNS e contadores sem travar a interface. Se a configuração mudar durante uso normal, a próxima atualização reflete os novos dados.
- [X] Endereços IPv6 longos e múltiplos endereços continuam legíveis ao redimensionar/rolar; fechar e reabrir funciona.

Conferência local opcional (sem enviar tráfego):

```bash
ip -brief address
ip route show
ip -6 route show
cat /etc/resolv.conf
```

O app mostra os gateways expostos pela API local; não apresenta todas as tabelas de roteamento nem determina qual rota cada conexão utiliza. DNS pode ser intermediado pelo sistema ou substituído por VPNs/aplicativos. No ambiente do agente, netlink está bloqueado; o usuário confirmou a leitura real de endereços/gateways na sua sessão.

Resultado/mensagem:

Todos os itens desta etapa foram marcados e confirmados pelo usuário. Alterações preparadas para commit manual, conforme a entrega anterior.
