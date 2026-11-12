# Pentare.Caso

Biblioteca JavaScript para personalizar o formulário de **Ocorrências
(Case / `incident`)** em uma aplicação model-driven do Microsoft
Dynamics 365 / Dataverse.

A biblioteca utiliza o contexto do formulário (`formContext`), eventos
`OnChange`, Dataverse Web API e operações assíncronas para apresentar
informações relacionadas ao cliente e alertar sobre possíveis títulos
duplicados.

## 1. Funcionalidades

### 1.1. Atualização de informações do cliente

Quando o campo Cliente (`customerid`) é alterado, a biblioteca:

1.  Identifica o registro selecionado e seu tipo (`account` ou
    `contact`).
2.  Limpa os campos calculados antes de iniciar as consultas.
3.  Consulta a quantidade de Ocorrências ativas relacionadas ao cliente.
4.  Consulta a data de criação da Ocorrência ativa mais recente.
5.  Atualiza os campos do formulário quando as consultas terminam e a
    resposta ainda corresponde ao cliente selecionado.

Campos preenchidos:

  -----------------------------------------------------------------------
  Campo lógico                        Finalidade
  ----------------------------------- -----------------------------------
  `ptr_qtdcasosativos`                Quantidade de Ocorrências ativas do
                                      cliente.

  `ptr_ultimocasoem`                  Data de criação da Ocorrência ativa
                                      mais recente.
  -----------------------------------------------------------------------

A consulta considera registros com `statecode eq 0`.

### 1.2. Verificação de títulos duplicados

Quando o Cliente (`customerid`) ou o Título (`title`) é alterado, a
biblioteca consulta as Ocorrências ativas do cliente e verifica se
existe outra Ocorrência com o mesmo título.

A comparação:

-   remove espaços em branco no início e no final (`trim`);
-   converte os títulos para letras minúsculas (`toLowerCase`);
-   desconsidera a própria Ocorrência em edição, comparando o GUID do
    registro atual com o GUID de cada resultado;
-   percorre as páginas retornadas pela Web API usando `nextLink`.

Quando encontra uma duplicidade, apresenta uma notificação `WARNING` no
formulário. O aviso é informativo e **não bloqueia o salvamento**.

A verificação considera somente Ocorrências ativas e não substitui uma
validação no servidor. Portanto, não garante unicidade em criações
realizadas por integrações, importações ou chamadas diretas à API.

## 2. Estrutura da biblioteca

O código está organizado no namespace `Pentare.Caso`.

  ----------------------------------------------------------------------------------------------------------------
  Propriedade / método                                                         Responsabilidade
  ---------------------------------------------------------------------------- -----------------------------------
  `formContext`                                                                Armazena o contexto do formulário
                                                                               em execução.

  `contadorTituloAtual`                                                        Identifica a requisição mais
                                                                               recente de verificação de título.

  `contadorClienteAtual`                                                       Identifica a requisição mais
                                                                               recente de consulta de informações
                                                                               do cliente.

  `OnLoad(executionContext)`                                                   Obtém o contexto do formulário e
                                                                               registra os eventos `OnChange`.

  `OnChange()`                                                                 Registra os manipuladores dos
                                                                               campos Cliente e Título.

  `OnSave()`                                                                   Reservado para futuras validações
                                                                               no evento de salvamento; atualmente
                                                                               está vazio.

  `VerificaClienteAtual(contador, idCliente)`                                  Confirma se a consulta do cliente
                                                                               ainda é válida.

  `trataAlteracaoDeCliente()`                                                  Consulta e atualiza as informações
                                                                               relacionadas ao cliente.

  `BuscaQtdCasosAtivos(idCliente, customerType)`                               Consulta a quantidade de
                                                                               Ocorrências ativas.

  `BuscaUltimoCasoAberto(idCliente)`                                           Consulta a data de criação da
                                                                               Ocorrência ativa mais recente.

  `BuscaTitulos(idCliente)`                                                    Recupera os títulos das Ocorrências
                                                                               ativas, incluindo páginas
                                                                               seguintes.

  `VerificaRequisicaoAtual(contadorVerificacao, idCliente, textoComparacao)`   Confirma se a verificação de título
                                                                               ainda corresponde aos valores
                                                                               atuais do formulário.

  `VerificarTituloExistente()`                                                 Procura títulos duplicados e
                                                                               apresenta um aviso.
  ----------------------------------------------------------------------------------------------------------------

