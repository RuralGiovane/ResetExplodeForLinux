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


## Testes ativos de rede — 25/10/2026 (aprovado)

- [X] O cartão de rede mostra destino, TESTAR LATÊNCIA, CANCELAR TESTE e explicação das métricas, com texto legível ao redimensionar.
- [X] Campo vazio, domínio e URL geram orientação sem travar; um novo teste pode ser iniciado.
- [X] Testar `127.0.0.1` e, se disponível, `::1`: observar respostas, RTT e jitter; erro de permissão deve aparecer como indisponibilidade, sem perda calculada.
- [X] Informar o IP real do gateway ou outro destino conhecido: conferir destino, 10 tentativas, métricas e estados. Ausência de resposta não comprova falha da internet.
- [X] Cancelar durante execução: sem resultado parcial, controles liberados, nova execução possível.
- [X] Benchmarks e exportações ficam indisponíveis durante teste; teste de rede fica bloqueado durante benchmark/exportação.
- [X] Fechar durante execução: app encerra após cancelar; reabrir permite novo teste e não restaura resultados antigos.

Resultado: usuário confirmou “tudo validado” para esta etapa. Aceite abrange o roteiro acima; não foram fornecidos valores individuais das medições.


## Exportação de rede — 25/10/2026 (aprovado)

- [X] Abrir o app e exportar HTML/JSON antes de executar teste: inventário disponível e nenhum resultado ICMP; conferir aviso de IPs/gateways/DNS.
- [X] Executar teste até concluir e exportar: conferir destino, contagem, perda, RTT, jitter, estados e datas nos dois formatos.
- [X] Iniciar novo teste e cancelar: exportação informa cancelamento sem métricas do teste anterior. Repetir com entrada inválida: erro sem métricas antigas.
- [X] Cancelar o diálogo de salvar: status de cancelamento e botões liberados; nova exportação funciona.
- [X] Substituir um JSON maior por um relatório menor: arquivo continua JSON válido, sem conteúdo residual.
- [X] Enquanto exporta, benchmarks/teste ICMP ficam bloqueados; durante teste ICMP/benchmark, exportação de rede fica bloqueada.
- [X] Fechar durante exportação/diálogo: concluir ou cancelar diálogo e confirmar encerramento sem erro; reabrir normalmente.
- [X] HTML abre no navegador com conteúdo legível, interfaces, DNS, metodologia e estados indisponíveis claramente apresentados.

Resultado: usuário confirmou “tudo validado” para esta etapa, abrangendo o roteiro de exportação acima.


## Download/upload HTTP — 25/10/2026 (aprovado)

- [X] Conferir destino Cloudflare, 10 MB de download + 5 MB de upload sintético, overhead adicional e limite de 20 s por etapa antes de iniciar.
- [X] Iniciar TESTAR DOWNLOAD / UPLOAD e aguardar: conferir taxas em Mbit/s, 10.000.000/5.000.000 bytes, durações, servidor e horário. Mensagem de erro deve ficar explícita se o serviço não estiver acessível.
- [X] Exportar HTML/JSON depois do sucesso: conferir mesmas taxas/bytes/durações, metodologia e datas. JSON usa esquema 2 e contém `throughput`; ICMP/inventário continuam disponíveis.
- [X] Repetir e cancelar durante transferência: sem taxas antigas/parciais, relatório com `throughput: null` e estado de cancelamento. Nova execução funciona.
- [X] Se possível, testar sem conectividade: erro explícito sem taxa zero fictícia; recuperar conexão e repetir com sucesso.
- [X] ICMP, benchmarks e exportações ficam bloqueados durante transferência, e transferência fica bloqueada enquanto essas ações rodam.
- [X] Fechar durante transferência: encerramento após cancelamento, sem travamento; reabrir permite novo teste e não restaura resultados antigos.
- [X] Redimensionar/rolar o cartão e confirmar legibilidade dos controles, aviso e resultados.

