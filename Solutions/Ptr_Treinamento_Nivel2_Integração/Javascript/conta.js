if (typeof (Pentare) == "undefined") { Pentare = {}; }

Pentare.Conta = {
    formContext: {},

    /**
     * @function OnLoad
     * @description Inicializa o contexto do formulário e registra
     * os eventos OnChange dos campos.
     * @param {Object} executionContext Contexto de execução do formulário.
     * @returns {void}
     */
    OnLoad: function (executionContext) {
        Pentare.Conta.formContext = executionContext.getFormContext();
        Pentare.Conta.OnChange();
    },

    /**
     * @function OnChange
     * @description Registra os eventos de alteração dos campos
     * que possuem validações específicas.
     * @returns {void}
     */
    OnChange: function () {
        Pentare.Conta.formContext.getAttribute("ptr_cnpj").addOnChange(Pentare.Conta.ValidaCnpj);
    },

    /**
     * @function OnSave
     * @description Atualiza o contexto do formulário e executa
     * a validação do CEP antes do salvamento.
     * @param {Object} executionContext Contexto de execução do formulário.
     * @returns {void}
     */
    OnSave: function (executionContext) {
        Pentare.Conta.formContext = executionContext.getFormContext();

        Pentare.Conta.ValidaCep();
    },

    /**
     * @function ValidaCnpj
     * @description Valida o CNPJ informado no formulário.
     * Remove notificações anteriores e apresenta uma notificação
     * caso o CNPJ não seja válido.
     * @returns {void}
     */
    ValidaCnpj: function () {
        const formContext = Pentare.Conta.formContext;
        const cnpjValor = formContext.getAttribute("ptr_cnpj").getValue();
        const cnpjControl = formContext.getControl("ptr_cnpj");

        if (!cnpjValor || cnpjValor.length == 0) return;
        if (!cnpjControl) return;

        cnpjControl.clearNotification("cnpj_invalido");

        const cnpjValidado = Pentare.Conta.ValidarDigitosVerificadoresCnpj(cnpjValor);

        if (!cnpjValidado){
            cnpjControl.setNotification("Informe um CNPJ válido.", "cnpj_invalido");
            return;
        }
    },

    /**
     * @function ValidaCep
     * @description Verifica se o CEP informado possui exatamente
     * 8 dígitos numéricos, desconsiderando caracteres especiais.
     * Apresenta uma notificação caso o CEP seja inválido.
     * @returns {void}
     */
    ValidaCep: function () {
        const formContext = Pentare.Conta.formContext;

        const cepAttribute = formContext.getAttribute("address1_postalcode");

        if (!cepAttribute) return;

        const cepValor = cepAttribute.getValue();

        formContext.ui.clearFormNotification("cep_invalido");

        if (!cepValor) return;

        const cepNormalizado = cepValor.replace(/\D/g, "");

        if (cepNormalizado.length !== 8) {
            formContext.ui.setFormNotification(
                "CEP inválido. Informe um CEP com 8 dígitos.",
                "WARNING",
                "cep_invalido"
            );
        }
    },

     /**
     * @function ValidarDigitosVerificadoresCnpj
     * @description Valida a estrutura e os dígitos verificadores
     * de um CNPJ, considerando o formato alfanumérico.
     *
     * A função normaliza o valor recebido, verifica o formato,
     * rejeita sequências com caracteres repetidos e calcula
     * os dois dígitos verificadores utilizando o módulo 11.
     *
     * @param {string|null} cnpjInformado CNPJ informado pelo usuário,
     * podendo conter pontuação e caracteres alfanuméricos.
     * @returns {boolean} Retorna true se a estrutura e os dígitos
     * verificadores forem válidos; caso contrário, retorna false.
     */
    ValidarDigitosVerificadoresCnpj: function (cnpjInformado) {

        const cnpj = (cnpjInformado || "").replace(/[^0-9a-zA-Z]/g, "").toUpperCase();

        if (!/^[A-Z0-9]{12}[0-9]{2}$/.test(cnpj)) {
            return false;
        }

        if (/^([A-Z0-9])\1{13}$/.test(cnpj)) {
            return false;
        }

        /**
         * @function converterCaractere
         * @description Converte um caractere alfanumérico em seu
         * valor numérico utilizando o código ASCII menos 48.
         * @param {string} caractere Caractere a ser convertido.
         * @returns {number} Valor numérico correspondente ao caractere.
         */
        function converterCaractere(caractere) {
            return caractere.charCodeAt(0) - 48;
        }

        /**
         * @function calcularDigito
         * @description Calcula um dígito verificador utilizando
         * os pesos definidos e o algoritmo de módulo 11.
         * @param {string} base Sequência de caracteres utilizada no cálculo.
         * @param {number[]} pesos Pesos multiplicadores de cada posição.
         * @returns {number} Dígito verificador calculado.
         */
        function calcularDigito(base, pesos) {

            let soma = 0;

            for (let i = 0; i < base.length; i++) {
                soma += converterCaractere(base[i]) * pesos[i];
            }

            const resto = soma % 11;

            return resto < 2 ? 0 : 11 - resto;
        }

        const basePrimeiroDv = cnpj.substring(0, 12);

        const pesosPrimeiroDv = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        const primeiroDv = calcularDigito(basePrimeiroDv, pesosPrimeiroDv);

        const baseSegundoDv = cnpj.substring(0, 12) + primeiroDv;

        const pesosSegundoDv = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        const segundoDv = calcularDigito(baseSegundoDv, pesosSegundoDv);

        return (
            primeiroDv === Number(cnpj[12]) &&
            segundoDv === Number(cnpj[13])
        );
    }
};