Os métodos assíncronos retornam `Promise` e utilizam `async/await`.

### Documentação do código com JSDoc

As funções e propriedades da biblioteca também possuem comentários no
padrão **JSDoc**. Essa documentação descreve a finalidade de cada método,
seus parâmetros, tipos de retorno e comportamento assíncrono.

As principais tags utilizadas são:

- `@namespace`: identifica o namespace `Pentare.Caso`.
- `@description`: explica a responsabilidade da biblioteca ou do método.
- `@param`: documenta os parâmetros recebidos, incluindo seus tipos.
- `@returns`: descreve o valor ou a `Promise` retornada.
- `@async`: identifica funções assíncronas.
- `@type`: informa o tipo de uma propriedade.
- `@throws`: documenta exceções que podem ser propagadas ao chamador.

Exemplo do padrão utilizado:

```javascript
/**
 * Consulta a quantidade de Ocorrências ativas relacionadas a um cliente.
 *
 * @async
 * @param {string} idCliente - GUID do cliente consultado.
 * @param {string} customerType - Tipo do cliente (account ou contact).
 * @returns {Promise<number>} Quantidade de Ocorrências ativas encontradas.
 */
BuscaQtdCasosAtivos: async function (idCliente, customerType) {
    // Implementação
}
```

Para gerar um arquivo XML de documentação a partir dos comentários JSDoc,
é necessário utilizar uma ferramenta de geração compatível com JavaScript;
a opção de geração XML de documentação do Visual Studio destinada a
comentários XML de C# não gera automaticamente documentação JSDoc.


## 3. Eventos do formulário

A configuração esperada é:

  Evento     Função                                Campo
  ---------- ------------------------------------- --------------
  OnLoad     `Pentare.Caso.OnLoad`                 Formulário
  OnChange   Registrado pelo método `OnChange()`   `customerid`
  OnChange   Registrado pelo método `OnChange()`   `title`

O método `OnLoad` chama `OnChange`, que registra os manipuladores por
meio de `addOnChange`.

### Configuração no Dynamics 365

1.  Abra a solução que contém a tabela Ocorrência (`incident`).
2.  Abra o formulário principal utilizado pela aplicação.
3.  Na seção de bibliotecas de formulário, adicione o recurso JavaScript
    que contém `Pentare.Caso`.
4.  Na seção de eventos do formulário, configure o evento **OnLoad**
    para chamar `Pentare.Caso.OnLoad`.
5.  Marque a opção para transmitir o contexto de execução como primeiro
    parâmetro.
6.  Salve e publique as alterações.
7.  Abra uma Ocorrência e teste a alteração dos campos Cliente e Título.

Como os eventos `OnChange` são registrados pelo próprio `OnLoad`, não é
necessário cadastrar manualmente as funções `trataAlteracaoDeCliente` e
`VerificarTituloExistente` nos campos, desde que o `OnLoad` esteja
configurado corretamente.

> O método `OnSave` está vazio na implementação atual. Não é necessário
> registrá-lo enquanto não houver lógica de salvamento a executar.

## 4. Consultas ao Dataverse

### 4.1. Quantidade de Ocorrências ativas

`BuscaQtdCasosAtivos` utiliza `fetch` para chamar o endpoint da
Dataverse Web API:

``` text
/api/data/v9.2/incidents
```

A consulta seleciona `incidentid`, filtra pelo cliente
(`_customerid_value`) e por `statecode eq 0`, e solicita a contagem com
`$count=true`.

O total é obtido da propriedade `@odata.count` da resposta JSON. Se a
propriedade não estiver presente, o método retorna `0`.

### 4.2. Ocorrência ativa mais recente

`BuscaUltimoCasoAberto` utiliza:

-   `$select=createdon`;
-   filtro pelo cliente e por `statecode eq 0`;
-   `$orderby=createdon desc`;
-   `$top=1`.

O método retorna a data `createdon` do primeiro resultado ou `null`
quando não há registros.

### 4.3. Títulos das Ocorrências

`BuscaTitulos` utiliza `Xrm.WebApi.retrieveMultipleRecords` e recupera
`incidentid` e `title`.

