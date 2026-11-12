# PTR_Treinamento_Nivel2_Integracao

## Integrações de CEP e CNPJ — Dynamics 365 / Dataverse

## 1. Identificação do projeto

| Item | Descrição |
|---|---|
| Solução | `PTR_Treinamento_Nivel2_Integracao` |
| Plataforma | Microsoft Dynamics 365 / Microsoft Dataverse |
| Automação | Power Automate |
| Linguagem dos plug-ins | C# |
| Tabela principal | Conta (`account`) |
| Integração de CEP | ViaCEP |
| Integração de CNPJ | BrasilAPI |

## 2. Objetivo

O projeto tem como objetivo automatizar consultas de informações cadastrais e de endereço na tabela **Conta** do Dynamics 365.

São disponibilizadas duas integrações independentes:

- **Consulta de CEP:** utiliza o serviço ViaCEP para obter informações de endereço a partir do CEP informado.
- **Consulta de CNPJ:** utiliza a BrasilAPI para obter informações cadastrais de uma empresa a partir do CNPJ informado.

As consultas são executadas por meio de Custom APIs e plug-ins C#, enquanto os fluxos do Power Automate são responsáveis por controlar o processamento e atualizar os registros no Dataverse.

A solução também contempla tratamento de falhas, controle de reprocessamento, prevenção de recursividade e proteção contra resultados obsoletos.

## 3. Arquitetura da solução

A arquitetura utiliza os seguintes componentes:

| Componente | Responsabilidade |
|---|---|
| Dataverse | Armazenamento dos registros e informações consultadas. |
| Power Automate | Monitoramento de alterações, controle do processamento e atualização das Contas. |
| Custom APIs | Disponibilização das operações de consulta. |
| Plug-ins C# | Comunicação HTTP com os serviços externos e processamento das respostas. |
| JavaScript | Validações e atualização dos dados exibidos nos formulários, conforme a implementação adotada. |

### 3.1. Visão geral

Cada integração possui seu próprio fluxo, Custom API e plug-in.

**Integração de CEP:**

1. O usuário informa ou altera o CEP da Conta.
2. O fluxo identifica a alteração.
3. O CEP é normalizado e comparado com o CEP anteriormente processado.
4. O fluxo atualiza o status para Pendente.
5. A Custom API `ptr_ConsultarCep` é executada.
6. O plug-in `ConsultarCep` consulta o ViaCEP.
7. Os dados retornados são disponibilizados nos parâmetros de saída.
8. O fluxo verifica se o CEP da Conta continua igual ao consultado.
9. Se a consulta for válida, os campos de endereço são atualizados.

**Integração de CNPJ:**

1. O usuário informa ou altera o CNPJ da Conta.
2. O fluxo identifica a alteração.
3. O CNPJ é normalizado e comparado com o CNPJ anteriormente processado.
4. O fluxo atualiza o status para Pendente.
5. A Custom API `ptr_ConsultarCnpj` é executada.
6. O plug-in `ConsultarCnpj` consulta a BrasilAPI.
7. Os dados cadastrais são disponibilizados nos parâmetros de saída.
8. O fluxo verifica se o CNPJ da Conta continua igual ao consultado.
9. Se a consulta for válida, os campos cadastrais são atualizados.

## 4. Estrutura dos componentes

### 4.1. Custom APIs

| Custom API | Parâmetro de entrada | Serviço externo |
|---|---|---|
| `ptr_ConsultarCep` | `ptr_CEP` | ViaCEP |
| `ptr_ConsultarCnpj` | `ptr_CNPJ` | BrasilAPI |

As duas operações são independentes e possuem parâmetros próprios de entrada e saída.

### 4.2. Plug-ins

Namespace utilizado:

`Pentare.Treinamento.Plugin.Contas`

| Classe | Interface | Responsabilidade |
|---|---|---|
| `ConsultarCep` | `IPlugin` | Consulta o ViaCEP e processa os dados do endereço. |
| `ConsultarCnpj` | `IPlugin` | Consulta a BrasilAPI e processa os dados cadastrais. |

Os plug-ins utilizam:

- `IPluginExecutionContext` para acessar os parâmetros da Custom API.
- `ITracingService` para registrar informações de execução.
- `HttpClient` para realizar requisições HTTP.
- `DataContractJsonSerializer` para desserializar as respostas JSON.
- Tratamento de exceções para falhas de comunicação, timeout e processamento.

