# Pentare.Caso — Gerenciamento de Ocorrências

Biblioteca JavaScript desenvolvida para personalizar o formulário de Ocorrências (Case/`incident`) no Microsoft Dynamics 365 Customer Service.

A solução utiliza a Client API do Dynamics 365 e a Dataverse Web API para automatizar consultas relacionadas ao cliente, apresentar informações sobre ocorrências existentes e identificar possíveis títulos duplicados.

## 1. Objetivo

O objetivo da biblioteca é aprimorar a experiência de utilização do formulário de Ocorrências, automatizando consultas e validações durante o preenchimento dos registros.

As principais funcionalidades são:

- Consulta automática da quantidade de ocorrências ativas do cliente selecionado.
- Identificação da data de criação da última ocorrência ativa.
- Verificação de títulos duplicados para o mesmo cliente.
- Desconsideração da própria ocorrência durante a verificação de duplicidade.
- Controle de concorrência entre requisições assíncronas.
- Apresentação de notificações informativas e de advertência no formulário.

## 2. Tecnologias utilizadas

| Tecnologia | Aplicação |
|---|---|
| JavaScript (ES6+) | Implementação da lógica de negócio no formulário. |
| Dynamics 365 Client API | Manipulação de atributos, controles e eventos do formulário. |
| Dataverse Web API | Consulta de registros da tabela `incident`. |
| Fetch API | Requisição HTTP para consulta da quantidade de ocorrências. |
| Xrm.WebApi | Recuperação de registros e paginação de resultados. |
| JSDoc | Documentação técnica das funções e propriedades. |

## 3. Estrutura da biblioteca

A biblioteca utiliza o namespace `Pentare.Caso`, responsável por organizar as funções relacionadas ao formulário de Ocorrências.

```javascript
Pentare.Caso = {
    formContext: {},
    contadorTituloAtual: 0,
    contadorClienteAtual: 0,

    OnLoad: function (executionContext) {},
    OnChange: function () {},
    OnSave: function () {},

    VerificaClienteAtual: function () {},
    trataAlteracaoDeCliente: async function () {},

    BuscaQtdCasosAtivos: async function () {},
    BuscaUltimoCasoAberto: async function () {},
    BuscaTitulos: async function () {},

    VerificaRequisicaoAtual: function () {},
    VerificarTituloExistente: async function () {}
};
```

### 3.1. Propriedades

| Propriedade | Descrição |
|---|---|
| `formContext` | Armazena o contexto do formulário atualmente em execução. |
| `contadorTituloAtual` | Identifica a requisição mais recente de verificação de títulos. |
| `contadorClienteAtual` | Identifica a requisição mais recente de consulta de informações do cliente. |

Os contadores são utilizados para controlar a concorrência entre operações assíncronas, evitando que respostas de requisições antigas sobrescrevam informações mais recentes.

## 4. Funcionalidades

### 4.1. Consulta de informações do cliente

Quando o campo Cliente (`customerid`) é alterado, a biblioteca executa automaticamente duas consultas:

**Quantidade de ocorrências ativas**

A função `BuscaQtdCasosAtivos()` consulta a tabela `incident` por meio da Dataverse Web API.

Critérios utilizados:

- Cliente relacionado ao registro.
- Ocorrências com `statecode = 0` (ativas).

O resultado é apresentado no campo `ptr_qtdcasosativos`.

**Data da última ocorrência ativa**

A função `BuscaUltimoCasoAberto()` recupera a data de criação da ocorrência ativa mais recente.

A consulta utiliza:

- Ordenação por `createdon` em ordem decrescente.
- Limitação de resultados com `$top=1`.

O resultado é apresentado no campo `ptr_ultimocasoem`.

**Comportamento adicional**

- Quando nenhum cliente está selecionado, os campos são limpos.
- São aceitos clientes dos tipos `account` e `contact`.
- Os campos são limpos antes da realização das consultas.
- Em caso de falha, uma notificação é apresentada no formulário.

### 4.2. Verificação de títulos duplicados