Resultado: usuário confirmou “tudo validado” para o roteiro desta etapa. Cenários condicionais seguem a disponibilidade indicada no roteiro; valores individuais das medições não foram informados.


## Resolução de nomes — 25/10/2026 (aprovado)

- [X] Conferir campo de domínio, botões e explicação do resolvedor/cache; abrir a aplicação não inicia consulta.
- [X] Resolver `localhost` e um domínio conhecido: conferir nome normalizado, endereços e tempo observado. Falha deve ser explícita se o ambiente não resolver.
- [X] Campo vazio, IP, URL e nome com porta/caminho são rejeitados; corrigir permite nova consulta.
- [X] Testar nome internacional se disponível: nome ASCII normalizado e resultado/erro legível.
- [X] Exportar HTML/JSON após sucesso: nome, endereços, datas e duração presentes; JSON esquema 3 com `dnsResolution`, mantendo ICMP/throughput.
- [X] Nova tentativa inválida ou com falha não exporta a resolução anterior: resultado nulo, estado explícito.
- [X] Se a consulta durar o suficiente, cancelar/fechar durante execução: sem resultado tardio/parcial, controles liberados ao terminar; reabrir normalmente. Prazo de 5 s solicita cancelamento e depende do resolvedor.
- [X] Conferir bloqueios recíprocos entre resolução, ICMP, download/upload, benchmarks e exportações, além de legibilidade ao redimensionar.

Resultado: usuário confirmou a validação desta etapa. Cenários condicionais seguem a disponibilidade indicada no roteiro; domínios e valores individuais não foram informados.


## Organização em sub-abas — 25/10/2026 (pendente)

- [ ] Abrir Sistema e sensores: Visão geral selecionada, sem a antiga lista longa de cartões.
- [ ] Acessar Computador, Sensores, Rede, Armazenamento, Alimentação e Maker: cada sub-aba mostra seu cartão e controles.
- [ ] Redimensionar até 820 × 620 e maximizar: cabeçalhos acessíveis, com quebra de linha quando necessário, sem rolagem horizontal do conteúdo.
- [ ] Rolar uma seção longa: título e navegação permanecem visíveis. Navegar pelas sub-abas com teclado e mouse, com foco/seleção identificáveis.
- [ ] Alterar filtro em Sensores e campos em Rede, ir a outra sub-aba/Benchmark e voltar: seleção, campos e resultados preservados durante a sessão.
- [ ] Na Rede, resultado ICMP aparece junto aos controles de latência; DNS, transferência e exportações continuam acessíveis.
- [ ] Atualizar inventários e testar uma exportação: diálogos, status e bloqueios continuam funcionando nas respectivas sub-abas.
- [ ] Durante teste de rede, trocar de sub-aba e voltar para cancelar; ao fechar durante teste, encerramento continua funcionando.


## Visão geral com resumo do sistema — 25/10/2026 (aprovado)

Organização das sub-abas aprovada pelo usuário; este roteiro cobre o resumo solicitado em seguida.

- [X] Conferir logo/nome da máquina e sistema, kernel, arquitetura, processador, threads, placa-mãe, BIOS e memória total contra a aba Computador.
- [X] Conferir GPU/fonte com Sensores; nome do driver genérico não é apresentado como modelo inferido. Indisponibilidade deve ficar explícita.
- [X] Conferir capacidade/usado em `/` com Armazenamento; atualizar inventário atualiza também o resumo.
- [X] CPU, temperatura e RAM seguem atualizando; alternar entre sub-abas e Benchmark preserva a navegação e os campos.
- [X] Conferir legibilidade em janela reduzida e maximizada, com quebra de nomes longos e rolagem do cartão.

Resultado: usuário aprovou o resumo (“ficou perfeito”) e solicitou o commit da entrega. Organização das sub-abas já aprovada anteriormente.
