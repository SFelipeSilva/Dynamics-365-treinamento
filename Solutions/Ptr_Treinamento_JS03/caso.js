/**
 * @namespace Pentare.Caso
 * @description Contém as funções responsáveis pelo gerenciamento do formulário
 * de Ocorrências (incident) no Dynamics 365.
 *
 * Responsabilidades:
 * - Registrar eventos OnChange dos campos Cliente e Título.
 * - Consultar a quantidade de casos ativos do cliente.
 * - Consultar a data de abertura do último caso ativo.
 * - Verificar a existência de títulos duplicados para o mesmo cliente.
 * - Controlar requisições assíncronas para evitar a atualização de dados
 *   com respostas de consultas anteriores.
 *
 * @property {Object} formContext - Contexto do formulário atualmente em execução.
 * @property {number} contadorTituloAtual - Identificador incremental utilizado
 * para controlar as requisições de verificação de títulos.
 * @property {number} contadorClienteAtual - Identificador incremental utilizado
 * para controlar as requisições de consulta de informações do cliente.
 */
if (typeof (Pentare) == "undefined") { Pentare = {}; }

Pentare.Caso = {
    /**
     * Contexto do formulário atualmente em execução.
     * @type {Object}
     */
    formContext: {},

    /**
     * Contador utilizado para identificar a requisição mais recente
     * de verificação de títulos duplicados.
     * @type {number}
     */
    contadorTituloAtual: 0,

    /**
     * Contador utilizado para identificar a requisição mais recente
     * de consulta de informações do cliente.
     * @type {number}
     */
    contadorClienteAtual: 0,

    /**
     * Evento OnLoad do formulário.
     *
     * Recupera o contexto do formulário e registra os eventos OnChange
     * dos campos Cliente e Título.
     *
     * @param {Object} executionContext - Contexto de execução do evento.
     * @returns {void}
     */
    OnLoad: function (executionContext) {
        Pentare.Caso.formContext = executionContext.getFormContext();
        Pentare.Caso.OnChange();
    },

    /**
     * Registra os manipuladores de eventos OnChange dos campos
     * customerid e title.
     *
     * O campo customerid executa as funções de atualização das informações
     * do cliente e verificação de títulos duplicados.
     *
     * O campo title executa a verificação de títulos duplicados.
     *
     * @returns {void}
     */
    OnChange: function () {
        const formContext = Pentare.Caso.formContext;

        const atributoCliente = formContext.getAttribute("customerid");
        const atributoTitulo = formContext.getAttribute("title");

        if (atributoCliente) {
            formContext.getAttribute("customerid").addOnChange(Pentare.Caso.trataAlteracaoDeCliente);
            formContext.getAttribute("customerid").addOnChange(Pentare.Caso.VerificarTituloExistente);
        }

        if (atributoTitulo) {
            formContext.getAttribute("title").addOnChange(Pentare.Caso.VerificarTituloExistente);
        }
    },

    /**
     * Evento OnSave do formulário.
     *
     * Reservado para implementações de validação executadas durante
     * a tentativa de salvamento da ocorrência.
     *
     * @returns {void}
     */
    OnSave: function () {

    },

    /**
     * Verifica se a requisição de consulta do cliente ainda é válida.
     *
     * Compara o identificador da requisição com o contador atual e verifica
     * se o cliente informado continua selecionado no formulário.
     *
     * Evita que respostas de consultas anteriores atualizem os campos
     * com informações de outro cliente.
     *
     * @param {number} contador - Identificador da requisição executada.
     * @param {string} idCliente - GUID do cliente consultado, sem chaves
     * e convertido para letras minúsculas.
     *
     * @returns {boolean} Retorna true se a requisição ainda for válida;
     * caso contrário, retorna false.
     */
    VerificaClienteAtual: function (contador, idCliente) {
        const formContext = Pentare.Caso.formContext;

        if (contador !== Pentare.Caso.contadorClienteAtual) {
            return false;
        }

        const cliente = formContext.getAttribute("customerid").getValue();

        if (!cliente || cliente.length === 0) {
            return false;
        }

        const idClienteAtual = cliente[0].id.replace(/[{}]/g, "").toLowerCase();

        return idClienteAtual === idCliente;
    },

    /**
     * Manipula a alteração do campo Cliente.
     *
     * Consulta a quantidade de casos ativos e a data de abertura do último
     * caso ativo relacionado ao cliente selecionado.
     *
     * Os campos são limpos antes da consulta e atualizados somente quando
     * a requisição continua válida.
     *
     * Em caso de falha, apresenta uma notificação no formulário.
     *
     * @async
     * @returns {Promise<void>} Promessa resolvida após a conclusão
     * das consultas e atualização dos campos.
     */
    trataAlteracaoDeCliente: async function () {
        const formContext = Pentare.Caso.formContext;

        const atributoCliente = formContext.getAttribute("customerid");
        const atributoCasosAtivos = formContext.getAttribute("ptr_qtdcasosativos");
        const atributoUltimoCaso = formContext.getAttribute("ptr_ultimocasoem");
        const cliente = atributoCliente.getValue();

        const minhaVerificacao = ++Pentare.Caso.contadorClienteAtual;

        if (!cliente || cliente.length === 0) {
            atributoCasosAtivos.setValue(null);
            atributoUltimoCaso.setValue(null);

            formContext.ui.clearFormNotification("erro_consulta_casos");
            formContext.ui.clearFormNotification("erro_verificacao_titulo");

            return;
        }

        var idCliente = cliente[0].id.replace(/[{}]/g, '').toLowerCase();
        var tipoCliente = cliente[0].entityType;

        if (tipoCliente !== "account" && tipoCliente !== "contact") {
            atributoCasosAtivos.setValue(null);
            atributoUltimoCaso.setValue(null);
            return;
        }

        atributoCasosAtivos.setValue(null);
        atributoUltimoCaso.setValue(null);

        try {
            const qtdCasosAtivos = await Pentare.Caso.BuscaQtdCasosAtivos(idCliente, tipoCliente);
            const dataUltimoCaso = await Pentare.Caso.BuscaUltimoCasoAberto(idCliente);

            if (!Pentare.Caso.VerificaClienteAtual(minhaVerificacao, idCliente)) {
                return;
            }

            formContext.ui.clearFormNotification("erro_consulta_casos");

            atributoCasosAtivos.setValue(qtdCasosAtivos);

            atributoUltimoCaso.setValue(dataUltimoCaso ? new Date(dataUltimoCaso) : null);

            console.log(
                `Dados atualizados para o cliente ${tipoCliente}: ${idCliente}`
            );

        } catch (error) {

            if (!Pentare.Caso.VerificaClienteAtual(minhaVerificacao, idCliente)) {
                return;
            }

            console.error("Erro ao buscar casos ativos:", error);
            atributoCasosAtivos.setValue(null);
            atributoUltimoCaso.setValue(null);

            formContext.ui.setFormNotification(
                "Não foi possível verificar os casos. Tente novamente.",
                "WARNING",
                "erro_consulta_casos"
            );
        }
    },

    /**
     * Consulta a quantidade de casos ativos relacionados a um cliente.
     *
     * Utiliza a Dataverse Web API para recuperar a quantidade de registros
     * da tabela incident que possuem o cliente informado e estão ativos.
     *
     * @async
     * @param {string} idCliente - GUID do cliente consultado.
     * @param {string} customerType - Tipo do cliente (account ou contact).
     *
     * @returns {Promise<number>} Quantidade de casos ativos encontrados.
     *
     * @throws {Error} Quando a requisição HTTP retorna uma resposta
     * que indica falha.
     */
    BuscaQtdCasosAtivos: async function (idCliente, customerType) {
        const baseUrl = Xrm.Utility.getGlobalContext().getClientUrl();

        const apiVersion = "v9.2";

        const url =
            `${baseUrl}/api/data/${apiVersion}/incidents` +
            `?$select=incidentid` +
            `&$filter=_customerid_value eq ${idCliente} and statecode eq 0` +
            `&$count=true`;

        const response = await fetch(url, {
            method: "GET",
            headers: {
                "Accept": "application/json",
                "OData-Version": "4.0",
                "OData-MaxVersion": "4.0"
            }
        });

        if (!response.ok) {
            throw new Error(
                `Erro ao consultar casos ativos: ${response.status} ${await response.text()}`
            );
        }

        const data = await response.json();

        return data["@odata.count"] ?? 0;
    },

    /**
     * Consulta a data de criação do último caso ativo de um cliente.
     *
     * Recupera somente o registro mais recente, ordenando os casos
     * pela data de criação em ordem decrescente.
     *
     * @async
     * @param {string} idCliente - GUID do cliente consultado.
     *
     * @returns {Promise<string|null>} Data de criação do último caso
     * encontrado ou null quando não existem casos ativos.
     */
    BuscaUltimoCasoAberto: async function (idCliente) {
        const query =
            `?$select=createdon` +
            `&$filter=_customerid_value eq ${idCliente} and statecode eq 0` +
            `&$orderby=createdon desc` +
            `&$top=1`;

        const resultado = await Xrm.WebApi.retrieveMultipleRecords("incident", query);

        return resultado.entities[0]?.createdon ?? null;
    },

    /**
     * Recupera os títulos dos casos ativos relacionados a um cliente.
     *
     * Percorre todas as páginas retornadas pela Dataverse Web API,
     * utilizando o nextLink para recuperar os registros restantes.
     *
     * @async
     * @param {string} idCliente - GUID do cliente consultado.
     *
     * @returns {Promise<Array<Object>>} Lista de casos contendo
     * os atributos incidentid e title.
     */
    BuscaTitulos: async function (idCliente) {
        let query =
            `?$select=incidentid,title` +
            `&$filter=_customerid_value eq ${idCliente} and statecode eq 0`;

        const casos = [];

        while (query) {
            const resultado = await Xrm.WebApi.retrieveMultipleRecords("incident", query);

            casos.push(...resultado.entities);

            query = resultado.nextLink ?? null;
        }

        return casos;
    },

    /**
     * Verifica se a requisição de verificação de título ainda é válida.
     *
     * Compara o contador da requisição com o contador atual e verifica
     * se o cliente e o título permanecem iguais aos valores utilizados
     * no início da consulta.
     *
     * Evita que respostas de requisições anteriores sejam processadas
     * após alterações nos campos Cliente ou Título.
     *
     * @param {number} contadorVerificacao - Identificador da requisição.
     * @param {string} idCliente - GUID do cliente consultado.
     * @param {string} textoComparacao - Título normalizado utilizado
     * na consulta.
     *
     * @returns {boolean} Retorna true se a requisição ainda for válida;
     * caso contrário, retorna false.
     */
    VerificaRequisicaoAtual: function (contadorVerificacao, idCliente, textoComparacao) {
        const formContext = Pentare.Caso.formContext;

        if (contadorVerificacao !== Pentare.Caso.contadorTituloAtual) {
            return false;
        }

        const clienteAtual = formContext.getAttribute("customerid").getValue();
        const tituloAtual = formContext.getAttribute("title").getValue();

        if (!clienteAtual || clienteAtual.length === 0) {
            return false;
        }

        const idClienteAtual = clienteAtual[0].id.replace(/[{}]/g, "").toLowerCase();

        if (idClienteAtual !== idCliente) {
            return false;
        }

        if (!tituloAtual || tituloAtual.trim().toLowerCase() !== textoComparacao) {
            return false;
        }

        return true;
    },

    /**
     * Verifica se já existe um caso ativo com o mesmo título
     * para o cliente selecionado.
     *
     * Consulta os casos ativos do cliente e compara os títulos
     * desconsiderando espaços nas extremidades e diferenças
     * entre letras maiúsculas e minúsculas.
     *
     * O próprio registro em edição é desconsiderado na comparação.
     *
     * Utiliza um contador incremental para ignorar respostas
     * de requisições anteriores.
     *
     * Quando encontra uma duplicidade, apresenta uma notificação
     * de aviso no formulário, sem impedir o salvamento.
     *
     * @async
     * @returns {Promise<void>} Promessa resolvida após a conclusão
     * da verificação.
     *
     * @throws {Error} Erros de consulta são tratados internamente
     * e apresentados por meio de uma notificação no formulário.
     */
    VerificarTituloExistente: async function () {
        const formContext = Pentare.Caso.formContext;
        const cliente = formContext.getAttribute("customerid").getValue();
        const tituloCasoAtual = formContext.getAttribute("title").getValue();

        if (!tituloCasoAtual) return;

        const minhaVerificacao = ++Pentare.Caso.contadorTituloAtual;

        formContext.ui.clearFormNotification("titulo_duplicado");

        if (!cliente || cliente.length === 0 || !tituloCasoAtual || !tituloCasoAtual.trim()) return;

        const idCliente = cliente[0].id.replace(/[{}]/g, "").toLowerCase();
        const idCasoAtual = (formContext.data.entity.getId() || "").replace(/[{}]/g, "").toLowerCase();
        const tituloNormalizado = tituloCasoAtual.trim().toLowerCase();

        try {
            const listaCasos = await Pentare.Caso.BuscaTitulos(idCliente);

            if (!Pentare.Caso.VerificaRequisicaoAtual(minhaVerificacao, idCliente, tituloNormalizado)) {
                return;
            }

            const existe = listaCasos.some(caso => {
                const idCasoExistente = caso.incidentid.toLowerCase();

                return idCasoExistente !== idCasoAtual &&
                    caso.title &&
                    caso.title.trim().toLowerCase() === tituloNormalizado;
            });

            formContext.ui.clearFormNotification("erro_verificacao_titulo");

            if (existe) {
                console.log("Já existe um caso com esse título para o cliente.");

                formContext.ui.setFormNotification(
                    "Já existe uma Ocorrência com o mesmo título para esse cliente.",
                    "WARNING",
                    "titulo_duplicado"
                );
            }
        } catch (error) {

            if (!Pentare.Caso.VerificaRequisicaoAtual(minhaVerificacao, idCliente, tituloNormalizado)) {
                return;
            }

            console.error("Erro ao verificar o título:", error);

            formContext.ui.setFormNotification(
                "Não foi possível verificar se já existe um caso com esse título. Tente novamente.",
                "WARNING",
                "erro_verificacao_titulo"
            );
        }
    }
};