A função `VerificarTituloExistente()` verifica se já existe uma ocorrência ativa com o mesmo título para o cliente selecionado.

A verificação é executada quando:

- O campo Cliente (`customerid`) é alterado.
- O campo Título (`title`) é alterado.

**Critérios de comparação**

A comparação dos títulos considera:

- Remoção de espaços em branco no início e no final.
- Conversão dos caracteres para letras minúsculas.
- Comparação de igualdade entre os títulos normalizados.

Exemplo:

| Título original | Resultado |
|---|---|
| `Erro no sistema` | Considerado igual |
| `ERRO NO SISTEMA` | Considerado igual |
| `  Erro no sistema  ` | Considerado igual |
| `Erro no acesso` | Considerado diferente |

A comparação não normaliza espaços internos entre palavras.

**Desconsideração da ocorrência atual**

Durante a edição de uma ocorrência, o identificador do próprio registro é excluído da comparação.

Isso evita que o sistema identifique o título atual como duplicado do próprio registro.

**Paginação**

A função `BuscaTitulos()` utiliza o mecanismo `nextLink` da Dataverse Web API para recuperar todos os registros retornados pela consulta, evitando limitar a verificação apenas à primeira página.

**Notificação**

Quando uma duplicidade é identificada, o formulário apresenta uma notificação do tipo `WARNING`.

A notificação é informativa e não impede o salvamento do registro.

## 5. Controle de concorrência

As consultas são executadas de forma assíncrona. Portanto, uma requisição iniciada anteriormente pode terminar depois de uma requisição mais recente.

Para evitar inconsistências, a biblioteca utiliza contadores incrementais.

### 5.1. Contador de cliente

A propriedade `contadorClienteAtual` controla as consultas relacionadas às informações do cliente.

A função `VerificaClienteAtual()` verifica:

1. Se o contador da requisição corresponde ao contador atual.
2. Se existe um cliente selecionado.
3. Se o identificador do cliente permanece igual ao utilizado na consulta.

### 5.2. Contador de títulos

A propriedade `contadorTituloAtual` controla as consultas de títulos duplicados.

A função `VerificaRequisicaoAtual()` verifica:

1. Se o contador da requisição corresponde ao contador atual.
2. Se existe um cliente selecionado.
3. Se o identificador do cliente permanece igual.
4. Se o título atual permanece igual ao título utilizado na consulta.

Dessa forma, somente respostas de requisições ainda válidas podem atualizar as notificações do formulário.

## 6. Campos Dataverse utilizados

### Tabela: Ocorrência (`incident`)

| Nome lógico | Tipo de informação | Finalidade |
|---|---|---|
| `customerid` | Lookup | Identifica o cliente relacionado à ocorrência. |
| `title` | Texto | Armazena o título da ocorrência. |
| `incidentid` | Identificador | Identifica unicamente a ocorrência. |
| `createdon` | Data e hora | Registra a data de criação da ocorrência. |
| `statecode` | Status | Identifica se a ocorrência está ativa ou inativa. |

### Tabela: Conta (`account`) / Contato (`contact`)

Os registros dessas tabelas são utilizados como clientes relacionados à ocorrência por meio do campo polimórfico `customerid`.

### Campos personalizados da tabela Ocorrência

| Nome lógico | Finalidade |
|---|---|
| `ptr_qtdcasosativos` | Apresenta a quantidade de ocorrências ativas do cliente. |
| `ptr_ultimocasoem` | Apresenta a data de criação da última ocorrência ativa. |

## 7. Notificações do formulário

A biblioteca utiliza notificações para informar o usuário sobre o resultado das consultas.

| Identificador | Tipo | Descrição |
|---|---|---|
| `erro_consulta_casos` | WARNING | Não foi possível consultar as informações do cliente. |
| `erro_verificacao_titulo` | WARNING | Não foi possível verificar a existência de títulos duplicados. |
| `titulo_duplicado` | WARNING | Já existe uma ocorrência com o mesmo título para o cliente. |

As notificações são gerenciadas pelos métodos:

- `formContext.ui.setFormNotification()`
- `formContext.ui.clearFormNotification()`