A consulta percorre todas as páginas enquanto houver `nextLink`,
acumulando os resultados em um único array. Isso permite comparar o
título com todos os registros retornados, não apenas com a primeira
página.

## 5. Controle de concorrência das consultas

As consultas são assíncronas. O usuário pode alterar o Cliente ou o
Título antes de uma consulta anterior terminar. Sem uma proteção, uma
resposta antiga poderia atualizar o formulário com dados que já não
correspondem aos valores selecionados.

A biblioteca utiliza dois contadores independentes:

-   `contadorClienteAtual`: controla as consultas iniciadas após
    alterações no Cliente;
-   `contadorTituloAtual`: controla as verificações iniciadas após
    alterações no Cliente ou no Título.

Cada execução incrementa seu contador e guarda o valor em uma variável
local. Depois do `await`, a função de validação compara esse valor com o
contador global e também confere os valores atuais do formulário.

Exemplo conceitual:

1.  A consulta A é iniciada com contador `1`.
2.  O usuário altera o campo e inicia a consulta B, com contador `2`.
3.  A consulta B termina e pode atualizar o formulário.
4.  A consulta A termina depois, mas seu contador `1` não corresponde ao
    contador atual `2`; sua resposta é descartada.

Os contadores não precisam ser zerados no `OnLoad`. Mantê-los
incrementais evita reutilizar identificadores enquanto operações
anteriores ainda podem estar em andamento.

> Os contadores impedem o processamento de respostas obsoletas, mas não
> cancelam as requisições HTTP já iniciadas.

## 6. Notificações do formulário

A biblioteca utiliza `formContext.ui.setFormNotification` para
apresentar avisos que não devem impedir o salvamento.

  ---------------------------------------------------------------------------
  Identificador               Nível                   Finalidade
  --------------------------- ----------------------- -----------------------
  `erro_consulta_casos`       `WARNING`               Falha ao consultar as
                                                      informações do cliente.

  `erro_verificacao_titulo`   `WARNING`               Falha ao verificar
                                                      títulos duplicados.

  `titulo_duplicado`          `WARNING`               Existe outra Ocorrência
                                                      ativa com o mesmo
                                                      título para o cliente.
  ---------------------------------------------------------------------------

As notificações são removidas com `formContext.ui.clearFormNotification`
quando a condição correspondente deixa de ser aplicável ou quando uma
consulta termina com sucesso, conforme o fluxo implementado.

**Importante:** `setFormNotification` não bloqueia o salvamento,
inclusive quando utilizado com nível `ERROR`. Para impedir o salvamento,
é necessária uma estratégia de validação própria, como
`preventDefault()` no evento `OnSave` ou uma validação no servidor. O
método `control.setNotification`, por sua vez, bloqueia o salvamento
enquanto a notificação de campo estiver ativa.

## 7. Tratamento de erros

As consultas assíncronas são executadas dentro de blocos `try/catch`.

Quando ocorre uma falha:

-   a resposta é verificada para garantir que ainda pertence à execução
    atual;
-   os campos de quantidade de casos e data do último caso são limpos
    quando a consulta do cliente falha;
-   uma mensagem de aviso é apresentada ao usuário;
-   detalhes técnicos são enviados ao console por `console.error`.

A biblioteca não deve apresentar ao usuário detalhes internos da
resposta HTTP ou da exceção.

## 8. Dependências e permissões

A implementação depende de:

-   formulário model-driven da tabela `incident`;
-   campos padrão `customerid` e `title`;
-   campos personalizados `ptr_qtdcasosativos` e `ptr_ultimocasoem`;
-   objeto global `Xrm`;
-   `Xrm.Utility.getGlobalContext().getClientUrl()`;
-   `Xrm.WebApi.retrieveMultipleRecords`;
-   suporte a `fetch`, `Promise`, `async/await` e recursos modernos de
    JavaScript.

O usuário que executa a biblioteca precisa ter privilégios de leitura
sobre Ocorrências e sobre os dados necessários para que o Dataverse
retorne os registros. A segurança do Dataverse continua sendo aplicada
às consultas.

