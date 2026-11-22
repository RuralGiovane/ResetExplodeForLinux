# Validação e evidências

Atualizado em 22/11/2026. Documentação não substitui um resultado de teste.

## Camadas de validação

| Camada | O que demonstra | O que não demonstra |
| --- | --- | --- |
| Build | Fontes/recursos compilam no ambiente usado | Correção funcional ou visual |
| SmokeTests | Cenários automatizados e fixtures | Suporte a todo hardware |
| Diagnóstico real opt-in | Uma coleta/operação naquele ambiente | Universalidade ou aceite da UI |
| Roteiro manual | Interações explicitamente conferidas | Medições não realizadas |
| Série de benchmarks | Repetibilidade/metodologia daquela série | Equivalência automática entre SOs |

## Evidência recente

Build Avalonia/Core/Hardware/SmokeTests e suíte completa passaram após adicionar stress CPU/RAM, usando UseAppHost=false. O stress foi testado com cargas pequenas e temperatura simulada, incluindo corrupção de dados, cancelamento e watchdog.

Layout inicial de Estabilidade foi capturado na sessão gráfica em 936×993. O gerenciador de janelas impediu validar os dois tamanhos pretendidos. O verificador exibiu uma exceção de D-Bus no primeiro fechamento; a segunda execução não reproduziu. Não há causa comprovada nem correção declarada.

## Roteiros preservados

- [Interface](../VALIDACAO_GRAFICA.md)
- [Hardware](../VALIDACAO_HARDWARE.md)
- [Metodologia CPU/RAM](../VALIDACAO_BENCHMARKS.md)
- [Disco/carregamento](../VALIDACAO_DISCO.md)
- [Estabilidade e stress](../VALIDACAO_ESTABILIDADE_MEMORIA.md)

Aceites de outubro são históricos; não marcar as entregas de novembro como aprovadas por herança. Maker conectado, carga prolongada, equivalência de benchmarks, build Windows e parte de estabilidade real seguem pendentes.

## Modos úteis

Executar a DLL de SmokeTests com --stress-only, --stability-only, --thermal-only, --report-only ou --serial-only restringe verificações. --report-only não cobre por si só o relatório de estabilidade: ele é exercitado em --stability-only e na suíte completa.

Diagnósticos --live-* têm efeitos próprios: alguns consultam hardware, outros geram tráfego ou arquivos de teste. Ler [Program.cs](../../Linux/SmokeTests/Program.cs) antes de usá-los. Não confundir passagem de uma fixture com execução real.