As notificações são informativas e não bloqueiam o salvamento.

## 8. Configuração no Dynamics 365

### 8.1. Publicação da biblioteca

1. Acesse a solução que contém a tabela Ocorrência.
2. Adicione ou abra o recurso da Web JavaScript correspondente à biblioteca.
3. Publique o recurso da Web.
4. Abra o formulário principal da tabela Ocorrência.

### 8.2. Configuração dos eventos

No editor do formulário, acesse a seção de eventos e configure:

| Evento | Biblioteca | Função | Passar contexto de execução |
|---|---|---|---|
| OnLoad | Biblioteca Pentare.Caso | `Pentare.Caso.OnLoad` | Sim |

A função `OnChange` registra automaticamente os eventos dos campos `customerid` e `title`.

Não é necessário registrar individualmente `trataAlteracaoDeCliente` e `VerificarTituloExistente` no formulário, pois o próprio código realiza esse registro.

A função `OnSave` está reservada para futuras implementações de validação e não possui lógica atualmente.

### 8.3. Publicação do formulário

Após configurar os eventos:

1. Salve as alterações.
2. Publique o formulário.
3. Abra uma ocorrência para testar as funcionalidades.

## 9. Cenários de teste

| Cenário | Resultado esperado |
|---|---|
| Selecionar uma Conta como cliente | Consultar ocorrências ativas e atualizar os campos personalizados. |
| Selecionar um Contato como cliente | Consultar ocorrências ativas e atualizar os campos personalizados. |
| Remover o cliente | Limpar quantidade de ocorrências e data da última ocorrência. |
| Selecionar um cliente sem ocorrências ativas | Apresentar quantidade zero e data vazia. |
| Alterar o cliente durante uma consulta | Ignorar respostas referentes ao cliente anterior. |
| Informar um título já existente para o cliente | Apresentar notificação de duplicidade. |
| Informar um título com letras maiúsculas ou espaços nas extremidades | Identificar a duplicidade quando o título normalizado for igual. |
| Editar uma ocorrência sem alterar seu título | Não identificar o próprio registro como duplicado. |
| Informar um título inexistente | Não apresentar notificação de duplicidade. |
| Ocorrer falha na consulta | Apresentar notificação de advertência. |
| Alterar o título durante uma consulta | Ignorar respostas referentes ao título anterior. |

## 10. Tratamento de erros

As operações assíncronas possuem blocos `try/catch` para tratar falhas durante as consultas.

Quando uma consulta falha:

- O erro é registrado no console por meio de `console.error()`.
- Os campos relacionados à consulta do cliente são limpos quando aplicável.
- Uma notificação é apresentada ao usuário.
- Respostas de requisições obsoletas são ignoradas.

As mensagens de erro são apresentadas de maneira simplificada, evitando expor detalhes técnicos ao usuário final.

## 11. Considerações técnicas

- A biblioteca depende do contexto de execução de um formulário Model-Driven.
- Os campos utilizados precisam estar disponíveis no formulário.
- O usuário precisa possuir as permissões necessárias para consultar os registros envolvidos.
- As consultas consideram somente ocorrências ativas.
- A verificação de títulos duplicados é uma validação realizada no cliente e não representa uma restrição de unicidade no Dataverse.
- A verificação JavaScript não impede duplicidades criadas por integrações, importações ou outras aplicações.
- Os contadores controlam a validade das respostas, mas não cancelam as requisições HTTP em andamento.
- A função `BuscaQtdCasosAtivos()` utiliza a versão `v9.2` da Dataverse Web API.

## 13. Documentação técnica

As funções e propriedades da biblioteca são documentadas utilizando o padrão JSDoc.

Para gerar documentação HTML automaticamente, é possível utilizar o JSDoc com a configuração apropriada do projeto.

Referência: https://jsdoc.app/

---

**Projeto:** Pentare.Caso

**Plataforma:** Microsoft Dynamics 365 / Microsoft Dataverse

**Linguagem:** JavaScript

**Componente:** Biblioteca de scripts do formulário de Ocorrências