## 5. Integração de CEP

### 5.1. Identificação

| Item | Descrição |
|---|---|
| Custom API | `ptr_ConsultarCep` |
| Plug-in | `ConsultarCep` |
| Serviço | [ViaCEP](https://viacep.com.br/) |
| Método HTTP | GET |
| Endpoint | `https://viacep.com.br/ws/{cep}/json/` |

### 5.2. Campos da tabela Conta

| Campo | Nome lógico | Finalidade |
|---|---|---|
| CEP | `address1_postalcode` | CEP informado pelo usuário. |
| Logradouro | `address1_line1` | Logradouro retornado pelo ViaCEP. |
| Bairro | `address1_line2` | Bairro retornado pelo ViaCEP. |
| Cidade | `address1_city` | Cidade retornada pelo ViaCEP. |
| Estado/UF | `address1_stateorprovince` | Unidade federativa. |
| CEP Processado | `ptr_cepprocessado` | Controle do CEP submetido à consulta. |
| Status Consulta CEP | `ptr_statusconsultacep` | Situação do processamento. |
| Última Consulta CEP | `ptr_ultimaconsultacep` | Data e hora da consulta. |

### 5.3. Normalização e validação

O plug-in remove caracteres não numéricos antes de realizar a consulta.

Exemplo:

- CEP informado: `69911-778`
- CEP normalizado: `69911778`

A consulta somente é realizada quando o CEP normalizado possui oito dígitos.

### 5.4. Parâmetros da Custom API

**Entrada:**

| Parâmetro | Tipo |
|---|---|
| `ptr_CEP` | String |

**Saída:**

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `ptr_Sucesso` | Boolean | Indica se a consulta foi bem-sucedida. |
| `ptr_Cep` | String | CEP retornado pelo serviço. |
| `ptr_Logradouro` | String | Logradouro encontrado. |
| `ptr_Bairro` | String | Bairro encontrado. |
| `ptr_Cidade` | String | Cidade encontrada. |
| `ptr_UF` | String | Unidade federativa. |
| `ptr_Mensagem` | String | Mensagem sobre o resultado da consulta. |

### 5.5. Status da consulta

A coluna `ptr_statusconsultacep` utiliza as seguintes situações:

| Status | Significado |
|---|---|
| Pendente | Consulta iniciada e ainda não concluída. |
| Validado | CEP encontrado e endereço obtido. |
| Inválido | CEP não encontrado ou reprovado na validação. |
| Erro | Falha técnica durante o processamento. |

Os valores numéricos das opções Choice devem ser conferidos no ambiente Dataverse.

### 5.6. Tratamento de falhas

O plug-in contempla:

- CEP com quantidade incorreta de dígitos.
- CEP não encontrado na base do ViaCEP.
- Timeout da requisição.
- Falhas HTTP ou de comunicação.
- Erros durante a desserialização e o processamento da resposta.

O resultado é disponibilizado pelos parâmetros de saída da Custom API, permitindo que o fluxo classifique a situação da consulta.

## 6. Integração de CNPJ

### 6.1. Identificação

| Item | Descrição |
|---|---|
| Custom API | `ptr_ConsultarCnpj` |
| Plug-in | `ConsultarCnpj` |
| Serviço | [BrasilAPI](https://brasilapi.com.br/) |
| Método HTTP | GET |
| Endpoint | `https://brasilapi.com.br/api/cnpj/v1/{cnpj}` |

### 6.2. Campos da tabela Conta

| Campo | Nome lógico | Finalidade |
|---|---|---|
| CNPJ | `ptr_cnpj` | CNPJ informado pelo usuário. |
| CNPJ Processado | `ptr_cnpjprocessado` | Controle do CNPJ submetido à consulta. |
| Status Consulta CNPJ | `ptr_statusconsultacnpj` | Situação do processamento. |
| Última Consulta CNPJ | `ptr_ultimaconsultacnpj` | Data e hora da consulta. |
| Razão Social | `ptr_razaosocial` | Razão social retornada pela BrasilAPI. |
| Situação Cadastral | `ptr_situacaocadastral` | Situação cadastral da empresa. |
| Mensagem Consulta CNPJ | `ptr_mensagemconsultacnpj` | Mensagem sobre o resultado da consulta. |

### 6.3. Normalização do CNPJ

O plug-in utiliza o método `NormalizarCnpj()` para remover caracteres que não sejam letras ou números e converter o resultado para maiúsculo.

Exemplo:

- CNPJ informado: `12.ABC.345/01DE-35`
- CNPJ normalizado: `12ABC34501DE35`

A normalização permite trabalhar com o CNPJ sem pontuação na URL da BrasilAPI.

**Importante:** a normalização não substitui a validação do formato e dos dígitos verificadores. A validação deve ser realizada separadamente antes da consulta, conforme a regra aplicável ao formato de CNPJ utilizado.

### 6.4. Parâmetros da Custom API

**Entrada:**

| Parâmetro | Tipo |
|---|---|
| `ptr_CNPJ` | String |

**Saída:**

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `ptr_SucessoConsulta` | Boolean | Indica se a consulta foi bem-sucedida. |
| `ptr_Cnpj` | String | CNPJ retornado pela BrasilAPI. |
| `ptr_RazaoSocial` | String | Razão social da empresa. |
| `ptr_SituacaoCadastral` | Integer | Código da situação cadastral. |
| `ptr_MensagemCnpj` | String | Mensagem sobre o resultado da consulta. |

Os nomes dos parâmetros devem corresponder exatamente aos nomes definidos na Custom API do Dataverse.

### 6.5. Situação cadastral

A situação cadastral retornada pela BrasilAPI é representada por um código numérico.

Na configuração considerada para o projeto:

| Código | Situação |
|---|---|
| 1 | NULA |
| 2 | ATIVA |
| 3 | SUSPENSA |
| 4 | INAPTA |
| 8 | BAIXADA |

A coluna `ptr_situacaocadastral` utiliza Choice. Os valores numéricos devem ser conferidos na definição da coluna no Dataverse.

### 6.6. Tratamento de falhas HTTP

O plug-in contempla tratamento específico para os seguintes códigos:

| Código HTTP | Tratamento |
|---|---|
| 200 | Processa a resposta e disponibiliza os dados. |
| 400 | Requisição inválida. |
| 404 | CNPJ não encontrado. |
| 429 | Limite de consultas atingido. |
| 503 | Serviço temporariamente indisponível. |
| Outros | Falha na consulta, com registro do código HTTP. |

Também são tratados:

- Timeout da requisição.
- Falhas de comunicação.
- Resposta vazia.
- Erros de desserialização ou processamento.

O `HttpClient` utiliza timeout explícito de 15 segundos e identifica a aplicação por meio do cabeçalho User-Agent.

## 7. Power Automate

As integrações são controladas por fluxos separados, permitindo que a consulta de CEP e a consulta de CNPJ sejam executadas independentemente.

### 7.1. Gatilhos

Os fluxos utilizam o gatilho do Dataverse:

**Quando uma linha é adicionada, modificada ou excluída.**

A tabela monitorada é **Contas (`account`)**.

Os atributos monitorados são:

- CEP: `address1_postalcode`.
- CNPJ: `ptr_cnpj`.

A configuração do gatilho deve limitar as execuções às operações e colunas necessárias.

### 7.2. Controle de reprocessamento

Cada integração possui uma coluna de controle:

- `ptr_cepprocessado`
- `ptr_cnpjprocessado`

Antes de executar a consulta, o fluxo compara o valor atual com o valor processado, após a normalização.

Se os valores forem iguais, uma nova consulta não é necessária.

Se forem diferentes, o fluxo prossegue com o processamento.

Esse mecanismo evita consultas repetidas quando o próprio fluxo atualiza outros campos da Conta.

### 7.3. Status Pendente

Antes de executar a Custom API, o fluxo atualiza o status da respectiva consulta para **Pendente**.

Isso permite identificar que existe um processamento em andamento.

### 7.4. Execução das Custom APIs

Cada fluxo executa sua própria Custom API:

| Fluxo | Custom API | Entrada |
|---|---|---|
| Consulta CEP | `ptr_ConsultarCep` | `ptr_CEP` |
| Consulta CNPJ | `ptr_ConsultarCnpj` | `ptr_CNPJ` |

Os parâmetros de saída são utilizados para decidir quais informações devem ser gravadas na Conta.

### 7.5. Proteção contra resultados obsoletos

Após a consulta externa, o fluxo obtém novamente o registro da Conta e compara o valor atual com aquele enviado à Custom API.

Se o usuário tiver alterado o CEP ou CNPJ durante a consulta, o resultado antigo deve ser descartado.

Essa verificação evita que uma resposta referente a um dado anterior sobrescreva informações de uma alteração mais recente.

### 7.6. Tratamento de falhas

Os fluxos devem distinguir:

- Resultado válido.
- Dado inválido ou não encontrado.
- Falha técnica.
- Timeout.
- Serviço externo indisponível.

As falhas da execução da Custom API também devem ser tratadas por meio da configuração **Executar após (Configure run after)**.

O identificador da Conta deve ser obtido diretamente do gatilho, para que o tratamento de erro não dependa de uma ação intermediária que possa ter falhado.

## 8. Atualização dos formulários

As atualizações feitas pelo Power Automate são gravadas diretamente no Dataverse. Entretanto, um formulário já aberto pode continuar exibindo os dados anteriores.

Para atualizar a interface, pode ser utilizada uma biblioteca JavaScript na tabela Conta.

A atualização dos dados do formulário é realizada por:

```javascript
formContext.data.refresh(false);
```

O parâmetro `false` evita salvar automaticamente as alterações pendentes antes da atualização.

Na implementação de monitoramento considerada para o CEP:

- Intervalo de verificação: 5 segundos.
- Limite de monitoramento: 5 minutos.
- Estados finais: Validado, Inválido e Erro.
- A atualização deve respeitar a existência de alterações não salvas no formulário.

A aplicação desse mecanismo à consulta de CNPJ depende da configuração correspondente na biblioteca e no formulário.

## 9. Estrutura de status e processamento

| Integração | Status | Significado |
|---|---|---|
| CEP | Pendente | Consulta em andamento. |
| CEP | Validado | Endereço obtido. |
| CEP | Inválido | CEP não encontrado ou inválido. |
| CEP | Erro | Falha técnica. |
| CNPJ | Pendente | Consulta em andamento. |
| CNPJ | Validado | Dados cadastrais obtidos. |
| CNPJ | Inválido | CNPJ inválido ou não encontrado. |
| CNPJ | Erro | Falha técnica. |

Os valores internos das opções Choice devem ser conferidos no ambiente.

## 10. Configuração e implantação

Para disponibilizar a solução em outro ambiente, verifique os seguintes componentes.

### 10.1. Dataverse

- [ ] Importar a solução `PTR_Treinamento_Nivel2_Integracao`.
- [ ] Conferir a tabela Conta (`account`).
- [ ] Conferir as colunas personalizadas de CEP.
- [ ] Conferir as colunas personalizadas de CNPJ.
- [ ] Conferir os tipos das colunas.
- [ ] Conferir os valores das opções Choice.
- [ ] Conferir os formulários e os campos disponibilizados.

### 10.2. Custom APIs e plug-ins

- [ ] Conferir a Custom API `ptr_ConsultarCep`.
- [ ] Conferir a Custom API `ptr_ConsultarCnpj`.
- [ ] Conferir os parâmetros de entrada e saída.
- [ ] Registrar os tipos de plug-in correspondentes.
- [ ] Associar cada plug-in à respectiva Custom API.
- [ ] Verificar as permissões de execução necessárias.
- [ ] Habilitar o rastreamento de plug-ins durante testes e diagnóstico.

### 10.3. Power Automate

- [ ] Importar ou configurar o fluxo de consulta de CEP.
- [ ] Importar ou configurar o fluxo de consulta de CNPJ.
- [ ] Configurar as conexões do Dataverse.
- [ ] Conferir os identificadores das ações utilizadas nas expressões.
- [ ] Conferir os parâmetros das Custom APIs.
- [ ] Verificar as condições de reprocessamento.
- [ ] Configurar os tratamentos de falha e timeout.
- [ ] Ativar os fluxos.

### 10.4. JavaScript

- [ ] Publicar as bibliotecas utilizadas nos formulários.
- [ ] Conferir os nomes lógicos dos campos.
- [ ] Configurar os eventos OnLoad e OnChange necessários.
- [ ] Habilitar a passagem do contexto de execução quando exigido.
- [ ] Publicar as personalizações.

### 10.5. Conexões e permissões

As conexões utilizadas pelos fluxos devem estar ativas e autorizadas no ambiente de destino.

As permissões precisam permitir as operações necessárias sobre a tabela Conta e a execução das Custom APIs.

## 11. Cenários de teste

### 11.1. Consulta de CEP

| Cenário | Resultado esperado |
|---|---|
| Informar CEP válido | Endereço preenchido e status Validado. |
| Informar CEP inexistente | Status Inválido. |
| Informar CEP com hífen | Normalização e consulta corretas. |
| Informar CEP com quantidade incorreta de dígitos | Consulta não executada. |
| Salvar sem alterar o CEP | Nenhuma nova consulta. |
| Alterar CEP durante a consulta | Resultado antigo descartado. |
| ViaCEP indisponível | Status Erro. |
| Timeout | Status Erro. |
| Fluxo atualizar outros campos | Não realizar consulta repetida para o mesmo CEP. |

### 11.2. Consulta de CNPJ

| Cenário | Resultado esperado |
|---|---|
| Informar CNPJ válido | Razão social e situação cadastral preenchidas. |
| Informar CNPJ com pontuação | Normalização e consulta corretas. |
| Informar CNPJ inexistente | Status Inválido. |
| Informar formato inválido | Consulta não deve prosseguir após a validação. |
| Salvar sem alterar o CNPJ | Nenhuma nova consulta. |
| Alterar CNPJ durante a consulta | Resultado antigo descartado. |
| BrasilAPI retornar HTTP 400 | Requisição classificada como inválida. |
| BrasilAPI retornar HTTP 404 | CNPJ não encontrado. |
| BrasilAPI retornar HTTP 429 | Limite de consultas atingido. |
| BrasilAPI retornar HTTP 503 | Serviço indisponível. |
| Timeout | Status Erro. |
| Falha de comunicação | Status Erro. |

## 12. Considerações técnicas e limitações

- As consultas dependem da disponibilidade dos serviços externos ViaCEP e BrasilAPI.
- O plug-in utiliza `HttpClient` com timeout explícito e tratamento de exceções.
- As Custom APIs separam a consulta externa da responsabilidade de atualização da Conta.
- O Power Automate é responsável pela atualização dos dados no Dataverse.
- O uso de uma Custom API chamada pelo fluxo não garante, por si só, que a requisição HTTP esteja fora de uma transação do Dataverse. O contexto de execução deve ser avaliado.
- Evite execuções em massa sem controle de volume e frequência.
- A normalização do CNPJ não substitui a validação do formato e dos dígitos verificadores.
- Os valores Choice precisam corresponder às opções configuradas no ambiente.
- O plug-in de CNPJ utiliza `0` como valor de situação cadastral em retornos de falha na implementação atual. Esse valor não deve ser tratado como uma opção válida do Choice sem que exista essa opção. O fluxo precisa tratar o cenário de falha sem gravar um código inválido, ou a saída deve ser ajustada para permitir valor nulo.
- Em caso de falha técnica, evite apagar informações cadastrais previamente válidas sem uma regra de negócio que determine esse comportamento.
- Os nomes internos das ações do Power Automate podem variar entre ambientes e versões dos fluxos. As expressões devem ser conferidas após a importação.

## 13. Serviços externos

| Serviço | Finalidade | Documentação |
|---|---|---|
| ViaCEP | Consulta de endereços por CEP. | https://viacep.com.br/ |
| BrasilAPI | Consulta de dados cadastrais por CNPJ. | https://brasilapi.com.br/docs |

## 14. Conclusão

A solução `PTR_Treinamento_Nivel2_Integracao` amplia as funcionalidades do Dynamics 365 por meio de integrações com serviços externos.

A consulta de CEP automatiza o preenchimento de endereço das Contas, enquanto a consulta de CNPJ permite obter informações cadastrais de empresas.

A arquitetura combina Dataverse, Power Automate, Custom APIs, plug-ins C# e JavaScript, mantendo separadas as responsabilidades de consulta, processamento, atualização e apresentação dos dados.

O projeto também contempla mecanismos para evitar reprocessamentos desnecessários, tratar falhas de comunicação e impedir que resultados antigos sobrescrevam informações mais recentes.

---

**Projeto:** `PTR_Treinamento_Nivel2_Integracao`  
**Documentação:** README.md  
**Versão do documento:** 2.0  
**Plataforma:** Microsoft Dynamics 365 / Dataverse