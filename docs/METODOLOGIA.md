# Metodologia de cálculo — K2 Marketing

Este documento descreve a origem de cada número exibido na plataforma e as regras que garantem indicadores confiáveis. A mesma informação está disponível na página **Metodologia**, dentro do sistema.

## 1. Origem dos dados

| Origem (`DataSource`) | Como entra no sistema |
|---|---|
| **Manual** | Digitado na tela de Métricas ou pelo botão *Registrar gasto* do orçamento |
| **CsvImport** | Importado por CSV, após prévia, validação e confirmação |
| **Demo** | Gerado pelo seed de demonstração |
| **Integration** | Reservado para conectores futuros (nenhum ativo nesta versão) |

Cada linha de métrica e cada conversão guarda a sua origem, e o dashboard mostra quantas linhas do período vieram de cada fonte.

**Calculados:** CTR, CPC, CPM, CPL, CPA, ROAS, consumo de orçamento e variações em relação ao período anterior. Esses valores **nunca são gravados** e são recalculados pelo servidor a cada consulta.

## 2. Fórmulas

| Indicador | Fórmula | Denominador zero |
|---|---|---|
| CTR (%) | (Cliques ÷ Impressões) × 100 | `null` ("—") |
| CPC | Investimento ÷ Cliques | `null` |
| CPM | (Investimento ÷ Impressões) × 1.000 | `null` |
| CPL | Investimento ÷ Leads | `null` |
| CPA | Investimento ÷ Conversões | `null` |
| ROAS | Receita atribuída ÷ Investimento | `null` |
| Consumo do orçamento | Realizado ÷ Planejado × 100 | `null` |

- Valores monetários usam `decimal` (C#) e `numeric(18,2)` (PostgreSQL). Indicadores são arredondados em 4 casas (*away from zero*).
- **Agregação:** períodos e grupos (cliente, plataforma, campanha) somam os valores brutos primeiro e só então aplicam a fórmula. Exemplo: com 10 cliques em 100 impressões num dia e 100 cliques em 10.000 impressões no outro, o CTR do período é 110 ÷ 10.100 = **1,09%**, e não a média simples (5,5%).
- Implementação: `BackEnd/src/K2Marketing.Domain/MetricCalculator.cs`, coberta por testes unitários.

## 3. Regras contra dupla contagem

1. **Investimento:** a única fonte é `CampaignMetrics.Spend`. O realizado de cada orçamento é sincronizado a partir das métricas do período (`BudgetSyncService`).
2. **Registrar gasto** no orçamento **soma** o valor à métrica de nível campanha do dia (criando a linha se ela não existir), dentro de uma transação. Não existe um segundo lançamento de gasto.
3. **Uma linha de métrica por campanha + anúncio + dia**, garantida por índice único `NULLS NOT DISTINCT`. Na importação CSV, linhas já existentes podem ser ignoradas ou substituídas, e linhas repetidas dentro do próprio arquivo são descartadas.
4. **Leads e conversões da plataforma** (reportados nas métricas) e os **cadastrados no CRM** são exibidos separadamente e nunca somados.
5. **Conversões do CRM** têm uma chave única por campanha (`EventKey`):
   - com ID externo (pedido/transação): `ext:{id}`, sem diferenciar maiúsculas de minúsculas;
   - tipos Lead, Cadastro e Contato com lead vinculado: `lead:{leadId}:{tipo}`, de modo que o mesmo lead não converte duas vezes no mesmo tipo;
   - demais casos: impressão digital SHA-256 de lead + tipo + data/hora (minuto) + receita.
6. **Três números distintos no CRM:** conversões gerais (todos os eventos), leads únicos convertidos (cada lead conta uma vez) e vendas (somente o tipo Compra).
7. **Receita atribuída** (métricas), **receita de vendas** (CRM) e **receita da agência** são conceitos diferentes. Os orçamentos tratam apenas do investimento em mídia do cliente.

## 4. Alertas de orçamento

Limites de **80%, 90% e 100%**, comparados com valores exatos (79,999% não dispara o alerta de 80%). Cada limite gera **uma** notificação por orçamento. Se o gasto cair (por exemplo, numa métrica corrigida), o limite pode voltar a ser notificado ao ser atingido de novo.

## 5. Comparação de campanhas

- Uma campanha só é **elegível para destaques** com pelo menos 100 cliques, 7 dias com dados e investimento maior que zero.
- O **ROAS** só é destacado quando todas as campanhas elegíveis têm receita atribuída.
- Há avisos quando objetivos ou plataformas diferem, quando falta volume e quando falta receita: **maior ROAS não significa automaticamente a melhor campanha.**
- Empates não geram destaque.

## 6. Observações do relatório executivo

As observações são geradas por regras objetivas sobre os números do período: variação de investimento ou ROAS de ±10% ou mais, CTR abaixo de 1% com volume relevante, maior ROAS entre campanhas elegíveis, CPA acima de 1,5× a média, campanhas com gasto e sem conversão, orçamentos com consumo de 90% ou mais, diferença de 20% ou mais entre leads da plataforma e do CRM, e presença de dados de demonstração.

## 7. Limitações conhecidas

- O **alcance agregado** é a soma do alcance diário informado e pode conter as mesmas pessoas em dias diferentes.
- A data local usada em "hoje" segue o fuso `America/Sao_Paulo`.