## 9. Cenários de teste

  -----------------------------------------------------------------------
  Cenário                             Resultado esperado
  ----------------------------------- -----------------------------------
  Selecionar uma Conta com            Quantidade e data da Ocorrência
  Ocorrências ativas                  ativa mais recente preenchidas.

  Selecionar um Contato com           Informações relacionadas ao Contato
  Ocorrências ativas                  preenchidas.

  Selecionar cliente sem Ocorrências  Quantidade igual a `0` e data do
  ativas                              último caso vazia.

  Limpar o campo Cliente              Campos calculados limpos e
                                      notificações de consulta removidas
                                      conforme a implementação.

  Alterar rapidamente o Cliente       Somente a resposta referente ao
                                      cliente atual pode atualizar os
                                      campos.

  Informar título já utilizado em     Aviso de duplicidade apresentado;
  outra Ocorrência ativa do mesmo     salvamento permitido.
  cliente                             

  Informar título utilizado apenas    Nenhum aviso de duplicidade.
  por outro cliente                   

  Informar título com diferenças de   Títulos são considerados
  maiúsculas/minúsculas               equivalentes.

  Informar título com espaços no      Espaços nas extremidades são
  início ou no final                  desconsiderados.

  Editar uma Ocorrência sem alterar   O próprio registro não é
  seu título                          considerado duplicidade.

  Alterar rapidamente o título        Somente a verificação referente ao
                                      título atual pode apresentar
                                      resultado.

  Cliente sem Ocorrências na primeira Consulta de títulos termina sem
  página                              duplicidade, após percorrer as
                                      páginas disponíveis.

  Falha na consulta                   Aviso apresentado e erro registrado
                                      no console.
  -----------------------------------------------------------------------

## 10. Pontos de atenção e melhorias futuras

Os itens abaixo são observações sobre a implementação atual e possíveis
evoluções; não representam funcionalidades já implementadas.

1.  **Parâmetro `customerType`:** é recebido por `BuscaQtdCasosAtivos`,
    mas não é utilizado no corpo da função. Pode ser removido da
    assinatura e da chamada, caso não haja intenção de utilizá-lo.
2.  **Limpeza de aviso ao apagar o título:** em
    `VerificarTituloExistente`, a função retorna quando o título está
    vazio antes de limpar `titulo_duplicado`. Na implementação atual,
    isso pode manter um aviso anterior visível depois que o título for
    apagado. Mova a limpeza da notificação para antes dessa validação.
3.  **Validação de duplicidade no servidor:** a verificação JavaScript é
    informativa e sujeita a concorrência. Se a regra precisar ser
    obrigatória para todas as origens de gravação, implemente também uma
    validação no Dataverse.
4.  **Cancelamento de requisições:** os contadores descartam respostas
    obsoletas, mas não cancelam a operação de rede. Caso isso seja
    necessário, avalie `AbortController` para chamadas compatíveis.
5.  **Registro repetido de eventos:** se o `OnLoad` puder ser executado
    mais de uma vez na mesma instância de formulário, avalie evitar o
    registro duplicado de manipuladores `OnChange`.

## 11. Manutenção e publicação

Após modificar o JavaScript:

1.  Salve o arquivo.
2.  Atualize o recurso Web JavaScript na solução, quando aplicável.
3.  Publique as personalizações.
4.  Atualize o formulário e teste os cenários desta documentação.
5.  Se o navegador continuar utilizando uma versão anterior do script,
    faça uma atualização forçada da página (por exemplo, `Ctrl + F5`).

Mantenha os nomes lógicos dos campos, identificadores de notificação e
nomes das funções sincronizados entre o código, a configuração do
formulário e esta documentação.

------------------------------------------------------------------------

## 12. Resumo

A biblioteca `Pentare.Caso` complementa o formulário de Ocorrências do
Dynamics 365 com consultas relacionadas ao cliente e verificação de
títulos duplicados.

O controle por contadores protege a interface contra respostas
assíncronas obsoletas. As notificações de formulário comunicam falhas e
possíveis duplicidades sem impedir o salvamento. Os comentários JSDoc
complementam esta documentação ao descrever diretamente as funções,
parâmetros e retornos no código-fonte. A lógica permanece no lado do
cliente e deve ser complementada por validações no servidor quando uma
regra de negócio precisar ser garantida para todas as formas de criação
e atualização de registros.
